using System;
using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Anything weapons can hurt. Damage is an abstract number; there is no gore anywhere.
    public interface IDamageable
    {
        bool Down { get; }
        void TakeDamage(float amount);
    }

    public class Health : MonoBehaviour, IDamageable
    {
        public float Max { get; set; } = 100;
        public float Current { get; private set; } = 100;
        public bool Down => Current <= 0;
        public event Action Downed;
        public event Action Hurt;
        public float DamageScale { get; set; } = 1; // lowered by blocking, 0 while dodging

        public void TakeDamage(float amount)
        {
            amount *= DamageScale;
            if (Down || amount <= 0) return;
            Current = Mathf.Max(0, Current - amount);
            Hurt?.Invoke();
            if (Down) Downed?.Invoke();
        }

        public void Restore() => Current = Max;
    }
}
