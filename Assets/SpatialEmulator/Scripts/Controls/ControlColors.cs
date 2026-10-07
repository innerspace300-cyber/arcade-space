// ControlColors.cs — the on-screen controls' colours, picked in Settings >
// COLORS: the joystick's knob (its ring and arrows in a colour to go with
// it - a black knob gets a neon green ring) and the action buttons (A, B and C together),
// each CLASSIC (the art as drawn: a red knob, red / yellow / green buttons)
// or neon green, pink, orange, purple, or black. The colours are recoloured
// copies of the red art (Resources/ControlColors, same shading), swapped in
// at run time; the choice is kept (PlayerPrefs).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.Controls
{
    public static class ControlColors
    {
        public static readonly string[] Names = { "CLASSIC", "NEON GREEN", "NEON PINK", "NEON ORANGE", "NEON PURPLE", "BLACK" };
        static readonly string[] Files = { null, "Green", "Pink", "Orange", "Purple", "Black" };
        /// Each colour as a swatch (CLASSIC: the red).
        public static readonly Color[] Swatches =
        {
            new Color32(232, 40, 58, 255), new Color32(57, 255, 60, 255), new Color32(255, 40, 200, 255),
            new Color32(255, 128, 24, 255), new Color32(170, 64, 255, 255), new Color32(64, 62, 78, 255),
        };

        const string JoystickKey = "ARcade.JoystickColor", ButtonsKey = "ARcade.ButtonColor";

        public static int Joystick
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(JoystickKey, 0), 0, Names.Length - 1);
            set { PlayerPrefs.SetInt(JoystickKey, value); PlayerPrefs.Save(); Apply(); }
        }

        public static int Buttons
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ButtonsKey, 0), 0, Names.Length - 1);
            set { PlayerPrefs.SetInt(ButtonsKey, value); PlayerPrefs.Save(); Apply(); }
        }

        // The art as drawn, the first time each control is seen.
        static readonly Dictionary<ArcadeButton, (Sprite normal, Sprite pressed)> s_buttons = new Dictionary<ArcadeButton, (Sprite, Sprite)>();
        static readonly Dictionary<Image, Sprite> s_knobs = new Dictionary<Image, Sprite>();
        static readonly Dictionary<Image, Sprite> s_padArt = new Dictionary<Image, Sprite>();
        // The joystick's ring and arrow art -> their coloured copies' names.
        static readonly Dictionary<string, string> PadArt = new Dictionary<string, string>
            { { "JoyRing", "Ring" }, { "JoyArrowUp", "ArrowUp" }, { "JoyArrowRight", "ArrowRight" } };

        /// The controls' own art (CLASSIC): the knob, and the A button.
        public static Sprite ArtKnob
        {
            get { foreach (var art in s_knobs.Values) return art; return null; }
        }
        public static Sprite ArtButton
        {
            get { foreach (var pair in s_buttons) if (pair.Key && pair.Key.button == RetroPadButton.B) return pair.Value.normal; return null; }
        }

        /// The knob in colour `index` (null: the art as drawn).
        public static Sprite Knob(int index) => Files[index] == null ? null : Resources.Load<Sprite>("ControlColors/Knob" + Files[index]);
        /// A button in colour `index`, up or held, in the shade for button A
        /// (deep), B (the colour itself) or C (light) - so they tell apart
        /// when they're all one colour (null: the art as drawn).
        public static Sprite Button(int index, bool pressed = false, char shade = 'B')
            => Files[index] == null ? null : Resources.Load<Sprite>("ControlColors/Button" + (pressed ? "Pressed" : "") + shade + Files[index]);

        /// The colours were changed (or put on the controls).
        public static event System.Action Changed;

        /// Puts the chosen colours on the controls in the scene.
        public static void Apply()
        {
            ApplyToControls();
            Changed?.Invoke();
        }

        static void ApplyToControls()
        {
            int buttons = Buttons, joystick = Joystick;
            foreach (var b in Object.FindObjectsByType<ArcadeButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (b.button != RetroPadButton.B && b.button != RetroPadButton.A && b.button != RetroPadButton.Y) continue;
                if (!s_buttons.ContainsKey(b))
                {
                    var image = b.visual as Image;
                    var normal = b.NormalSprite ? b.NormalSprite : image ? image.sprite : null;
                    s_buttons[b] = (normal, b.pressedSprite);
                }
                var art = s_buttons[b];
                char shade = b.button == RetroPadButton.B ? 'A' : b.button == RetroPadButton.A ? 'B' : 'C';   // the A, B and C buttons
                b.SetLook(Button(buttons, false, shade) ?? art.normal, Button(buttons, true, shade) ?? art.pressed);
            }
            foreach (var pad in Object.FindObjectsByType<ArcadeDPad>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var knob = pad.knob ? pad.knob.GetComponent<Image>() : null;
                if (knob)
                {
                    if (!s_knobs.ContainsKey(knob)) s_knobs[knob] = knob.sprite;
                    knob.sprite = Knob(joystick) ?? s_knobs[knob];
                }
                // The ring and the arrows (down and left are the up and right art, flipped).
                foreach (var image in pad.GetComponentsInChildren<Image>(true))
                {
                    if (image == knob) continue;
                    if (!s_padArt.ContainsKey(image))
                    {
                        if (!image.sprite || !PadArt.ContainsKey(image.sprite.name)) continue;
                        s_padArt[image] = image.sprite;
                    }
                    var art = s_padArt[image];
                    var colored = Files[joystick] == null ? null : Resources.Load<Sprite>("ControlColors/" + PadArt[art.name] + Files[joystick]);
                    image.sprite = colored ? colored : art;
                }
            }
        }
    }
}
