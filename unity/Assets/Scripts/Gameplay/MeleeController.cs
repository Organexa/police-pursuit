using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Hand-to-hand: light and heavy strikes with wind-up, hit window and recovery, a three-hit light combo,
    // block, dodge and stamina. Strikes lock on to the nearest live target in reach. No gore.
    // shortcut: a fist box stands in for punch and kick animations; drive the windows from animation events later.
    public class MeleeController : MonoBehaviour
    {
        public struct Strike
        {
            public float Damage, Windup, Active, Recovery, Stamina;
        }

        public static readonly Strike Light = new Strike { Damage = 10, Windup = 0.15f, Active = 0.1f, Recovery = 0.25f, Stamina = 10 };
        public static readonly Strike Heavy = new Strike { Damage = 25, Windup = 0.4f, Active = 0.15f, Recovery = 0.5f, Stamina = 25 };
        public const float Reach = 1.6f, LockRange = 2.5f, ComboGap = 0.5f, ComboBonus = 1.5f;
        public const float MaxStamina = 100, Regen = 25, RegenDelay = 0.6f;
        public const float BlockScale = 0.2f, BlockCost = 5, DodgeCost = 20, DodgeTime = 0.25f, DodgeSpeed = 9, DodgeCooldown = 0.6f;

        public float Stamina { get; private set; } = MaxStamina;
        public bool Busy => phase != Phase.None || Dodging; // no new move, and no walking, until it ends
        public bool Dodging => dodgeLeft > 0;
        public bool Blocking { get; private set; }
        public int Combo { get; private set; } // 1..3 within a light chain

        enum Phase { None, Windup, Active, Recovery }
        Phase phase;
        Strike current;
        float phaseLeft, sinceStrike = 99, sinceSpend = 99, dodgeLeft, dodgeCooldown;
        Vector3 dodgeDir;
        Health own;
        CharacterController body;
        GameObject fist;
        readonly System.Collections.Generic.HashSet<IDamageable> struck = new System.Collections.Generic.HashSet<IDamageable>();

        void Awake()
        {
            own = GetComponent<Health>();
            body = GetComponent<CharacterController>();
            fist = Shapes.Box(transform, new Vector3(0.2f, 1.2f, 0.8f), Vector3.one * 0.22f, Color.white, glow: 1);
            fist.SetActive(false);
            own.Hurt += () => { if (Blocking) Spend(BlockCost); };
        }

        public bool LightAttack() => Begin(Light, chain: true);
        public bool HeavyAttack() => Begin(Heavy, chain: false);

        bool Begin(Strike s, bool chain)
        {
            if (Busy || Blocking || Stamina < s.Stamina) return false;
            Combo = chain && sinceStrike < ComboGap + s.Recovery && Combo < 3 ? Combo + 1 : 1;
            current = s;
            if (chain && Combo == 3) current.Damage *= ComboBonus;
            Spend(s.Stamina);
            FaceNearest();
            struck.Clear();
            phase = Phase.Windup;
            phaseLeft = s.Windup;
            return true;
        }

        public void SetBlock(bool on)
        {
            Blocking = on && phase == Phase.None && !Dodging && Stamina > 0;
            own.DamageScale = Dodging ? 0 : Blocking ? BlockScale : 1;
        }

        public bool Dodge(Vector3 direction)
        {
            if (Busy || dodgeCooldown > 0 || Stamina < DodgeCost) return false;
            Spend(DodgeCost);
            dodgeDir = Vector3.ProjectOnPlane(direction.sqrMagnitude > 0.01f ? direction : -transform.forward, Vector3.up).normalized;
            dodgeLeft = DodgeTime;
            dodgeCooldown = DodgeCooldown;
            Blocking = false;
            own.DamageScale = 0; // untouchable while dodging
            return true;
        }

        public void ResetMelee()
        {
            phase = Phase.None;
            dodgeLeft = dodgeCooldown = 0;
            Stamina = MaxStamina;
            Combo = 0;
            Blocking = false;
            own.DamageScale = 1;
            fist.SetActive(false);
        }

        void Spend(float amount)
        {
            Stamina = Mathf.Max(0, Stamina - amount);
            sinceSpend = 0;
        }

        void FaceNearest()
        {
            Health best = null;
            float bestDistance = LockRange;
            foreach (var h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (h == own || h.Down) continue;
                float d = Vector3.Distance(h.transform.position, transform.position);
                if (d < bestDistance) { best = h; bestDistance = d; }
            }
            if (best == null) return;
            var flat = Vector3.ProjectOnPlane(best.transform.position - transform.position, Vector3.up);
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            sinceStrike += dt;
            sinceSpend += dt;
            dodgeCooldown -= dt;
            if (sinceSpend > RegenDelay && !Blocking) Stamina = Mathf.Min(MaxStamina, Stamina + Regen * dt);

            if (Dodging)
            {
                dodgeLeft -= dt;
                if (body != null && body.enabled) body.Move(dodgeDir * DodgeSpeed * dt);
                if (!Dodging) own.DamageScale = Blocking ? BlockScale : 1;
            }

            if (phase == Phase.None) return;
            if (phase == Phase.Active) Hit();
            if ((phaseLeft -= dt) > 0) return;
            phase = phase == Phase.Windup ? Phase.Active : phase == Phase.Active ? Phase.Recovery : Phase.None;
            phaseLeft = phase == Phase.Active ? current.Active : current.Recovery;
            fist.SetActive(phase == Phase.Active);
            if (phase == Phase.Recovery) sinceStrike = 0;
        }

        // Everything in front within reach is struck once per swing, only inside the active window.
        void Hit()
        {
            var centre = transform.position + Vector3.up * 1.1f + transform.forward * (Reach / 2);
            foreach (var c in Physics.OverlapSphere(centre, Reach / 2))
            {
                var victim = c.GetComponentInParent<IDamageable>();
                if (victim == null || ReferenceEquals(victim, own) || !struck.Add(victim)) continue;
                victim.TakeDamage(current.Damage);
            }
        }
    }
}
