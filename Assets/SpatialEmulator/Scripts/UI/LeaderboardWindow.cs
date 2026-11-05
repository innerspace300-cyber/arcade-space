// LeaderboardWindow.cs — the running demo's global top 50 (Games.GameCenter:
// ENDLESS KNIGHT's or THE TIME CRYSTAL's), opened by the SCORES key on the
// cabinet's right panel while a demo runs
// (AddKey). On the screen, so it reads the same in AR and in full screen; the
// game holds while it's open. A scrolling list - rank, name, score - with the
// player's own row lit, and their place below the 50 if they're further
// down. Styled like the game picker (its message box, keys, text and
// colours, on a canvas scaled the same way). Made at run time.

using SpatialEmulator.Games;
using SpatialEmulator.Mobile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class LeaderboardWindow : MonoBehaviour
    {
        static LeaderboardWindow s_instance;

        GamePicker _picker;
        GameObject _dialog;
        TMP_Text _status, _title;
        string _board;
        GameObject _tabs, _levels;
        TMP_Text[] _modeLabels, _levelLabels;
        bool _knight;
        RectTransform _view;
        RectTransform _list;
        ScrollRect _scroll;

        public static bool IsOpen => s_instance && s_instance._dialog && s_instance._dialog.activeSelf;

        public static void Open()
        {
            if (!s_instance)
            {
                var go = new GameObject("Leaderboard Window");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<LeaderboardWindow>();
            }
            s_instance.Show();
        }

        /// The SCORES key: a line of its own on the save panel's Content,
        /// shown while the demo runs (copied from its CONTROLS key).
        public static void AddKey(Transform saveContent)
        {
            var controls = saveContent ? saveContent.Find("Controls Line") : null;
            if (!controls || saveContent.Find("Scores Line")) return;
            var line = Instantiate(controls.gameObject, saveContent, false);
            line.name = "Scores Line";
            var restart = saveContent.Find("Restart Line");
            line.transform.SetSiblingIndex((restart ? restart : controls).GetSiblingIndex() + 1);
            var key = line.GetComponentInChildren<Button>(true);
            key.name = "Scores Button";
            var label = key.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = "SCORES";
            key.onClick = new Button.ButtonClickedEvent();
            key.onClick.AddListener(() => { Haptics.Play(Haptics.Kind.Light); Open(); });
            saveContent.parent.gameObject.AddComponent<ShowWithDemo>().line = line;
        }

        class ShowWithDemo : MonoBehaviour
        {
            public GameObject line;
            void LateUpdate()
            {
                bool demo = Demo.PitDemoGame.IsRunning || Demo.TimeCrystalGame.IsRunning;
                if (line && line.activeSelf != demo) line.SetActive(demo);
            }
        }

        void Show()
        {
            if (!_dialog) Build();
            if (!_dialog) return;
            _dialog.SetActive(true);
            MobileRetroDepthLayerStack.Paused = true;
            // This run counts too.
            string board = GameCenter.CurrentBoard;
            GameCenter.Submit(GameCenter.CurrentScore, board);
            // ENDLESS KNIGHT: HI-SCORE / TIMES (its level time boards, LV1-LV4 under TIMES).
            _knight = board == GameCenter.LeaderboardId;
            Load(board);
        }

        void Load(string board)
        {
            _board = board;
            bool time = GameCenter.IsTimeBoard(board);
            int level = 0;
            for (int i = 1; i <= GameCenter.KnightLevels; i++) if (board == GameCenter.LevelTimeBoard(i)) level = i;
            if (_title) _title.text = (board == GameCenter.TimeCrystalLeaderboardId ? "THE TIME CRYSTAL" : "ENDLESS KNIGHT")
                + (time ? "\nLEVEL " + level + " BEST TIMES" : "\nTOP 50");
            // The toggle and, under TIMES, the levels - the chosen ones lit.
            if (_tabs) _tabs.SetActive(_knight);
            if (_levels) _levels.SetActive(_knight && time);
            Stretch(_view, 10, 36, 10, !_knight ? 38 : time ? 70 : 54);
            if (_modeLabels != null)
            {
                if (_modeLabels[0]) _modeLabels[0].color = time ? _picker.textColor : _picker.warningColor;
                if (_modeLabels[1]) _modeLabels[1].color = time ? _picker.warningColor : _picker.textColor;
            }
            for (int i = 0; _levelLabels != null && i < _levelLabels.Length; i++)
                if (_levelLabels[i]) _levelLabels[i].color = i + 1 == level ? _picker.warningColor : _picker.textColor;
            Clear();
            _status.text = "LOADING...";
            _status.gameObject.SetActive(true);
            GameCenter.LoadTop(gameObject, nameof(OnLoaded), board);
        }



        void Close()
        {
            _dialog.SetActive(false);
            MobileRetroDepthLayerStack.Paused = GamePicker.IsOpen || SettingsScreen.IsOpen || MenuPause.Held;   // (opened from a menu that's still open: still held)
        }

        // From SEGameCenter.mm (UnitySendMessage).
        void OnLoaded(string reply)
        {
            if (!_dialog) return;
            var entries = GameCenter.Parse(reply, out string error);
            Clear();
            if (entries == null || entries.Length == 0)
            {
                _status.text = error ?? "No scores yet - be the first";
                return;
            }
            _status.gameObject.SetActive(false);
            int previous = 0;
            foreach (var entry in entries)
            {
                if (previous > 0 && entry.rank > previous + 1) Row("...", "", "", false);
                Row(entry.rank + ".", entry.name, GameCenter.IsTimeBoard(_board) ? GameCenter.FormatTime(entry.score) : entry.score.ToString("N0"), entry.me);
                previous = entry.rank;
            }
            _scroll.verticalNormalizedPosition = 1f;
        }

        void Clear()
        {
            for (int i = _list.childCount - 1; i >= 0; i--) Destroy(_list.GetChild(i).gameObject);
        }

        void Row(string rank, string name, string score, bool me)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(_list, false);
            row.GetComponent<LayoutElement>().preferredHeight = 11;
            Color color = me ? _picker.warningColor : _picker.textColor;
            Text(row.transform, rank, 0f, 0.14f, TextAlignmentOptions.Left, color);
            Text(row.transform, name, 0.14f, 0.62f, TextAlignmentOptions.Left, color);
            Text(row.transform, score, 0.62f, 1f, TextAlignmentOptions.Right, color);
        }

        TMP_Text Text(Transform parent, string text, float from, float to, TextAlignmentOptions align, Color color)
        {
            var label = Instantiate(_picker.toastLabel, parent, false);
            label.gameObject.SetActive(true);
            label.text = text;
            label.color = color;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(from, 0f);
            rect.anchorMax = new Vector2(to, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return label;
        }

        void Build()
        {
            _picker = FindAnyObjectByType<GamePicker>(FindObjectsInactive.Include);
            if (!_picker || !_picker.toast || !_picker.addRomButton) return;

            _dialog = new GameObject("Dialog", typeof(RectTransform));
            _dialog.transform.SetParent(transform, false);
            var canvas = _dialog.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 35;   // over the controls, under the prompts
            var scaler = _dialog.AddComponent<CanvasScaler>();
            var from = _picker.GetComponentInParent<Canvas>().rootCanvas.GetComponent<CanvasScaler>();
            if (from)
            {
                scaler.uiScaleMode = from.uiScaleMode;
                scaler.scaleFactor = from.scaleFactor;
                scaler.referenceResolution = from.referenceResolution;
                scaler.screenMatchMode = from.screenMatchMode;
                scaler.matchWidthOrHeight = from.matchWidthOrHeight;
                scaler.referencePixelsPerUnit = from.referencePixelsPerUnit;
            }
            _dialog.AddComponent<GraphicRaycaster>();

            var shade = new GameObject("Shade", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            shade.transform.SetParent(_dialog.transform, false);
            shade.color = new Color(0f, 0f, 0f, 0.6f);
            Stretch(shade.rectTransform, 0, 0, 0, 0);

            var toastImage = _picker.toast.GetComponent<Image>();
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            box.transform.SetParent(_dialog.transform, false);
            if (toastImage) { box.sprite = toastImage.sprite; box.type = toastImage.type; box.color = toastImage.color; }
            else box.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
            box.rectTransform.sizeDelta = new Vector2(176, 250);

            var title = Text(box.transform, "ENDLESS KNIGHT\nTOP 50", 0f, 1f, TextAlignmentOptions.Center, _picker.textColor);
            _title = title;
            title.textWrappingMode = TextWrappingModes.Normal;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0, 26);
            titleRect.anchoredPosition = new Vector2(0, -8);

            // The list, scrolling between the title and the CLOSE key.
            // ENDLESS KNIGHT's toggle under the title: HI-SCORE (its scores) or TIMES - and under TIMES a row
            // of its levels, each one's best times (level 1's first).
            _tabs = Row(box.transform, "Mode", -36f, new[] { "HI-SCORE", "TIMES" }, out _modeLabels,
                i => Load(i == 0 ? GameCenter.LeaderboardId : GameCenter.LevelTimeBoard(1)));
            var levelNames = new string[GameCenter.KnightLevels];
            for (int i = 0; i < levelNames.Length; i++) levelNames[i] = "LV" + (i + 1);
            _levels = Row(box.transform, "Levels", -52f, levelNames, out _levelLabels, i => Load(GameCenter.LevelTimeBoard(i + 1)));

            var view = _view = new GameObject("View", typeof(RectTransform), typeof(RectMask2D), typeof(Image)).GetComponent<RectTransform>();
            view.SetParent(box.transform, false);
            view.GetComponent<Image>().color = Color.clear;   // (to be dragged)
            Stretch(view, 10, 36, 10, 38);
            _list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            _list.SetParent(view, false);
            _list.anchorMin = new Vector2(0f, 1f);
            _list.anchorMax = new Vector2(1f, 1f);
            _list.pivot = new Vector2(0.5f, 1f);
            _list.sizeDelta = Vector2.zero;
            var layout = _list.GetComponent<VerticalLayoutGroup>();
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            _list.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll = box.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = view;
            _scroll.content = _list;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;

            _status = Text(box.transform, "", 0f, 1f, TextAlignmentOptions.Center, _picker.textColor);
            _status.textWrappingMode = TextWrappingModes.Normal;
            Stretch(_status.rectTransform, 14, 40, 14, 40);

            var close = (RectTransform)Instantiate(_picker.addRomButton.gameObject, box.transform, false).transform;
            close.name = "Close";
            close.gameObject.SetActive(true);
            close.anchorMin = close.anchorMax = new Vector2(0.5f, 0f);
            close.pivot = new Vector2(0.5f, 0f);
            close.sizeDelta = new Vector2(70, 20);
            close.anchoredPosition = new Vector2(0, 9);
            var closeLabel = close.GetComponentInChildren<TMP_Text>(true);
            if (closeLabel) closeLabel.text = "CLOSE";
            var button = close.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => { Haptics.Play(Haptics.Kind.Light); Close(); });
            _dialog.SetActive(false);
        }

        // A row of keys (the picker's own), `y` under the box's top, each calling `pick` with its index.
        GameObject Row(Transform parent, string name, float y, string[] labels, out TMP_Text[] texts, System.Action<int> pick)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rect = (RectTransform)row.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-20f, 14f);
            rect.anchoredPosition = new Vector2(0f, y);
            texts = new TMP_Text[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var key = (RectTransform)Instantiate(_picker.addRomButton.gameObject, row.transform, false).transform;
                key.name = labels[i];
                key.gameObject.SetActive(true);
                key.anchorMin = new Vector2(i / (float)labels.Length, 0f);
                key.anchorMax = new Vector2((i + 1) / (float)labels.Length, 1f);
                key.pivot = new Vector2(0.5f, 0.5f);
                key.offsetMin = new Vector2(1f, 0f);
                key.offsetMax = new Vector2(-1f, 0f);
                var label = key.GetComponentInChildren<TMP_Text>(true);
                if (label) label.text = labels[i];
                texts[i] = label;
                int which = i;
                var button = key.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => { Haptics.Play(Haptics.Kind.Light); pick(which); });
            }
            return row;
        }

        static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
