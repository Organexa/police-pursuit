using System;
using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Instant-hit shooting with a magazine, reload and mobile aim assist. Effects are a flash and a spark only.
    public class WeaponController : MonoBehaviour
    {
        public const float ChestHeight = 1.4f;
        public WeaponData Data { get; private set; }
        public int Ammo { get; private set; }
        public bool Reloading => reloadLeft > 0;
        public event Action Fired;

        float cooldown, reloadLeft, flashLeft, sparkLeft;
        Health own;
        GameObject flash, spark;

        public void Init(WeaponData data)
        {
            Data = data;
            Ammo = data.magazine;
            own = GetComponent<Health>();
            flash = Shapes.Box(transform, new Vector3(0.25f, ChestHeight, 0.6f), Vector3.one * 0.18f, Color.yellow, glow: 3);
            spark = Shapes.Box(null, Vector3.zero, Vector3.one * 0.25f, Color.white, PrimitiveType.Sphere, glow: 3);
            spark.transform.SetParent(transform.parent, false);
            flash.SetActive(false);
            spark.SetActive(false);
        }

        public void Restock()
        {
            Ammo = Data.magazine;
            reloadLeft = cooldown = 0;
        }

        public void Reload()
        {
            if (!Reloading && Ammo < Data.magazine) reloadLeft = Data.reloadTime;
        }

        // Fires towards `aim` (flat world direction), bending to the nearest live target in the assist cone.
        // Returns false when it could not fire (cooling down, reloading, empty).
        public bool Fire(Vector3 aim)
        {
            if (cooldown > 0 || Reloading) return false;
            if (Ammo == 0) { Reload(); return false; }
            Ammo--;
            cooldown = Data.fireInterval;

            var origin = transform.position + Vector3.up * ChestHeight;
            var target = Assist(origin, aim);
            var dir = target != null ? target.transform.position + Vector3.up * 1.1f - origin : aim;
            var flat = Vector3.ProjectOnPlane(dir, Vector3.up);
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat);

            // Queries ignore the collider they start inside, so the shooter's own body is never hit.
            if (Physics.Raycast(origin, dir.normalized, out var hit, Data.range))
            {
                spark.transform.position = hit.point;
                spark.SetActive(true);
                sparkLeft = 0.08f;
                var victim = hit.collider.GetComponentInParent<IDamageable>();
                if (victim != null && UnityEngine.Random.value < Data.hitChance) victim.TakeDamage(Data.damage);
            }
            flash.SetActive(true);
            flashLeft = 0.05f;
            if (Ammo == 0) Reload();
            Fired?.Invoke();
            return true;
        }

        Health Assist(Vector3 origin, Vector3 aim)
        {
            if (Data.assistAngle <= 0) return null;
            Health best = null;
            float bestAngle = Data.assistAngle;
            // A few targets at most and only on a shot, so a scene search is cheap enough.
            foreach (var h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (h == own || h.Down) continue;
                var to = h.transform.position + Vector3.up * 1.1f - origin;
                if (to.magnitude > Data.range) continue;
                float angle = Vector3.Angle(Vector3.ProjectOnPlane(aim, Vector3.up), Vector3.ProjectOnPlane(to, Vector3.up));
                if (angle >= bestAngle) continue;
                if (Physics.Raycast(origin, to.normalized, out var hit, to.magnitude + 0.5f)
                    && hit.collider.GetComponentInParent<Health>() != h) continue; // behind a wall
                best = h;
                bestAngle = angle;
            }
            return best;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            cooldown -= dt;
            if (Reloading && (reloadLeft -= dt) <= 0) Ammo = Data.magazine;
            if (flashLeft > 0 && (flashLeft -= dt) <= 0) flash.SetActive(false);
            if (sparkLeft > 0 && (sparkLeft -= dt) <= 0) spark.SetActive(false);
        }
    }
}
