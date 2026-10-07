// PressOffset.cs — nudges a button's label down while it's held, to go with
// a keycap sprite whose pressed state sits lower.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    [RequireComponent(typeof(Selectable))]
    public class PressOffset : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public RectTransform content;
        public Vector2 pressedOffset = new Vector2(0, -1);

        Vector2 _rest;
        bool _pressed;

        void Awake()
        {
            if (content) _rest = content.anchoredPosition;
        }

        void OnDisable() => Set(false);

        public void OnPointerDown(PointerEventData eventData) => Set(GetComponent<Selectable>().IsInteractable());
        public void OnPointerUp(PointerEventData eventData) => Set(false);
        public void OnPointerExit(PointerEventData eventData) => Set(false);

        void Set(bool pressed)
        {
            if (!content || pressed == _pressed) return;
            _pressed = pressed;
            content.anchoredPosition = pressed ? _rest + pressedOffset : _rest;
        }
    }
}
