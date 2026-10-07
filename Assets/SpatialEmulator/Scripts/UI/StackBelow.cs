// StackBelow.cs — keeps a UI element a few pixels below another one that
// grows (the LOCK keycap under the saves panel, which gets taller as saves
// are added), or at the other one's top while it's hidden.

using UnityEngine;

namespace SpatialEmulator.UI
{
    public class StackBelow : MonoBehaviour
    {
        [Tooltip("Top-anchored element to sit under (same parent, pivot at its top).")]
        public RectTransform above;
        [Tooltip("Gap between the two, in canvas pixels.")]
        public float spacing = 6f;

        void LateUpdate()
        {
            var rect = (RectTransform)transform;
            float y = above.anchoredPosition.y;
            if (above.gameObject.activeInHierarchy) y -= above.rect.height + spacing;
            if (!Mathf.Approximately(rect.anchoredPosition.y, y))
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }
    }
}
