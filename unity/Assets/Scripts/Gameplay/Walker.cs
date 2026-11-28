using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // The player on foot: walk, run, sprint and jump, moving relative to the camera.
    // shortcut: capsule figure with no animation; swap in a rigged character and an Animator when a pack is chosen.
    public class Walker : MonoBehaviour
    {
        public const float WalkSpeed = 2.5f, RunSpeed = 4.5f, SprintSpeed = 7f, JumpSpeed = 6f, Gravity = 20f;
        public const float Height = 1.8f, Radius = 0.35f;

        public Vector2 Move { get; set; } // stick or keys, -1..1 each axis
        public bool Sprint { get; set; }
        public bool Jump { get; set; }
        public float SpeedScale { get; set; } = 1; // wading halves it
        public float Speed { get; private set; } // horizontal m/s
        public CharacterController Body { get; private set; }
        float fall;

        public static Walker Create(Transform parent)
        {
            var go = new GameObject("Walker");
            go.transform.SetParent(parent, false);
            Shapes.Box(go.transform, new Vector3(0, Height / 2, 0), new Vector3(Radius * 2, Height / 2, Radius * 2),
                Shapes.Cyan, PrimitiveType.Capsule);
            Shapes.Box(go.transform, new Vector3(0, 1.55f, 0.3f), new Vector3(0.3f, 0.12f, 0.12f), Color.white); // shows facing
            var w = go.AddComponent<Walker>();
            w.Body = go.AddComponent<CharacterController>();
            w.Body.height = Height;
            w.Body.radius = Radius;
            w.Body.center = new Vector3(0, Height / 2, 0);
            return w;
        }

        public void Place(Vector3 feet, float yaw)
        {
            Body.enabled = false; // a CharacterController overrides a direct move
            transform.SetPositionAndRotation(feet, Quaternion.Euler(0, yaw, 0));
            Body.enabled = true;
            fall = 0;
            Speed = 0;
        }

        // Called by OpenWorld before it moves the camera, so the camera never lags a frame behind.
        public void Tick(float cameraYaw, float dt)
        {
            var input = Vector2.ClampMagnitude(Move, 1);
            float speed = input.magnitude < 0.1f ? 0
                : Sprint ? SprintSpeed
                : input.magnitude > 0.8f ? RunSpeed : WalkSpeed;
            speed *= SpeedScale;
            var dir = Quaternion.Euler(0, cameraYaw, 0) * new Vector3(input.x, 0, input.y).normalized;
            if (speed > 0)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1 - Mathf.Exp(-12 * dt));

            if (Body.isGrounded)
            {
                fall = -1; // keeps it pressed to the ground on slopes
                if (Jump) fall = JumpSpeed;
            }
            fall -= Gravity * dt;
            Jump = false;
            var before = transform.position;
            Body.Move((dir * speed + Vector3.up * fall) * dt);
            var moved = transform.position - before;
            Speed = new Vector2(moved.x, moved.z).magnitude / Mathf.Max(dt, 1e-5f);
        }
    }
}
