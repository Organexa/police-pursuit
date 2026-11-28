using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Physics car for the open world: Rigidbody + four WheelColliders, rear-wheel drive.
    // shortcut: one balanced handling preset and no gears; add presets and a gearbox with vehicle classes.
    public class VehicleController : MonoBehaviour
    {
        public const float Mass = 1400;
        public const float TopSpeed = 50; // m/s, about 180 km/h
        const float MaxTorque = 1800, BrakeTorque = 3500, HandbrakeTorque = 6000;
        const float SteerLow = 32, SteerHigh = 8; // degrees at standstill and at top speed

        public Rigidbody Body { get; private set; }
        public float Speed => Vector3.Dot(Body.linearVelocity, transform.forward); // forward m/s, negative in reverse
        public float SpeedKmh => Mathf.Abs(Speed) * 3.6f;

        public float Steer { get; set; }    // -1..1
        public float Pedal { get; set; }    // +1 gas, -1 brake then reverse
        public bool Handbrake { get; set; }
        public float SpeedLimit { get; set; } = TopSpeed; // AI cars get less, so they never out-drive the player

        WheelCollider[] wheels; // front left, front right, rear left, rear right
        float stuckTime;

        // Damage (only cars with TakesDamage, i.e. yours: AI cars stay fair and never break down).
        // Hits over DamageFrom m/s of change in speed cost condition; at 0 the engine dies until the car is repaired.
        public const float MaxCondition = 100, DamageFrom = 4, DamagePerMs = 6;
        public bool TakesDamage { get; set; }
        public float Condition { get; private set; } = MaxCondition;
        public bool Wrecked => Condition <= 0;
        public void Repair() => Condition = MaxCondition;

        void OnCollisionEnter(Collision hit)
        {
            if (!TakesDamage) return;
            float change = hit.impulse.magnitude / Body.mass; // m/s this car's speed changed by
            if (change > DamageFrom) Condition = Mathf.Max(0, Condition - (change - DamageFrom) * DamagePerMs);
        }

        // Brake lights glow while braking or on the handbrake; headlights are always on (it is night).
        public bool BrakeLightsOn { get; private set; }
        Renderer[] brakeLamps;

        void AddLamps()
        {
            var body = transform.GetChild(0);
            foreach (float x in new[] { -0.6f, 0.6f })
                Shapes.Box(body, new Vector3(x, 0.75f, 2.02f), new Vector3(0.4f, 0.15f, 0.05f), Shapes.Hex(0xFFF1C9), glow: 1.5f).name = "Headlight";
            brakeLamps = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                var lamp = Shapes.Box(body, new Vector3(i == 0 ? -0.65f : 0.65f, 0.8f, -2.02f), new Vector3(0.35f, 0.12f, 0.05f), Shapes.Hex(0x5A0A0A));
                lamp.name = "Brake light";
                brakeLamps[i] = lamp.GetComponent<Renderer>();
            }
        }

        void ShowBrakeLights(bool on)
        {
            if (on == BrakeLightsOn || brakeLamps == null) return; // only on a change: no per-frame property blocks
            BrakeLightsOn = on;
            foreach (var r in brakeLamps) Shapes.Paint(r, on ? Shapes.Red : Shapes.Hex(0x5A0A0A), on ? 2 : 0);
        }

        // A rear tyre (0 left, 1 right) that is sliding sideways or locked under braking, and where it touches the road.
        // Wheelspin under power never counts: every full-throttle start left a long pair of marks.
        // Measured here (Unity 6000.6, 2026-11-28): forwardSlip is about +1 spinning under power, -1 locked braking.
        public const float SkidSlip = 0.35f;
        public bool Sliding(int rear, out Vector3 at)
        {
            at = default;
            if (!wheels[2 + rear].GetGroundHit(out var hit) || Mathf.Abs(Speed) < 3) return false;
            at = hit.point;
            return Mathf.Abs(hit.sidewaysSlip) > SkidSlip || hit.forwardSlip < -SkidSlip * 1.5f;
        }

        // A police car gets a red and blue light bar on the roof.
        public static VehicleController Create(Transform parent, Vector3 pos, Color paint, bool police = false)
        {
            var go = new GameObject(police ? "Police" : "Vehicle");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var body = Shapes.Car(go.transform, "Body", paint).transform;
            Models.Dress(body, paint, Models.PlayerLod);
            var car = go.AddComponent<VehicleController>();
            if (police) car.AddLightBar();
            return car;
        }

        public bool HasLightBar => red != null;
        public bool LightsShowing => HasLightBar && (red.activeSelf || blue.activeSelf);
        // The colour the light bar is showing right now (red, blue), or clear when dark.
        public Color LightColour => !HasLightBar ? Color.clear : red.activeSelf ? Shapes.Red : blue.activeSelf ? Color.blue : Color.clear;
        GameObject red, blue;

        public void AddLightBar()
        {
            if (HasLightBar) return;
            var body = transform.GetChild(0);
            float roof = Models.Available ? 1.28f : 1.45f;
            red = Shapes.Box(body, new Vector3(-0.4f, roof, -0.3f), new Vector3(0.6f, 0.2f, 0.4f), Shapes.Red, glow: 1);
            blue = Shapes.Box(body, new Vector3(0.4f, roof, -0.3f), new Vector3(0.6f, 0.2f, 0.4f), Color.blue, glow: 1.5f);
        }

        // Alternates red and blue while on; both dark while off.
        public void Flash(bool on)
        {
            if (!HasLightBar) return;
            bool redOn = Time.time % 0.4f < 0.2f;
            red.SetActive(on && redOn);
            blue.SetActive(on && !redOn);
        }

        void Awake()
        {
            Body = gameObject.AddComponent<Rigidbody>();
            Body.mass = Mass;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.linearDamping = 0.05f;
            Body.angularDamping = 0.5f;

            var hull = gameObject.AddComponent<BoxCollider>();
            hull.center = new Vector3(0, 0.75f, 0);
            hull.size = new Vector3(1.8f, 0.7f, 4f);
            Body.centerOfMass = new Vector3(0, 0.35f, 0.1f); // low, so it does not roll over in turns

            wheels = new[]
            {
                Wheel("FL", -0.8f, 1.3f), Wheel("FR", 0.8f, 1.3f),
                Wheel("RL", -0.8f, -1.3f), Wheel("RR", 0.8f, -1.3f),
            };
            AddLamps();
        }

        WheelCollider Wheel(string name, float x, float z)
        {
            var go = new GameObject("Wheel " + name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(x, 0.55f, z);
            var w = go.AddComponent<WheelCollider>();
            w.mass = 20;
            w.radius = 0.35f;
            w.suspensionDistance = 0.25f;
            w.suspensionSpring = new JointSpring { spring = 35000, damper = 4500, targetPosition = 0.5f };
            var side = w.sidewaysFriction;
            side.stiffness = 1.6f; // balanced preset: grippy but slides under the handbrake
            w.sidewaysFriction = side;
            var fwd = w.forwardFriction;
            fwd.stiffness = 1.5f;
            w.forwardFriction = fwd;
            return w;
        }

        void FixedUpdate()
        {
            float speed = Speed;
            float t = Mathf.Clamp01(Mathf.Abs(speed) / SpeedLimit);
            float steer = Mathf.Clamp(Steer, -1, 1) * Mathf.Lerp(SteerLow, SteerHigh, t);
            wheels[0].steerAngle = wheels[1].steerAngle = steer;

            float pedal = Mathf.Clamp(Pedal, -1, 1);
            // Brake while rolling forward; once nearly stopped the same pedal reverses (and gas brakes a reversing car).
            bool braking = (pedal < 0 && speed > 1) || (pedal > 0 && speed < -1);
            float drive = braking || Wrecked ? 0 : pedal * MaxTorque * (1 - t); // torque fades towards top speed
            if (pedal < 0 && !braking) drive *= 0.5f; // slower in reverse
            float brake = braking ? BrakeTorque * Mathf.Abs(pedal) : 0;
            ShowBrakeLights(braking || Handbrake);

            for (int i = 0; i < 4; i++)
            {
                bool rear = i >= 2;
                wheels[i].motorTorque = rear ? drive : 0;
                wheels[i].brakeTorque = rear && Handbrake ? HandbrakeTorque : brake;
                if (rear)
                {
                    var side = wheels[i].sidewaysFriction;
                    side.stiffness = Handbrake ? 0.7f : 1.6f;
                    wheels[i].sidewaysFriction = side;
                }
            }
            if (pedal == 0 && !Handbrake && Mathf.Abs(speed) < 0.5f)
                foreach (var w in wheels) w.brakeTorque = 500; // parked cars do not creep down slopes

            Body.AddForce(-transform.up * Body.linearVelocity.sqrMagnitude * 3f); // downforce
            Recover(Time.fixedDeltaTime);
        }

        // Puts a car that has rolled over back on its wheels after two seconds.
        void Recover(float dt)
        {
            stuckTime = transform.up.y < 0.3f && Body.linearVelocity.magnitude < 2 ? stuckTime + dt : 0;
            if (stuckTime > 2) Place(transform.position + Vector3.up, transform.eulerAngles.y);
        }

        public void Place(Vector3 pos, float yaw)
        {
            stuckTime = 0;
            Body.linearVelocity = Body.angularVelocity = Vector3.zero;
            Body.position = transform.position = pos;
            Body.rotation = transform.rotation = Quaternion.Euler(0, yaw, 0);
        }
    }
}
