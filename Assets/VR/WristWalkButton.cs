using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>
/// Free walking between groves, hands only (backlog V15, Jim's V4 choice: walk, not teleport).
///
///   1. Look at your left wrist, as if checking a watch: a small "Walk" button appears just
///      above it. (Palm-up can be required as well: Require Palm Up + Palm Axis.)
///   2. Press it (ray-pinch with the right hand, or poke). The button now reads "Cancel" and a
///      ring appears on the ground where your right hand points (snapped to the NavMesh).
///   3. Pinch with the right hand, or hold the ring still for Dwell Seconds, to confirm.
///      WalkAlongMover.WalkToPoint carries you there at walking pace with the comfort
///      vignette. No teleport jump.
///   4. While walking, the existing Stop button (WalkStopButton) stops you; arrival ends the walk.
///
/// The button hides during conversations (walking chatter excepted) and while a philosopher
/// is walking you somewhere: those walks have their own Stop button.
///
/// No Meta types: the hands are read by name. Left/Right Wrist default to the rig's
/// LeftHandAnchor / RightHandAnchor. Pinch and the pointing ray come from Meta's OVRHand
/// (GetFingerIsPinching, PointerPose) through reflection when it is present. Without a
/// tracked right hand (e.g. in the Editor) the head is used as the pointer, tilted down, and
/// dwell confirms; Press() / Confirm() / Cancel() can also be called from anything.
///
/// Setup
///   1. Add this to the camera rig root (the object WalkAlongMover moves), next to
///      WalkStopButton. Everything else is found automatically.
///   2. Right-click the component > Build Button, then select the "WristWalk Canvas" and apply
///      Meta's Interaction SDK > Add Ray Interaction to Canvas (as for the Talk buttons).
/// </summary>
[DisallowMultipleComponent]
public class WristWalkButton : MonoBehaviour
{
    private const float PixelsPerMetre = 1000f;

    private enum Mode { Idle, Aiming }

    [Header("References (found automatically if empty)")]
    [SerializeField] private WalkAlongMover mover;
    [SerializeField] private Transform head;
    [SerializeField] private DialogueRunner dialogueRunner;
    [Tooltip("Left wrist. Empty = LeftHandAnchor in the rig.")]
    [SerializeField] private Transform leftWrist;
    [Tooltip("Right hand, the fallback pointer. Empty = RightHandAnchor in the rig.")]
    [SerializeField] private Transform rightHand;

    [Header("Show when looking at the wrist")]
    [SerializeField] private float lookAngle = 35f;
    [SerializeField] private float maxWristDistance = 0.75f;
    [Tooltip("Keep the button up this long after the wrist leaves view, so you can reach it.")]
    [SerializeField] private float holdSeconds = 2f;
    [SerializeField] private bool requirePalmUp = false;
    [Tooltip("The wrist's local axis that points out of the palm (calibrate on the headset).")]
    [SerializeField] private Vector3 palmAxis = Vector3.up;
    [SerializeField] private float palmUpAngle = 50f;

    [Header("Aiming")]
    [SerializeField] private float maxWalkDistance = 30f;
    [SerializeField] private float dwellSeconds = 1.5f;
    [SerializeField] private float dwellRadius = 0.5f;
    [Tooltip("Pointer tilt when the head is used as the pointer (no tracked hand).")]
    [SerializeField] private float headPointerTilt = 25f;
    [SerializeField] private Color ringColor = new Color(0.55f, 0.85f, 1f, 0.9f);
    [Tooltip("Material for the target ring and ray (keeps the shader in Quest builds). Empty = URP Unlit by name.")]
    [SerializeField] private Material ringMaterial;

    [Header("Button")]
    [SerializeField] private Vector2 sizeMetres = new Vector2(0.11f, 0.05f);
    [SerializeField] private float aboveWrist = 0.07f;
    [SerializeField] private string walkLabel = "Walk";
    [SerializeField] private string cancelLabel = "Cancel";

    [Header("UI (built automatically if empty)")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Button button;
    [SerializeField] private Text label;

    private Mode mode = Mode.Idle;
    private float lastSeenTime = -999f;
    private float aimStartTime;
    private bool pinchArmed;
    private bool lastPinch;
    private Vector3 dwellCentre;
    private float dwellTimer;
    private bool hasTarget;
    private Vector3 target;
    private Transform ring;
    private LineRenderer line;

    // OVRHand via reflection
    private Component leftOvrHand, rightOvrHand;
    private MethodInfo isPinching;
    private object indexFinger;
    private PropertyInfo isTrackedProp, pointerPoseProp, pointerValidProp;
    private bool searchedHands;

    /// <summary>True while the player is choosing a spot.</summary>
    public bool IsAiming => mode == Mode.Aiming;

    private void Awake()
    {
        EnsureCanvas();
        if (button != null) button.onClick.AddListener(Press);
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Press);
    }

    // ---------------------------------------------------------------- public API

    /// <summary>The wrist button: start aiming, or cancel aiming.</summary>
    public void Press()
    {
        if (mode == Mode.Aiming) { Cancel(); return; }
        mode = Mode.Aiming;
        aimStartTime = Time.time;
        pinchArmed = false; // the pinch that pressed the button must be released first
        dwellTimer = 0f;
        hasTarget = false;
        if (label != null) label.text = cancelLabel;
    }

    /// <summary>Walk to the spot being pointed at (pinch, or call from anything).</summary>
    public void Confirm()
    {
        if (mode != Mode.Aiming || !hasTarget || mover == null) return;
        mover.WalkToPoint(target);
        EndAiming();
    }

    public void Cancel()
    {
        EndAiming();
    }

    /// <summary>Builds the button if it doesn't exist yet. Safe to call in the Editor.</summary>
    [ContextMenu("Build Button")]
    public Canvas EnsureCanvas()
    {
        if (canvas == null)
        {
            Transform existing = transform.Find("WristWalk Canvas");
            if (existing != null) canvas = existing.GetComponent<Canvas>();
        }
        if (canvas == null) BuildButton();
        if (button == null) button = canvas.GetComponentInChildren<Button>(true);
        if (label == null && button != null) label = button.GetComponentInChildren<Text>(true);
        if (label != null && string.IsNullOrEmpty(label.text)) label.text = walkLabel;
        return canvas;
    }

    // ---------------------------------------------------------------- update

    private void LateUpdate()
    {
        Resolve();
        if (mover == null || head == null || canvas == null) { SetVisible(false); ShowAim(false); return; }

        if (Busy())
        {
            if (mode == Mode.Aiming) EndAiming();
            SetVisible(false);
            return;
        }

        if (mode == Mode.Aiming)
        {
            Aim();
            PlaceButton();
            SetVisible(true);
            return;
        }

        if (WristInView()) lastSeenTime = Time.time;
        bool show = Time.time - lastSeenTime < holdSeconds && leftWrist != null;
        SetVisible(show);
        if (show) PlaceButton();
    }

    /// <summary>Conversations, philosopher walks and point walks already under way hide the button.</summary>
    private bool Busy()
    {
        if (dialogueRunner != null && dialogueRunner.IsDialogueRunning && !WalkingChatter.IsChatting) return true;
        if (mover.IsMoving) return true;
        PhilosopherWalker w = PhilosopherWalker.Active;
        if (w != null && (w.State == PhilosopherWalker.WalkState.Walking || w.State == PhilosopherWalker.WalkState.HandingOff)) return true;
        return false;
    }

    private bool WristInView()
    {
        if (leftWrist == null || !leftWrist.gameObject.activeInHierarchy) return false;
        if (leftOvrHand != null && !Tracked(leftOvrHand)) return false;
        Vector3 toWrist = leftWrist.position - head.position;
        if (toWrist.magnitude > maxWristDistance || toWrist.magnitude < 0.1f) return false;
        if (Vector3.Angle(head.forward, toWrist) > lookAngle) return false;
        if (requirePalmUp && Vector3.Angle(leftWrist.TransformDirection(palmAxis), Vector3.up) > palmUpAngle) return false;
        return true;
    }

    private void Aim()
    {
        Ray ray = PointerRay(out bool fromHand);
        hasTarget = false;
        if (Physics.Raycast(ray, out RaycastHit hit, maxWalkDistance + 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 p = hit.point;
            if (NavMesh.SamplePosition(p, out NavMeshHit nav, 2f, NavMesh.AllAreas)) p = nav.position;
            Vector3 flat = p - head.position;
            flat.y = 0f;
            if (flat.magnitude <= maxWalkDistance && flat.magnitude > 1f)
            {
                hasTarget = true;
                target = p;
            }
        }

        ShowAim(hasTarget, ray.origin);

        // Dwell: hold the ring roughly still to confirm.
        if (hasTarget)
        {
            if ((target - dwellCentre).sqrMagnitude > dwellRadius * dwellRadius) { dwellCentre = target; dwellTimer = 0f; }
            else dwellTimer += Time.deltaTime;
            if (ring != null) ring.localScale = new Vector3(0.6f, 0.01f, 0.6f) * (1f - 0.4f * Mathf.Clamp01(dwellTimer / dwellSeconds));
            if (Time.time - aimStartTime > 0.5f && dwellTimer >= dwellSeconds) { Confirm(); return; }
        }
        else dwellTimer = 0f;

        // Pinch (right hand): needs a release after the press that opened aiming.
        bool pinch = RightPinching();
        if (!pinch) pinchArmed = true;
        if (pinch && !lastPinch && pinchArmed && Time.time - aimStartTime > 0.3f && fromHand) Confirm();
        lastPinch = pinch;
    }

    private void EndAiming()
    {
        mode = Mode.Idle;
        hasTarget = false;
        dwellTimer = 0f;
        ShowAim(false);
        if (label != null) label.text = walkLabel;
    }

    // ---------------------------------------------------------------- hands

    private Ray PointerRay(out bool fromHand)
    {
        fromHand = false;
        if (rightOvrHand != null && Tracked(rightOvrHand) && pointerPoseProp != null)
        {
            bool valid = pointerValidProp == null || (bool)pointerValidProp.GetValue(rightOvrHand);
            var pose = pointerPoseProp.GetValue(rightOvrHand) as Transform;
            if (valid && pose != null) { fromHand = true; return new Ray(pose.position, pose.forward); }
        }
        if (rightHand != null && rightHand.gameObject.activeInHierarchy && rightOvrHand != null && Tracked(rightOvrHand))
        {
            fromHand = true;
            return new Ray(rightHand.position, rightHand.forward);
        }
        Vector3 dir = Quaternion.AngleAxis(headPointerTilt, head.right) * head.forward;
        return new Ray(head.position, dir);
    }

    private bool RightPinching()
    {
        if (rightOvrHand == null || isPinching == null || indexFinger == null) return false;
        try { return (bool)isPinching.Invoke(rightOvrHand, new[] { indexFinger }); }
        catch { return false; }
    }

    private bool Tracked(Component ovrHand)
    {
        if (isTrackedProp == null) return true;
        try { return (bool)isTrackedProp.GetValue(ovrHand); } catch { return false; }
    }

    private void Resolve()
    {
        if (mover == null) mover = FindAnyObjectByType<WalkAlongMover>();
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (dialogueRunner == null) dialogueRunner = FindAnyObjectByType<DialogueRunner>();
        if (leftWrist == null) leftWrist = FindInScene("LeftHandAnchor");
        if (rightHand == null) rightHand = FindInScene("RightHandAnchor");
        if (searchedHands) return;
        searchedHands = true;

        Type ovrHand = null;
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            ovrHand = a.GetType("OVRHand");
            if (ovrHand != null) break;
        }
        if (ovrHand == null) return;
        foreach (UnityEngine.Object o in FindObjectsByType(ovrHand, FindObjectsInactive.Include))
        {
            var c = (Component)o;
            string n = c.gameObject.name;
            if (n.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0) leftOvrHand = c;
            else if (n.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0) rightOvrHand = c;
        }
        Type finger = ovrHand.GetNestedType("HandFinger");
        if (finger != null)
        {
            indexFinger = Enum.Parse(finger, "Index");
            isPinching = ovrHand.GetMethod("GetFingerIsPinching", new[] { finger });
        }
        isTrackedProp = ovrHand.GetProperty("IsTracked");
        pointerPoseProp = ovrHand.GetProperty("PointerPose");
        pointerValidProp = ovrHand.GetProperty("IsPointerPoseValid");
    }

    private static Transform FindInScene(string name)
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            if (t.name == name) return t;
        return null;
    }

    // ---------------------------------------------------------------- visuals

    private void PlaceButton()
    {
        Vector3 pos;
        if (leftWrist != null && leftWrist.gameObject.activeInHierarchy && (leftOvrHand == null || Tracked(leftOvrHand)))
        {
            pos = leftWrist.position + Vector3.up * aboveWrist;
        }
        else
        {
            // Wrist lost while aiming: keep the Cancel button low and to the left of the view.
            Vector3 flat = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, flat);
            pos = head.position + flat * 0.55f - right * 0.2f + Vector3.down * 0.3f;
        }
        Transform t = canvas.transform;
        t.position = pos;
        Vector3 away = pos - head.position;
        if (away.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(away, Vector3.up);
    }

    private void ShowAim(bool show, Vector3 from = default)
    {
        if (show && ring == null) BuildRing();
        if (ring != null) ring.gameObject.SetActive(show);
        if (line != null) line.enabled = show;
        if (!show) return;
        ring.position = target + Vector3.up * 0.02f;
        line.SetPosition(0, from);
        line.SetPosition(1, target + Vector3.up * 0.02f);
    }

    private void BuildRing()
    {
        Material mat;
        if (ringMaterial != null) mat = new Material(ringMaterial);
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            mat = new Material(shader);
        }
        mat.color = ringColor;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", ringColor);

        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "WristWalk Target";
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring = go.transform;
        ring.localScale = new Vector3(0.6f, 0.01f, 0.6f);

        var lineGo = new GameObject("WristWalk Ray");
        line = lineGo.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = 0.006f;
        line.endWidth = 0.003f;
        line.sharedMaterial = mat;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void SetVisible(bool visible)
    {
        if (canvas != null && canvas.gameObject.activeSelf != visible) canvas.gameObject.SetActive(visible);
    }

    private void BuildButton()
    {
        var canvasGo = new GameObject("WristWalk Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRect = (RectTransform)canvasGo.transform;
        canvasRect.sizeDelta = sizeMetres * PixelsPerMetre;
        canvasRect.localScale = Vector3.one / PixelsPerMetre;
        canvasGo.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

        var buttonGo = new GameObject("Walk Button", typeof(RectTransform), typeof(Image), typeof(Button));
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
        textRect.offsetMin = new Vector2(8f, 6f);
        textRect.offsetMax = new Vector2(-8f, -6f);

        label = textGo.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 10;
        label.resizeTextMaxSize = 60;
        label.raycastTarget = false;
        label.text = walkLabel;
    }
}
