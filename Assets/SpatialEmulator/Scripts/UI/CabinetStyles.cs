// CabinetStyles.cs — the cabinet's frame colours, picked in Settings >
// COLORS: CLASSIC (purple with aqua gems, as drawn), AQUA & PINK, BLACK &
// ORANGE or GREEN & BLACK. Each is a recoloured copy of the frame, crest and
// checker art (Resources/CabinetStyles, same pixels), swapped onto every
// ScreenFrame as it appears and when the choice changes (PlayerPrefs). The
// style carries on to the rest of the cabinet: its side panel's border and
// keycaps (and LOCK's; Restyle), and the demo's dialogue boxes - their lines
// tinted in the style's colours (DialogLine, DialogAccent).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public static class CabinetStyles
    {
        public static readonly string[] Names = { "CLASSIC", "AQUA & PINK", "BLACK & ORANGE", "GREEN & BLACK" };
        static readonly string[] Files = { "Classic", "AquaPink", "BlackOrange", "GreenBlack" };
        /// Each style's main and accent colour, for its name in Settings.
        public static readonly Color[] Swatches =
        {
            new Color32(182, 151, 239, 255), new Color32(0, 220, 255, 255), new Color32(255, 122, 24, 255), new Color32(57, 255, 60, 255),
        };

        /// The dialogue boxes' lines (frames, portrait frames, rules)...
        public static readonly Color[] DialogLines =
        {
            new Color32(0, 220, 255, 255), new Color32(0, 220, 255, 255), new Color32(255, 122, 24, 255), new Color32(57, 255, 60, 255),
        };
        /// ...and their arrows and PRESS A.
        public static readonly Color[] DialogAccents =
        {
            new Color32(0, 220, 255, 255), new Color32(255, 46, 200, 255), new Color32(255, 176, 0, 255), new Color32(200, 255, 48, 255),
        };
        /// The side panel's title.
        public static readonly Color[] Titles =
        {
            new Color32(232, 224, 255, 255), new Color32(200, 250, 255, 255), new Color32(255, 200, 154, 255), new Color32(200, 255, 200, 255),
        };
        /// The style's name on its art files (Resources/CabinetStyles/<art><suffix>).
        public static string ArtSuffix => Files[Current];
        public static Color DialogLine => DialogLines[Current];
        public static Color DialogAccent => DialogAccents[Current];
        public static Color Title => Titles[Current];

        /// The style changed (what's open restyles itself).
        public static event System.Action Changed;

        const string Key = "ARcade.CabinetStyle";
        // The panel art that comes in each style (Resources/CabinetStyles/<name><style>).
        static readonly HashSet<string> UiArt = new HashSet<string>
            { "PanelLarge", "ButtonKeyB_Unpressed", "ButtonKeyB_Pressed", "ButtonKeyBPurple_Unpressed", "ButtonKeyBPurple_Pressed" };
        static readonly Dictionary<Image, string> s_uiArt = new Dictionary<Image, string>();
        static readonly Dictionary<Button, string> s_pressedArt = new Dictionary<Button, string>();
        static readonly string[] Parts = { "Frame", "Crest", "Checker" };
        static readonly string[] PartArt = { "ScreenFrame", "ScreenFrameCrest", "CheckerTile" };
        static readonly Dictionary<Image, Sprite> s_art = new Dictionary<Image, Sprite>();

        public static int Current
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, Names.Length - 1);
            set
            {
                PlayerPrefs.SetInt(Key, value);
                PlayerPrefs.Save();
                foreach (var frame in Object.FindObjectsByType<ScreenFrame>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Apply(frame);
                Changed?.Invoke();
            }
        }

        /// The frame art of style `index`, part 0 frame / 1 crest / 2 checker (Classic: copies of the art as drawn).
        public static Sprite Art(int index, int part) => Files[index] == null ? null : Resources.Load<Sprite>($"CabinetStyles/{PartArt[part]}{Files[index]}");

        /// The chosen style on the panel art under `root` (its border and keycaps, up and pressed).
        public static void Restyle(Transform root)
        {
            string style = Files[Current];
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (!s_uiArt.ContainsKey(image))
                {
                    if (!image.sprite || !UiArt.Contains(image.sprite.name)) continue;
                    s_uiArt[image] = image.sprite.name;   // the art as drawn, the first time
                }
                var art = Resources.Load<Sprite>($"CabinetStyles/{s_uiArt[image]}{style}");
                if (art) image.sprite = art;
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.transition != Selectable.Transition.SpriteSwap) continue;
                if (!s_pressedArt.ContainsKey(button))
                {
                    var pressed = button.spriteState.pressedSprite;
                    if (!pressed || !UiArt.Contains(pressed.name)) continue;
                    s_pressedArt[button] = pressed.name;
                }
                var art = Resources.Load<Sprite>($"CabinetStyles/{s_pressedArt[button]}{style}");
                if (!art) continue;
                var state = button.spriteState;
                state.pressedSprite = art;
                button.spriteState = state;
            }
        }

        /// The chosen style on this frame.
        public static void Apply(ScreenFrame frame)
        {
            int style = Current;
            for (int i = 0; i < Parts.Length; i++)
            {
                var image = frame.transform.Find(Parts[i])?.GetComponent<Image>();
                if (!image) continue;
                if (!s_art.ContainsKey(image)) s_art[image] = image.sprite;
                image.sprite = Art(style, i) ?? s_art[image];
            }
        }
    }
}
