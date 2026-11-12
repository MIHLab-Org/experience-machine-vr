using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Diagnostic text in the top-left corner of the dialogue panel (headset test, V10):
/// build version, the EventSystem's active input module, each hand ray's state and what it
/// is touching, and the last UI click. Meta types are read by name, so no SDK references.
/// Put it on the dialogue Canvas. Turn it off (or remove it) for the release build.
/// </summary>
public class HandDebugHUD : MonoBehaviour
{
    public bool show = true;
    public int fontSize = 28;

    private Text label;
    private readonly List<Component> rays = new List<Component>();
    private float nextScan;
    private string lastClick = "none";

    private void Start()
    {
        var go = new GameObject("HandDebugHUD Text", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -10f); rt.sizeDelta = new Vector2(-40f, 260f);
        label = go.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize; label.color = new Color(1f, 0.95f, 0.4f); label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Overflow;
        foreach (var b in GetComponentsInChildren<Button>(true))
        {
            string n = b.name;
            b.onClick.AddListener(() => lastClick = n + " @ " + Time.time.ToString("F1") + "s");
        }
    }

    private void Update()
    {
        if (label == null) return;
        label.enabled = show;
        if (!show) return;
        if (Time.unscaledTime > nextScan) { Scan(); nextScan = Time.unscaledTime + 3f; }

        var sb = new StringBuilder();
        sb.Append("v").Append(Application.version).Append("   module: ");
        var es = EventSystem.current;
        sb.Append(es != null && es.currentInputModule != null ? es.currentInputModule.GetType().Name : "none");
        sb.Append("   last click: ").Append(lastClick).Append('\n');
        foreach (var r in rays)
        {
            if (r == null || !r.gameObject.activeInHierarchy) continue;
            string side = Path(r.transform).Contains("Left") ? "L" : "R";
            object state = Get(r, "State");
            object cand = Get(r, "Candidate") ?? Get(r, "Interactable");
            sb.Append(side).Append(" ray: ").Append(state ?? "?").Append("  target: ").Append(Name(cand)).Append('\n');
        }
        if (es != null && es.currentSelectedGameObject != null) sb.Append("selected: ").Append(es.currentSelectedGameObject.name);
        label.text = sb.ToString();
    }

    private void Scan()
    {
        rays.Clear();
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            if (mb.GetType().Name == "RayInteractor" && Path(mb.transform).Contains("Hand")) rays.Add(mb);
    }

    private static object Get(object o, string prop)
    {
        try
        {
            var p = o.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
            return p != null ? p.GetValue(o) : null;
        }
        catch { return null; }
    }

    private static string Name(object o)
    {
        if (o == null) return "-";
        if (o is Component c) return c.transform.parent != null ? c.transform.parent.name + "/" + c.name : c.name;
        return o.ToString();
    }

    private static string Path(Transform t)
    {
        string s = t.name;
        for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
        return s;
    }
}
