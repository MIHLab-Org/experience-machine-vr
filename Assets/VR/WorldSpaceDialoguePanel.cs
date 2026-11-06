using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>
/// Keeps the Yarn dialogue canvas in world space in front of the player's head, and makes
/// every step of a conversation pressable by hand (backlog V6, Quest VR).
///
///   - Turns the canvas it sits on into a World Space canvas about Distance (1.2 m) in front
///     of the head, sized to Panel Width (0.9 m) so lines are readable.
///   - Follows lazily: it stays put until the head turns more than Recenter Angle (35 deg)
///     away from it, or the player moves more than Recenter Distance from the spot it expects,
///     then glides back in front. It snaps straight in front whenever a conversation starts.
///   - Always turns to face the player (yaw only, so it never tilts).
///   - Adds a big Continue button while a line is showing (calls DialogueRunner.RequestNextLine,
///     the same call Yarn's own LinePresenterButtonHandler makes), so lines never need F.
///   - Options are already buttons: Yarn Spinner 3's OptionItem is a Selectable with
///     IPointerClickHandler, so a poke or ray-pinch selects it. This script only makes each
///     option at least Min Option Height tall so it is easy to hit.
///
/// Plain Unity UI only (no Meta types), so it compiles before the Meta XR SDK is installed.
/// Hands reach the canvas once Meta's PointableCanvas is on it (V7's menu adds it).
///
/// Checked against Yarn Spinner for Unity 3.2.8 (package hash bfc5b6a), Runtime/Views:
///   LinePresenter, OptionsPresenter, OptionItem, LinePresenterButtonHandler, LineAdvancer.
///
/// Setup
///   1. Add this component to the Canvas that holds the Yarn presenters (the "Dialogue System"
///      prefab's Canvas: it has LinePresenter and OptionsPresenter children). V7's menu
///      "MIHLab > Make scene hand-ready" does this. Leave Convert To World Space on; it sets
///      Render Mode = World Space at run time (the scene file is not changed). The Canvas
///      Scaler is disabled at run time because it does nothing in World Space.
///   2. Head: leave empty to use Camera.main (the Meta rig's CenterEyeAnchor is MainCamera).
///   3. Dialogue Runner: leave empty to find the one in the scene.
///   4. Continue Button: leave empty and one is built (0.24 x 0.09 m, bottom right of the
///      panel). To style your own, add a Button to the canvas and drag it in.
///
/// Yarn presenter settings to change (in the Dialogue System prefab / scene, by hand or V7)
///   - LinePresenter: Auto Advance OFF (the Continue button advances). Use Fade Effect can stay
///     on. Typewriter is fine; pressing Continue mid-line skips to the next line.
///   - LinePresenter: do not ALSO add a LinePresenterButtonHandler to its Event Handlers list,
///     or one press will advance two lines. (If you prefer Yarn's handler, set Build Continue
///     Button off and point the handler at your own button instead.)
///   - OptionsPresenter: Option View Prefab stays as is. Show Unavailable Options off (greyed
///     options look pressable in VR). Shows Last Line ON is recommended, so the question stays
///     visible above the choices.
///   - LineAdvancer (if the Dialogue System has one): it can stay for desktop play; set its
///     Input Mode to None (or remove the key bindings) in the VR scene so stray input does
///     not skip lines.
///   - EventSystem: in VR it needs Meta's PointableCanvasModule (V7). Mouse clicks in the
///     Editor need an Input System UI Input Module, as before.
///
/// It never deletes anything; it only changes the canvas render mode, scale and position at
/// run time, and adds the Continue button and LayoutElements on option items.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public class WorldSpaceDialoguePanel : MonoBehaviour
{
    [Header("References (found automatically if empty)")]
    [Tooltip("Headset camera. Empty = Camera.main.")]
    [SerializeField] private Transform head;
    [SerializeField] private DialogueRunner dialogueRunner;

    [Header("Panel")]
    [Tooltip("Set Render Mode to World Space at run time.")]
    [SerializeField] private bool convertToWorldSpace = true;
    [Tooltip("Width of the whole canvas in metres. Height follows the canvas aspect.")]
    [SerializeField] private float panelWidth = 0.9f;
    [Tooltip("Distance in front of the head, metres.")]
    [SerializeField] private float distance = 1.2f;
    [Tooltip("Height relative to the eyes, metres (negative = a little below eye level, easier to read and poke).")]
    [SerializeField] private float heightOffset = -0.15f;

    [Header("Lazy follow")]
    [Tooltip("Re-centre when the panel is more than this many degrees from where the player looks (flat).")]
    [SerializeField, Range(5f, 90f)] private float recenterAngle = 35f;
    [Tooltip("Re-centre when the head is this far (metres) from where the panel expects it, e.g. during a walk.")]
    [SerializeField] private float recenterDistance = 0.6f;
    [Tooltip("How quickly the panel glides back (higher = faster).")]
    [SerializeField] private float followSpeed = 4f;

    [Header("Hand buttons")]
    [SerializeField] private bool buildContinueButton = true;
    [SerializeField] private Button continueButton;
    [Tooltip("Minimum height of each option button, in canvas pixels.")]
    [SerializeField] private float minOptionHeight = 110f;

    [Header("Events")]
    [Tooltip("Fires when Continue is pressed, e.g. to play a click.")]
    public UnityEvent onContinue;

    private Canvas canvas;
    private RectTransform rect;
    private bool wasRunning;
    private bool moving;
    private Vector3 targetPosition;
    private Vector3 anchorHeadPosition;
    private float optionScanTimer;
    private bool optionsVisible;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        rect = GetComponent<RectTransform>();
        if (dialogueRunner == null) dialogueRunner = FindFirstObjectByType<DialogueRunner>();
        if (convertToWorldSpace) MakeWorldSpace();
        if (continueButton == null && buildContinueButton) continueButton = BuildContinueButton();
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(Continue);
            continueButton.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
    }

    /// <summary>Advances to the next line (same as Yarn's continue button). Safe to call any time.</summary>
    public void Continue()
    {
        if (dialogueRunner == null || !dialogueRunner.IsDialogueRunning) return;
        dialogueRunner.RequestNextLine();
        onContinue?.Invoke();
    }

    /// <summary>Puts the panel straight in front of the head now (e.g. from a recenter gesture).</summary>
    public void SnapInFront()
    {
        if (!ResolveHead()) return;
        targetPosition = DesiredPosition();
        anchorHeadPosition = head.position;
        transform.position = targetPosition;
        FaceHead();
        moving = false;
    }

    private void LateUpdate()
    {
        bool running = dialogueRunner != null && dialogueRunner.IsDialogueRunning;
        if (running && !wasRunning) SnapInFront();
        wasRunning = running;

        if (running) UpdateOptions();
        else optionsVisible = false;

        if (continueButton != null)
        {
            bool show = running && !optionsVisible;
            if (continueButton.gameObject.activeSelf != show) continueButton.gameObject.SetActive(show);
        }

        if (canvas.renderMode == RenderMode.WorldSpace) Follow();
    }

    private void Follow()
    {
        if (!ResolveHead()) return;

        Vector3 flatForward = FlatForward();
        Vector3 toPanel = transform.position - head.position;
        toPanel.y = 0f;
        float angle = toPanel.sqrMagnitude > 0.0001f ? Vector3.Angle(flatForward, toPanel) : 180f;
        Vector3 headMoved = head.position - anchorHeadPosition;
        headMoved.y = 0f;

        if (!moving && (angle > recenterAngle || headMoved.magnitude > recenterDistance))
            moving = true;

        if (moving)
        {
            targetPosition = DesiredPosition();
            anchorHeadPosition = head.position;
            transform.position = Vector3.Lerp(transform.position, targetPosition, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            if ((transform.position - targetPosition).sqrMagnitude < 0.0004f) moving = false; // within 2 cm
        }

        FaceHead();
    }

    private Vector3 DesiredPosition()
    {
        return head.position + FlatForward() * distance + Vector3.up * heightOffset;
    }

    private Vector3 FlatForward()
    {
        Vector3 f = head.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) { f = head.up; f.y = 0f; } // looking straight down/up
        return f.sqrMagnitude < 0.0001f ? Vector3.forward : f.normalized;
    }

    /// <summary>World Space UI is read from the side its forward points away from, so look from the head through the panel.</summary>
    private void FaceHead()
    {
        Vector3 away = transform.position - head.position;
        away.y = 0f;
        if (away.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
    }

    private bool ResolveHead()
    {
        if (head != null) return true;
        var cam = Camera.main;
        if (cam != null) head = cam.transform;
        return head != null;
    }

    private void MakeWorldSpace()
    {
        // Size is read before switching: in Screen Space the rect is the screen size.
        Vector2 size = rect.rect.size;
        if (size.x < 1f || size.y < 1f) size = new Vector2(1600f, 900f);

        var scaler = GetComponent<CanvasScaler>();
        if (scaler != null) scaler.enabled = false;

        canvas.renderMode = RenderMode.WorldSpace;
        if (ResolveHead() && head.TryGetComponent(out Camera cam)) canvas.worldCamera = cam;

        rect.sizeDelta = size;
        rect.pivot = new Vector2(0.5f, 0.5f);
        float scale = panelWidth / size.x;
        transform.localScale = new Vector3(scale, scale, scale);

        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        if (ResolveHead()) SnapInFront();
    }

    /// <summary>Notes whether any option button is showing and gives each a comfortable height.</summary>
    private void UpdateOptions()
    {
        optionScanTimer -= Time.unscaledDeltaTime;
        if (optionScanTimer > 0f) return;
        optionScanTimer = 0.15f;

        optionsVisible = false;
        var items = GetComponentsInChildren<OptionItem>(false);
        foreach (var item in items)
        {
            if (!item.isActiveAndEnabled) continue;
            optionsVisible = true;
            var le = item.GetComponent<LayoutElement>();
            if (le == null) le = item.gameObject.AddComponent<LayoutElement>();
            if (le.minHeight < minOptionHeight) le.minHeight = minOptionHeight;
        }
    }

    /// <summary>The RectTransform of Yarn's LinePresenter (found by type name), or null.</summary>
    private RectTransform FindLineBox()
    {
        foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb != null && mb.GetType().Name == "LinePresenter") return mb.transform as RectTransform;
        }
        return null;
    }

    /// <summary>Builds a large Continue button just below the line box (or at the bottom right of the canvas).</summary>
    private Button BuildContinueButton()
    {
        var go = new GameObject("VR Continue Button", typeof(RectTransform), typeof(Image), typeof(Button));

        // 0.24 x 0.09 m whatever the canvas pixel size is.
        float pxPerMetre = rect.rect.width > 1f ? rect.rect.width / Mathf.Max(0.1f, panelWidth) : 1600f / 0.9f;
        var r = go.GetComponent<RectTransform>();
        r.sizeDelta = new Vector2(0.24f, 0.09f) * pxPerMetre;

        // Hang it just below the line box (Yarn's LinePresenter), centred, so it never covers the
        // text. Falls back to the bottom right of the canvas if there is no LinePresenter.
        RectTransform lineBox = FindLineBox();
        if (lineBox != null)
        {
            go.transform.SetParent(lineBox, false);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 1f);
            // The line box may be scaled differently from the canvas; keep the button's size in metres.
            float s = lineBox.lossyScale.x > 0f && transform.lossyScale.x > 0f ? transform.lossyScale.x / lineBox.lossyScale.x : 1f;
            r.sizeDelta *= s;
            r.anchoredPosition = new Vector2(0f, -0.02f * pxPerMetre * s);
        }
        else
        {
            go.transform.SetParent(transform, false);
            r.anchorMin = r.anchorMax = new Vector2(1f, 0f);
            r.pivot = new Vector2(1f, 0f);
            r.anchoredPosition = new Vector2(-0.03f, 0.03f) * pxPerMetre;
        }
        go.transform.SetAsLastSibling();
        go.AddComponent<LayoutElement>().ignoreLayout = true; // in case the line box uses a layout group

        var img = go.GetComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.14f, 0.92f);

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.85f, 0.85f, 1f, 1f);
        colors.pressedColor = new Color(0.6f, 0.6f, 0.7f, 1f);
        btn.colors = colors;
        btn.targetGraphic = img;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        var label = textGo.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = "Continue";
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.fontSize = Mathf.RoundToInt(0.045f * pxPerMetre * (r.sizeDelta.x / (0.24f * pxPerMetre)));
        label.raycastTarget = false;

        return btn;
    }
}
