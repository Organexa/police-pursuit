using System;
using PolicePursuit.Core;
using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Kinematic arcade car: auto-gas, steer, drift charges a boost.
    public class PlayerCar : MonoBehaviour
    {
        public const float BoostFactor = 1.3f;
        public const float HalfWidth = 0.9f;
        public const float HalfLength = 2f;
        public const float RoadHalfWidth = 5f;
        public const float MaxHealth = 100f;

        const float Accel = 12f;

        public float Health { get; private set; } = MaxHealth;
        public Car Spec { get; private set; } = CarList.All[0];
        public float TopSpeed => Spec.TopSpeed;
        public void Repair() => Health = MaxHealth;
        public float Speed { get; private set; }
        public float ButtonSteer { get; set; }
        public bool ButtonDrift { get; set; }

        public event Action Destroyed;
        public event Action Damaged;

        float driftCharge;
        float boostLeft;
        Transform body;

        void Awake()
        {
            body = Shapes.Car(transform, "Body", Shapes.Cyan).transform;
            ResetCar();
        }

        public void Apply(Car car)
        {
            Spec = car;
            Shapes.Paint(body.GetChild(0).GetComponent<Renderer>(), Shapes.Hex(car.Color));
            ResetCar();
        }

        public void ResetCar()
        {
            Health = MaxHealth;
            Speed = 0;
            driftCharge = 0;
            boostLeft = 0;
            ButtonSteer = 0;
            ButtonDrift = false;
            transform.position = Vector3.zero;
            body.localRotation = Quaternion.identity;
        }

        // Returns metres travelled this frame.
        public float Tick(float dt, float keySteer, bool keyDrift)
        {
            float steer = Mathf.Clamp(keySteer + ButtonSteer, -1, 1);
            bool drifting = keyDrift || ButtonDrift;

            if (drifting)
            {
                driftCharge = Mathf.Min(1, driftCharge + 0.5f * dt);
            }
            else
            {
                if (driftCharge > 0.2f) boostLeft = 1.5f * driftCharge;
                driftCharge = 0;
            }
            boostLeft = Mathf.Max(0, boostLeft - dt);

            // Never above BoostFactor x TopSpeed. Server anti-cheat allows top speed x 1.1 on average.
            float target = boostLeft > 0 ? TopSpeed * BoostFactor : TopSpeed;
            if (drifting) target = TopSpeed * 0.6f;
            Speed = Mathf.MoveTowards(Speed, target, Accel * dt);

            var p = transform.position;
            p.x = Mathf.Clamp(p.x + steer * Spec.Handling * (drifting ? 1.4f : 1f) * dt,
                -RoadHalfWidth + HalfWidth, RoadHalfWidth - HalfWidth);
            p.z += Speed * dt;
            transform.position = p;
            body.localRotation = Quaternion.Euler(0, steer * (drifting ? 25 : 8), 0);

            return Speed * dt;
        }

        public void TakeDamage(float amount)
        {
            if (Health <= 0) return;
            Health = Mathf.Max(0, Health - amount * (1 - Spec.Armor));
            Damaged?.Invoke();
            if (Health <= 0) Destroyed?.Invoke();
        }
    }
}
