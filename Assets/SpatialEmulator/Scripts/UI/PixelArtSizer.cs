// PixelArtSizer.cs — draws a pixel-art element on a canvas that isn't
// integer-scaled (ArcadeControls scales by screen width) at the same pixel
// size as the pixel HUD: one art pixel = PixelCanvasScaler.Scale screen
// pixels. It sizes the RectTransform, the 9-slice borders
// (pixelsPerUnitMultiplier) and an optional label from sizes given in art
// pixels, and keeps them right if the screen changes.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class PixelArtSizer : MonoBehaviour
    {
        public Vector2 artSize = new Vector2(36, 20);
        public Image image;
        public TMP_Text label;
        [Tooltip("Font size in art pixels (8 = Game Compact's native size).")]
        public float labelArtSize = 8f;
        [Tooltip("Label insets from the button's edges in art pixels: left, bottom, right, top.")]
        public Vector4 labelInsets = new Vector4(2, 5, 2, 1);
        [Tooltip("Also place the element at artOffset art pixels from its anchor.")]
        public bool useArtOffset;
        public Vector2 artOffset;

        /// Canvas units per art pixel, for offsets (e.g. a pressed label).
        public float ArtPixel { get; private set; } = 1f;

        Canvas _canvas;
        PixelCanvasScaler _pixelScaler;
        float _applied = -1f;

        void OnEnable() => _applied = -1f;

        void LateUpdate()
        {
            if (!_canvas) _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (!_pixelScaler) _pixelScaler = FindAnyObjectByType<PixelCanvasScaler>();
            if (!_canvas || _canvas.scaleFactor <= 0f) return;

            // Normally the pixel HUD's scale; if the HUD is switched off, the
            // same formula from this canvas's display size, so hiding the HUD
            // can't shrink the controls.
            int scale = _pixelScaler && _pixelScaler.isActiveAndEnabled
                ? _pixelScaler.Scale
                : PixelCanvasScaler.ScaleFor(_canvas.renderingDisplaySize);
            float artPixel = scale / _canvas.scaleFactor;
            if (Mathf.Approximately(artPixel, _applied)) return;
            _applied = artPixel;
            ArtPixel = artPixel;

            var self = (RectTransform)transform;
            self.sizeDelta = artSize * artPixel;
            if (useArtOffset) self.anchoredPosition = artOffset * artPixel;
            // Sliced borders are drawn at borderPixels / multiplier canvas units.
            if (image) image.pixelsPerUnitMultiplier = 1f / artPixel;
            if (label)
            {
                label.fontSize = labelArtSize * artPixel;
                var rect = label.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(labelInsets.x, labelInsets.y) * artPixel;
                rect.offsetMax = new Vector2(-labelInsets.z, -labelInsets.w) * artPixel;
            }
        }
    }
}
