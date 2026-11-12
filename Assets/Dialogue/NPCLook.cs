using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes one philosopher look different from others that share the same body (backlog C1),
/// with no new textures or meshes: clothing tints through MaterialPropertyBlock (the shared
/// materials are never changed), hair meshes switched on or off by name, and height
/// (0.92-1.08). Settings come from an NPCLookPreset; the C2 menu item
/// (MIHLab > Give selected philosophers a body) adds this component for you.
///
/// Renderer / hair names per body (read from the FBX files and their .meta import data):
///
///   Mr Herbert  (models/mr_herbert_charater/Commission Mr_ John Herbert ... 1_0.fbx, Character Creator rig)
///     skin      CC_Base_Body                      (never tinted)
///     clothing  robe  (material slots: default, default2, belt, pocket), Slippers_Socks
///     hair      Old_Long (long grey hair), RL_G6_Hair (short hair / beard layer)
///
///   shorts-shirt-guy  (models/shirts-shorts-guy/shorts-shirt-guy.fbx, Mixamo Ch42)
///     skin      Ch42_Body1, Ch42__Eyelashes       (never tinted)
///     clothing  Ch42_Shirt, Ch42_Shorts, Ch42_Sneakers
///     hair      Ch42_Hair1
///
///   business-lady  (models/business-lady.fbx, Mixamo Ch22; not in the build yet)
///     skin      Ch22_Body, Ch22_Eyelashes
///     clothing  Ch22_Shirt, Ch22_Pants, Ch22_Sneakers
///     hair      Ch22_Hair
///
///   hiker-lady  (models/hiker-lady.fbx, Mixamo Ch21; not in the build yet)
///     skin      Ch21_Body, Ch21_Eyelasshes        (sic, spelled that way in the FBX)
///     clothing  Ch21_Shirt, Ch21_Pants, Ch21_Shoes
///     hair      Ch21_Hair
///
/// The Mixamo bodies share one texture atlas across body and clothes, so a tint on the
/// Shirt renderer only changes the shirt. Names are matched exactly first, then as a
/// prefix ("Ch42_Hair" finds "Ch42_Hair1"). The look is applied in the Editor too, so
/// you see it without pressing Play.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class NPCLook : MonoBehaviour
{
    public const float MinHeight = 0.92f;
    public const float MaxHeight = 1.08f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP Lit
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in / legacy
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");     // URP Lit texture
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");     // Built-in / legacy texture
    private static readonly string[] SkinWords = { "Body", "Skin", "Eye", "Lash", "Teeth", "Tongue", "Nails", "Cornea", "Tearline" };

    [Tooltip("The look to apply.")]
    public NPCLookPreset preset;

    [Tooltip("The body model (child) to recolour and scale. Blank = this object.")]
    public Transform bodyRoot;

    [Tooltip("0 = use the preset's height.")]
    [Range(0f, MaxHeight)]
    public float heightOverride = 0f;

    [SerializeField, HideInInspector] private Vector3 baseScale;
    [SerializeField, HideInInspector] private bool baseScaleSaved;
    [SerializeField, HideInInspector] private Transform scaledRoot;

    private readonly List<Renderer> tinted = new List<Renderer>();
    private MaterialPropertyBlock block;

    private Transform Root => bodyRoot != null ? bodyRoot : transform;

    private void OnEnable() => Apply();

    private void OnValidate()
    {
        // OnValidate can run mid-import; defer to the next editor tick.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
#endif
    }

    private void OnDisable() => ClearTints();

    /// <summary>Re-applies colours, hair and height. Safe to call repeatedly.</summary>
    public void Apply()
    {
        Transform root = Root;
        if (root == null) return;

        if (!baseScaleSaved || scaledRoot != root)
        {
            // First run, or Body Root was changed: put the old root back, remember the new one.
            if (baseScaleSaved && scaledRoot != null) scaledRoot.localScale = baseScale;
            baseScale = root.localScale;
            scaledRoot = root;
            baseScaleSaved = true;
        }

        ClearTints();
        if (preset == null)
        {
            root.localScale = baseScale;
            return;
        }

        if (block == null) block = new MaterialPropertyBlock();

        foreach (NPCLookPreset.ColourSlot slot in preset.colours)
        {
            if (slot == null || string.IsNullOrEmpty(slot.rendererName)) continue;

            if (IsSkin(slot.rendererName))
            {
                Debug.LogWarning($"[NPCLook] {name}: '{slot.rendererName}' looks like skin; skipped (C1 recolours clothing only).", this);
                continue;
            }

            Renderer r = FindChild<Renderer>(root, slot.rendererName);
            if (r == null)
            {
                Debug.LogWarning($"[NPCLook] {name}: no renderer named '{slot.rendererName}' under {root.name}.", this);
                continue;
            }

            block.Clear();
            block.SetColor(BaseColorId, slot.colour);
            block.SetColor(ColorId, slot.colour);
            if (slot.plain)
            {
                block.SetTexture(BaseMapId, Texture2D.whiteTexture);
                block.SetTexture(MainTexId, Texture2D.whiteTexture);
            }

            if (slot.materialIndex >= 0 && slot.materialIndex < r.sharedMaterials.Length)
            {
                r.SetPropertyBlock(block, slot.materialIndex);
            }
            else
            {
                r.SetPropertyBlock(block);
            }
            tinted.Add(r);
        }

        foreach (string n in preset.show) SetActive(root, n, true);
        foreach (string n in preset.hide) SetActive(root, n, false);

        float h = heightOverride > 0f ? heightOverride : preset.height;
        h = Mathf.Clamp(h, MinHeight, MaxHeight);
        root.localScale = baseScale * h;
    }

    private void ClearTints()
    {
        foreach (Renderer r in tinted)
        {
            if (r == null) continue;
            r.SetPropertyBlock(null);
            for (int i = 0; i < r.sharedMaterials.Length; i++) r.SetPropertyBlock(null, i);
        }
        tinted.Clear();
    }

    private void SetActive(Transform root, string childName, bool active)
    {
        if (string.IsNullOrEmpty(childName)) return;
        Transform t = FindChild<Transform>(root, childName);
        if (t == null)
        {
            Debug.LogWarning($"[NPCLook] {name}: no child named '{childName}' under {root.name}.", this);
            return;
        }
        if (t.gameObject.activeSelf != active) t.gameObject.SetActive(active);
    }

    private static T FindChild<T>(Transform root, string childName) where T : Component
    {
        T prefix = null;
        foreach (T c in root.GetComponentsInChildren<T>(true))
        {
            string n = c.gameObject.name;
            if (string.Equals(n, childName, System.StringComparison.OrdinalIgnoreCase)) return c;
            if (prefix == null && n.StartsWith(childName, System.StringComparison.OrdinalIgnoreCase)) prefix = c;
        }
        return prefix;
    }

    private static bool IsSkin(string rendererName)
    {
        foreach (string w in SkinWords)
        {
            if (rendererName.IndexOf(w, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }
}
