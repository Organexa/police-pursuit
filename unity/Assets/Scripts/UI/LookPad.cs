using UnityEngine;
using UnityEngine.EventSystems;

namespace PolicePursuit.UI
{
    // Drag anywhere not covered by a button to turn the camera.
    public class LookPad : MonoBehaviour, IDragHandler
    {
        public float Yaw { get; set; } // degrees, read and reset by the open world each frame

        public void OnDrag(PointerEventData e) => Yaw += e.delta.x * 0.25f;
    }
}
