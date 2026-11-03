using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

/// <summary>
/// Drop-in trigger for every stationary philosopher on the Reality+ map
/// (Descartes, Berkeley, Nagel ...) and for each area's gatekeeper.
///
/// The player walks up, a prompt appears, and pressing the talk key (E on keyboard,
/// the south button on a gamepad) starts this NPC's Yarn node, e.g. "Talk_Descartes"
/// or "Gate_Epistemology". Socrates keeps his own DialogueTrigger; this is for everyone else.
///
/// Like DialogueTrigger, it never starts a conversation while another one is running.
/// </summary>
public class PhilosopherNPC : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] private DialogueRunner dialogueRunner;
    [Tooltip("Yarn node to run, e.g. Talk_Descartes or Gate_Epistemology.")]
    [SerializeField] private string startNode = "Talk_Descartes";
    [Tooltip("Name shown in the on-screen prompt.")]
    [SerializeField] private string displayName = "Descartes";

    [Header("Trigger")]
    [SerializeField] private float talkRadius = 3f;
    [Tooltip("If off, the conversation starts as soon as the player walks into range (once per visit).")]
    [SerializeField] private bool requireKeyPress = true;
    [SerializeField] private Key talkKey = Key.E;
    [SerializeField] private bool showPrompt = true;

    [Header("Behaviour")]
    [SerializeField] private bool facePlayer = true;
    [SerializeField] private float turnSpeed = 6f;

    [Header("Player")]
    [Tooltip("Same object you dragged into Socrates' Player Override. Leave empty to find the object tagged Player.")]
    [SerializeField] private Transform playerOverride;

    private Transform player;
    private bool inRange;
    private bool firedThisVisit;
    private bool talking;

    private void Start()
    {
        if (dialogueRunner == null)
        {
            dialogueRunner = FindAnyObjectByType<DialogueRunner>();
        }
        if (dialogueRunner == null)
        {
            Debug.LogError($"[PhilosopherNPC] {name}: no DialogueRunner in the scene.", this);
        }

        player = playerOverride;
        if (player == null)
        {
            GameObject tagged = GameObject.FindWithTag("Player");
            if (tagged != null) player = tagged.transform;
        }
        if (player == null)
        {
            Debug.LogWarning($"[PhilosopherNPC] {name}: no player found. Assign Player Override.", this);
        }
    }

    private void Update()
    {
        if (player == null || dialogueRunner == null) return;

        Vector3 a = transform.position; a.y = 0f;
        Vector3 b = player.position;    b.y = 0f;
        float d = Vector3.Distance(a, b);

        bool wasInRange = inRange;
        inRange = d <= talkRadius;
        if (!inRange && wasInRange) firedThisVisit = false;

        bool runnerBusy = dialogueRunner.IsDialogueRunning;
        if (talking && !runnerBusy) talking = false;

        if (inRange && facePlayer)
        {
            Vector3 dir = b - a;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, want, Time.deltaTime * turnSpeed);
            }
        }

        if (!inRange || runnerBusy || string.IsNullOrEmpty(startNode)) return;

        bool wantsToTalk = requireKeyPress ? TalkPressed() : !firedThisVisit;
        if (!wantsToTalk) return;

        firedThisVisit = true;
        talking = true;
        dialogueRunner.StartDialogue(startNode);
    }

    // ---------------------------------------------------------------- public API (VR Talk button, backlog V5)

    /// <summary>Name shown on prompts.</summary>
    public string DisplayName => displayName;

    /// <summary>True when a conversation could start now (no other dialogue running).</summary>
    public bool CanTalk => dialogueRunner != null && !dialogueRunner.IsDialogueRunning && !string.IsNullOrEmpty(startNode);

    /// <summary>
    /// Starts this philosopher's node, exactly as pressing the talk key does. Used by
    /// HandTalkPrompt (poke / ray-pinch) so talking never needs a keyboard. Returns false
    /// if another conversation is running.
    /// </summary>
    public bool StartTalk()
    {
        if (!CanTalk) return false;
        firedThisVisit = true;
        talking = true;
        dialogueRunner.StartDialogue(startNode);
        return true;
    }

    private bool TalkPressed()
    {
        if (Keyboard.current != null && Keyboard.current[talkKey].wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
        return false;
    }

    private void OnGUI()
    {
        if (!showPrompt || !requireKeyPress || !inRange || talking) return;
        if (dialogueRunner != null && dialogueRunner.IsDialogueRunning) return;

        string text = $"Press {talkKey} to talk to {displayName}";
        var style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        float w = 360f, h = 40f;
        GUI.Box(new Rect((Screen.width - w) / 2f, Screen.height - h - 40f, w, h), text, style);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, talkRadius);
    }
}
