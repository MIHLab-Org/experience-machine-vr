using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>
/// A small world-space "Talk" button that floats above a philosopher (backlog V5, Quest VR).
/// It appears when the player is within Show Radius and is looking roughly at the philosopher,
/// and hides while any dialogue is running. Pressing it starts the conversation through the
/// philosopher's public entry point:
///   PhilosopherNPC.StartTalk()      - stationary philosophers (same as pressing E)
///   PhilosopherWalker.StartWalk()   - walkers: stands up and runs the Join Node
/// so talking never needs a keyboard or a controller.
///
/// Plain Unity UI only (no Meta types), so it compiles before the Meta XR SDK is installed.
/// Hands reach it once Meta's PointableCanvas is on the canvas (V7's menu adds it), or with any
/// other UI raycaster. Something else (a pinch gesture, a voice command) can call Talk() too.
///
/// Setup
///   1. Add this component to the philosopher's root (the object with PhilosopherNPC or
///      PhilosopherWalker). V7's menu "MIHLab > Make scene hand-ready" does this for the
///      selected philosophers.
///   2. Head: leave empty to use Camera.main (the Meta camera rig's CenterEyeAnchor is tagged
///      MainCamera). Set it by hand if the main camera is not the headset camera.
///   3. Leave Canvas / Button empty and the prompt builds its own button at run time (a white
///      label on a dark rounded panel, 0.30 x 0.12 m). To style it yourself, make a World Space
///      Canvas child with a Button and drag both in; Label is then optional.
///   4. Hands: the canvas needs Meta's PointableCanvas (Interaction SDK: add the
///      "PointableCanvasModule" to the EventSystem and a PointableCanvas + RayInteractable /
///      PokeInteractable on the canvas, or run V7's menu). In the Editor, a mouse click works
///      if the scene's EventSystem has an Input System UI Input Module and the canvas a
///      Graphic Raycaster (added automatically when the button is built here).
///   5. Optional: On Talk (UnityEvent) fires after a successful press, e.g. to play a click.
///
/// Notes
///   - The button is sized for reading at 1-3 m. Height Above Head is measured from the
///     philosopher's pivot (feet for the Mixamo bodies).
///   - If Join On Approach is on, a walker starts by himself at Trigger Radius; keep Show Radius
///     larger than that so the button is useful, or turn Join On Approach off for hands-only play.
///   - It never deletes or changes anything on the philosopher.
/// </summary>
[DisallowMultipleComponent]
public class HandTalkPrompt : MonoBehaviour
{
    [Header("Target (found on this object or its parents if empty)")]
    [SerializeField] private PhilosopherNPC npc;
    [SerializeField] private PhilosopherWalker walker;
    [SerializeField] private DialogueRunner dialogueRunner;

    [Header("When to show")]
    [Tooltip("Headset camera. Empty = Camera.main.")]
    [SerializeField] private Transform head;
    [Tooltip("Show the button when the head is this close (metres, flat distance).")]
    [SerializeField] private float showRadius = 4f;
    [Tooltip("Show only when the philosopher is within this many degrees of where the player looks (flat). 180 = always.")]
    [SerializeField, Range(10f, 180f)] private float facingAngle = 50f;

    [Header("Placement")]
    [SerializeField] private float heightAboveHead = 2.15f;
    [Tooltip("Turn the button to face the player every frame (yaw only).")]
    [SerializeField] private bool billboard = true;

    [Header("UI (built at run time if empty)")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Button button;
    [SerializeField] private Text label;
    [Tooltip("{0} = philosopher's name.")]
    [SerializeField] private string labelFormat = "Talk to {0}";
    [SerializeField] private Vector2 sizeMetres = new Vector2(0.30f, 0.12f);

    [Header("Events")]
    [SerializeField] private UnityEvent onTalk = new UnityEvent();

    private const float PixelsPerMetre = 1000f; // canvas units per metre for the built button

    private bool visible;

    /// <summary>True while the button is showing.</summary>
    public bool IsShowing => visible;

    private void Reset()
    {
        npc = GetComponentInParent<PhilosopherNPC>();
        walker = GetComponentInParent<PhilosopherWalker>();
    }

    private void Awake()
    {
        if (npc == null) npc = GetComponentInParent<PhilosopherNPC>();
        if (walker == null) walker = GetComponentInParent<PhilosopherWalker>();
        if (npc == null && walker == null)
        {
            Debug.LogWarning($"[HandTalkPrompt] {name}: no PhilosopherNPC or PhilosopherWalker on this object or its parents.", this);
        }

        if (canvas == null) BuildButton();
        if (button == null && canvas != null) button = canvas.GetComponentInChildren<Button>(true);
        if (button != null) button.onClick.AddListener(Talk);
        if (label == null && button != null) label = button.GetComponentInChildren<Text>(true);
        if (label != null) label.text = string.Format(labelFormat, DisplayName());

        SetVisible(false);
    }

    private void Start()
    {
        if (dialogueRunner == null) dialogueRunner = FindAnyObjectByType<DialogueRunner>();
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Talk);
    }

    private void LateUpdate()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (canvas == null || head == null)
        {
            SetVisible(false);
            return;
        }

        bool show = ShouldShow();
        SetVisible(show);
        if (!show) return;

        Transform c = canvas.transform;
        c.position = transform.position + Vector3.up * heightAboveHead;
        if (billboard)
        {
            Vector3 toHead = head.position - c.position;
            toHead.y = 0f;
            // UI faces -forward, so point forward away from the head.
            if (toHead.sqrMagnitude > 0.0001f) c.rotation = Quaternion.LookRotation(-toHead.normalized, Vector3.up);
        }
    }

    /// <summary>Starts the conversation. Wired to the button; safe to call from a gesture too.</summary>
    public void Talk()
    {
        if (DialogueBusy()) return;

        bool started = false;
        if (walker != null && walker.isActiveAndEnabled) started = walker.StartWalk();
        else if (npc != null && npc.isActiveAndEnabled) started = npc.StartTalk();

        if (started)
        {
            SetVisible(false);
            onTalk.Invoke();
        }
    }

    /// <summary>
    /// Builds the Talk button now if none is assigned, and returns its canvas. Used by V7's
    /// editor menu so the canvas exists in the scene and Meta's "Add Poke/Ray Interaction to
    /// Canvas" quick actions can be applied to it. At run time Awake does the same.
    /// </summary>
    public Canvas EnsureCanvas()
    {
        if (npc == null) npc = GetComponentInParent<PhilosopherNPC>();
        if (walker == null) walker = GetComponentInParent<PhilosopherWalker>();
        if (canvas == null)
        {
            BuildButton();
            canvas.transform.position = transform.position + Vector3.up * heightAboveHead;
        }
        if (button == null) button = canvas.GetComponentInChildren<Button>(true);
        if (label == null && button != null) label = button.GetComponentInChildren<Text>(true);
        if (label != null) label.text = string.Format(labelFormat, DisplayName());
        return canvas;
    }

    // ---------------------------------------------------------------- internals

    private bool ShouldShow()
    {
        if (DialogueBusy()) return false;
        if (walker != null && walker.isActiveAndEnabled)
        {
            if (!walker.CanStartWalk) return false;
        }
        else if (npc != null && npc.isActiveAndEnabled)
        {
            if (!npc.CanTalk) return false;
        }
        else
        {
            return false;
        }

        Vector3 toNpc = transform.position - head.position;
        toNpc.y = 0f;
        if (toNpc.magnitude > showRadius) return false;
        if (facingAngle >= 180f || toNpc.sqrMagnitude < 0.0001f) return true;

        Vector3 look = head.forward;
        look.y = 0f;
        if (look.sqrMagnitude < 0.0001f) return true; // looking straight up or down
        return Vector3.Angle(look, toNpc) <= facingAngle;
    }

    private bool DialogueBusy()
    {
        return dialogueRunner != null && dialogueRunner.IsDialogueRunning;
    }

    private string DisplayName()
    {
        if (npc != null && !string.IsNullOrEmpty(npc.DisplayName)) return npc.DisplayName;
        if (walker != null) return walker.Id;
        return name;
    }

    private void SetVisible(bool show)
    {
        if (canvas == null || visible == show && canvas.gameObject.activeSelf == show) return;
        visible = show;
        canvas.gameObject.SetActive(show);
    }

    /// <summary>Builds a World Space canvas with one Button and a Text label as a child of this object.</summary>
    private void BuildButton()
    {
        var canvasGo = new GameObject("TalkPrompt Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRect = (RectTransform)canvasGo.transform;
        canvasRect.sizeDelta = sizeMetres * PixelsPerMetre;
        canvasRect.localScale = Vector3.one / PixelsPerMetre;
        canvasGo.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

        var buttonGo = new GameObject("Talk Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(canvasGo.transform, false);
        var buttonRect = (RectTransform)buttonGo.transform;
        buttonRect.anchorMin = Vector2.zero;
        buttonRect.anchorMax = Vector2.one;
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        var image = buttonGo.GetComponent<Image>();
        image.color = new Color(0.08f, 0.09f, 0.12f, 0.85f);
        button = buttonGo.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.75f, 0.85f, 1f, 1f);
        colors.pressedColor = new Color(0.55f, 0.7f, 1f, 1f);
        button.colors = colors;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(buttonGo.transform, false);
        var textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 8f);
        textRect.offsetMax = new Vector2(-12f, -8f);

        label = textGo.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 10;
        label.resizeTextMaxSize = 60;
        label.raycastTarget = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, showRadius);
        Gizmos.DrawWireCube(transform.position + Vector3.up * heightAboveHead, new Vector3(sizeMetres.x, sizeMetres.y, 0.01f));
    }
}
