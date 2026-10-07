// CabinetOrientation.cs — the layer stack is a one-sided diorama (flat quads
// facing the viewer), so it only reads correctly from the front.
//
// On a floor or table it faces the AR camera once, at the moment it's
// placed. After that it's turned only through Turn/SetTurn (CabinetManipulator's
// twist gesture and ring), within a small range around that facing - enough
// to see the layers stand apart, never so far the depth gives itself away -
// and it eases to each new angle, so a fast drag can't make it jump about.
//
// On a wall it hangs like a TV: flush against the wall with the screen
// facing straight out and the screen centred on the tapped point. It turns
// within the same range as on a floor (turning shows off the layer
// separation), and scaling grows it from the screen's centre.
//
// On a ceiling it hangs down like a suspended TV: its top just under the
// tapped point, facing the camera and turnable like on a floor, and scaling
// grows it downward from its top.
//
// The template's ObjectSpawner places the cabinet with its up axis along
// the surface normal, which is how a wall is recognised; that happens after
// OnEnable, so the first LateUpdate reads it.
//
// It's sized to fit the phone's view from where it was placed (FitToView)
// before it's ever seen: it spawns too small to see, and as soon as its
// screen frame and side panel have sized themselves (a frame or two) it
// takes the scale that makes them fitWidth of the picture across, and no
// more than fitHeight of it top to bottom (the controls cover the bottom of
// the screen), within CabinetManipulator's scale limits.
//
// Runs late, after the frame's gestures have set where it's turning to.

using SpatialEmulator.Mobile;
using UnityEngine;

namespace SpatialEmulator
{
    [DefaultExecutionOrder(1000)]
    public class CabinetOrientation : MonoBehaviour
    {
        [Tooltip("Degrees of yaw the cabinet may be rotated away from its initial camera-facing orientation.")]
        public float maxRotationDegrees = 25f;
        [Tooltip("Seconds it takes to ease round to where it's been turned.")]
        public float turnSmoothing = 0.12f;
        [Tooltip("Seconds after spawn to keep tracking the camera before locking the base facing — covers the placement frames so the lock isn't taken before the cabinet has settled.")]
        public float settleSeconds = 0.4f;
        [Tooltip("A surface whose normal is within this many degrees of horizontal counts as a wall.")]
        public float wallMaxNormalElevation = 30f;
        [Tooltip("Gap between a wall-mounted cabinet's back and the wall, or a ceiling-hung cabinet's top and the ceiling, in meters.")]
        public float wallGap = 0.01f;
        [Tooltip("On placing, the cabinet (frame and side panel) is scaled to this fraction of the camera's view across.")]
        public float fitWidth = 0.88f;
        [Tooltip("...and to no more than this fraction of it top to bottom.")]
        public float fitHeight = 0.5f;

        /// Placed on a wall (hangs like a TV) rather than stood on a surface.
        public bool wallMounted { get; private set; }

        /// Placed, turned to the camera and fitted to the view.
        public bool Settled => _placed && _fitted && !_settling;

        /// Placed and sized to the view - the first frame it's seen (it may
        /// still be turning to face the camera).
        public bool Fitted => _placed && _fitted;

        /// How far it's been turned from its first facing, in degrees
        /// (positive clockwise seen from above): where it's heading, within
        /// +-maxRotationDegrees.
        public float Turn => _turnTarget;

        /// Turns it to `degrees` from its first facing - held to the range;
        /// it eases there.
        public void SetTurn(float degrees) => _turnTarget = Mathf.Clamp(degrees, -maxRotationDegrees, maxRotationDegrees);

        float _turn, _turnTarget, _turnSpeed;

        /// Hung from a ceiling.
        public bool ceilingMounted { get; private set; }

        /// The local point pinch-scaling grows from: the pivot (the base) on a
        /// floor, the screen's centre on a wall, the top on a ceiling.
        public Vector3 scaleCenterLocal => wallMounted ? ScreenCenterLocal : ceilingMounted ? TopLocal : Vector3.zero;

        float _baseYaw;
        float _settleUntil;
        bool _settling;
        bool _placed;
        bool _initialized;
        bool _fitted;
        int _fitFrames;
        const float HiddenScale = 0.0001f;   // until it's fitted

        Vector3 ScreenCenterLocal
        {
            get
            {
                var stack = GetComponentInChildren<MobileRetroDepthLayerStack>(true);
                return stack ? transform.InverseTransformPoint(stack.transform.position) : Vector3.zero;
            }
        }

        // Top of the cabinet's collider, above the pivot.
        Vector3 TopLocal
        {
            get
            {
                var box = GetComponentInChildren<BoxCollider>(true);
                if (!box) return ScreenCenterLocal;
                Vector3 top = box.center + Vector3.up * box.size.y * 0.5f;
                return transform.InverseTransformPoint(box.transform.TransformPoint(top));
            }
        }

        void OnEnable()
        {
            _settling = true;
            _settleUntil = Time.unscaledTime + settleSeconds;
            _placed = false;
            _initialized = false;
            _fitted = false;
            _fitFrames = 0;
            transform.localScale = Vector3.one * HiddenScale;
            wallMounted = false;
            ceilingMounted = false;
        }

        void FaceCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 toCamera = cam.transform.position - transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 0.0001f) return;
            // The layer quads' correct (unmirrored) side faces the stack's
            // local -Z, so the cabinet's forward (+Z) must point AWAY from
            // the camera for the viewer to see the front, not the back.
            transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
            _baseYaw = transform.eulerAngles.y;
            _turn = _turnTarget = _turnSpeed = 0f;
            _initialized = true;
        }

        // First frame after spawning: the spawner has set up = surface normal.
        void Place()
        {
            _placed = true;
            Vector3 normal = transform.up;
            float elevation = Mathf.Asin(Mathf.Clamp(normal.y, -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 outward = new Vector3(normal.x, 0f, normal.z);
            if (elevation < -(90f - wallMaxNormalElevation))
            {
                // Ceiling: stand upright (the camera-facing settle turns it),
                // hanging with its top just under the tapped point.
                ceilingMounted = true;
                transform.rotation = Quaternion.identity;
                Vector3 tap = transform.position;
                transform.position += tap - transform.TransformPoint(new Vector3(0f, TopLocal.y, 0f)) + Vector3.down * wallGap;
                FaceCamera();
                return;
            }
            if (Mathf.Abs(elevation) > wallMaxNormalElevation || outward.sqrMagnitude < 0.0001f)
                return; // floor or table: face the camera

            wallMounted = true;
            _settling = false;
            outward.Normalize();
            // Forward into the wall, so the front faces out of it.
            transform.rotation = Quaternion.LookRotation(-outward, Vector3.up);
            _baseYaw = transform.eulerAngles.y;
            _turn = _turnTarget = _turnSpeed = 0f;
            _initialized = true;
            // Centre the screen on the tapped point, just off the wall.
            Vector3 tapped = transform.position;
            transform.position += tapped - transform.TransformPoint(ScreenCenterLocal) + outward * wallGap;
        }

        // The scale that makes the frame and side panel fill the view across
        // (or fitHeight of it top to bottom, if that's smaller), from the
        // camera's distance to their middle - which moves as it grows from
        // scaleCenterLocal, so it's worked out a few times over.
        bool FitToView()
        {
            var cam = Camera.main;
            if (!cam || !LocalExtents(out var min, out var max)) return false;
            Vector3 middle = (min + max) * 0.5f;
            // Across, it's centred on its screen where it was aimed (the
            // panel hangs off one side): the wider half counts twice.
            float width = 2f * Mathf.Max(-min.x, max.x), height = max.y - min.y;
            if (width <= 0f || height <= 0f) return false;
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), tanH = tanV * cam.aspect;
            Vector3 centerLocal = scaleCenterLocal;
            Vector3 anchor = transform.TransformPoint(centerLocal);
            float scale = transform.localScale.x;
            for (int i = 0; i < 4; i++)
            {
                Vector3 at = anchor + transform.rotation * ((middle - centerLocal) * scale);
                float distance = Vector3.Distance(cam.transform.position, at);
                scale = Mathf.Min(fitWidth * 2f * distance * tanH / width, fitHeight * 2f * distance * tanV / height);
            }
            var manipulator = FindAnyObjectByType<CabinetManipulator>();
            scale = Mathf.Clamp(scale, manipulator ? manipulator.minScale : 0.3f, manipulator ? manipulator.maxScale : 25f);
            transform.localScale = Vector3.one * scale;
            transform.position += anchor - transform.TransformPoint(centerLocal);
            return true;
        }

        // The screen frame's and side panel's corners, in the cabinet's own (unscaled) space.
        bool LocalExtents(out Vector3 min, out Vector3 max)
        {
            min = Vector3.one * float.MaxValue;
            max = Vector3.one * float.MinValue;
            bool any = false;
            var corners = new Vector3[4];
            var frame = GetComponentInChildren<UI.ScreenFrame>(true);
            var panel = GetComponentInChildren<UI.SaveStatePanel>(true);
            foreach (var rect in new[] { frame ? (RectTransform)frame.transform : null, panel ? (RectTransform)panel.transform : null })
            {
                if (!rect || !rect.gameObject.activeInHierarchy) continue;
                rect.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var local = transform.InverseTransformPoint(corner);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                    any = true;
                }
            }
            return any;
        }

        void LateUpdate()
        {
            if (!_placed) Place();
            if (!_fitted)
            {
                // Once the frame has sized itself (and the panel beside it, a
                // frame on); after half a second regardless, at full size if
                // it can't be fitted.
                var frame = GetComponentInChildren<UI.ScreenFrame>(true);
                _fitFrames++;
                if ((frame && frame.Version > 0 && _fitFrames >= 2) || _fitFrames > 30)
                {
                    _fitted = true;
                    if (!FitToView()) transform.localScale = Vector3.one;
                }
            }
            if (_settling)
            {
                FaceCamera();
                if (Time.unscaledTime >= _settleUntil) _settling = false;
                return;
            }
            if (!_initialized) return;
            // Upright (no pitch or roll), eased round toward its turn.
            _turn = Mathf.SmoothDamp(_turn, _turnTarget, ref _turnSpeed, turnSmoothing, Mathf.Infinity, Time.unscaledDeltaTime);
            transform.rotation = Quaternion.Euler(0f, _baseYaw + _turn, 0f);
        }
    }
}
