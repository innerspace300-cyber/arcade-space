// ArcadeTouchRouter.cs — feeds every finger on the screen to the arcade
// controls. The EventSystem can't: under the Input System, XRI's
// XRUIInputModule (which the AR template uses) tracks a single pointer - one
// Touchscreen position - so holding the D-pad kept every button from
// registering. This reads all touches from EnhancedTouch instead.
//
// A finger belongs to what it first touched until it lifts: a button stays
// held, and the D-pad (a floating pad over its whole zone) follows it
// anywhere. Fingers that start anywhere else are left to the scene
// (CabinetManipulator, AR placement), which can ask OwnsTouch to skip ours.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace SpatialEmulator.Controls
{
    // Runs before the scene's touch users, so a finger's owner is settled by
    // the time they look at it.
    [DefaultExecutionOrder(-200)]
    public class ArcadeTouchRouter : MonoBehaviour
    {
        public const int MouseId = int.MinValue; // left mouse button stands in for one finger in the Editor

        [Tooltip("Seconds after the last control finger lifts that busy stays true, so AR placement doesn't treat its release as a tap.")]
        public float busyLinger = 0.2f;

        /// Off while there's nothing to steer (no cabinet placed), so a tap in
        /// the D-pad zone reaches the scene and can place one.
        [HideInInspector] public bool dpadEnabled = true;

        /// Screen positions where a new touch goes to the scene even inside
        /// the D-pad zone (the cabinet gizmo's arrows). Buttons still win.
        public Func<Vector2, bool> scenePriority;

        /// True while a finger is on the controls, and briefly after.
        public bool busy => _onControls || Time.unscaledTime < _busyUntil;

        public bool OwnsTouch(int touchId) => _owners.TryGetValue(touchId, out var owner) && owner;

        /// Where the finger holding `button` is now (screen pixels), if one is.
        /// (The built-in demo's spell wheel follows it, SpellWheel.)
        public bool TryGetFinger(ArcadeButton button, out Vector2 screenPosition) => _heldAt.TryGetValue(button, out screenPosition);

        /// The canvas the controls are on, and its camera (null for an overlay).
        public Canvas Canvas => _canvas;
        readonly Dictionary<ArcadeButton, Vector2> _heldAt = new Dictionary<ArcadeButton, Vector2>();

        Canvas _canvas;
        ArcadeDPad _dpad;
        ArcadeButton[] _buttons;
        // Finger id -> the control it began on, null for a finger left to the scene.
        readonly Dictionary<int, MonoBehaviour> _owners = new Dictionary<int, MonoBehaviour>();
        readonly HashSet<int> _seen = new HashSet<int>();
        readonly HashSet<ArcadeButton> _held = new HashSet<ArcadeButton>();
        readonly List<int> _gone = new List<int>();
        int? _dpadFinger;
        Vector2 _dpadPosition;
        bool _onControls;
        float _busyUntil;

        // Nothing to steer until CabinetManipulator says a cabinet is placed;
        // starting enabled flashed the resting joystick for a frame at launch.
        void Awake() => dpadEnabled = false;

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            _canvas = GetComponentInParent<Canvas>();
            _dpad = GetComponentInChildren<ArcadeDPad>(true);
            _buttons = GetComponentsInChildren<ArcadeButton>(true);
        }

        void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            _owners.Clear();
            _onControls = false;
            if (_dpad) _dpad.SetFinger(null, default, null);
            foreach (var button in _buttons)
                if (button) button.SetHeld(false);
        }

        void Update()
        {
            Camera eventCamera = _canvas && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            _seen.Clear();
            _held.Clear();
            _heldAt.Clear();
            _dpadFinger = null;

            foreach (var touch in Touch.activeTouches)
            {
                bool ended = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                Route(touch.touchId, touch.screenPosition, touch.began, ended, eventCamera);
            }
#if UNITY_EDITOR
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame))
                Route(MouseId, mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame, eventCamera);
#endif

            // Forget fingers that disappeared without reporting an end.
            _gone.Clear();
            foreach (var id in _owners.Keys)
                if (!_seen.Contains(id)) _gone.Add(id);
            foreach (var id in _gone)
                _owners.Remove(id);

            if (_dpad)
            {
                _dpad.available = dpadEnabled;
                _dpad.SetFinger(_dpadFinger, _dpadPosition, eventCamera);
            }
            foreach (var button in _buttons)
                if (button) button.SetHeld(_held.Contains(button));

            _onControls = false;
            foreach (var owner in _owners.Values)
                if (owner) { _onControls = true; break; }
            if (_onControls) _busyUntil = Time.unscaledTime + busyLinger;
        }

        void Route(int id, Vector2 position, bool began, bool ended, Camera eventCamera)
        {
            if (began)
                _owners[id] = HitTest(position, eventCamera);
            if (!_owners.TryGetValue(id, out var owner))
                return; // started before the controls were enabled
            _seen.Add(id);

            // A tap that begins and ends between two frames still counts as
            // held for one frame; ArcadeInput keeps it pressed long enough.
            if (owner == _dpad && owner) { _dpadFinger = id; _dpadPosition = position; }
            else if (owner is ArcadeButton button) { _held.Add(button); _heldAt[button] = position; }

            if (ended)
                _owners.Remove(id);
        }

        MonoBehaviour HitTest(Vector2 position, Camera eventCamera)
        {
            foreach (var button in _buttons)
                if (button && button.isActiveAndEnabled && Contains(button.transform, position, eventCamera))
                    return button;
            if (scenePriority != null && scenePriority(position))
                return null;
            // One thumb steers the pad; a second finger in the zone goes to the scene.
            if (dpadEnabled && _dpad && _dpad.isActiveAndEnabled && !DPadFingerDown() && Contains(_dpad.transform, position, eventCamera))
                return _dpad;
            return null;
        }

        bool DPadFingerDown()
        {
            foreach (var owner in _owners.Values)
                if (owner == _dpad) return true;
            return false;
        }

        static bool Contains(Transform rect, Vector2 position, Camera eventCamera)
            => RectTransformUtility.RectangleContainsScreenPoint((RectTransform)rect, position, eventCamera);
    }
}
