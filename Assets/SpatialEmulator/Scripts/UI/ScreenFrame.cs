// ScreenFrame.cs — a pixel-art frame around the cabinet's screen, in AR:
// a world-space canvas just behind the back layer, with a checkerboard
// backing, so with layer spacing the sprites pop out of the frame. It grows
// with the layer spacing so that, seen from the front, it still surrounds the
// front layer. Follows the screen's size.
//
// Lives in ARCabinet.prefab (built by GamePickerBuilder).

using SpatialEmulator.Mobile;
using UnityEngine;

namespace SpatialEmulator.UI
{
    public class ScreenFrame : MonoBehaviour
    {
        public MobileRetroDepthLayerStack stack;
        [Tooltip("Frame pixels per screen height: 224 makes one frame pixel the size of one game pixel.")]
        public float pixelsPerScreenHeight = 224f;
        [Tooltip("Frame pixels between the frame's outer edge and the screen.")]
        public float border = 12f;
        [Tooltip("How far behind the back layer the frame sits, in metres.")]
        public float backGap = 0.001f;

        [Tooltip("Typical viewing distance to the front layer, in metres (cabinet scale 1): " +
                 "the frame grows by (viewDistance + stack depth) / viewDistance.")]
        public float viewDistance = 0.7f;

        float _height = -1f, _depth = -1f;
        Vector3 _center = Vector3.positiveInfinity;
        float _aspect = -1f;

        /// How much the frame has grown with the layer spacing (1 = none).
        public float Grow { get; private set; } = 1f;
        /// Half the frame's outer width, in the cabinet's local metres.
        public float OuterHalfWidth { get; private set; }
        /// Half the frame's outer height, in the cabinet's local metres.
        public float OuterHalfHeight { get; private set; }
        /// The frame's opening (the screen), in frame pixels.
        public Vector2 InnerSize => ((RectTransform)transform).sizeDelta - 2f * border * Vector2.one;
        /// Bumped whenever the frame moves or resizes.
        public int Version { get; private set; }

        // Settings' cabinet colours (CabinetStyles).
        void Awake() => CabinetStyles.Apply(this);

        void LateUpdate()
        {
            if (!stack) return;
            float height = stack.screenHeight * stack.pixelAspect;
            float depth = stack.FrontOffset;
            // (And wherever the picture is: the CRT cabinet moves it, and back;
            // and its shape: tall for a vertical game.)
            Vector3 center = stack.transform.localPosition;
            float aspect = stack.PictureAspect;
            if (Mathf.Approximately(height, _height) && Mathf.Approximately(depth, _depth) && center == _center && Mathf.Approximately(aspect, _aspect)) return;
            _height = height;
            _depth = depth;
            _center = center;
            _aspect = aspect;

            var rect = (RectTransform)transform;
            float scale = stack.screenHeight / pixelsPerScreenHeight;
            float width = stack.screenHeight * aspect * stack.pixelAspect;
            float grow = (viewDistance + depth) / viewDistance;
            rect.localScale = Vector3.one * scale * grow;
            rect.sizeDelta = new Vector2(width / scale + 2 * border, stack.screenHeight / scale + 2 * border);
            rect.localPosition = new Vector3(center.x, center.y, center.z + backGap);
            Grow = grow;
            OuterHalfWidth = rect.sizeDelta.x * rect.localScale.x * 0.5f;
            OuterHalfHeight = rect.sizeDelta.y * rect.localScale.y * 0.5f;
            Version++;
        }
    }
}
