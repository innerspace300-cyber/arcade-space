using UnityEngine;

// ARcade: how the road's foes die, in their pixels - bones bursting out
// (thigh bones, ribs, a skull, bits) that fly, tumble, bounce on the road and
// fade; and blood sprayed in arcs that glows (the frogs' green, the
// revenants' neon aqua), splashing down and fading on the road. Each piece
// is one of these, made by Bones / Blood; they stay put on the road as it
// scrolls. And a blast (the bomb guy's explosion, turned to a colour) that
// can hurt the knight (ExplosionDamage).
public class FoeFx : MonoBehaviour
{
    Vector2 _velocity;
    float _spin, _born, _life, _ground, _lastRoad;
    bool _landed, _splat, _feather, _puff, _spark, _ring, _twinkle, _lava;
    Color _hot;
    float _drag = 4f, _fall = 1.5f;
    float _sway, _puffGrow;
    SpriteRenderer _sr, _glow;
    Transform _follow;   // (moving with what it's on - a foe knocked back - not left behind on the road)
    Vector3 _followLast;
    Color _color;
    bool _chrome;   // (the knight's blood: rainbow chrome, like her health bar - ChromeBar)
    float _hueOff;
    static bool s_chrome;

    const float Gravity = 6f;
    static Sprite s_long, s_rib, s_bit, s_skull, s_drop, s_glow, s_splat, s_feather, s_puff, s_ring;

    static Sprite Make(string[] rows, Color32 light, Color32 dark)
    {
        int h = rows.Length, w = rows[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = rows[h - 1 - y][x];
                px[y * w + x] = c == 'L' ? light : c == 'D' ? dark : c == 'K' ? new Color32(30, 24, 28, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
    }

    static void Init()
    {
        if (s_long) return;
        var bone = new Color32(222, 214, 194, 255); var shade = new Color32(150, 142, 126, 255);
        s_long = Make(new[] { "L.....L", "LLLLLLL", "D.....D" }, bone, shade);
        s_rib = Make(new[] { ".LLL.", "L...L", "D...D" }, bone, shade);
        s_bit = Make(new[] { "LL", "LD" }, bone, shade);
        s_skull = Make(new[] { ".LLL.", "LLLLL", "LKLKL", "LLLLD", ".LDL." }, bone, shade);
        var white = new Color32(255, 255, 255, 255);
        s_drop = Make(new[] { "LL", "LL" }, white, white);
        s_splat = Make(new[] { ".LL.", "LLLL" }, white, white);
        var soft = new Color32(205, 215, 230, 255);
        s_feather = Make(new[] { ".LLLL.", "DLLLLL", ".LLL.." }, white, soft);
        s_puff = Make(new[] { "..LLLL..", ".LLLLLL.", "LLLLLLLL", "LLLLLLLL", "LLLLLLLD", "LLLLLLDD", ".DLLLDD.", "..DDDD.." }, white, soft);
        // A soft round glow (for the blood), 8 px.
        const int g = 8;
        var tex = new Texture2D(g, g, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var gp = new Color32[g * g];
        for (int y = 0; y < g; y++)
            for (int x = 0; x < g; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(g / 2f, g / 2f)) / (g / 2f);
                gp[y * g + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) * 255f));
            }
        tex.SetPixels32(gp); tex.Apply();
        s_glow = Sprite.Create(tex, new Rect(0, 0, g, g), new Vector2(0.5f, 0.5f), 1f);
        // A thin ring (a ripple), 32 px across.
        const int r = 32;
        var rt = new Texture2D(r, r, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var rp = new Color32[r * r];
        for (int y = 0; y < r; y++)
            for (int x = 0; x < r; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r / 2f, r / 2f)) / (r / 2f);
                rp[y * r + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) / 0.12f) * 255f));
            }
        rt.SetPixels32(rp); rt.Apply();
        s_ring = Sprite.Create(rt, new Rect(0, 0, r, r), new Vector2(0.5f, 0.5f), 1f);
    }

    /// A burst of magic at `at` in `color`, in pixels: a bright flash, a ring of pixels racing out,
    /// a spray of twinkling pixel sparks (its colour, lighter, white) and a few drifting up.
    public static void MagicBurst(Vector3 at, Color color, float size = 1f, int over = 0)
    {
        Init();
        float px = RoadFoe.Pixel * 1.5f;
        color.a = 1f;
        var light = Color.Lerp(color, Color.white, 0.5f);
        // The flash.
        var flash = Piece(s_glow, at, px * 4f * size, new Color(light.r, light.g, light.b, 0.9f), 0.22f, 47 + over);
        flash._ring = true;
        flash._puffGrow = 6f;
        // The ring: pixels in a circle, all racing out together.
        int ring = 18;
        for (int i = 0; i < ring; i++)
        {
            float a = i / (float)ring * Mathf.PI * 2f;
            var fx = Piece(s_bit, at, px, i % 2 == 0 ? light : color, 0.45f, 46 + over);
            fx._spark = true; fx._drag = 3f; fx._fall = 0f;
            fx._velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.2f * Mathf.Sqrt(size);
        }
        // The spray.
        for (int i = 0; i < 22; i++)
        {
            var c = i % 5 == 0 ? Color.white : i % 2 == 0 ? light : color;
            var fx = Piece(i % 3 == 0 ? s_drop : s_bit, at + (Vector3)Random.insideUnitCircle * px * 2f, px * Random.Range(0.7f, 1.3f), c, Random.Range(0.5f, 0.9f), 45 + over);
            fx._spark = true; fx._twinkle = true; fx._sway = Random.value;
            fx._drag = 3.5f; fx._fall = 0.8f;
            fx._velocity = Random.insideUnitCircle.normalized * Random.Range(0.8f, 2.6f) * Mathf.Sqrt(size);
        }
        // And a few drifting up.
        for (int i = 0; i < 6; i++)
        {
            var fx = Piece(s_bit, at + new Vector3(Random.Range(-1f, 1f) * px * 4f, 0f, 0f), px, light, Random.Range(0.8f, 1.2f), 45 + over);
            fx._spark = true; fx._twinkle = true; fx._sway = Random.value;
            fx._drag = 1f; fx._fall = -0.6f;
            fx._velocity = new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.3f, 0.6f));
        }
    }

    /// One twinkling pixel of magic left at `at` (a magic shot's trail), fading.
    public static void Sparkle(Vector3 at, Color color, float size = 1f)
    {
        Init();
        var fx = Piece(s_bit, at, RoadFoe.Pixel * 1.2f * size, color, Random.Range(0.25f, 0.45f), 44);
        fx._spark = true; fx._twinkle = true; fx._sway = Random.value;
        fx._drag = 2f; fx._fall = -0.2f;
        fx._velocity = Random.insideUnitCircle * 0.2f;
    }

    /// A magic shield struck at `at`: glowing shards flung out (`outward`: away from the blow) and a ripple swelling round the spot.
    public static void ShieldHit(Vector3 at, Color color, Vector2 outward, int count = 12)
    {
        Init();
        float px = RoadFoe.Pixel;
        var bright = Color.Lerp(color, Color.white, 0.45f);
        bright.a = 1f;
        for (int i = 0; i < count; i++)
        {
            var dir = (outward.normalized + Random.insideUnitCircle * 1.1f).normalized;
            var fx = Piece(i % 2 == 0 ? s_drop : s_bit, at, px * Random.Range(0.7f, 1.2f), i % 4 == 0 ? Color.white : bright, Random.Range(0.3f, 0.5f), 45);
            fx._spark = true;
            fx._velocity = dir * Random.Range(1.4f, 2.6f);
            fx._spin = Random.Range(-600f, 600f);
            var glow = new GameObject("GLOW").AddComponent<SpriteRenderer>();
            glow.gameObject.layer = fx.gameObject.layer;
            glow.transform.SetParent(fx.transform, false);
            glow.transform.localScale = Vector3.one * 1.4f;
            glow.sprite = s_glow;
            glow.color = new Color(color.r, color.g, color.b, 0.6f);
            Sort(glow, 44);
            fx._glow = glow;
        }
        for (int i = 0; i < 2; i++)
        {
            var ring = Piece(s_ring, at, px * (1.5f + i * 1f), new Color(bright.r, bright.g, bright.b, 0.3f - i * 0.12f), 0.25f + i * 0.06f, 46);   // (small: a tight ripple)
            ring._ring = true;
            ring._puffGrow = 4f - i * 1f;
        }
    }

    // Moving with `on` from now (its blow's flash and sparks with a foe knocked back).
    static void Follow(FoeFx fx, Transform on)
    {
        if (!on) return;
        fx._follow = on; fx._followLast = on.position;
    }

    // A glow round a spark, in its colour.
    static void Glow(FoeFx fx, Color color, float scale, float alpha, int order)
    {
        var glow = new GameObject("GLOW").AddComponent<SpriteRenderer>();
        glow.gameObject.layer = fx.gameObject.layer;
        glow.transform.SetParent(fx.transform, false);
        glow.transform.localScale = Vector3.one * scale;
        glow.sprite = s_glow;
        glow.color = new Color(color.r, color.g, color.b, alpha);
        Sort(glow, order);
        fx._glow = glow;
    }

    // (Impact's `over`, while it's making its pieces.)
    static SpriteRenderer s_over;

    // On TopLayer at `order` - or, with s_over, as far over that renderer as `order` is over 39.
    static void Sort(SpriteRenderer sr, int order)
    {
        if (s_over) { sr.sortingLayerID = s_over.sortingLayerID; sr.sortingOrder = s_over.sortingOrder + 1 + Mathf.Max(0, order - 39); return; }
        sr.sortingLayerName = order > 200 ? ChestGem.KnightLayer : "TopLayer";   // (past 200: over the knight, on her layer)
        sr.sortingOrder = order;
    }

    /// A blow glancing off the knight's Shield Buff at `at`: a gold flash, a ring of gold pixels racing out,
    /// and gold and white pixel sparks flung off her (`outward`: back toward what hit her), twinkling as they fade.
    public static void GoldBurst(Vector3 at, Vector2 outward)
    {
        Init();
        float px = RoadFoe.Pixel * 1.3f;
        var gold = new Color(1f, 0.8f, 0.2f, 1f);
        var light = new Color(1f, 0.95f, 0.6f, 1f);
        var core = Piece(s_glow, at, px * 1.5f, new Color(1f, 1f, 0.9f, 0.95f), 0.12f, 48);
        core._ring = true; core._puffGrow = 9f;
        var flash = Piece(s_glow, at, px * 2.5f, new Color(gold.r, gold.g, gold.b, 0.8f), 0.22f, 47);
        flash._ring = true; flash._puffGrow = 6f;
        var ripple = Piece(s_ring, at, px * 1.5f, new Color(light.r, light.g, light.b, 0.5f), 0.25f, 47);
        ripple._ring = true; ripple._puffGrow = 5f;
        int ring = 16;
        for (int i = 0; i < ring; i++)
        {
            float a = i / (float)ring * Mathf.PI * 2f;
            var fx = Piece(s_bit, at, px, i % 2 == 0 ? light : gold, 0.35f, 46);
            fx._spark = true; fx._drag = 3f; fx._fall = 0f;
            fx._velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.2f;   // (a tight ring)
        }
        for (int i = 0; i < 18; i++)
        {
            var dir = (outward.normalized + Random.insideUnitCircle * 1.2f).normalized;
            var c = i % 4 == 0 ? Color.white : i % 2 == 0 ? light : gold;
            var fx = Piece(i % 3 == 0 ? s_drop : s_bit, at + (Vector3)Random.insideUnitCircle * px, px * Random.Range(0.7f, 1.2f), c, Random.Range(0.35f, 0.65f), 46);
            fx._spark = true; fx._twinkle = true; fx._sway = Random.value;
            fx._drag = 3.5f; fx._fall = 1.5f;
            fx._velocity = dir * Random.Range(1.4f, 3.2f);
            fx._spin = Random.Range(-600f, 600f);
            if (i % 2 == 0) Glow(fx, gold, 1.4f, 0.6f, 45);
        }
    }

    /// A blow landing on a foe at `at`: a white-hot flash and a flash in `color`, and neon sparks in its
    /// colour (lighter, white) flung out away from the blow (`outward`), glowing, falling a little as they fade.
    /// `over`: drawn just over it, on its sorting layer (the Slime Boss: its own layer, over TopLayer).
    public static void Impact(Vector3 at, Color color, Vector2 outward, float size = 1f, int count = 10, Transform follow = null, SpriteRenderer over = null)
    {
        s_over = over;
        try { ImpactNow(at, color, outward, size, count, follow); }
        finally { s_over = null; }
    }

    /// `draw` (Gore, Blood, Bones...) drawn just over `over`, on its sorting layer - a foe's own burst over it,
    /// not behind it (the foes are over the knight).
    public static void Over(SpriteRenderer over, System.Action draw)
    {
        s_over = over;
        try { draw(); }
        finally { s_over = null; }
    }

    static void ImpactNow(Vector3 at, Color color, Vector2 outward, float size, int count, Transform follow)
    {
        Init();
        color.a = 1f;
        float px = RoadFoe.Pixel;
        var light = Color.Lerp(color, Color.white, 0.45f);
        var core = Piece(s_glow, at, px * 1.25f * size, new Color(1f, 1f, 1f, 0.95f), 0.1f, 48);
        core._ring = true; core._puffGrow = 10f; Follow(core, follow);
        var flash = Piece(s_glow, at, px * 2f * size, new Color(color.r, color.g, color.b, 0.75f), 0.18f, 47);
        flash._ring = true; flash._puffGrow = 7f; Follow(flash, follow);
        var ripple = Piece(s_ring, at, px * 1.25f * size, new Color(light.r, light.g, light.b, 0.45f), 0.2f, 47);
        ripple._ring = true; ripple._puffGrow = 6f; Follow(ripple, follow);
        for (int i = 0; i < count; i++)
        {
            var dir = (outward.normalized + Random.insideUnitCircle * 0.9f).normalized;
            var c = i % 4 == 0 ? Color.white : i % 2 == 0 ? light : color;
            var fx = Piece(i % 3 == 0 ? s_drop : s_bit, at + (Vector3)Random.insideUnitCircle * px * size, px * Random.Range(0.7f, 1.3f), c, Random.Range(0.25f, 0.5f), 46);
            fx._spark = true;
            fx._drag = 4f; fx._fall = 2f;
            fx._velocity = dir * Random.Range(1.6f, 3.4f) * Mathf.Sqrt(size);
            fx._spin = Random.Range(-500f, 500f);
            Glow(fx, color, 1.3f, 0.6f, 45);
            Follow(fx, follow);
        }
    }

    /// Blood FX's slime bursts (EndlessKnightDirector.goreBursts) - kept for effects that want them; Gore draws its own pixels now.
    public static GameObject[] Bursts;
    static readonly System.Collections.Generic.Dictionary<Color, Material> s_goreLooks = new System.Collections.Generic.Dictionary<Color, Material>();
    const float BurstArtHue = 265f / 360f;

    /// Blood bursting out of `at` in `color`, in pixels: a quick neon flash, then drops of every size
    /// flung out all round - fast, then falling - that splash on the road, each glowing.
    public static void Gore(Vector3 at, Color color, float size = 1f, int drops = 8, bool bloom = true, Transform follow = null, bool chrome = false)
    {
        s_chrome = chrome;
        GoreBurst(at, color, size, drops, bloom, follow);
        s_chrome = false;
    }

    static void GoreBurst(Vector3 at, Color color, float size, int drops, bool bloom, Transform follow)
    {
        Init();
        color.a = 1f;
        float px = RoadFoe.Pixel;
        // (a pale colour - white neon - keeps its shadier pixels pale too, all of it glowing)
        Color.RGBToHSV(color, out _, out float sat, out _);
        float shade = Mathf.Lerp(0.25f, 1f, sat);
        if (bloom)
        {
            // The explosion: a hot white-hot core flash, a bigger flash in its colour,
            // a ring of its pixels racing out, and puffs of its spray swelling.
            var core = Piece(s_glow, at, px * 1.5f * size, new Color(1f, 1f, 1f, 0.9f), 0.12f, 42);
            core._ring = true;
            core._puffGrow = 9f;
            Follow(core, follow);
            var flash = Piece(s_glow, at, px * 2.5f * size, new Color(color.r, color.g, color.b, 0.85f), 0.25f, 40);
            flash._ring = true;
            flash._puffGrow = 6f;
            Follow(flash, follow);
            var light = Color.Lerp(color, Color.white, 0.35f);
            int ring = Mathf.RoundToInt(14 * Mathf.Sqrt(size));
            for (int i = 0; i < ring; i++)
            {
                float a = i / (float)ring * Mathf.PI * 2f;
                var fx = Piece(s_bit, at, px * 1.5f, i % 2 == 0 ? light : color, 0.35f, 46);
                fx._spark = true; fx._drag = 3f; fx._fall = 0f;
                fx._velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.3f * Mathf.Sqrt(size);   // (a tight ring)
                Follow(fx, follow);
            }
            var mist = Color.Lerp(color, Color.black, 0.45f * shade);
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * Mathf.PI * 2f + Random.value;
                var puff = Piece(s_puff, at, px * Random.Range(0.6f, 0.9f) * size, new Color(mist.r, mist.g, mist.b, 0.7f), Random.Range(0.3f, 0.45f), 41);
                puff._puff = true;
                puff._velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f) * Random.Range(0.2f, 0.4f) * Mathf.Sqrt(size);
                puff._puffGrow = 2f;
                Follow(puff, follow);
            }
        }
        var dark = Color.Lerp(color, Color.black, 0.35f * shade);
        int count = Mathf.RoundToInt(10 * size) + drops;
        for (int i = 0; i < count; i++)
        {
            float a = Random.value * Mathf.PI * 2f, speed = Random.Range(1.2f, 3.4f) * Mathf.Sqrt(size);
            var sprite = i % 4 == 0 ? s_splat : i % 3 == 0 ? s_bit : s_drop;
            var fx = Piece(sprite, at + (Vector3)Random.insideUnitCircle * px * 2f * size, px * Random.Range(0.8f, 1.8f), i % 3 == 0 ? dark : color, Random.Range(1.2f, 2.2f), 41);
            fx._velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f + 0.5f) * speed;
            fx._splat = true;
            var glow = new GameObject("GLOW").AddComponent<SpriteRenderer>();
            glow.gameObject.layer = fx.gameObject.layer;
            glow.transform.SetParent(fx.transform, false);
            glow.transform.localScale = Vector3.one * 1.1f;
            glow.sprite = s_glow;
            glow.color = new Color(color.r, color.g, color.b, 0.55f);
            Sort(glow, 39);
            fx._glow = glow;
        }
    }

    /// Plain blood splashing out of `at` in `color` (no glow, no flash): a burst of its liquid, a gout
    /// of heavy drops - blobs, drips, specks, dark and light - flung up and out, arcing down to splat
    /// on the road, and pools spreading there; a little dark spray.
    public static void Splash(Vector3 at, Color color, float size = 1f)
    {
        Init();
        color.a = 1f;
        var dark = Color.Lerp(color, Color.black, 0.45f);
        var deep = Color.Lerp(color, Color.black, 0.65f);
        var wet = Color.Lerp(color, Color.white, 0.25f);   // (its sheen)
        float px = RoadFoe.Pixel;
        if (AirBursts != null && AirBursts.Length > 0 && RoadEnemy.HueMaterial)
        {
            var prefab = AirBursts[Random.Range(0, AirBursts.Length)];
            if (prefab)
            {
                var go = Object.Instantiate(prefab, new Vector3(at.x, at.y, SpikeTrap.LaneZ - 0.03f), Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
                go.layer = LayerMask.NameToLayer("PitActors");
                var fx = go.GetComponent<PitSpriteFx>();
                if (fx) { fx.characterScale = 1.2f * size * Random.Range(0.85f, 1.15f); fx.snapToRoad = false; fx.fps *= 1.5f; }
                foreach (var r in go.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    r.sharedMaterial = BloodLook(color);
                    Sort(r, 44);
                }
            }
        }
        // The gout: mostly up and out, heavy.
        int count = Mathf.RoundToInt(26 * size);
        for (int i = 0; i < count; i++)
        {
            float a = Mathf.Lerp(15f, 165f, Random.value) * Mathf.Deg2Rad, speed = Random.Range(1.2f, 3.6f) * Mathf.Sqrt(size);
            var sprite = i % 5 == 0 ? s_splat : i % 3 == 0 ? s_bit : s_drop;
            var c = i % 4 == 0 ? dark : i % 7 == 0 ? wet : i % 5 == 0 ? deep : color;
            var fx = Piece(sprite, at + (Vector3)Random.insideUnitCircle * px * 2.5f * size, px * Random.Range(0.8f, 2.2f) * (i % 6 == 0 ? 1.5f : 1f), c, Random.Range(1.8f, 3f), 41);
            fx._velocity = new Vector2(Mathf.Cos(a) * 1.2f, Mathf.Sin(a)) * speed;
            fx._splat = true;
        }
        // Specks: a fine spray, quick.
        for (int i = 0; i < Mathf.RoundToInt(10 * size); i++)
        {
            var fx = Piece(s_drop, at, px * 0.6f, i % 2 == 0 ? color : dark, Random.Range(0.35f, 0.6f), 42);
            fx._spark = true; fx._drag = 2.5f; fx._fall = 3f;
            fx._velocity = Random.insideUnitCircle.normalized * Random.Range(2f, 4f) * Mathf.Sqrt(size);
        }
        // Pools spreading on the road under it.
        for (int i = 0; i < Mathf.RoundToInt(3 * size); i++)
        {
            var pool = Piece(s_splat, new Vector3(at.x + Random.Range(-0.25f, 0.25f) * size, SpikeTrap.PlayerFeetY + Random.Range(-0.03f, 0.01f), at.z), px * Random.Range(2f, 3.5f) * size, i % 2 == 0 ? dark : deep, Random.Range(2.5f, 3.5f), 38);
            pool._landed = true;
        }
        // A little dark mist.
        for (int i = 0; i < 3; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            var puff = Piece(s_puff, at, px * Random.Range(1f, 1.5f) * size, new Color(deep.r, deep.g, deep.b, 0.6f), Random.Range(0.3f, 0.45f), 40);
            puff._puff = true; puff._puffGrow = 1.5f;
            puff._velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * Random.Range(0.3f, 0.6f);
        }
    }

    // The liquid bursts in a colour, as blood (not glowing).
    static readonly System.Collections.Generic.Dictionary<Color, Material> s_bloodLooks = new System.Collections.Generic.Dictionary<Color, Material>();
    static Material BloodLook(Color color)
    {
        if (s_bloodLooks.TryGetValue(color, out var look) && look) return look;
        Color.RGBToHSV(color, out float h, out float s, out float v);
        look = new Material(RoadEnemy.HueMaterial) { name = "Blood " + ColorUtility.ToHtmlStringRGB(color) };
        look.SetFloat("_Hue", Mathf.Repeat(h - BurstArtHue, 1f));
        look.SetFloat("_Saturation", Mathf.Clamp(s * 1.1f, 0f, 2f));
        look.SetFloat("_Brightness", Mathf.Clamp(0.35f + v * 0.5f, 0f, 2f));
        s_bloodLooks[color] = look;
        return look;
    }

    static Material GoreLook(Color color)
    {
        if (s_goreLooks.TryGetValue(color, out var look) && look) return look;
        Color.RGBToHSV(color, out float h, out float s, out float v);
        look = new Material(RoadEnemy.HueMaterial) { name = "Gore " + ColorUtility.ToHtmlStringRGB(color) };
        look.SetFloat("_Hue", Mathf.Repeat(h - BurstArtHue, 1f));
        look.SetFloat("_Saturation", Mathf.Clamp(s * 1.3f, 0f, 2f));
        look.SetFloat("_Brightness", Mathf.Clamp(0.6f + v * 0.9f, 0f, 2f));   // (glowing)
        s_goreLooks[color] = look;
        return look;
    }

    /// Bursts of liquid in mid-air (Blood FX's slime essence), turned to a colour (EndlessKnightDirector.liquidBursts).
    public static GameObject[] AirBursts;

    /// A magic potion bursting at `at` in `color`: a blob of its liquid exploding in the air, glowing,
    /// a heavy spray of drops splashing down onto the road, its splash there, and a few sparkles of its magic.
    public static void Potion(Vector3 at, Color color, float size = 1f)
    {
        Init();
        color.a = 1f;
        if (AirBursts != null && AirBursts.Length > 0 && RoadEnemy.HueMaterial)
        {
            var prefab = AirBursts[Random.Range(0, AirBursts.Length)];
            if (prefab)
            {
                var go = Object.Instantiate(prefab, new Vector3(at.x, at.y, SpikeTrap.LaneZ - 0.03f), Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
                go.layer = LayerMask.NameToLayer("PitActors");
                var fx = go.GetComponent<PitSpriteFx>();
                if (fx) { fx.characterScale = 1.4f * size * Random.Range(0.85f, 1.15f); fx.snapToRoad = false; fx.fps *= 1.4f; }
                foreach (var r in go.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    r.sharedMaterial = GoreLook(color);
                    Sort(r, 44);
                }
            }
        }
        Gore(at, color, size, Mathf.RoundToInt(22 * size));
        // A few sparkles of its magic.
        float px = RoadFoe.Pixel * 1.5f;
        var light = Color.Lerp(color, Color.white, 0.55f);
        for (int i = 0; i < 6; i++)
        {
            var fx = Piece(s_bit, at + (Vector3)Random.insideUnitCircle * px * 3f, px, i % 2 == 0 ? Color.white : light, Random.Range(0.4f, 0.7f), 45);
            fx._spark = true; fx._twinkle = true; fx._sway = Random.value;
            fx._drag = 3f; fx._fall = -0.4f;
            fx._velocity = Random.insideUnitCircle.normalized * Random.Range(0.5f, 1.4f);
        }
    }


    /// Level 2's candy corn picked up (PitCollectable), each colour its own burst (LevelCollection.CornColours):
    /// orange - little candy corns popping out and bouncing on the road; red - embers fountaining up from a ripple;
    /// pink - confetti fluttering down; purple - a pinwheel of twinkles spiralling out; blue - bubbles wobbling up;
    /// green - big four-point stars shooting out, twinkling.
    public static void Corn(Vector3 at, int colour)
    {
        Init();
        if (!s_kernel)
        {
            var white = new Color32(255, 255, 255, 255);
            s_kernel = Make(new[] { ".L.", "LLL", "DDD" }, white, new Color32(255, 170, 30, 255));
            s_bubble = Make(new[] { ".LL.", "L.DL", "L..L", ".LL." }, new Color32(190, 230, 255, 255), white);
            s_star = Make(new[] { "..L..", "..L..", "LLDLL", "..L..", "..L.." }, white, white);
        }
        float px = RoadFoe.Pixel;
        Color main = CornMain[colour], light = Color.Lerp(main, Color.white, 0.5f);
        at.z -= 0.02f;
        int over = 210;   // (over the knight, like the corn was)
        switch (colour)
        {
            case 0:   // orange: candy corn popping
                for (int i = 0; i < 9; i++)
                {
                    var fx = Piece(s_kernel, at, px * Random.Range(1f, 1.4f), Color.white, Random.Range(1f, 1.4f), over);
                    fx._velocity = new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(1.6f, 2.8f));
                    fx._spin = Random.Range(-600f, 600f);
                }
                break;
            case 1:   // red: embers fountaining up
            {
                var ring = Piece(s_ring, at, px * 0.5f, new Color(light.r, light.g, light.b, 0.9f), 0.35f, over);
                ring._ring = true; ring._puffGrow = 5f;
                for (int i = 0; i < 18; i++)
                {
                    var c = i % 3 == 0 ? new Color(1f, 0.8f, 0.3f) : i % 2 == 0 ? light : main;
                    var fx = Piece(s_bit, at + new Vector3(Random.Range(-2f, 2f) * px, 0f, 0f), px * Random.Range(0.8f, 1.3f), c, Random.Range(0.6f, 1.1f), over);
                    fx._spark = true; fx._twinkle = true; fx._sway = Random.value;
                    fx._drag = 1.5f; fx._fall = -1.4f;
                    fx._velocity = new Vector2(Random.Range(-0.7f, 0.7f), Random.Range(0.6f, 1.8f));
                }
                break;
            }
            case 2:   // pink: confetti
                for (int i = 0; i < 18; i++)
                {
                    var c = i % 3 == 0 ? Color.white : i % 3 == 1 ? light : main;
                    var fx = Piece(i % 2 == 0 ? s_drop : s_bit, at, px * Random.Range(0.8f, 1.2f), c, Random.Range(1.2f, 1.8f), over);
                    fx._feather = true;
                    fx._velocity = new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(1.2f, 2.4f));
                    fx._spin = Random.Range(-500f, 500f);
                    fx._sway = Random.value * 6f;
                }
                break;
            case 3:   // purple: a pinwheel of twinkles
            {
                var flash = Piece(s_glow, at, px * 5f, new Color(light.r, light.g, light.b, 0.8f), 0.25f, over);
                flash._ring = true; flash._puffGrow = 4f;
                int arms = 5, per = 4;
                float turn = Random.value * Mathf.PI * 2f;
                for (int a = 0; a < arms; a++)
                    for (int k = 0; k < per; k++)
                    {
                        float ang = turn + a / (float)arms * Mathf.PI * 2f + k * 0.35f;
                        float speed = 0.7f + k * 0.45f;
                        var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        var fx = Piece(s_bit, at, px * (1.3f - k * 0.15f), k % 2 == 0 ? light : main, 0.5f + k * 0.08f, over);
                        fx._spark = true; fx._twinkle = true; fx._sway = Random.value;
                        fx._drag = 2.5f; fx._fall = 0f;
                        fx._velocity = (dir + new Vector2(-dir.y, dir.x) * 0.9f) * speed;   // (out, and round)
                    }
                break;
            }
            case 4:   // blue: bubbles
                for (int i = 0; i < 9; i++)
                {
                    var fx = Piece(s_bubble, at + (Vector3)Random.insideUnitCircle * px * 3f, px * Random.Range(0.9f, 1.6f), Color.white, Random.Range(0.9f, 1.4f), over);
                    fx._spark = true; fx._sway = Random.value;
                    fx._drag = 1.2f; fx._fall = -0.9f;
                    fx._velocity = new Vector2(Random.Range(-0.9f, 0.9f), Random.Range(0.2f, 0.9f));
                    fx._spin = Random.Range(-90f, 90f);
                }
                for (int i = 0; i < 8; i++)
                {
                    var fx = Piece(s_drop, at, px * 0.8f, i % 2 == 0 ? light : main, Random.Range(0.35f, 0.55f), over);
                    fx._spark = true; fx._drag = 3f; fx._fall = 2f;
                    fx._velocity = Random.insideUnitCircle.normalized * Random.Range(1f, 1.8f);
                }
                break;
            default:   // green: stars shooting out
                for (int i = 0; i < 6; i++)
                {
                    float ang = i / 6f * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                    var fx = Piece(s_star, at, px * Random.Range(1.1f, 1.5f), i % 2 == 0 ? light : Color.white, Random.Range(0.6f, 0.8f), over);
                    fx._spark = true; fx._twinkle = true; fx._sway = Random.value;
                    fx._drag = 3f; fx._fall = 0.4f;
                    fx._spin = Random.Range(-360f, 360f);
                    fx._velocity = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(1.6f, 2.4f);
                }
                for (int i = 0; i < 10; i++)
                {
                    var fx = Piece(s_bit, at, px, main, Random.Range(0.3f, 0.5f), over);
                    fx._spark = true; fx._twinkle = true; fx._drag = 4f; fx._fall = 0f;
                    fx._velocity = Random.insideUnitCircle.normalized * Random.Range(0.6f, 1.4f);
                }
                break;
        }
    }
    static Sprite s_kernel, s_bubble, s_star;

    /// Level 3's peony picked up (PitCollectable), in its slice colour (LevelCollection.CornColours): a soft bloom,
    /// then its petals bursting up out of it and fluttering down, swaying and turning, in its shades and white.
    public static void Petals(Vector3 at, int colour)
    {
        Init();
        if (!s_petal)
        {
            var white = new Color32(255, 255, 255, 255);
            s_petal = Make(new[] { ".LL", "LLD", "LD." }, white, new Color32(205, 205, 205, 255));
            s_petalSmall = Make(new[] { "LL", "LD" }, white, new Color32(205, 205, 205, 255));
        }
        float px = RoadFoe.Pixel;
        colour = Mathf.Clamp(colour, 0, CornMain.Length - 1);
        Color main = colour == 5 ? new Color32(255, 206, 30, 255) : CornMain[colour], light = Color.Lerp(main, Color.white, 0.45f);   // (the green slice's peony is yellow)
        at.z -= 0.02f;
        int over = 210;   // (over the knight, like the flower was)
        var bloom = Piece(s_glow, at, px * 5f, new Color(light.r, light.g, light.b, 0.7f), 0.3f, over);
        bloom._ring = true; bloom._puffGrow = 4f;
        for (int i = 0; i < 16; i++)
        {
            var c = i % 5 == 0 ? Color.white : i % 2 == 0 ? light : main;
            var fx = Piece(i % 3 == 0 ? s_petalSmall : s_petal, at + (Vector3)Random.insideUnitCircle * px * 2f, px * Random.Range(0.9f, 1.4f), c, Random.Range(1.3f, 2f), over);
            fx._feather = true;
            fx._velocity = new Vector2(Random.Range(-1.4f, 1.4f), Random.Range(1.1f, 2.2f));
            fx._spin = Random.Range(-260f, 260f);
            fx._sway = Random.value * 6f;
        }
    }
    static Sprite s_petal, s_petalSmall;

    /// Level 3's egg picked up (ChestGem.miniEggs): a soft bloom, then little eggs - the same one's - popping up
    /// out of it and tumbling down to the road, with white glints.
    public static void MiniEggs(Vector3 at, Sprite[] eggs, int count = 14)
    {
        Init();
        if (eggs == null || eggs.Length == 0) return;
        float px = RoadFoe.Pixel;
        at.z -= 0.02f;
        int over = 210;   // (over the knight, like the egg was)
        var bloom = Piece(s_glow, at, px * 5f, new Color(1f, 0.95f, 0.8f, 0.7f), 0.3f, over);
        bloom._ring = true; bloom._puffGrow = 4f;
        for (int i = 0; i < count; i++)
        {
            var egg = eggs[Random.Range(0, eggs.Length)];
            if (!egg) continue;
            float tall = px * Random.Range(9f, 12f);   // (its picture, padding and all: the egg about half the size of hers)
            var fx = Piece(egg, at + (Vector3)Random.insideUnitCircle * px * 2f, tall / Mathf.Max(1e-4f, egg.bounds.size.y), Color.white, Random.Range(1.1f, 1.6f), over);
            fx._velocity = new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(1.4f, 2.6f));   // (upright: turned, so few pixels, they'd be mush)
        }
        for (int i = 0; i < 6; i++)
        {
            var fx = Piece(s_bit, at + (Vector3)(Random.insideUnitCircle * px * 3f), px * 0.9f, Color.white, Random.Range(0.3f, 0.6f), over + 1);
            fx._spark = true; fx._twinkle = true; fx._sway = Random.value;
            fx._drag = 3f; fx._fall = 0.5f;
            fx._velocity = Random.insideUnitCircle.normalized * Random.Range(0.4f, 1.2f);
        }
    }
    // (The orange slices' own main colours - scrimsy's - in LevelCollection.CornColours' order.)
    static readonly Color[] CornMain =
    {
        new Color32(255, 155, 68, 255), new Color32(232, 4, 72, 255), new Color32(255, 124, 153, 255),
        new Color32(189, 144, 234, 255), new Color32(99, 177, 255, 255), new Color32(119, 189, 40, 255),
    };

    /// Glass shards of a magic shield at `at` in its colour (and white glints): flung out (`outward`, or every way
    /// when zero) from all over a round `radius` wide, spinning, falling to the road and tinkling to rest there.
    public static void Glass(Vector3 at, Color color, int count, Vector2 outward, float size = 1f, float radius = 0f)
    {
        Init();
        if (!s_shards[0])
        {
            var white = new Color32(255, 255, 255, 255); var edge = new Color32(200, 220, 255, 255);
            s_shards[0] = Make(new[] { "L...", "LL..", "LDL.", "LLDL" }, white, edge);
            s_shards[1] = Make(new[] { ".L", "LL", "LD", "L.", "L." }, white, edge);
            s_shards[2] = Make(new[] { "LLL", ".DL", "..L" }, white, edge);
            s_shards[3] = Make(new[] { "L", "D", "L" }, white, edge);
        }
        float px = RoadFoe.Pixel;
        color.a = 1f;
        var light = Color.Lerp(color, Color.white, 0.55f);
        for (int i = 0; i < count; i++)
        {
            var dir = outward == Vector2.zero ? Random.insideUnitCircle.normalized : (outward.normalized + Random.insideUnitCircle * 0.6f).normalized;
            var from = at + (Vector3)(dir * radius * Random.Range(0.5f, 1f));
            var c = i % 4 == 0 ? Color.white : i % 2 == 0 ? light : new Color(color.r, color.g, color.b, 0.85f);
            var fx = Piece(s_shards[i % s_shards.Length], from, px * Random.Range(0.9f, 1.4f) * Mathf.Sqrt(size), c, Random.Range(1.1f, 1.7f), 46);
            fx._velocity = dir * Random.Range(1.2f, 2.6f) * Mathf.Sqrt(size) + Vector2.up * Random.Range(0.4f, 1.2f);
            fx._spin = Random.Range(-720f, 720f);
            fx.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        }
        // Glints as it goes.
        for (int i = 0; i < count / 3; i++)
        {
            var fx = Piece(s_bit, at + (Vector3)(Random.insideUnitCircle * Mathf.Max(radius, px * 3f)), px * 0.9f, Color.white, Random.Range(0.3f, 0.6f), 47);
            fx._spark = true; fx._twinkle = true; fx._sway = Random.value;
            fx._drag = 3f; fx._fall = 0.5f;
            fx._velocity = Random.insideUnitCircle.normalized * Random.Range(0.4f, 1.2f);
        }
    }
    static readonly Sprite[] s_shards = new Sprite[4];

    /// A soft round glow (for auras).
    public static Sprite GlowSprite { get { Init(); return s_glow; } }

    static FoeFx Piece(Sprite sprite, Vector3 at, float pixel, Color color, float life, int order)
    {
        var go = new GameObject("FOE FX");
        go.layer = LayerMask.NameToLayer("PitActors");
        go.transform.position = at;
        go.transform.localScale = Vector3.one * pixel;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        Sort(sr, order);
        var fx = go.AddComponent<FoeFx>();
        fx._sr = sr;
        fx._color = color;
        fx._chrome = s_chrome;
        fx._hueOff = Random.value * 0.35f;
        fx._born = Time.time;
        fx._life = life;
        fx._ground = SpikeTrap.PlayerFeetY + Random.Range(-0.04f, 0.02f);
        fx._lastRoad = Road;
        return fx;
    }

    // Her drawn body, once a frame.
    static int s_herFrame = -1;
    static Bounds s_her;
    static bool OnHer(Vector3 at)
    {
        if (s_herFrame != Time.frameCount) { s_herFrame = Time.frameCount; s_her = RoadFoe.PlayerBody(); }
        return s_her.size != Vector3.zero && at.x > s_her.min.x && at.x < s_her.max.x && at.y > s_her.min.y - RoadFoe.Pixel && at.y < s_her.max.y;
    }

    static float Road => EndlessKnightDirector.Player ? EndlessKnightDirector.Player.AccumulatedFrames * EndlessKnightDirector.RoadUnitsPerFrame : 0f;

    /// Bones bursting out of `at`: count pieces (a skull among them if skull).
    public static void Bones(Vector3 at, int count = 9, bool skull = true, float spread = 1f)
    {
        Init();
        float px = RoadFoe.Pixel;
        for (int i = 0; i < count; i++)
        {
            var sprite = skull && i == 0 ? s_skull : i % 3 == 0 ? s_rib : i % 3 == 1 ? s_long : s_bit;
            var fx = Piece(sprite, at + (Vector3)Random.insideUnitCircle * px * 3f, px, Color.white, Random.Range(1.4f, 2.2f), 40);
            fx._velocity = new Vector2(Random.Range(-1.3f, 1.3f), Random.Range(1.2f, 2.6f)) * spread;
            fx._spin = Random.Range(-720f, 720f);
        }
    }

    /// Blood sprayed out of `at` in arcs, glowing in its colour, splashing down on the road.
    public static void Blood(Vector3 at, Color color, int count = 16, float size = 1f, float spread = 1f)
    {
        Init();
        float px = RoadFoe.Pixel * size;
        for (int i = 0; i < count; i++)
        {
            var fx = Piece(s_drop, at + (Vector3)Random.insideUnitCircle * px * 2f, px * Random.Range(0.6f, 1.1f), color, Random.Range(1.6f, 2.6f), 41);
            fx._velocity = new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(0.6f, 2.2f)) * spread;
            fx._splat = true;
            // Its glow round it.
            var glow = new GameObject("GLOW").AddComponent<SpriteRenderer>();
            glow.gameObject.layer = fx.gameObject.layer;
            glow.transform.SetParent(fx.transform, false);
            glow.transform.localScale = Vector3.one * 1.1f;
            glow.sprite = s_glow;
            glow.color = new Color(color.r, color.g, color.b, 0.55f);
            Sort(glow, 39);
            fx._glow = glow;
        }
    }

    /// A drop of lava off a dragon's fire at `at`: white-hot, glowing, falling onto the road and pooling
    /// there, cooling from yellow-hot to its colour as it fades.
    public static void Lava(Vector3 at, Color color, Vector2 velocity, float size = 1f)
    {
        Init();
        color.a = 1f;
        float px = RoadFoe.Pixel * Mathf.Max(0.8f, Mathf.Sqrt(size));
        var fx = Piece(Random.value < 0.25f ? s_bit : s_drop, at, px * Random.Range(0.7f, 1.4f), color, Random.Range(1.1f, 2f), 41);
        fx._velocity = velocity;
        fx._splat = true;
        fx._lava = true;
        fx._hot = Color.Lerp(color, new Color(1f, 0.95f, 0.55f), 0.65f);
        var glow = new GameObject("GLOW").AddComponent<SpriteRenderer>();
        glow.gameObject.layer = fx.gameObject.layer;
        glow.transform.SetParent(fx.transform, false);
        glow.transform.localScale = Vector3.one * 1.6f;
        glow.sprite = s_glow;
        glow.color = new Color(color.r, color.g, color.b, 0.7f);
        Sort(glow, 39);
        fx._glow = glow;
        fx._sway = Random.value * 10f;
    }

    /// A rock smash on the road at `at` (Gollux's fist coming down): chunks of rock in its colours (`light`, `dark`)
    /// burst up and out across `width`, tumbling and bouncing back down, crumbs among them, and a cloud of dust.
    /// `blast`: bursting out every way from `at` in mid-air instead (its body blowing apart), falling to the road.
    /// `bigShare`: how many of them are big chunks (the rest chips and crumbs), `bigSize` how big those are.
    /// `size`: every piece (and a blast's reach) scaled by it.
    public enum GemKind { Cut, Lava, Shard }

    /// A rock-thrower's own look (ROCKY's): its rocks' colours, and gems among them (clear: none).
    public struct RockLook
    {
        public Color light, dark, gem, gemDark;
        public GemKind kind;
    }
    /// The next Rocks call's gems (one call only: RoadBoss sets it as it throws them).
    public static RockLook? NextGems;

    // A gem piece in its own colours (its glint white), made once each.
    static readonly System.Collections.Generic.Dictionary<(Color, Color, GemKind), Sprite> s_gems = new System.Collections.Generic.Dictionary<(Color, Color, GemKind), Sprite>();
    static Sprite Gem(Color light, Color dark, GemKind kind)
    {
        if (s_gems.TryGetValue((light, dark, kind), out var sp) && sp) return sp;
        var rows = kind == GemKind.Shard ? new[] { "..W..", ".LLD.", ".LLD.", ".LLD.", "..D.." }
            : kind == GemKind.Lava ? new[] { "LL.", "LLD", ".DD" }
            : new[] { "..L..", ".LWL.", "LLLLD", ".LDD.", "..D.." };
        int h = rows.Length, w = rows[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = rows[h - 1 - y][x];
                px[y * w + x] = c == 'L' ? light : c == 'D' ? dark : c == 'W' ? Color.white : Color.clear;
            }
        tex.SetPixels32(px); tex.Apply();
        return s_gems[(light, dark, kind)] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
    }

    public static void Rocks(Vector3 at, Color light, Color dark, int count = 14, float width = 0.3f, bool blast = false,
        float bigShare = 1f / 3f, float bigSize = 1f, float size = 1f, float ground = float.NaN)
    {
        var gems = NextGems;
        NextGems = null;
        bool withGems = gems.HasValue && gems.Value.gem.a > 0f;
        Init();
        if (!s_rock)
        {
            var white = new Color32(255, 255, 255, 255); var grey = new Color32(150, 150, 150, 255);
            s_rock = Make(new[] { ".LLL.", "LLLLD", "LLLDD", ".LDD." }, white, grey);
            s_chip = Make(new[] { "LL.", "LLD", ".DD" }, white, grey);
        }
        float px = RoadFoe.Pixel * size;
        for (int i = 0; i < count; i++)
        {
            bool big = (i + 0.5f) / count < bigShare;
            // (with gems: every third a gem, the rocks mostly the bright end of their colours)
            bool gem = withGems && i % 3 == 2;
            var c = gem ? Color.white : Color.Lerp(dark, light, withGems ? Mathf.Sqrt(Random.value) : Random.value);
            var shape = gem ? Gem(gems.Value.gem, gems.Value.gemDark, gems.Value.kind) : big ? s_rock : i % 3 == 1 ? s_chip : s_bit;
            var fx = Piece(shape, at + new Vector3(Random.Range(-width, width) * 0.5f, px * 2f, 0f),
                px * (gem ? Random.Range(1.3f, 1.8f) * (big ? bigSize : 1f) : big ? Random.Range(1.6f, 2.4f) * bigSize : Random.Range(0.9f, 1.4f)), c, Random.Range(1f, 1.5f), 47);
            fx._velocity = blast ? (Random.insideUnitCircle.normalized * Random.Range(1.2f, 3f) + Vector2.up * 1.2f) * size
                : new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(1.2f, big ? 2.4f : 3f));
            fx._spin = Random.Range(-500f, 500f);
            // (every rock down to the road - broken off up high, or knocked out of a fist - unless it starts under it)
            bool toRoad = blast || !float.IsNaN(ground) || at.y > SpikeTrap.PlayerFeetY + 0.02f;
            fx._ground = toRoad ? (float.IsNaN(ground) ? SpikeTrap.PlayerFeetY : ground) + Random.Range(-0.04f, 0.02f) : at.y;
        }
        for (int i = 0; i < 6; i++)
        {
            var dust = Piece(s_puff, at + new Vector3(Random.Range(-width, width), blast ? Random.Range(-width, width) : px * 3f, 0f), px * Random.Range(1.6f, 2.6f), new Color(0.55f, 0.47f, 0.42f, 0.65f), Random.Range(0.5f, 0.8f), 42);
            dust._puff = true;
            dust._velocity = new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.1f, 0.35f));
            dust._puffGrow = 1.8f;
        }
    }
    static Sprite s_rock, s_chip;

    /// Earth thrown up out of the ground at `at` (something climbing out): clods of dirt flung up, falling back, and a little dust.
    public static void Dirt(Vector3 at, int count = 10, float width = 0.3f)
    {
        Init();
        float px = RoadFoe.Pixel;
        Color[] earth = { new Color(0.32f, 0.22f, 0.14f), new Color(0.22f, 0.15f, 0.1f), new Color(0.42f, 0.3f, 0.18f), new Color(0.16f, 0.12f, 0.09f) };
        for (int i = 0; i < count; i++)
        {
            var c = earth[Random.Range(0, earth.Length)];
            var fx = Piece(i % 3 == 0 ? s_bit : s_drop, at + new Vector3(Random.Range(-width, width), 0f, 0f), px * Random.Range(0.8f, 1.5f), c, Random.Range(0.9f, 1.4f), 43);
            fx._velocity = new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(1f, 2.2f));
            fx._spin = Random.Range(-400f, 400f);
            fx._ground = at.y;
        }
        for (int i = 0; i < 3; i++)
        {
            var dust = Piece(s_puff, at + new Vector3(Random.Range(-width, width), RoadFoe.Pixel * 2f, 0f), px * Random.Range(1f, 1.6f), new Color(0.45f, 0.36f, 0.27f, 0.6f), Random.Range(0.4f, 0.6f), 42);
            dust._puff = true;
            dust._velocity = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.15f, 0.4f));
            dust._puffGrow = 1.4f;
        }
    }

    /// A poof of smoke at `at`: round puffs bursting out and swelling as they fade.
    public static void Poof(Vector3 at, int count = 7, float size = 1f)
    {
        Init();
        float px = RoadFoe.Pixel * size;
        for (int i = 0; i < count; i++)
        {
            float a = i / (float)count * Mathf.PI * 2f + Random.value * 0.6f;
            var fx = Piece(s_puff, at, px * Random.Range(1.4f, 2.2f), new Color(1f, 1f, 1f, 0.9f), Random.Range(0.35f, 0.5f), 42);
            fx._puff = true;
            fx._velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f) * Random.Range(0.5f, 0.9f) * Mathf.Sqrt(size);
            fx._puffGrow = Random.Range(1.6f, 2.4f);
        }
    }

    /// Feathers bursting out of `at` in `color` (and white), then drifting down, swaying, and fading.
    public static void Feathers(Vector3 at, Color color, int count = 12)
    {
        Init();
        float px = RoadFoe.Pixel;
        for (int i = 0; i < count; i++)
        {
            var c = i % 3 == 0 ? Color.white : color;
            var fx = Piece(s_feather, at + (Vector3)Random.insideUnitCircle * px * 2f, px * Random.Range(0.9f, 1.3f), c, Random.Range(1.6f, 2.4f), 41);
            fx._feather = true;
            fx._velocity = new Vector2(Random.Range(-1.4f, 1.4f), Random.Range(0.8f, 2f));
            fx._spin = Random.Range(-200f, 200f);
            fx._sway = Random.value * 6f;
            fx.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        }
    }

    /// The bomb guy's explosion at `at`, turned to `hue` (degrees; the explosion's own: about 30),
    /// hurting the knight if harmful (her Shield Buff aside).
    public static void Blast(BombGuy bomb, Vector3 at, float hueDegrees, Material hueMaterial, int damage, bool harmful, bool onGround = true, bool tight = false)
    {
        if (!bomb || !bomb.explosionPrefab) return;
        var boom = Object.Instantiate(bomb.explosionPrefab, new Vector3(at.x, onGround ? SpikeTrap.PlayerFeetY : at.y, SpikeTrap.LaneZ - 0.02f), Quaternion.identity);
        boom.transform.localScale = Vector3.one * (SpikeTrap.PixelSize / 0.01f * bomb.explosionScale);
        if (hueMaterial)
        {
            var look = new Material(hueMaterial);
            look.SetFloat("_Hue", Mathf.Repeat((hueDegrees - 30f) / 360f, 1f));
            look.SetFloat("_Saturation", 1.4f);
            look.SetFloat("_Brightness", 1.25f);
            foreach (var r in boom.GetComponentsInChildren<SpriteRenderer>(true)) r.sharedMaterial = look;
        }
        if (harmful)
        {
            var blast = boom.AddComponent<ExplosionDamage>();
            blast.damage = damage;
            blast.knockHeight = bomb.knockHeight;
            blast.knockBack = bomb.knockBack;
            blast.tight = tight;
            blast.until = 0.2f;   // (level 4's revenants: hurting only as it bursts, not all the while its cloud hangs there)
        }
        PitAudio.PlayClip(bomb.explosionSound, bomb.explosionVolume);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Rigid, harmful ? 1f : 0.5f);
    }

    void Update()
    {
        // Staying put on the road as it scrolls - or with what it's on, as that moves.
        float road = Road;
        if (_follow) { transform.position += _follow.position - _followLast; _followLast = _follow.position; }
        else transform.position += Vector3.left * (road - _lastRoad);   // (what it was on gone: on the road again)
        _lastRoad = road;

        float age = Time.time - _born;
        if (_chrome)
        {
            // Rainbow chrome: a pastel hue flowing with time and where it is, a glint now and then, in a few steps.
            float h = Mathf.Repeat(transform.position.x * 4f + Time.time * 0.8f + _hueOff, 1f);
            var c = Color.Lerp(Color.HSVToRGB(h, 1f, 1f), Color.white, 0.25f);
            if (Mathf.Repeat(Time.time * 1.7f + _hueOff * 3f, 1f) < 0.08f) c = Color.white;
            c = new Color(Mathf.Round(c.r * 7f) / 7f, Mathf.Round(c.g * 7f) / 7f, Mathf.Round(c.b * 7f) / 7f);
            _color = new Color(c.r, c.g, c.b, _color.a);
            if (_glow) _glow.color = new Color(c.r, c.g, c.b, _glow.color.a);
        }
        if (_ring)
        {
            // Swelling fast, thinning out.
            transform.localScale *= 1f + _puffGrow * Time.deltaTime;
            float k = Mathf.Clamp01(1f - age / _life);
            _sr.color = new Color(_color.r, _color.g, _color.b, _color.a * k);
            if (age > _life) Destroy(gameObject);
            return;
        }
        if (_spark)
        {
            // Flung out, slowing, a little gravity, shrinking as it fades.
            _velocity *= Mathf.Exp(-_drag * Time.deltaTime);
            _velocity.y -= _fall * Time.deltaTime;
            transform.position += (Vector3)(_velocity * Time.deltaTime);
            transform.Rotate(0f, 0f, _spin * Time.deltaTime);
            float k = Mathf.Clamp01(1f - age / _life);
            if (_twinkle) k *= ((int)((age + _sway) * 24f)) % 3 == 0 ? 0.25f : 1f;   // (twinkling as it goes)
            _sr.color = new Color(_color.r, _color.g, _color.b, _color.a * k);
            if (_glow) _glow.color = new Color(_glow.color.r, _glow.color.g, _glow.color.b, 0.6f * k);
            if (age > _life) Destroy(gameObject);
            return;
        }
        if (_puff)
        {
            // Swelling and slowing as it fades.
            transform.position += (Vector3)(_velocity * Time.deltaTime);
            _velocity *= Mathf.Exp(-5f * Time.deltaTime);
            transform.localScale *= 1f + _puffGrow * Time.deltaTime;
            float k = Mathf.Clamp01(1f - age / _life);
            _sr.color = new Color(_color.r, _color.g, _color.b, _color.a * k * k);
            if (age > _life) Destroy(gameObject);
            return;
        }
        if (_feather && !_landed)
        {
            // Out fast, then drifting down slowly, swaying, turning.
            _velocity.x *= Mathf.Exp(-3f * Time.deltaTime);
            _velocity.y = Mathf.Max(_velocity.y - 4f * Time.deltaTime, -0.35f);
            float sway = Mathf.Sin(Time.time * 4f + _sway) * 0.25f;
            transform.position += (Vector3)((_velocity + new Vector2(sway, 0f)) * Time.deltaTime);
            transform.Rotate(0f, 0f, (_spin * 0.3f + sway * 120f) * Time.deltaTime);
            if (transform.position.y <= _ground && _velocity.y < 0f)
            {
                transform.position = new Vector3(transform.position.x, _ground, transform.position.z);
                _landed = true;
            }
        }
        else if (!_landed)
        {
            _velocity.y -= Gravity * Time.deltaTime;
            transform.position += (Vector3)(_velocity * Time.deltaTime);
            transform.Rotate(0f, 0f, _spin * Time.deltaTime);
            if (transform.position.y <= _ground && _velocity.y < 0f)
            {
                transform.position = new Vector3(transform.position.x, _ground, transform.position.z);
                if (_splat)
                {
                    // A splash on the road.
                    _landed = true;
                    _sr.sprite = s_splat;
                    transform.rotation = Quaternion.identity;
                }
                else if (Mathf.Abs(_velocity.y) > 0.6f)
                {
                    _velocity = new Vector2(_velocity.x * 0.5f, -_velocity.y * 0.35f);   // a bounce
                    _spin *= 0.5f;
                }
                else
                {
                    _landed = true;
                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Round(transform.eulerAngles.z / 90f) * 90f);
                }
            }
        }
        // Blood that's come down on her (big on viking food, she's over a lot of the road): gone, not stuck on her.
        if (_landed && OnHer(transform.position)) { Destroy(gameObject); return; }   // (anything landed - blood, bones, dirt - never left drawn over her)
        float fade = Mathf.Clamp01((_life - age) / 0.6f);
        if (_lava)
        {
            // Hot, then cooling to its colour, then darker; its glow flickering.
            var c = Color.Lerp(_hot, _color, Mathf.Clamp01(age / (_life * 0.5f)));
            c = Color.Lerp(c, _color * 0.55f, Mathf.Clamp01((age - _life * 0.5f) / (_life * 0.5f)));
            _sr.color = new Color(c.r, c.g, c.b, fade);
            float flicker = 0.75f + 0.25f * Mathf.Sin((Time.time + _sway) * 22f);
            if (_glow) _glow.color = new Color(_color.r, _color.g, _color.b, 0.7f * fade * flicker);
            if (age > _life) Destroy(gameObject);
            return;
        }
        _sr.color = new Color(_color.r, _color.g, _color.b, _color.a * fade);
        if (_glow) _glow.color = new Color(_color.r, _color.g, _color.b, 0.55f * fade * (_landed ? 0.6f : 1f));
        if (age > _life) Destroy(gameObject);
    }
}
