// ArcadeDPad.cs — floating 8-way on-screen stick (drawn as an arcade
// joystick: a base and a knob that follows the thumb). It stays hidden until
// a finger lands in its zone (this RectTransform), then appears centered
// under that finger. The finger's offset from the pad's center picks the
// direction(s), so diagonals work and the thumb can roll between directions
// without lifting; if the thumb drifts past the pad's edge the pad follows
// (within the zone), so reversing never needs a long slide back. Driven by ArcadeTouchRouter,
// which tracks every finger (the UI EventSystem here only sees one).
//
// Until the player has used it once, the pad rests visible at restPoint
// whenever steering is available (router sets available), so it's clear
// the stick is there; after the first thumb lifts it goes back to floating.

using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.Controls
{
    [RequireComponent(typeof(RectTransform))]
    public class ArcadeDPad : MonoBehaviour
    {
        [Tooltip("The pad's visuals: a child of this zone, shown under the finger while one is down.")]
        public RectTransform pad;
        [Tooltip("Fraction of the pad's radius around the center that counts as no direction.")]
        [Range(0f, 0.9f)] public float deadZone = 0.2f;
        [Tooltip("Drag the pad along when the finger moves past its edge.")]
        public bool followFinger = true;

        [Header("Visuals (optional)")]
        [Tooltip("Joystick knob, a child of the pad; follows the thumb up to knobTravel of the pad's radius.")]
        public RectTransform knob;
        [Range(0f, 1f)] public float knobTravel = 1f;
        public Graphic upKey;
        public Graphic downKey;
        public Graphic leftKey;
        public Graphic rightKey;
        public Color pressedTint = new Color(0.55f, 0.55f, 0.55f, 1f);

        [Header("Resting pad (before first use)")]
        public bool showAtRest = true;
        [Tooltip("Where the pad rests, as a fraction of the zone (0,0 = bottom left). Ignored when restBeside is set.")]
        public Vector2 restPoint = new Vector2(0.5f, 0.5f);
        [Tooltip("Rest just left of this control, level with its centre (the right arrow points at it).")]
        public RectTransform restBeside;
        [Tooltip("Gap between the pad and restBeside, in art pixels.")]
        public float restGapArt = 4f;
        public SpatialEmulator.UI.PixelArtSizer padSizer;

        /// Set by ArcadeTouchRouter: whether the stick can steer right now
        /// (a cabinet is placed and not being edited).
        public bool available { get; set; }

        bool _used;

        RectTransform _zone;
        int? _fingerId;
        Color _upColor, _downColor, _leftColor, _rightColor;

        void Awake()
        {
            _zone = (RectTransform)transform;
            if (upKey) _upColor = upKey.color;
            if (downKey) _downColor = downKey.color;
            if (leftKey) _leftColor = leftKey.color;
            if (rightKey) _rightColor = rightKey.color;
            if (pad) pad.gameObject.SetActive(false);
        }

        void OnDisable() => SetFinger(null, default, null);

        /// The finger steering the pad (null when none is) and its screen position.
        /// A finger id the pad hasn't seen yet re-centers the pad under it.
        public void SetFinger(int? fingerId, Vector2 screenPosition, Camera eventCamera)
        {
            if (fingerId == null || !pad ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(_zone, screenPosition, eventCamera, out var local))
            {
                if (_fingerId != null) _used = true; // first thumb lifted: float from now on
                _fingerId = null;
                if (pad)
                {
                    bool rest = showAtRest && !_used && available && isActiveAndEnabled;
                    pad.gameObject.SetActive(rest);
                    if (rest) pad.localPosition = RestPosition();
                }
                if (knob) knob.localPosition = Vector3.zero;
                SetDirections(false, false, false, false);
                ArcadeInput.SetPush(0f);
                return;
            }

            if (fingerId != _fingerId)
            {
                _fingerId = fingerId;
                pad.localPosition = local;
                pad.gameObject.SetActive(true);
            }

            Vector2 center = pad.localPosition;
            Vector2 offset = local - center;
            float radius = Mathf.Min(pad.rect.width, pad.rect.height) * 0.5f;
            if (followFinger && offset.magnitude > radius)
            {
                // Follow, but keep the center inside the zone so a long drag
                // can't pull the pad over the buttons.
                Rect zone = _zone.rect;
                center = local - offset.normalized * radius;
                center = new Vector2(Mathf.Clamp(center.x, zone.xMin, zone.xMax), Mathf.Clamp(center.y, zone.yMin, zone.yMax));
                pad.localPosition = center;
                offset = local - center;
            }
            if (knob) knob.localPosition = Vector2.ClampMagnitude(offset, radius * knobTravel);
            if (offset.magnitude < deadZone * radius)
            {
                SetDirections(false, false, false, false);
                ArcadeInput.SetPush(0f);
                return;
            }
            ArcadeInput.SetPush(radius > 0f ? offset.magnitude / radius : 1f);

            // Eight 45-degree sectors centered on the axes and diagonals.
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            bool right = angle > -67.5f && angle < 67.5f;
            bool up = angle > 22.5f && angle < 157.5f;
            bool left = angle > 112.5f || angle < -112.5f;
            bool down = angle > -157.5f && angle < -22.5f;
            SetDirections(up, down, left, right);
        }

        Vector2 RestPosition()
        {
            if (!restBeside) return Rect.NormalizedToPoint(_zone.rect, restPoint);
            var corners = new Vector3[4];
            restBeside.GetWorldCorners(corners); // bottom-left, top-left, top-right, bottom-right
            Vector2 leftMiddle = _zone.InverseTransformPoint((corners[0] + corners[1]) * 0.5f);
            float radius = pad.rect.width * 0.5f;
            float gap = restGapArt * (padSizer ? padSizer.ArtPixel : 1f);
            // Keep the whole pad on screen (the zone's left edge is the screen's).
            float x = Mathf.Max(leftMiddle.x - gap - radius, _zone.rect.xMin + radius);
            return new Vector2(x, leftMiddle.y);
        }

        void SetDirections(bool up, bool down, bool left, bool right)
        {
            ArcadeInput.SetPressed(RetroPadButton.Up, up);
            ArcadeInput.SetPressed(RetroPadButton.Down, down);
            ArcadeInput.SetPressed(RetroPadButton.Left, left);
            ArcadeInput.SetPressed(RetroPadButton.Right, right);
            Tint(upKey, _upColor, up);
            Tint(downKey, _downColor, down);
            Tint(leftKey, _leftColor, left);
            Tint(rightKey, _rightColor, right);
        }

        void Tint(Graphic key, Color normal, bool pressed)
        {
            if (key) key.color = pressed ? normal * pressedTint : normal;
        }
    }
}
