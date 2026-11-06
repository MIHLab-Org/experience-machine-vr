using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// MIHLab > Set up VR slice (one click). Does Jim's Editor steps in one go, in the open scene:
///   1. Bakes a NavMesh (W5): finds or creates a "NavMesh" object with a NavMesh Surface
///      (All Game Objects, Physics Colliders, so trees with colliders become holes), bakes it
///      and saves the data as an asset next to the scene so it survives a reload.
///   2. Converts Socrates to a walker (W5): runs MIHLab > Convert selected NPC to Walker on the
///      object whose name contains "Socrates" and that has a DialogueTrigger or PhilosopherNPC.
///      Skipped if he already has a PhilosopherWalker.
///   3. Makes the scene hand-ready (V7): runs MIHLab > Make scene hand-ready with Socrates and
///      the contest philosophers selected (names containing Socrates, Heim, Bostrom, Nozick,
///      Augustine, Gregory, Chrysostom; whichever exist).
///   4. Adds the walk mover (V8): puts WalkAlongMover on the camera rig root (Meta's
///      OVRCameraRig if present, otherwise the object tagged Player), sets its Rig, and points
///      every PhilosopherWalker's Player Override at the same object.
/// Every step logs what it did, goes through Undo, and never deletes anything. Running it again
/// is safe: finished steps are skipped (the NavMesh is re-baked into a new asset; delete old ones by hand if you like). Save the scene afterwards.
/// Left for Jim: Meta's right-click quick actions on the canvases the menu selects at the end
/// (Interaction SDK > Add Poke / Add Ray Interaction to Canvas); see
/// Docs/how-to-make-scene-hand-ready.md.
/// </summary>
public static class MIHLabOneClick
{
    private const string Menu = "MIHLab/Set up VR slice (one click)";
    private const string Tag = "[MIHLab one-click]";
    private static readonly string[] ContestNames =
        { "Socrates", "Heim", "Bostrom", "Nozick", "Augustine", "Gregory", "Chrysostom" };

    [MenuItem(Menu, false, 0)]
    private static void SetUp()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("Set up VR slice", "Save the scene first (it needs a folder for the NavMesh data).", "OK");
            return;
        }
        if (scene.name == "SampleScene" &&
            !EditorUtility.DisplayDialog("Set up VR slice",
                "This is SampleScene, the flat-screen version. The VR setup is meant for VR_Slice. Continue anyway?",
                "Continue", "Cancel"))
        {
            return;
        }

        UnityEngine.Object[] oldSelection = Selection.objects;
        try
        {
            BakeNavMesh(scene.path);
            ConvertSocrates();
            MakeHandReady();
            AddMover();
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} Stopped: {e.Message}\n{e.StackTrace}");
            Selection.objects = oldSelection;
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"{Tag} Done. Now: right-click each selected canvas > Interaction SDK > Add Poke Interaction to Canvas, then Add Ray Interaction to Canvas (accept any Fix the wizard offers). Then save the scene.");
    }

    // ---------------------------------------------------------------- 1. NavMesh

    private static void BakeNavMesh(string scenePath)
    {
        NavMeshSurface surface = UnityEngine.Object.FindAnyObjectByType<NavMeshSurface>(FindObjectsInactive.Include);
        if (surface == null)
        {
            var go = new GameObject("NavMesh");
            Undo.RegisterCreatedObjectUndo(go, "Create NavMesh");
            surface = Undo.AddComponent<NavMeshSurface>(go);
            Debug.Log($"{Tag} Created a 'NavMesh' object with a NavMesh Surface.", go);
        }

        Undo.RecordObject(surface, "Bake NavMesh");
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        EditorUtility.DisplayProgressBar("Set up VR slice", "Baking the NavMesh (this can take a minute)...", 0.2f);
        try
        {
            surface.BuildNavMesh();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (surface.navMeshData == null)
        {
            Debug.LogWarning($"{Tag} The bake produced no NavMesh. Check that the terrain has a Terrain Collider.", surface);
            return;
        }

        // Save the data as an asset next to the scene, or it is lost when the scene reloads.
        string dir = Path.Combine(Path.GetDirectoryName(scenePath) ?? "Assets", Path.GetFileNameWithoutExtension(scenePath));
        if (!AssetDatabase.IsValidFolder(dir))
        {
            AssetDatabase.CreateFolder(Path.GetDirectoryName(dir), Path.GetFileName(dir));
        }
        string assetPath = Path.Combine(dir, "NavMesh-" + surface.gameObject.name + ".asset").Replace('\\', '/');
        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath); // never overwrite or delete an earlier bake
        AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(surface);
        Debug.Log($"{Tag} NavMesh baked and saved to {assetPath}. The blue overlay in the Scene view shows where philosophers can walk.", surface);
    }

    // ---------------------------------------------------------------- 2. Socrates

    private static void ConvertSocrates()
    {
        GameObject socrates = FindPhilosopher("Socrates", requireWalker: false);
        if (socrates == null)
        {
            Debug.LogWarning($"{Tag} No Socrates found (an object named *Socrates* with DialogueTrigger or PhilosopherNPC). Skipped the conversion.");
            return;
        }
        if (socrates.GetComponent<PhilosopherWalker>() != null)
        {
            Debug.Log($"{Tag} {socrates.name} is already a walker.", socrates);
            return;
        }
        Selection.objects = new UnityEngine.Object[] { socrates };
        if (!EditorApplication.ExecuteMenuItem("MIHLab/Convert selected NPC to Walker"))
        {
            throw new InvalidOperationException("Could not run MIHLab > Convert selected NPC to Walker.");
        }
        var walker = socrates.GetComponent<PhilosopherWalker>();
        if (walker != null)
        {
            var so = new SerializedObject(walker);
            SerializedProperty id = so.FindProperty("philosopherId");
            if (id != null && string.IsNullOrEmpty(id.stringValue))
            {
                id.stringValue = "Socrates"; // so $came_from reads "Socrates" in the church fathers' dialogue
                so.ApplyModifiedProperties();
            }
        }
        Debug.Log($"{Tag} Converted {socrates.name} to a walker.", socrates);
    }

    // ---------------------------------------------------------------- 3. Hand-ready

    private static void MakeHandReady()
    {
        var found = new List<UnityEngine.Object>();
        foreach (string n in ContestNames)
        {
            GameObject go = FindPhilosopher(n, requireWalker: false);
            if (go != null && !found.Contains(go)) found.Add(go);
        }
        Debug.Log($"{Tag} Philosophers getting Talk buttons: {(found.Count == 0 ? "none found" : string.Join(", ", found.ConvertAll(o => o.name)))}.");
        Selection.objects = found.ToArray();
        if (!EditorApplication.ExecuteMenuItem("MIHLab/Make scene hand-ready"))
        {
            throw new InvalidOperationException("Could not run MIHLab > Make scene hand-ready.");
        }
        // That menu leaves the canvases that still need Meta's quick actions selected.
    }

    // ---------------------------------------------------------------- 4. Walk mover

    private static void AddMover()
    {
        Transform rig = FindRig();
        if (rig == null)
        {
            Debug.LogWarning($"{Tag} No camera rig found (no OVRCameraRig and nothing tagged Player). Add WalkAlongMover by hand.");
            return;
        }

        var mover = UnityEngine.Object.FindAnyObjectByType<WalkAlongMover>(FindObjectsInactive.Include);
        if (mover == null)
        {
            mover = Undo.AddComponent<WalkAlongMover>(rig.gameObject);
            Debug.Log($"{Tag} Added WalkAlongMover to {rig.name}.", rig);
        }
        var so = new SerializedObject(mover);
        so.FindProperty("rig").objectReferenceValue = rig;
        so.ApplyModifiedProperties();

        foreach (PhilosopherWalker w in UnityEngine.Object.FindObjectsByType<PhilosopherWalker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var wso = new SerializedObject(w);
            SerializedProperty p = wso.FindProperty("playerOverride");
            if (p != null && p.objectReferenceValue != rig)
            {
                p.objectReferenceValue = rig;
                wso.ApplyModifiedProperties();
                Debug.Log($"{Tag} {w.name}: Player Override set to {rig.name}.", w);
            }
        }
        Debug.Log($"{Tag} Walk mover moves '{rig.name}'.", rig);
    }

    private static Transform FindRig()
    {
        Type ovrRig = FindType("OVRCameraRig");
        if (ovrRig != null)
        {
            var c = UnityEngine.Object.FindAnyObjectByType(ovrRig, FindObjectsInactive.Exclude) as Component;
            if (c != null)
            {
                // If the rig sits inside a player object with a CharacterController (Meta's player
                // controller), move that object; otherwise move the rig itself.
                for (Transform t = c.transform; t != null; t = t.parent)
                {
                    if (t.GetComponent<CharacterController>() != null) return t;
                }
                return c.transform;
            }
        }
        GameObject player = GameObject.FindWithTag("Player");
        return player != null ? player.transform : null;
    }

    // ---------------------------------------------------------------- helpers

    private static GameObject FindPhilosopher(string namePart, bool requireWalker)
    {
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            GameObject go = t.gameObject;
            if (go.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) < 0) continue;
            bool isWalker = go.GetComponent<PhilosopherWalker>() != null;
            if (requireWalker && !isWalker) continue;
            if (isWalker || go.GetComponent<PhilosopherNPC>() != null || go.GetComponent<DialogueTrigger>() != null) return go;
        }
        return null;
    }

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
