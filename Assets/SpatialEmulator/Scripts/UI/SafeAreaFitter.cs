// SafeAreaFitter.cs — stretches a RectTransform over Screen.safeArea, so
// its children stay clear of the notch and the home indicator.

using UnityEngine;

namespace SpatialEmulator.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        Rect _applied;
        Vector2Int _screen;

        void OnEnable() => Apply(true);

        void Update() => Apply(false);

        void Apply(bool force)
        {
            Rect safe = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && safe == _applied && screen == _screen) return;
            if (screen.x <= 0 || screen.y <= 0) return;
            _applied = safe;
            _screen = screen;

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            rect.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
