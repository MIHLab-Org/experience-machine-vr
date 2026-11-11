using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>
/// Hand way to stop a walk (backlog V25, hands-only audit gap G4, comfort requirement B15).
///
/// While a philosopher is walking the player somewhere (WalkAlongMover is carrying them, or
/// a carry is waiting because the player pressed Stop), a small world-space button floats low
/// and slightly to the left of the player's view. Pressing it calls WalkAlongMover.Toggle():
/// "Stop" halts the carry, "Walk on" resumes it. It hides during dialogue and when no walk is
/// under way. A stopped carry also resumes by itself after the next conversation
/// (WalkAlongMover's Resume After Dialogue).
///
/// Plain Unity UI only (no Meta types), so it compiles with or without the Meta XR SDK.
///
/// Setup
///   1. Add this to the camera rig root (the object WalkAlongMover moves), so the button is
///      carried along. Mover and Head can stay empty: the first enabled WalkAlongMover and
///      Camera.main (CenterEyeAnchor) are used.
///   2. Right-click the component > Build Button (or call EnsureCanvas()) so the
///      "WalkStop Canvas" exists in the scene before Play.
///   3. Select that canvas and apply Meta's quick action Interaction SDK > Add Ray Interaction
///      to Canvas, so a hand ray and pinch can press it (same as the Talk buttons). It sits
///      about 0.6 m away, so Add Poke Interaction to Canvas works too.
/// </summary>
public class WalkStopButton : MonoBehaviour
{
    private const float PixelsPerMetre = 1000f;

    [SerializeField] private WalkAlongMover mover;
    [SerializeField] private Transform head;
    [SerializeField] private DialogueRunner dialogueRunner;

    [Header("Placement (metres, relative to the head)")]
    [SerializeField] private float distance = 0.6f;
    [SerializeField] private float belowEyes = 0.35f;
    [SerializeField] private float sideways = -0.18f;
    [SerializeField] private Vector2 sizeMetres = new Vector2(0.20f, 0.08f);
    [Tooltip("Re-centre in front of the head once it has turned more than this many degrees.")]
    [SerializeField] private float followAngle = 30f;

    [Header("Labels")]
    [SerializeField] private string stopLabel = "Stop";
    [SerializeField] private string resumeLabel = "Walk on";

    [Header("UI (built automatically if empty)")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Button button;
    [SerializeField] private Text label;

    [Tooltip("Keep the button up this long after the carry pauses (its short question pauses), so it doesn't blink.")]
    [SerializeField] private float holdAfterPause = 3.5f;

    private float anchorYaw;
    private bool placed;
    private float lastMovingTime = -999f;

    private void Awake()
    {
        EnsureCanvas();
        if (button != null) button.onClick.AddListener(OnPressed);
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnPressed);
    }

    private void LateUpdate()
    {
        if (mover == null) mover = FindAnyObjectByType<WalkAlongMover>();
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (dialogueRunner == null) dialogueRunner = FindAnyObjectByType<DialogueRunner>();
        if (mover == null || head == null || canvas == null) { SetVisible(false); return; }

        bool show = ShouldShow();
        SetVisible(show);
        if (!show) { placed = false; return; }

        if (label != null) label.text = mover.IsStopped ? resumeLabel : stopLabel;
        Place();
    }

    /// <summary>Builds the button if it doesn't exist yet. Safe to call in the Editor.</summary>
    [ContextMenu("Build Button")]
    public Canvas EnsureCanvas()
    {
        if (canvas == null)
        {
            Transform existing = transform.Find("WalkStop Canvas");
            if (existing != null) canvas = existing.GetComponent<Canvas>();
        }
        if (canvas == null) BuildButton();
        if (button == null) button = canvas.GetComponentInChildren<Button>(true);
        if (label == null && button != null) label = button.GetComponentInChildren<Text>(true);
        if (label != null && string.IsNullOrEmpty(label.text)) label.text = stopLabel;
        return canvas;
    }

    // ---------------------------------------------------------------- internals

    private bool ShouldShow()
    {
        if (dialogueRunner != null && dialogueRunner.IsDialogueRunning && !WalkingChatter.IsChatting) return false;
        if (mover.IsMoving) { lastMovingTime = Time.time; return true; }
        // Stopped by the player while a walk is still waiting to go on.
        PhilosopherWalker walker = PhilosopherWalker.Active;
        if (mover.IsStopped && walker != null && walker.Goal != null) return true;
        // A short pause in the middle of a walk.
        return Time.time - lastMovingTime < holdAfterPause;
    }

    private void Place()
    {
        Vector3 flat = head.forward;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) flat = Vector3.ProjectOnPlane(head.up, Vector3.up);
        flat.Normalize();
        float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;

        // Lazy follow: stay put until the head has turned well away, then re-centre.
        if (!placed || Mathf.Abs(Mathf.DeltaAngle(anchorYaw, yaw)) > followAngle)
        {
            anchorYaw = yaw;
            placed = true;
        }

        Quaternion facing = Quaternion.Euler(0f, anchorYaw, 0f);
        Vector3 forward = facing * Vector3.forward;
        Vector3 right = facing * Vector3.right;
        Vector3 pos = head.position + forward * distance + right * sideways + Vector3.down * belowEyes;
        Transform t = canvas.transform;
        t.position = pos;
        t.rotation = Quaternion.LookRotation(pos - head.position, Vector3.up);
    }

    private void OnPressed()
    {
        if (mover != null) mover.Toggle();
    }

    private void SetVisible(bool visible)
    {
        if (canvas != null && canvas.gameObject.activeSelf != visible) canvas.gameObject.SetActive(visible);
    }

    private void BuildButton()
    {
        var canvasGo = new GameObject("WalkStop Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRect = (RectTransform)canvasGo.transform;
        canvasRect.sizeDelta = sizeMetres * PixelsPerMetre;
        canvasRect.localScale = Vector3.one / PixelsPerMetre;
        canvasGo.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

        var buttonGo = new GameObject("Stop Button", typeof(RectTransform), typeof(Image), typeof(Button));
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
        label.text = stopLabel;
    }
}
