using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // A police helicopter that joins at wanted level 4: it hovers high over the spot it is watching and shines a
    // searchlight down on it. That spot trails the player at a capped speed, so a fast car can leave the beam behind.
    // shortcut: placeholder boxes for the body and rotor; no sound.
    public class Helicopter : MonoBehaviour
    {
        public const int FromWanted = 4;
        public const float Height = 30, TrackSpeed = 18, FlyInSpeed = 25; // metres, m/s (player tops 50), m/s flying in
        public const float Lead = 1, MaxLead = 15; // the beam aims where you will be in about a second, at most 15 m ahead
        public Vector3 Watching { get; private set; } // ground point under the beam
        public bool Overhead { get; private set; } // arrived above its spot, beam on
        public Light Beam { get; private set; }
        Transform rotor;

        public static Helicopter Create(Transform parent)
        {
            var go = new GameObject("Helicopter");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<Helicopter>();
            Shapes.Box(go.transform, Vector3.zero, new Vector3(2.2f, 1.6f, 4.5f), Shapes.Dark);
            Shapes.Box(go.transform, new Vector3(0, 0.2f, -4), new Vector3(0.4f, 0.4f, 4), Shapes.Dark); // tail
            h.rotor = Shapes.Box(go.transform, new Vector3(0, 1.1f, 0), new Vector3(9, 0.08f, 0.35f), Shapes.Navy).transform;
            Shapes.Box(go.transform, new Vector3(0.9f, -0.2f, 2.1f), Vector3.one * 0.3f, Shapes.Red, glow: 2); // nav light
            var beam = new GameObject("Searchlight").AddComponent<Light>();
            beam.transform.SetParent(go.transform, false);
            beam.transform.localPosition = new Vector3(0, -0.9f, 1.8f);
            beam.type = LightType.Spot;
            beam.spotAngle = 22;
            beam.range = Height * 2;
            beam.intensity = 40;
            beam.color = new Color(0.9f, 0.95f, 1f);
            beam.shadows = LightShadows.None; // phones: no extra shadow pass
            h.Beam = beam;
            go.SetActive(false);
            return h;
        }

        // Flies in from the district's edge, high up, towards wherever the player is.
        public void Arrive(Vector3 player)
        {
            var edge = new Vector3(-OpenWorld.Edge, Height, -OpenWorld.Edge);
            transform.position = edge;
            Watching = new Vector3(player.x, 0, player.z);
            Overhead = false;
            Beam.enabled = false;
            gameObject.SetActive(true);
        }

        public void Leave()
        {
            gameObject.SetActive(false);
            Overhead = false;
        }

        public static Vector3 Aim(Vector3 player, Vector3 velocity)
        {
            var ahead = Vector3.ClampMagnitude(new Vector3(velocity.x, 0, velocity.z) * Lead, MaxLead);
            return new Vector3(player.x, 0, player.z) + ahead;
        }

        public void Tick(Vector3 player, Vector3 velocity, float dt)
        {
            var target = Aim(player, velocity);
            Watching = Vector3.MoveTowards(Watching, target, TrackSpeed * dt);
            var over = Watching + Vector3.up * Height;
            transform.position = Vector3.MoveTowards(transform.position, over, (Overhead ? TrackSpeed * 1.5f : FlyInSpeed) * dt);
            Overhead = Overhead || Vector3.Distance(transform.position, over) < 2;
            Beam.enabled = Overhead;
            var flat = Vector3.ProjectOnPlane(over - transform.position, Vector3.up);
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), dt * 2);
            Beam.transform.rotation = Quaternion.LookRotation(Watching - Beam.transform.position);
            rotor.Rotate(0, 900 * dt, 0, Space.Self);
        }

        // True when the player stands or drives inside the lit circle.
        public bool InBeam(Vector3 player)
        {
            if (!Overhead) return false;
            float radius = Height * Mathf.Tan(Beam.spotAngle * 0.5f * Mathf.Deg2Rad);
            return Vector2.Distance(new Vector2(player.x, player.z), new Vector2(Watching.x, Watching.z)) < radius;
        }
    }
}
