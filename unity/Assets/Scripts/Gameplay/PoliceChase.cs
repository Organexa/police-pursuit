using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Drives a police VehicleController after a target: straight at it when in sight,
    // otherwise crossroads to crossroads along the open world's road grid.
    // shortcut: grid route only (no NavMesh, no traffic); upgrade with a real road network.
    public class PoliceChase : MonoBehaviour
    {
        const float NodeReached = 9; // metres from a crossroads centre
        public const float StuckAfter = 1.5f, ReverseFor = 1.5f;

        public Transform Target { get; set; } // the player's car, or the player on foot
        public bool InSight { get; private set; }
        VehicleController car;
        Vector2Int next;
        bool routing;
        float stuckTime, reverseLeft;

        void Awake()
        {
            car = GetComponent<VehicleController>();
            car.SpeedLimit = VehicleController.TopSpeed * 0.9f;
        }

        public static Vector2Int Node(Vector3 p) => new Vector2Int(Index(p.x), Index(p.z));
        static int Index(float v) => Mathf.Clamp(Mathf.RoundToInt(v / OpenWorld.Pitch + OpenWorld.Blocks / 2f), 0, OpenWorld.Blocks);
        public static Vector3 Centre(Vector2Int n) =>
            new Vector3((n.x - OpenWorld.Blocks / 2f) * OpenWorld.Pitch, 0, (n.y - OpenWorld.Blocks / 2f) * OpenWorld.Pitch);

        // One crossroads towards the goal, along the axis with the larger gap: always a straight road on this grid.
        public static Vector2Int Step(Vector2Int from, Vector2Int to)
        {
            var d = to - from;
            if (d == Vector2Int.zero) return from;
            return Mathf.Abs(d.x) >= Mathf.Abs(d.y)
                ? from + new Vector2Int(System.Math.Sign(d.x), 0)
                : from + new Vector2Int(0, System.Math.Sign(d.y));
        }

        public void ResetChase()
        {
            routing = false;
            stuckTime = reverseLeft = 0;
        }

        void FixedUpdate() // steer every physics step: long frames would otherwise skip corners
        {
            car.Flash(true);
            if (Target == null) { car.Pedal = car.Steer = 0; return; } // nobody to chase: stop

            var from = transform.position + Vector3.up;
            var to = Target.position + Vector3.up;
            // Queries ignore the collider they start inside, so the police car's own hull is not hit.
            InSight = !Physics.Linecast(from, to, out var hit) || hit.transform == Target || (hit.rigidbody != null && hit.rigidbody.transform == Target);

            Vector3 goal;
            if (InSight)
            {
                goal = Target.position;
                routing = false;
            }
            else
            {
                if (!routing) { next = Node(transform.position); routing = true; }
                if (Flat(Centre(next) - transform.position) < NodeReached)
                    next = Step(next, Node(Target.position));
                goal = Centre(next);
            }
            Steer(car, goal, InSight, Time.deltaTime, ref stuckTime, ref reverseLeft);
        }

        // Shared by every AI car: aim at `goal`, brake for corners, back out when stuck.
        // `gentle` closes in slowly instead of ramming.
        public static void Steer(VehicleController car, Vector3 goal, bool gentle, float dt, ref float stuckTime, ref float reverseLeft)
        {
            var transform = car.transform;
            var local = transform.InverseTransformPoint(goal);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float speed = car.Speed;

            if (reverseLeft > 0)
            {
                reverseLeft -= dt;
                car.Pedal = -1;
                car.Steer = -Mathf.Sign(angle); // back out turning the other way
                return;
            }

            float steer = Mathf.Clamp(angle / 25f, -1, 1);
            bool sharpTurn = Mathf.Abs(angle) > 50;
            float distance = Flat(goal - transform.position);
            float pedal = sharpTurn && speed > 10 ? -0.6f // brake into a corner
                : gentle && distance < 8 ? (speed > 2 ? 0 : 0.5f) // creep in at walking pace, no ramming at speed
                : 1;

            // Traffic in the way: swerve to whichever side of it is nearer, and ease off when it is close.
            // The cast starts outside the car's own hull, which it would otherwise hit.
            float look = Mathf.Clamp(Mathf.Abs(speed) * 1.2f, 8, 25);
            var eye = transform.position + Vector3.up * 0.8f + transform.forward * 2.3f;
            // People on a crossing (only they are on a layer this cast sees) get a harder swerve and a full brake.
            if (Physics.SphereCast(eye, 1f, transform.forward, out var hit, look))
            {
                bool person = hit.collider.GetComponentInParent<Pedestrian>() != null;
                if (person || hit.collider.GetComponentInParent<CivilCar>() != null)
                {
                    float side = transform.InverseTransformPoint(hit.collider.bounds.center).x;
                    steer = Mathf.Clamp(steer - Mathf.Sign(side) * (person ? 1 : 0.8f), -1, 1);
                    if (person && hit.distance < 12 && speed > 4) pedal = -1;
                    else if (hit.distance < 6 && speed > 6) pedal = -0.5f;
                }
            }
            car.Steer = steer;
            car.Pedal = pedal;

            stuckTime = car.Pedal > 0 && Mathf.Abs(speed) < 1 ? stuckTime + dt : 0;
            if (stuckTime > StuckAfter) { stuckTime = 0; reverseLeft = ReverseFor; }
        }

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
