// CabinetManipulator.cs — select, move, scale, rotate and delete the placed
// cabinet. Nothing touches it until it's selected, so play can't knock it
// around:
//  - tap the cabinet to select it; tap it again, or tap empty space, to let go;
//  - while it's selected a gizmo shows three arrows, drag one to slide the
//    cabinet along it (X = its left/right, Y = up/down, Z = toward/away), and
//    a ring around its base, drag it to turn the cabinet;
//  - two fingers anywhere pinch to scale and twist to rotate it;
//  - the Delete button, shown only while it's selected, removes it.
// After idleTimeout seconds without a touch the gizmo and Delete button fade
// out (as the planes do, CabinetPlaneFader) and the cabinet lets go; tap it
// to select it again. Touches that begin on the arcade controls
// (ArcadeTouchRouter) or on any UI are ignored. This replaces XRI's grab +
// ARTransformer, which moved the cabinet on any drag with no selection step.

using System.Collections.Generic;
using SpatialEmulator.Controls;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace SpatialEmulator
{
    // After ArcadeTouchRouter (-200), which claims control fingers first.
    [DefaultExecutionOrder(-150)]
    public class CabinetManipulator : MonoBehaviour
    {
        public SingleCabinetGate gate;
        public ArcadeTouchRouter router;
        [Tooltip("Shown only while the cabinet is selected; fades with the gizmo.")]
        public Button deleteButton;
        [Tooltip("SpatialEmulator/GizmoOverlay. Referenced here so player builds include it.")]
        public Shader gizmoShader;

        [Header("Limits")]
        public float minScale = 0.3f;
        [Tooltip("25x makes the 0.4 m screen 10 m tall - drive-in size. The AR camera's far clip plane " +
                 "must reach past a cabinet this big (the scene's is 300 m).")]
        public float maxScale = 25f;

        [Header("Timing")]
        [Tooltip("Seconds after the last touch before the gizmo (and the planes) start to fade.")]
        public float idleTimeout = 3f;
        [Tooltip("Seconds the gizmo takes to fade out, after which the cabinet is deselected. Matches the planes' fade.")]
        public float fadeSeconds = 1f;

        [Header("Touch")]
        [Tooltip("Longest press, in seconds, that counts as a tap.")]
        public float tapSeconds = 0.35f;
        [Tooltip("Finger travel, in inches, beyond which a press is no longer a tap.")]
        public float tapSlopInches = 0.12f;
        [Tooltip("How close, in inches, a finger must land to an arrow or the ring to grab it.")]
        public float handleReachInches = 0.3f;
        [Tooltip("Pinch ratio change before scaling starts, so a twist doesn't also resize.")]
        public float scaleDeadZone = 0.04f;
        [Tooltip("Twist, in degrees, before rotating starts, so a pinch doesn't also turn it.")]
        public float rotateDeadZone = 6f;

        /// Time.unscaledTime of the last touch that placed, selected or moved
        /// the cabinet (CabinetPlaneFader shows the planes for a while after).
        public float lastInteractionTime { get; private set; } = float.NegativeInfinity;

        public bool isSelected => _selected && _cabinet;

        /// While locked, taps can't select the cabinet, so playing up close
        /// never brings up the gizmo by accident (CabinetLockButton).
        public bool locked
        {
            get => _locked;
            set
            {
                _locked = value;
                if (value) SetSelected(false);
            }
        }
        bool _locked;

        /// How visible the selection UI is: 1 while editing, fading to 0.
        public float editAlpha => isSelected && _gizmo ? _gizmo.alpha : 0f;

        class Finger
        {
            public Vector2 start, position;
            public float startTime;
            public bool moved, ignored, gestured;
        }

        readonly Dictionary<int, Finger> _fingers = new Dictionary<int, Finger>();
        readonly List<int> _active = new List<int>();
        readonly List<int> _gone = new List<int>();
        readonly HashSet<int> _seen = new HashSet<int>();
        static readonly List<RaycastResult> s_uiHits = new List<RaycastResult>();
        PointerEventData _pointer;
        EventSystem _pointerEventSystem;

        GameObject _cabinet;
        BoxCollider _box;
        CabinetOrientation _orientation;
        bool _selected;
        CabinetGizmo _gizmo;
        CanvasGroup _deleteGroup;

        // Handle drag in progress: an arrow (0-2) or the ring.
        int _dragFinger = int.MaxValue;
        int _dragHandle = CabinetGizmo.None;
        Vector2 _dragStartScreen;
        Vector3 _dragDirection, _dragStartPosition;
        Vector2 _dragScreenPerMeter;
        Quaternion _ringStartRotation;
        float _ringLastAngle, _ringTurned;
        float _turnFrom;   // CabinetOrientation's turn as the ring or twist began (moved along at the range's ends)
        bool _ringOnSurface;

        // Two-finger gesture in progress.
        bool _twoFinger, _scaling, _rotating;
        int _gestureA, _gestureB;
        float _gestureDistance, _gestureAngle, _gestureScale, _twisted;
        Quaternion _gestureRotation;

        float Dpi => Screen.dpi > 0f ? Screen.dpi : 326f;
        float Reach => handleReachInches * Dpi;

        void OnEnable()
        {
            if (!_gizmo && gizmoShader) _gizmo = CabinetGizmo.Create(gizmoShader);
            if (router) router.scenePriority = OnGizmoHandle;
            if (deleteButton)
            {
                _deleteGroup = deleteButton.GetComponent<CanvasGroup>();
                if (!_deleteGroup) _deleteGroup = deleteButton.gameObject.AddComponent<CanvasGroup>();
                deleteButton.onClick.AddListener(DeleteCabinet);
                deleteButton.gameObject.SetActive(false);
            }
        }

        void OnDisable()
        {
            if (router && router.scenePriority == OnGizmoHandle) router.scenePriority = null;
            if (deleteButton) deleteButton.onClick.RemoveListener(DeleteCabinet);
            SetSelected(false);
        }

        void OnDestroy()
        {
            if (_gizmo) Destroy(_gizmo.gameObject);
        }

        bool OnGizmoHandle(Vector2 screenPosition)
            => isSelected && _gizmo && _gizmo.HitTest(screenPosition, Reach) != CabinetGizmo.None;

        void Update()
        {
            TrackCabinet();

            // Full screen: the cabinet's out of sight (AR's off), so taps
            // don't select it - no gizmo or DELETE popping up, nothing taking
            // touches from the joystick.
            if (UI.FullScreenTest.Enabled)
            {
                if (_selected) SetSelected(false);
                _gone.Clear();
                foreach (var id in _fingers.Keys) _gone.Add(id);
                foreach (var id in _gone) Release(id, tapped: false);
                if (router) router.dpadEnabled = _cabinet;
                return;
            }

            _seen.Clear();
            foreach (var touch in Touch.activeTouches)
            {
                bool ended = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                Track(touch.touchId, touch.screenPosition, touch.began, ended);
            }
#if UNITY_EDITOR
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame))
                Track(ArcadeTouchRouter.MouseId, mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame);
#endif
            _gone.Clear();
            foreach (var id in _fingers.Keys)
                if (!_seen.Contains(id)) _gone.Add(id);
            foreach (var id in _gone)
                Release(id, tapped: false);

            Manipulate();
            FadeWhenIdle();

            // The joystick only runs while playing: off until a cabinet is
            // placed (so a tap in its zone places one) and while the cabinet
            // is selected (so its zone can't fight the gizmo's handles).
            if (router) router.dpadEnabled = _cabinet && !isSelected;
        }

        // Follow the gate's cabinet: a new one starts unselected, and counts as
        // an interaction so the planes stay up for a moment after placing it.
        void TrackCabinet()
        {
            var cabinet = gate ? gate.cabinet : null;
            if (cabinet == _cabinet) return;
            SetSelected(false);
            _cabinet = cabinet;
            _box = cabinet ? cabinet.GetComponentInChildren<BoxCollider>() : null;
            _orientation = cabinet ? cabinet.GetComponent<CabinetOrientation>() : null;
            if (cabinet) lastInteractionTime = Time.unscaledTime;
        }

        void Track(int id, Vector2 position, bool began, bool ended)
        {
            if (began)
            {
                var finger = new Finger
                {
                    start = position,
                    position = position,
                    startTime = Time.unscaledTime,
                    ignored = (router && router.OwnsTouch(id)) || IsOverUI(position),
                };
                _fingers[id] = finger;
                if (!finger.ignored && isSelected && _dragHandle == CabinetGizmo.None && !_twoFinger)
                {
                    int handle = _gizmo.HitTest(position, Reach);
                    if (handle == CabinetGizmo.Ring) BeginTurn(id, position);
                    else if (handle != CabinetGizmo.None) BeginSlide(id, handle, position);
                }
            }
            if (!_fingers.TryGetValue(id, out var tracked)) return; // began before this was enabled
            _seen.Add(id);
            tracked.position = position;
            if ((position - tracked.start).magnitude > tapSlopInches * Dpi) tracked.moved = true;

            if (ended)
            {
                bool tapped = !tracked.ignored && !tracked.moved && !tracked.gestured && id != _dragFinger &&
                              Time.unscaledTime - tracked.startTime <= tapSeconds;
                Release(id, tapped);
            }
        }

        void Release(int id, bool tapped)
        {
            var finger = _fingers[id];
            _fingers.Remove(id);
            if (id == _dragFinger) EndDrag();
            if (tapped) Tap(finger.position);
        }

        void Manipulate()
        {
            _active.Clear();
            foreach (var pair in _fingers)
                if (!pair.Value.ignored) _active.Add(pair.Key);

            if (!isSelected)
            {
                _twoFinger = false;
                return;
            }
            if (_active.Count > 0) lastInteractionTime = Time.unscaledTime;

            // The gesture keeps the same two fingers until one lifts, so the
            // span between them can't flip direction mid-twist.
            if (_twoFinger && !(_fingers.ContainsKey(_gestureA) && _fingers.ContainsKey(_gestureB)))
                _twoFinger = false;
            if (!_twoFinger && _active.Count >= 2)
            {
                if (_dragHandle != CabinetGizmo.None) EndDrag(); // a second finger turns a drag into a pinch/twist
                BeginPinchTwist(_active[0], _active[1]);
            }
            if (_twoFinger)
            {
                Finger a = _fingers[_gestureA], b = _fingers[_gestureB];
                a.gestured = b.gestured = true;
                PinchTwist(a.position, b.position);
                return;
            }
            if (_dragHandle != CabinetGizmo.None && _fingers.TryGetValue(_dragFinger, out var dragger))
            {
                if (_dragHandle == CabinetGizmo.Ring) Turn(dragger.position);
                else Slide(dragger.position);
            }
        }

        // Fade the gizmo and Delete button out once idle, then let go.
        void FadeWhenIdle()
        {
            if (!isSelected) return;
            float idle = Time.unscaledTime - lastInteractionTime - idleTimeout;
            float alpha = fadeSeconds > 0f ? 1f - Mathf.Clamp01(idle / fadeSeconds) : (idle > 0f ? 0f : 1f);
            if (_gizmo) _gizmo.alpha = alpha;
            if (_deleteGroup) _deleteGroup.alpha = alpha;
            if (alpha <= 0f) SetSelected(false);
        }

        void Tap(Vector2 screenPosition)
        {
            if (!_cabinet) return;
            var cam = Camera.main;
            // The demo's fairy changes colour when tapped (even up close).
            if (cam && Demo.PitDemoGame.TryTap(cam.ScreenPointToRay(screenPosition))) return;
            bool onCabinet = !_locked && cam && _box && RayHitsBox(cam.ScreenPointToRay(screenPosition), _box);
            if (onCabinet)
            {
                SetSelected(!_selected);
                lastInteractionTime = Time.unscaledTime;
            }
            else if (_selected)
            {
                SetSelected(false);
            }
        }

        void SetSelected(bool selected)
        {
            selected &= _cabinet;
            if (_dragHandle != CabinetGizmo.None) EndDrag();
            _twoFinger = false;
            _selected = selected;
            if (_gizmo)
            {
                if (selected) _gizmo.Show(_cabinet.transform, _box ? _box.center : Vector3.zero, _box ? _box.size : Vector3.one * 0.5f);
                else _gizmo.Hide();
            }
            if (deleteButton) deleteButton.gameObject.SetActive(selected);
            if (_deleteGroup) _deleteGroup.alpha = 1f;
        }

        void DeleteCabinet()
        {
            if (!isSelected) return;
            var cabinet = _cabinet;
            SetSelected(false);
            Destroy(cabinet);
        }

        // ---- arrows: slide along an axis ----

        void BeginSlide(int fingerId, int axis, Vector2 screenPosition)
        {
            var cam = Camera.main;
            if (!cam) return;
            _dragFinger = fingerId;
            _dragHandle = axis;
            _dragDirection = _gizmo.Axis(axis);
            _dragStartPosition = _cabinet.transform.position;
            _dragStartScreen = screenPosition;

            // How far, in pixels, one meter along the axis moves on screen. An
            // axis pointing nearly at the camera barely moves on screen, so
            // cap the sensitivity rather than send the cabinet flying.
            Vector3 o = _gizmo.origin;
            Vector2 perMeter = (Vector2)(cam.WorldToScreenPoint(o + _dragDirection * 0.1f) - cam.WorldToScreenPoint(o)) * 10f;
            float minPerMeter = Screen.height * 0.1f;
            if (perMeter.magnitude < minPerMeter)
                perMeter = (perMeter.sqrMagnitude > 1e-6f ? perMeter.normalized : Vector2.up) * minPerMeter;
            _dragScreenPerMeter = perMeter;
            _gizmo.SetHighlight(axis);
        }

        void Slide(Vector2 screenPosition)
        {
            float meters = Vector2.Dot(screenPosition - _dragStartScreen, _dragScreenPerMeter) / _dragScreenPerMeter.sqrMagnitude;
            _cabinet.transform.position = _dragStartPosition + _dragDirection * meters;
        }

        // ---- ring: turn around the vertical axis ----

        void BeginTurn(int fingerId, Vector2 screenPosition)
        {
            _dragFinger = fingerId;
            _dragHandle = CabinetGizmo.Ring;
            _dragStartScreen = screenPosition;
            _ringStartRotation = _cabinet.transform.rotation;
            _ringTurned = 0f;
            _turnFrom = _orientation ? _orientation.Turn : 0f;
            _ringOnSurface = TryRingAngle(screenPosition, out _ringLastAngle);
            _gizmo.SetHighlight(CabinetGizmo.Ring);
        }

        // The finger's point on the ring's plane drags the ring around, so the
        // cabinet turns as if grabbed. CabinetOrientation holds it to its range
        // and eases it there.
        void Turn(Vector2 screenPosition)
        {
            if (_ringOnSurface)
            {
                if (!TryRingAngle(screenPosition, out float angle)) return;
                _ringTurned += Mathf.DeltaAngle(_ringLastAngle, angle);
                _ringLastAngle = angle;
            }
            else
            {
                // The camera is level with the ring, so there's no usable point
                // on its plane: a sideways drag turns it, 90 degrees per inch
                // (dragging the near edge right turns it counterclockwise).
                _ringTurned = -(screenPosition.x - _dragStartScreen.x) / Dpi * 90f;
            }
            TurnBy(_ringTurned, _ringStartRotation);
        }

        // The cabinet turned `gesture` degrees (clockwise from above) since the
        // ring or twist began. At the end of its range the start moves along,
        // so turning back answers at once (no slack to take up first).
        void TurnBy(float gesture, Quaternion startRotation)
        {
            if (!_orientation)
            {
                _cabinet.transform.rotation = Quaternion.AngleAxis(gesture, Vector3.up) * startRotation;
                return;
            }
            float want = _turnFrom + gesture;
            _orientation.SetTurn(want);
            if (!Mathf.Approximately(_orientation.Turn, want)) _turnFrom = _orientation.Turn - gesture;
        }

        bool TryRingAngle(Vector2 screenPosition, out float angle)
        {
            angle = 0f;
            var cam = Camera.main;
            if (!cam) return false;
            Vector3 center = _gizmo.ringCenter;
            var ray = cam.ScreenPointToRay(screenPosition);
            if (!new Plane(Vector3.up, center).Raycast(ray, out float distance) || distance > 20f) return false;
            Vector3 offset = ray.GetPoint(distance) - center;
            if (offset.sqrMagnitude < 1e-4f) return false;
            angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg; // Unity yaw: clockwise from +Z seen from above
            return true;
        }

        void EndDrag()
        {
            _dragFinger = int.MaxValue;
            _dragHandle = CabinetGizmo.None;
            if (_gizmo) _gizmo.SetHighlight(CabinetGizmo.None);
        }

        // ---- two fingers: pinch to scale, twist to rotate ----

        void BeginPinchTwist(int fingerA, int fingerB)
        {
            _twoFinger = true;
            _gestureA = fingerA;
            _gestureB = fingerB;
            _scaling = _rotating = false;
            Span(_fingers[fingerA].position, _fingers[fingerB].position, out _gestureDistance, out _gestureAngle);
            _gestureScale = _cabinet.transform.localScale.x;
            _gestureRotation = _cabinet.transform.rotation;
        }

        static void Span(Vector2 a, Vector2 b, out float distance, out float angle)
        {
            Vector2 span = b - a;
            distance = Mathf.Max(span.magnitude, 1f);
            angle = Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg;
        }

        void PinchTwist(Vector2 a, Vector2 b)
        {
            Span(a, b, out float distance, out float angle);
            var t = _cabinet.transform;

            // Each starts past its dead zone and re-bases there, so it doesn't jump.
            if (!_scaling && Mathf.Abs(distance / _gestureDistance - 1f) > scaleDeadZone)
            {
                _scaling = true;
                _gestureDistance = distance;
                _gestureScale = t.localScale.x;
            }
            if (_scaling)
            {
                // Grows from CabinetOrientation's scale center (the screen's
                // middle for a wall-mounted cabinet), which stays put.
                Vector3 center = _orientation ? _orientation.scaleCenterLocal : Vector3.zero;
                Vector3 anchor = t.TransformPoint(center);
                t.localScale = Vector3.one * Mathf.Clamp(_gestureScale * distance / _gestureDistance, minScale, maxScale);
                t.position += anchor - t.TransformPoint(center);
            }

            // (Added up a step at a time, so twisting round past half a turn
            // doesn't flip it.)
            float step = Mathf.DeltaAngle(_gestureAngle, angle);
            if (!_rotating)
            {
                if (Mathf.Abs(step) <= rotateDeadZone) return;
                _rotating = true;
                _gestureAngle = angle;
                _gestureRotation = t.rotation;
                _twisted = 0f;
                _turnFrom = _orientation ? _orientation.Turn : 0f;
                return;
            }
            _twisted += step;
            _gestureAngle = angle;
            // Screen twist is counterclockwise-positive; turning the cabinet the
            // same way seen from above is a negative yaw.
            TurnBy(-_twisted, _gestureRotation);
        }

        // ---- helpers ----

        bool IsOverUI(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (!eventSystem) return false;
            if (_pointer == null || _pointerEventSystem != eventSystem)
            {
                _pointer = new PointerEventData(eventSystem);
                _pointerEventSystem = eventSystem;
            }
            _pointer.position = screenPosition;
            s_uiHits.Clear();
            eventSystem.RaycastAll(_pointer, s_uiHits);
            foreach (var hit in s_uiHits)
                if (!(hit.module is PhysicsRaycaster)) return true; // graphics only, not 3D objects
            return false;
        }

        static bool RayHitsBox(Ray worldRay, BoxCollider box)
        {
            var t = box.transform;
            var local = new Ray(t.InverseTransformPoint(worldRay.origin), t.InverseTransformVector(worldRay.direction));
            return new Bounds(box.center, box.size).IntersectRay(local);
        }
    }
}
