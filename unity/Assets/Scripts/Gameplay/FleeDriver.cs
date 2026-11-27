using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // An escaping car: at each crossroads it takes the road that leads furthest from its pursuer, never straight back.
    public class FleeDriver : MonoBehaviour
    {
        public Transform From { get; set; } // the pursuer
        VehicleController car;
        Vector2Int next, prev;
        float stuckTime, reverseLeft;

        void Awake()
        {
            car = GetComponent<VehicleController>();
            car.SpeedLimit = VehicleController.TopSpeed * 0.8f; // slower than the player, so it can be caught
        }

        public void StartAt(Vector2Int from, Vector2Int towards)
        {
            prev = from;
            next = towards;
            stuckTime = reverseLeft = 0;
        }

        public static Vector2Int Choose(Vector2Int at, Vector2Int prev, Vector3 pursuer)
        {
            Vector2Int best = prev;
            float bestDistance = -1;
            foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                var n = at + d;
                if (n == prev || n.x < 0 || n.y < 0 || n.x > OpenWorld.Blocks || n.y > OpenWorld.Blocks) continue;
                float distance = Vector3.Distance(PoliceChase.Centre(n), pursuer);
                if (distance > bestDistance) { best = n; bestDistance = distance; }
            }
            return best;
        }

        void Update()
        {
            if (From == null) return;
            var centre = PoliceChase.Centre(next);
            if (Vector2.Distance(new Vector2(centre.x, centre.z), new Vector2(transform.position.x, transform.position.z)) < 9)
            {
                var chosen = Choose(next, prev, From.position);
                prev = next;
                next = chosen;
            }
            PoliceChase.Steer(car, PoliceChase.Centre(next), false, Time.deltaTime, ref stuckTime, ref reverseLeft);
        }

        public void Park()
        {
            enabled = false;
            car.Pedal = car.Steer = 0;
            car.Handbrake = true;
        }
    }
}
