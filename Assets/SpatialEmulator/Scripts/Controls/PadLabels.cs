// PadLabels.cs — what a controller the phone sits in (a Razer Kishi V2: TvOutput.GripOn) calls its buttons, for
// anything on screen that names one: its X, Y, B and A, each drawn as on the controller (black, a white letter, a
// white edge so it shows on the dark boxes), its shoulders LB and RB, and ... for the button under its left stick.
// ENDLESS KNIGHT's three (GamepadInput): X light attack, A jump, B magic.

using SpatialEmulator.UI;
using UnityEngine;

namespace SpatialEmulator.Controls
{
    public static class PadLabels
    {
        public const int ButtonPixels = 9;
        const float FontPixel = 0.00625f;   // (RestPrompt's: one of the pixel font's pixels, unscaled)

        /// A Kishi's on: its labels, not PlayStation's.
        public static bool Kishi => TvOutput.GripOn;

        public static string Coin => Kishi ? "LB" : "L1";
        public static string Start => Kishi ? "RB" : "R1";
        public static string Select => Kishi ? "..." : "Create/View";

        static readonly string[] Disc =
        {
            "..WWWWW..",
            ".WBBBBBW.",
            "WBBBBBBBW",
            "WBBBBBBBW",
            "WBBBBBBBW",
            "WBBBBBBBW",
            "WBBBBBBBW",
            ".WBBBBBW.",
            "..WWWWW..",
        };

        static string[] Letter(char letter)
        {
            switch (letter)
            {
                case 'A': return new[] { ".###.", "#...#", "#####", "#...#", "#...#" };
                case 'B': return new[] { "####.", "#...#", "####.", "#...#", "####." };
                case 'X': return new[] { "#...#", ".#.#.", "..#..", ".#.#.", "#...#" };
                default: return new[] { "#...#", ".#.#.", "..#..", "..#..", "..#.." };   // (Y)
            }
        }

        static readonly System.Collections.Generic.Dictionary<char, Sprite> s_buttons = new System.Collections.Generic.Dictionary<char, Sprite>();

        /// The button with this letter (X, Y, B or A), ButtonPixels across, its foot at the line as the font's letters stand.
        public static Sprite Button(char letter)
        {
            if (s_buttons.TryGetValue(letter, out var sprite) && sprite) return sprite;
            int n = ButtonPixels;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            var white = new Color32(255, 255, 255, 255);
            var black = new Color32(0, 0, 0, 255);
            var rows = Letter(letter);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    char c = Disc[y][x];
                    bool ink = y >= 2 && y < 7 && x >= 2 && x < 7 && rows[y - 2][x - 2] == '#';
                    px[(n - 1 - y) * n + x] = c == '.' ? new Color32(0, 0, 0, 0) : c == 'W' || ink ? white : black;
                }
            tex.SetPixels32(px);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0f), 1f / FontPixel);
            s_buttons[letter] = sprite;
            return sprite;
        }
    }
}
