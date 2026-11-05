// SpellWheel.cs — the built-in demo's spell choice on the C button. Holding
// C for HoldSeconds fans four round gothic keys out round it, each with its
// spell's icon (Resources/Spells: a lightning bolt for Holy Slash, a holy
// cross for Great Heal, a sword and a shield for the buffs), and the one the
// finger (or a controller's left stick) points at from C lights; letting go
// there makes it C's spell, which a tap of C then casts (PitDemoGame). A tag
// over C names the spell (the one pointed at, while choosing), and the
// choice is kept (PlayerPrefs).
// With a controller the on-screen controls are hidden, C with them: then the
// keys fan out in an arc over the knight's head instead (on a canvas of their
// own), the one the stick or d-pad points at lit and named under them.
//
// Built at run time on the controls' C button, so the hand-placed controls
// aren't rebuilt; it's only shown while the demo plays. The demo has no use for COIN or START (Great Heal is
// one of C's spells), so they're hidden while it plays.

using SpatialEmulator.Controls;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpatialEmulator.Demo
{
    public class SpellWheel : MonoBehaviour
    {
        public enum Spell { HolySlash, GreatHeal, SwordBuff, ShieldBuff }

        static readonly string[] Names = { "SLASH", "HEAL", "SWORD", "SHIELD" };
        static readonly string[] Icons = { "Spells/SpellSlash", "Spells/SpellHeal", "Spells/SpellSword", "Spells/SpellShield" };
        // Round C, above and to its left (degrees, 0 = right, anticlockwise).
        // (Clear of the B button down to C's left.)
        static readonly float[] Angles = { 75f, 105f, 135f, 165f };

        public const float HoldSeconds = 0.5f;
        const string PrefsKey = "EndlessKnight.Spell";

        public static SpellWheel Instance { get; private set; }
        public static Spell Current
        {
            get => (Spell)Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, 0), 0, Names.Length - 1);
            private set { PlayerPrefs.SetInt(PrefsKey, (int)value); PlayerPrefs.Save(); }
        }

        ArcadeButton _button;
        ArcadeTouchRouter _router;
        RectTransform _wheel, _tag, _aTag, _bTag, _hint;
        Image[] _keys;
        TMP_Text _tagText, _aTagText;
        Sprite _keySprite, _keyPressed;
        float _radius, _tagPopAt = -10f;
        int _highlight = -1;
        ArcadeButton[] _demoHides;
        int _hiddenState = -1;

        public bool IsOpen => _wheel && _wheel.gameObject.activeSelf;

        // Round her (a controller, the controls hidden): the keys close about her - left, over each shoulder, right.
        static readonly float[] OverAngles = { 180f, 125f, 55f, 0f };
        float[] _angles = Angles;
        Canvas _over;
        TMP_Text _overName;
        Transform _home;
        Vector2 _homeMin, _homeMax, _homePos;
        Vector3 _homeScale;
        bool _overHead;

        /// On the controls' C button (once; it stays with the scene's controls).
        public static SpellWheel Find()
        {
            if (Instance) return Instance;
            // (Not in the Editor out of play - a test render: its pieces would be left in the scene, and saved.)
            if (!Application.isPlaying) return null;
            foreach (var b in FindObjectsByType<ArcadeButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.button == RetroPadButton.Y)
                {
                    var wheel = b.GetComponent<SpellWheel>();
                    if (!wheel) wheel = b.gameObject.AddComponent<SpellWheel>();
                    wheel.Build(b);
                    return wheel;
                }
            return null;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build(ArcadeButton button)
        {
            if (_wheel) return;
            Instance = this;
            _button = button;
            _router = FindAnyObjectByType<ArcadeTouchRouter>(FindObjectsInactive.Include);

            // The START keycap's look and font.
            ArcadeButton start = null;
            foreach (var b in FindObjectsByType<ArcadeButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.button == RetroPadButton.Start) start = b;
            var startLabel = start ? start.GetComponentInChildren<TMP_Text>(true) : null;
            _keySprite = Resources.Load<Sprite>("Spells/SpellKey");
            var hides = new System.Collections.Generic.List<ArcadeButton>();
            foreach (var b in FindObjectsByType<ArcadeButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.button == RetroPadButton.Start || b.button == RetroPadButton.Select) hides.Add(b);
            _demoHides = hides.ToArray();
            _keyPressed = Resources.Load<Sprite>("Spells/SpellKeyPressed");
            var buttonRect = (RectTransform)button.transform;
            // Round keys 36 art pixels across, at 4 canvas units a pixel.
            Vector2 keySize = new Vector2(144f, 144f);
            _radius = 300f;

            _wheel = Group("Spell Wheel", buttonRect);
            _keys = new Image[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                var key = new GameObject(Names[i], typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                key.SetParent(_wheel, false);
                key.sizeDelta = keySize;
                float a = Angles[i] * Mathf.Deg2Rad;
                key.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _radius;
                var image = key.GetComponent<Image>();
                image.sprite = _keySprite;
                image.raycastTarget = false;
                _keys[i] = image;
                // Its icon, 20 art pixels in the key's 36.
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                icon.SetParent(key, false);
                icon.sizeDelta = keySize * (20f / 36f);
                var iconImage = icon.GetComponent<Image>();
                iconImage.sprite = Resources.Load<Sprite>(Icons[i]);
                iconImage.raycastTarget = false;
            }
            _wheel.gameObject.SetActive(false);

            // The spell's name over C.
            _tag = Group("Spell Tag", buttonRect);
            // (Nine art pixels - 4 canvas units each - lower than it was, and one right: between the HUD and C.)
            _tag.anchoredPosition += TagOffset(buttonRect);
            // (A's, B's and C's tags all one size)
            float tagSize = (startLabel ? startLabel.fontSize * 0.8f : 20f) * TagScale;
            _tagText = Label(_tag, startLabel, "", tagSize);
            _tagText.color = new Color(1f, 0.92f, 0.55f, 1f);   // gold
            _tagText.rectTransform.sizeDelta = new Vector2(buttonRect.rect.width * 1.6f, 40f);

            // And over A, placed just the same: LIGHT ATK, or while a prompt gives it an answer, that (NO, for RESTART).
            ArcadeButton aButton = null;
            foreach (var b in FindObjectsByType<ArcadeButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.button == RetroPadButton.B) aButton = b;
            if (aButton)
            {
                var aRect = (RectTransform)aButton.transform;
                _aTag = Group("Answer Tag", aRect);
                _aTag.anchoredPosition += TagOffset(aRect);
                _aTagText = Label(_aTag, startLabel, "", tagSize);
                _aTagText.color = _tagText.color;
                _aTagText.rectTransform.sizeDelta = new Vector2(aRect.rect.width * 1.6f, 40f);
                _aTag.gameObject.SetActive(false);
            }
            // Level 1's hint under C: a pixel arrow pointing up at it, HOLD FOR / MAGIC SPELLS under that.
            _hint = Group("Magic Hint", buttonRect);
            var arrow = new GameObject("Arrow", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            arrow.SetParent(_hint, false);
            float artPixel = buttonRect.rect.width / 36f;   // (the button's own art pixels: 36 across)
            arrow.sizeDelta = new Vector2(9f, 10f) * artPixel;
            arrow.anchoredPosition = new Vector2(0f, -HintArrowDrop);
            var arrowImage = arrow.GetComponent<Image>();
            arrowImage.sprite = UpArrow();
            arrowImage.color = _tagText.color;
            arrowImage.raycastTarget = false;
            for (int i = 0; i < 2; i++)
            {
                var line = Label(_hint, startLabel, i == 0 ? "HOLD FOR" : "MAGIC SPELLS", tagSize);
                line.color = _tagText.color;
                line.rectTransform.sizeDelta = new Vector2(buttonRect.rect.width * 2.4f, 40f);
                line.rectTransform.anchoredPosition = new Vector2(0f, -HintArrowDrop - 5f * artPixel - 12f - tagSize * 0.5f - i * tagSize * 1.15f);
            }
            _hint.gameObject.SetActive(false);

            // And over B: JUMP.
            foreach (var b in FindObjectsByType<ArcadeButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.button == RetroPadButton.A)
                {
                    var bRect = (RectTransform)b.transform;
                    _bTag = Group("Jump Tag", bRect);
                    _bTag.anchoredPosition += TagOffset(bRect);
                    var bText = Label(_bTag, startLabel, "JUMP", tagSize);
                    bText.color = _tagText.color;
                    bText.rectTransform.sizeDelta = new Vector2(bRect.rect.width * 1.6f, 40f);
                    _bTag.gameObject.SetActive(false);
                }
        }

        // Level 1's hint's arrow, its middle this far under C's middle (under the C printed under it).
        const float HintArrowDrop = 205f;   // (the C's glyph: its middle 129 under, 65 tall)

        // A gold pixel arrow pointing up, 9 by 10 (white: tinted).
        static Sprite UpArrow()
        {
            var tex = new Texture2D(9, 10, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[9 * 10];
            for (int y = 0; y < 10; y++)
                for (int x = 0; x < 9; x++)
                {
                    int row = 9 - y;   // (from the top)
                    bool head = row < 5 && Mathf.Abs(x - 4) <= row;
                    bool shaft = row >= 5 && Mathf.Abs(x - 4) <= 1;
                    px[y * 9 + x] = head || shaft ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 9, 10), new Vector2(0.5f, 0.5f), 100f);
        }

        // The buttons' tags a little smaller than they were (A's LIGHT ATK fits).
        const float TagScale = 0.85f;

        // A hint's place from its button's centre: just over it, between the HUD and it.
        static Vector2 TagOffset(RectTransform button) => new Vector2(4f, button.rect.height * 0.62f - 36f);

        // A container laid over the button, on the controls' canvas (so it's
        // not tinted or squashed with the button as it's pressed).
        RectTransform Group(string name, RectTransform buttonRect)
        {
            // (One of each: any left from before goes - Editor launches had saved 14 into the scene.)
            for (int i = buttonRect.parent.childCount - 1; i >= 0; i--)
            {
                var old = buttonRect.parent.GetChild(i);
                if (old.name != name) continue;
                if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
            }
            var group = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            if (!Application.isPlaying) group.gameObject.hideFlags = HideFlags.DontSave;   // (never saved into the scene)
            group.SetParent(buttonRect.parent, false);
            group.anchorMin = buttonRect.anchorMin;
            group.anchorMax = buttonRect.anchorMax;
            group.pivot = new Vector2(0.5f, 0.5f);
            group.sizeDelta = Vector2.zero;
            // The button's centre.
            group.anchoredPosition = buttonRect.anchoredPosition + Vector2.Scale(new Vector2(0.5f, 0.5f) - buttonRect.pivot, buttonRect.rect.size);
            group.SetAsLastSibling();
            return group;
        }

        static TMP_Text Label(RectTransform parent, TMP_Text like, string text, float size)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            var rect = label.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = parent.sizeDelta == Vector2.zero ? new Vector2(200f, 40f) : parent.sizeDelta;
            if (like) { label.font = like.font; label.color = like.color; }
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        public void Open()
        {
            if (!_wheel) return;
            var controls = _button ? _button.GetComponentInParent<Canvas>() : null;
            OverHead(controls && !controls.rootCanvas.enabled);
            _wheel.gameObject.SetActive(true);
            _wheel.SetAsLastSibling();
            _highlight = -1;
            Paint();
        }

        // Onto its own canvas over the knight (or back by C).
        void OverHead(bool on)
        {
            if (on == _overHead) return;
            _overHead = on;
            if (on)
            {
                if (!_over)
                {
                    var go = new GameObject("Spell Wheel Over Knight");
                    _over = go.AddComponent<Canvas>();
                    _over.renderMode = RenderMode.ScreenSpaceOverlay;
                    _over.sortingOrder = 30;
                    _overName = Label((RectTransform)go.transform, _tagText, "", 20f);
                    _overName.color = _tagText ? _tagText.color : Color.yellow;
                    _overName.rectTransform.anchorMin = _overName.rectTransform.anchorMax = Vector2.zero;
                }
                _over.gameObject.SetActive(true);
                UI.TvOutput.Adopt(_over);   // (on the TV at its size from the first frame - not a tiny one first)
                _home = _wheel.parent;
                _homeMin = _wheel.anchorMin; _homeMax = _wheel.anchorMax; _homePos = _wheel.anchoredPosition; _homeScale = _wheel.localScale;
                _wheel.SetParent(_over.transform, false);
                _wheel.anchorMin = _wheel.anchorMax = Vector2.zero;
                _angles = OverAngles;
            }
            else
            {
                _wheel.SetParent(_home, false);
                _wheel.anchorMin = _homeMin; _wheel.anchorMax = _homeMax; _wheel.anchoredPosition = _homePos; _wheel.localScale = _homeScale;
                _angles = Angles;
                if (_over) _over.gameObject.SetActive(false);
            }
            for (int i = 0; i < _keys.Length; i++)
            {
                float a = _angles[i] * Mathf.Deg2Rad;
                _keys[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _radius;
            }
        }

        // Round her: in full screen about her middle in the picture, the keys just clear of her; in AR, the middle of the screen.
        void PlaceOverHead()
        {
            var screen = UI.TvOutput.ScreenOf(_over);   // (on the TV with the picture, there: TvOutput)
            float key = screen.y * (UI.TvOutput.OnTv(_over) ? 0.06f : 0.038f);   // (a key's size on screen - bigger on a landscape TV)
            float scale = key / 144f;
            _wheel.localScale = Vector3.one * scale;
            Vector2 at = new Vector2(screen.x * 0.5f, screen.y * 0.5f);
            float reach = screen.y * 0.08f;   // (from her middle to the keys' middles)
            var knight = EndlessKnightDirector.Player;
            var cam = PitDemoGame.ActorsCamera;
            var body = knight ? knight.transform.Find("ARIANA")?.GetComponent<SpriteRenderer>() ?? knight.GetComponentInChildren<SpriteRenderer>() : null;
            if (UI.FullScreenTest.Enabled && cam && body)
            {
                var b = body.bounds;
                var pic = UI.FullScreenTest.PictureRect;
                if (pic.width > 1f && UI.TvOutput.Showing == UI.TvOutput.OnTv(_over))   // (the picture's on the screen the wheel is on)
                {
                    var low = cam.WorldToViewportPoint(new Vector3(b.center.x, b.min.y, b.center.z));
                    var high = cam.WorldToViewportPoint(new Vector3(b.center.x, b.max.y, b.center.z));
                    float tall = (high.y - low.y) * pic.height;   // (her sprite's frame on screen)
                    at = new Vector2(pic.xMin + low.x * pic.width, pic.yMin + (low.y + (high.y - low.y) * 0.75f) * pic.height);   // (round her shoulders and head)
                    reach = Mathf.Max(tall * 0.4f, key * 1.1f);
                }
            }
            _wheel.anchoredPosition = at;
            for (int i = 0; i < _keys.Length; i++)
            {
                float a = _angles[i] * Mathf.Deg2Rad;
                _keys[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (reach / scale);
            }
            if (_overName)
            {
                _overName.text = _highlight >= 0 ? Names[_highlight] : "";
                _overName.fontSize = screen.y * (UI.TvOutput.OnTv(_over) ? 0.035f : 0.022f);
                _overName.rectTransform.sizeDelta = new Vector2(key * 6f, key);
                _overName.rectTransform.anchoredPosition = at + Vector2.up * (reach + key * 1.1f);
            }
        }

        /// Follows the finger on C (or the controller's stick) while open.
        public void Track()
        {
            if (!IsOpen) return;
            if (_overHead) PlaceOverHead();
            Vector2 direction = default;
            bool pointing = false;
            if (_router && _router.TryGetFinger(_button, out var screen))
            {
                var canvas = _router.Canvas;
                var cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                var parent = (RectTransform)_wheel.parent;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, cam, out var local))
                {
                    // From C's centre, in the wheel's own space.
                    direction = local - (Vector2)parent.InverseTransformPoint(_wheel.position);
                    pointing = direction.magnitude > _radius * 0.4f;
                }
            }
            else if (Gamepad.current != null)
            {
                var pad = Gamepad.current;
                direction = pad.leftStick.ReadValue();
                // (or the d-pad: its diagonals too)
                var dpad = new Vector2((pad.dpad.right.isPressed ? 1f : 0f) - (pad.dpad.left.isPressed ? 1f : 0f), (pad.dpad.up.isPressed ? 1f : 0f) - (pad.dpad.down.isPressed ? 1f : 0f));
                if (dpad != Vector2.zero) direction = dpad.normalized;
                pointing = direction.magnitude > 0.5f;
            }
            int highlight = -1;
            if (pointing)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, best = 40f;
                for (int i = 0; i < _angles.Length; i++)
                {
                    float off = Mathf.Abs(Mathf.DeltaAngle(angle, _angles[i]));
                    if (off < best) { best = off; highlight = i; }
                }
            }
            if (highlight != _highlight) { _highlight = highlight; Paint(); }
        }

        /// Letting go: the spell pointed at becomes C's (true if one was).
        public bool Choose()
        {
            if (!IsOpen) return false;
            _wheel.gameObject.SetActive(false);
            OverHead(false);
            if (_highlight < 0) return false;
            Current = (Spell)_highlight;
            _tagPopAt = Time.unscaledTime;
            return true;
        }

        public void Close() { if (_wheel) { _wheel.gameObject.SetActive(false); OverHead(false); } }

        void Paint()
        {
            for (int i = 0; i < _keys.Length; i++)
            {
                bool lit = i == _highlight;
                _keys[i].sprite = lit && _keyPressed ? _keyPressed : _keySprite;
                _keys[i].rectTransform.localScale = Vector3.one * (lit ? 1.15f : 1f);
            }
        }

        void Update()
        {
            bool playing = PitDemoGame.IsRunning && _button && _button.isActiveAndEnabled;
            if (!playing && IsOpen) Close();
            // (THE TIME CRYSTAL has no START either, and shows COIN only at CONTINUE?.)
            bool hideStart = PitDemoGame.IsRunning || TimeCrystalGame.IsRunning;
            bool hideCoin = PitDemoGame.IsRunning || (TimeCrystalGame.IsRunning && !TimeCrystalGame.WantsCoin);
            int state = (hideStart ? 1 : 0) | (hideCoin ? 2 : 0);
            if (_demoHides != null && _hiddenState != state)
            {
                _hiddenState = state;
                foreach (var b in _demoHides)
                    if (b) b.gameObject.SetActive(!(b.button == RetroPadButton.Select ? hideCoin : hideStart));
            }
            if (_tag)
            {
                if (_tag.gameObject.activeSelf != playing) _tag.gameObject.SetActive(playing);
                if (_bTag && _bTag.gameObject.activeSelf != playing) _bTag.gameObject.SetActive(playing);
                bool hint = playing && PitDemoGame.Level == 1;
                if (_hint && _hint.gameObject.activeSelf != hint) _hint.gameObject.SetActive(hint);
                if (_aTag)
                {
                    // (what A does - or what it answers: NO)
                    string a = playing ? DemoDialogue.AHint ?? "LIGHT ATK" : null;
                    if (_aTag.gameObject.activeSelf != (a != null)) _aTag.gameObject.SetActive(a != null);
                    if (a != null) _aTagText.text = a;
                }
                if (playing)
                {
                    // (C rests her while REST is offered over her head, or she's resting: it says so)
                    var knight = EndlessKnightDirector.Player;
                    bool rest = !IsOpen && (RestPrompt.Showing || (knight && knight.IsResting));
                    // (a prompt up - RESTART's warning, the witch's lines, the portal's offer: what C answers in it)
                    string answer = DemoDialogue.CHint;
                    _tagText.text = answer ?? (rest ? "REST" : Names[IsOpen && _highlight >= 0 ? _highlight : (int)Current]);
                    float t = (Time.unscaledTime - _tagPopAt) / 0.3f;
                    _tag.localScale = Vector3.one * (t >= 0f && t < 1f ? Mathf.Lerp(1.5f, 1f, t) : 1f);
                }
            }
        }
    }
}
