// SettingsScreen.cs — the pixel-art Settings window (gear button beside
// GAMES): controller status, when to show the on-screen controls, layer
// spacing (AR Frame mode, games that space out; live: the window fades while the slider is dragged so the
// cabinet shows through), whether games resume from their newest save, and
// whether the game picker shows in the cabinet's frame (Games in AR), the
// SOUND window (music and SFX volumes, haptics: SettingsAudioRows), and
// ABOUT: credits, the GPL notice, a link to the source and the licence text
// (the GPL requires all three in the app). Built by GamePickerBuilder, same
// style as the game picker.

using SpatialEmulator.Controls;
using SpatialEmulator.Games;
using SpatialEmulator.Mobile;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class SettingsScreen : MonoBehaviour
    {
        static bool s_open;

        public static bool IsOpen => s_open;

        public Button openButton;
        public GameObject window;
        public Button closeButton;
        public TMP_Text controllerStatus;
        public Button touchControlsButton;
        public TMP_Text touchControlsValue;
        public TMP_Text touchControlsDetail;
        public Button resumeButton;
        public TMP_Text resumeValue;
        public Button pickerInARButton;
        public TMP_Text pickerInARValue;
        public Slider spacingSlider;
        public TMP_Text spacingValue;
        [Tooltip("Faded while the spacing slider is dragged, so the cabinet shows through.")]
        public CanvasGroup windowGroup;
        [Range(0f, 1f)] public float draggingAlpha = 0.2f;
        [Tooltip("Slider values this close to 1 snap to the default spacing.")]
        public float snapToDefault = 0.06f;

        [Header("About")]
        public Button aboutButton;
        public GameObject aboutWindow;
        public Button aboutCloseButton;
        public Button sourceButton;
        public RectTransform aboutContent;
        [Tooltip("Cloned once per block of text: one TMP text can't hold the whole licence (65k vertex limit).")]
        public TMP_Text aboutTextTemplate;
        public TextAsset licenseText;
        public string sourceUrl = "https://github.com/innerspace300-cyber/arcade-space";

        [TextArea] public string noControllerText =
            "No controller connected. Pair a PS5, PS4, Xbox or MFi controller in the iOS Settings app under Bluetooth";

        void Awake()
        {
            noControllerText = "None connected. Pair one in iOS Settings > Bluetooth";
            window.SetActive(false);
            openButton.onClick.AddListener(Open);
            // Top right, faded (GAMES has the top left, or the cabinet's panel).
            var gear = (RectTransform)openButton.transform;
            gear.anchorMin = gear.anchorMax = new Vector2(1f, 1f);
            gear.anchoredPosition = new Vector2(-24f, gear.anchoredPosition.y + 18f);   // (as far in as the menu key, MenuDropdown)
            var fade = openButton.GetComponent<CanvasGroup>();
            if (!fade) fade = openButton.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 0.35f;
            gear.localScale = Vector3.one * 0.8f;   // (small and faint: out of the way)
            closeButton.onClick.AddListener(Close);
            touchControlsButton.onClick.AddListener(CycleTouchControls);
            resumeButton.onClick.AddListener(ToggleResume);
            pickerInARButton.onClick.AddListener(TogglePickerInAR);
            aboutWindow.SetActive(false);
            aboutTextTemplate.gameObject.SetActive(false);
            aboutButton.onClick.AddListener(() => aboutWindow.SetActive(true));
            aboutCloseButton.onClick.AddListener(() => aboutWindow.SetActive(false));
            sourceButton.onClick.AddListener(() => Application.OpenURL(sourceUrl));
            spacingSlider.minValue = 0f;
            spacingSlider.maxValue = AppSettings.MaxLayerSpacingScale;
            spacingSlider.onValueChanged.AddListener(OnSpacingChanged);
            var trigger = spacingSlider.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerDown, () => windowGroup.alpha = draggingAlpha);
            AddTrigger(trigger, EventTriggerType.PointerUp, () => windowGroup.alpha = 1f);
            GamepadInput.ControllerChanged += OnControllerChanged;
            ControlColorsWindow.Build(this);   // COLORS, beside ABOUT
            SettingsAudioRows.Build(this);     // SOUND (music, SFX, haptics), beside them
            // Shorter rows (after SOUND and CAP have copied them as they were): each key's
            // hint beside it, not on a line of its own over it - room for the keys below.
            var rows = window.transform.Find("Safe Area/Panel/Rows");
            foreach (string row in new[] { "ON-SCREEN CONTROLS", "RESUME SAVES", "GAMES IN AR" })
                HintBesideKey(rows ? rows.Find(row) : null);
        }

        void OnDestroy()
        {
            GamepadInput.ControllerChanged -= OnControllerChanged;
            if (s_open) SetOpen(false);
        }

        public void Open() => SetOpen(true);
        public void Close()
        {
            PlayerPrefs.Save(); // the slider only saves on close
            SetOpen(false);
        }

        // The row's Detail text moved into its key's strip (Value), to the right of the key,
        // wrapping in what's left of the row's width; the strip as tall as the taller of the two.
        static void HintBesideKey(Transform row)
        {
            var value = row ? row.Find("Value") as RectTransform : null;
            var detail = row ? row.Find("Detail") as RectTransform : null;
            if (!value || !detail || value.childCount == 0) return;
            var key = (RectTransform)value.GetChild(0);
            const float gap = 6f;
            detail.SetParent(value, false);
            detail.anchorMin = new Vector2(0f, 0f); detail.anchorMax = new Vector2(1f, 1f);
            detail.pivot = new Vector2(0f, 0.5f);
            detail.offsetMin = new Vector2(key.sizeDelta.x + gap, 0f);
            detail.offsetMax = Vector2.zero;
            var text = detail.GetComponent<TMP_Text>();
            if (text) text.alignment = TextAlignmentOptions.MidlineLeft;
            // The strip: at least the key's height, more if the hint wraps to more lines.
            var fit = value.gameObject.AddComponent<HintHeight>();
            fit.text = text; fit.key = key; fit.inset = key.sizeDelta.x + gap;
        }

        // Keeps a key's strip as tall as its hint needs (the hint can change: ON-SCREEN CONTROLS').
        class HintHeight : MonoBehaviour
        {
            public TMP_Text text;
            public RectTransform key;
            public float inset;
            LayoutElement _element;
            string _shown;
            float _width = -1f;

            void LateUpdate()
            {
                if (!text) return;
                float width = ((RectTransform)transform).rect.width - inset;
                if (width <= 1f || (text.text == _shown && Mathf.Approximately(width, _width))) return;
                _shown = text.text; _width = width;
                if (!_element) _element = GetComponent<LayoutElement>();
                float need = text.GetPreferredValues(text.text, width, 0f).y + 2f;
                _element.preferredHeight = Mathf.Max(key.sizeDelta.y, need);
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform.parent);
            }
        }

        static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        void OnSpacingChanged(float value)
        {
            if (Mathf.Abs(value - 1f) < snapToDefault && !Mathf.Approximately(value, 1f))
            {
                spacingSlider.SetValueWithoutNotify(1f);
                value = 1f;
            }
            AppSettings.LayerSpacingScale = value;
            ShowSpacing(value);
        }

        void ShowSpacing(float value)
        {
            // Only in AR Frame mode, and only for games that space out: the
            // whole LAYER SPACING row is gone in the cabinet (CRT) mode and
            // for the always-flat games.
            spacingValue.transform.parent.gameObject.SetActive(!CrtEffect.On && !FlatGames.Current);
            spacingValue.text = value <= 0.001f ? "Flat - every layer on one plane"
                : Mathf.Approximately(value, 1f) ? "Default"
                : $"{value:0.0}x the default";
        }

        void SetOpen(bool open)
        {
            s_open = open;
            window.SetActive(open);
            if (!open) aboutWindow.SetActive(false);
            else FillAbout();
            MobileRetroDepthLayerStack.Paused = open || GamePicker.IsOpen;
            if (open) Refresh();
        }

        void FillAbout()
        {
            if (aboutContent.childCount > 1) return; // only the template so far
            string text =
                $"ARCADE {Application.version}\n" +
                "(c) 2026 Wayne Lamb\n\n" +
                "ARcade is free software: you can redistribute it and/or modify it under the terms of the " +
                "GNU General Public License as published by the Free Software Foundation, version 2 or (at your option) " +
                "any later version. It comes with NO WARRANTY. The complete source code is at\n" + sourceUrl + "\n\n" +
                "ARcade doesn't include any games. Only play ROMs you own.\n\n" +
                "EMULATION\n" +
                "Based on MAME, copyright (c) 1997-2026 MAMEdev and contributors, under the GNU General Public License " +
                "version 2 (some source files under less restrictive licences, as noted in their headers), " +
                "through the libretro MAME core. MAME is a registered trademark of Gregory Ember. " +
                "This software is based in part on the work of the Independent JPEG Group.\n\n" +
                "ART AND FONTS\n" +
                "Pixel UI & HUD 4, Pixel UI pack 3 and the Dead Revolver fonts by Dead Revolver. " +
                "Ultimate Mobile Controls Kit by Downtown Game Studio. Digital Numbers font. " +
                "Feel and Nice Vibrations by More Mountains. Used under their licences.\n\n" +
                "ENDLESS KNIGHT (DEMO)\n" +
                "Blood FX 1.0 and 2.0 by Raphael Hatencia (RagnaPixel Studio), under Creative Commons Attribution 4.0 " +
                "(recoloured). Fruit icons by scrimsy (Trent Consalvo), under Creative Commons Attribution-ShareAlike 4.0. " +
                "Diamonds & Gems by Random Precision Software. " +
                "Pixel Potions by Skulitom Rivskiy, made with PixelForge (CC0). Candy icons by acursedpixel. Dark crystal by Frakassets. " +
                "Moonlit Shrine Ruins background art by KATO. Mountain Temple & Bamboo Forest from Nasha's Parallax Worlds. " +
                "Starry Night backgrounds, the portal pack, the fairies, the Salamander Witch, Fantasy #25 (the viking), " +
                "Gollux, the Cacodaemon, the Necromancer, Revenant, Zombie, Elder Dragon and Giant Toad packs, " +
                "Fantasy Eggs, the 32x32 key pack, Fast Food by Captainskeleto, food and flower icons, " +
                "Super Pixel Monsters Pack 2 by Will Tice / unTied Games (untiedgames.com), " +
                "Rat by ggoolmool (ggoolmool.itch.io), the Fantasy #8 cat demon, " +
                "and the gravestone, mushroom, trap, bomb and explosion sprites used under their licences.\n\n" +
                "THE TIME CRYSTAL (DEMO)\n" +
                "Animated Pixel Ships and Pixel Spaceships for SHMUP 2 by dylestorm (livingtheindie.com). " +
                "Super Pixel Projectiles Packs 1 and 2 by Will Tice (untiedgames.com). " +
                "3D Lasers and Jet engine effects by Hovl Studio. Combat drone by SovDigital. " +
                "Magic Crystal by Creativekind. Sci-fi space backgrounds, Pixel Arsenal, Retro Arsenal and " +
                "the coin sprites used under their licences.\n\n" +
                "MUSIC AND SOUND\n" +
                "Endless Knight music from AudioJungle (Elevate Audio and others). " +
                "Boss Battle by Retro BGM Chan and Synthwave Retro 80s by Tunetank (Pixabay). " +
                "Sound effects from AudioJungle, Toon Projectiles SFX, Pixel Arsenal, Retro Arsenal, " +
                "Pixabay and the freesound community, used under their licences.\n\n" +
                "Made with Unity. All trademarks are the property of their respective owners.\n\n" +
                (licenseText ? Unwrap(licenseText.text) : "");
            // Blocks of whole paragraphs, each small enough for one text mesh.
            var block = new System.Text.StringBuilder();
            foreach (string paragraph in text.Replace("\r", "").Split(new[] { "\n\n" }, System.StringSplitOptions.None))
            {
                if (block.Length > 0 && block.Length + paragraph.Length > 2500) { AddAboutBlock(block.ToString()); block.Clear(); }
                if (block.Length > 0) block.Append("\n\n");
                block.Append(paragraph);
            }
            if (block.Length > 0) AddAboutBlock(block.ToString());
        }

        // The licence file is hard-wrapped at 72 columns and indented with
        // tabs: rejoin each paragraph's lines and collapse the runs of
        // whitespace, so it wraps to the panel.
        static string Unwrap(string text)
        {
            text = System.Text.RegularExpressions.Regex.Replace(text.Replace("\r", ""), @"(?<!\n)\n(?!\n)", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");
            return System.Text.RegularExpressions.Regex.Replace(text, @"\n ", "\n").Trim();
        }

        void AddAboutBlock(string text)
        {
            var copy = Instantiate(aboutTextTemplate, aboutContent);
            copy.text = text;
            copy.gameObject.SetActive(true);
        }

        // The controller's POWER: a ten-bar meter in its colour (green, yellow under half, red under a fifth), the percent,
        // and CHARGING on the cable. Nothing if it doesn't say.
        static string PowerLine()
        {
            int level = GamepadInput.Battery(out bool charging);
            if (level < 0) return "";
            int bars = Mathf.Clamp(Mathf.CeilToInt(level / 10f), level > 0 ? 1 : 0, 10);
            string colour = level < 20 ? "#FF4A4A" : level < 50 ? "#FFD23C" : "#5CF29A";
            return $"\nPOWER <color={colour}>{new string('|', bars)}</color><color=#5A5470>{new string('|', 10 - bars)}</color> {level}%"
                + (charging ? " CHARGING" : level < 20 ? " <color=#FF4A4A>LOW</color>" : "");
        }

        float _nextPower;

        void LateUpdate()
        {
            // (kept current while it's open: a few seconds apart)
            if (!s_open || Time.unscaledTime < _nextPower || GamepadInput.ControllerName == null) return;
            _nextPower = Time.unscaledTime + 3f;
            controllerStatus.text = $"Connected: {GamepadInput.ControllerName}{PowerLine()}";
        }

        void OnControllerChanged(string name)
        {
            if (s_open) Refresh();
        }

        void CycleTouchControls()
        {
            AppSettings.TouchControls = (TouchControlsMode)(((int)AppSettings.TouchControls + 1) % 3);
            Refresh();
        }

        void ToggleResume()
        {
            AppSettings.ResumeLatestSave = !AppSettings.ResumeLatestSave;
            Refresh();
        }

        void TogglePickerInAR()
        {
            AppSettings.PickerInAR = !AppSettings.PickerInAR;
            Refresh();
        }

        void Refresh()
        {
            string controller = GamepadInput.ControllerName;
            controllerStatus.text = controller != null ? $"Connected: {controller}{PowerLine()}" : noControllerText;

            switch (AppSettings.TouchControls)
            {
                case TouchControlsMode.Auto:
                    touchControlsValue.text = "AUTO";
                    touchControlsDetail.text = "Hidden while a controller is connected";
                    break;
                case TouchControlsMode.Always:
                    touchControlsValue.text = "ALWAYS";
                    touchControlsDetail.text = "Always shown, even with a controller";
                    break;
                default:
                    touchControlsValue.text = "HIDDEN";
                    touchControlsDetail.text = "Never shown - play with a controller";
                    break;
            }
            resumeValue.text = AppSettings.ResumeLatestSave ? "ON" : "OFF";
            pickerInARValue.text = AppSettings.PickerInAR ? "ON" : "OFF";
            spacingSlider.SetValueWithoutNotify(AppSettings.LayerSpacingScale);
            ShowSpacing(AppSettings.LayerSpacingScale);
            windowGroup.alpha = 1f;
        }
    }
}
