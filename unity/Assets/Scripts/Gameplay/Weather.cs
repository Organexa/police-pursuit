using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Passing showers over the open world: dry, then rain, on a fixed cycle (tests can force it).
    // While it rains, falling-rain particles follow the camera, the fog closes in, and wet prints wash away.
    public class Weather : MonoBehaviour
    {
        public const float DryFor = 180, RainFor = 90, Cycle = DryFor + RainFor, RainFog = 0.7f;
        public bool Raining { get; private set; }
        public bool? Forced { get; set; } // null: follow the cycle
        public ParticleSystem Rain { get; private set; }
        float clock;

        public static bool RainingAt(float seconds) => ((seconds % Cycle) + Cycle) % Cycle >= DryFor;

        public void Init(int drops)
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            Rain = go.AddComponent<ParticleSystem>();
            Rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = Rain.main;
            main.loop = true;
            main.startLifetime = 1.2f;
            main.startSpeed = 0;
            main.startSize = 0.04f;
            main.startColor = new Color(0.7f, 0.8f, 1f, 0.5f);
            main.maxParticles = drops;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 2.5f;
            var emission = Rain.emission;
            emission.rateOverTime = drops / 1.2f;
            var shape = Rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40, 0.1f, 40);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 6;
            // The game's own shader (always in the build via Resources); a shader found by name can be stripped on Android.
            r.material = new Material(Resources.Load<Shader>("Flat"));
            r.material.SetColor("_Color", new Color(0.45f, 0.55f, 0.75f));
        }

        public void ResetWeather()
        {
            clock = 0;
            Forced = null;
            Apply(false);
        }

        void Apply(bool raining)
        {
            if (raining == Raining && (Rain.isPlaying == raining)) return;
            Raining = raining;
            if (raining) Rain.Play(); else Rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            NightLook.Fog(raining ? RainFog : 1);
        }

        void Update()
        {
            clock += Time.deltaTime;
            Apply(Forced ?? RainingAt(clock));
            var cam = Camera.main;
            if (cam != null) Rain.transform.position = cam.transform.position + cam.transform.forward * 10 + Vector3.up * 15;
        }

        void OnDisable()
        {
            if (Rain != null) Rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Raining = false;
            NightLook.Fog(1); // the endless run keeps its normal night
        }
    }
}
