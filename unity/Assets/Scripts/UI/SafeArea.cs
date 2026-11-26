using UnityEngine;

namespace PolicePursuit.UI
{
    // Keeps a full-screen RectTransform inside notches and rounded corners.
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect applied;

        void Update()
        {
            var area = Screen.safeArea;
            if (area == applied || Screen.width == 0 || Screen.height == 0) return;
            applied = area;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
