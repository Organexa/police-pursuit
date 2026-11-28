using UnityEngine;
using UnityEngine.EventSystems;

namespace PolicePursuit.UI
{
    // Virtual joystick: Value is -1..1 on each axis while a finger drags from the stick's centre.
    public class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Vector2 Value { get; private set; }
        public RectTransform Knob { get; set; }

        public void OnPointerDown(PointerEventData e) => OnDrag(e);
        public void OnPointerUp(PointerEventData e) => SetValue(Vector2.zero);

        public void OnDrag(PointerEventData e)
        {
            var rect = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out var local)) return;
            SetValue(Vector2.ClampMagnitude(local / (rect.rect.width / 2), 1));
        }

        void SetValue(Vector2 v)
        {
            Value = v;
            if (Knob != null) Knob.anchoredPosition = v * ((RectTransform)transform).rect.width / 2;
        }

        void OnDisable() => SetValue(Vector2.zero);
    }
}
