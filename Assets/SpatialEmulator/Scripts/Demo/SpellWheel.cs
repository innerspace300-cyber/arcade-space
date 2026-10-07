// SpellWheel.cs — the built-in demo's spell choice on the C button. Holding
// C for HoldSeconds fans four round gothic keys out round it, each with its
// spell's icon (Resources/Spells: a lightning bolt for Holy Slash, a holy
// cross for Great Heal, a sword and a shield for the buffs), and the one the
// finger (or a controller's left stick) points at from C lights; letting go
// there makes it C's spell, which a tap of C then casts (PitDemoGame). A tag
// over C names the spell (the one pointed at, while choosing), and the
// choice is kept (PlayerPrefs).
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
        RectTransform _wheel, _tag;
        Image[] _keys;
        TMP_Text _tagText;
        Sprite _keySprite, _keyPressed;
        float _radius, _tagPopAt = -10f;
        int _highlight = -1;
        ArcadeButton[] _demoHides;
        bool _hidden;

        public bool IsOpen => _wheel && _wheel.gameObject.activeSelf;

        /// On the controls' C button (once; it stays with the scene's controls).
        public static SpellWheel Find()
        {
            if (Instance) return Instance;
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
            // (Two art pixels - 4 canvas units each - lower than it was.)
            _tag.anchoredPosition += new Vector2(0f, buttonRect.rect.height * 0.62f - 8f);
            _tagText = Label(_tag, startLabel, "", startLabel ? startLabel.fontSize * 0.8f : 20f);
            _tagText.color = new Color(1f, 0.92f, 0.55f, 1f);   // gold
            _tagText.rectTransform.sizeDelta = new Vector2(buttonRect.rect.width * 1.6f, 40f);
        }

        // A container laid over the button, on the controls' canvas (so it's
        // not tinted or squashed with the button as it's pressed).
        RectTransform Group(string name, RectTransform buttonRect)
        {
            var group = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
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
            _wheel.gameObject.SetActive(true);
            _wheel.SetAsLastSibling();
            _highlight = -1;
            Paint();
        }

        /// Follows the finger on C (or the controller's stick) while open.
        public void Track()
        {
            if (!IsOpen) return;
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
                direction = Gamepad.current.leftStick.ReadValue();
                pointing = direction.magnitude > 0.5f;
            }
            int highlight = -1;
            if (pointing)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, best = 40f;
                for (int i = 0; i < Angles.Length; i++)
                {
                    float off = Mathf.Abs(Mathf.DeltaAngle(angle, Angles[i]));
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
            if (_highlight < 0) return false;
            Current = (Spell)_highlight;
            _tagPopAt = Time.unscaledTime;
            return true;
        }

        public void Close() { if (_wheel) _wheel.gameObject.SetActive(false); }

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
            if (_demoHides != null && _hidden != PitDemoGame.IsRunning)
            {
                _hidden = PitDemoGame.IsRunning;
                foreach (var b in _demoHides) if (b) b.gameObject.SetActive(!_hidden);
            }
            if (_tag)
            {
                if (_tag.gameObject.activeSelf != playing) _tag.gameObject.SetActive(playing);
                if (playing)
                {
                    _tagText.text = Names[IsOpen && _highlight >= 0 ? _highlight : (int)Current];
                    float t = (Time.unscaledTime - _tagPopAt) / 0.3f;
                    _tag.localScale = Vector3.one * (t >= 0f && t < 1f ? Mathf.Lerp(1.5f, 1f, t) : 1f);
                }
            }
        }
    }
}
