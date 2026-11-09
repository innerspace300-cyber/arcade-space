// SettingsAudioRows.cs — Settings > SOUND and Settings > CAP: two windows
// (ABOUT's, emptied, like COLORS). SOUND has the MUSIC and SFX volume sliders
// (GameAudio), the HAPTICS switch (Haptics), PERF STATS (PerfReadout) and FULL
// SCREEN; CAP has the CAP key's video options - CAP LENGTH, FADE IN and FADE
// OUT (ScreenCap; holding CAP opens it too). Rows are copies of the LAYER
// SPACING row (title, detail, slider) and of GAMES IN AR's (title, detail,
// ON/OFF key). Their keys and COLORS and ABOUT sit two by two under the
// Settings rows. Made at run time.

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class SettingsAudioRows : MonoBehaviour
    {
        GameObject _window, _capWindow;
        static SettingsAudioRows s_instance;

        /// Settings, open at this window (holding the CAP key).
        public static void OpenCapOptions()
        {
            var settings = FindAnyObjectByType<SettingsScreen>(FindObjectsInactive.Include);
            if (!settings || !s_instance || !s_instance._capWindow) return;
            settings.Open();
            s_instance._capWindow.SetActive(true);
        }

        public static void Build(SettingsScreen settings)
        {
            var rows = settings.window.transform.Find("Safe Area/Panel/Rows");
            var sliderRow = rows ? rows.Find("LAYER SPACING") : null;
            var toggleRow = rows ? rows.Find("GAMES IN AR") : null;
            if (!sliderRow || !toggleRow || !settings.aboutButton || !settings.aboutWindow) return;
            s_instance = settings.gameObject.AddComponent<SettingsAudioRows>();
            s_instance.BuildWindow(settings, sliderRow, toggleRow);
        }

        void BuildWindow(SettingsScreen settings, Transform sliderRow, Transform toggleRow)
        {
            GameAudio.Apply();

            // Four keys under the rows, two by two: SOUND and CAP over COLORS and ABOUT
            // (four across would be too narrow for their names).
            var about = (RectTransform)settings.aboutButton.transform;
            var soundKey = Key(about, "Sound Button", "SOUND", () => _window.SetActive(true));
            var capKey = Key(about, "Cap Button", "CAP", () => _capWindow.SetActive(true));
            var colors = about.parent.Find("Colors Button") as RectTransform;
            var grid = new[] { soundKey, capKey, colors, about };
            const float width = 78f, gap = 4f, rowGap = 4f;
            float bottom = about.anchoredPosition.y, height = about.sizeDelta.y;
            for (int i = 0; i < grid.Length; i++)
            {
                if (!grid[i]) continue;
                int col = i % 2, row = i / 2;   // (row 0 the upper one)
                grid[i].sizeDelta = new Vector2(width, height);
                grid[i].anchoredPosition = new Vector2((col - 0.5f) * (width + gap), bottom + (1 - row) * (height + rowGap));
            }

            var content = MakeWindow(settings, "Sound Window", "SOUND", out _window);
            Volume(sliderRow, content, "MUSIC", "ENDLESS KNIGHT's soundtrack", () => GameAudio.Music, v => GameAudio.Music = v);
            Volume(sliderRow, content, "SFX", "Sound effects, and the games' own sound", () => GameAudio.Sfx, v => GameAudio.Sfx = v);
            Switch(toggleRow, content, "HAPTICS", "Taps you feel as you play", () => Haptics.Enabled, on => Haptics.Enabled = on);
            Switch(toggleRow, content, "PERF STATS", "Frame rate and phone heat, top left", () => PerfReadout.Enabled, on => PerfReadout.Enabled = on);
            Switch(toggleRow, content, "FULL SCREEN", "Camera off: the game fills the screen", () => FullScreenTest.Enabled, on => FullScreenTest.Enabled = on);

            var cap = MakeWindow(settings, "Cap Window", "CAP", out _capWindow);
            Choice(toggleRow, cap, "CAP LENGTH", "How long a CAP video records", () => ScreenCap.LengthLabel, ScreenCap.NextLength);
            Switch(toggleRow, cap, "FADE IN", "CAP videos start from black", () => ScreenCap.FadeIn, on => ScreenCap.FadeIn = on);
            Switch(toggleRow, cap, "FADE OUT", "CAP videos end in black", () => ScreenCap.FadeOut, on => ScreenCap.FadeOut = on);

            _window.SetActive(false);
            _capWindow.SetActive(false);
        }

        // A copy of ABOUT's key: `label`, doing `tap`.
        static RectTransform Key(RectTransform about, string name, string label, UnityEngine.Events.UnityAction tap)
        {
            var key = Instantiate(about.gameObject, about.parent).GetComponent<RectTransform>();
            key.name = name;
            var button = key.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(tap);
            var text = key.GetComponentInChildren<TMP_Text>(true);
            if (text) text.text = label;
            return key;
        }

        // ABOUT's window, emptied: `title` on its banner, its rows in a column that scrolls
        // (more than fit) where ABOUT's text was. Returns that column.
        public static Transform MakeWindow(SettingsScreen settings, string name, string titleText, out GameObject window)
        {
            var w = Instantiate(settings.aboutWindow, settings.aboutWindow.transform.parent);
            w.name = name;
            window = w;
            var area = (RectTransform)w.transform.Find("Text");
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image)).GetComponent<RectTransform>();
            viewport.SetParent(w.transform, false);
            if (area)
            {
                viewport.anchorMin = area.anchorMin; viewport.anchorMax = area.anchorMax;
                viewport.offsetMin = area.offsetMin; viewport.offsetMax = area.offsetMax;
                Destroy(area.gameObject);
            }
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // (catches the drag)
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 8f;
            var source = w.transform.Find("Source Button");
            if (source) Destroy(source.gameObject);
            var title = w.transform.Find("Title Banner")?.GetComponentInChildren<TMP_Text>(true);
            if (title) title.text = titleText;
            var close = w.transform.Find("Close Button")?.GetComponent<Button>();
            if (close)
            {
                close.onClick.RemoveAllListeners();
                close.onClick.AddListener(() => { PlayerPrefs.Save(); w.SetActive(false); });
            }
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            return content;
        }

        void Update()
        {
            // Settings closing takes these with it.
            if (SettingsScreen.IsOpen) return;
            if (_window && _window.activeSelf) _window.SetActive(false);
            if (_capWindow && _capWindow.activeSelf) _capWindow.SetActive(false);
        }

        static void Volume(Transform like, Transform parent, string name, string about, System.Func<float> get, System.Action<float> set)
        {
            var row = Instantiate(like.gameObject, parent);
            row.name = name;
            foreach (var trigger in row.GetComponentsInChildren<EventTrigger>(true)) Destroy(trigger);   // (spacing's fade-while-dragging)
            var title = row.transform.Find("Title")?.GetComponent<TMP_Text>();
            var detail = row.transform.Find("Detail")?.GetComponent<TMP_Text>();
            var slider = row.GetComponentInChildren<Slider>(true);
            var tick = slider.transform.Find("Track/Default Tick");   // (spacing's default mark)
            if (tick) Destroy(tick.gameObject);
            slider.onValueChanged.RemoveAllListeners();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.interactable = true;
            var group = slider.GetComponent<CanvasGroup>();
            if (group) group.alpha = 1f;
            void Show(float v)
            {
                if (title) title.text = $"{name}  {(v <= 0.001f ? "OFF" : Mathf.RoundToInt(v * 100f) + "%")}";
                if (detail) detail.text = about;
            }
            slider.SetValueWithoutNotify(get());
            Show(get());
            slider.onValueChanged.AddListener(v => { set(v); Show(v); });
        }

        // A key that steps through choices, showing the one picked.
        static void Choice(Transform like, Transform parent, string name, string about, System.Func<string> show, System.Action next)
        {
            var row = Instantiate(like.gameObject, parent);
            row.name = name;
            var title = row.transform.Find("Title")?.GetComponent<TMP_Text>();
            var detail = row.transform.Find("Detail")?.GetComponent<TMP_Text>();
            var button = row.GetComponentInChildren<Button>(true);
            var label = button ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (title) title.text = name;
            if (detail) detail.text = about;
            if (label) label.text = show();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                next();
                if (label) label.text = show();
                Haptics.Play(Haptics.Kind.Light);
            });
        }

        static void Switch(Transform like, Transform parent, string name, string about, System.Func<bool> get, System.Action<bool> set)
        {
            var row = Instantiate(like.gameObject, parent);
            row.name = name;
            var title = row.transform.Find("Title")?.GetComponent<TMP_Text>();
            var detail = row.transform.Find("Detail")?.GetComponent<TMP_Text>();
            var button = row.GetComponentInChildren<Button>(true);
            var label = button ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (title) title.text = name;
            if (detail) detail.text = about;
            void Show() { if (label) label.text = get() ? "ON" : "OFF"; }
            Show();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                set(!get());
                Show();
                Haptics.Play(Haptics.Kind.Light);   // (felt as it comes on)
            });
        }
    }
}
