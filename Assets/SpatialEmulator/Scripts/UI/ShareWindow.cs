// ShareWindow.cs — a CAP video's finished with a controller connected (ScreenCap): iOS's
// share sheet can't be steered by a controller (and is only ever on the phone), so this
// comes up instead, steered like the other windows (ControllerMenuNav) and on the TV
// with them (TvOutput.MenusOnTv). SAVE TO PHOTOS saves the video (Plugins/iOS/SEShare.mm),
// SHARE ON PHONE brings up iOS's sheet on the phone (AirDrop, Messages, YouTube...),
// CLOSE leaves it. The game holds while it's open. Styled like SAVES (SavesWindow).
// Made at run time.

using System;
using System.Runtime.InteropServices;
using SpatialEmulator.Mobile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class ShareWindow : MonoBehaviour
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SE_SaveVideo(string path, string gameObject, string method);
#endif

        static ShareWindow s_instance;

        GamePicker _picker;
        GameObject _dialog;
        TMP_Text _status;
        Button _save;
        string _path;
        Action<bool> _done;   // (true: on to iOS's sheet)
        bool _saving;

        public static bool IsOpen => s_instance && s_instance._dialog && s_instance._dialog.activeSelf;

        /// The video at `path`; `done` when it's closed (true: SHARE ON PHONE was chosen).
        public static bool Open(string path, Action<bool> done)
        {
            if (!s_instance)
            {
                var go = new GameObject("Share Window");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<ShareWindow>();
            }
            return s_instance.Show(path, done);
        }

        bool Show(string path, Action<bool> done)
        {
            if (!_dialog) Build();
            if (!_dialog) return false;
            _path = path;
            _done = done;
            _saving = false;
            _status.text = "VIDEO READY";
            _save.interactable = true;
            _dialog.SetActive(true);
            MobileRetroDepthLayerStack.Paused = true;
            return true;
        }

        void Close(bool share)
        {
            _dialog.SetActive(false);
            var done = _done;
            _done = null;
            done?.Invoke(share);
        }

        void Save()
        {
            if (_saving) return;
            _saving = true;
            _save.interactable = false;
            _status.text = "SAVING...";
#if UNITY_IOS && !UNITY_EDITOR
            SE_SaveVideo(_path, gameObject.name, nameof(OnSaved));
#else
            OnSaved("1");
#endif
        }

        // From SEShare.mm: "1" saved, "0" not (Photos refused, say).
        void OnSaved(string ok)
        {
            _saving = false;
            if (!IsOpen) return;
            if (ok == "1") { Haptics.Play(Haptics.Kind.Medium); _status.text = "SAVED TO PHOTOS"; }
            else { _status.text = "COULDN'T SAVE\nALLOW PHOTOS IN SETTINGS"; _save.interactable = true; }
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
            Stretch(shade.rectTransform);

            var toastImage = _picker.toast.GetComponent<Image>();
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            box.transform.SetParent(_dialog.transform, false);
            if (toastImage) { box.sprite = toastImage.sprite; box.type = toastImage.type; box.color = toastImage.color; }
            else box.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
            box.rectTransform.sizeDelta = new Vector2(176, 132);

            _status = Instantiate(_picker.toastLabel, box.transform, false);
            _status.gameObject.SetActive(true);
            _status.color = _picker.textColor;
            _status.alignment = TextAlignmentOptions.Center;
            _status.textWrappingMode = TextWrappingModes.Normal;
            _status.rectTransform.localScale = Vector3.one;
            var statusRect = _status.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 1f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.sizeDelta = new Vector2(-16, 28);
            statusRect.anchoredPosition = new Vector2(0, -8);

            _save = Key(box.transform, "Save", "SAVE TO PHOTOS", 66, Save);
            Key(box.transform, "Share", "SHARE ON PHONE", 38, () => Close(true));
            Key(box.transform, "Close", "CLOSE", 10, () => Close(false));
            _dialog.SetActive(false);
        }

        // One of the picker's keys, copied: `bottom` up from the box's foot.
        Button Key(Transform box, string name, string text, float bottom, Action tapped)
        {
            var key = (RectTransform)Instantiate(_picker.addRomButton.gameObject, box, false).transform;
            key.name = name;
            key.gameObject.SetActive(true);
            key.anchorMin = key.anchorMax = new Vector2(0.5f, 0f);
            key.pivot = new Vector2(0.5f, 0f);
            key.sizeDelta = new Vector2(132, 22);
            key.anchoredPosition = new Vector2(0, bottom);
            var label = key.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = text;
            var button = key.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => { Haptics.Play(Haptics.Kind.Light); tapped(); });
            return button;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
