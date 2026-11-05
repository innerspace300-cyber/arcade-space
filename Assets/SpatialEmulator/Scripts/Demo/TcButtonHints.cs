// TcButtonHints.cs — THE TIME CRYSTAL's names for the on-screen buttons, over
// each while it's being played: A FIRE, B BOMB, C LASER (TimeCrystalGame's
// ReadControls). In the buttons' own font, each a touch of its button's
// colour. Off through the story and whenever another game's on (the buttons
// themselves are off through the story: GamepadInput).
// Full screen, the A, B and C buttons drop to just under the picture (LASER's
// label a few pixels below it) and COIN goes under C, level with B's letter;
// the stick's touch zone reaches up onto the picture (to its middle), as wide as ever.

using SpatialEmulator.Controls;
using TMPro;
using UnityEngine;

namespace SpatialEmulator.Demo
{
    public class TcButtonHints : MonoBehaviour
    {
        static TcButtonHints s_hints;
        static readonly (RetroPadButton button, string text)[] Names =
            { (RetroPadButton.B, "FIRE"), (RetroPadButton.A, "BOMB"), (RetroPadButton.Y, "LASER") };

        TMP_Text[] _labels;

        /// Starts it, the first time the game runs.
        public static void Ensure()
        {
            if (s_hints || !Application.isPlaying) return;
            s_hints = new GameObject("Time Crystal Button Hints").AddComponent<TcButtonHints>();
        }

        void Update()
        {
            bool on = TimeCrystalGame.Running && !TimeCrystal.TcStory.Active;
            if (_labels == null && on) Build();
            if (_labels == null) return;
            foreach (var label in _labels) if (label && label.gameObject.activeSelf != on) label.gameObject.SetActive(on);
        }

        // ---- Full screen: the buttons under the picture, COIN under C ----
        RectTransform[] _abc;
        RectTransform _coin;
        TMP_Text _bLetter;
        bool _coinInPicture;
        Canvas _coinOver;   // (still there while its insert plays out, after CONTINUE?'s gone)
        float _coinSpot;
        Vector2[] _abcHome;
        Vector2 _coinHome;
        Vector3 _coinScale;
        float _dy;   // (the buttons' drop now, in the controls' canvas units)
        bool _moved;
        RectTransform _stickZone;
        Vector2 _stickZoneHome;
        const float StickReach = 0.5f;   // (the zone's top: this far up the picture)

        void LateUpdate()
        {
            if (_abc == null) FindKeys();
            if (_abc == null) return;
            bool want = TimeCrystalGame.Running && UI.FullScreenTest.Enabled && UI.FullScreenTest.PictureRect.height > 0f && !UI.TvOutput.Showing;   // (the picture on the TV: the keys at home on the phone)
            if (!want)
            {
                if (_moved) Home();
                return;
            }
            var canvas = _abc[2].GetComponentInParent<Canvas>();
            float k = canvas ? canvas.rootCanvas.scaleFactor : 1f;   // (screen pixels per canvas unit)
            if (k <= 0f) return;
            // LASER's top (its label over C: about a quarter of C's height over it) a few pixels under the picture.
            var c = _abc[2];
            float cTop = (c.anchoredPosition.y + c.rect.height * 0.5f + c.rect.height * 0.24f) * k;
            float target = UI.FullScreenTest.PictureRect.yMin - Screen.width * 0.012f;
            float dy = _dy + (target - cTop) / k;
            // Never off the bottom (A's the lowest): the safe area's bottom, and a little.
            float floor = Screen.safeArea.yMin / k + 12f;
            dy = Mathf.Max(dy, floor - (_abcHome[0].y - _abc[0].rect.height * 0.5f));
            dy = Mathf.Min(dy, 0f);
            _dy = dy;
            for (int i = 0; i < 3; i++) _abc[i].anchoredPosition = _abcHome[i] + new Vector2(0f, dy);
            // The stick's zone (bottom left, pivot there): taller, up onto the picture; never shorter than it was.
            if (_stickZone)
            {
                var pic = UI.FullScreenTest.PictureRect;
                float zoneTop = (pic.yMin + pic.height * StickReach) / k;
                _stickZone.sizeDelta = new Vector2(_stickZoneHome.x, Mathf.Max(_stickZoneHome.y, zoneTop - _stickZone.anchoredPosition.y));
            }
            // COIN under C at its own size, level with B's letter (the "B" under the B button).
            if (_coin)
            {
                _coin.localScale = _coinScale;
                _coin.anchoredPosition = new Vector2(_abcHome[2].x, _abcHome[2].y + dy);
                if (_bLetter)
                {
                    _bLetter.ForceMeshUpdate();
                    float y = _bLetter.transform.TransformPoint(_bLetter.textBounds.center).y;
                    _coin.position = new Vector3(_coin.position.x, y, _coin.position.z);
                }
                // CONTINUE? up: in the picture, under INSERT COIN / TO CONTINUE (TcFighterSelect).
                float coinY = TimeCrystal.TcFighterSelect.CoinY;
                if (coinY >= 0f || (TimeCrystalGame.WantsCoin && _coinInPicture))
                {
                    if (coinY >= 0f) _coinSpot = coinY;
                    var pic = UI.FullScreenTest.PictureRect;
                    var root = canvas ? canvas.rootCanvas : null;
                    var at = new Vector2(pic.center.x, pic.yMin + pic.height * _coinSpot);
                    if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)_coin.parent, at,
                            root && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null, out var world))
                        _coin.position = world;
                    _coinInPicture = true;
                }
                else _coinInPicture = false;
                // In the picture, over its layers (the ships' layer, darkened, draws over the controls: FullScreenTest).
                if (_coinInPicture && !_coinOver)
                {
                    _coinOver = _coin.GetComponent<Canvas>() ?? _coin.gameObject.AddComponent<Canvas>();
                    _coinOver.overrideSorting = true;
                    _coinOver.sortingOrder = 19;   // (over the on-top layers' 15+, under the game picker's 20)
                }
                if (_coinOver) _coinOver.overrideSorting = _coinInPicture;   // (not disabled: a disabled canvas hides what's on it)
            }
            _moved = true;
        }

        void Home()
        {
            for (int i = 0; i < 3; i++) if (_abc[i]) _abc[i].anchoredPosition = _abcHome[i];
            if (_coin) { _coin.anchoredPosition = _coinHome; _coin.localScale = _coinScale; }
            if (_stickZone) _stickZone.sizeDelta = _stickZoneHome;
            _dy = 0f;
            _moved = false;
        }

        void FindKeys()
        {
            var buttons = FindObjectsByType<ArcadeButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var abc = new RectTransform[3];
            foreach (var b in buttons)
            {
                if (b.name == "Button A") abc[0] = (RectTransform)b.transform;
                else if (b.name == "Button B") abc[1] = (RectTransform)b.transform;
                else if (b.name == "Button C") abc[2] = (RectTransform)b.transform;
            }
            if (!abc[0] || !abc[1] || !abc[2]) return;
            _abc = abc;
            _abcHome = new[] { abc[0].anchoredPosition, abc[1].anchoredPosition, abc[2].anchoredPosition };
            _coin = abc[2].parent ? abc[2].parent.Find("Coin") as RectTransform : null;
            if (_coin) { _coinHome = _coin.anchoredPosition; _coinScale = _coin.localScale; }
            var bLabel = abc[1].Find("Label");
            _bLetter = bLabel ? bLabel.GetComponent<TMP_Text>() : null;
            var stick = FindAnyObjectByType<ArcadeDPad>(FindObjectsInactive.Include);
            _stickZone = stick ? (RectTransform)stick.transform : null;
            if (_stickZone) _stickZoneHome = _stickZone.sizeDelta;
        }

        // Over each of its buttons (found once: the controls are built into the scene).
        void Build()
        {
            var buttons = FindObjectsByType<ArcadeButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _labels = new TMP_Text[Names.Length];
            for (int i = 0; i < Names.Length; i++)
                foreach (var b in buttons)
                {
                    if (b.button != Names[i].button || !b.name.StartsWith("Button")) continue;
                    var own = b.GetComponentInChildren<TMP_Text>(true);
                    var rect = (RectTransform)b.transform;
                    var label = new GameObject("TC HINT", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                    var lr = (RectTransform)label.transform;
                    lr.SetParent(rect, false);
                    lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 1f);
                    lr.pivot = new Vector2(0.5f, 0f);
                    lr.sizeDelta = new Vector2(rect.rect.width * 1.6f, rect.rect.height * 0.4f);
                    lr.anchoredPosition = new Vector2(0f, rect.rect.height * 0.04f);
                    if (own) { label.font = own.font; label.fontSharedMaterial = own.fontSharedMaterial; }
                    label.text = Names[i].text;
                    label.alignment = TextAlignmentOptions.Bottom;
                    label.fontStyle = FontStyles.Bold;
                    label.enableAutoSizing = false;
                    label.fontSize = rect.rect.height * 0.2f;
                    var tint = b.visual ? b.visual.color : Color.white;
                    label.color = Color.Lerp(Color.white, tint, 0.35f);
                    label.outlineWidth = 0.25f;
                    label.outlineColor = new Color32(10, 10, 16, 255);
                    label.raycastTarget = false;
                    _labels[i] = label;
                    break;
                }
        }
    }
}
