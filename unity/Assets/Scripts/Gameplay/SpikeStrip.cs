using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // A spike strip across one lane of the road beyond the next crossroads in the player's path. Driving over it
    // blows the tyres: top speed halved until the player starts over. The other lane is always left clear.
    // shortcut: a striped flat box; no deflating-tyre visuals or sound.
    public class SpikeStrip : MonoBehaviour
    {
        public const float FirstAfter = 35, Every = 50, MinSpeed = 8, Lasts = 30, ClearBehind = 60;
        public const float LaneLength = 5, Depth = 0.8f, Inner = 1, PuncturedScale = 0.5f; // metres across from the centre line
        public float Length { get; private set; } = LaneLength; // a full-width strip (at crossroads) is longer
        public bool Active => strip.activeSelf;
        // Dropped from the helicopter: it falls for a moment and only bites once it is down.
        public const float FallFor = 0.8f;
        public bool Falling => fallLeft > 0;
        float fallLeft, fallFrom;
        public Vector3 Centre { get; private set; }
        public Vector3 Along { get; private set; } // direction of travel the strip faces
        GameObject strip;
        float sinceStart, nextAt = FirstAfter, upSince;

        public void Init(float length = LaneLength)
        {
            Length = length;
            strip = new GameObject("Spike strip");
            strip.transform.SetParent(transform, false);
            for (int i = 0; i < 10; i++) // dark base with bright teeth, readable from a distance at night
                Shapes.Box(strip.transform, new Vector3((i - 4.5f) * Length / 10, 0.05f, 0), new Vector3(Length / 10 * 0.9f, 0.1f, Depth),
                    i % 2 == 0 ? Shapes.Hex(0x22252C) : Color.yellow, glow: i % 2 == 0 ? 0 : 0.8f);
            strip.SetActive(false);
        }

        public void ResetStrip()
        {
            strip.SetActive(false);
            sinceStart = 0;
            nextAt = FirstAfter;
        }

        // On the right-hand lane of the road from `from` to `to`, `at` of the way along (a third by default).
        // fullWidth: across both lanes, centred on the road (used at crossroads, where another exit is left open).
        public void Spawn(Vector2Int from, Vector2Int to, float at = 1 / 3f, bool fullWidth = false)
        {
            var a = PoliceChase.Centre(from);
            var dir = (PoliceChase.Centre(to) - a).normalized;
            var across = Vector3.Cross(Vector3.up, dir);
            Along = dir;
            Centre = a + dir * OpenWorld.Pitch * at + (fullWidth ? Vector3.zero : across * (Inner + Length / 2));
            strip.transform.position = transform.TransformPoint(Centre);
            strip.transform.rotation = Quaternion.LookRotation(dir);
            strip.SetActive(true);
            upSince = 0;
            nextAt = sinceStart + Every;
        }

        public void Clear()
        {
            strip.SetActive(false);
            fallLeft = 0;
        }

        public void Drop(float fromHeight)
        {
            fallFrom = fromHeight;
            fallLeft = FallFor;
            Fall(0);
        }

        void Update()
        {
            if (Falling) Fall(Time.deltaTime);
        }

        void Fall(float dt)
        {
            fallLeft = Mathf.Max(0, fallLeft - dt);
            var p = strip.transform.position;
            strip.transform.position = new Vector3(p.x, transform.position.y + fallFrom * (fallLeft / FallFor), p.z);
        }

        // True when a point is over the strip (the car's centre is close enough: its wheels are wider than that).
        public bool Over(Vector3 p)
        {
            if (!Active || Falling) return false;
            var local = Quaternion.Inverse(Quaternion.LookRotation(Along)) * (p - Centre);
            return Mathf.Abs(local.x) < Length / 2 + 0.6f && Mathf.Abs(local.z) < Depth / 2 + 1.2f;
        }

        public void Tick(VehicleController player, bool chased, float dt)
        {
            sinceStart += dt;
            if (Active)
            {
                upSince += dt;
                float past = Vector3.Dot(player.transform.position - Centre, Along);
                if (!chased || upSince > Lasts || past > ClearBehind) Clear();
                return;
            }
            if (!chased || sinceStart < nextAt || Mathf.Abs(player.Speed) < MinSpeed) return;
            if (RoadBlock.Plan(player.transform.position, player.Body.linearVelocity, out var a, out var b)) Spawn(a, b);
            else nextAt = sinceStart + 2;
        }
    }
}
