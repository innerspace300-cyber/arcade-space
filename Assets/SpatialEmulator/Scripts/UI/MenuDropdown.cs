// MenuDropdown.cs — the screen's menu: three bars top left (no keycap),
// across from the Settings gear (and faded like it), that pause the game and drop down the MENU panel's
// keys - GAMES, FRAME, FULL, CAB, CAP (LOCK is by the cabinet's own menu, GamesPanel); in full screen GAMES, AR,
// CAP and, for a game that saves, SAVES (SavesWindow) - on the screen, so they're in
// reach in any mode, full screen too (where FULL reads AR: back to AR, as the
// old AR key did). It takes the place of the screen's own GAMES and AR keys
// (GamePicker, FullScreenTest). The keys are copies of GAMES's keycap; a key
// that's on (FRAME or CAB, LOCKED) shows its pressed cap. Opposite it, top
// right, the side section: the cabinet's right-hand panel on the screen (the
// game's name, SAVE and its newest saves, CONTROLS, RESTART, SCORES). Only the three bars put
// them away (a tap elsewhere's caught, the game paused under it). Made at run time.

using SpatialEmulator.Games;
using SpatialEmulator.Mobile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class MenuDropdown : MonoBehaviour
    {
        const float KeyWidth = 40f;
        const float KeyGap = 3f;
        const float BoxPad = 5f;
        const float MenuSize = 28f;
        const float BarThickness = 1f, BarSpacing = 4f;   // the three bars: a pixel thick, this far apart
        // Its middle, in from the screen's top left corner.
        static readonly Vector2 MenuInset = new Vector2(28f, -4f);   // (tops level: the gear's 20 px)

        static MenuDropdown s_instance;

        /// Made (the screen's GAMES and AR keys stay hidden).
        public static bool Exists => s_instance;

        GamePicker _picker;
        SettingsScreen _settings;
        SingleCabinetGate _gate;
        CabinetManipulator _manipulator;
        GameObject _menuKey, _blocker, _list;
        Sprite _up, _down;
        Image _frame, _cab;
        TMP_Text _fullLabel;
        GameObject _fullKey;   // (AR / FULL: gone on a TV or in a Kishi / Backbone - TvOutput - full screen only there)
        readonly System.Collections.Generic.List<RectTransform> _rows = new System.Collections.Generic.List<RectTransform>();
        GameObject[] _arOnly;   // FRAME, CAB, LOCK: nothing to do in full screen
        GameObject[] _saveKeys;   // SAVE, SAVES: in full screen, for a game that saves (not the demo)
        TMP_Text _restartLabel;
        float _restartArmedUntil = -1f;
        const float RestartConfirmSeconds = 3f;

        void Restart()
        {
            // ENDLESS KNIGHT's witch does the asking.
            if (Demo.PitDemoGame.IsRunning) { Close(); SaveStatePanel.RestartGame(); return; }
            if (_restartArmedUntil < 0f || Time.unscaledTime > _restartArmedUntil)
            {
                _restartArmedUntil = Time.unscaledTime + RestartConfirmSeconds;
                if (_restartLabel) _restartLabel.text = "SURE?";
                return;
            }
            DisarmRestart();
            Close();
            SaveStatePanel.RestartGame();
        }

        void DisarmRestart()
        {
            _restartArmedUntil = -1f;
            if (_restartLabel) _restartLabel.text = "RESTART";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var picker = FindAnyObjectByType<GamePicker>(FindObjectsInactive.Include);
            if (!picker || !picker.openButton) return;
            s_instance = picker.gameObject.AddComponent<MenuDropdown>();
        }

        void Start()
        {
            _picker = GetComponent<GamePicker>();
            _settings = FindAnyObjectByType<SettingsScreen>(FindObjectsInactive.Include);
            var games = (RectTransform)_picker.openButton.transform;
            var root = games.parent;
            var gameImage = games.GetComponent<Image>();
            _up = gameImage ? gameImage.sprite : null;
            _down = games.GetComponent<Button>().spriteState.pressedSprite;

            // The three-bar key: top left, its top level with the gear's top right.
            float y = games.anchoredPosition.y;
            if (_settings && _settings.openButton) y = ((RectTransform)_settings.openButton.transform).anchoredPosition.y;
            y += MenuInset.y;
            var menu = Key(root, "", new Vector2(MenuSize, MenuSize), Toggle);
            menu.name = "Menu Button";
            menu.anchorMin = menu.anchorMax = new Vector2(0f, 1f);
            menu.pivot = new Vector2(0.5f, 0.5f);
            menu.anchoredPosition = new Vector2(MenuInset.x, y);
            Bars(menu.GetComponentInChildren<TMP_Text>(true));
            // The gear centred on the middle bar.
            if (_settings && _settings.openButton)
            {
                var gear = (RectTransform)_settings.openButton.transform;
                float barsMiddle = menu.anchoredPosition.y + (0.5f - menu.pivot.y) * menu.sizeDelta.y;
                float gearMiddleOffset = (0.5f - gear.pivot.y) * gear.rect.height * gear.localScale.y;
                gear.anchoredPosition = new Vector2(gear.anchoredPosition.x, barsMiddle - gearMiddleOffset);
            }
            // Just the three bars: no keycap behind them (still the key's
            // size to tap).
            var keycap = menu.GetComponent<Image>();
            if (keycap) keycap.color = Color.clear;
            menu.GetComponent<Button>().transition = Selectable.Transition.None;
            menu.gameObject.AddComponent<CanvasGroup>().alpha = 0.22f * 1.35f;
            // Its own canvas, over every game's and HUD's (the on-top layers, Time Crystal's
            // overlays, the controls) - under only the windows (35+) - so nothing hides it.
            var own = menu.gameObject.AddComponent<Canvas>();
            own.overrideSorting = true;
            own.sortingOrder = MenuOrder;
            menu.gameObject.AddComponent<GraphicRaycaster>();
            _menuKey = menu.gameObject;

            // Taps anywhere else are caught (the game's paused: its controls, under this, do nothing) -
            // only the three bars close the menu (MenuPause's 3, 2, 1 then).
            _blocker = new GameObject("Menu Blocker", typeof(RectTransform), typeof(Image), typeof(Button));
            _blocker.layer = games.gameObject.layer;
            var blockRect = (RectTransform)_blocker.transform;
            blockRect.SetParent(root, false);
            blockRect.anchorMin = Vector2.zero;
            blockRect.anchorMax = Vector2.one;
            blockRect.offsetMin = blockRect.offsetMax = Vector2.zero;
            _blocker.GetComponent<Image>().color = Color.clear;

            // The list, hanging under the key.
            _list = new GameObject("Menu List", typeof(RectTransform), typeof(Image));
            _list.layer = games.gameObject.layer;
            var list = (RectTransform)_list.transform;
            list.SetParent(root, false);
            var box = _list.GetComponent<Image>();
            var toast = _picker.toast ? _picker.toast.GetComponent<Image>() : null;
            if (toast) { box.sprite = toast.sprite; box.type = toast.type; box.color = toast.color; }
            else box.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
            const float height = 20f;
            list.anchorMin = list.anchorMax = list.pivot = new Vector2(0f, 1f);
            list.anchoredPosition = new Vector2(MenuInset.x - MenuSize * 0.5f, y - MenuSize * 0.5f - 4f);

            RectTransform Row(RectTransform key)
            {
                key.SetParent(list, false);
                key.anchorMin = key.anchorMax = key.pivot = new Vector2(0.5f, 1f);
                _rows.Add(key);
                return key;
            }
            Row(Key(list, "GAMES", new Vector2(KeyWidth, height), () => { Close(); _picker.Open(); }));
            _frame = Row(Key(list, "FRAME", new Vector2(KeyWidth, height), () => CrtEffect.On = false)).GetComponent<Image>();
            var full = Row(Key(list, "FULL", new Vector2(KeyWidth, height), Full));
            _fullLabel = full.GetComponentInChildren<TMP_Text>(true);
            _fullKey = full.gameObject;
            _cab = Row(Key(list, "CAB", new Vector2(KeyWidth, height), () => CrtEffect.On = !CrtEffect.On)).GetComponent<Image>();
            // CAP: ScreenCap's key, on a copy of the keycap.
            var cap = Key(list, "CAP", new Vector2(KeyWidth, height), null);
            ScreenCap.MakeButton(cap.gameObject, _up, _down);
            cap.GetComponent<Button>().onClick.AddListener(Close);
            Row(cap);
            // All the save states, in full screen (in AR they're on the cabinet's panel).
            // (SAVE and RESTART are in the side section, on the right.)
            var saves = Row(Key(list, "SAVES", new Vector2(KeyWidth, height), SavesWindow.Open));
            _saveKeys = new[] { saves.gameObject };
            // (LOCK isn't here: it's by the 3D cabinet's own menu, GamesPanel.)
            _arOnly = new[] { _frame.gameObject, _cab.gameObject };

            BuildSide(root, y);

            _blocker.transform.SetAsLastSibling();
            _list.transform.SetAsLastSibling();
            _side.transform.SetAsLastSibling();
            _menuKey.transform.SetAsLastSibling();   // (over the tap-catcher: the three bars close the menu)
            Layout();
            Close();
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
            Games.SaveStates.Changed -= OnSavesChanged;
            foreach (var t in _thumbs.Values) if (t) Destroy(t);
        }

        // ---- The side section: the cabinet's right-hand panel (SaveStatePanel) on the screen ----
        // Opposite the list, top right under the gear, while the menu's open:
        // the game's name, SAVE and its newest saves (tap one to load it),
        // CONTROLS (ENDLESS KNIGHT's HOW TO PLAY) and RESTART - in AR and in
        // full screen alike.

        const float SideWidth = KeyWidth;   // (as wide as the list)
        GameObject _side;
        readonly System.Collections.Generic.Dictionary<RectTransform, Vector2> _homes = new System.Collections.Generic.Dictionary<RectTransform, Vector2>();
        readonly System.Collections.Generic.List<RectTransform> _sideRows = new System.Collections.Generic.List<RectTransform>();
        TMP_Text _sideTitle, _sideCount, _sideEmpty;
        GameObject _sideSave, _sideControls, _sideRestart, _sideScores;
        RectTransform[] _sideSlots;
        RawImage[] _sideThumbs;
        TMP_Text[] _sideTimes;
        System.Collections.Generic.List<Games.SaveState> _sideSaves = new System.Collections.Generic.List<Games.SaveState>();
        readonly System.Collections.Generic.Dictionary<string, Texture2D> _thumbs = new System.Collections.Generic.Dictionary<string, Texture2D>();
        bool _sideStale = true;
        string _sideGame;
        bool _sideDemo;

        void BuildSide(Transform root, float y)
        {
            _side = new GameObject("Menu Side", typeof(RectTransform), typeof(Image));
            _side.layer = _list.layer;
            var side = (RectTransform)_side.transform;
            side.SetParent(root, false);
            var box = _side.GetComponent<Image>();
            var listBox = _list.GetComponent<Image>();
            box.sprite = listBox.sprite; box.type = listBox.type; box.color = listBox.color;
            side.anchorMin = side.anchorMax = side.pivot = new Vector2(1f, 1f);
            side.anchoredPosition = new Vector2(-(MenuInset.x - MenuSize * 0.5f), y - MenuSize * 0.5f - 4f);

            RectTransform SideRow(RectTransform row)
            {
                row.SetParent(side, false);
                row.anchorMin = row.anchorMax = row.pivot = new Vector2(0.5f, 1f);
                _sideRows.Add(row);
                return row;
            }
            const float height = 20f;
            _sideTitle = Label(side, 9f, CabinetStyles.Title);
            SideRow(_sideTitle.rectTransform);
            _sideSave = SideRow(Key(side, "SAVE", new Vector2(SideWidth, height), () => { Close(); SavesWindow.SaveNow(); })).gameObject;
            _sideCount = Label(side, 6f, Color.white);
            SideRow(_sideCount.rectTransform);
            _sideEmpty = Label(side, 6f, new Color(0.75f, 0.65f, 1f));
            _sideEmpty.text = "No saves yet";
            SideRow(_sideEmpty.rectTransform);
            var panel = FindAnyObjectByType<SaveStatePanel>(FindObjectsInactive.Include);
            int count = panel && panel.slots != null && panel.slots.Length > 0 ? panel.slots.Length : 3;
            _sideSlots = new RectTransform[count];
            _sideThumbs = new RawImage[count];
            _sideTimes = new TMP_Text[count];
            for (int i = 0; i < count; i++)
            {
                int index = i;
                float thumbHeight = (SideWidth - 8f) * 0.75f;
                var slot = Key(side, "", new Vector2(SideWidth, thumbHeight + 14f), () => LoadSide(index));
                slot.name = "Save Slot " + (i + 1);
                var thumb = new GameObject("Thumbnail", typeof(RectTransform), typeof(RawImage));
                thumb.layer = _list.layer;
                var tr = (RectTransform)thumb.transform;
                tr.SetParent(slot, false);
                tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 1f);
                tr.sizeDelta = new Vector2(SideWidth - 8f, thumbHeight);
                tr.anchoredPosition = new Vector2(0f, -4f);
                _sideThumbs[i] = thumb.GetComponent<RawImage>();
                _sideThumbs[i].raycastTarget = false;
                var time = slot.GetComponentInChildren<TMP_Text>(true);
                if (time)
                {
                    var r = time.rectTransform;
                    r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 0f);
                    r.pivot = new Vector2(0.5f, 0f);
                    r.sizeDelta = new Vector2(0f, 10f);
                    r.anchoredPosition = new Vector2(0f, 1f);
                    time.fontSize = Mathf.Min(time.fontSize, 6f);
                }
                _sideTimes[i] = time;
                _sideSlots[i] = SideRow(slot);
            }
            _sideControls = SideRow(Key(side, "CONTROLS", new Vector2(SideWidth, height), () => { Close(); Demo.DemoDialogue.ShowHowToPlay(); })).gameObject;
            // RESTART: tap, then SURE? (ENDLESS KNIGHT's witch asks instead).
            _sideRestart = SideRow(Key(side, "RESTART", new Vector2(SideWidth, height), Restart)).gameObject;
            _restartLabel = _sideRestart.GetComponentInChildren<TMP_Text>(true);
            // SCORES, under RESTART as on the cabinet's panel: the demo's Game Center hi-scores (LeaderboardWindow).
            _sideScores = SideRow(Key(side, "SCORES", new Vector2(SideWidth, height), LeaderboardWindow.Open)).gameObject;
            // Its keys' labels smaller if they'd run past the cap (CONTROLS).
            foreach (var row in _sideRows)
            {
                if (!row.GetComponent<Button>()) continue;
                var label = row.GetComponentInChildren<TMP_Text>(true);
                if (!label || label.text.Length == 0) continue;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                float fits = (SideWidth - 6f) / Mathf.Max(1f, label.GetPreferredValues(label.text).x);
                if (fits < 1f) label.fontSize *= fits;
            }
            Games.SaveStates.Changed += OnSavesChanged;
            _side.SetActive(false);
        }

        void OnSavesChanged(string game) => _sideStale = true;

        TMP_Text Label(Transform parent, float size, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.layer = _list.layer;
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            var keyText = _picker.openButton.GetComponentInChildren<TMP_Text>(true);
            if (keyText) { text.font = keyText.font; text.fontSharedMaterial = keyText.fontSharedMaterial; }
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.enableAutoSizing = true;   // (smaller to fit across)
            text.fontSizeMin = 3f;
            text.fontSizeMax = size;
            text.rectTransform.sizeDelta = new Vector2(SideWidth, size + 3f);
            return text;
        }

        void ShowSide()
        {
            if (!_side) return;
            bool running = LibretroCore.IsRunning || Demo.LayeredDemo.AnyRunning;
            if (_side.activeSelf != running) _side.SetActive(running);
            if (!running) return;
            bool demo = Demo.LayeredDemo.AnyRunning;
            if (!_sideStale && _sideGame == GameSelection.Current && _sideDemo == demo) return;
            _sideStale = false;
            _sideGame = GameSelection.Current;
            _sideDemo = demo;
            _sideSaves = demo || string.IsNullOrEmpty(_sideGame) ? new System.Collections.Generic.List<Games.SaveState>() : Games.SaveStates.List(_sideGame);
            string title = demo ? SaveStatePanel.DemoName : "SAVES";   // (as the panel's)
            _sideTitle.text = title.ToUpperInvariant();
            float lines = _sideTitle.text.Split('\n').Length;
            _sideTitle.rectTransform.sizeDelta = new Vector2(SideWidth, lines * (_sideTitle.fontSize + 2f) + 1f);
            // (At its size, or smaller to fit across.)
            _sideTitle.enableAutoSizing = true;
            _sideTitle.fontSizeMin = 4f;
            _sideTitle.fontSizeMax = 9f;
            _sideSave.SetActive(SavesWindow.CanSave);
            _sideCount.gameObject.SetActive(!demo);
            _sideCount.text = _sideSaves.Count == 1 ? "1 SAVE" : $"{_sideSaves.Count} SAVES";
            _sideEmpty.gameObject.SetActive(!demo && _sideSaves.Count == 0);
            for (int i = 0; i < _sideSlots.Length; i++)
            {
                bool used = i < _sideSaves.Count;
                _sideSlots[i].gameObject.SetActive(used);
                if (!used) continue;
                _sideThumbs[i].texture = Thumbnail(_sideSaves[i]);
                if (_sideTimes[i]) _sideTimes[i].text = SaveStatePanel.Describe(_sideSaves[i]);
            }
            _sideControls.SetActive(Demo.PitDemoGame.IsRunning);
            _sideScores.SetActive(Demo.PitDemoGame.IsRunning || Demo.TimeCrystalGame.IsRunning);
            // The rows shown, top down, and the box around them.
            float y = -BoxPad;
            foreach (var row in _sideRows)
            {
                if (!row.gameObject.activeSelf) continue;
                row.anchoredPosition = new Vector2(0f, y);
                y -= row.sizeDelta.y + KeyGap;
            }
            ((RectTransform)_side.transform).sizeDelta = new Vector2(SideWidth + 2 * BoxPad, -y - KeyGap + BoxPad);
        }

        void LoadSide(int index)
        {
            if (index >= _sideSaves.Count) return;
            var save = _sideSaves[index];
            Close();
            bool ok = Games.SaveStates.Load(save);
            _picker.ShowMessage(ok ? $"Loaded your save of {SaveStatePanel.Describe(save)}" : "Couldn't load that save", !ok);
        }

        Texture2D Thumbnail(Games.SaveState save)
        {
            if (save.thumbnailPath == null || !System.IO.File.Exists(save.thumbnailPath)) return null;
            if (_thumbs.TryGetValue(save.thumbnailPath, out var tex) && tex) return tex;
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(System.IO.File.ReadAllBytes(save.thumbnailPath))) { Destroy(tex); return null; }
            tex.filterMode = FilterMode.Point;
            _thumbs[save.thumbnailPath] = tex;
            return tex;
        }


        // A copy of GAMES's keycap: `label`, `size`, doing `tap`.
        RectTransform Key(Transform parent, string label, Vector2 size, UnityEngine.Events.UnityAction tap)
        {
            var key = (RectTransform)Instantiate(_picker.openButton, parent, false).transform;
            key.gameObject.SetActive(true);
            key.name = label.Length > 0 ? char.ToUpper(label[0]) + label.Substring(1).ToLower() + " Button" : "Key";
            key.sizeDelta = size;
            var text = key.GetComponentInChildren<TMP_Text>(true);
            if (text) text.text = label;
            var button = key.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            if (tap != null) button.onClick.AddListener(() => { Haptics.Play(Haptics.Kind.Light); tap(); });
            return key;
        }

        // The three bars, on the key's label (so they go down with the cap).
        static void Bars(TMP_Text label)
        {
            if (!label) return;
            for (int i = -1; i <= 1; i++)
            {
                var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                bar.gameObject.layer = label.gameObject.layer;
                bar.transform.SetParent(label.transform, false);
                bar.color = Color.white;
                bar.raycastTarget = false;
                var shadow = bar.gameObject.AddComponent<Shadow>();   // (seen against a bright room)
                shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
                shadow.effectDistance = new Vector2(0.5f, -0.5f);
                var rect = bar.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(14f, BarThickness);   // (minimal: a pixel's line each)
                rect.anchoredPosition = new Vector2(0f, i * BarSpacing);
            }
        }

        bool IsOpen => _list && _list.activeSelf;
        bool _setAside;   // open, under a window opened from it (SCORES, SAVES)

        /// Folds the full-screen menu away if it's open (its game let go: MenuPause).
        public static void CloseIfOpen() { if (s_instance && s_instance.IsOpen) s_instance.Close(); }

        /// Down (its keys showing).
        public static bool IsShowing => s_instance && s_instance.IsOpen;

        /// A controller's Options: opens it, or puts it away (as the three bars do).
        public static void ToggleFromController() { if (s_instance && s_instance._list) s_instance.Toggle(); }

        void Toggle()
        {
            // Over the game list on the screen: it's put away, the menu opens.
            if (GamePicker.IsOpen && !GamePicker.InAR && _picker) _picker.Close();
            if (IsOpen) Close();
            else
            {
                _list.SetActive(true);
                _blocker.SetActive(true);
                _sideStale = true;
                MenuPause.Open(MenuPause.Menu.Screen);   // (the game - or the demo - holds while it's open; 3, 2, 1 when it closes)
                Show();
            }
        }

        void Close()
        {
            if ((_list && _list.activeSelf) || _setAside) MenuPause.Close(MenuPause.Menu.Screen);
            _setAside = false;
            if (_list) _list.SetActive(false);
            if (_blocker) _blocker.SetActive(false);
            if (_side) _side.SetActive(false);
            if (_restartArmedUntil > 0f) DisarmRestart();   // (a SURE? doesn't outlast the menu)
        }

        // FULL: into full screen (a cabinet's put down to run the game if
        // there's none yet); in full screen, AR: back to AR - or, for a
        // full-screen-only game, a prompt to pick another.
        void Full()
        {
            Close();
            if (!FullScreenTest.Enabled) FullScreenTest.EnterWithCabinet();
            else if (GameCatalog.Find(GameSelection.Current) is { FullScreenOnly: true }) HeavyGamePrompt.NotInAR(_picker);
            else FullScreenTest.ReturnToAR();
        }

        bool HasCabinet
        {
            get
            {
                if (!_gate) _gate = FindAnyObjectByType<SingleCabinetGate>();
                return _gate && _gate.cabinet && !FullScreenTest.Enabled;
            }
        }

        // The shown keys, top down, and the box around them.
        void Layout()
        {
            float y = -BoxPad, width = 0f;
            foreach (var key in _rows)
            {
                if (!key.gameObject.activeSelf) continue;
                key.anchoredPosition = new Vector2(0f, y);
                y -= key.sizeDelta.y + KeyGap;
                width = Mathf.Max(width, key.sizeDelta.x);
            }
            ((RectTransform)_list.transform).sizeDelta = new Vector2(width + 2 * BoxPad, -y - KeyGap + BoxPad);
        }

        void Show()
        {
            bool full = FullScreenTest.Enabled;
            bool saving = full && SavesWindow.CanSave;
            bool toAr = !TvOutput.Showing;
            if (_arOnly != null && (_arOnly[0].activeSelf == full || _saveKeys[0].activeSelf != saving || (_fullKey && _fullKey.activeSelf != toAr)))
            {
                foreach (var key in _arOnly) key.SetActive(!full);
                foreach (var key in _saveKeys) key.SetActive(saving);
                if (_fullKey) _fullKey.SetActive(toAr);
                Layout();
            }
            ShowSide();
            if (_restartArmedUntil > 0f && Time.unscaledTime > _restartArmedUntil) DisarmRestart();
            if (!_manipulator) _manipulator = FindAnyObjectByType<CabinetManipulator>(FindObjectsInactive.Include);
            if (_frame) _frame.sprite = CrtEffect.On ? _up : _down;
            if (_cab) _cab.sprite = CrtEffect.On ? _down : _up;
            if (_fullLabel) _fullLabel.text = FullScreenTest.Enabled ? "AR" : "FULL";
        }

        // Handheld (a Kishi, a Backbone - TvOutput), the phone on its side: the three bars and their list in from the
        // left by the notch's inset, the gear and the side section in from the right by the right's (whichever end
        // the notch is at, either way up). Back where they were otherwise.
        void KeepClearOfNotch()
        {
            float left = 0f, right = 0f;
            var canvas = _menuKey.GetComponentInParent<Canvas>();
            float k = canvas ? canvas.rootCanvas.scaleFactor : 1f;
            if (TvOutput.Handheld && k > 0f)
            {
                var safe = Screen.safeArea;
                left = Mathf.Round(safe.xMin / k);
                right = Mathf.Round((Screen.width - safe.xMax) / k);
            }
            Shift((RectTransform)_menuKey.transform, left);
            if (_list) Shift((RectTransform)_list.transform, left);
            if (_settings && _settings.openButton) Shift((RectTransform)_settings.openButton.transform, -right);
            if (_side) Shift((RectTransform)_side.transform, -right);
        }

        void Shift(RectTransform key, float dx)
        {
            if (!_homes.TryGetValue(key, out var home)) _homes[key] = home = key.anchoredPosition;
            var at = home + new Vector2(dx, 0f);
            if (key.anchoredPosition != at) key.anchoredPosition = at;
        }

        const int MenuOrder = 34;   // (over the game picker (20), SpellWheel (30); under the windows (35))

        void LateUpdate()
        {
            if (!_menuKey) return;
            KeepClearOfNotch();
            // (The list on the AR cabinet doesn't cover the screen.)
            bool covered = (GamePicker.IsOpen && !GamePicker.InAR) || SettingsScreen.IsOpen || LeaderboardWindow.IsOpen || SavesWindow.IsOpen;
            // The three bars always there - a way out from anywhere.
            if (!_menuKey.activeSelf) _menuKey.SetActive(true);
            if (_menuKey.TryGetComponent(out Canvas own))
            {
                if (!own.enabled) own.enabled = true;
                if (!own.overrideSorting || own.sortingOrder != MenuOrder) { own.overrideSorting = true; own.sortingOrder = MenuOrder; }
                var home = own.transform.parent ? own.transform.parent.GetComponentInParent<Canvas>() : null;
                if (home && !home.rootCanvas.enabled) home.rootCanvas.enabled = true;
            }
            var t = _menuKey.transform;
            if (t.GetSiblingIndex() != t.parent.childCount - 1) t.SetAsLastSibling();   // (over the game list too)
            // A window opened from the menu (SCORES, SAVES) goes over it: the menu's set aside,
            // still open - the game held (MenuPause) - and back when the window's closed.
            if (covered)
            {
                if (IsOpen) { _list.SetActive(false); _blocker.SetActive(false); if (_side) _side.SetActive(false); _setAside = true; }
                return;
            }
            if (_setAside)
            {
                _setAside = false;
                _list.SetActive(true); _blocker.SetActive(true); _sideStale = true;
            }
            if (IsOpen) Show();
        }
    }
}
