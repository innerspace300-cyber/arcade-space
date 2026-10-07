// SavesWindow.cs — save states in full screen, from the screen's menu
// (MenuDropdown's SAVE and SAVES keys); in AR they're on the cabinet's side
// panel (SaveStatePanel). SAVE saves the game now, with a thumbnail of its
// latest frame (FullScreenTest.CaptureFrame); SAVES opens a window of the
// game's saves, newest first - thumbnail and time - tap one to load it, DEL
// to delete it. The game holds while it's open. Styled like the leaderboard
// (LeaderboardWindow: the game picker's message box, keys, text and colours,
// on a canvas scaled the same way). Made at run time. Not for the demo (it
// doesn't save).

using System.Collections.Generic;
using SpatialEmulator.Games;
using SpatialEmulator.Mobile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class SavesWindow : MonoBehaviour
    {
        const float RowHeight = 32f;

        static SavesWindow s_instance;

        GamePicker _picker;
        GameObject _dialog;
        TMP_Text _status;
        RectTransform _list;
        ScrollRect _scroll;
        string _game;
        readonly List<Texture2D> _thumbs = new List<Texture2D>();

        /// A game that saves is running (an emulated one: not the demo).
        public static bool CanSave => LibretroCore.IsRunning && !Demo.PitDemoGame.IsRunning && !string.IsNullOrEmpty(GameSelection.Current);

        public static bool IsOpen => s_instance && s_instance._dialog && s_instance._dialog.activeSelf;

        /// Saves the running game, with a thumbnail of its picture.
        public static void SaveNow()
        {
            if (!CanSave) return;
            var thumb = FullScreenTest.CaptureFrame();
            var save = SaveStates.Save(GameSelection.Current, thumb);
            if (thumb) Destroy(thumb);
            Message(save != null ? "Game saved" : "Couldn't save this game", save == null);
        }

        public static void Open()
        {
            if (!CanSave) return;
            if (!s_instance)
            {
                var go = new GameObject("Saves Window");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<SavesWindow>();
            }
            s_instance.Show();
        }

        void Show()
        {
            if (!_dialog) Build();
            if (!_dialog) return;
            _dialog.SetActive(true);
            MobileRetroDepthLayerStack.Paused = true;
            Fill();
        }

        void Close()
        {
            _dialog.SetActive(false);
            Clear();
            MobileRetroDepthLayerStack.Paused = GamePicker.IsOpen || SettingsScreen.IsOpen || LeaderboardWindow.IsOpen;
        }

        void LateUpdate()
        {
            // (The game changed or stopped under it, or full screen ended.)
            if (IsOpen && (!CanSave || GameSelection.Current != _game || !FullScreenTest.Enabled)) Close();
        }

        void Fill()
        {
            Clear();
            _game = GameSelection.Current;
            var saves = SaveStates.List(_game);
            _status.gameObject.SetActive(saves.Count == 0);
            _status.text = "No saves yet - tap SAVE in the menu to save";
            foreach (var save in saves) Row(save);
            _scroll.verticalNormalizedPosition = 1f;
        }

        void Clear()
        {
            for (int i = _list.childCount - 1; i >= 0; i--) Destroy(_list.GetChild(i).gameObject);
            foreach (var thumb in _thumbs) if (thumb) Destroy(thumb);
            _thumbs.Clear();
        }

        // A save: its thumbnail and time - tap to load - and DEL.
        void Row(SaveState save)
        {
            var row = new GameObject("Save", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
            row.transform.SetParent(_list, false);
            row.GetComponent<LayoutElement>().preferredHeight = RowHeight;
            var back = row.GetComponent<Image>();
            back.color = new Color(1f, 1f, 1f, 0.06f);
            var button = row.GetComponent<Button>();
            button.targetGraphic = back;
            button.onClick.AddListener(() =>
            {
                Haptics.Play(Haptics.Kind.Light);
                bool ok = SaveStates.Load(save);
                Message(ok ? $"Loaded your save of {Describe(save)}" : "Couldn't load that save", !ok);
                if (ok) Close();
            });

            // The thumbnail, fitted in a 40 x 30 box at its own shape.
            var thumb = Thumbnail(save);
            var image = new GameObject("Thumbnail", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(row.transform, false);
            image.raycastTarget = false;
            image.texture = thumb;
            image.color = thumb ? Color.white : new Color(0f, 0f, 0f, 0.5f);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            float aspect = thumb ? thumb.width / (float)thumb.height : 4f / 3f;
            rect.sizeDelta = aspect > 40f / 28f ? new Vector2(40f, 40f / aspect) : new Vector2(28f * aspect, 28f);
            rect.anchoredPosition = new Vector2(2f + (40f - rect.sizeDelta.x) * 0.5f, 0f);

            var time = Text(row.transform, Describe(save), _picker.textColor);
            var timeRect = time.rectTransform;
            timeRect.anchorMin = new Vector2(0f, 0f);
            timeRect.anchorMax = new Vector2(1f, 1f);
            timeRect.offsetMin = new Vector2(48f, 0f);
            timeRect.offsetMax = new Vector2(-40f, 0f);

            var delete = (RectTransform)Instantiate(_picker.addRomButton.gameObject, row.transform, false).transform;
            delete.name = "Delete";
            delete.gameObject.SetActive(true);
            delete.anchorMin = delete.anchorMax = delete.pivot = new Vector2(1f, 0.5f);
            delete.sizeDelta = new Vector2(34, 20);
            delete.anchoredPosition = new Vector2(-2f, 0f);
            var deleteLabel = delete.GetComponentInChildren<TMP_Text>(true);
            if (deleteLabel) deleteLabel.text = "DEL";
            var deleteButton = delete.GetComponent<Button>();
            deleteButton.onClick = new Button.ButtonClickedEvent();
            deleteButton.onClick.AddListener(() =>
            {
                Haptics.Play(Haptics.Kind.Light);
                SaveStates.Delete(save);
                Message("Save deleted");
                Fill();
            });
        }

        Texture2D Thumbnail(SaveState save)
        {
            if (save.thumbnailPath == null) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(System.IO.File.ReadAllBytes(save.thumbnailPath))) { Destroy(tex); return null; }
            }
            catch (System.Exception) { Destroy(tex); return null; }
            tex.filterMode = FilterMode.Point;
            _thumbs.Add(tex);
            return tex;
        }

        static string Describe(SaveState save)
        {
            var when = save.savedAt;
            var today = System.DateTime.Now.Date;
            if (when.Date == today) return when.ToString("h:mm tt");
            if (when.Date == today.AddDays(-1)) return "Yesterday " + when.ToString("h:mm tt");
            return when.ToString("MMM d h:mm tt");
        }

        static void Message(string text, bool error = false)
        {
            var picker = FindAnyObjectByType<GamePicker>();
            if (picker) picker.ShowMessage(text, error);
        }

        TMP_Text Text(Transform parent, string text, Color color)
        {
            var label = Instantiate(_picker.toastLabel, parent, false);
            label.gameObject.SetActive(true);
            label.text = text;
            label.color = color;
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.rectTransform.localScale = Vector3.one;
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

            var title = Text(box.transform, "SAVES", _picker.textColor);
            title.alignment = TextAlignmentOptions.Center;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0, 16);
            titleRect.anchoredPosition = new Vector2(0, -10);

            // The list, scrolling between the title and the CLOSE key.
            var view = new GameObject("View", typeof(RectTransform), typeof(RectMask2D), typeof(Image)).GetComponent<RectTransform>();
            view.SetParent(box.transform, false);
            view.GetComponent<Image>().color = Color.clear;   // (to be dragged)
            Stretch(view, 10, 36, 10, 30);
            _list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            _list.SetParent(view, false);
            _list.anchorMin = new Vector2(0f, 1f);
            _list.anchorMax = new Vector2(1f, 1f);
            _list.pivot = new Vector2(0.5f, 1f);
            _list.sizeDelta = Vector2.zero;
            var layout = _list.GetComponent<VerticalLayoutGroup>();
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 3f;
            _list.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll = box.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = view;
            _scroll.content = _list;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;

            _status = Text(box.transform, "", _picker.textColor);
            _status.alignment = TextAlignmentOptions.Center;
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

        static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
