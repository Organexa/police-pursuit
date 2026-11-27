using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace PolicePursuit.Gameplay
{
    // Civilian cars to dodge plus police that chase and ram.
    // shortcut: hits are box distance checks and traffic cars pass through each other; add avoidance if it shows.
    public class Traffic : MonoBehaviour
    {
        const float CivilDamage = 25f;
        const float RamDamage = 15f;
        const float CopSteer = 4f;
        static readonly float[] Lanes = { -4f, 0f, 4f };

        public event Action NearMiss;

        class Civil { public Transform T; public float Speed; public bool Passed; }

        PlayerCar player;
        readonly List<Civil> civils = new List<Civil>();
        readonly List<Transform> cops = new List<Transform>();

        public void Init(PlayerCar target) => player = target;

        // Metres to the closest police car, for the siren; huge when there is none.
        public float NearestCop(Vector3 from)
        {
            float nearest = float.MaxValue;
            foreach (var c in cops) nearest = Mathf.Min(nearest, Vector3.Distance(from, c.position));
            return nearest;
        }

        public void ResetTraffic()
        {
            foreach (var c in civils) Remove(c.T);
            foreach (var c in cops) Remove(c);
            civils.Clear();
            cops.Clear();
        }

        public void Tick(float dt, int wanted, int maxCars)
        {
            int copCount = Mathf.Min(wanted, maxCars / 2);
            while (cops.Count < copCount) cops.Add(SpawnCop());
            while (civils.Count < maxCars - copCount) civils.Add(SpawnCivil());
            // More wanted means more cops: make room for them by dropping the newest civilians.
            while (civils.Count > maxCars - copCount)
            {
                Remove(civils[civils.Count - 1].T);
                civils.RemoveAt(civils.Count - 1);
            }

            TickCivils(dt);
            TickCops(dt);
        }

        void TickCivils(float dt)
        {
            var p = player.transform.position;
            foreach (var c in civils)
            {
                var pos = c.T.position;
                pos.z += c.Speed * dt;
                c.T.position = pos;

                float dx = Mathf.Abs(pos.x - p.x);
                float dz = pos.z - p.z;
                if (dx < PlayerCar.HalfWidth * 2 && Mathf.Abs(dz) < PlayerCar.HalfLength * 2)
                {
                    player.TakeDamage(CivilDamage);
                    Place(c);
                    continue;
                }
                if (!c.Passed && dz < -PlayerCar.HalfLength * 2)
                {
                    c.Passed = true;
                    if (dx < PlayerCar.HalfWidth * 2 + 1.2f) NearMiss?.Invoke();
                }
                if (dz < -30) Place(c);
            }
        }

        void TickCops(float dt)
        {
            var p = player.transform.position;
            foreach (var cop in cops)
            {
                var pos = cop.position;
                float behind = p.z - pos.z;
                float speed = behind > 0 ? player.Speed + 3 : player.Speed * 0.9f;
                pos.z += Mathf.Min(speed, player.TopSpeed * 1.15f) * dt;
                pos.x = Mathf.MoveTowards(pos.x, p.x, CopSteer * dt);

                if (Mathf.Abs(pos.x - p.x) < PlayerCar.HalfWidth * 2 &&
                    Mathf.Abs(pos.z - p.z) < PlayerCar.HalfLength * 2)
                {
                    player.TakeDamage(RamDamage);
                    pos.z = p.z - 25;
                }
                if (p.z - pos.z > 80) pos.z = p.z - 40;
                cop.position = pos;

                // Flash the light bar.
                bool redOn = Time.time % 0.4f < 0.2f;
                cop.GetChild(0).GetChild(2).gameObject.SetActive(redOn);
                cop.GetChild(0).GetChild(3).gameObject.SetActive(!redOn);
            }
        }

        Civil SpawnCivil()
        {
            var go = new GameObject("Civil");
            go.transform.SetParent(transform, false);
            Shapes.Car(go.transform, "Body", Random.value < 0.5f ? Shapes.Magenta : Color.grey);
            var civil = new Civil { T = go.transform };
            Place(civil);
            return civil;
        }

        void Place(Civil c)
        {
            c.Speed = Random.Range(12f, 20f);
            c.Passed = false;
            c.T.position = new Vector3(Lanes[Random.Range(0, Lanes.Length)], 0,
                player.transform.position.z + Random.Range(120f, 260f));
        }

        Transform SpawnCop()
        {
            var go = new GameObject("Cop");
            go.transform.SetParent(transform, false);
            var body = Shapes.Car(go.transform, "Body", Color.white).transform;
            Shapes.Box(body, new Vector3(-0.4f, 1.45f, -0.3f), new Vector3(0.6f, 0.25f, 0.5f), Shapes.Red, glow: 1);
            Shapes.Box(body, new Vector3(0.4f, 1.45f, -0.3f), new Vector3(0.6f, 0.25f, 0.5f), Color.blue, glow: 1.5f);
            go.transform.position = new Vector3(Lanes[cops.Count % Lanes.Length], 0,
                player.transform.position.z - 25 - 12 * cops.Count);
            return go.transform;
        }

        static void Remove(Transform t)
        {
            t.gameObject.SetActive(false); // Destroy is deferred to the end of the frame
            Destroy(t.gameObject);
        }
    }
}
