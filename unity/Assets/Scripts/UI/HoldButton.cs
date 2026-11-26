using UnityEngine;
using UnityEngine.EventSystems;

namespace PolicePursuit.UI
{
    // True while a finger or mouse is down on this button.
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Held { get; private set; }

        public void OnPointerDown(PointerEventData e) => Held = true;
        public void OnPointerUp(PointerEventData e) => Held = false;
        public void OnPointerExit(PointerEventData e) => Held = false;

        void OnDisable() => Held = false;
    }
}
