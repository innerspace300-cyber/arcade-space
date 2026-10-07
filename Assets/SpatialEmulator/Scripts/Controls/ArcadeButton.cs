// ArcadeButton.cs — an on-screen arcade button (action button, Coin, Start)
// that holds one RetroPad input while a finger is on it. Driven by
// ArcadeTouchRouter, which tracks every finger, so buttons work while the
// D-pad is held (the UI EventSystem here only sees one finger).

using SpatialEmulator.Games;
using SpatialEmulator.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.Controls
{
    public class ArcadeButton : MonoBehaviour
    {
        [Tooltip("RetroPad input this button holds. libretro-mame maps B/A/Y to MAME Buttons 1/2/3, Select to Coin.")]
        public RetroPadButton button = RetroPadButton.B;
        [Tooltip("Emulated frames a tap stays pressed at minimum; Coin needs longer for some games to register it.")]
        public int minHoldFrames = 3;

        [Tooltip("Coin, in games where the coin slot picks the player (PlayerSlots): holding it this long opens the player chooser; a shorter press inserts a coin when released.")]
        public float chooserHoldSeconds = 0.5f;

        [Header("Press feedback (optional)")]
        public Graphic visual;
        public Color pressedTint = new Color(0.7f, 0.7f, 0.7f, 1f);
        public float pressedScale = 0.92f;

        [Header("Keycap press (optional; replaces tint and scale)")]
        [Tooltip("Sprite shown while held, swapped into visual (an Image).")]
        public Sprite pressedSprite;
        [Tooltip("Moved down by pressedDrop art pixels while held.")]
        public RectTransform pressedContent;
        public float pressedDrop = 1f;
        public PixelArtSizer sizer;
        [Tooltip("Recoloured while held (e.g. a neon letter going dark on a filled button).")]
        public Graphic pressedLabel;
        public Color pressedLabelColor = Color.clear;

        bool _down;
        bool _coinPending;
        float _downAt;
        Color _normalColor;
        Vector3 _normalScale;
        Sprite _normalSprite;
        Color _normalLabelColor;

        public bool isHeld => _down;

        /// The keycap's art, up and held (ControlColors); null keeps the art it has.
        public Sprite NormalSprite => _normalSprite;
        public void SetLook(Sprite normal, Sprite pressed)
        {
            if (!normal) return;
            _normalSprite = normal;
            if (pressed) pressedSprite = pressed;
            if (visual is Image image) image.sprite = _down && pressedSprite ? pressedSprite : _normalSprite;
        }

        /// Counts presses that reached the game (for Coin, the coins
        /// inserted: a hold that opened the player chooser doesn't count).
        public int pressCount { get; private set; }

        void Awake()
        {
            if (visual) _normalColor = visual.color;
            if (visual is Image image) _normalSprite = image.sprite;
            if (pressedLabel) _normalLabelColor = pressedLabel.color;
            _normalScale = transform.localScale;
        }

        void OnDisable()
        {
            _coinPending = false;
            SetHeld(false);
        }

        void Update()
        {
            if (!_down || !_coinPending || Time.unscaledTime - _downAt < chooserHoldSeconds) return;
            _coinPending = false;
            PlayerSelect.Open();
        }

        public void SetHeld(bool held)
        {
            if (held == _down) return;
            _down = held;
            if (held) Haptics.Play(Haptics.Kind.Tick);   // a tick under the thumb
            if (button == RetroPadButton.Select && PlayerSlots.Current != null)
            {
                // Wait to see whether this is a tap (coin) or a hold (chooser).
                if (held) { _coinPending = true; _downAt = Time.unscaledTime; }
                else if (_coinPending)
                {
                    _coinPending = false;
                    ArcadeInput.SetPressed(button, true, minHoldFrames);
                    ArcadeInput.SetPressed(button, false, minHoldFrames);
                    pressCount++;
                    PlayerSlots.NoteCoin();
                }
            }
            else
            {
                ArcadeInput.SetPressed(button, held, minHoldFrames);
                if (held)
                {
                    pressCount++;
                    if (button == RetroPadButton.Select) PlayerSlots.NoteCoin();
                }
            }
            if (pressedSprite && visual is Image keycap)
            {
                keycap.sprite = held ? pressedSprite : _normalSprite;
                if (pressedLabel && pressedLabelColor.a > 0f)
                    pressedLabel.color = held ? pressedLabelColor : _normalLabelColor;
                if (pressedContent)
                {
                    float step = pressedDrop * (sizer ? sizer.ArtPixel : 1f);
                    pressedContent.anchoredPosition += new Vector2(0, held ? -step : step);
                }
                return;
            }
            if (visual) visual.color = held ? _normalColor * pressedTint : _normalColor;
            transform.localScale = held ? _normalScale * pressedScale : _normalScale;
        }
    }
}
