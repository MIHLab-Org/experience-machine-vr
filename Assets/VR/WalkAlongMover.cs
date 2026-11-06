using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>
/// Auto-follow for walks (backlog V8, Jim's V4 choice). While a philosopher walks with the
/// player (PhilosopherWalker.Active, state Walking or HandingOff), this carries the player's
/// camera-rig root toward where the walk is heading, at the philosopher's walking pace. The
/// philosopher keeps his slot beside the player, so the two walk together. The player's view
/// is never rotated: they look around freely while carried.
///
///   - Pauses while any dialogue runs, and every Pause Every seconds for Pause For seconds so
///     the philosopher can ask his next Walking Node question (PhilosopherWalker asks when the
///     player stands still).
///   - Stops at the philosopher's Arrive Distance from the goal, so his Arrival Node or the
///     handoff fires as before.
///   - Paths around trees on the NavMesh when one is baked; otherwise a straight line with the
///     ground found by a ray from above (terrain colliders are one-sided).
///   - Comfort vignette: darkens the edge of view while moving (toggle with Vignette).
///   - Stop() halts the carry (for a pinch gesture or a button), Resume() continues, Toggle()
///     flips. By default a stopped carry resumes after the next conversation ends.
///   - WalkToPoint(position) walks the player to any spot with the same motion and vignette,
///     for V15's wrist Walk button. It stops on arrival or on Stop().
///
/// Plain Unity only (no Meta types), so it compiles with or without the Meta XR SDK.
///
/// Setup
///   1. Add this to any object in the scene (e.g. the camera rig root, or the Dialogue System).
///   2. Rig: the object to move. For Meta's camera rig, drag in the OVRCameraRig root (the
///      parent of TrackingSpace), and set each PhilosopherWalker's Player Override to the same
///      object so they agree on where the player is. Empty = the active walker's player.
///   3. Head: leave empty to use Camera.main (CenterEyeAnchor).
///   4. If the rig root has a CharacterController (Meta's player controller does), it is moved
///      with CharacterController.Move so collisions still apply.
///   5. Turn off any other locomotion (thumbstick movement) in VR_Slice; hands-only play has none.
///   6. A pinch or button calls Stop() / Toggle(); V7's menu or Meta's Building Blocks wire that.
/// </summary>
[DisallowMultipleComponent]
public class WalkAlongMover : MonoBehaviour
{
    [Header("References (found automatically if empty)")]
    [Tooltip("Object to move: the camera rig root. Empty = the active walker's player transform.")]
    [SerializeField] private Transform rig;
    [Tooltip("Headset camera, for the vignette. Empty = Camera.main.")]
    [SerializeField] private Camera head;
    [SerializeField] private DialogueRunner dialogueRunner;

    [Header("Auto-follow")]
    [SerializeField] private bool autoFollow = true;
    [Tooltip("Fraction of the philosopher's walk speed. Below 1 lets him keep up easily.")]
    [SerializeField, Range(0.3f, 1f)] private float speedFactor = 0.6f;
    [Tooltip("Top speed in m/s, whatever the walker's speed (comfort).")]
    [SerializeField] private float maxSpeed = 1.4f;
    [Tooltip("Pause for a checkpoint question every this many seconds of carrying. 0 = never.")]
    [SerializeField] private float pauseEvery = 15f;
    [Tooltip("Pause length, seconds. Keep it longer than the walkers' Stopped Dwell Time (0.9 s).")]
    [SerializeField] private float pauseFor = 2.5f;
    [Tooltip("After Stop(), carry on by itself when the next conversation ends.")]
    [SerializeField] private bool resumeAfterDialogue = true;

    [Header("Point walking (V15)")]
    [SerializeField] private float pointWalkSpeed = 1.3f;
    [SerializeField] private float pointArriveDistance = 0.4f;

    [Header("Comfort vignette")]
    [SerializeField] private bool vignette = true;
    [SerializeField, Range(0f, 1f)] private float vignetteStrength = 0.75f;
    [Tooltip("Seconds to fade in or out.")]
    [SerializeField] private float vignetteFade = 0.25f;

    [Header("Events")]
    public UnityEvent onStartMoving;
    public UnityEvent onStopMoving;

    private const float AccelTime = 0.4f;       // seconds to reach full speed (ease in/out)
    private const float RepathInterval = 0.5f;
    private const float NavSampleRadius = 2f;

    private bool stopped;
    private bool pointWalking;
    private Vector3 pointTarget;
    private float carryTimer;
    private float pauseTimer;
    private float currentSpeed;
    private bool wasMoving;
    private bool dialogueWasRunning;

    private NavMeshPath path;
    private int corner;
    private float repathTimer;
    private Vector3 pathGoal;

    private Image vignetteImage;
    private float vignetteAlpha;

    /// <summary>True while the player is being carried this frame.</summary>
    public bool IsMoving => wasMoving;
    public bool IsStopped => stopped;
    public bool VignetteEnabled { get => vignette; set => vignette = value; }

    // ---------------------------------------------------------------- public API

    /// <summary>Halt the carry now (pinch or button). Cancels a point walk.</summary>
    public void Stop()
    {
        stopped = true;
        pointWalking = false;
    }

    /// <summary>Carry on after Stop().</summary>
    public void Resume()
    {
        stopped = false;
        carryTimer = 0f;
    }

    /// <summary>Stop if carrying or about to, otherwise resume.</summary>
    public void Toggle()
    {
        if (stopped) Resume();
        else Stop();
    }

    /// <summary>Walk the player to a spot (V15). Snapped to the NavMesh or ground.</summary>
    public void WalkToPoint(Vector3 position)
    {
        pointTarget = Ground(position, ResolveRig());
        pointWalking = true;
        stopped = false;
        path = null;
    }

    public void ToggleVignette() { vignette = !vignette; }

    // ---------------------------------------------------------------- lifecycle

    private void Start()
    {
        if (dialogueRunner == null) dialogueRunner = FindAnyObjectByType<DialogueRunner>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        bool dialogue = dialogueRunner != null && dialogueRunner.IsDialogueRunning;
        if (dialogueWasRunning && !dialogue && resumeAfterDialogue && stopped && !pointWalking) Resume();
        dialogueWasRunning = dialogue;

        Transform mover = ResolveRig();
        Vector3? target = null;
        float speed = 0f;
        float arrive = pointArriveDistance;

        if (mover != null && !stopped && !dialogue)
        {
            if (pointWalking)
            {
                target = pointTarget;
                speed = pointWalkSpeed;
            }
            else if (autoFollow && TryWalkGoal(out Vector3 goal, out float walkerSpeed, out float arriveDistance))
            {
                // Short pause now and then so the philosopher can ask his next question.
                if (pauseTimer > 0f)
                {
                    pauseTimer -= dt;
                }
                else
                {
                    target = goal;
                    speed = Mathf.Min(maxSpeed, walkerSpeed * speedFactor);
                    arrive = arriveDistance * 0.8f;
                }
            }
        }

        bool moving = false;
        if (target.HasValue && FlatDistance(mover.position, target.Value) > arrive)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, speed, speed / AccelTime * dt);
            moving = Step(mover, target.Value, currentSpeed * dt, dt);
            if (moving && !pointWalking)
            {
                carryTimer += dt;
                if (pauseEvery > 0f && carryTimer >= pauseEvery)
                {
                    carryTimer = 0f;
                    pauseTimer = pauseFor;
                }
            }
        }
        else
        {
            currentSpeed = 0f;
            if (pointWalking && target.HasValue) pointWalking = false; // arrived
        }

        if (moving != wasMoving)
        {
            wasMoving = moving;
            if (moving) onStartMoving?.Invoke();
            else onStopMoving?.Invoke();
        }

        UpdateVignette(dt);
    }

    // ---------------------------------------------------------------- walking

    private bool TryWalkGoal(out Vector3 goal, out float walkerSpeed, out float arriveDistance)
    {
        goal = default;
        walkerSpeed = 0f;
        arriveDistance = 0f;
        PhilosopherWalker w = PhilosopherWalker.Active;
        if (w == null || !w.isActiveAndEnabled || w.WaitingForDialogue) return false;
        if (w.State != PhilosopherWalker.WalkState.Walking && w.State != PhilosopherWalker.WalkState.HandingOff) return false;
        Transform g = w.Goal;
        if (g == null) return false;
        goal = g.position;
        walkerSpeed = w.WalkSpeed;
        arriveDistance = w.ArriveDistance;
        return true;
    }

    /// <summary>Moves one step along the NavMesh path (or straight) toward target. Returns true if it moved.</summary>
    private bool Step(Transform mover, Vector3 target, float distance, float dt)
    {
        if (distance <= 0f) return true; // easing in; counts as moving

        Vector3 next = NextWaypoint(mover.position, target, dt);
        Vector3 delta = next - mover.position;
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.0001f) return false;
        Vector3 step = Vector3.ClampMagnitude(delta, distance);

        Vector3 newPos = Ground(mover.position + step, mover);
        Vector3 move = newPos - mover.position;

        if (mover.TryGetComponent(out CharacterController cc) && cc.enabled)
        {
            cc.Move(move);
        }
        else
        {
            mover.position = newPos;
        }
        return true;
    }

    private Vector3 NextWaypoint(Vector3 from, Vector3 target, float dt)
    {
        repathTimer -= dt;
        if (path == null || repathTimer <= 0f || (target - pathGoal).sqrMagnitude > 1f)
        {
            repathTimer = RepathInterval;
            pathGoal = target;
            corner = 1;
            if (path == null) path = new NavMeshPath();
            if (!(NavMesh.SamplePosition(from, out NavMeshHit a, NavSampleRadius, NavMesh.AllAreas) &&
                  NavMesh.SamplePosition(target, out NavMeshHit b, NavSampleRadius * 2f, NavMesh.AllAreas) &&
                  NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) &&
                  path.status != NavMeshPathStatus.PathInvalid))
            {
                path.ClearCorners();
            }
        }

        if (path.corners.Length < 2) return target; // no NavMesh: straight line

        while (corner < path.corners.Length - 1 && FlatDistance(from, path.corners[corner]) < 0.3f) corner++;
        return path.corners[Mathf.Min(corner, path.corners.Length - 1)];
    }

    /// <summary>Snaps a point to the NavMesh, or to the ground below it (ignoring the rig's own colliders).</summary>
    private static Vector3 Ground(Vector3 p, Transform ignore)
    {
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, NavSampleRadius, NavMesh.AllAreas)) return hit.position;
        // Ray from above: terrain colliders only block from their upper side.
        RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * 50f, Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 result = p;
        foreach (RaycastHit rh in hits)
        {
            if (ignore != null && rh.collider.transform.IsChildOf(ignore)) continue;
            if (rh.collider.GetComponentInParent<PhilosopherWalker>() != null) continue;
            if (rh.distance < best) { best = rh.distance; result = rh.point; }
        }
        return result;
    }

    private Transform ResolveRig()
    {
        if (rig != null) return rig;
        PhilosopherWalker w = PhilosopherWalker.Active;
        return w != null ? w.Player : null;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ---------------------------------------------------------------- vignette

    private void UpdateVignette(float dt)
    {
        float goal = vignette && wasMoving ? vignetteStrength : 0f;
        float rate = vignetteFade > 0f ? dt / vignetteFade : 1f;
        vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, goal, rate);

        if (vignetteAlpha <= 0.001f)
        {
            if (vignetteImage != null) vignetteImage.enabled = false;
            return;
        }
        if (vignetteImage == null && !BuildVignette()) return;
        vignetteImage.enabled = true;
        vignetteImage.color = new Color(0f, 0f, 0f, vignetteAlpha);
    }

    /// <summary>
    /// A Screen Space - Camera canvas just in front of the headset camera with a radial
    /// black-edged image (UI/Default shader, so nothing extra has to be included in builds).
    /// </summary>
    private bool BuildVignette()
    {
        if (head == null) head = Camera.main;
        if (head == null) return false;

        var go = new GameObject("WalkAlong Vignette", typeof(RectTransform), typeof(Canvas));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = head;
        canvas.planeDistance = head.nearClipPlane + 0.05f;
        canvas.sortingOrder = 32000;

        var imgGo = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(go.transform, false);
        var r = (RectTransform)imgGo.transform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;

        vignetteImage = imgGo.GetComponent<Image>();
        vignetteImage.sprite = MakeVignetteSprite(128);
        vignetteImage.raycastTarget = false;
        vignetteImage.enabled = false;
        return true;
    }

    private static Sprite MakeVignetteSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);          // 0 centre, 1 edge midpoints
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.0f, d));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));     // tinted by Image.color
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
