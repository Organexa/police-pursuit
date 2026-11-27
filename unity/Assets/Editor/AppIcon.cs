using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Sets the app icon at build time (no committed ProjectSettings.asset; the images are not imported yet
// when editor scripts first load on a clean CI machine).
class AppIcon : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report) => Apply();

    public static void Apply()
    {
        var flat = Load("icon");
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { flat }, IconKind.Any);
        // Android's own slots: two layers for the adaptive icon, otherwise the flat picture.
        foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var icon in icons)
                icon.SetTextures(icon.maxLayerCount > 1
                    ? new[] { Load("background"), Load("foreground") }
                    : new[] { flat });
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }
    }

    static Texture2D Load(string name)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icon/" + name + ".png");
        if (texture == null) throw new BuildFailedException("App icon image missing: Assets/Icon/" + name + ".png");
        return texture;
    }
}
