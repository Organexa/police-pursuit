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

        // W or Up = gas, S or Down = brake / reverse.
        public static float Pedal()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return 0;
            float p = 0;
            if (k.upArrowKey.isPressed || k.wKey.isPressed) p += 1;
            if (k.downArrowKey.isPressed || k.sKey.isPressed) p -= 1;
            return p;
#else
            return Input.GetAxisRaw("Vertical");
#endif
        }

        // F enters or leaves the car; Shift sprints and Space jumps on foot.
        public static bool Interact() => Pressed(KeyCode.F);
        public static bool JumpPressed() => Pressed(KeyCode.Space);
        public static bool ReloadPressed() => Pressed(KeyCode.R);
        public static bool HornPressed() => Pressed(KeyCode.H);
        public static bool SirenPressed() => Pressed(KeyCode.N);
        // J punch, K kick, Q dodge, hold L to block.
        public static bool PunchPressed() => Pressed(KeyCode.J);
        public static bool KickPressed() => Pressed(KeyCode.K);
        public static bool DodgePressed() => Pressed(KeyCode.Q);

        // Hold G to handcuff.
        public static bool ArrestHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.gKey.isPressed;
#else
            return Input.GetKey(KeyCode.G);
#endif
        }

        public static bool BlockHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.lKey.isPressed;
#else
            return Input.GetKey(KeyCode.L);
#endif
        }

        // Ctrl fires on foot (editor testing; the mouse is left for the on-screen buttons and camera drag).
        public static bool FireHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;
#else
            return Input.GetKey(KeyCode.LeftControl);
#endif
        }

        public static bool Sprint()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#else
            return Input.GetKey(KeyCode.LeftShift);
#endif
        }

        static bool Pressed(KeyCode code)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            switch (code)
            {
                case KeyCode.F: return k.fKey.wasPressedThisFrame;
                case KeyCode.R: return k.rKey.wasPressedThisFrame;
                case KeyCode.H: return k.hKey.wasPressedThisFrame;
                case KeyCode.N: return k.nKey.wasPressedThisFrame;
                case KeyCode.J: return k.jKey.wasPressedThisFrame;
                case KeyCode.K: return k.kKey.wasPressedThisFrame;
                case KeyCode.Q: return k.qKey.wasPressedThisFrame;
                default: return k.spaceKey.wasPressedThisFrame;
            }
#else
            return Input.GetKeyDown(code);
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
