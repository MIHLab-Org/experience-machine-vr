using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A plain, steady hand pointer for every Unity UI button in the game (dialogue Continue and
/// options, Talk, Stop, wrist Walk). It replaces Meta's hand ray for UI, which only works
/// while the system "pointer pose" is valid and flickered on the headset (V10, 2026-11-12).
///
/// Each tracked hand casts a ray from an estimated shoulder through the hand, so it works
/// with any hand shape. A dot shows where it lands. To press a button:
///   - pinch (thumb + index) while the dot is on it, or
///   - hold the dot on it for Hand Dwell seconds (the dot grows as it fills).
/// If no hand is tracked, looking at a button for Gaze Dwell seconds presses it (headset only).
/// Meta's OVRHand is read by name, so there are no SDK type references. Put it on the Camera Rig.
/// </summary>
public class SimpleHandPointer : MonoBehaviour
{
    [Tooltip("Unlit material for the dots and lines (e.g. WristWalkTarget).")]
    public Material material;
    public float maxDistance = 6f;
    [Tooltip("Seconds the dot must rest on a button to press it without pinching. 0 = pinch only.")]
    public float handDwell = 1.2f;
    [Tooltip("Seconds of looking at a button to press it when no hand is tracked. 0 = off.")]
    public float gazeDwell = 3f;
    public bool gazeInEditor = false;
    [Tooltip("Extra hit area around buttons, as a fraction of their size.")]
    public float padding = 0.15f;
    public float smoothing = 18f;
    public Color idleColor = new Color(1f, 1f, 1f, 0.9f);
    public Color hoverColor = new Color(1f, 0.85f, 0.2f, 1f);

    private class Pointer
    {
        public Component ovrHand; public Transform anchor; public float side;
        public Transform dot; public LineRenderer line; public Renderer dotRenderer;
        public Selectable hover; public float dwell; public bool wasPinching; public bool armed = true;
        public Vector3 dir; public bool hasDir;
    }

    private readonly Pointer[] pointers = { new Pointer { side = -1f }, new Pointer { side = 1f }, new Pointer { side = 0f } }; // L, R, gaze
    private Transform head;
    private MethodInfo isPinching; private object indexFinger; private PropertyInfo isTrackedProp;
    private bool resolved; private float lastHandTime = -10f; private float clickCooldown;
    private MaterialPropertyBlock block;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>Text for the debug HUD.</summary>
    public string Status { get; private set; } = "";

    private void Update()
    {
        Resolve();
        if (head == null) return;
        if (clickCooldown > 0f) clickCooldown -= Time.unscaledDeltaTime;
        var sb = new System.Text.StringBuilder();
        bool anyHand = false;
        for (int i = 0; i < 2; i++)
        {
            Pointer p = pointers[i];
            bool tracked = p.ovrHand != null && p.anchor != null && Tracked(p.ovrHand);
            if (!tracked) { Clear(p); sb.Append(i == 0 ? "L:- " : "R:- "); continue; }
            anyHand = true; lastHandTime = Time.unscaledTime;
            Vector3 flatRight = head.right; flatRight.y = 0f; flatRight.Normalize();
            Vector3 shoulder = head.position + Vector3.down * 0.2f + flatRight * (0.18f * p.side);
            Vector3 want = (p.anchor.position - shoulder).normalized;
            p.dir = p.hasDir ? Vector3.Slerp(p.dir, want, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime)) : want;
            p.hasDir = true;
            bool pinch = Pinching(p.ovrHand);
            Tick(p, new Ray(p.anchor.position, p.dir), pinch && !p.wasPinching, handDwell, true);
            p.wasPinching = pinch;
            sb.Append(i == 0 ? "L:" : "R:").Append(p.hover != null ? p.hover.name : "ray").Append(pinch ? "(pinch) " : " ");
        }
        Pointer g = pointers[2];
        bool gaze = !anyHand && gazeDwell > 0f && Time.unscaledTime - lastHandTime > 1f && (!Application.isEditor || gazeInEditor);
        if (gaze) { Tick(g, new Ray(head.position, head.forward), false, gazeDwell, false); sb.Append("gaze:").Append(g.hover != null ? g.hover.name : "-"); }
        else Clear(g);
        Status = sb.ToString();
    }

    private void Tick(Pointer p, Ray ray, bool pinchDown, float dwellTime, bool showLine)
    {
        Selectable hit = FindSelectable(ray, out Vector3 point, out float dist);
        if (hit != p.hover)
        {
            Send(p.hover, ExecuteEvents.pointerExitHandler);
            p.hover = hit; p.dwell = 0f; p.armed = true;
            Send(p.hover, ExecuteEvents.pointerEnterHandler);
        }
        EnsureVisuals(p);
        float fill = 0f;
        if (hit != null)
        {
            if (p.armed && dwellTime > 0f) { p.dwell += Time.unscaledDeltaTime; fill = Mathf.Clamp01(p.dwell / dwellTime); }
            bool dwellDone = p.armed && dwellTime > 0f && p.dwell >= dwellTime;
            if ((pinchDown || dwellDone) && clickCooldown <= 0f)
            {
                clickCooldown = 0.5f; p.dwell = 0f; p.armed = !dwellDone; // after a dwell click, leave the button before dwelling again
                Send(hit, ExecuteEvents.pointerClickHandler);
                fill = 0f;
            }
        }
        Vector3 end = hit != null ? point : ray.origin + ray.direction * 1.5f;
        float d = hit != null ? dist : 1.5f;
        p.dot.gameObject.SetActive(true);
        p.dot.position = end - ray.direction * 0.01f;
        p.dot.localScale = Vector3.one * (0.012f + 0.004f * d) * (1f + 1.5f * fill);
        SetColor(p.dotRenderer, hit != null ? hoverColor : idleColor);
        p.line.enabled = showLine;
        if (showLine) { p.line.SetPosition(0, ray.origin + ray.direction * 0.08f); p.line.SetPosition(1, end); }
    }

    private Selectable FindSelectable(Ray ray, out Vector3 point, out float best)
    {
        Selectable found = null; point = default; best = maxDistance;
        foreach (Selectable s in Selectable.allSelectablesArray)
        {
            if (s == null || !s.isActiveAndEnabled || !s.IsInteractable()) continue;
            var rt = s.transform as RectTransform; if (rt == null) continue;
            Canvas c = s.GetComponentInParent<Canvas>();
            if (c == null || c.renderMode != RenderMode.WorldSpace) continue;
            var plane = new Plane(rt.forward, rt.position);
            if (!plane.Raycast(ray, out float t) || t <= 0.05f || t >= best) continue;
            Vector3 world = ray.GetPoint(t);
            Vector2 local = rt.InverseTransformPoint(world);
            Rect r = rt.rect;
            float px = r.width * padding, py = r.height * padding;
            if (local.x < r.xMin - px || local.x > r.xMax + px || local.y < r.yMin - py || local.y > r.yMax + py) continue;
            found = s; best = t; point = world;
        }
        return found;
    }

    private static void Send<T>(Selectable s, ExecuteEvents.EventFunction<T> fn) where T : IEventSystemHandler
    {
        if (s == null || EventSystem.current == null) return;
        ExecuteEvents.Execute(s.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, fn);
    }

    private void Clear(Pointer p)
    {
        if (p.hover != null) { Send(p.hover, ExecuteEvents.pointerExitHandler); p.hover = null; }
        p.dwell = 0f; p.hasDir = false; p.wasPinching = false;
        if (p.dot != null) p.dot.gameObject.SetActive(false);
        if (p.line != null) p.line.enabled = false;
    }

    private void EnsureVisuals(Pointer p)
    {
        if (p.dot != null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "HandPointer Dot"; Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        p.dot = go.transform; p.dotRenderer = go.GetComponent<Renderer>();
        if (material != null) p.dotRenderer.sharedMaterial = material;
        p.dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var lg = new GameObject("HandPointer Line"); lg.transform.SetParent(transform, false);
        p.line = lg.AddComponent<LineRenderer>();
        p.line.positionCount = 2; p.line.useWorldSpace = true;
        p.line.startWidth = 0.004f; p.line.endWidth = 0.002f;
        if (material != null) p.line.sharedMaterial = material;
        p.line.startColor = new Color(1f, 1f, 1f, 0.6f); p.line.endColor = new Color(1f, 1f, 1f, 0.25f);
        p.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void SetColor(Renderer r, Color c)
    {
        if (block == null) block = new MaterialPropertyBlock();
        block.SetColor(BaseColor, c); block.SetColor(ColorId, c);
        r.SetPropertyBlock(block);
    }

    private bool Tracked(Component hand)
    {
        if (isTrackedProp == null) return false;
        try { return (bool)isTrackedProp.GetValue(hand); } catch { return false; }
    }

    private bool Pinching(Component hand)
    {
        if (isPinching == null || indexFinger == null) return false;
        try { return (bool)isPinching.Invoke(hand, new[] { indexFinger }); } catch { return false; }
    }

    private void Resolve()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (resolved) return;
        resolved = true;
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "LeftHandAnchor") pointers[0].anchor = t;
            else if (t.name == "RightHandAnchor") pointers[1].anchor = t;
        }
        Type ovrHand = null;
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies()) { ovrHand = a.GetType("OVRHand"); if (ovrHand != null) break; }
        if (ovrHand == null) return;
        foreach (UnityEngine.Object o in FindObjectsByType(ovrHand, FindObjectsInactive.Include))
        {
            var c = (Component)o;
            if (c.gameObject.name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0) pointers[0].ovrHand = c;
            else if (c.gameObject.name.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0) pointers[1].ovrHand = c;
        }
        Type finger = ovrHand.GetNestedType("HandFinger");
        if (finger != null) { indexFinger = Enum.Parse(finger, "Index"); isPinching = ovrHand.GetMethod("GetFingerIsPinching", new[] { finger }); }
        isTrackedProp = ovrHand.GetProperty("IsTracked");
    }
}
