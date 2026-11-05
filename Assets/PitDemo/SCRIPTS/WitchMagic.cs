using UnityEngine;

// ARcade: the campfire witch's own magic - a rainbow outline, its colours
// rippling outward round her like a wave, orange sparks rising off her
// (GateMotes), charging up - her bloom surging in beats, lightning crackling
// round her - and she levitates: lifted a little off the road, bobbing
// slowly, orange and purple sparkles twinkling under her feet. Added by
// PitDemoGame with the campfire witch's RoadPin, and by DeathRespawn to the
// witch summoned after a death.
[DefaultExecutionOrder(320)]   // after RoadPin and LevelLit
public class WitchMagic : MonoBehaviour
{
    public Color edge = new Color(1f, 0.55f, 0.12f, 0.95f);
    public Color bloom = new Color(0.7f, 0.25f, 1f, 1f);
    public Color sparks = new Color(1f, 0.6f, 0.15f, 1f);
    [Tooltip("How high she floats and bobs, in her art's pixels.")]
    public float lift = 4f, bob = 2f;
    public float bobSeconds = 2.6f;
    [Tooltip("All her magic's opacity (its outline, lightning, sparks, sparkles).")]
    [Range(0f, 1f)] public float opacity = 0.5f;

    [Header("Power (charging up)")]
    public Color auraOuter = new Color(0.62f, 0.2f, 1f, 1f);
    public Color auraInner = new Color(1f, 0.6f, 0.15f, 1f);
    public float pulsesPerSecond = 2.2f;
    public int bolts = 7;

    SpriteRenderer _sr;
    readonly System.Collections.Generic.List<SpriteRenderer> _bolts = new System.Collections.Generic.List<SpriteRenderer>();
    float[] _boltUntil;
    float _baseY = float.NaN, _pixel, _nextSparkle, _phase;   // (_baseY: how far up she's floated now)
    GateMotes _motes;
    static Sprite[] s_bolts;

    // Lightning, its art: jagged one-pixel bolts zigzagging up, a fork off some, a faint glow
    // each side of the bright core - a few of them, picked at random as they crackle.
    static Sprite[] Bolts()
    {
        if (s_bolts != null) return s_bolts;
        const int w = 9, h = 26, count = 8;
        s_bolts = new Sprite[count];
        var rng = new System.Random(3);
        for (int n = 0; n < count; n++)
        {
            var px = new Color32[w * h];
            void Put(int x, int y, byte a) { if (x >= 0 && x < w && y >= 0 && y < h && px[y * w + x].a < a) px[y * w + x] = new Color32(255, 255, 255, a); }
            int len = 14 + rng.Next(h - 14), cx = w / 2, fork = rng.Next(3) == 0 ? rng.Next(4, len - 4) : -1;
            for (int y = 0; y < len; y++)
            {
                if (y > 0 && rng.Next(3) != 0) cx = Mathf.Clamp(cx + (rng.Next(2) == 0 ? -1 : 1) * (rng.Next(5) == 0 ? 2 : 1), 1, w - 2);
                Put(cx, y, 255); Put(cx - 1, y, 90); Put(cx + 1, y, 90);
                if (y == fork)
                {
                    // a little fork, off to one side
                    int fx = cx, dir = rng.Next(2) == 0 ? -1 : 1;
                    for (int k = 1; k < 6; k++) { fx = Mathf.Clamp(fx + dir * (k % 2 == 0 ? 1 : 0) + dir, 0, w - 1); Put(fx, y + k, 200); }
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Witch Bolt " + n };
            tex.SetPixels32(px); tex.Apply(false, true);
            s_bolts[n] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
        }
        return s_bolts;
    }

    // Her outline a rainbow: rings of her shape, each a pixel further out, the colours flowing out
    // through them like a wave (each band born at her edge, rippling outward, fading as it goes).
    [Header("Rainbow outline")]
    public int rainbowRings = 4;
    public float rainbowSpeed = 0.6f;   // (colour cycles a second)
    readonly System.Collections.Generic.List<SpriteRenderer> _rings = new System.Collections.Generic.List<SpriteRenderer>();
    readonly System.Collections.Generic.List<int> _ringOf = new System.Collections.Generic.List<int>();
    MaterialPropertyBlock _ringBlock;
    static Material s_solid;
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void MakeRainbow()
    {
        if (!s_solid) { var shader = Shader.Find("SpatialEmulator/SpriteSolid"); if (shader) s_solid = new Material(shader) { name = "Witch Rainbow" }; }
        if (!s_solid) return;
        float px = 1f / _sr.sprite.pixelsPerUnit;
        for (int ring = 1; ring <= rainbowRings; ring++)
        {
            int dirs = ring == 1 ? 8 : 12;
            for (int i = 0; i < dirs; i++)
            {
                float a = i * Mathf.PI * 2f / dirs;
                var go = new GameObject("RAINBOW");
                go.layer = _sr.gameObject.layer;
                go.transform.SetParent(_sr.transform, false);
                go.transform.localPosition = new Vector3(Mathf.Round(Mathf.Cos(a) * ring) * px, Mathf.Round(Mathf.Sin(a) * ring) * px, 0.0006f);
                var r = go.AddComponent<SpriteRenderer>();
                r.sharedMaterial = s_solid;
                _rings.Add(r);
                _ringOf.Add(ring);
            }
        }
    }

    void Rainbow(float surge)
    {
        if (_rings.Count == 0) return;
        _ringBlock ??= new MaterialPropertyBlock();
        bool on = _sr.enabled && _sr.sprite;
        float t = (Time.time + _phase) * rainbowSpeed;
        for (int i = 0; i < _rings.Count; i++)
        {
            var r = _rings[i];
            r.enabled = on;
            if (!on) continue;
            int ring = _ringOf[i];
            r.sprite = _sr.sprite;
            r.flipX = _sr.flipX;
            r.sortingLayerID = _sr.sortingLayerID;
            r.sortingOrder = _sr.sortingOrder - ring;   // (the inner rings over the outer)
            // (the hue a ring shows now, the ring inside it showed a moment ago: the colours travel out)
            float hue = Mathf.Repeat(t - ring * 0.12f, 1f);
            var c = Color.HSVToRGB(hue, 0.85f, 1f);
            float fade = 1f - (ring - 1f) / rainbowRings;
            _ringBlock.SetColor(ColorId, new Color(c.r, c.g, c.b, (ring == 1 ? 0.95f : 0.55f) * fade * (0.75f + 0.25f * surge) * opacity));
            r.SetPropertyBlock(_ringBlock);
        }
    }

    SpriteRenderer Layer(string name, int order)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.gameObject.layer = _sr.gameObject.layer;
        r.transform.SetParent(transform, false);
        r.sortingLayerID = _sr.sortingLayerID;
        r.sortingOrder = _sr.sortingOrder + order;
        return r;
    }

    // Charging up: her bloom surging in beats, and lightning crackling round her - each bolt a
    // flash of a few frames somewhere on her, then somewhere else; more of them on each surge.
    void Power()
    {
        float beat = Mathf.Repeat((Time.time + _phase) * pulsesPerSecond, 1f);
        float surge = beat < 0.15f ? beat / 0.15f : 1f - (beat - 0.15f) / 0.85f;
        surge *= surge;
        Rainbow(surge);
        if (_bolts.Count == 0)
        {
            for (int i = 0; i < bolts; i++) _bolts.Add(Layer("POWER BOLT", i % 2 == 0 ? 3 : -2));
            _boltUntil = new float[bolts];
        }
        bool on = _sr.enabled;
        var drawn = RoadFoe.Drawn(_sr);
        var parent = transform.lossyScale;
        float k = _pixel * 100f;   // (the bolts' art pixels: hers)
        var art = Bolts();
        for (int i = 0; i < _bolts.Count; i++)
        {
            var b = _bolts[i];
            if (!on) { b.enabled = false; continue; }
            if (Time.time >= _boltUntil[i])
            {
                // A new flash (or a dark moment between), more often on a surge.
                bool flash = Random.value < 0.35f + 0.55f * surge;
                _boltUntil[i] = Time.time + Random.Range(0.04f, 0.1f);
                b.enabled = flash;
                if (!flash) continue;
                b.sprite = art[Random.Range(0, art.Length)];
                // Somewhere round her body: hugging its sides and over her head, pointing up.
                float side = Random.value < 0.5f ? -1f : 1f;
                float x = drawn.center.x + side * drawn.extents.x * Random.Range(0.35f, 1.05f);
                float y = drawn.min.y + drawn.size.y * Random.Range(0f, 0.75f);
                b.transform.position = new Vector3(x, y, transform.position.z - 0.001f);
                b.transform.rotation = Quaternion.Euler(0f, 0f, side * Random.Range(-10f, 35f));
                float stretch = Random.Range(0.7f, 1.2f);
                b.transform.localScale = new Vector3((Random.value < 0.5f ? -1f : 1f) * k / Mathf.Abs(parent.x), k * stretch / Mathf.Abs(parent.y), 1f);
                var c = Random.value < 0.25f ? Color.white : Random.value < 0.5f ? auraInner : auraOuter;
                var lit = Color.Lerp(c, Color.white, 0.3f);
                b.color = new Color(lit.r, lit.g, lit.b, opacity);
            }
        }
    }

    void Start()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
        if (!_sr || !_sr.sprite) { enabled = false; return; }
        _pixel = Mathf.Abs(_sr.transform.lossyScale.y) / _sr.sprite.pixelsPerUnit;
        _phase = Random.value * 6f;
        MakeRainbow();

    }

    void LateUpdate()
    {
        if (!_sr) return;
        // Her sparks, once she's there to see (the summoned witch comes hidden, in a strike).
        if (!_motes && _sr.enabled) { _motes = GateMotes.Add(_sr, GateMotes.Kind.Sparks, new Color(sparks.r, sparks.g, sparks.b, opacity)); if (_motes) { _motes.fromBody = true; _motes.count = 16; } }
        // Floating: lifted off the road, bobbing slowly (on top of wherever else she's put).
        float up = (lift + bob * Mathf.Sin((Time.time + _phase) * Mathf.PI * 2f / bobSeconds)) * _pixel;
        if (float.IsNaN(_baseY)) _baseY = 0f;
        transform.position += Vector3.up * (up - _baseY);
        _baseY = up;
        Power();
        // Her magic under her feet: little sparkles, orange and purple, twinkling up.
        if (_sr.enabled && Time.time >= _nextSparkle)
        {
            _nextSparkle = Time.time + 0.09f;
            var drawn = RoadFoe.Drawn(_sr);
            var at = new Vector3(drawn.center.x + Random.Range(-0.5f, 0.5f) * drawn.size.x * 0.7f, drawn.min.y - _pixel * Random.Range(0f, 3f), transform.position.z - 0.01f);
            var c = Random.value < 0.5f ? sparks : bloom;
            FoeFx.Sparkle(at, new Color(c.r, c.g, c.b, opacity), Random.Range(0.6f, 1.1f));
        }
    }
}
