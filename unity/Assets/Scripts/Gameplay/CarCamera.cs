using UnityEngine;

namespace PolicePursuit.Gameplay
{
    // Chase or bumper camera; the choice is remembered.
    public class CarCamera : MonoBehaviour
    {
        const string Key = "cam_mode";
        static readonly Vector3 Chase = new Vector3(0, 4, -9);
        static readonly Vector3 Bumper = new Vector3(0, 1.1f, 2.2f);

        Transform target;
        bool bumper;

        public static CarCamera Create(Transform target)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = NightLook.Sky;
            UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam)
                .renderPostProcessing = true;
            cam.farClipPlane = 350;

            var rig = cam.gameObject.AddComponent<CarCamera>();
            rig.target = target;
            rig.bumper = PlayerPrefs.GetInt(Key, 0) == 1;
            rig.Snap();
            return rig;
        }

        public void Toggle()
        {
            bumper = !bumper;
            PlayerPrefs.SetInt(Key, bumper ? 1 : 0);
            Snap();
        }

        public void Snap()
        {
            transform.position = target.position + (bumper ? Bumper : Chase);
            transform.rotation = Quaternion.Euler(bumper ? 2 : 15, 0, 0);
        }

        void LateUpdate()
        {
            var wanted = target.position + (bumper ? Bumper : Chase);
            if (!bumper) wanted.x = Mathf.Lerp(transform.position.x, wanted.x, 8 * Time.deltaTime);
            // z is locked to the car so the follow distance does not grow with speed.
            transform.position = wanted;
        }
    }
}
