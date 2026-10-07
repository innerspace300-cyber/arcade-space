// SettingsAudioRows.cs — Settings > SOUND: a window, SOUND + CAP (ABOUT's,
// emptied, like COLORS) with the MUSIC and SFX volume sliders (GameAudio),
// the HAPTICS switch (Haptics), PERF STATS (PerfReadout), and the CAP key's
// video options - CAP LENGTH, FADE IN and FADE OUT (ScreenCap; holding CAP opens the
// window too) - copies of the LAYER SPACING row (title, detail, slider)
// and of GAMES IN AR's (title, detail, ON/OFF key). Its button goes beside
// ABOUT and COLORS, the three of them sharing the row. Made at run time.

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class SettingsAudioRows : MonoBehaviour
    {
        GameObject _window;
        static SettingsAudioRows s_instance;

        /// Settings, open at this window (holding the CAP key).
        public static void OpenCapOptions()
        {
            var settings = FindAnyObjectByType<SettingsScreen>(FindObjectsInactive.Include);
            if (!settings || !s_instance || !s_instance._window) return;
            settings.Open();
            s_instance._window.SetActive(true);
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

            // ABOUT, COLORS and SOUND along the bottom, a third each.
            var about = (RectTransform)settings.aboutButton.transform;
            var open = Instantiate(about.gameObject, about.parent).GetComponent<RectTransform>();
            open.name = "Sound Button";
            var buttons = new System.Collections.Generic.List<RectTransform> { about };
            var colors = about.parent.Find("Colors Button") as RectTransform;
            if (colors) buttons.Add(colors);
            buttons.Add(open);
            const float width = 52f, gap = 4f;
            float y = about.anchoredPosition.y;
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].sizeDelta = new Vector2(width, buttons[i].sizeDelta.y);
                buttons[i].anchoredPosition = new Vector2((i - (buttons.Count - 1) * 0.5f) * (width + gap), y);
            }
            var openButton = open.GetComponent<Button>();
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(() => _window.SetActive(true));
            var openLabel = open.GetComponentInChildren<TMP_Text>(true);
            if (openLabel) openLabel.text = "SOUND";

            // The window: ABOUT's, emptied.
            _window = Instantiate(settings.aboutWindow, settings.aboutWindow.transform.parent);
            _window.name = "Sound Window";
            // Its rows scroll (more than fit): a viewport where ABOUT's text
            // was, the rows in a column inside it, as tall as they are.
            var area = (RectTransform)_window.transform.Find("Text");
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image)).GetComponent<RectTransform>();
            viewport.SetParent(_window.transform, false);
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
            var source = _window.transform.Find("Source Button");
            if (source) Destroy(source.gameObject);
            var title = _window.transform.Find("Title Banner")?.GetComponentInChildren<TMP_Text>(true);
            if (title) title.text = "SOUND + CAP";
            var close = _window.transform.Find("Close Button")?.GetComponent<Button>();
            if (close)
            {
                close.onClick.RemoveAllListeners();
                close.onClick.AddListener(() => { PlayerPrefs.Save(); _window.SetActive(false); });
            }
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;

            Volume(sliderRow, content, "MUSIC", "ENDLESS KNIGHT's soundtrack", () => GameAudio.Music, v => GameAudio.Music = v);
            Volume(sliderRow, content, "SFX", "Sound effects, and the games' own sound", () => GameAudio.Sfx, v => GameAudio.Sfx = v);
            Switch(toggleRow, content, "HAPTICS", "Taps you feel as you play", () => Haptics.Enabled, on => Haptics.Enabled = on);
            Switch(toggleRow, content, "PERF STATS", "Frame rate and phone heat, top left", () => PerfReadout.Enabled, on => PerfReadout.Enabled = on);
            Switch(toggleRow, content, "FULL SCREEN", "Camera off: the game fills the screen", () => FullScreenTest.Enabled, on => FullScreenTest.Enabled = on);
            Choice(toggleRow, content, "CAP LENGTH", "How long a CAP video records", () => ScreenCap.LengthLabel, ScreenCap.NextLength);
            Switch(toggleRow, content, "FADE IN", "CAP videos start from black", () => ScreenCap.FadeIn, on => ScreenCap.FadeIn = on);
            Switch(toggleRow, content, "FADE OUT", "CAP videos end in black", () => ScreenCap.FadeOut, on => ScreenCap.FadeOut = on);
            _window.SetActive(false);
        }

        void Update()
        {
            // Settings closing takes this with it.
            if (_window && _window.activeSelf && !SettingsScreen.IsOpen) _window.SetActive(false);
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
