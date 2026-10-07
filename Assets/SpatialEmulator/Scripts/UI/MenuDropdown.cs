// MenuDropdown.cs — the screen's menu: three bars top left (no keycap),
// across from the Settings gear (and faded like it), that pause the game and drop down the MENU panel's
// keys - GAMES, FRAME, FULL, CAB, CAP, LOCK; in full screen GAMES, AR,
// CAP and, for a game that saves, SAVE and SAVES (SavesWindow) - on the screen, so they're in
// reach in any mode, full screen too (where FULL reads AR: back to AR, as the
// old AR key did). It takes the place of the screen's own GAMES and AR keys
// (GamePicker, FullScreenTest). The keys are copies of GAMES's keycap; a key
// that's on (FRAME or CAB, LOCKED) shows its pressed cap. A tap outside the
// keys puts the list away. Made at run time.

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
        Image _frame, _cab, _lock;
        TMP_Text _fullLabel, _lockLabel;
        Button _lockButton;
        readonly System.Collections.Generic.List<RectTransform> _rows = new System.Collections.Generic.List<RectTransform>();
        GameObject[] _arOnly;   // FRAME, CAB, LOCK: nothing to do in full screen
        GameObject[] _saveKeys;   // SAVE, SAVES: in full screen, for a game that saves (not the demo)

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
            // Just the three bars: no keycap behind them (still the key's
            // size to tap).
            var keycap = menu.GetComponent<Image>();
            if (keycap) keycap.color = Color.clear;
            menu.GetComponent<Button>().transition = Selectable.Transition.None;
            menu.gameObject.AddComponent<CanvasGroup>().alpha = 0.33f;
            _menuKey = menu.gameObject;

            // A tap anywhere else puts the list away.
            _blocker = new GameObject("Menu Blocker", typeof(RectTransform), typeof(Image), typeof(Button));
            _blocker.layer = games.gameObject.layer;
            var blockRect = (RectTransform)_blocker.transform;
            blockRect.SetParent(root, false);
            blockRect.anchorMin = Vector2.zero;
            blockRect.anchorMax = Vector2.one;
            blockRect.offsetMin = blockRect.offsetMax = Vector2.zero;
            _blocker.GetComponent<Image>().color = Color.clear;
            _blocker.GetComponent<Button>().onClick.AddListener(Close);

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
            _cab = Row(Key(list, "CAB", new Vector2(KeyWidth, height), () => CrtEffect.On = !CrtEffect.On)).GetComponent<Image>();
            // CAP: ScreenCap's key, on a copy of the keycap.
            var cap = Key(list, "CAP", new Vector2(KeyWidth, height), null);
            ScreenCap.MakeButton(cap.gameObject, _up, _down);
            cap.GetComponent<Button>().onClick.AddListener(Close);
            Row(cap);
            // Save states, in full screen (in AR they're on the cabinet's panel).
            var save = Row(Key(list, "SAVE", new Vector2(KeyWidth, height), () => { Close(); SavesWindow.SaveNow(); }));
            var saves = Row(Key(list, "SAVES", new Vector2(KeyWidth, height), () => { Close(); SavesWindow.Open(); }));
            _saveKeys = new[] { save.gameObject, saves.gameObject };
            var lockKey = Row(Key(list, "LOCK", new Vector2(KeyWidth, height), ToggleLock));
            _lock = lockKey.GetComponent<Image>();
            _lockLabel = lockKey.GetComponentInChildren<TMP_Text>(true);
            _lockButton = lockKey.GetComponent<Button>();
            _arOnly = new[] { _frame.gameObject, _cab.gameObject, lockKey.gameObject };

            _blocker.transform.SetAsLastSibling();
            _list.transform.SetAsLastSibling();
            Layout();
            Close();
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
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
                shadow.effectDistance = new Vector2(1f, -1f);
                var rect = bar.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(18f, 3f);
                rect.anchoredPosition = new Vector2(0f, i * 6f);
            }
        }

        bool IsOpen => _list && _list.activeSelf;

        void Toggle()
        {
            if (IsOpen) Close();
            else
            {
                _list.SetActive(true);
                _blocker.SetActive(true);
                MobileRetroDepthLayerStack.Paused = true;   // (the game - or the demo - holds while it's open)
                Show();
            }
        }

        void Close()
        {
            if (_list && _list.activeSelf)
                MobileRetroDepthLayerStack.Paused = GamePicker.IsOpen || SettingsScreen.IsOpen || LeaderboardWindow.IsOpen || SavesWindow.IsOpen;
            if (_list) _list.SetActive(false);
            if (_blocker) _blocker.SetActive(false);
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

        void ToggleLock()
        {
            if (_manipulator) _manipulator.locked = !_manipulator.locked;
            Show();
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
            if (_arOnly != null && (_arOnly[0].activeSelf == full || _saveKeys[0].activeSelf != saving))
            {
                foreach (var key in _arOnly) key.SetActive(!full);
                foreach (var key in _saveKeys) key.SetActive(saving);
                Layout();
            }
            if (!_manipulator) _manipulator = FindAnyObjectByType<CabinetManipulator>(FindObjectsInactive.Include);
            if (_frame) _frame.sprite = CrtEffect.On ? _up : _down;
            if (_cab) _cab.sprite = CrtEffect.On ? _down : _up;
            if (_fullLabel) _fullLabel.text = FullScreenTest.Enabled ? "AR" : "FULL";
            bool locked = _manipulator && _manipulator.locked;
            if (_lock) _lock.sprite = locked ? _down : _up;
            if (_lockLabel) _lockLabel.text = locked ? "LOCKED" : "LOCK";
            // (Nothing to lock with no cabinet out.)
            if (_lockButton) _lockButton.interactable = HasCabinet;
            if (_lockLabel) _lockLabel.alpha = HasCabinet ? 1f : 0.4f;
        }

        void LateUpdate()
        {
            if (!_menuKey) return;
            bool covered = GamePicker.IsOpen || SettingsScreen.IsOpen || LeaderboardWindow.IsOpen || SavesWindow.IsOpen;
            if (_menuKey.activeSelf == covered) _menuKey.SetActive(!covered);
            if (covered) { if (IsOpen) Close(); return; }
            if (IsOpen) Show();
        }
    }
}
