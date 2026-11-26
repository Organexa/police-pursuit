using PolicePursuit.Core;
using UnityEditor;
using UnityEditor.Build;

// Player settings, set on every editor load so a CI build has them without a committed ProjectSettings.asset.
[InitializeOnLoad]
static class PackageName
{
    static PackageName()
    {
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, UpdateCheck.PackageName);
        // The screens are laid out for a phone held sideways.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
    }
}
