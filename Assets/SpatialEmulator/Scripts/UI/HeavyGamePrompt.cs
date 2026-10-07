// HeavyGamePrompt.cs — the messages about games too heavy to keep full speed
// in AR (CatalogGame.FullScreenOnly: CV1000), which play in FULL SCREEN only
// (FullScreenTest: camera off, where they do keep up):
//   - one starting in AR goes straight into full screen (no message)
//   - the AR key pressed while one's on: it isn't available in AR; CHOOSE
//     GAME opens the game list with only the games that play in AR
// Styled like the game picker: its message box (the speech scanlines) and
// its key buttons, on a canvas scaled the same way. Made at run time.

using System;
using SpatialEmulator.Games;
using SpatialEmulator.Mobile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class HeavyGamePrompt : MonoBehaviour
    {
        static HeavyGamePrompt s_instance;

        GameObject _dialog;
        TMP_Text _message;
        Button _left, _right, _middle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("Heavy Game Prompt");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<HeavyGamePrompt>();
        }

        void OnEnable() => MobileRetroDepthLayerStack.GameStarted += OnGameStarted;
        void OnDisable() => MobileRetroDepthLayerStack.GameStarted -= OnGameStarted;

        void OnGameStarted(string game)
        {
            // (Started in AR some other way - the last game, resumed: straight
            // into full screen.)
            if (FullScreenTest.Enabled || GameCatalog.Find(game) is not { FullScreenOnly: true }) return;
            FullScreenTest.Enabled = true;
        }

        /// The AR key with a full-screen-only game on.
        public static void NotInAR(GamePicker picker)
        {
            if (!s_instance) return;
            s_instance.Show("HEAVY GAMES NOT AVAILABLE IN AR\n\nChoose another game to play in AR",
                ("CHOOSE GAME", () => { if (picker) picker.OpenForAR(); }), ("CANCEL", null));
        }

        // One button (b's label null) in the middle, or two side by side.
        void Show(string message, (string label, Action action) a, (string label, Action action) b)
        {
            if (!_dialog) Build();
            if (!_dialog) return;
            _message.text = message;
            bool two = b.label != null;
            Set(_middle, two ? default : a);
            Set(_left, two ? a : default);
            Set(_right, two ? b : default);
            _dialog.SetActive(true);
        }

        void Set(Button button, (string label, Action action) choice)
        {
            button.gameObject.SetActive(choice.label != null);
            if (choice.label == null) return;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = choice.label;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                _dialog.SetActive(false);
                Haptics.Play(Haptics.Kind.Light);
                choice.action?.Invoke();
            });
        }

        void Build()
        {
            var picker = FindAnyObjectByType<GamePicker>(FindObjectsInactive.Include);
            if (!picker || !picker.addRomButton || !picker.toast) return;

            _dialog = new GameObject("Dialog", typeof(RectTransform));
            _dialog.transform.SetParent(transform, false);
            var canvas = _dialog.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;   // over the game picker
            var scaler = _dialog.AddComponent<CanvasScaler>();
            var pickerScaler = picker.GetComponentInParent<Canvas>().rootCanvas.GetComponent<CanvasScaler>();
            if (pickerScaler)
            {
                scaler.uiScaleMode = pickerScaler.uiScaleMode;
                scaler.scaleFactor = pickerScaler.scaleFactor;
                scaler.referenceResolution = pickerScaler.referenceResolution;
                scaler.screenMatchMode = pickerScaler.screenMatchMode;
                scaler.matchWidthOrHeight = pickerScaler.matchWidthOrHeight;
                scaler.physicalUnit = pickerScaler.physicalUnit;
                scaler.fallbackScreenDPI = pickerScaler.fallbackScreenDPI;
                scaler.defaultSpriteDPI = pickerScaler.defaultSpriteDPI;
                scaler.referencePixelsPerUnit = pickerScaler.referencePixelsPerUnit;
            }
            _dialog.AddComponent<GraphicRaycaster>();

            // The world behind dimmed, and taps on it held off.
            var shade = new GameObject("Shade", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            shade.transform.SetParent(_dialog.transform, false);
            shade.color = new Color(0f, 0f, 0f, 0.6f);
            var shadeRect = shade.rectTransform;
            shadeRect.anchorMin = Vector2.zero; shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;

            // The box: the picker's message box look.
            var toastImage = picker.toast.GetComponent<Image>();
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            box.transform.SetParent(_dialog.transform, false);
            if (toastImage) { box.sprite = toastImage.sprite; box.type = toastImage.type; box.color = toastImage.color; }
            else box.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
            box.rectTransform.sizeDelta = new Vector2(170, 92);

            _message = Instantiate(picker.toastLabel, box.transform, false);
            _message.name = "Message";
            _message.gameObject.SetActive(true);
            _message.color = picker.textColor;
            _message.alignment = TextAlignmentOptions.Center;
            _message.textWrappingMode = TextWrappingModes.Normal;
            var messageRect = _message.rectTransform;
            messageRect.anchorMin = Vector2.zero; messageRect.anchorMax = Vector2.one;
            messageRect.pivot = new Vector2(0.5f, 0.5f);
            messageRect.offsetMin = new Vector2(10, 34); messageRect.offsetMax = new Vector2(-10, -8);
            messageRect.localScale = Vector3.one;

            _left = MakeButton(picker, box.transform, -39);
            _right = MakeButton(picker, box.transform, 39);
            _middle = MakeButton(picker, box.transform, 0);
            _dialog.SetActive(false);
        }

        static Button MakeButton(GamePicker picker, Transform parent, float x)
        {
            var rect = (RectTransform)Instantiate(picker.addRomButton.gameObject, parent, false).transform;
            rect.name = "Button";
            rect.gameObject.SetActive(true);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(70, 20);
            rect.anchoredPosition = new Vector2(x, 9);
            var label = rect.GetComponentInChildren<TMP_Text>(true);
            if (label)
            {
                // Longer labels than the picker's own shrink to fit, with
                // room left at both ends.
                label.fontSizeMax = label.fontSize * 0.8f;
                label.fontSizeMin = label.fontSize * 0.4f;
                label.enableAutoSizing = true;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.margin = new Vector4(7, 0, 7, 0);
            }
            return rect.GetComponent<Button>();
        }
    }
}
