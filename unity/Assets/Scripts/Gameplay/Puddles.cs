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
            }
        }

        public void Show(bool raining)
        {
            foreach (var p in puddles) if (p.gameObject.activeSelf != raining) p.gameObject.SetActive(raining);
        }

        // Ten times a second is enough to follow a 0.2 s flash.
        void Update()
        {
            if (Time.time < nextPaint || puddles.Count == 0 || !puddles[0].gameObject.activeSelf) return;
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
