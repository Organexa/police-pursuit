using System.Collections.Generic;
using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Civilian cars for the open world: they keep to the right-hand lane, turn at random at each crossroads
    // (never straight back), stop for anything in front of them, and are knocked loose when hit hard.
    // They stop at the line for red (and for amber when there is room), then go on green.
    // shortcut: no give-way rules inside a crossroads; turning cars just cut across.
    public class CivilCar : MonoBehaviour
    {
        public const float Lane = 3.5f, NodeClear = 9, Cruise = 9, Brake = 15, Accel = 4, LookAhead = 10, CrashSpeed = 5;

        public Rigidbody Body { get; private set; }
        public float Speed { get; private set; }
        public bool Crashed { get; private set; }
        public float CrashedAt { get; private set; }
        Vector2Int from, to;
        bool toEnd; // false: heading for the segment's start point, true: for its end
        float blockedFor; // two cars nose to nose would wait for ever: after a while, creep on
        System.Random random;

        // Lane points of the road from crossroads a to b: start just past a, end just before b, on the right.
        public static Vector3 LaneStart(Vector2Int a, Vector2Int b) => Lane3(a, b, PoliceChase.Centre(a), NodeClear);
        public static Vector3 LaneEnd(Vector2Int a, Vector2Int b) => Lane3(a, b, PoliceChase.Centre(b), -NodeClear);

        static Vector3 Lane3(Vector2Int a, Vector2Int b, Vector3 at, float along)
        {
            var dir = (PoliceChase.Centre(b) - PoliceChase.Centre(a)).normalized;
            return at + dir * along + Vector3.Cross(Vector3.up, dir) * Lane;
        }

        public static Vector2Int Next(Vector2Int at, Vector2Int came, System.Random random)
        {
            var options = new List<Vector2Int>(3);
            foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                var n = at + d;
                if (n != came && n.x >= 0 && n.y >= 0 && n.x <= OpenWorld.Blocks && n.y <= OpenWorld.Blocks) options.Add(n);
            }
            return options[random.Next(options.Count)];
        }

        public static CivilCar Create(Transform parent, int seed)
        {
            var go = new GameObject("Civil");
            go.transform.SetParent(parent, false);
            var paint = seed % 3 == 0 ? Shapes.Magenta : seed % 3 == 1 ? Color.grey : Shapes.Hex(0x3A7D44);
            Models.Dress(Shapes.Car(go.transform, "Body", paint).transform, paint * 0.7f, Models.TrafficLod);
            var hull = go.AddComponent<BoxCollider>();
            hull.center = new Vector3(0, 0.75f, 0);
            hull.size = new Vector3(1.8f, 0.9f, 4f);
            var c = go.AddComponent<CivilCar>();
            c.Body = go.AddComponent<Rigidbody>();
            c.Body.mass = 1200;
            c.Body.interpolation = RigidbodyInterpolation.Interpolate;
            c.random = new System.Random(seed);
            return c;
        }

        // Puts the car at the start of the road a -> b, driving normally again.
        public void Place(Vector2Int a, Vector2Int b)
        {
            from = a;
            to = b;
            toEnd = true;
            Crashed = false;
            blockedFor = shift = applied = 0;
            PullingOver = false;
            gameObject.SetActive(true);
            Speed = Cruise * 0.5f;
            Body.isKinematic = true; // driven by script; the player and police still bump into it
            var start = LaneStart(a, b);
            transform.SetPositionAndRotation(start, Quaternion.LookRotation(LaneEnd(a, b) - start));
            Body.position = transform.position;
            Body.rotation = transform.rotation;
        }

        public Vector2Int Heading => to;
        public Vector2Int Coming => from;
        public bool WaitingAtLight { get; private set; }
        // Set by CivilTraffic while a police car with its lights on is close behind: ease to the kerb and slow down.
        public bool PullingOver { get; set; }
        public const float PullOverShift = 1, PullOverSpeed = 2;
        float shift, applied; // metres towards the kerb: wanted now, and already moved

        void FixedUpdate()
        {
            if (Crashed) return;
            float dt = Time.fixedDeltaTime;
            // Only along a straight road: never mid-turn in a crossroads.
            float target = PullingOver && toEnd ? PullOverShift : 0;
            shift = Mathf.MoveTowards(shift, target, dt);
            var kerb = Vector3.Cross(Vector3.up, (PoliceChase.Centre(to) - PoliceChase.Centre(from)).normalized);
            var goal = (toEnd ? LaneEnd(from, to) : LaneStart(from, to)) + kerb * applied;
            var pos = Body.position;
            var flat = goal - pos;
            flat.y = 0;
            bool stopForLight = toEnd && TrafficLights.MustStop(TrafficLights.For(to, from, Time.time), flat.magnitude);
            WaitingAtLight = stopForLight && flat.magnitude < 3;
            if (flat.magnitude < 1 && stopForLight) { Speed = 0; return; } // at the line: wait for green
            if (flat.magnitude < 1)
            {
                if (toEnd)
                {
                    var next = Next(to, from, random);
                    from = to;
                    to = next;
                    toEnd = false; // cut across the crossroads to the next road's start
                }
                else toEnd = true;
                return;
            }

            // Stop for whatever is in front: other cars, the player on foot, police. Buildings never sit in a lane.
            var eye = pos + Vector3.up * 0.7f + transform.forward * 2.1f;
            bool blocked = Physics.Raycast(eye, transform.forward, out var ahead, LookAhead);
            // Queuing behind a car at a red light is not a stand-off: never creep into it.
            var front = blocked ? ahead.collider.GetComponentInParent<CivilCar>() : null;
            bool queuing = front != null && Vector3.Dot(front.transform.forward, transform.forward) > 0.5f; // same way: a queue
            if (blocked && ahead.collider.GetComponentInParent<Pedestrian>() != null) queuing = true; // never creep into a person
            blockedFor = blocked && !queuing ? blockedFor + dt : 0;
            if (blockedFor > 4) blocked = blockedFor < 4.5f; // re-checks every 4.5 s, creeping at walking pace in between
            float cap = blockedFor > 4 ? 1.5f : Cruise;
            Speed = blocked ? Mathf.Max(0, Speed - Brake * dt) : Mathf.Min(cap, Speed + Accel * dt);
            // Fast enough to stop on the line, gently, no faster.
            if (PullingOver) Speed = Mathf.Min(Speed, PullOverSpeed);
            if (stopForLight) Speed = Mathf.Min(Speed, Mathf.Sqrt(2 * Brake * 0.5f * Mathf.Max(0, flat.magnitude - 0.8f)));
            if (blockedFor > 4.5f + 1.5f) blockedFor = 0;

            var dir = flat.normalized;
            Body.MoveRotation(Quaternion.RotateTowards(Body.rotation, Quaternion.LookRotation(dir), 120 * dt));
            // Sidestep straight towards (or back from) the kerb: steering at a point 40 m ahead would barely move it.
            Body.MovePosition(pos + transform.forward * Speed * dt + kerb * (shift - applied));
            applied = shift;
        }

        // A hard hit knocks the car loose: physics takes over and it stops driving until respawned.
        void OnCollisionEnter(Collision hit)
        {
            if (Crashed || hit.relativeVelocity.magnitude < CrashSpeed) return;
            Crashed = true;
            CrashedAt = Time.time;
            Body.isKinematic = false;
            Body.linearVelocity = transform.forward * Speed;
            Speed = 0;
        }
    }

    public class CivilTraffic : MonoBehaviour
    {
        public const float RespawnAfter = 8, RespawnAway = 40;
        public readonly List<CivilCar> Cars = new List<CivilCar>();
        public Transform Player { get; set; }
        public VehicleController PoliceCar { get; set; }
        public VehicleController PlayerSiren { get; set; } // the player's police car while its siren is on
        public VehicleController[] MoreSirens { get; set; } = new VehicleController[0]; // e.g. roadblock cars
        public const float SirenHeard = 25, SirenFrom = 3; // only a police car on the move has its siren going
        float nextSirenCheck;

        // Behind or alongside (not already well past) and close: the siren tells a driver to make way.
        public static bool ShouldPullOver(Transform car, Vector3 police, float range)
        {
            var back = police - car.position;
            back.y = 0;
            return back.magnitude < range && Vector3.Dot(back, car.forward) < 2;
        }
        System.Random random = new System.Random(11);

        public void Init(int count)
        {
            for (int i = 0; i < count; i++) Cars.Add(CivilCar.Create(transform, i));
            ResetTraffic();
        }

        // Spreads the cars over different roads, each on its own segment, so none start on top of another.
        public void ResetTraffic()
        {
            random = new System.Random(11);
            var used = new HashSet<(Vector2Int, Vector2Int)>();
            foreach (var car in Cars) Spawn(car, used);
        }

        void Spawn(CivilCar car, HashSet<(Vector2Int, Vector2Int)> used)
        {
            for (int tries = 0; tries < 50; tries++)
            {
                var a = new Vector2Int(random.Next(OpenWorld.Blocks + 1), random.Next(OpenWorld.Blocks + 1));
                var b = CivilCar.Next(a, new Vector2Int(-1, -1), random);
                var start = CivilCar.LaneStart(a, b) + transform.position;
                if (used.Contains((a, b))) continue;
                if (Player != null && Vector3.Distance(start, Player.position) < RespawnAway) continue;
                if (Physics.CheckBox(start + Vector3.up * 0.8f, new Vector3(1.2f, 0.6f, 2.5f))) continue;
                used.Add((a, b));
                car.Place(a, b);
                return;
            }
            car.gameObject.SetActive(false); // nowhere free: sit this one out
        }

        bool Heard(CivilCar car, VehicleController siren) =>
            Sounding(siren) && ShouldPullOver(car.transform, siren.transform.position, SirenHeard);

        bool HeardAny(CivilCar car)
        {
            foreach (var s in MoreSirens) if (Heard(car, s)) return true;
            return false;
        }

        public static bool Sounding(VehicleController c) =>
            c != null && c.gameObject.activeInHierarchy && Mathf.Abs(c.Speed) > SirenFrom;

        void Update()
        {
            if (Time.time >= nextSirenCheck)
            {
                nextSirenCheck = Time.time + 0.2f;
                foreach (var car in Cars)
                    car.PullingOver = !car.Crashed && (Heard(car, PoliceCar) || Heard(car, PlayerSiren) || HeardAny(car));
            }
            foreach (var car in Cars)
            {
                if (!car.gameObject.activeSelf || !car.Crashed || Time.time - car.CrashedAt < RespawnAfter) continue;
                if (Player != null && Vector3.Distance(car.transform.position, Player.position) < RespawnAway) continue;
                Spawn(car, new HashSet<(Vector2Int, Vector2Int)>());
            }
        }
    }
}
