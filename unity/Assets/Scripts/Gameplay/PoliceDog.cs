using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // A police dog released from the chasing police car when the player is on foot nearby. It runs straight at the
    // player when in sight, otherwise crossroads to crossroads, and holds the player (a bust, no injury shown) if it
    // stays right beside them for a second. Sprinting outruns it slowly; a dodge breaks its hold.
    // Water breaks the trail: once the player wades in, it goes to where it last had the scent, waits, and gives up.
    // It cannot be hurt: no Health, so neither shots nor fists affect it.
    // shortcut: a box figure, no animation or barking.
    public class PoliceDog : MonoBehaviour
    {
        public const float Speed = 6.5f, ReleaseRange = 40, HoldRange = 1.2f, HoldFor = 1;
        public float HoldProgress => held / HoldFor;
        public bool Holding => held >= HoldFor;
        public const float SniffFor = 6;
        // Barks while it can see the player close by: the police cannot be lost while it does.
        public const float BarkRange = 30, BarkEvery = 1.5f;
        public bool Barking { get; private set; }
        public event System.Action Barked;
        float nextBark;
        public bool LostScent { get; private set; }
        public bool GaveUp => LostScent && sniffed > SniffFor;
        public Vector3 LastScent { get; private set; }
        float sniffed;
        CharacterController body;
        Transform target;
        MeleeController targetMelee;
        Vector2Int next;
        bool routing;
        float held, fall;

        public static PoliceDog Create(Transform parent)
        {
            var go = new GameObject("Police dog");
            go.transform.SetParent(parent, false);
            Shapes.Box(go.transform, new Vector3(0, 0.45f, 0), new Vector3(0.35f, 0.4f, 0.9f), Shapes.Hex(0x6B4A2B));
            Shapes.Box(go.transform, new Vector3(0, 0.7f, 0.5f), new Vector3(0.25f, 0.25f, 0.3f), Shapes.Hex(0x4A3320)); // head
            var d = go.AddComponent<PoliceDog>();
            d.body = go.AddComponent<CharacterController>();
            d.body.height = 0.8f;
            d.body.radius = 0.25f;
            d.body.center = new Vector3(0, 0.45f, 0);
            go.SetActive(false);
            return d;
        }

        public void Release(Vector3 at, Transform player, MeleeController playerMelee)
        {
            body.enabled = false;
            transform.position = at;
            body.enabled = true;
            target = player;
            targetMelee = playerMelee;
            routing = false;
            held = fall = sniffed = 0;
            LostScent = false;
            LastScent = player.position;
            gameObject.SetActive(true);
        }

        public void Recall()
        {
            gameObject.SetActive(false);
            held = sniffed = 0;
            LostScent = false; // a stale "gave up" would keep every later dog in the car
            Barking = false;
        }

        void Update()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            if (!LostScent && OpenWorld.InWater(target.position)) LostScent = true;
            if (LostScent) { Barking = false; Sniff(dt); return; }
            LastScent = target.position;
            var from = transform.position + Vector3.up * 0.6f;
            var to = target.position + Vector3.up * 0.6f;
            bool inSight = !Physics.Linecast(from, to, out var hit) || hit.transform == target;
            Vector3 goal;
            if (inSight) { goal = target.position; routing = false; }
            else
            {
                if (!routing) { next = PoliceChase.Node(transform.position); routing = true; }
                var c = PoliceChase.Centre(next);
                if (new Vector2(c.x - transform.position.x, c.z - transform.position.z).magnitude < 3)
                    next = PoliceChase.Step(next, PoliceChase.Node(target.position));
                goal = PoliceChase.Centre(next);
            }

            var flat = goal - transform.position;
            flat.y = 0;
            float distance = Vector3.Distance(transform.position, target.position);
            Barking = inSight && distance < BarkRange;
            if (Barking && Time.time >= nextBark)
            {
                nextBark = Time.time + BarkEvery;
                Barked?.Invoke();
            }
            var move = distance > HoldRange * 0.8f ? flat.normalized * Speed : Vector3.zero;
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat);
            fall = body.isGrounded ? -1 : fall - 20 * dt;
            body.Move((move + Vector3.up * fall) * dt);

            bool dodging = targetMelee != null && targetMelee.Dodging;
            held = distance < HoldRange && !dodging ? held + dt : 0;
        }

        // Trots to the last place it had the scent and noses about there.
        void Sniff(float dt)
        {
            held = 0;
            var flat = LastScent - transform.position;
            flat.y = 0;
            if (flat.magnitude > 0.5f)
            {
                transform.rotation = Quaternion.LookRotation(flat);
                fall = body.isGrounded ? -1 : fall - 20 * dt;
                body.Move((flat.normalized * Speed * 0.5f + Vector3.up * fall) * dt);
            }
            else sniffed += dt;
        }
    }
}
