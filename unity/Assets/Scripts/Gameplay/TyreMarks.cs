using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Skid marks and tyre smoke while a rear tyre slides, and engine smoke once the car is badly damaged.
    // A fixed pool of flat strips (nothing allocated during play); the oldest is reused when the pool runs out.
    public class TyreMarks : MonoBehaviour
    {
        public const int Pool = 80;
        public const float Every = 0.5f, Life = 10, SmokeBelow = 50; // metres between marks, seconds, condition
        readonly GameObject[] marks = new GameObject[Pool];
        readonly float[] laidAt = new float[Pool];
        readonly Vector3[] last = new Vector3[2];
        readonly bool[] wasSliding = new bool[2];
        int next;
        VehicleController car;
        ParticleSystem smoke;

        public int Showing
        {
            get { int n = 0; foreach (var m in marks) if (m != null && m.activeSelf) n++; return n; }
        }
        public int SmokePuffs { get; private set; }

        void Awake()
        {
            car = GetComponent<VehicleController>();
            var go = new GameObject("Tyre smoke");
            go.transform.SetParent(transform.parent, false); // world space, not dragged along with the car
            smoke = go.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 1.2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.maxParticles = 200;
            main.gravityModifier = -0.05f; // drifts up a little
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = smoke.emission;
            emission.enabled = false; // puffs only, by Emit
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = new Material(Resources.Load<Shader>("Flat")); // always in the build
            r.material.SetColor("_Color", new Color(0.55f, 0.57f, 0.6f));
        }

        void Update()
        {
            for (int i = 0; i < Pool; i++)
                if (marks[i] != null && marks[i].activeSelf && Time.time - laidAt[i] > Life) marks[i].SetActive(false);

            for (int w = 0; w < 2; w++)
            {
                bool sliding = car.Sliding(w, out var at);
                if (sliding)
                {
                    if (!wasSliding[w]) last[w] = at; // a fresh skid starts here
                    else if (Vector3.Distance(at, last[w]) >= Every) { Lay(last[w], at); last[w] = at; }
                    Puff(at, 1);
                }
                wasSliding[w] = sliding;
            }
            if (car.TakesDamage && car.Condition < SmokeBelow && Random.value < (car.Wrecked ? 0.6f : 0.2f))
                Puff(transform.TransformPoint(new Vector3(0, 1.1f, 1.6f)), 1); // from under the bonnet
        }

        void Lay(Vector3 from, Vector3 to)
        {
            if (marks[next] == null)
            {
                marks[next] = Shapes.Box(transform.parent, Vector3.zero, Vector3.one, Shapes.Hex(0x0C0D10));
                marks[next].name = "Skid mark";
            }
            var m = marks[next];
            var flat = to - from;
            flat.y = 0;
            m.transform.position = new Vector3((from.x + to.x) / 2, 0.035f, (from.z + to.z) / 2); // just above the road
            m.transform.rotation = Quaternion.LookRotation(flat);
            m.transform.localScale = new Vector3(0.22f, 0.01f, flat.magnitude + 0.05f);
            m.SetActive(true);
            laidAt[next] = Time.time;
            next = (next + 1) % Pool;
        }

        void Puff(Vector3 at, int count)
        {
            smoke.Emit(new ParticleSystem.EmitParams { position = at + Vector3.up * 0.3f, applyShapeToPosition = false }, count);
            SmokePuffs += count;
        }

        // A fresh start (new visit, bust, mission): no old marks left on the road.
        public void Clear()
        {
            foreach (var m in marks) if (m != null) m.SetActive(false);
            wasSliding[0] = wasSliding[1] = false;
        }
    }
}
