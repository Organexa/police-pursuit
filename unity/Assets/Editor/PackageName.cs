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
        // The name under the icon on the phone; unset, Unity used "unity" and "DefaultCompany". Saves on Android
        // live under the package id above, so these can change without moving anyone's data.
        PlayerSettings.productName = "Police Pursuit";
        PlayerSettings.companyName = "Organexa";
        // The screens are laid out for a phone held sideways.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
    }
}
