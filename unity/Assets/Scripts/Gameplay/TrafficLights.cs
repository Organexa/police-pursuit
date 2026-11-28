using System.Collections.Generic;
using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // A light at every crossroads: north-south green, amber, then east-west green, amber. Each crossroads runs
    // a few seconds out of step with its neighbours so the whole grid does not change at once.
    // Only civilian cars obey them; the police in pursuit and the player do not have to.
    public class TrafficLights : MonoBehaviour
    {
        public enum Light { Green, Amber, Red }
        public const float GreenFor = 8, AmberFor = 2, Cycle = 2 * (GreenFor + AmberFor);

        static readonly Color GreenOn = Color.green, AmberOn = new Color(1, 0.7f, 0), RedOn = Color.red;
        readonly List<(Vector2Int node, Vector2Int from, Renderer lamp)> lamps = new List<(Vector2Int, Vector2Int, Renderer)>();
        readonly Dictionary<Renderer, Light> shown = new Dictionary<Renderer, Light>();

        public static float Offset(Vector2Int node) => (node.x * 3 + node.y * 7) % Cycle;

        // What a car arriving at `node` from the crossroads `from` sees at `time`.
        public static Light For(Vector2Int node, Vector2Int from, float time)
        {
            bool northSouth = from.x == node.x;
            float t = ((time + Offset(node)) % Cycle + Cycle) % Cycle;
            float mine = northSouth ? t : (t + GreenFor + AmberFor) % Cycle; // east-west runs half a cycle later
            return mine < GreenFor ? Light.Green : mine < GreenFor + AmberFor ? Light.Amber : Light.Red;
        }

        // Seconds of red left for cars arriving from `from` (0 when it is not red).
        public static float RedLeft(Vector2Int node, Vector2Int from, float time)
        {
            if (For(node, from, time) != Light.Red) return 0;
            bool northSouth = from.x == node.x;
            float t = ((time + Offset(node)) % Cycle + Cycle) % Cycle;
            float mine = northSouth ? t : (t + GreenFor + AmberFor) % Cycle;
            return Cycle - mine;
        }

        // Stop for red; for amber too unless already too close to stop comfortably.
        public static bool MustStop(Light light, float toLine) => light == Light.Red || (light == Light.Amber && toLine > 4);

        public void Build()
        {
            for (int x = 0; x <= OpenWorld.Blocks; x++)
            for (int z = 0; z <= OpenWorld.Blocks; z++)
            {
                var node = new Vector2Int(x, z);
                foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    var from = node + d;
                    if (from.x < 0 || from.y < 0 || from.x > OpenWorld.Blocks || from.y > OpenWorld.Blocks) continue;
                    // On the kerb at the stop line, on the arriving car's right, at head height for the camera.
                    var line = CivilCar.LaneEnd(from, node);
                    var dir = (PoliceChase.Centre(node) - PoliceChase.Centre(from)).normalized;
                    var at = line + Vector3.Cross(Vector3.up, dir) * (OpenWorld.Road / 2 - CivilCar.Lane - 0.3f) + Vector3.up * 3;
                    Shapes.Box(transform, at - Vector3.up * 1.5f, new Vector3(0.12f, 3, 0.12f), Shapes.Dark);
                    var lamp = Shapes.Box(transform, at, new Vector3(0.35f, 0.35f, 0.35f), GreenOn, glow: 1.5f).GetComponent<Renderer>();
                    lamps.Add((node, from, lamp));
                }
            }
        }

        public int LampCount => lamps.Count;

        // Repaints only lamps that changed, a few times a second.
        float nextPaint;
        void Update()
        {
            if (Time.time < nextPaint) return;
            nextPaint = Time.time + 0.2f;
            foreach (var (node, from, lamp) in lamps)
            {
                var light = For(node, from, Time.time);
                if (shown.TryGetValue(lamp, out var was) && was == light) continue;
                shown[lamp] = light;
                Shapes.Paint(lamp, light == Light.Green ? GreenOn : light == Light.Amber ? AmberOn : RedOn, 1.5f);
            }
        }
    }
}
