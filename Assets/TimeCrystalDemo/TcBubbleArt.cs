using UnityEngine;

namespace TimeCrystal
{
    // A power-up's soap bubble, pixel art made here: a thin white rim (grey here and there), rainbow film just
    // inside it (pink, magenta, blue, cyan, yellow) slowly drifting round, glossy streaks - white, blue and
    // magenta up its top left, green low on its right - and clear in the middle for the power-up inside.
    // Frames is the drift (TcPowerUp plays it); sized like the pack bubble it replaces.
    public static class TcBubbleArt
    {
        const int Size = 30, FrameCount = 8;
        const float WorldSize = 1.3f;   // (the old bubble's 1.44 with its padding: about the same bubble)
        static Sprite[] s_frames;

        static readonly Color White = new Color(1f, 1f, 1f, 1f), Grey = new Color(0.62f, 0.62f, 0.68f, 1f);
        // The film's colours round the rim, from its right (0 degrees) anticlockwise.
        static readonly (float deg, Color c)[] Film =
        {
            (20f, new Color(1f, 0.55f, 0.8f)), (110f, new Color(0.85f, 0.2f, 0.95f)), (160f, new Color(0.25f, 0.4f, 1f)),
            (205f, new Color(0.3f, 0.95f, 1f)), (270f, new Color(1f, 0.95f, 0.45f)), (320f, new Color(0.2f, 0.45f, 1f)),
        };

        public static Sprite[] Frames
        {
            get
            {
                if (s_frames != null && s_frames.Length > 0 && s_frames[0]) return s_frames;
                s_frames = new Sprite[FrameCount];
                for (int i = 0; i < FrameCount; i++) s_frames[i] = Make(i * 360f / FrameCount * 0.25f);
                return s_frames;
            }
        }

        static Sprite Make(float drift)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[Size * Size];
            float c = (Size - 1) * 0.5f, R = c - 0.5f;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float dx = x - c, dy = y - c, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float deg = Mathf.Repeat(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, 360f);
                    Color col = Color.clear;
                    if (r > R - 0.9f && r <= R + 0.3f)
                    {
                        // The rim: white, a few greyed breaks.
                        col = (deg > 60f && deg < 75f) || (deg > 288f && deg < 300f) ? Grey : White;
                    }
                    else if (r > R - 3.2f && r <= R - 0.9f)
                    {
                        // The film inside the rim, in arcs that come and go round it.
                        var film = FilmAt(Mathf.Repeat(deg - drift, 360f), out float strength);
                        if (strength > 0.45f) col = film;
                    }
                    px[y * Size + x] = col;
                }
            // Glossy streaks: a slanting white-blue-magenta stroke at the top left, a green one low right, a few specks.
            Stroke(px, c, 125f, 0.45f, 0.8f, new[] { new Color(0.85f, 0.2f, 0.95f), new Color(0.25f, 0.45f, 1f), White, White });
            Stroke(px, c, 140f, 0.32f, 0.55f, new[] { new Color(0.3f, 0.95f, 1f), White });
            Stroke(px, c, -25f, 0.12f, 0.42f, new[] { new Color(0.3f, 1f, 0.6f), new Color(0.3f, 1f, 0.6f), White });
            Speck(px, c, 115f, 0.62f); Speck(px, c, 108f, 0.68f); Speck(px, c, 165f, 0.62f); Speck(px, c, -10f, 0.5f);
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size / WorldSize);
        }

        static Color FilmAt(float deg, out float strength)
        {
            // Nearest film colour round the rim; strongest at its own angle, fading between.
            float best = 999f; Color col = White;
            foreach (var (d, c) in Film)
            {
                float off = Mathf.Abs(Mathf.DeltaAngle(deg, d));
                if (off < best) { best = off; col = c; }
            }
            strength = 1f - best / 28f;
            return col;
        }

        // A short stroke across the bubble at `deg`, from `r0` to `r1` (fractions of its radius), its colours along it.
        static void Stroke(Color[] px, float c, float deg, float r0, float r1, Color[] colours)
        {
            float a = deg * Mathf.Deg2Rad, R = c - 0.5f;
            int steps = Mathf.CeilToInt((r1 - r0) * R * 2f);
            var along = new Vector2(Mathf.Cos(a + 1.1f), Mathf.Sin(a + 1.1f));   // (slanted across the radius)
            var at = new Vector2(c + Mathf.Cos(a) * R * r0, c + Mathf.Sin(a) * R * r0);
            var to = new Vector2(c + Mathf.Cos(a) * R * r1, c + Mathf.Sin(a) * R * r1) + along * 2f;
            for (int i = 0; i <= steps; i++)
            {
                var p = Vector2.Lerp(at, to, i / (float)steps);
                var col = colours[Mathf.Min(colours.Length - 1, i * colours.Length / (steps + 1))];
                Put(px, p, col);
                Put(px, p + along * 0.8f, col);   // (two pixels thick)
            }
        }

        static void Speck(Color[] px, float c, float deg, float r)
        {
            float a = deg * Mathf.Deg2Rad, R = c - 0.5f;
            Put(px, new Vector2(c + Mathf.Cos(a) * R * r, c + Mathf.Sin(a) * R * r), White);
        }

        static void Put(Color[] px, Vector2 p, Color col)
        {
            int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            px[y * Size + x] = col;
        }
    }
}
