using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Spike strips laid at the next crossroads ahead: full-width across the road straight on and across one turning.
    // The other turning is always left open, so they force a turn, never a puncture.
    public class CrossroadSpikes : MonoBehaviour
    {
        public const float FirstAfter = 60, Every = 60, MinSpeed = 8, Lasts = 30;
        public static float Past => (CivilCar.NodeClear + 1) / OpenWorld.Pitch; // just past the crossroads, on each exit
        public readonly SpikeStrip[] Strips = new SpikeStrip[2];
        public Vector2Int Node { get; private set; }
        public Vector2Int Open { get; private set; } // the exit left clear
        public bool Active => Strips[0].Active || Strips[1].Active;
        float sinceStart, nextAt = FirstAfter, upSince;
        System.Random random = new System.Random(9);

        public void Init()
        {
            for (int i = 0; i < Strips.Length; i++)
            {
                Strips[i] = new GameObject("Crossroads spikes " + i).AddComponent<SpikeStrip>();
                Strips[i].transform.SetParent(transform, false);
                Strips[i].Init(OpenWorld.Road - 1);
            }
        }

        static bool Inside(Vector2Int n) => n.x >= 0 && n.y >= 0 && n.x <= OpenWorld.Blocks && n.y <= OpenWorld.Blocks;

        // Which exits of `node` to block when arriving along `step`: straight on, plus one turning if there are two.
        // Returns the exit left open. Never blocks the way you came.
        public static Vector2Int Choose(Vector2Int node, Vector2Int step, int pick, out Vector2Int blockA, out Vector2Int? blockB)
        {
            var perp = new Vector2Int(-step.y, step.x);
            var straight = node + step;
            var turns = new[] { node + perp, node - perp };
            blockA = straight;
            blockB = null;
            bool left = Inside(turns[0]), right = Inside(turns[1]);
            if (left && right)
            {
                blockB = turns[pick % 2];
                return turns[1 - pick % 2];
            }
            return left ? turns[0] : turns[1]; // at the edge: the only turning stays open
        }

        public void ResetSpikes()
        {
            foreach (var s in Strips) s.Clear();
            sinceStart = 0;
            nextAt = FirstAfter;
            random = new System.Random(9);
        }

        public bool Spawn(Vector2Int node, Vector2Int step)
        {
            if (!Inside(node + step)) return false; // heading off the grid: nothing to block straight on
            Node = node;
            Open = Choose(node, step, random.Next(2), out var a, out var b);
            Strips[0].Spawn(node, a, Past, fullWidth: true);
            if (b.HasValue) Strips[1].Spawn(node, b.Value, Past, fullWidth: true);
            else Strips[1].Clear();
            upSince = 0;
            nextAt = sinceStart + Every;
            return true;
        }

        public bool Over(Vector3 p) => Strips[0].Over(p) || Strips[1].Over(p);

        public void Tick(VehicleController player, bool chased, float dt)
        {
            sinceStart += dt;
            if (Active)
            {
                upSince += dt;
                if (!chased || upSince > Lasts) foreach (var s in Strips) s.Clear();
                return;
            }
            if (!chased || sinceStart < nextAt || Mathf.Abs(player.Speed) < MinSpeed) return;
            if (!RoadBlock.Plan(player.transform.position, player.Body.linearVelocity, out var node, out var beyond) || !Spawn(node, beyond - node))
                nextAt = sinceStart + 2;
        }
    }
}
