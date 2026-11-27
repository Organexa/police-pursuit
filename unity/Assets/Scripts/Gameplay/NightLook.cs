using PolicePursuit.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PolicePursuit.Gameplay
{
    // Night sky, fog and the post effects, which follow the graphics preset.
    public static class NightLook
    {
        public static readonly Color Sky = Shapes.Hex(0x0B1030);
        const float FogStart = 90f, FogEnd = 300f; // civilian cars appear 120-260 m ahead and stay visible

        public static (bool bloom, bool motionBlur) Effects(GfxPreset p) =>
            (p != GfxPreset.Low, p == GfxPreset.High);

        static Bloom bloom;
        static MotionBlur blur;

        public static void Apply(GfxPreset p)
        {
            // Our own fog in Flat.shader: Unity's fog variants are stripped when the scene has none.
            Shader.SetGlobalColor("_NightFog", Sky);
            Shader.SetGlobalVector("_NightFogRange", new Vector4(FogStart, 1f / (FogEnd - FogStart)));
            if (bloom == null) Build();
            (bloom.active, blur.active) = Effects(p);
        }

        static void Build()
        {
            var go = new GameObject("Night Volume");
            Object.DontDestroyOnLoad(go);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            var colour = profile.Add<ColorAdjustments>();
            colour.contrast.Override(10);
            colour.saturation.Override(15);

            bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1f); // only colours brighter than white glow
            bloom.intensity.Override(1.2f);
            bloom.scatter.Override(0.65f);

            blur = profile.Add<MotionBlur>();
            blur.intensity.Override(0.25f);
        }
    }
}
