// ScrollBoost.cs — makes a ScrollRect's drag go further than the finger
// (factor 1.5: half again as far), and so its fling faster too (the
// ScrollRect's own momentum is taken from how fast the list moved). Added
// after the ScrollRect on its object, so each drag step runs just after the
// ScrollRect's and stretches what it moved. The game list's (GamePicker).

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollBoost : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public float factor = 1.5f;

        ScrollRect _scroll;
        Vector2 _start, _set;

        void Awake()
        {
            _scroll = GetComponent<ScrollRect>();
            _scroll.scrollSensitivity *= factor;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_scroll.content) _start = _set = _scroll.content.anchoredPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_scroll.content || eventData.button != PointerEventData.InputButton.Left) return;
            var content = _scroll.content;
            if (content.anchoredPosition == _set) return;   // (the ScrollRect didn't move it: nothing to stretch)
            _set = content.anchoredPosition = _start + (content.anchoredPosition - _start) * factor;
        }
    }
}
