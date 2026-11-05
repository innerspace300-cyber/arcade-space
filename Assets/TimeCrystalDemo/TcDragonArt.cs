using System.Collections.Generic;
using UnityEngine;

namespace TimeCrystal
{
    // BLAZE's twin Chinese dragons, pixel art made here, red and gold (TcDragonPair): a head seen from above,
    // snout up (gold horns sweeping back, long gold whiskers, yellow eyes, white fangs, a gold mane), a body
    // segment (red scales, scalloped darker, a gold spine down its back; drawn pointing the way it goes), a
    // claw, and the tail's gold tuft. Each is drawn as half a picture and mirrored.
    public static class TcDragonArt
    {
        public const float ArtPixel = 2f / TcArt.PixelsPerUnit;   // (the ships' 2x pixels)

        // The look (Wayne's red-and-gold dragon): glossy red scales, a bright yellow belly band, pale cream mane,
        // horns, whiskers and tail tuft.
        // Three looks, a bomb each in turn (TcDragonPair): red and gold; aqua and pink; neon blue and white.
        // (Red*: the scales; Belly*: the belly band and brows; Cream*: the mane, horns, whiskers and tail tuft.)
        public const int Looks = 3;
        struct Palette { public Color red, redLight, redDark, belly, bellyDark, cream, creamShade; }
        static readonly Palette[] Palettes =
        {
            new Palette { red = new Color(0.9f, 0.13f, 0.1f), redLight = new Color(1f, 0.42f, 0.36f), redDark = new Color(0.58f, 0.05f, 0.05f),
                          belly = new Color(1f, 0.84f, 0.18f), bellyDark = new Color(0.86f, 0.58f, 0.08f),
                          cream = new Color(1f, 0.95f, 0.74f), creamShade = new Color(0.93f, 0.8f, 0.5f) },
            new Palette { red = new Color(0.25f, 0.95f, 0.2f), redLight = new Color(0.68f, 1f, 0.5f), redDark = new Color(0.05f, 0.5f, 0.12f),
                          belly = new Color(1f, 0.8f, 0.15f), bellyDark = new Color(0.82f, 0.55f, 0.06f),
                          cream = new Color(1f, 0.93f, 0.6f), creamShade = new Color(0.92f, 0.76f, 0.35f) },
            new Palette { red = new Color(0.15f, 0.5f, 1f), redLight = new Color(0.55f, 0.8f, 1f), redDark = new Color(0.06f, 0.2f, 0.62f),
                          belly = new Color(0.96f, 0.98f, 1f), bellyDark = new Color(0.72f, 0.8f, 0.95f),
                          cream = new Color(1f, 1f, 1f), creamShade = new Color(0.78f, 0.88f, 1f) },
        };
        static Color Red, RedLight, RedDark, Belly, BellyDark, Cream, CreamShade;
        static int s_look = -1;

        /// Which look the sprites below are drawn in (each look's drawn once, then kept).
        public static int Look
        {
            get => Mathf.Max(0, s_look);
            set
            {
                int v = ((value % Looks) + Looks) % Looks;
                if (v == s_look) return;
                s_look = v;
                var p = Palettes[v];
                Red = p.red; RedLight = p.redLight; RedDark = p.redDark; Belly = p.belly; BellyDark = p.bellyDark; Cream = p.cream; CreamShade = p.creamShade;
            }
        }
        static void Ready() { if (s_look < 0) Look = 0; }

        static Sprite s_segment;
        static readonly Sprite[] s_head = new Sprite[Looks], s_claw = new Sprite[Looks], s_tuft = new Sprite[Looks], s_plate = new Sprite[Looks];

        // . clear  K outline  R red  r dark red  O mouth  W fang  G gold  g dark gold  Y eye  P pupil
        static Color Of(char c) => c switch
        {
            'K' => new Color(0.18f, 0.03f, 0.04f),
            'R' => new Color(0.86f, 0.13f, 0.1f),
            'r' => new Color(0.56f, 0.06f, 0.07f),
            'O' => new Color(1f, 0.45f, 0.1f),
            'W' => new Color(1f, 0.97f, 0.9f),
            'G' => new Color(1f, 0.8f, 0.22f),
            'g' => new Color(0.82f, 0.48f, 0.08f),
            'Y' => new Color(1f, 0.95f, 0.35f),
            'P' => new Color(0.1f, 0.02f, 0.02f),
            _ => Color.clear,
        };

        // Half a head (left side, then its middle column), snout at the top.
        // Half a body segment (pointing up): an overlapping scale plate - coral round its sides, gold-tipped
        // pointed scales across its top and a spike on the spine (the body's tinted coral to gold along its length).
        static readonly string[] SegmentHalf =
        {
            "........|K",
            ".......K|G",
            "...K..KG|G",
            "..KGK.KR|g",
            ".KRGRKRR|K",
            ".KRRRRGR|G",
            "KRRrRRRG|G",
            "KRRRRrRR|R",
            "KrRRRRRr|R",
            ".KrRrRRR|R",
            "..KKrRRr|R",
            "....KKKK|K",
        };

        static readonly string[] TuftHalf = { "..G|.", ".GG|G", "GgG|G", ".GG|g", "G.G|G", "..G|G", ".G.|G", "...|G" };

        public static Sprite Head { get { Ready(); return s_head[Look] ? s_head[Look] : (s_head[Look] = DrawHead()); } }
        public static Sprite Segment => s_segment ? s_segment : (s_segment = Build(SegmentHalf, 0.5f));
        public static Sprite Claw { get { Ready(); return s_claw[Look] ? s_claw[Look] : (s_claw[Look] = DrawClaw()); } }
        public static Sprite Tuft { get { Ready(); return s_tuft[Look] ? s_tuft[Look] : (s_tuft[Look] = DrawTuft()); } }

        // ---- Drawn from shapes: the head (seen from above, snout up) and the claws ----
        // A canvas of colours; shapes filled into it; then a dark outline round everything solid.
        const float FinePixel = ArtPixel * 0.55f;   // (the head and claws: finer pixels, so their shapes read)

        class Canvas
        {
            public readonly int w, h;
            public readonly Color[] px;
            public Canvas(int w, int h) { this.w = w; this.h = h; px = new Color[w * h]; }
            public bool In(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;
            public void Set(int x, int y, Color c) { if (In(x, y)) px[y * w + x] = c; }
            public Color Get(int x, int y) => In(x, y) ? px[y * w + x] : Color.clear;
            // A filled triangle.
            public void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
            {
                int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
                int y0 = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), y1 = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
                        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                        if (!(neg && pos)) Set(x, y, col);
                    }
            }
            static float Cross(Vector2 p, Vector2 a, Vector2 b) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            // A curve through points, `w0` thick at its start tapering to `w1`.
            public void Stroke(Vector2[] pts, float w0, float w1, Color col)
            {
                float total = 0f; for (int i = 1; i < pts.Length; i++) total += Vector2.Distance(pts[i - 1], pts[i]);
                float walked = 0f;
                for (int i = 1; i < pts.Length; i++)
                {
                    float seg = Vector2.Distance(pts[i - 1], pts[i]);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(seg * 3f));
                    for (int k = 0; k <= steps; k++)
                    {
                        float u = (walked + seg * k / steps) / Mathf.Max(0.001f, total);
                        var p = Vector2.Lerp(pts[i - 1], pts[i], k / (float)steps);
                        float r = Mathf.Lerp(w0, w1, u) * 0.5f;
                        for (int y = Mathf.FloorToInt(p.y - r); y <= Mathf.CeilToInt(p.y + r); y++)
                            for (int x = Mathf.FloorToInt(p.x - r); x <= Mathf.CeilToInt(p.x + r); x++)
                                if ((x + 0.5f - p.x) * (x + 0.5f - p.x) + (y + 0.5f - p.y) * (y + 0.5f - p.y) <= r * r + 0.25f) Set(x, y, col);
                    }
                    walked += seg;
                }
            }
            // The outline: every empty pixel touching something solid goes dark - round the shapes, not over
            // them, so thin lines (whiskers, horn tips, talons) keep their colour.
            public void Outline(Color col)
            {
                var edge = new bool[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (px[y * w + x].a == 0f && (Get(x - 1, y).a > 0f || Get(x + 1, y).a > 0f || Get(x, y - 1).a > 0f || Get(x, y + 1).a > 0f)) edge[y * w + x] = true;
                for (int i = 0; i < edge.Length; i++) if (edge[i]) px[i] = col;
            }
            public Sprite Sprite(Vector2 pivot)
            {
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                tex.SetPixels(px); tex.Apply();
                return UnityEngine.Sprite.Create(tex, new Rect(0, 0, w, h), pivot, 1f / FinePixel);
            }
        }

        // The head, snout up, as from above (like the articulated dragon's): a long face tapering to a narrow
        // nose, broad at the back; gold brows over the eyes, gold along the nose; two antlers at the back of the
        // head pointing back along the body, each with a branch; long wavy whiskers from beside the nose trailing
        // back alongside. Coral red, darker down its sides.
        static Sprite DrawHead()
        {
            const int W = 57, H = 104, cx = 28;
            var c = new Canvas(W, H);
            Color coral = Red, side = RedDark, gold = Belly, goldDark = CreamShade, horn = Cream;
            int top = H - 3;
            // Half-width down from the nose (v = 0 at its front).
            float Half(float v)
            {
                if (v < 10f) return 3f + 3.6f * Mathf.Sqrt(v / 10f);                // (the nose: rounded, curving out)
                if (v < 30f) return Mathf.Lerp(6.6f, 9.5f, Mathf.SmoothStep(0f, 1f, (v - 10f) / 20f));   // (the face, curving wider)
                if (v < 38f) return Mathf.Lerp(9.5f, 10.5f, (v - 30f) / 8f);       // (the back of the head)
                if (v < 44f) return Mathf.Lerp(10.5f, 7.5f, (v - 38f) / 6f);
                return 7.5f;                                                        // (the neck)
            }
            const float HeadEnd = 54f;
            for (int y = 0; y <= top; y++)
            {
                float v = top - y;
                if (v > HeadEnd) continue;   // (below the neck: only the whiskers and antlers)
                float half = Half(v);
                for (int x = 0; x < W; x++)
                {
                    float dx = Mathf.Abs(x - cx);
                    if (dx > half) continue;
                    var col = dx > half - 2f ? side : coral;
                    if (x < cx && dx > 1f && dx < half - 2f) col = Color.Lerp(coral, Color.white, 0.1f);   // (light from the left)
                    c.Set(x, y, col);
                }
            }
            // Scales over the head behind the nose: rows of little darker arcs, offset row to row.
            for (int y = 0; y <= top; y++)
            {
                float v = top - y;
                if (v < 12f || v > 46f) continue;
                int row = Mathf.FloorToInt(v / 3f);
                for (int x = 0; x < W; x++)
                {
                    if (c.Get(x, y) != coral && c.Get(x, y) != Color.Lerp(coral, Color.white, 0.1f)) continue;
                    float sx = (x + (row % 2) * 2f) % 4f;
                    if (Mathf.RoundToInt(v) % 3 == 0 && (sx < 1f || sx > 3f)) c.Set(x, y, side);
                }
            }
            // Yellow down the bridge of the nose.
            c.Stroke(new[] { new Vector2(cx, top - 4f), new Vector2(cx, top - 19f) }, 2f, 1.2f, gold);
            foreach (int sd in new[] { -1, 1 })
            {
                // Nostrils: big, round, near the nose's front.
                for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) c.Set(cx + sd * 2 + (sd > 0 ? x : -x), top - 3 - y, Of('K'));
                c.Set(cx + sd * 4, top - 4, Of('K'));
                // Eyebrows: thick and yellow, swept up and back.
                c.Stroke(new[] { new Vector2(cx + sd * 2f, top - 19f), new Vector2(cx + sd * 6f, top - 21.5f), new Vector2(cx + sd * 10f, top - 24f) }, 3.2f, 2f, Belly);
                // Eyes: big - white round a yellow iris, a dark slit.
                int ex = cx + sd * 6, ey = top - 25;
                for (int y = 0; y < 3; y++) for (int x = -1; x <= 2; x++) c.Set(ex + x * sd, ey - y, x == -1 || x == 2 ? Color.white : Of('Y'));
                c.Set(ex, ey, Of('P')); c.Set(ex, ey - 1, Of('P')); c.Set(ex, ey - 2, Of('P'));
            }
            // Antlers: from the back of the head, pointing back along the body (down), a branch out from each.
            foreach (int sd in new[] { -1, 1 })
            {
                var root = new Vector2(cx + sd * 6f, top - 36f);
                c.Stroke(new[] { root, root + new Vector2(sd * 2f, -8f), root + new Vector2(sd * 3f, -18f), root + new Vector2(sd * 2f, -26f) }, 3.6f, 1.2f, horn);
                c.Stroke(new[] { root + new Vector2(sd * 2.5f, -9f), root + new Vector2(sd * 7f, -12f), root + new Vector2(sd * 9f, -10f) }, 2.2f, 1f, horn);
            }
            // Whiskers: from beside the nose, out a little, then long and wavy back alongside the head and body.
            foreach (int sd in new[] { -1, 1 })
            {
                var p = new Vector2(cx + sd * 3.5f, top - 4f);
                var pts = new List<Vector2> { p };
                p += new Vector2(sd * 4f, -1f); pts.Add(p);
                p += new Vector2(sd * 5f, -4f); pts.Add(p);
                for (int s2 = 1; s2 <= 40; s2++)
                {
                    float y = top - 9f - s2 * 2.2f;
                    pts.Add(new Vector2(cx + sd * (13f + 3.2f * Mathf.Sin(s2 * 0.55f)), y));
                }
                c.Stroke(pts.ToArray(), 1.6f, 1f, Cream);
            }
            // A cream mane flowing back from the back of the head, wisps either side.
            foreach (int sd in new[] { -1, 1 })
                for (int k = 0; k < 4; k++)
                {
                    var root = new Vector2(cx + sd * (7f + k * 0.8f), top - (34f + k * 4f));
                    c.Stroke(new[] { root, root + new Vector2(sd * 4f, -5f), root + new Vector2(sd * 6f, -11f), root + new Vector2(sd * 5f, -16f) }, 2.6f, 0.8f, k % 2 == 0 ? Cream : CreamShade);
                }
            c.Outline(Of('K'));
            return c.Sprite(new Vector2(0.5f, (H - 3f - 26f) / H));   // (pivot at the eyes)
        }

        // An arm and claw, pointing up (out from the body): a big muscular red arm - the upper arm straight out
        // with a bulging bicep, the elbow, then the forearm bent 45 degrees towards +x (the sprite's mirrored for
        // the other way: TcDragonPair), scaled darker red, lit along the muscle - then a small red foot spread open
        // like an eagle's: four toes fanned out, none crossing, each with a curved ivory talon.
        static Sprite DrawClaw()
        {
            const int W = 41, H = 47, cx = 12;
            var c = new Canvas(W, H);
            Color ivory = new Color(1f, 0.97f, 0.88f), ivoryShade = new Color(0.82f, 0.76f, 0.62f);
            Color red = Red, redLight = RedLight, redDark = RedDark;
            Vector2 Rot(Vector2 v, float deg) { float r = deg * Mathf.Deg2Rad; return new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r)); }
            var shoulder = new Vector2(cx, 0f);
            var elbow = new Vector2(cx, 15f);
            var fore = Rot(Vector2.up, -45f);                 // (the forearm: 45 degrees over towards +x)
            var wrist = elbow + fore * 12f;
            // The upper arm, a bulging bicep on it; the forearm, swelling then tapering.
            c.Stroke(new[] { shoulder, elbow }, 9f, 7.5f, red);
            c.Stroke(new[] { new Vector2(cx - 0.8f, 6f), new Vector2(cx - 0.8f, 10f) }, 10.5f, 10f, red);
            c.Stroke(new[] { elbow, elbow + fore * 5f, wrist }, 7.5f, 5f, red);
            c.Stroke(new[] { elbow + fore * 3.5f, elbow + fore * 6f }, 8.5f, 7.5f, red);
            // Light along the muscle's outer side.
            c.Stroke(new[] { new Vector2(cx - 3.5f, 1f), new Vector2(cx - 4.6f, 8f), new Vector2(cx - 3f, 14f) }, 1.6f, 1.2f, redLight);
            c.Stroke(new[] { elbow + Rot(fore, 90f) * 3f, elbow + fore * 7f + Rot(fore, 90f) * 3f }, 1.4f, 1f, redLight);
            // Scales: little dark marks in rows over the arm.
            foreach (var at in new[] { 2f, 5f, 8f, 11f, 14f })
                for (int x = -3; x <= 3; x += 3)
                {
                    int sx = cx + x + ((int)at % 2), sy = (int)at;
                    if (c.Get(sx, sy) == red) { c.Set(sx, sy, redDark); c.Set(sx + 1, sy, redDark); }
                }
            for (float u = 2f; u < 11f; u += 3f)
                foreach (float o in new[] { -2f, 1f })
                {
                    var p = elbow + fore * u + Rot(fore, 90f) * o;
                    int sx = Mathf.RoundToInt(p.x), sy = Mathf.RoundToInt(p.y);
                    if (c.Get(sx, sy) == red) c.Set(sx, sy, redDark);
                }
            // The foot, then the toes fanned open along the forearm's way - straight, spread, never crossing -
            // each with a talon hooking the same way (curved, like an eagle's).
            c.Stroke(new[] { wrist, wrist + fore * 1.5f }, 5.5f, 5.5f, red);
            var palm = wrist + fore * 2f;
            foreach (var (deg, len) in new[] { (-62f, 5.5f), (-22f, 7f), (18f, 7f), (58f, 6f) })
            {
                var dir = Rot(fore, -deg);
                var p1 = palm + dir * 1.5f;
                var p2 = p1 + dir * len;
                c.Stroke(new[] { p1, p2 }, 2.8f, 2.1f, red);
                c.Set(Mathf.RoundToInt((p1 + p2).x * 0.5f), Mathf.RoundToInt((p1 + p2).y * 0.5f), redDark);   // (a knuckle)
                var hook = Rot(dir, 90f);
                var t1 = p2 + dir * 1.8f;
                var t2 = t1 + dir * 0.8f + hook * 1.4f;
                c.Stroke(new[] { p2, t1, t2 }, 1.9f, 0.7f, ivory);
                c.Set(Mathf.RoundToInt(t1.x + hook.x * 0.7f), Mathf.RoundToInt(t1.y + hook.y * 0.7f), ivoryShade);
            }
            c.Outline(Of('K'));
            return c.Sprite(new Vector2((cx + 0.5f) / W, 0.01f));
        }

        // A body plate (pointing up): glossy red, scaled (rows of darker arcs, lit at the top left), a bright
        // yellow belly band down its left side (banded), and cream mane wisps flowing back off the spine.
        public static Sprite Plate(float k)
        {
            Ready();
            if (s_plate[Look]) return s_plate[Look];
            const int W = 23, H = 19;
            var c = new Canvas(W, H);
            float cx = 11f, cy = 8f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f - cx) / 9.5f, dy = (y + 0.5f - cy) / 7f;
                    if (dx * dx + dy * dy > 1f) continue;
                    var col = Red;
                    if (dx < -0.25f && dy > 0.1f) col = Color.Lerp(Red, RedLight, 0.6f);   // (the gloss, top left)
                    if (dy < -0.45f) col = RedDark;
                    // Scales: arcs in rows, offset each row.
                    int row = Mathf.FloorToInt((y + 0.5f) / 3f);
                    float sx = (x + (row % 2) * 2f) % 4f;
                    if ((y % 3 == 0) && (sx < 1f || sx > 3f)) col = RedDark;
                    // The belly band down the left side, banded.
                    if (dx < -0.62f) col = (y % 3 == 1) ? BellyDark : Belly;
                    c.Set(x, y, col);
                }
            // Cream mane wisps off the spine, flowing back (down) and out a little.
            c.Stroke(new[] { new Vector2(cx, 15f), new Vector2(cx + 2f, 10f), new Vector2(cx + 4.5f, 4f), new Vector2(cx + 5f, 0.5f) }, 2.6f, 0.8f, Cream);
            c.Stroke(new[] { new Vector2(cx - 1f, 14f), new Vector2(cx + 0f, 8f), new Vector2(cx + 1.5f, 2f) }, 2.2f, 0.8f, CreamShade);
            c.Outline(Of('K'));
            return s_plate[Look] = c.Sprite(new Vector2(0.5f, 0.5f));
        }

        // The tail's tuft: cream wisps fanning back from the tail's end.
        static Sprite DrawTuft()
        {
            const int W = 31, H = 29, cx = 15;
            var c = new Canvas(W, H);
            foreach (var (deg, len) in new[] { (-38f, 18f), (-18f, 23f), (0f, 25f), (18f, 22f), (38f, 17f) })
            {
                float a = deg * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));    // (out from the tail's end: it's laid pointing on back)
                var bend = new Vector2(-dir.y, dir.x) * 2.5f;
                var p0 = new Vector2(cx, 2f);
                c.Stroke(new[] { p0, p0 + dir * len * 0.5f + bend * 0.5f, p0 + dir * len + bend }, 3.2f, 0.8f, deg == 0f || Mathf.Abs(deg) > 30f ? Cream : CreamShade);
            }
            c.Outline(Of('K'));
            return c.Sprite(new Vector2(0.5f, 2f / H));
        }

        // Rows top to bottom, each "left|middle": mirrored into a full row.
        static Sprite Build(string[] half, float pivotY)
        {
            int left = half[0].IndexOf('|');
            int w = left * 2 + 1, h = half.Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int row = 0; row < h; row++)
            {
                var parts = half[row].Split('|');
                string l = parts[0].PadRight(left, '.');
                char m = parts.Length > 1 && parts[1].Length > 0 ? parts[1][0] : '.';
                int y = h - 1 - row;
                for (int x = 0; x < left; x++)
                {
                    var c = Of(l[x]);
                    tex.SetPixel(x, y, c);
                    tex.SetPixel(w - 1 - x, y, c);
                }
                tex.SetPixel(left, y, Of(m));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, pivotY), 1f / ArtPixel);
        }
    }
}
