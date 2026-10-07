// CabinetLockButton.cs — toggles CabinetManipulator.locked. Locked, the
// cabinet can't be selected by a tap, so you can play right up close without
// the move/rotate gizmo popping up when a finger lands on the cabinet.
// With sprites set (the pixel keycap), locked shows the pressed-down cap;
// otherwise the background is tinted. It sits on the cabinet (under the
// saves panel), so it finds the scene's CabinetManipulator itself.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator
{
    [RequireComponent(typeof(Button))]
    public class CabinetLockButton : MonoBehaviour
    {
        public CabinetManipulator manipulator;
        public Image background;
        public TMP_Text label;
        public Color unlockedColor = new Color(0.62f, 0.64f, 0.68f);
        public Color lockedColor = new Color(1f, 0.72f, 0.2f);
        public Sprite unlockedSprite;
        public Sprite lockedSprite;

        void OnEnable()
        {
            if (!manipulator) manipulator = FindAnyObjectByType<CabinetManipulator>(FindObjectsInactive.Include);
            GetComponent<Button>().onClick.AddListener(Toggle);
            Refresh();
        }

        void OnDisable() => GetComponent<Button>().onClick.RemoveListener(Toggle);

        void Toggle()
        {
            if (!manipulator) return;
            manipulator.locked = !manipulator.locked;
            Refresh();
        }

        // (The screen's menu can lock it too: MenuDropdown.)
        bool _shown;
        void LateUpdate()
        {
            if (manipulator && manipulator.locked != _shown) Refresh();
        }

        void Refresh()
        {
            bool locked = manipulator && manipulator.locked;
            _shown = locked;
            if (background && unlockedSprite && lockedSprite) background.sprite = locked ? lockedSprite : unlockedSprite;
            else if (background) background.color = locked ? lockedColor : unlockedColor;
            if (label) label.text = locked ? "LOCKED" : "LOCK";
        }
    }
}
