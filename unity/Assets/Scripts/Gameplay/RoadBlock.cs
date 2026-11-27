using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Two parked police cars angled across the road beyond the next crossroads in the player's path.
    // Always beatable: they leave a gap wide enough for a car on one side, and there is a turning before them.
    public class RoadBlock : MonoBehaviour
    {
        public const float FirstAfter = 20, Every = 45, MinSpeed = 8, Lasts = 25, ClearBehind = 60;
        public static readonly float[] Offsets = { -4.5f, 1.5f }; // across the road from its centre line, metres
        public const float Angle = 60; // degrees off the road's direction

        public bool Active => cars[0].gameObject.activeSelf;
        public Vector2Int From { get; private set; }
        public Vector2Int To { get; private set; }
        public Vector3 Centre { get; private set; }
        readonly VehicleController[] cars = new VehicleController[2];
        float sinceStart, nextAt = FirstAfter, upSince;

        public void Init()
        {
            for (int i = 0; i < cars.Length; i++)
            {
                cars[i] = VehicleController.Create(transform, Vector3.zero, Color.white, police: true);
                cars[i].name = "Roadblock car";
                cars[i].gameObject.SetActive(false);
            }
        }

        // The road just past the crossroads the player is heading for: from that crossroads one more along.
        // False when there is no such road (heading out of the district) or the player is barely moving.
        public static bool Plan(Vector3 pos, Vector3 velocity, out Vector2Int from, out Vector2Int to)
        {
            from = to = default;
            var flat = new Vector2(velocity.x, velocity.z);
            if (flat.magnitude < 1) return false;
            var step = Mathf.Abs(flat.x) > Mathf.Abs(flat.y)
                ? new Vector2Int((int)Mathf.Sign(flat.x), 0) : new Vector2Int(0, (int)Mathf.Sign(flat.y));
            // The next crossroads ahead: the nearest one, or one further if it is already behind.
            var near = PoliceChase.Node(pos);
            var toNear = PoliceChase.Centre(near) - pos;
            from = Vector2.Dot(new Vector2(toNear.x, toNear.z), step) > 0 ? near : near + step;
            to = from + step;
            bool inside(Vector2Int n) => n.x >= 0 && n.y >= 0 && n.x <= OpenWorld.Blocks && n.y <= OpenWorld.Blocks;
            return inside(from) && inside(to);
        }

        public void ResetBlock()
        {
            foreach (var c in cars) c.gameObject.SetActive(false);
            sinceStart = 0;
            nextAt = FirstAfter;
        }

        // Called every frame by OpenWorld while the police are after the player.
        public void Tick(VehicleController player, bool chased, float dt)
        {
            sinceStart += dt;
            if (Active)
            {
                upSince += dt;
                var past = Vector3.Dot(player.transform.position - Centre, (PoliceChase.Centre(To) - PoliceChase.Centre(From)).normalized);
                if (upSince > Lasts || past > ClearBehind || !chased) Clear();
                foreach (var c in cars) c.Flash(true);
                return;
            }
            if (!chased || sinceStart < nextAt || Mathf.Abs(player.Speed) < MinSpeed) return;
            if (Plan(player.transform.position, player.Body.linearVelocity, out var a, out var b)) Spawn(a, b);
            else nextAt = sinceStart + 2; // try again shortly
        }

        public void Spawn(Vector2Int from, Vector2Int to)
        {
            From = from;
            To = to;
            var a = PoliceChase.Centre(from);
            var dir = (PoliceChase.Centre(to) - a).normalized;
            var across = Vector3.Cross(Vector3.up, dir);
            Centre = a + dir * OpenWorld.Pitch / 2; // halfway along the road
            for (int i = 0; i < cars.Length; i++)
            {
                var pos = Centre + across * Offsets[i] + Vector3.up * 0.6f;
                cars[i].gameObject.SetActive(true);
                cars[i].Place(transform.TransformPoint(pos), Quaternion.LookRotation(dir).eulerAngles.y + (i == 0 ? Angle : -Angle));
                cars[i].Pedal = cars[i].Steer = 0;
                cars[i].Handbrake = true;
            }
            upSince = 0;
            nextAt = sinceStart + Every;
        }

        public void Clear()
        {
            foreach (var c in cars) c.gameObject.SetActive(false);
        }

        public VehicleController this[int i] => cars[i];
    }
}
