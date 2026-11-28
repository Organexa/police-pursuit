using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Wet prints left for a while after leaving water: one every few steps, each fading after a few seconds.
    // A fixed pool of flat boxes, so nothing is allocated during play. Used for the police dog and for the player.
    public class PrintTrail
    {
        public const float WetFor = 6, Life = 3;
        public const int Pool = 20;
        public bool Wet => wetLeft > 0;
        readonly float every;
        readonly Vector3 size;
        readonly Transform parent;
        readonly GameObject[] prints = new GameObject[Pool];
        readonly float[] printedAt = new float[Pool];
        int next = 0, newest = -1;
        float wetLeft;
        Vector3 last;

        public PrintTrail(Transform parent, float every, Vector3 size)
        {
            this.parent = parent;
            this.every = every;
            this.size = size;
        }

        public int Showing
        {
            get { int n = 0; foreach (var p in prints) if (p != null && p.activeSelf) n++; return n; }
        }

        public void Soak(Vector3 at)
        {
            wetLeft = WetFor;
            last = at;
        }

        // Called every frame: fades old prints and, while still wet, lays a new one every `every` metres.
        public void Tick(Transform walker, float dt)
        {
            for (int i = 0; i < Pool; i++)
                if (prints[i] != null && prints[i].activeSelf && Time.time - printedAt[i] > Life) prints[i].SetActive(false);
            if (!Wet) return;
            wetLeft -= dt;
            var flat = walker.position - last;
            flat.y = 0;
            if (flat.magnitude < every) return;
            last = walker.position;
            if (prints[next] == null)
            {
                prints[next] = Shapes.Box(parent, Vector3.zero, size, Shapes.Hex(0x14161C));
                prints[next].name = "Wet print";
            }
            var p = prints[next];
            float side = next % 2 == 0 ? 0.12f : -0.12f; // left, right, left
            p.transform.position = new Vector3(walker.position.x, 0.07f, walker.position.z) + walker.right * side;
            p.transform.rotation = walker.rotation;
            p.SetActive(true);
            printedAt[next] = Time.time;
            newest = next;
            next = (next + 1) % Pool;
        }

        // The most recent print still on the ground, if any.
        public bool Newest(out Vector3 at)
        {
            at = default;
            if (newest < 0 || prints[newest] == null || !prints[newest].activeSelf) return false;
            at = prints[newest].transform.position;
            return true;
        }

        public void Clear()
        {
            wetLeft = 0;
            newest = -1;
            foreach (var p in prints) if (p != null) p.SetActive(false);
        }
    }
}
