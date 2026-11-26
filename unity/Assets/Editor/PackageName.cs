using PolicePursuit.Core;
using UnityEditor;
using UnityEditor.Build;

// Set on every editor load, so a CI build has it without a committed ProjectSettings.asset.
[InitializeOnLoad]
static class PackageName
{
    static PackageName() =>
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, UpdateCheck.PackageName);
}
