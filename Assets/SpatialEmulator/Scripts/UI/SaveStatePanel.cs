// SaveStatePanel.cs — a pixel-art panel on the side of the cabinet, in AR:
// a SAVE button and the newest save states of the game that's playing
// (thumbnail + time). Tap a save to load it; hold one for its DELETE button.
// RESTART (tap it twice) restarts the game, like the board's reset switch.
// With Settings > Resume saves on, a game loads its newest save as it starts.
// The built-in demo (PitDemoGame) gets the same panel, but it doesn't save:
// titled DEMO, no SAVE button or saves; CONTROLS shows its HOW TO PLAY box,
// and RESTART - after the witch's warning (DemoDialogue) - wipes the hi
// score and progress and starts over from the opening dialogue.
//
// Lives in ARCabinet.prefab as a world-space canvas (built by
// GamePickerBuilder); it sits just right of the screen and follows the
// screen's size.

using System.Collections.Generic;
using SpatialEmulator.Games;
using SpatialEmulator.Mobile;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class SaveStatePanel : MonoBehaviour
    {
        [System.Serializable]
        public class Slot
        {
            public GameObject root;
            public Button button;
            public HoldGesture hold;
            public RawImage thumbnail;
            public TMP_Text time;
            public Button deleteButton;
        }

        public Canvas canvas;
        public MobileRetroDepthLayerStack stack;
        [Tooltip("The screen frame: the panel sits beside it, at its depth, and grows with it.")]
        public ScreenFrame frame;
        public GameObject content;
        public Button saveButton;
        public TMP_Text countLabel;
        public GameObject emptyLabel;
        public Slot[] slots;
        public Button restartButton;
        public TMP_Text restartLabel;
        [Tooltip("Seconds RESTART waits for the confirming second tap.")]
        public float restartConfirmSeconds = 3f;
        [Tooltip("Gap between the screen's right edge and the panel, in metres (cabinet scale 1).")]
        public float gap = 0.02f;
        [Tooltip("Frames a game runs before Resume saves loads its newest save.")]
        public int resumeAfterFrames = 30;
        [Tooltip("The panel's size against the prefab's (it and the MENU panel mirroring it): bigger, easier to tap in AR.")]
        public float sizeScale = 2f;

        readonly Dictionary<string, Texture2D> _thumbs = new Dictionary<string, Texture2D>();
        readonly List<RaycastResult> _hits = new List<RaycastResult>();
        List<SaveState> _saves = new List<SaveState>();
        string _game;
        bool _demo;
        TMP_Text _title;
        string _titleDefault;
        bool _resumePending;
        int _deleteSlot = -1;
        float _placedForHeight = -1f;
        float _restartArmedUntil = -1f;
        int _placedForFrame = -1;
        Vector3 _baseScale;
        GameObject _controlsLine, _demoRule;

        // What the demo keeps between runs: RESTART wipes it.
        static readonly string[] DemoProgressKeys =
            { "EndlessKnight.HiScore", "EndlessKnight.FruitComplete", "EndlessKnight.Spell", Demo.DemoDialogue.SeenKey, LevelPortal.OpenKey };

        void Awake()
        {
            _baseScale = transform.localScale * sizeScale;
            saveButton.onClick.AddListener(SaveNow);
            restartButton.onClick.AddListener(OnRestartTapped);
            BuildControlsButton();
            ApplyStyle();
            CabinetStyles.Changed += ApplyStyle;
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].button.onClick.AddListener(() => OnSlotTapped(index));
                slots[i].hold.onHold += () => ShowDelete(index);
                slots[i].deleteButton.onClick.AddListener(() => DeleteSlot(index));
                slots[i].deleteButton.gameObject.SetActive(false);
            }
        }

        void OnEnable()
        {
            MobileRetroDepthLayerStack.GameStarted += OnGameStarted;
            SaveStates.Changed += OnSavesChanged;
            GameSelection.Changed += Refresh;
            // The game may have started just before this panel subscribed.
            if (LibretroCore.IsRunning && MobileRetroDepthLayerStack.FramesSinceStart < resumeAfterFrames)
                _resumePending = AppSettings.ResumeLatestSave;
            Refresh();
        }

        void OnDisable()
        {
            MobileRetroDepthLayerStack.GameStarted -= OnGameStarted;
            SaveStates.Changed -= OnSavesChanged;
            GameSelection.Changed -= Refresh;
        }

        // Settings > COLORS' cabinet style: the panel's border and keycaps
        // (LOCK's too), its title and the DEMO line.
        void ApplyStyle()
        {
            CabinetStyles.Restyle(transform);
            if (_title == null && content.transform.Find("Title")) _title = content.transform.Find("Title").GetComponent<TMP_Text>();
            if (_title) _title.color = CabinetStyles.Title;
            if (_demoRule) _demoRule.GetComponent<Image>().color = CabinetStyles.Title;
        }

        void OnDestroy()
        {
            CabinetStyles.Changed -= ApplyStyle;
            foreach (var t in _thumbs.Values) if (t) Destroy(t);
        }

        void OnGameStarted(string game)
        {
            _resumePending = AppSettings.ResumeLatestSave;
            Refresh();
        }

        void OnSavesChanged(string game)
        {
            if (game == _game) Refresh();
        }

        void Update()
        {
            if (!canvas.worldCamera) canvas.worldCamera = Camera.main;
            bool running = LibretroCore.IsRunning || Demo.PitDemoGame.IsRunning;
            if (content.activeSelf != running) content.SetActive(running);
            if (running && (_game != GameSelection.Current || _demo != Demo.PitDemoGame.IsRunning)) Refresh();
            PlaceBesideScreen();
            if (_restartArmedUntil > 0f && Time.unscaledTime > _restartArmedUntil) DisarmRestart();
            if (_demo && _restartArmedUntil < 0f)
            {
                string label = Demo.DemoDialogue.AskingRestart ? "SURE?" : "RESTART";
                if (restartLabel.text != label) restartLabel.text = label;
            }

            if (_resumePending && LibretroCore.IsRunning && MobileRetroDepthLayerStack.FramesSinceStart >= resumeAfterFrames)
            {
                _resumePending = false;
                if (_saves.Count > 0 && SaveStates.Load(_saves[0]))
                    Message($"Resumed from your save of {Describe(_saves[0])}");
            }

            // Any press away from the DELETE button puts it away.
            if (_deleteSlot >= 0 && Pointer.current != null && Pointer.current.press.wasPressedThisFrame && EventSystem.current)
            {
                var data = new PointerEventData(EventSystem.current) { position = Pointer.current.position.ReadValue() };
                EventSystem.current.RaycastAll(data, _hits);
                var delete = slots[_deleteSlot].deleteButton.transform;
                bool onDelete = false;
                foreach (var hit in _hits)
                    if (hit.gameObject.transform.IsChildOf(delete)) { onDelete = true; break; }
                if (!onDelete) HideDelete();
            }
        }

        // Right of the screen frame, its top level with the frame's top, at
        // the frame's depth and growing with it (without a frame: beside the
        // screen, level with its middle).
        /// Where the panel's top left corner goes instead, in the cabinet's
        /// local metres (null: beside the frame) - CrtCabinet's 3D cabinet
        /// puts it by the front corner of its top.
        public Vector3? Corner
        {
            get => _corner;
            set { if (_corner != value) { _corner = value; _cornerVersion++; } }
        }
        Vector3? _corner;
        int _cornerVersion, _placedForCorner = -1;

        void PlaceBesideScreen()
        {
            if (!stack) return;
            if (frame)
            {
                if (_placedForFrame == frame.Version && _placedForCorner == _cornerVersion) return;
                _placedForFrame = frame.Version;
                _placedForCorner = _cornerVersion;
            }
            else
            {
                if (Mathf.Approximately(_placedForHeight, stack.screenHeight * stack.pixelAspect)) return;
                _placedForHeight = stack.screenHeight * stack.pixelAspect;
            }
            float grow = frame ? frame.Grow : 1f;
            float halfWidth = frame ? frame.OuterHalfWidth : stack.screenHeight * stack.PictureAspect * stack.pixelAspect * 0.5f;
            var rect = (RectTransform)transform;
            rect.localScale = _baseScale * grow;
            float panelHalf = rect.rect.width * rect.localScale.x * 0.5f;
            Vector3 center = stack.transform.localPosition;
            float z = frame ? frame.transform.localPosition.z : 0f;
            // The panel's content hangs from the top of its rect.
            float y = frame ? center.y + frame.OuterHalfHeight - rect.rect.height * rect.localScale.y * (1f - rect.pivot.y) : center.y;
            rect.localPosition = new Vector3(center.x + halfWidth + gap * grow + panelHalf, y, z);
            if (_corner is Vector3 corner)
                rect.localPosition = new Vector3(corner.x + panelHalf, corner.y - rect.rect.height * rect.localScale.y * (1f - rect.pivot.y), corner.z);
        }

        void Refresh()
        {
            HideDelete();
            _game = GameSelection.Current;
            _demo = Demo.PitDemoGame.IsRunning;
            _saves = _demo ? new List<SaveState>() : SaveStates.List(_game);
            saveButton.transform.parent.gameObject.SetActive(!_demo);   // its line
            if (_title == null && content.transform.Find("Title")) _title = content.transform.Find("Title").GetComponent<TMP_Text>();
            if (_titleDefault == null && _title) _titleDefault = _title.text;
            if (_title) _title.text = _demo ? "DEMO" : _titleDefault;
            countLabel.gameObject.SetActive(!_demo);
            if (_controlsLine) _controlsLine.SetActive(_demo);
            if (_demoRule) _demoRule.SetActive(_demo);
            countLabel.text = _saves.Count == 1 ? "1 SAVE" : $"{_saves.Count} SAVES";
            emptyLabel.SetActive(!_demo && _saves.Count == 0);
            for (int i = 0; i < slots.Length; i++)
            {
                bool used = i < _saves.Count;
                slots[i].root.SetActive(used);
                if (!used) continue;
                slots[i].time.text = Describe(_saves[i]);
                slots[i].thumbnail.texture = Thumbnail(_saves[i]);
            }
        }

        static string Describe(SaveState save)
        {
            var when = save.savedAt;
            var today = System.DateTime.Now.Date;
            if (when.Date == today) return when.ToString("h:mm tt");
            if (when.Date == today.AddDays(-1)) return "Yesterday";
            return when.ToString("MMM d");
        }

        Texture2D Thumbnail(SaveState save)
        {
            if (save.thumbnailPath == null) return null;
            if (_thumbs.TryGetValue(save.thumbnailPath, out var tex) && tex) return tex;
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            if (!tex.LoadImage(System.IO.File.ReadAllBytes(save.thumbnailPath))) { Destroy(tex); return null; }
            tex.filterMode = FilterMode.Point;
            _thumbs[save.thumbnailPath] = tex;
            return tex;
        }

        void SaveNow()
        {
            if (!LibretroCore.IsRunning || string.IsNullOrEmpty(_game)) return;
            var thumb = stack ? stack.CaptureScreen() : null;
            var save = SaveStates.Save(_game, thumb);
            if (thumb) Destroy(thumb);
            Message(save != null ? "Game saved" : "Couldn't save this game", save == null);
        }

        void OnRestartTapped()
        {
            if (!LibretroCore.IsRunning && !Demo.PitDemoGame.IsRunning) return;
            if (Demo.PitDemoGame.IsRunning)
            {
                // The witch asks first (A yes, B no; RESTART again is a yes).
                Demo.DemoDialogue.AskRestart(RestartDemo);
                return;
            }
            if (_restartArmedUntil < 0f)
            {
                _restartArmedUntil = Time.unscaledTime + restartConfirmSeconds;
                restartLabel.text = "SURE?";
                return;
            }
            DisarmRestart();
            LibretroCore.Reset();
            PlayerSlots.NoteRestart();
            Message("Game restarted");
        }

        // The demo from the very start: hi score, fruit, spell and the opening
        // dialogue wiped, then a fresh run (the cabinet relaunches it).
        static void RestartDemo()
        {
            if (PitScoreManager.Instance) Games.GameCenter.Submit(PitScoreManager.Instance.GetScore());
            foreach (var key in DemoProgressKeys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Demo.PitDemoGame.ResetLevels();   // back to the first level
            DeathRespawn.RestartRequested = true;
            Message("Game restarted");
        }

        // CONTROLS, over RESTART in the demo: a copy of RESTART's line with
        // SAVE's gold keycap.
        void BuildControlsButton()
        {
            var restartLine = restartButton.transform.parent;
            _controlsLine = Instantiate(restartLine.gameObject, restartLine.parent);
            _controlsLine.name = "Controls Line";
            _controlsLine.transform.SetSiblingIndex(restartLine.GetSiblingIndex());
            var button = _controlsLine.GetComponentInChildren<Button>(true);
            button.name = "Controls Button";
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Demo.DemoDialogue.ShowHowToPlay);
            if (button.image && saveButton.image) button.image.sprite = saveButton.image.sprite;
            button.spriteState = saveButton.spriteState;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label)
            {
                label.text = "CONTROLS";
                var saveLabel = saveButton.GetComponentInChildren<TMP_Text>(true);
                if (saveLabel) label.color = saveLabel.color;
            }
            _controlsLine.SetActive(false);

            // A line between the DEMO title and its buttons: one art pixel
            // in the title's colour, most of the panel's width.
            var title = content.transform.Find("Title");
            _demoRule = new GameObject("Demo Rule", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            var rule = (RectTransform)_demoRule.transform;
            rule.SetParent(content.transform, false);
            rule.SetSiblingIndex(title ? title.GetSiblingIndex() + 1 : 0);
            rule.sizeDelta = new Vector2(40f, 1f);
            var titleText = title ? title.GetComponent<TMP_Text>() : null;
            var ruleImage = _demoRule.GetComponent<Image>();
            ruleImage.color = titleText ? titleText.color : Color.white;
            ruleImage.raycastTarget = false;
            var size = _demoRule.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = 1f;
            size.preferredWidth = 40f;
            _demoRule.SetActive(false);
        }

        void DisarmRestart()
        {
            _restartArmedUntil = -1f;
            restartLabel.text = "RESTART";
        }

        void OnSlotTapped(int index)
        {
            if (slots[index].hold.ConsumeHold()) return;
            if (_deleteSlot >= 0) { HideDelete(); return; }
            if (index >= _saves.Count) return;
            bool ok = SaveStates.Load(_saves[index]);
            Message(ok ? $"Loaded your save of {Describe(_saves[index])}" : "Couldn't load that save", !ok);
        }

        void ShowDelete(int index)
        {
            HideDelete();
            _deleteSlot = index;
            slots[index].deleteButton.gameObject.SetActive(true);
        }

        void HideDelete()
        {
            if (_deleteSlot >= 0 && _deleteSlot < slots.Length) slots[_deleteSlot].deleteButton.gameObject.SetActive(false);
            _deleteSlot = -1;
        }

        void DeleteSlot(int index)
        {
            HideDelete();
            if (index >= _saves.Count) return;
            SaveStates.Delete(_saves[index]);
            Message("Save deleted");
        }

        static void Message(string text, bool error = false)
        {
            var picker = FindAnyObjectByType<GamePicker>();
            if (picker) picker.ShowMessage(text, error);
        }
    }
}
