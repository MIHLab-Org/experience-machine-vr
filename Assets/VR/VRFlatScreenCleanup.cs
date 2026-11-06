using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Switches off flat-screen-only UI when the game runs in a headset (Quest VR).
/// Today that is the Starter Assets on-screen joystick canvas (any object with
/// UICanvasControllerInput, e.g. UI_Canvas_StarterAssetsInputs_Joysticks): in VR it would float
/// in front of the player and do nothing, because movement is by hand (V8 / wrist Walk button).
///
/// No setup: it runs by itself after every scene load. It only disables objects (never deletes),
/// and only when an XR device is active or in an Android player build (this repo is the Quest
/// edition), so Editor play without a headset keeps the joysticks.
/// The "Press E to talk" hint is hidden separately in PhilosopherNPC.OnGUI.
/// </summary>
public static class VRFlatScreenCleanup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Cleanup();
        Watcher.Watch();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Cleanup();
        Watcher.Watch();
    }

    /// <summary>
    /// The headset (or the Meta XR Simulator) often becomes active a few frames after the scene
    /// loads, so a check at load time alone misses it. This keeps checking for a few seconds.
    /// </summary>
    private class Watcher : MonoBehaviour
    {
        private const float WatchSeconds = 10f;
        private static Watcher instance;
        private float timeLeft;

        public static void Watch()
        {
            if (instance == null)
            {
                var go = new GameObject("VRFlatScreenCleanup") { hideFlags = HideFlags.HideInHierarchy };
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Watcher>();
            }
            instance.timeLeft = WatchSeconds;
            instance.enabled = true;
        }

        private void Update()
        {
            if (InHeadset())
            {
                Cleanup();
                enabled = false;
                return;
            }
            timeLeft -= Time.unscaledDeltaTime;
            if (timeLeft <= 0f) enabled = false;
        }
    }

    private static bool InHeadset()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return XRSettings.isDeviceActive;
#endif
    }

    private static void Cleanup()
    {
        if (!InHeadset()) return;
        var joysticks = Object.FindObjectsByType<UICanvasControllerInput>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var j in joysticks)
        {
            j.gameObject.SetActive(false);
            Debug.Log($"[VRFlatScreenCleanup] Disabled flat-screen joystick canvas '{j.gameObject.name}' for VR.");
        }
    }
}
