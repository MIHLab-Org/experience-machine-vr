using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MIHLab > Make scene hand-ready (backlog V7, Quest VR).
///
/// Run it in VR_Slice (not SampleScene), with the philosophers you want to talk to selected.
/// In the open scene it:
///   1. finds the Yarn dialogue canvas (the root Canvas above a LinePresenter), sets it to
///      World Space now (same size and scale WorldSpaceDialoguePanel uses at run time) and adds
///      WorldSpaceDialoguePanel (V6) if it is missing;
///   2. adds a HandTalkPrompt (V5) to every selected philosopher (objects with PhilosopherNPC or
///      PhilosopherWalker; children of a selected parent count too) and builds its Talk button
///      in the scene, so the canvas exists before Play;
///   3. if Meta's Interaction SDK is installed (types found by name, so this compiles without
///      it), lists the canvases that still have no PointableCanvas and selects them, ready for
///      Meta's quick actions: right-click > Interaction SDK > Add Poke Interaction to Canvas,
///      then Add Ray Interaction to Canvas. Those set up the PointableCanvas, the poke and ray
///      interactables and their surfaces together; a bare PointableCanvas alone does not let
///      hands press anything, so this menu does not add one by itself.
/// Logs every change; never deletes anything; everything goes through Undo (Ctrl+Z).
/// Save the scene afterwards. See Docs/how-to-make-scene-hand-ready.md.
/// </summary>
public static class MIHLabVRSetup
{
    private const string Menu = "MIHLab/Make scene hand-ready";
    private const string Tag = "[MIHLab hand-ready]";

    [MenuItem(Menu)]
    private static void MakeHandReady()
    {
        Undo.SetCurrentGroupName("Make scene hand-ready");
        int group = Undo.GetCurrentGroup();
        var touched = new List<Canvas>();
        int changes = 0;

        // 1. Dialogue canvas.
        Canvas dialogue = FindDialogueCanvas();
        if (dialogue == null)
        {
            Debug.LogWarning($"{Tag} No Yarn dialogue canvas found (no LinePresenter under a Canvas in the open scene). Skipped step 1.");
        }
        else
        {
            changes += PrepareDialogueCanvas(dialogue);
            touched.Add(dialogue);
        }

        // 2. Talk prompts on the selected philosophers.
        var philosophers = SelectedPhilosophers();
        if (philosophers.Count == 0)
        {
            Debug.LogWarning($"{Tag} No philosophers selected (objects with PhilosopherNPC or PhilosopherWalker). Select them in the Hierarchy and run the menu again to add Talk buttons.");
        }
        foreach (GameObject go in philosophers)
        {
            var prompt = go.GetComponent<HandTalkPrompt>();
            if (prompt == null)
            {
                prompt = Undo.AddComponent<HandTalkPrompt>(go);
                Debug.Log($"{Tag} {go.name}: added HandTalkPrompt.", go);
                changes++;
            }

            bool hadCanvas = HasPromptCanvas(prompt);
            Undo.RecordObject(prompt, "Build Talk button");
            Canvas c = prompt.EnsureCanvas();
            if (!hadCanvas && c != null)
            {
                Undo.RegisterCreatedObjectUndo(c.gameObject, "Build Talk button");
                Debug.Log($"{Tag} {go.name}: built its Talk button canvas in the scene.", c);
                changes++;
            }
            EditorUtility.SetDirty(prompt);
            if (c != null) touched.Add(c);
        }

        // 3. Meta Interaction SDK.
        Type pointableCanvas = FindType("Oculus.Interaction.PointableCanvas");
        if (pointableCanvas == null)
        {
            Debug.Log($"{Tag} Meta Interaction SDK not found, so no hand interaction was checked. Install the Meta XR All-in-One SDK (V3), then run this menu again.");
        }
        else
        {
            var needQuickActions = new List<UnityEngine.Object>();
            foreach (Canvas c in touched)
            {
                if (c.GetComponentInChildren(pointableCanvas, true) != null)
                {
                    Debug.Log($"{Tag} {c.name}: already has a PointableCanvas, hands can reach it.", c);
                }
                else
                {
                    needQuickActions.Add(c.gameObject);
                    Debug.Log($"{Tag} {c.name}: needs Meta's quick actions. Right-click it > Interaction SDK > Add Poke Interaction to Canvas, then Add Ray Interaction to Canvas.", c);
                }
            }
            if (needQuickActions.Count > 0) Selection.objects = needQuickActions.ToArray();

            Type module = FindType("Oculus.Interaction.PointableCanvasModule");
            if (module != null && UnityEngine.Object.FindAnyObjectByType(module, FindObjectsInactive.Include) == null)
            {
                Debug.Log($"{Tag} The EventSystem has no PointableCanvasModule yet. Meta's quick action wizard offers to add it (Fix button); accept it.");
            }
        }

        Undo.CollapseUndoOperations(group);
        if (changes > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"{Tag} Done: {changes} change(s). Save the scene to keep them.");
    }

    // ---------------------------------------------------------------- step 1

    private static Canvas FindDialogueCanvas()
    {
        foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb == null || mb.GetType().Name != "LinePresenter") continue;
            Canvas c = mb.GetComponentInParent<Canvas>(true);
            if (c != null) return c.rootCanvas != null ? c.rootCanvas : c;
        }
        return null;
    }

    private static int PrepareDialogueCanvas(Canvas canvas)
    {
        int changes = 0;
        GameObject go = canvas.gameObject;

        if (go.GetComponent<WorldSpaceDialoguePanel>() == null)
        {
            Undo.AddComponent<WorldSpaceDialoguePanel>(go);
            Debug.Log($"{Tag} {go.name}: added WorldSpaceDialoguePanel (V6).", go);
            changes++;
        }

        if (canvas.renderMode != RenderMode.WorldSpace)
        {
            var rect = (RectTransform)canvas.transform;
            // Screen Space canvases have the screen's size; use the scaler's reference size instead
            // so the panel looks the same as the run-time conversion.
            Vector2 size = new Vector2(1600f, 900f);
            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.referenceResolution.x >= 1f && scaler.referenceResolution.y >= 1f)
            {
                size = scaler.referenceResolution;
            }

            Undo.RecordObject(canvas, "World Space dialogue canvas");
            Undo.RecordObject(rect, "World Space dialogue canvas");
            canvas.renderMode = RenderMode.WorldSpace;
            rect.sizeDelta = size;
            rect.pivot = new Vector2(0.5f, 0.5f);
            float scale = 0.9f / size.x; // WorldSpaceDialoguePanel's default Panel Width
            rect.localScale = new Vector3(scale, scale, scale);

            Camera cam = Camera.main;
            if (cam != null)
            {
                canvas.worldCamera = cam;
                Vector3 forward = cam.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                forward.Normalize();
                rect.position = cam.transform.position + forward * 1.2f + Vector3.up * -0.15f;
                rect.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }

            if (scaler != null)
            {
                Undo.RecordObject(scaler, "World Space dialogue canvas");
                scaler.enabled = false;
            }
            if (go.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(go);

            Debug.Log($"{Tag} {go.name}: set to World Space ({size.x:0} x {size.y:0} px, 0.9 m wide). WorldSpaceDialoguePanel moves it in front of the player at run time.", go);
            changes++;
        }
        return changes;
    }

    // ---------------------------------------------------------------- step 2

    private static List<GameObject> SelectedPhilosophers()
    {
        var result = new List<GameObject>();
        foreach (GameObject sel in Selection.gameObjects)
        {
            foreach (Transform t in sel.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                if (result.Contains(go)) continue;
                if (go.GetComponent<PhilosopherNPC>() != null || go.GetComponent<PhilosopherWalker>() != null)
                {
                    result.Add(go);
                }
            }
        }
        return result;
    }

    private static bool HasPromptCanvas(HandTalkPrompt prompt)
    {
        var so = new SerializedObject(prompt);
        SerializedProperty p = so.FindProperty("canvas");
        return p != null && p.objectReferenceValue != null;
    }

    // ---------------------------------------------------------------- helpers

    private static Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = asm.GetType(fullName, false);
            if (t != null) return t;
        }
        return null;
    }
}
