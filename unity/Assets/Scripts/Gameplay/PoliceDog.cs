using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // A police dog released from the chasing police car when the player is on foot nearby. It runs straight at the
    // player when in sight, otherwise crossroads to crossroads, and holds the player (a bust, no injury shown) if it
    // stays right beside them for a second. Sprinting outruns it slowly; a dodge breaks its hold.
    // In water it swims after the player at half speed while it can see them; a player in the water and out of its
    // sight has broken the trail, so it goes to where it last saw them, noses about, and gives up.
    // It cannot be hurt: no Health, so neither shots nor fists affect it.
    // shortcut: a box figure, no animation or barking.
    public class PoliceDog : MonoBehaviour
    {
        public const float Speed = 6.5f, ReleaseRange = 40, HoldRange = 1.2f, HoldFor = 1;
        public float HoldProgress => held / HoldFor;
        public bool Holding => held >= HoldFor;
        public const float SniffFor = 6, SwimScale = 0.5f;
        public bool Swimming => OpenWorld.InWater(transform.position);
        public float CurrentSpeed => Swimming ? Speed * SwimScale : Speed;
        // Out of the water it stops for a second to shake itself dry: a moment's head start.
        public const float ShakeFor = 1;
        public bool Shaking => shakeLeft > 0;
        float shakeLeft;
        bool wasSwimming;
        // Wet paw prints for a while after a swim.
        public const float WetFor = PrintTrail.WetFor, PrintLife = PrintTrail.Life, PrintEvery = 0.6f;
        public const int PrintPool = PrintTrail.Pool;
        public bool Wet => paws.Wet;
        public int PrintsShowing => paws.Showing;
        PrintTrail paws;
        // The player's own wet footprints: a dog that has lost sight (or the scent) follows the newest it can see.
        public const float TrackRange = 20;
        PrintTrail footprints;
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
        bool routing, detouring;
        Vector3 detour;
        public const float CornerMargin = 1.5f;
        float held, fall;

        public static PoliceDog Create(Transform parent)
        {
            var go = new GameObject("Police dog");
            go.transform.SetParent(parent, false);
            Shapes.Box(go.transform, new Vector3(0, 0.45f, 0), new Vector3(0.35f, 0.4f, 0.9f), Shapes.Hex(0x6B4A2B));
            Shapes.Box(go.transform, new Vector3(0, 0.7f, 0.5f), new Vector3(0.25f, 0.25f, 0.3f), Shapes.Hex(0x4A3320)); // head
            var d = go.AddComponent<PoliceDog>();
            d.body = go.AddComponent<CharacterController>();
            d.paws = new PrintTrail(parent, PrintEvery, new Vector3(0.14f, 0.01f, 0.18f));
            d.body.height = 0.8f;
            d.body.radius = 0.25f;
            d.body.center = new Vector3(0, 0.45f, 0);
            go.SetActive(false);
            return d;
        }

        public void Release(Vector3 at, Transform player, MeleeController playerMelee, PrintTrail playerFootprints = null)
        {
            footprints = playerFootprints;
            body.enabled = false;
            transform.position = at;
            body.enabled = true;
            target = player;
            targetMelee = playerMelee;
            routing = detouring = false; // a detour left over from the last dog would send this one the wrong way
            held = fall = sniffed = shakeLeft = 0;
            paws.Clear();
            wasSwimming = false;
            LostScent = false;
            LastScent = player.position;
            gameObject.SetActive(true);
        }

        public void Recall()
        {
            gameObject.SetActive(false);
            held = sniffed = 0;
            LostScent = false; // a stale "gave up" would keep every later dog in the car
            paws.Clear(); // only the dog ages them
            Barking = false;
        }

        void Update()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            if (wasSwimming && !Swimming) { shakeLeft = ShakeFor; paws.Soak(transform.position); }
            paws.Tick(transform, Swimming ? 0 : dt);
            wasSwimming = Swimming;
            if (Shaking) { Shake(dt); return; }
            if (LostScent && SeesFootprint(out _)) { LostScent = false; sniffed = 0; } // picked the trail up again
            if (LostScent) { Barking = false; Sniff(dt); return; }
            var from = transform.position + Vector3.up * 0.6f;
            var to = target.position + Vector3.up * 0.6f;
            bool inSight = !Physics.Linecast(from, to, out var hit) || hit.transform == target;
            if (OpenWorld.InWater(target.position) && !inSight) { LostScent = true; Barking = false; return; }
            LastScent = target.position;
            Vector3 goal;
            if (inSight) { goal = target.position; routing = detouring = false; }
            else if (SeesFootprint(out var print)) { goal = print; detouring = false; }
            else if (detouring && Flat(detour - transform.position) > 1) goal = detour;
            else if (Detour(hit.collider, target.position, out detour)) { detouring = true; goal = detour; }
            else
            {
                detouring = false;
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
            var move = distance > HoldRange * 0.8f ? flat.normalized * CurrentSpeed : Vector3.zero;
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat);
            fall = body.isGrounded ? -1 : fall - 20 * dt;
            body.Move((move + Vector3.up * fall) * dt);

            bool dodging = targetMelee != null && targetMelee.Dodging;
            held = distance < HoldRange && !dodging ? held + dt : 0;
        }

        // Round whatever is in the way: the corner of its footprint (plus a margin) that the dog can run to in a
        // straight line and that makes the shortest trip to the player. A fence's open end, a building's corner.
        bool Detour(Collider blocker, Vector3 destination, out Vector3 corner)
        {
            corner = default;
            if (blocker == null) return false;
            var b = blocker.bounds;
            float best = float.MaxValue;
            bool found = false;
            var eye = transform.position + Vector3.up * 0.6f;
            foreach (var c in new[]
            {
                new Vector3(b.min.x - CornerMargin, 0, b.min.z - CornerMargin), new Vector3(b.min.x - CornerMargin, 0, b.max.z + CornerMargin),
                new Vector3(b.max.x + CornerMargin, 0, b.min.z - CornerMargin), new Vector3(b.max.x + CornerMargin, 0, b.max.z + CornerMargin),
            })
            {
                var at = new Vector3(c.x, transform.position.y, c.z);
                if (Flat(at - transform.position) < 1) continue; // already there
                if (Physics.Linecast(eye, at + Vector3.up * 0.6f, out var h) && h.transform != target) continue;
                float trip = Flat(at - transform.position) + Flat(destination - at);
                if (trip < best) { best = trip; corner = at; found = true; }
            }
            return found;
        }

        public void WashPrints() => paws.Clear();

        static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        // The player's newest wet footprint, if it is close and in plain view.
        bool SeesFootprint(out Vector3 at)
        {
            at = default;
            if (footprints == null || !footprints.Newest(out at)) return false;
            if (Flat(at - transform.position) > TrackRange) return false;
            // The newest print is right under the player, so seeing them standing on it counts as seeing it.
            return !Physics.Linecast(transform.position + Vector3.up * 0.6f, at + Vector3.up * 0.3f, out var hit) || hit.transform == target;
        }

        void Shake(float dt)
        {
            shakeLeft -= dt;
            held = 0;
            var figure = transform.GetChild(0);
            figure.localRotation = Shaking ? Quaternion.Euler(0, 0, Mathf.Sin(shakeLeft * 60) * 20) : Quaternion.identity;
            fall = body.isGrounded ? -1 : fall - 20 * dt;
            body.Move(Vector3.up * fall * dt);
        }

        // Trots to the last place it had the scent and noses about there.
        void Sniff(float dt)
        {
            held = 0;
            // Round anything in the way, just as when chasing: a fence must not pin it in place for good.
            var eye = transform.position + Vector3.up * 0.6f;
            var spot = LastScent;
            if (detouring && Flat(detour - transform.position) > 1) spot = detour;
            else if (Physics.Linecast(eye, LastScent + Vector3.up * 0.6f, out var block) && block.transform != target
                     && Detour(block.collider, LastScent, out detour)) { detouring = true; spot = detour; }
            else detouring = false;
            var flat = spot - transform.position;
            flat.y = 0;
            if (flat.magnitude > 0.5f)
            {
                transform.rotation = Quaternion.LookRotation(flat);
                fall = body.isGrounded ? -1 : fall - 20 * dt;
                body.Move((flat.normalized * CurrentSpeed * 0.5f + Vector3.up * fall) * dt);
            }
            else if (!detouring) sniffed += dt;
        }
    }
}
