// DemoDialogue.cs — ENDLESS KNIGHT's dialogue boxes:
//  - the opening (first run only, PlayerPrefs): the witch welcomes the knight
//    to the endless loop, the knight answers, then HOW TO PLAY shows the
//    joystick and the A, B and C buttons (mini copies of the controls' own
//    art) with what each does;
//  - HOW TO PLAY on its own (the side panel's CONTROLS button);
//  - the RESTART warning: the witch asks before a restart wipes the hi score
//    and progress (C - green - yes, A - red - no);
//  - the PORTAL offer, once a run has every fruit: the witch's congratulations,
//    then open the portal to the next loop, or keep going (YES / NO).
// The opening and HOW TO PLAY leave the game running (the knight just
// doesn't take the controls); the RESTART warning freezes it. A press of A, B or C (or a
// controller's buttons) moves it on - finishing the line being typed first -
// so taps on the screen stay free for moving and sizing the cabinet.
//
// Pixel UI's scanline speech box (Resources/Dialogue), drawn into the
// HUD's layer (the front one), over the HUD: one canvas unit is one pixel of
// that layer's picture, the box's art at two (the game's own scale), the
// portraits (Portraits 2: Eleonore for the witch, Joanna for the knight) at one.
// The opening's lines are in a short box along the top, with just their
// faces, so the knight and the witch stay in view below it. The boxes'
// lines, arrows and PRESS C take the cabinet style's colours (Settings >
// COLORS; CabinetStyles) - the art's lines are white, tinted.
// Built at run time.

using SpatialEmulator.Controls;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpatialEmulator.Demo
{
    public class DemoDialogue : MonoBehaviour
    {
        public const string SeenKey = "EndlessKnight.IntroSeen";

        // Restart and Portal are the witch asking, in the large box, YES or NO.
        enum Speaker { Witch, Knight, HowToPlay, Restart, Portal }

        struct Slide
        {
            public Speaker speaker;
            public string text;
            public Slide(Speaker speaker, string text) { this.speaker = speaker; this.text = text; }
        }

        static readonly Slide[] Intro =
        {
            new Slide(Speaker.Witch, "WELCOME TO THE ENDLESS LOOP.\nTRY TO ESCAPE IF YOU CAN."),
            new Slide(Speaker.Knight, "AN ENDLESS LOOP? THEN I'LL CUT MY WAY OUT."),
            new Slide(Speaker.HowToPlay, null),
        };
        static readonly Slide[] HowToOnly = { new Slide(Speaker.HowToPlay, null) };
        static readonly Slide[] RestartPrompt =
        {
            new Slide(Speaker.Restart, "START OVER? YOUR <nobr>HI SCORE</nobr> AND ALL YOUR PROGRESS WILL BE WIPED."),
        };

        // Every fruit collected: the witch offers the portal to the next loop (LevelPortal).
        static readonly Slide[] PortalPrompt =
        {
            new Slide(Speaker.Witch, "CONGRATS! YOU COLLECTED EVERY FRUIT.\nNOW I CAN OPEN A PORTAL TO THE NEXT LOOP!"),
            new Slide(Speaker.Portal, "OPEN THE PORTAL? OR STAY IN THIS LOOP AND KEEP GOING FOR A <nobr>HI SCORE?</nobr>"),
        };

        // The rows of the HOW TO PLAY box: the control, its name (gold), what it does.
        static readonly (string name, string does)[] HowTo =
        {
            ("MOVE", "PUSH ALL THE WAY TO SPRINT"),
            ("A  LIGHT ATTACK", "SWING YOUR SWORD"),
            ("B  JUMP", "AGAIN IN MID-AIR: DOUBLE JUMP"),
            ("C  MAGIC", "TAP TO CAST. HOLD FOR SPELLS"),
        };

        static readonly Color Gold = new Color32(255, 214, 64, 255);
        static readonly Color Cyan = new Color32(0, 220, 255, 255);   // the box's line
        const float CharsPerSecond = 45f;
        const float PressGuard = 0.25f;   // a slide ignores presses this long after it appears

        public static DemoDialogue Instance { get; private set; }
        /// Up (the game is frozen and its controls go nowhere).
        public static bool Showing => Instance && Instance._slide >= 0;
        /// One that freezes the game is up (the RESTART warning).
        public static bool Freezing => Showing && Instance._slides == RestartPrompt;
        /// The RESTART warning is up.
        public static bool AskingRestart => Showing && Instance._slides == RestartPrompt;

        // A box for spoken lines: its portrait, the line, the arrow and PRESS C.
        class TalkBox
        {
            public RectTransform root, pointer;
            public Image portrait;
            public TMP_Text text, hint;
            public int pointerHome;
        }

        RectTransform _root, _howTo;
        // Tinted the style's line colour, and its accent.
        readonly System.Collections.Generic.List<Graphic> _lines = new System.Collections.Generic.List<Graphic>();
        readonly System.Collections.Generic.List<Graphic> _accents = new System.Collections.Generic.List<Graphic>();
        TalkBox _short, _large, _talk;   // _talk: the one showing
        RectTransform _howToPointer, _choices;
        TMP_Text _howToHint;
        int _howToPointerHome;
        Sprite _witch, _knight, _witchFace, _knightFace;
        Sprite _red, _yellow, _green, _knob, _arrowUp, _arrowRight, _letterA, _letterC;
        TMP_FontAsset _font;
        TMP_Text _title;
        RectTransform[] _rules;
        Slide[] _slides;
        bool _markSeen;
        System.Action _onYes, _onNo;
        int _slide = -1;
        float _shownAt;
        bool _a, _b, _c;   // the buttons last frame

        /// The opening, unless it's been seen (a finished run of it).
        public static void ShowIntro()
        {
            if (PlayerPrefs.GetInt(SeenKey, 0) == 1) return;
            Open(Intro, true, null);
        }

        /// HOW TO PLAY, any time (not over another box).
        public static void ShowHowToPlay()
        {
            if (Showing) return;
            Open(HowToOnly, false, null);
        }

        /// The RESTART warning; `onYes` runs if it's accepted. Asked again
        /// while it's up (RESTART tapped twice) is a yes.
        public static void AskRestart(System.Action onYes)
        {
            if (AskingRestart) { Instance.Answer(true); return; }
            if (Instance) Destroy(Instance.gameObject);   // the opening gives way (it's not marked seen)
            Open(RestartPrompt, false, onYes);
        }

        /// The portal offer (every fruit collected): `onYes` opens it, `onNo` keeps the run going.
        public static void AskPortal(System.Action onYes, System.Action onNo)
        {
            if (Instance) Destroy(Instance.gameObject);
            Open(PortalPrompt, false, onYes, onNo);
        }

        static void Open(Slide[] slides, bool markSeen, System.Action onYes, System.Action onNo = null)
        {
            var game = PitDemoGame.Running;
            var cam = PitDemoGame.HudCamera;
            if (!game || !cam) return;
            var hud = game.GetComponentInChildren<HudJuice>(true);
            var go = new GameObject("Dialogue", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer(PitDemoGame.HudLayer);
            go.transform.SetParent(game.transform, false);
            var dialogue = go.AddComponent<DemoDialogue>();
            dialogue._slides = slides;
            dialogue._markSeen = markSeen;
            dialogue._onYes = onYes;
            dialogue._onNo = onNo;
            dialogue.Build(cam, hud && hud.healthText ? hud.healthText.font : null);
            dialogue.Show(0);
        }

        void Build(Camera cam, TMP_FontAsset font)
        {
            if (Instance && Instance != this) Destroy(Instance.gameObject);
            Instance = this;
            _font = font;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.5f;
            canvas.pixelPerfect = true;
            // Over everything: the last sorting layer (Default, after the
            // HUD's TopLayer; the witch and the spells draw there too).
            var layers = SortingLayer.layers;
            canvas.sortingLayerID = layers[layers.Length - 1].id;
            canvas.sortingOrder = 1000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            _root = (RectTransform)transform;

            _witch = Resources.Load<Sprite>("Dialogue/PortraitWitch");
            _knight = Resources.Load<Sprite>("Dialogue/PortraitKnight");
            _witchFace = Resources.Load<Sprite>("Dialogue/PortraitWitchFace");
            _knightFace = Resources.Load<Sprite>("Dialogue/PortraitKnightFace");
            FindControlArt();

            _short = BuildTalk("Talk", 228, 84, 64);
            _large = BuildTalk("Talk Large", 132, 144, 104);
            BuildChoices();
            BuildHowTo();
            Tint();
            UI.CabinetStyles.Changed += Tint;
        }

        // The controls' own art, for the mini buttons and joystick.
        void FindControlArt()
        {
            foreach (var b in FindObjectsByType<ArcadeButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var image = b.visual as Image;
                if (!image) continue;
                if (b.button == RetroPadButton.B) _red = image.sprite;
                if (b.button == RetroPadButton.A) _yellow = image.sprite;
                if (b.button == RetroPadButton.Y) _green = image.sprite;
                // Their pixel letters, under them on the controls.
                var glyph = b.transform.Find("Glyph")?.GetComponent<Image>();
                if (glyph && b.button == RetroPadButton.B) _letterA = glyph.sprite;
                if (glyph && b.button == RetroPadButton.Y) _letterC = glyph.sprite;
            }
            var pad = FindAnyObjectByType<ArcadeDPad>(FindObjectsInactive.Include);
            if (pad)
                foreach (var image in pad.GetComponentsInChildren<Image>(true))
                {
                    if (image.name == "Knob") _knob = image.sprite;
                    if (image.name == "Arrow Up") _arrowUp = image.sprite;
                    if (image.name == "Arrow Right") _arrowRight = image.sprite;
                }
        }

        // Witch or knight: a portrait (`face` pixels square) and their line,
        // across the picture at height `y`.
        TalkBox BuildTalk(string name, int y, int h, int face)
        {
            const int x = 13, w = 400;
            var talk = new TalkBox { root = SpeechBox(name, x, y, w, h) };
            // The portrait's frame (its line two pixels).
            int frameSize = face + 4, margin = (h - frameSize) / 2;
            var frame = Box("Portrait Frame", talk.root, margin + 2, margin, frameSize, frameSize);
            var frameImage = frame.gameObject.AddComponent<Image>();
            Sliced(frameImage, "Dialogue/DialogPortraitFrame");
            _lines.Add(frameImage);
            talk.portrait = Box("Portrait", frame, 2, 2, face, face).gameObject.AddComponent<Image>();
            int textX = margin + 2 + frameSize + 12;
            talk.text = Text("Line", talk.root, textX, 12, w - textX - 14, h - 12 - 10, TextAlignmentOptions.TopLeft);
            talk.pointerHome = 8;
            talk.pointer = Pointer(talk.root, w - 28, talk.pointerHome);
            talk.hint = Hint(talk.root, w - 28, talk.pointerHome - 2);
            _accents.Add(talk.pointer.GetComponent<Image>());
            _accents.Add(talk.hint);
            return talk;
        }

        // The RESTART warning's YES (C, green) and NO (A, red), under its line:
        // each mini button with its pixel letter under it, as on the controls
        // (the buttons may all be one colour), and the answer beside it in
        // HOW TO PLAY's gold.
        void BuildChoices()
        {
            _choices = Box("Choices", _large.root, 140, 26, 400 - 140 - 14, 48);
            Icon("Yes Button", _choices, _green, 18, 30);
            Icon("Yes Letter", _choices, _letterC, 18, 6);
            var yes = Text("Yes", _choices, 42, 22, 80, 16, TextAlignmentOptions.MidlineLeft);
            yes.text = "YES";
            yes.color = Gold;
            Icon("No Button", _choices, _red, 130, 30);
            Icon("No Letter", _choices, _letterA, 130, 6);
            var no = Text("No", _choices, 154, 22, 80, 16, TextAlignmentOptions.MidlineLeft);
            no.text = "NO";
            no.color = Gold;

            // The pair centred between the portrait's frame and the box's
            // right line (the large box: 144 high, a 104 px face in a frame
            // 4 bigger, as BuildTalk lays it out; the box's line 4 px in).
            const int boxW = 400, boxH = 144, face = 104, line = 4;
            int frameSize = face + 4, margin = (boxH - frameSize) / 2;
            float portraitRight = margin + 2 + frameSize;
            float middle = (portraitRight + boxW - line) * 0.5f;
            float left = 18f - (_green ? _green.rect.width * 0.5f : 12f);
            float right = 154f + no.GetPreferredValues("NO").x;
            var at = _choices.anchoredPosition;
            _choices.anchoredPosition = new Vector2(middle - (left + right) * 0.5f + _choices.sizeDelta.x * 0.5f, at.y);
        }

        // HOW TO PLAY: the joystick and the three buttons, each with its name and what it does.
        void BuildHowTo()
        {
            const int x = 13, y = 82, w = 400, h = 230;
            _howTo = SpeechBox("How To Play", x, y, w, h);
            var title = Text("Title", _howTo, 0, h - 30, w, 18, TextAlignmentOptions.Center);
            title.text = "HOW TO PLAY";
            title.color = Gold;
            // Cyan rules either side of it (fitted to it when it's shown).
            _title = title;
            _rules = new[] { Fill("Rule Left", _howTo, 16, h - 22, 0, 2, Cyan), Fill("Rule Right", _howTo, 16, h - 22, 0, 2, Cyan) };
            foreach (var rule in _rules) _lines.Add(rule.GetComponent<Image>());

            const int rowHeight = 42, firstTop = h - 38, iconX = 46;
            for (int i = 0; i < HowTo.Length; i++)
            {
                int top = firstTop - i * rowHeight, mid = top - rowHeight / 2;
                if (i == 0) Joystick(_howTo, iconX, mid);
                else Icon("Button", _howTo, i == 1 ? _red : i == 2 ? _yellow : _green, iconX, mid);
                var line = Text("Row " + i, _howTo, 88, top - rowHeight, w - 88 - 12, rowHeight, TextAlignmentOptions.MidlineLeft);
                line.lineSpacing = 4f;
                line.text = $"<color=#{ColorUtility.ToHtmlStringRGB(Gold)}>{HowTo[i].name}</color>\n{HowTo[i].does}";
            }
            _howToPointerHome = 10;
            _howToPointer = Pointer(_howTo, w - 28, _howToPointerHome);
            _howToHint = Hint(_howTo, w - 28, 8);
            _accents.Add(_howToPointer.GetComponent<Image>());
            _accents.Add(_howToHint);
        }

        // A mini joystick: the knob with its four arrows round it.
        void Joystick(RectTransform parent, int cx, int cy)
        {
            if (_knob) Icon("Knob", parent, _knob, cx, cy);
            if (_arrowUp)
            {
                int w = (int)_arrowUp.rect.width, h = (int)_arrowUp.rect.height;
                Picture(parent, "Arrow Up", _arrowUp, cx - w / 2, cy + 15, w, h);
                Picture(parent, "Arrow Down", _arrowUp, cx - w / 2, cy - 15 - h, w, h).localScale = new Vector3(1f, -1f, 1f);
            }
            if (_arrowRight)
            {
                int w = (int)_arrowRight.rect.width, h = (int)_arrowRight.rect.height;
                Picture(parent, "Arrow Right", _arrowRight, cx + 15, cy - h / 2, w, h);
                Picture(parent, "Arrow Left", _arrowRight, cx - 15 - w, cy - h / 2, w, h).localScale = new Vector3(-1f, 1f, 1f);
            }
        }

        // A sprite at its own size (one of its pixels to one of the picture's), centred on (cx, cy).
        static void Icon(string name, RectTransform parent, Sprite sprite, int cx, int cy)
        {
            if (!sprite) return;
            int w = (int)sprite.rect.width, h = (int)sprite.rect.height;
            Picture(parent, name, sprite, cx - w / 2, cy - h / 2, w, h);
        }

        static RectTransform Picture(RectTransform parent, string name, Sprite sprite, int x, int y, int w, int h)
        {
            var rect = Box(name, parent, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return rect;
        }

        // The scanline box: the dark lines tiled inside, the black-and-cyan frame round them.
        RectTransform SpeechBox(string name, int x, int y, int w, int h)
        {
            var box = Box(name, _root, x, y, w, h);
            var lines = Box("Scanlines", box, 2, 2, w - 4, h - 4);
            var tiled = lines.gameObject.AddComponent<Image>();
            tiled.sprite = Resources.Load<Sprite>("Dialogue/DialogScanlines");
            tiled.type = Image.Type.Tiled;
            tiled.pixelsPerUnitMultiplier = 0.5f;
            tiled.raycastTarget = false;
            var frame = Box("Frame", box, 0, 0, w, h);
            var frameImage = frame.gameObject.AddComponent<Image>();
            Sliced(frameImage, "Dialogue/DialogFrame");
            _lines.Add(frameImage);
            box.gameObject.SetActive(false);
            return box;
        }

        static void Sliced(Image image, string sprite)
        {
            image.sprite = Resources.Load<Sprite>(sprite);
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = 0.5f;   // two picture pixels to the art's one
            image.raycastTarget = false;
        }

        static RectTransform Fill(string name, RectTransform parent, int x, int y, int w, int h, Color color)
        {
            var image = Box(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        // The rules from the box's sides to ten pixels short of the title.
        void FitRules()
        {
            _title.ForceMeshUpdate();
            float width = _title.textBounds.size.x;
            if (width <= 0f) return;
            float boxWidth = _howTo.sizeDelta.x;
            int inner = Mathf.FloorToInt((boxWidth - width) * 0.5f) - 10;
            for (int i = 0; i < 2; i++)
            {
                var rule = _rules[i];
                int x = i == 0 ? 16 : Mathf.RoundToInt(boxWidth) - inner;
                float y = rule.anchoredPosition.y - rule.sizeDelta.y * 0.5f;
                rule.sizeDelta = new Vector2(inner - 16, rule.sizeDelta.y);
                rule.anchoredPosition = new Vector2(x + (inner - 16) * 0.5f, y + rule.sizeDelta.y * 0.5f);
            }
        }

        // The bobbing "more" arrow, and PRESS C beside it.
        static RectTransform Pointer(RectTransform parent, int x, int y)
            => Picture(parent, "More", Resources.Load<Sprite>("Dialogue/DialogPointer"), x, y, 14, 10);

        TMP_Text Hint(RectTransform parent, int right, int y)
        {
            var hint = Text("Press A", parent, right - 8 - 120, y, 120, 16, TextAlignmentOptions.MidlineRight);
            hint.text = "PRESS C";
            hint.color = Cyan;
            return hint;
        }

        // Game Compact at two picture pixels to the font's one (8 to the em).
        TMP_Text Text(string name, RectTransform parent, int x, int y, int w, int h, TextAlignmentOptions align)
        {
            var label = Box(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            if (_font) label.font = _font;
            label.fontSize = 16f;
            label.color = Color.white;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.richText = true;
            label.raycastTarget = false;
            return label;
        }

        // A rectangle in picture pixels from its parent's bottom-left corner (whole pixels, so the art stays crisp).
        static RectTransform Box(string name, RectTransform parent, int x, int y, int w, int h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x + w * 0.5f, y + h * 0.5f);
            return rect;
        }

        void Show(int slide)
        {
            _slide = slide;
            _shownAt = Time.unscaledTime;
            var s = _slides[slide];
            bool talk = s.speaker != Speaker.HowToPlay;
            // The opening's lines in the short box; the warning in the large one.
            bool large = s.speaker == Speaker.Restart || s.speaker == Speaker.Portal;
            _talk = talk ? (large ? _large : _short) : null;
            _short.root.gameObject.SetActive(_talk == _short);
            _large.root.gameObject.SetActive(_talk == _large);
            _howTo.gameObject.SetActive(!talk);
            if (!talk) FitRules();
            if (talk)
            {
                bool knight = s.speaker == Speaker.Knight;
                _talk.portrait.sprite = large ? (knight ? _knight : _witch) : (knight ? _knightFace : _witchFace);
                _talk.portrait.enabled = _talk.portrait.sprite;
                _talk.text.text = s.text;
                _talk.text.maxVisibleCharacters = 0;
                _talk.text.ForceMeshUpdate();   // (parsed now, for Typing)
            }
            if (_slides == RestartPrompt) Time.timeScale = 0f;
            Prompts(!Typing);
        }

        /// Moves on: finishes the line being typed, or goes to the next slide (closing after the last).
        public void Advance()
        {
            if (_slide < 0) return;
            if (Typing) { FinishLine(); return; }
            if (Asking) return;   // that takes a YES or a NO
            if (_slide + 1 < _slides.Length) Show(_slide + 1);
            else Close();
        }

        /// The RESTART warning's answer.
        public void Answer(bool yes)
        {
            if (!Asking) return;
            var onYes = _onYes;
            var onNo = _onNo;
            if (yes && _slides == RestartPrompt)
            {
                // Starting over: the box stays up, still, and goes with this
                // run - the cabinet holds the run's last picture until the
                // new one has drawn, and that's this box, not the game behind it.
                _slide = -1;
                onYes?.Invoke();
                return;
            }
            Close();
            if (yes) onYes?.Invoke();
            else onNo?.Invoke();
        }

        void FinishLine()
        {
            if (_talk != null) _talk.text.maxVisibleCharacters = int.MaxValue;
            _shownAt = Time.unscaledTime - 100f;
        }

        void Close()
        {
            _slide = -1;
            if (_markSeen)
            {
                PlayerPrefs.SetInt(SeenKey, 1);
                PlayerPrefs.Save();
            }
            if (!PitDemoGame.IsPaused && _slides == RestartPrompt) Time.timeScale = 1f;
            Destroy(gameObject);
        }

        bool Asking => _slide >= 0 && (_slides[_slide].speaker == Speaker.Restart || _slides[_slide].speaker == Speaker.Portal);
        // (Counting what's shown, not the <nobr> tags round HI SCORE.)
        bool Typing => _talk != null && _talk.text.maxVisibleCharacters < _talk.text.GetParsedText().Length;

        void Update()
        {
            if (_slide < 0) return;
            // A, B and C, as pressed this frame.
            bool a = Held(RetroPadButton.B), b = Held(RetroPadButton.A), c = Held(RetroPadButton.Y);
            bool pressA = a && !_a, pressB = b && !_b, pressC = c && !_c;
            _a = a; _b = b; _c = c;
            var keys = Keyboard.current;
            if (keys != null)
            {
                pressC |= keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame;
                pressA |= keys.escapeKey.wasPressedThisFrame;
            }

            // ARcade's own menus over the game: they have the controls.
            if (PitDemoGame.IsPaused) { _shownAt = Mathf.Max(_shownAt, Time.unscaledTime - PressGuard); return; }
            if (_slides == RestartPrompt && Time.timeScale != 0f) Time.timeScale = 0f;

            float t = Time.unscaledTime - _shownAt;
            if (Typing) _talk.text.maxVisibleCharacters = Mathf.FloorToInt(t * CharsPerSecond);

            // The arrow and PRESS C once the slide is all there (YES / NO for the warning).
            bool talk = _talk != null;
            bool ready = !Typing;
            var pointer = Prompts(ready);
            var p = pointer.anchoredPosition;
            float home = (talk ? _talk.pointerHome : _howToPointerHome) + 5;
            pointer.anchoredPosition = new Vector2(p.x, home + (Mathf.Repeat(Time.unscaledTime * 2f, 1f) < 0.5f ? 0f : -2f));

            if (t < PressGuard) return;
            if (Asking && ready)
            {
                // YES on the green C, NO on the red A.
                if (pressC) Answer(true);
                else if (pressA) Answer(false);
                return;
            }
            if (pressA || pressB || pressC) Advance();
        }

        static bool Held(RetroPadButton button) => ArcadeInput.IsPressed((uint)button);

        // YES / NO, or the arrow and PRESS C, shown once the slide is all
        // there (and hidden as it starts - it's drawn before its first Update,
        // or with ARcade's menus over the game, before any).
        RectTransform Prompts(bool ready)
        {
            bool talk = _talk != null;
            _choices.gameObject.SetActive(Asking && ready);
            var pointer = talk ? _talk.pointer : _howToPointer;
            var hint = talk ? _talk.hint : _howToHint;
            pointer.gameObject.SetActive(ready && !Asking);
            hint.gameObject.SetActive(ready && !Asking);
            return pointer;
        }

        // The cabinet style's colours on the lines and accents.
        void Tint()
        {
            foreach (var line in _lines) if (line) line.color = UI.CabinetStyles.DialogLine;
            foreach (var accent in _accents) if (accent) accent.color = UI.CabinetStyles.DialogAccent;
        }

        void OnDestroy()
        {
            UI.CabinetStyles.Changed -= Tint;
            if (Instance == this) Instance = null;
        }
    }
}
