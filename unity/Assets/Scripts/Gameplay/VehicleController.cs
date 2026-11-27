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
            float drive = braking ? 0 : pedal * MaxTorque * (1 - t); // torque fades towards top speed
            if (pedal < 0 && !braking) drive *= 0.5f; // slower in reverse
            float brake = braking ? BrakeTorque * Mathf.Abs(pedal) : 0;

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
