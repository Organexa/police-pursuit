using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

// The PP_AUTOTEST define (which lets an Android launch start Core.AutoTest) is on only when Unity is started with
// -ppAutotest: the android-check workflow's test APK. Every other build, release included, is built without it.
[InitializeOnLoad]
static class AutoTestDefine
{
    const string Define = "PP_AUTOTEST";

    static AutoTestDefine()
    {
        bool wanted = Environment.GetCommandLineArgs().Contains("-ppAutotest");
        var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Split(';')
            .Where(d => d.Length > 0).ToList();
        if (wanted == defines.Contains(Define)) return;
        if (wanted) defines.Add(Define); else defines.Remove(Define);
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, string.Join(";", defines));
    }
}
