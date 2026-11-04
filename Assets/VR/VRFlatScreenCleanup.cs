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
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Cleanup();

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
