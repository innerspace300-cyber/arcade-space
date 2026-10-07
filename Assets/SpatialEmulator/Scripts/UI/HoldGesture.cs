// HoldGesture.cs — press-and-hold on a UI element (a game row). Fires
// onHold once the finger has stayed put for holdSeconds; a drag (the list
// scrolling) cancels it. Deliberately not a drag handler, so the ScrollRect
// behind it still receives the drag.

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpatialEmulator.UI
{
    public class HoldGesture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public float holdSeconds = 0.5f;

        public event Action onHold;

        /// True from the moment a hold fires until the finger lifts, so the
        /// tap that would follow can be ignored.
        public bool Held { get; private set; }

        PointerEventData _pointer;
        float _downTime;

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointer = eventData;
            _downTime = Time.unscaledTime;
            Held = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pointer = null;
        }

        /// Call from the click handler: true if this click ends a hold and
        /// should be ignored. Resets the flag.
        public bool ConsumeHold()
        {
            bool held = Held;
            Held = false;
            return held;
        }

        void OnDisable()
        {
            _pointer = null;
            Held = false;
        }

        void Update()
        {
            if (_pointer == null) return;
            // The input module keeps updating the same event object while the
            // finger is down, so it reports the drag as it happens.
            if (_pointer.dragging || !_pointer.eligibleForClick)
            {
                _pointer = null;
                return;
            }
            if (Time.unscaledTime - _downTime < holdSeconds) return;
            _pointer = null;
            Held = true;
            onHold?.Invoke();
        }
    }
}
