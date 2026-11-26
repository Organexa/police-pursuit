using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PolicePursuit.Core
{
    public enum GfxPreset { Low, Medium, High }

    public static class GraphicsPresets
    {
        const string Key = "gfx_preset";

        // shortcut: RAM thresholds are guesses, tune once real phones are measured.
        public static GfxPreset Pick(int systemMb, int graphicsMb)
        {
            if (systemMb < 3000 || graphicsMb < 1024) return GfxPreset.Low;
            return systemMb < 6000 ? GfxPreset.Medium : GfxPreset.High;
        }

        public static int MaxCars(GfxPreset p) => p == GfxPreset.Low ? 8 : p == GfxPreset.Medium ? 12 : 16;
        public static float RenderScale(GfxPreset p) => p == GfxPreset.Low ? 0.7f : p == GfxPreset.Medium ? 0.85f : 1f;
        public static int Fps(GfxPreset p) => p == GfxPreset.Low ? 30 : 60;

        public static GfxPreset Load()
        {
            int saved = PlayerPrefs.GetInt(Key, -1);
            return saved >= 0 && saved <= 2
                ? (GfxPreset)saved
                : Pick(SystemInfo.systemMemorySize, SystemInfo.graphicsMemorySize);
        }

        public static void Save(GfxPreset p) => PlayerPrefs.SetInt(Key, (int)p);

        public static void Apply(GfxPreset p)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Fps(p);
            // shortcut: in the editor this edits the URP asset itself; copy the asset per preset if that gets annoying.
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.renderScale = RenderScale(p);
        }
    }
}
