using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Prepares Assets/ThirdParty (not in this repository) for phones: URP materials, smaller textures,
// readable car meshes.
class ThirdPartyImport : AssetPostprocessor
{
    public const string Root = "Assets/ThirdParty";

    void OnPreprocessTexture()
    {
        if (assetPath.StartsWith(Root)) ((TextureImporter)assetImporter).maxTextureSize = 1024;
    }

    void OnPreprocessModel()
    {
        // The cars are merged into one mesh when the game runs.
        if (assetPath.StartsWith(Root + "/Car")) ((ModelImporter)assetImporter).isReadable = true;
    }

    public static void ConvertMaterials()
    {
        if (!AssetDatabase.IsValidFolder(Root)) return;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null || m.shader == lit) continue;
            Convert(m, lit);
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
    }

    static void Convert(Material m, Shader lit)
    {
        Texture Read(string n) => m.HasProperty(n) ? m.GetTexture(n) : null;
        var main = Read("_MainTex");
        var bump = Read("_BumpMap");
        var metal = Read("_MetallicGlossMap");
        var glow = Read("_EmissionMap");
        var colour = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
        var emission = Color.black;
        float smoothness = 0.2f;
        string name = m.name;

        if (name.StartsWith("asphalt")) { colour *= 0.7f; smoothness = 0.62f; }
        else if (name.StartsWith("sideway")) { colour *= 0.45f; smoothness = 0.4f; }
        else if (name.EndsWith("_light"))
        {
            // Lamp glass. The white one also covers shop fronts, so it stays faint.
            var lamp = name.StartsWith("blue") ? new Color(0.2f, 0.5f, 1) : name.StartsWith("pink") ? new Color(1, 0.2f, 0.8f)
                : name.StartsWith("orange") ? new Color(1, 0.55f, 0.15f) : new Color(1, 0.72f, 0.42f);
            emission = lamp * (name.StartsWith("white") ? 0.5f : 2.5f);
            colour = lamp * 0.2f;
            glow = null;
        }
        else if (name == "building_interior" || name == "building_windows_wet")
        {
            colour = new Color(0.12f, 0.12f, 0.16f);
            glow = main;
            emission = name == "building_interior" ? new Color(0.28f, 0.2f, 0.1f) : new Color(0.12f, 0.13f, 0.2f);
        }
        else if (name.EndsWith("_Paint")) { colour = new Color(0.7f, 0.02f, 0.03f); main = null; smoothness = 0.88f; }
        else if (name.EndsWith("_Glass") || name.EndsWith("_Windows")) { colour = new Color(0.03f, 0.04f, 0.06f); main = null; smoothness = 0.95f; }
        else if (name.StartsWith("RMCar")) { if (glow != null) emission = Color.white * 1.8f; }
        else
        {
            colour *= 0.8f;
            if (glow != null) emission = Color.white * 0.55f;
        }

        m.shader = lit;
        colour.a = 1;
        m.SetColor("_BaseColor", colour);
        m.SetTexture("_BaseMap", main);
        m.SetFloat("_Smoothness", smoothness);
        if (name.EndsWith("_Paint")) m.SetFloat("_Metallic", 0.6f);
        if (bump != null) { m.SetTexture("_BumpMap", bump); m.EnableKeyword("_NORMALMAP"); }
        if (metal != null) { m.SetTexture("_MetallicGlossMap", metal); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
        if (emission != Color.black)
        {
            m.SetTexture("_EmissionMap", glow);
            m.SetColor("_EmissionColor", emission);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
    }
}

[InitializeOnLoad]
class ThirdPartyMaterials : IPreprocessBuildWithReport
{
    static ThirdPartyMaterials() => EditorApplication.delayCall += ThirdPartyImport.ConvertMaterials;

    public int callbackOrder => int.MinValue + 1;

    public void OnPreprocessBuild(BuildReport report) => ThirdPartyImport.ConvertMaterials();
}
