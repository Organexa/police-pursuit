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
            // Unity's own fog and light, for the models; the placeholder shader ignores both.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Sky;
            Fog(1);
            if (bloom == null) Build();
            (bloom.active, blur.active) = Effects(p);
        }

        // Fog distance: 1 is the normal night, less is thicker (rain).
        public static void Fog(float reach)
        {
            float start = FogStart * reach, end = FogEnd * reach;
            Shader.SetGlobalVector("_NightFogRange", new Vector4(start, 1f / (end - start)));
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = end;
        }

        // Moonlight, a pink fill from the city and something for car paint and wet road to mirror.
        static void Light(Transform parent)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.4f, 0.42f, 0.58f);
            foreach (var (angles, colour, power) in new[]
                { (new Vector3(35, -30, 0), new Color(0.7f, 0.76f, 1), 1.9f),
                  (new Vector3(15, 140, 0), new Color(1, 0.55f, 0.75f), 0.8f) })
            {
                var light = new GameObject("Night Light").AddComponent<UnityEngine.Light>();
                light.transform.SetParent(parent);
                light.transform.rotation = Quaternion.Euler(angles);
                light.type = LightType.Directional;
                light.color = colour;
                light.intensity = power;
            }
            var sky = new Cubemap(32, TextureFormat.RGBA32, true);
            for (int face = 0; face < 6; face++)
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        var c = face == 2 ? new Color(0.2f, 0.25f, 0.45f) : new Color(0.04f, 0.06f, 0.18f);
                        if (face != 2 && face != 3 && y > 13 && y < 17)
                            c = face == 0 ? new Color(1, 0.2f, 0.9f) : face == 1 ? new Color(0.1f, 0.9f, 1) : new Color(0.3f, 0.3f, 0.5f);
                        sky.SetPixel((CubemapFace)face, x, y, c);
                    }
            sky.Apply();
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = sky;
        }

        static void Build()
        {
            var go = new GameObject("Night Volume");
            Object.DontDestroyOnLoad(go);
            Light(go.transform);
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
