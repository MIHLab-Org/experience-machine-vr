using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A reusable look for a philosopher's body (backlog C1): clothing colours, which hair
/// meshes are shown, and height. Create one with Assets > Create > MIHLab > NPC Look Preset
/// and drag it onto an NPCLook component. Several philosophers can share a preset.
/// Renderer and hair names for each body are listed in NPCLook.cs.
/// </summary>
[CreateAssetMenu(menuName = "MIHLab/NPC Look Preset", fileName = "Look_")]
public class NPCLookPreset : ScriptableObject
{
    [Serializable]
    public class ColourSlot
    {
        [Tooltip("Child renderer name, e.g. Ch42_Shirt or robe. Clothing only; skin renderers are skipped.")]
        public string rendererName;
        [Tooltip("-1 = whole renderer. Otherwise only this material slot (e.g. the robe's belt).")]
        public int materialIndex = -1;
        public Color colour = Color.white;
        [Tooltip("Plain: drop the texture so the colour shows exactly (for a shirt whose texture is already strongly coloured). Off = tint over the texture.")]
        public bool plain = false;
    }

    [Tooltip("Which body this preset was made for (herbert, shorts-shirt-guy, business-lady, hiker-lady). Informational.")]
    public string body = "herbert";

    [Tooltip("Tints multiply the texture, so mid and light colours read best; white = unchanged.")]
    public List<ColourSlot> colours = new List<ColourSlot>();

    [Tooltip("Child objects to show (e.g. Old_Long for long hair).")]
    public List<string> show = new List<string>();

    [Tooltip("Child objects to hide (e.g. RL_G6_Hair, or Ch42_Hair1 for a bald figure).")]
    public List<string> hide = new List<string>();

    [Range(NPCLook.MinHeight, NPCLook.MaxHeight)]
    public float height = 1f;
}
