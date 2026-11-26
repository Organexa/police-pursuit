using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PolicePursuit.Gameplay
{
    // Keyboard for editor testing, plus Android Back (reported as Escape).
    public static class KeyInput
    {
        public static float TiltSteer(float x, float sensitivity) => Mathf.Clamp(x * 2.5f * sensitivity, -1, 1);

        // shortcut: no calibration or dead zone, tune on a real phone.
        public static float Tilt(float sensitivity)
        {
#if ENABLE_INPUT_SYSTEM
            var a = Accelerometer.current;
            if (a == null) return 0;
            if (!a.enabled) InputSystem.EnableDevice(a);
            return TiltSteer(a.acceleration.ReadValue().x, sensitivity);
#else
            return TiltSteer(Input.acceleration.x, sensitivity);
#endif
        }

        public static float Steer()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return 0;
            float s = 0;
            if (k.leftArrowKey.isPressed || k.aKey.isPressed) s -= 1;
            if (k.rightArrowKey.isPressed || k.dKey.isPressed) s += 1;
            return s;
#else
            return Input.GetAxisRaw("Horizontal");
#endif
        }

        public static bool Drift()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
#else
            return Input.GetKey(KeyCode.Space);
#endif
        }

        public static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
