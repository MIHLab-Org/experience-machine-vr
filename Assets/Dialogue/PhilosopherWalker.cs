using UnityEngine;
using UnityEngine.AI;
using Yarn.Unity;

/// <summary>
/// One reusable walk-and-talk component for every philosopher who walks with the player
/// (backlog W2). It does what DialogueTrigger does for Socrates and the Chalmers figure,
/// but moves with a NavMeshAgent, so the philosopher paths around trees and rocks instead
/// of walking in a straight line. DialogueTrigger and PhilosopherNPC are left untouched;
/// the W4 menu item (MIHLab > Convert selected NPC to Walker) adds this component and
/// copies the old settings across.
///
/// Flow (same as DialogueTrigger):
///   Idle (seated or standing) -> player comes within Trigger Radius -> stands, runs the
///   Join Node -> walks beside the player toward the Destination -> whenever the player
///   stops or falls behind, steps into their line of sight and runs the next Walking Node
///   -> when the player reaches the Destination, walks to the Sit Spot and runs the
///   Arrival Node -> optional Revisit Node when the player leaves and comes back.
///
/// Yarn can redirect a walk at any time with the W3 commands (YarnWalkCommands):
///   <<walk_to Oak_Grove>>, <<stop_walk>>, <<resume_walk>>, <<handoff Leibniz>>.
///
/// Public fields (Inspector):
///   Identity
///     Philosopher Id      Name used by <<handoff>> and stored in $came_from. Blank = GameObject name.
///     Join On Approach    Off = this philosopher only starts walking when another hands the player over.
///   Dialogue
///     Dialogue Runner     The scene's DialogueRunner (found automatically if blank).
///     Call Out Node       Optional; plays once at Call Out Radius while still idle.
///     Join Node           Plays when the player reaches Trigger Radius (and after a handoff).
///     Walking Nodes       Checkpoint questions, one per stop, in order.
///     Arrival Node        Plays after sitting down at the Destination.
///     Revisit Node        Optional; plays when the player walks off after arrival and comes back.
///   Player
///     Player Override     The object that actually moves when the player walks. Blank = tag lookup.
///   Trigger & destination
///     Trigger Radius, Destination, Arrive Distance, Sit Spot (falls back to Destination).
///   Follow
///     Walk Speed          Top speed (m/s). Eases off near the slot beside the player.
///     Stop Distance       How close to the slot counts as "there".
///     Side Offset         Metres to the player's right (negative = left).
///     Forward Offset      Metres ahead of the player.
///     Turn Speed          How quickly he turns to face where he is going.
///     Walk Bool Param     Animator bool set while moving (default IsWalking).
///   Stop when the player lags
///     Player Stopped Speed, Stopped Dwell Time, Fall Behind Distance, Checkpoint Cooldown,
///     Line Of Sight Distance (how far in front of the player he plants himself).
///     With no checkpoint nodes left he simply waits, facing the player, until they catch up.
///   Seated animation
///     Use Seated Animations, IsSeated / StandUp / SitDown parameter names, durations.
///   After a handoff
///     Return Home After Handoff  Walk back to where he started once the player is handed on.
///
/// Requires a baked NavMesh (Window > AI > Navigation, or a NavMeshSurface). Without one he
/// still works, moving in a straight line and snapping to the ground, and logs a warning.
/// </summary>
[DisallowMultipleComponent]
public class PhilosopherWalker : MonoBehaviour
{
    public enum WalkState { Idle, Rising, Greeting, Walking, Checkpoint, Holding, SittingDown, Arrived, HandingOff, Done }

    /// <summary>The walker currently walking with the player; the target of Yarn walk commands.</summary>
    public static PhilosopherWalker Active { get; private set; }

    [Header("Identity")]
    [SerializeField] private string philosopherId = "";
    [SerializeField] private bool joinOnApproach = true;

    [Header("Dialogue")]
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string callOutNode = "";
    [SerializeField] private float callOutRadius = 14f;
    [SerializeField] private string joinNode = "";
    [SerializeField] private string[] walkingNodes = new string[0];
    [SerializeField] private string arrivalNode = "";
    [SerializeField] private string revisitNode = "";

    [Header("Player")]
    [SerializeField] private Transform playerOverride;

    [Header("Trigger & destination")]
    [SerializeField] private float triggerRadius = 3f;
    [SerializeField] private Transform destination;
    [SerializeField] private float arriveDistance = 3f;
    [SerializeField] private Transform sitSpot;

    [Header("Follow")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float stopDistance = 0.35f;
    [SerializeField] private float sideOffset = -1f;
    [SerializeField] private float forwardOffset = 0.2f;
    [SerializeField] private float turnSpeed = 8f;
    [SerializeField] private string walkBoolParam = "IsWalking";

    [Header("Stop when the player lags")]
    [SerializeField] private float playerStoppedSpeed = 0.4f;
    [SerializeField] private float stoppedDwellTime = 0.9f;
    [SerializeField] private float fallBehindDistance = 6f;
    [SerializeField] private float checkpointCooldown = 6f;
    [SerializeField] private float lineOfSightDistance = 2.2f;

    [Header("Seated animation")]
    [SerializeField] private bool useSeatedAnimations = false;
    [SerializeField] private string seatedBoolParam = "IsSeated";
    [SerializeField] private string standUpTriggerParam = "StandUp";
    [SerializeField] private string sitDownTriggerParam = "SitDown";
    [SerializeField] private float standUpDuration = 1.4f;
    [SerializeField] private float sitDownDuration = 1.4f;

    [Header("After a handoff")]
    [SerializeField] private bool returnHomeAfterHandoff = false;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // Tuning that DialogueTrigger hard-coded (W1 audit, item 7); kept as constants here.
    private const float DialogueGrace = 0.3f;
    private const float RevisitMargin = 2f;
    private const float PlayerSpeedSmoothing = 0.35f;
    private const float NavSampleRadius = 2f;

    private Transform player;
    private WalkState state = WalkState.Idle;
    private Vector3 homePosition;

    private Vector3 lastPlayerPos;
    private float playerSpeed;
    private float stoppedTimer;
    private float cooldownTimer;
    private float stateTimer;

    private int checkpointIndex;
    private bool waitingForDialogue;
    private float dialogueGrace;
    private bool hasCalledOut;
    private bool revisitArmed;
    private bool warnedNoNavMesh;
    private bool moving;
    private bool satDown;
    private bool awaitingHandoff;

    // Set by Yarn commands.
    private Transform walkToTarget;
    private string walkToArrivalNode;
    private Transform handoffTarget;
    private string handoffNode;
    private PhilosopherWalker handoffWalker;

    public string Id => string.IsNullOrEmpty(philosopherId) ? gameObject.name : philosopherId;
    public WalkState State => state;
    public string JoinNode => joinNode;

    // Read-only views for WalkAlongMover (V8); no behaviour change.
    /// <summary>Where the walk is heading now (handoff target, walk_to target, or Destination). Null if none.</summary>
    public Transform Goal => CurrentGoal();
    /// <summary>The transform this walker treats as the player.</summary>
    public Transform Player => player;
    public float WalkSpeed => walkSpeed;
    public float ArriveDistance => arriveDistance;
    /// <summary>True while one of this walker's Yarn nodes is running or about to.</summary>
    public bool WaitingForDialogue => waitingForDialogue;

    // ---------------------------------------------------------------- lifecycle

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (dialogueRunner == null) dialogueRunner = FindAnyObjectByType<DialogueRunner>();
    }

    private void Start()
    {
        homePosition = transform.position;
        player = FindPlayerTransform();
        if (player != null) lastPlayerPos = player.position;

        if (dialogueRunner == null)
        {
            Debug.LogError($"[PhilosopherWalker] {name}: no DialogueRunner in the scene.", this);
        }

        if (agent != null)
        {
            // We turn him ourselves so he can face the player while standing still.
            agent.updateRotation = false;
            agent.stoppingDistance = stopDistance;
            agent.autoBraking = true;
        }

        SetBool(walkBoolParam, false);
        if (useSeatedAnimations) SetBool(seatedBoolParam, true);
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        stateTimer += dt;
        if (cooldownTimer > 0f) cooldownTimer -= dt;
        if (dialogueGrace > 0f) dialogueGrace -= dt;

        TrackPlayer(dt);

        if (waitingForDialogue && dialogueGrace <= 0f &&
            (dialogueRunner == null || !dialogueRunner.IsDialogueRunning))
        {
            waitingForDialogue = false;
            OnDialogueFinished();
        }

        moving = false;

        switch (state)
        {
            case WalkState.Idle:        TickIdle();            break;
            case WalkState.Rising:      TickRising(dt);        break;
            case WalkState.Greeting:    Halt(); FaceThePlayer(dt); break;
            case WalkState.Walking:     TickWalking(dt);       break;
            case WalkState.HandingOff:  TickWalking(dt);       break;
            case WalkState.Checkpoint:  TickCheckpoint(dt);    break;
            case WalkState.Holding:     Halt(); FaceThePlayer(dt); break;
            case WalkState.SittingDown: TickSittingDown(dt);   break;
            case WalkState.Arrived:     Halt(); FaceThePlayer(dt); TickArrived(); break;
            case WalkState.Done:        TickDone(dt);          break;
        }

        if (agent != null && UsingAgent) moving |= agent.velocity.sqrMagnitude > 0.04f;
        SetBool(walkBoolParam, moving);
    }

    // ---------------------------------------------------------------- public API (used by YarnWalkCommands)

    /// <summary>True while he is idle and could be asked to start walking (VR Talk button, backlog V5).</summary>
    public bool CanStartWalk =>
        state == WalkState.Idle && !awaitingHandoff && !waitingForDialogue &&
        (dialogueRunner == null || !dialogueRunner.IsDialogueRunning);

    /// <summary>
    /// Starts the walk now, as if the player had walked into Trigger Radius: he stands up and
    /// runs the Join Node. Used by HandTalkPrompt so a poke or ray-pinch can start him even
    /// when Join On Approach is off. Returns false if he is not idle or dialogue is running.
    /// </summary>
    public bool StartWalk()
    {
        if (!CanStartWalk) return false;
        Active = this;
        EnterState(WalkState.Rising);
        if (useSeatedAnimations)
        {
            SetBool(seatedBoolParam, false);
            SetTrigger(standUpTriggerParam);
        }
        return true;
    }

    /// <summary>Walk with the player to a new place. Optional node runs on arrival.</summary>
    public void WalkTo(Transform target, string arrivalNodeOnReach = null)
    {
        if (target == null) return;
        walkToTarget = target;
        walkToArrivalNode = arrivalNodeOnReach;
        handoffTarget = null;
        handoffWalker = null;
        checkpointIndex = walkingNodes != null ? walkingNodes.Length : 0; // no old checkpoints on a new leg
        if (state != WalkState.Greeting) EnterState(WalkState.Walking);
        Active = this;
    }

    /// <summary>Stop and face the player until resume_walk (or another command).</summary>
    public void StopWalk()
    {
        EnterState(WalkState.Holding);
    }

    /// <summary>Carry on toward the current destination.</summary>
    public void ResumeWalk()
    {
        if (state == WalkState.Holding || state == WalkState.Arrived || state == WalkState.Done)
        {
            EnterState(handoffTarget != null ? WalkState.HandingOff : WalkState.Walking);
            cooldownTimer = checkpointCooldown;
            Active = this;
        }
    }

    /// <summary>
    /// Walk the player to another philosopher. On arrival, $came_from is set to this
    /// philosopher's Id and the other one's entry node runs (or, if the other one is a
    /// PhilosopherWalker, it takes over the walk from its Join Node).
    /// </summary>
    public void HandoffTo(Transform target, string entryNode, PhilosopherWalker targetWalker)
    {
        if (target == null) return;
        handoffTarget = target;
        handoffNode = entryNode;
        handoffWalker = targetWalker;
        if (targetWalker != null) targetWalker.awaitingHandoff = true; // don't greet on approach; wait to be handed the player
        walkToTarget = null;
        checkpointIndex = walkingNodes != null ? walkingNodes.Length : 0;
        if (state != WalkState.Greeting) EnterState(WalkState.HandingOff);
        Active = this;
    }

    /// <summary>Called on the receiving walker at the end of a handoff.</summary>
    public void TakeOverFrom(string fromId)
    {
        awaitingHandoff = false;
        SetCameFrom(fromId);
        if (state != WalkState.Idle && state != WalkState.Done && state != WalkState.Arrived)
        {
            Active = this; // already walking with the player; just carry on
            return;
        }
        hasCalledOut = true;
        if (useSeatedAnimations)
        {
            SetBool(seatedBoolParam, false);
            SetTrigger(standUpTriggerParam);
        }
        Active = this;
        EnterState(WalkState.Rising);
    }

    // ---------------------------------------------------------------- states

    private void TickIdle()
    {
        Halt();
        if (!joinOnApproach || awaitingHandoff || player == null || waitingForDialogue) return;

        float d = FlatDistance(transform.position, player.position);

        if (!hasCalledOut && !string.IsNullOrEmpty(callOutNode) && d <= callOutRadius)
        {
            hasCalledOut = true;
            RunNode(callOutNode);
            return;
        }

        if (d <= triggerRadius)
        {
            Active = this;
            EnterState(WalkState.Rising);
            if (useSeatedAnimations)
            {
                SetBool(seatedBoolParam, false);
                SetTrigger(standUpTriggerParam);
            }
        }
    }

    private void TickRising(float dt)
    {
        Halt();
        FaceThePlayer(dt);
        float wait = useSeatedAnimations ? standUpDuration : 0f;
        if (stateTimer >= wait && !waitingForDialogue)
        {
            EnterState(WalkState.Greeting);
            if (!RunNode(joinNode)) OnDialogueFinished(); // no join node: start walking straight away
        }
    }

    private void TickWalking(float dt)
    {
        if (waitingForDialogue) { Halt(); FaceThePlayer(dt); return; }

        Transform goal = CurrentGoal();
        if (player == null || goal == null)
        {
            Halt();
            FaceThePlayer(dt);
            return;
        }

        // Arrived?
        if (FlatDistance(player.position, goal.position) <= arriveDistance)
        {
            OnPlayerReachedGoal();
            return;
        }

        // Stop and pose a question, or wait for the player to catch up.
        bool playerStopped = playerSpeed < playerStoppedSpeed;
        stoppedTimer = playerStopped ? stoppedTimer + dt : 0f;
        bool fellBehind = FlatDistance(transform.position, player.position) > fallBehindDistance;

        if (cooldownTimer <= 0f && HasCheckpointLeft() && (stoppedTimer >= stoppedDwellTime || fellBehind))
        {
            EnterState(WalkState.Checkpoint);
            stoppedTimer = 0f;
            return;
        }

        if (fellBehind)
        {
            // Nothing left to ask: just wait, facing the player, until they catch up.
            Halt();
            FaceThePlayer(dt);
            return;
        }

        // Hold a slot beside the player, easing speed by distance so he matches their pace.
        Vector3 slot = OnNavMesh(SlotBesidePlayer());
        float dist = FlatDistance(transform.position, slot);
        if (dist > stopDistance)
        {
            float speed = Mathf.Min(walkSpeed, walkSpeed * (dist / 2f) + 0.4f);
            MoveTowards(slot, speed, dt);
            FaceDirection(dist > 3f ? slot - transform.position : PlayerFlatForward(), dt);
        }
        else
        {
            Halt();
            FaceDirection(PlayerFlatForward(), dt);
        }
    }

    private void TickCheckpoint(float dt)
    {
        if (player == null) { EnterState(WalkState.Walking); return; }

        Vector3 spot = OnNavMesh(player.position + PlayerFlatForward() * lineOfSightDistance);
        float dist = FlatDistance(transform.position, spot);

        if (!waitingForDialogue && dist > stopDistance + 0.2f)
        {
            MoveTowards(spot, walkSpeed, dt);
            FaceDirection(spot - transform.position, dt);
            return;
        }

        Halt();
        FaceThePlayer(dt);

        if (!waitingForDialogue)
        {
            string node = NextCheckpointNode();
            if (string.IsNullOrEmpty(node) || !RunNode(node))
            {
                EnterState(ResumeState());
                cooldownTimer = checkpointCooldown;
            }
        }
    }

    private void TickSittingDown(float dt)
    {
        Transform target = sitSpot != null ? sitSpot : destination;
        if (target != null)
        {
            Vector3 seat = OnNavMesh(target.position);
            if (FlatDistance(transform.position, seat) > stopDistance + 0.2f)
            {
                MoveTowards(seat, walkSpeed, dt);
                FaceDirection(seat - transform.position, dt);
                stateTimer = 0f;
                satDown = false;
                return;
            }
        }

        Halt();
        FaceThePlayer(dt);
        if (useSeatedAnimations && !satDown)
        {
            satDown = true;
            SetTrigger(sitDownTriggerParam);
            SetBool(seatedBoolParam, true);
        }

        float wait = useSeatedAnimations ? sitDownDuration : 0f;
        if (stateTimer >= wait && !waitingForDialogue)
        {
            EnterState(WalkState.Arrived);
            RunNode(arrivalNode);
        }
    }

    private void TickArrived()
    {
        if (Active == this && !waitingForDialogue) Active = null;
        if (string.IsNullOrEmpty(revisitNode) || player == null || waitingForDialogue) return;

        float d = FlatDistance(transform.position, player.position);
        if (d > triggerRadius + RevisitMargin)
        {
            revisitArmed = true;
        }
        else if (revisitArmed && d <= triggerRadius)
        {
            revisitArmed = false;
            RunNode(revisitNode);
        }
    }

    private void TickDone(float dt)
    {
        if (returnHomeAfterHandoff && FlatDistance(transform.position, homePosition) > stopDistance + 0.2f)
        {
            Vector3 home = OnNavMesh(homePosition);
            MoveTowards(home, walkSpeed * 0.6f, dt);
            FaceDirection(home - transform.position, dt);
            return;
        }
        Halt();
    }

    private void OnPlayerReachedGoal()
    {
        Halt();

        if (state == WalkState.HandingOff && handoffTarget != null)
        {
            Transform target = handoffTarget;
            PhilosopherWalker next = handoffWalker;
            string node = handoffNode;
            handoffTarget = null;
            handoffWalker = null;
            EnterState(WalkState.Done);
            if (Active == this) Active = null;

            if (next != null && next != this)
            {
                next.TakeOverFrom(Id);
            }
            else
            {
                SetCameFrom(Id);
                RunNode(node);
            }
            return;
        }

        if (walkToTarget != null)
        {
            string node = walkToArrivalNode;
            walkToTarget = null;
            walkToArrivalNode = null;
            EnterState(WalkState.Holding);
            if (!string.IsNullOrEmpty(node)) RunNode(node);
            return;
        }

        satDown = false;
        EnterState(WalkState.SittingDown);
    }

    // ---------------------------------------------------------------- dialogue

    private void OnDialogueFinished()
    {
        switch (state)
        {
            case WalkState.Greeting:
            case WalkState.Checkpoint:
                EnterState(ResumeState());
                cooldownTimer = checkpointCooldown;
                stoppedTimer = 0f;
                break;
        }
    }

    private WalkState ResumeState()
    {
        return handoffTarget != null ? WalkState.HandingOff : WalkState.Walking;
    }

    /// <summary>Starts a node and waits for it. Returns false if there was nothing to run.</summary>
    private bool RunNode(string node)
    {
        if (string.IsNullOrEmpty(node) || dialogueRunner == null) return false;

        if (!dialogueRunner.IsDialogueRunning)
        {
            if (dialogueRunner.YarnProject != null &&
                System.Array.IndexOf(dialogueRunner.YarnProject.NodeNames, node) < 0)
            {
                Debug.LogWarning($"[PhilosopherWalker] {name}: Yarn node '{node}' does not exist.", this);
                return false;
            }
            _ = dialogueRunner.StartDialogue(node);
            Active = this; // Yarn walk commands in this node act on this philosopher
        }

        // If something else was already talking, adopt it and wait for it to finish
        // (same reasoning as DialogueTrigger.RunNode).
        waitingForDialogue = true;
        dialogueGrace = DialogueGrace;
        return true;
    }

    private void SetCameFrom(string fromId)
    {
        if (dialogueRunner != null && dialogueRunner.VariableStorage != null && !string.IsNullOrEmpty(fromId))
        {
            dialogueRunner.VariableStorage.SetValue("$came_from", fromId);
        }
    }

    private bool HasCheckpointLeft()
    {
        return walkingNodes != null && checkpointIndex < walkingNodes.Length;
    }

    private string NextCheckpointNode()
    {
        if (!HasCheckpointLeft()) return null;
        return walkingNodes[checkpointIndex++];
    }

    // ---------------------------------------------------------------- movement

    private bool UsingAgent => agent != null && agent.enabled && agent.isOnNavMesh;

    private Transform CurrentGoal()
    {
        if (handoffTarget != null) return handoffTarget;
        if (walkToTarget != null) return walkToTarget;
        return destination;
    }

    private void MoveTowards(Vector3 target, float speed, float dt)
    {
        moving = true;

        if (UsingAgent)
        {
            agent.speed = speed;
            agent.isStopped = false;
            if (!agent.hasPath || (agent.destination - target).sqrMagnitude > 0.04f)
            {
                agent.SetDestination(target);
            }
            return;
        }

        if (!warnedNoNavMesh)
        {
            warnedNoNavMesh = true;
            Debug.LogWarning($"[PhilosopherWalker] {name}: not on a NavMesh, walking in a straight line. Bake a NavMesh for the forest (W5).", this);
        }

        Vector3 step = target - transform.position;
        step.y = 0f;
        float max = speed * dt;
        Vector3 p = transform.position + (step.magnitude > max ? step.normalized * max : step);
        if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 50f, ~0, QueryTriggerInteraction.Ignore))
        {
            p.y = hit.point.y;
        }
        transform.position = p;
    }

    private void Halt()
    {
        if (UsingAgent && agent.hasPath)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private static Vector3 OnNavMesh(Vector3 point)
    {
        return NavMesh.SamplePosition(point, out NavMeshHit hit, NavSampleRadius, NavMesh.AllAreas) ? hit.position : point;
    }

    // ---------------------------------------------------------------- helpers

    private void EnterState(WalkState next)
    {
        state = next;
        stateTimer = 0f;
    }

    private void TrackPlayer(float dt)
    {
        if (player == null || dt <= 0f) return;
        Vector3 delta = player.position - lastPlayerPos;
        delta.y = 0f;
        playerSpeed = Mathf.Lerp(playerSpeed, delta.magnitude / dt, PlayerSpeedSmoothing);
        lastPlayerPos = player.position;
    }

    private Vector3 SlotBesidePlayer()
    {
        Vector3 fwd = PlayerFlatForward();
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
        return player.position + right * sideOffset + fwd * forwardOffset;
    }

    private Vector3 PlayerFlatForward()
    {
        if (player == null) return transform.forward;
        Vector3 f = player.forward;
        f.y = 0f;
        return f.sqrMagnitude < 0.0001f ? transform.forward : f.normalized;
    }

    private void FaceThePlayer(float dt)
    {
        if (player != null) FaceDirection(player.position - transform.position, dt);
    }

    private void FaceDirection(Vector3 dir, float dt)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, dt * turnSpeed);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void SetBool(string param, bool value)
    {
        if (animator != null && !string.IsNullOrEmpty(param)) animator.SetBool(param, value);
    }

    private void SetTrigger(string param)
    {
        if (animator != null && !string.IsNullOrEmpty(param)) animator.SetTrigger(param);
    }

    private Transform FindPlayerTransform()
    {
        if (playerOverride != null) return playerOverride;

        GameObject tagged = GameObject.FindWithTag("Player");
        if (tagged != null) return tagged.transform;

        GameObject xr = GameObject.Find("XR Origin");
        if (xr != null) return xr.transform;

        GameObject cam = GameObject.FindWithTag("MainCamera");
        if (cam != null) return cam.transform;

        Debug.LogWarning($"[PhilosopherWalker] {name}: could not find the player. Assign Player Override.", this);
        return null;
    }

    private void OnGUI()
    {
        if (!showDebug || !Application.isPlaying || !Debug.isDebugBuild || Active != this) return;

        Transform goal = CurrentGoal();
        string text =
            $"{Id}  state {state}\n" +
            $"navmesh {UsingAgent}   moving {moving}\n" +
            $"playerSpeed {playerSpeed:F2}   stoppedFor {stoppedTimer:F1}s\n" +
            $"goal {(goal != null ? goal.name : "<none>")}   checkpoint {checkpointIndex}/{(walkingNodes != null ? walkingNodes.Length : 0)}\n" +
            $"waitingDlg {waitingForDialogue}   cooldown {Mathf.Max(0f, cooldownTimer):F1}s";

        GUI.color = Color.black;
        GUI.Box(new Rect(8, 8, 380, 92), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(16, 14, 372, 84), text);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);

        if (!string.IsNullOrEmpty(callOutNode))
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, callOutRadius);
        }

        if (destination != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(destination.position, arriveDistance);
            Gizmos.DrawLine(transform.position, destination.position);
        }

        if (sitSpot != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(sitSpot.position, Vector3.one * 0.4f);
        }
    }
}
