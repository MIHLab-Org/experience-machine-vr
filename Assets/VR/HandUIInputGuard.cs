using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// On the headset, hand rays and pokes reach Unity UI only through Meta's PointableCanvasModule.
/// An EventSystem runs just one input module at a time, and Unity's InputSystemUIInputModule
/// (needed for the mouse in the Editor / XR Simulator) sits first and wins, so pinches on the
/// dialogue Continue button, Talk buttons and Stop button never registered on the Quest.
/// This turns every other input module off in player builds and leaves the Editor alone.
/// Put it on the same object as the EventSystem (UI_EventSystem).
/// </summary>
[DefaultExecutionOrder(-1000)]
public class HandUIInputGuard : MonoBehaviour
{
    [Tooltip("Also disable the mouse/keyboard module in the Editor (e.g. when testing over Link).")]
    public bool alsoInEditor = false;

    private void Awake()
    {
        if (Application.isEditor && !alsoInEditor) return;
        var pointable = GetComponent("PointableCanvasModule") as Behaviour;
        if (pointable == null) { Debug.LogWarning("[HandUIInputGuard] No PointableCanvasModule here; leaving input modules alone.", this); return; }
        foreach (var m in GetComponents<BaseInputModule>())
        {
            if (m == pointable) continue;
            if (m.enabled) { m.enabled = false; Debug.Log($"[HandUIInputGuard] Disabled {m.GetType().Name} so hand input reaches the UI.", this); }
        }
        pointable.enabled = true;
    }
}
