using System.Collections.Generic;
using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Puddles on the roads while it rains. They fake a reflection of police lights: a puddle near a flashing
    // light bar takes on its red or blue, stronger the closer it is.
    // shortcut: tinted flat boxes, not real reflections (a planar reflection pass is too heavy for phones).
    public class Puddles : MonoBehaviour
    {
        public const int Count = 24;
        public const float Range = 15;
        static readonly Color Water = Shapes.Hex(0x1A2230);
        public readonly List<VehicleController> Lights = new List<VehicleController>(); // anything with a light bar
        // Driving through one faster than this throws up spray; the player's car also makes a sound.
        public const float SplashSpeed = 5, SplashGap = 0.5f;
        public readonly List<VehicleController> Drivers = new List<VehicleController>();
        public CivilTraffic Traffic { get; set; }
        public VehicleController PlayerCar { get; set; }
        public event System.Action PlayerSplashed;
        public int Splashes { get; private set; }
        ParticleSystem spray;
        readonly List<float> splashedAt = new List<float>();
        readonly List<Renderer> puddles = new List<Renderer>();
        readonly List<float> strength = new List<float>();
        float nextPaint;

        public int Showing { get { int n = 0; foreach (var p in puddles) if (p.gameObject.activeSelf) n++; return n; } }
        public Vector3 Position(int i) => puddles[i].transform.position;
        public float Reflecting(int i) => strength[i]; // 0 plain water .. 1 right beside a flashing light

        public void Build()
        {
            var random = new System.Random(21);
            for (int i = 0; i < Count; i++)
            {
                // On a lane somewhere along one of the roads, between crossroads.
                int line = random.Next(OpenWorld.Blocks + 1);
                float along = (random.Next(OpenWorld.Blocks * 2) - OpenWorld.Blocks + 0.5f) * OpenWorld.Pitch / 2;
                float across = (line - OpenWorld.Blocks / 2f) * OpenWorld.Pitch + (random.Next(2) == 0 ? -1 : 1) * CivilCar.Lane;
                bool northSouth = random.Next(2) == 0;
                var pos = northSouth ? new Vector3(across, 0.06f, along) : new Vector3(along, 0.06f, across);
                var size = new Vector3(1.5f + (float)random.NextDouble() * 2, 0.01f, 1.5f + (float)random.NextDouble() * 2);
                var p = Shapes.Box(transform, pos, size, Water).GetComponent<Renderer>();
                p.name = "Puddle";
                p.gameObject.SetActive(false);
                puddles.Add(p);
                strength.Add(0);
                splashedAt.Add(-99);
            }
            BuildSpray();
        }

        void BuildSpray()
        {
            var go = new GameObject("Spray");
            go.transform.SetParent(transform, false);
            spray = go.AddComponent<ParticleSystem>();
            spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = spray.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.6f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2, 5);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.maxParticles = 300;
            main.gravityModifier = 1.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = spray.emission;
            emission.enabled = false; // bursts only, by Emit
            var shape = spray.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.6f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = new Material(Resources.Load<Shader>("Flat")); // always in the build
            r.material.SetColor("_Color", new Color(0.55f, 0.65f, 0.8f));
        }

        public void Show(bool raining)
        {
            foreach (var p in puddles) if (p.gameObject.activeSelf != raining) p.gameObject.SetActive(raining);
        }

        bool InPuddle(int i, Vector3 at)
        {
            var b = puddles[i].bounds;
            return at.x > b.min.x && at.x < b.max.x && at.z > b.min.z && at.z < b.max.z;
        }

        void CheckSplash(Vector3 at, float speed, bool player)
        {
            if (speed < SplashSpeed) return;
            for (int i = 0; i < puddles.Count; i++)
            {
                if (Time.time - splashedAt[i] < SplashGap || !InPuddle(i, at)) continue;
                splashedAt[i] = Time.time;
                Splashes++;
                spray.transform.position = new Vector3(at.x, 0.1f, at.z);
                spray.Emit(Mathf.Clamp((int)(speed * 2), 10, 60));
                if (player) PlayerSplashed?.Invoke();
            }
        }

        // Ten times a second is enough to follow a 0.2 s flash.
        void Update()
        {
            if (puddles.Count == 0 || !puddles[0].gameObject.activeSelf) return;
            foreach (var d in Drivers)
                if (d != null && d.gameObject.activeInHierarchy) CheckSplash(d.transform.position, Mathf.Abs(d.Speed), d == PlayerCar);
            if (Traffic != null)
                foreach (var c in Traffic.Cars)
                    if (c.gameObject.activeSelf) CheckSplash(c.transform.position, c.Crashed ? c.Body.linearVelocity.magnitude : c.Speed, false);
            if (Time.time < nextPaint) return;
            nextPaint = Time.time + 0.1f;
            for (int i = 0; i < puddles.Count; i++)
            {
                var at = puddles[i].transform.position;
                Color best = Color.clear;
                float near = 0;
                foreach (var car in Lights)
                {
                    if (car == null || !car.gameObject.activeInHierarchy) continue;
                    var colour = car.LightColour;
                    if (colour.a == 0) continue;
                    float k = 1 - Vector3.Distance(at, car.transform.position) / Range;
                    if (k > near) { near = k; best = colour; }
                }
                strength[i] = near;
                Shapes.Paint(puddles[i], near > 0 ? Color.Lerp(Water, best, near) : Water, near * 1.5f);
            }
        }
    }
}
