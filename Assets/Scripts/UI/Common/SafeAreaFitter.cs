using UnityEngine;

namespace LastGround.UI.Common
{
    /// <summary>Fits a full-screen RectTransform into Screen.safeArea (notch / punch-hole, TDD_01 §3.6).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rect;
        Rect _applied;
        Vector2Int _screen;

        void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != _applied || Screen.width != _screen.x || Screen.height != _screen.y)
                Apply();
        }

        void Apply()
        {
            Rect safe = Screen.safeArea;
            _applied = safe;
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;
            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
