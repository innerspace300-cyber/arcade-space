// PixelCanvasScaler.cs — scales a pixel-art canvas by a whole number, so
// one art pixel is exactly N screen pixels (7 on an iPhone 13 Pro Max) and
// point-filtered sprites and bitmap fonts stay crisp with no uneven pixels.
// Lay the canvas out in art pixels; N is the largest scale that still fits
// minArtWidth x minArtHeight art pixels on screen.

using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasScaler))]
    public class PixelCanvasScaler : MonoBehaviour
    {
        [Tooltip("Narrowest layout the canvas is designed for, in art pixels.")]
        public int minArtWidth = 176;
        [Tooltip("Shortest layout the canvas is designed for, in art pixels.")]
        public int minArtHeight = 300;

        CanvasScaler _scaler;
        Canvas _canvas;

        /// Screen pixels per art pixel.
        public int Scale { get; private set; } = 1;

        public const int DefaultMinArtWidth = 176;
        public const int DefaultMinArtHeight = 300;

        /// The whole-number scale for a display of this size (see Apply).
        public static int ScaleFor(Vector2 display, int minArtWidth = DefaultMinArtWidth, int minArtHeight = DefaultMinArtHeight)
        {
#if UNITY_EDITOR
            // Width only in the Editor: the Game view is usually far shorter
            // than a phone, and the height limit would shrink the HUD below the
            // device size. On a portrait phone the width decides anyway.
            return Mathf.Max(1, (int)display.x / minArtWidth);
#else
            return Mathf.Max(1, Mathf.Min((int)display.x / minArtWidth, (int)display.y / minArtHeight));
#endif
        }

        void OnEnable()
        {
            _scaler = GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            // The canvas's own display size, not Screen: in edit mode Screen is
            // whichever editor window happens to be drawing (a 2240x40 toolbar,
            // say), which shrank the whole pixel HUD - and everything
            // PixelArtSizer matches to it - in the Scene and Game views.
            if (!_canvas) _canvas = GetComponent<Canvas>();
            Vector2 display = _canvas ? _canvas.renderingDisplaySize : new Vector2(Screen.width, Screen.height);
            if (display.x <= 0 || display.y <= 0) return;
            int scale = ScaleFor(display, minArtWidth, minArtHeight);
            Scale = scale;
            if (!Mathf.Approximately(_scaler.scaleFactor, scale))
                _scaler.scaleFactor = scale;
        }
    }
}
