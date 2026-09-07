using UnityEngine;

namespace Roloc.Presentation
{
    public sealed class SafeArea : MonoBehaviour
    {
        Rect last;
        Vector2 lastSize;
        void Update()
        {
            Rect safe = Screen.safeArea;
            Vector2 size = new Vector2(Screen.width, Screen.height);
            if (safe == last && size == lastSize || size.x <= 0 || size.y <= 0) return;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
            rect.anchorMax = new Vector2(safe.xMax / size.x, safe.yMax / size.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            last = safe; lastSize = size;
        }
    }
}
