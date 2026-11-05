using System.Collections.Generic;
using UnityEngine;

// ARcade: a glowing outline round a whole sprite, to stand out on a dark road
// (the revenants' aqua, the skeletons' purple, Fantasy #25's green): its
// shape in the outline colour (SpatialEmulator/SpriteSolid, per renderer),
// nudged a pixel all round behind it, and a soft halo breathing behind that.
// They follow its frame, facing, draw order, mask and visibility every frame.
[DefaultExecutionOrder(200)]   // after its animation
public class SpriteOutline : MonoBehaviour
{
    public Color color = Color.white;
    [Tooltip("The soft glow behind it, as a share of the outline's opacity (0: none).")]
    public float halo = 0.4f;
    [Tooltip("The halo's own colour (clear: the outline's) - a black outline with a smoky grey haze round it.")]
    public Color haloColor = Color.clear;
    [Tooltip("The halo flickers like fire (a burning aura) rather than breathing.")]
    public bool fire;
    [Tooltip("The halo bright out toward its edge (a neon aura), breathing.")]
    public bool full;
    [Tooltip("How far the halo reaches round it (1: its usual size; less: hugging it).")]
    public float reach = 1f;

    static Material s_solid;
    static Sprite s_fireGlow;

    // A fuller glow than FoeFx's (bright out toward its edge, then gone): a fire's aura round a body.
    static Sprite FireGlow()
    {
        if (s_fireGlow) return s_fireGlow;
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(1f - d * d) * Mathf.Clamp01(1.15f - d) * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        s_fireGlow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 8f);   // (8 units across, as FoeFx's)
        return s_fireGlow;
    }
    static readonly int ColorId = Shader.PropertyToID("_Color");
    [Tooltip("A neon bloom along its edges (clear: none): its shape again in this colour, bloomPixels rings out, fading.")]
    public Color bloom = Color.clear;
    public int bloomPixels = 3;
    readonly List<SpriteRenderer> _bloom = new List<SpriteRenderer>();
    readonly List<float> _bloomAlpha = new List<float>();
    MaterialPropertyBlock _bloomBlock;
    SpriteRenderer _sr, _halo;
    readonly List<SpriteRenderer> _edge = new List<SpriteRenderer>();
    float _phase;

    public static SpriteOutline Add(SpriteRenderer sprite, Color color, float halo = 0.4f, Color haloColor = default)
    {
        if (!sprite || (color.a <= 0f && haloColor.a <= 0f)) return null;   // (a halo alone: an aura)
        var outline = sprite.gameObject.AddComponent<SpriteOutline>();
        outline.color = color;
        outline.halo = halo;
        outline.haloColor = haloColor;
        return outline;
    }

    void Start()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (!s_solid)
        {
            var shader = Shader.Find("SpatialEmulator/SpriteSolid");
            if (shader) s_solid = new Material(shader) { name = "Sprite Outline" };
        }
        if (!_sr || !s_solid) { enabled = false; return; }
        _phase = Random.value * 6f;
        var block = new MaterialPropertyBlock();
        block.SetColor(ColorId, color);
        float px = _sr.sprite ? 1f / _sr.sprite.pixelsPerUnit : 0.01f;
        for (int i = 0; i < (color.a > 0f ? 8 : 0); i++)
        {
            float a = i * Mathf.PI / 4f;
            var go = new GameObject("OUTLINE");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(Mathf.Round(Mathf.Cos(a)) * px, Mathf.Round(Mathf.Sin(a)) * px, 0.0005f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sharedMaterial = s_solid;
            r.SetPropertyBlock(block);
            _edge.Add(r);
        }
        if (bloom.a > 0f)
        {
            // Rings of its shape, one pixel further out each, fainter each: a glow that hugs its edges.
            for (int ring = 1; ring <= Mathf.Max(1, bloomPixels); ring++)
            {
                int dirs = ring == 1 ? 8 : 12;
                float fade = Mathf.Pow(1f - (ring - 1f) / Mathf.Max(1, bloomPixels), 1.4f);
                for (int i = 0; i < dirs; i++)
                {
                    float a = i * Mathf.PI * 2f / dirs;
                    var go = new GameObject("BLOOM");
                    go.layer = gameObject.layer;
                    go.transform.SetParent(transform, false);
                    int at = ring + (color.a > 0f ? 1 : 0);   // (outside a crisp edge, if it has one)
                    go.transform.localPosition = new Vector3(Mathf.Round(Mathf.Cos(a) * at) * px, Mathf.Round(Mathf.Sin(a) * at) * px, 0.0006f);
                    var r = go.AddComponent<SpriteRenderer>();
                    r.sharedMaterial = s_solid;
                    _bloom.Add(r);
                    _bloomAlpha.Add((ring == 1 ? 0.5f : 0.26f) * fade * (color.a > 0f ? 1.6f : 1f));   // (stronger past an edge)
                }
            }
        }
        if (halo > 0f)
        {
            var go = new GameObject("HALO");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            _halo = go.AddComponent<SpriteRenderer>();
            _halo.sprite = fire || full ? FireGlow() : FoeFx.GlowSprite;
        }
    }

    void OnDestroy()
    {
        foreach (var r in _edge) if (r) Destroy(r.gameObject);
        foreach (var r in _bloom) if (r) Destroy(r.gameObject);
        if (_halo) Destroy(_halo.gameObject);
    }

    void LateUpdate()
    {
        if (!_sr) return;
        bool on = _sr.enabled && _sr.sprite && _sr.color.a > 0.05f;
        foreach (var r in _edge)
        {
            r.sprite = _sr.sprite;
            r.flipX = _sr.flipX;
            r.sortingLayerID = _sr.sortingLayerID;
            r.sortingOrder = _sr.sortingOrder - 1;
            r.maskInteraction = _sr.maskInteraction;
            r.enabled = on;
        }
        if (_bloom.Count > 0)
        {
            float breathe = 0.85f + 0.15f * Mathf.Sin(Time.time * 3.3f + _phase);
            _bloomBlock ??= new MaterialPropertyBlock();
            for (int i = 0; i < _bloom.Count; i++)
            {
                var r = _bloom[i];
                r.sprite = _sr.sprite;
                r.flipX = _sr.flipX;
                r.sortingLayerID = _sr.sortingLayerID;
                r.sortingOrder = _sr.sortingOrder - 1;
                r.maskInteraction = _sr.maskInteraction;
                r.enabled = on;
                _bloomBlock.SetColor(ColorId, new Color(bloom.r, bloom.g, bloom.b, bloom.a * _bloomAlpha[i] * breathe * _sr.color.a));
                r.SetPropertyBlock(_bloomBlock);
            }
        }
        if (!_halo) return;
        // A soft glow round its body, breathing.
        var b = RoadFoe.Drawn(_sr);
        var world = transform.lossyScale;
        float pulse = fire
            ? 0.75f + 0.15f * Mathf.Sin(Time.time * 7f + _phase) + 0.1f * Mathf.Sin(Time.time * 17f + _phase * 2f)
            : 0.85f + 0.15f * Mathf.Sin(Time.time * 3f + _phase);
        float unit = FoeFx.GlowSprite.bounds.size.x;
        _halo.transform.position = new Vector3(b.center.x, b.center.y, transform.position.z + 0.001f);
        float wide = fire || full ? 2.3f : 1.6f, tall = fire || full ? 1.7f : 1.3f;   // (a fire's aura: bigger, licking up round it)
        wide = 1f + (wide - 1f) * reach;
        tall = 1f + (tall - 1f) * reach;
        _halo.transform.localScale = new Vector3(b.size.x * wide / Mathf.Max(1e-4f, Mathf.Abs(world.x)) / unit, b.size.y * tall / Mathf.Max(1e-4f, Mathf.Abs(world.y)) / unit, 1f);
        var hc = haloColor.a > 0f ? haloColor : color;
        _halo.color = new Color(hc.r, hc.g, hc.b, Mathf.Clamp01(hc.a * halo * pulse * (fire ? 1.5f : 1f)) * _sr.color.a);
        _halo.sortingLayerID = _sr.sortingLayerID;
        _halo.sortingOrder = _sr.sortingOrder - 2;
        _halo.maskInteraction = _sr.maskInteraction;
        _halo.enabled = on;
    }
}
