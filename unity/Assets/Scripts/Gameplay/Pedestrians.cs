using System.Collections.Generic;
using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // People walking round the pavement of one block. They cannot be hurt by anything: weapons pass through them
    // (Ignore Raycast layer, no Health), and a car that reaches them makes them dive against the wall and crouch.
    // They run when shots are fired nearby, when the police car is close, or when a car speeds past.
    // shortcut: capsule figures, no road crossing, no animation.
    public class Pedestrian : MonoBehaviour
    {
        public const float Pavement = 2; // metres between the kerb and the building
        public const float WalkSpeed = 1.4f, RunSpeed = 4, PanicFor = 4, DiveFor = 2;

        public float Speed => Time.time < panicUntil ? RunSpeed : WalkSpeed;
        public bool Panicking => Time.time < panicUntil;
        public bool Diving => Time.time < diveUntil;
        public Vector3 Block { get; private set; }

        float half; // from the block centre to the walking line
        float along; // metres round the loop
        int dir;
        float panicUntil, diveUntil;
        Transform figure;

        // Walking line: the pavement centre, half a pavement out from the building face.
        public static float WalkHalf => (OpenWorld.Pitch - OpenWorld.Road) / 2 + Pavement / 2;

        // Point `along` metres round a square of half-size `half` (anticlockwise from the south-west corner).
        public static Vector3 Loop(Vector3 centre, float half, float along)
        {
            float side = half * 2, perimeter = side * 4;
            along = ((along % perimeter) + perimeter) % perimeter;
            int edge = (int)(along / side);
            float t = along - edge * side;
            var p = edge == 0 ? new Vector3(-half + t, 0, -half)
                : edge == 1 ? new Vector3(half, 0, -half + t)
                : edge == 2 ? new Vector3(half - t, 0, half)
                : new Vector3(-half, 0, half - t);
            return centre + p;
        }

        public static Pedestrian Create(Transform parent, Vector3 block, float along, int dir, Color colour)
        {
            var go = new GameObject("Pedestrian") { layer = 2 }; // Ignore Raycast: shots, sight lines and traffic eyes pass
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<Pedestrian>();
            p.figure = Shapes.Box(go.transform, new Vector3(0, 0.85f, 0), new Vector3(0.5f, 0.8f, 0.5f), colour, PrimitiveType.Capsule).transform;
            p.figure.gameObject.layer = 2;
            var c = go.AddComponent<CapsuleCollider>();
            c.isTrigger = true;
            c.height = 1.6f;
            c.radius = 0.3f;
            c.center = new Vector3(0, 0.9f, 0); // clear of the ground, so it only meets cars
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true; // trigger events need a body
            p.Block = block;
            p.half = WalkHalf;
            p.along = along;
            p.dir = dir;
            go.transform.localPosition = Loop(block, p.half, along);
            return p;
        }

        public void Panic() => panicUntil = Time.time + PanicFor;

        void Update()
        {
            if (Diving) return;
            figure.localScale = new Vector3(0.5f, 0.8f, 0.5f);
            figure.localPosition = new Vector3(0, 0.85f, 0);
            float dt = Time.deltaTime;
            along += dir * Speed * dt;
            var next = Loop(Block, half, along);
            var step = next - transform.localPosition;
            if (step.sqrMagnitude > 1e-6f) transform.localRotation = Quaternion.LookRotation(step);
            transform.localPosition = next;
        }

        void OnTriggerEnter(Collider other)
        {
            var body = other.attachedRigidbody;
            if (body == null || (body.GetComponent<VehicleController>() == null && body.GetComponent<CivilCar>() == null)) return;
            // Dive towards the building, crouch, then carry on running.
            var toBlock = Block - transform.localPosition;
            toBlock.y = 0;
            var side = Mathf.Abs(toBlock.x) > Mathf.Abs(toBlock.z) ? new Vector3(Mathf.Sign(toBlock.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(toBlock.z));
            transform.localPosition += side * (Pavement / 2 - 0.35f);
            figure.localScale = new Vector3(0.5f, 0.4f, 0.5f);
            figure.localPosition = new Vector3(0, 0.4f, 0);
            diveUntil = Time.time + DiveFor;
            panicUntil = diveUntil + PanicFor;
        }
    }

    public class Pedestrians : MonoBehaviour
    {
        public const float ShotHeard = 30, PoliceNear = 15, FastCar = 10, FastCarNear = 10;
        public readonly List<Pedestrian> People = new List<Pedestrian>();
        public Transform PoliceCar { get; set; }
        public VehicleController PlayerCar { get; set; }
        float nextCheck;

        static readonly Color[] Clothes = { Shapes.Hex(0xC9B79C), Shapes.Hex(0x5B6C8F), Shapes.Hex(0x8F5B5B), Shapes.Hex(0x6F8F5B) };

        public void Init(int count)
        {
            var random = new System.Random(5);
            for (int i = 0; i < count; i++)
            {
                int bx = i % OpenWorld.Blocks, bz = (i / OpenWorld.Blocks) % OpenWorld.Blocks;
                var block = new Vector3((bx - OpenWorld.Blocks / 2f + 0.5f) * OpenWorld.Pitch, 0, (bz - OpenWorld.Blocks / 2f + 0.5f) * OpenWorld.Pitch);
                float along = (float)random.NextDouble() * Pedestrian.WalkHalf * 8;
                People.Add(Pedestrian.Create(transform, block, along, random.Next(2) == 0 ? 1 : -1, Clothes[i % Clothes.Length]));
            }
            WeaponController.AnyShot += OnShot;
        }

        void OnDestroy() => WeaponController.AnyShot -= OnShot;

        void OnShot(Vector3 at)
        {
            foreach (var p in People)
                if (Vector3.Distance(p.transform.position, at) < ShotHeard) p.Panic();
        }

        // Four times a second is plenty for "is a siren or a speeding car close".
        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.25f;
            bool fast = PlayerCar != null && PlayerCar.gameObject.activeInHierarchy && PlayerCar.SpeedKmh / 3.6f > FastCar;
            foreach (var p in People)
            {
                var at = p.transform.position;
                if ((PoliceCar != null && PoliceCar.gameObject.activeInHierarchy && Vector3.Distance(at, PoliceCar.position) < PoliceNear)
                    || (fast && Vector3.Distance(at, PlayerCar.transform.position) < FastCarNear))
                    p.Panic();
            }
        }
    }
}
