using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Creates and assigns the URP assets, so a clean CI machine builds with them and none is committed from a
// newer local editor. The folder is gitignored.
[InitializeOnLoad]
class RenderSetup : IPreprocessBuildWithReport
{
    const string Folder = "Assets/Settings";
    const string AssetPath = Folder + "/Urp.asset";

    static RenderSetup() => Apply();

    // First of all preprocessors: URP's own ones read the assigned pipeline.
    public int callbackOrder => int.MinValue;

    public void OnPreprocessBuild(BuildReport report)
    {
        Apply();
        if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset))
            throw new BuildFailedException("The URP pipeline could not be set up.");
    }

    public static void Apply()
    {
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
        if (asset == null)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Settings");
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            AssetDatabase.CreateAsset(renderer, Folder + "/UrpRenderer.asset");
            asset = UniversalRenderPipelineAsset.Create(renderer);
            asset.msaaSampleCount = 2;
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
        }
        GraphicsSettings.defaultRenderPipeline = asset;
        // The effects are added from code, so the build cannot tell which variants are used: keep them all.
        var stripping = GraphicsSettings.GetRenderPipelineSettings<URPShaderStrippingSetting>();
        if (stripping != null) stripping.stripUnusedPostProcessingVariants = false;
    }
}
