using UnityEngine;

// ARcade: the player lights up as her stats change - a white flash when
// she's hurt, a green outline blinking when she heals (slowly while she
// rests and heals), a purple one when she takes in mana - and gold for her spells: flashing as one is cast, and
// shimmering while a Sword or Shield Buff is on - and asleep with her
// health and mana full, a steady gold shimmer with a golden aura rising off
// her (Aura; green while she rests and heals, purple asleep). Her own material (a Shader Graph sprite) ignores the
// renderer's colour, so these are copies of her sprite in a flat colour
// (SpatialEmulator/SpriteSolid, its colour per copy): one over her for the flash, and four
// nudged a pixel left, right, up and down behind her for the outline. They
// follow her sprite, draw order and visibility every frame.
// EndlessKnightBuilder adds this to the player's ARIANA sprite.
[DefaultExecutionOrder(200)]   // after her animation and sorting are set
public class PlayerGlow : MonoBehaviour
{
    public Material solidMaterial;
    public Color shieldColor = new Color32(255, 214, 64, 255);
    public Color shieldShine = new Color32(255, 250, 220, 255);
    [Tooltip("A buff's last seconds blink, so its end isn't a surprise.")]
    public float buffWarning = 3f;
    public PlayerStats stats;

    public Color hurtColor = new Color(1f, 1f, 1f, 0.9f);
    public float hurtTime = 0.3f;
    public int hurtBlinks = 2;
    public Color healColor = new Color32(96, 232, 96, 255);
    public float healTime = 0.7f;
    public Color manaColor = new Color32(176, 96, 255, 255);
    public float manaTime = 0.6f;
    public int outlineBlinks = 3;
    [Tooltip("One slow blink of the green outline while she rests and heals.")]
    public float restBlinkSeconds = 1.6f;

    SpriteRenderer _body, _flash;
    PlayerScriptARIANAClips _player;
    MaterialPropertyBlock _block;
    static readonly int ColorId = Shader.PropertyToID("_Color");
    readonly SpriteRenderer[] _outline = new SpriteRenderer[4];
    int _health = int.MinValue;
    float _hurtAt = -10f, _healAt = -10f, _manaAt = -10f;

    void Awake()
    {
        _body = GetComponent<SpriteRenderer>();
        _flash = Copy("Hurt Flash");
        for (int i = 0; i < 4; i++) _outline[i] = Copy("Outline " + i);
    }

    SpriteRenderer Copy(string name, Material material = null)
    {
        var go = new GameObject(name);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = material ? material : solidMaterial;
        sr.enabled = false;
        return sr;
    }

    void OnEnable()
    {
        StatsControl.Healed += OnHealed;
        StatsControl.ManaGained += OnMana;
    }

    void OnDisable()
    {
        StatsControl.Healed -= OnHealed;
        StatsControl.ManaGained -= OnMana;
    }

    void OnHealed() => _healAt = Time.time;
    void OnMana() => _manaAt = Time.time;

    void Start()
    {
        _player = GetComponentInParent<PlayerScriptARIANAClips>();
        if (!stats)
        {
            var player = GetComponentInParent<PlayerScriptARIANAClips>();
            stats = player && player.playerStats ? player.playerStats : FindAnyObjectByType<PlayerStats>();
        }
    }

    void LateUpdate()
    {
        if (stats)
        {
            // Heals and mana come as events (they flash even when the bar
            // is already full); a hit is the health going down.
            if (_health != int.MinValue && stats.health < _health) _hurtAt = Time.time;
            _health = stats.health;
        }

        bool visible = _body && _body.enabled && _body.sprite && _body.gameObject.activeInHierarchy;

        // Hurt: white over her, blinking.
        float t = Time.time - _hurtAt;
        bool flash = visible && t < hurtTime && Mathf.Repeat(t / hurtTime * hurtBlinks, 1f) < 0.5f;
        Show(_flash, flash, hurtColor, 1, Vector3.zero);

        // Healed or mana: the outline blinking (healing wins when both);
        // under them, gold: flashing while a spell is cast, and shimmering
        // like a star while a buff (Sword or Shield, or both) is on.
        float heal = Time.time - _healAt, mana = Time.time - _manaAt;
        bool golden = _player && _player.IsSleeping && stats && stats.health >= stats.MaxHP && stats.mana >= stats.MaxMP;
        // The aura: green while she rests and heals, purple asleep (healing
        // and getting mana back), gold asleep with both full.
        bool sleeping = _player && _player.IsSleeping;
        bool restHealing = _player && _player.IsResting && !sleeping && stats && stats.health < stats.MaxHP;
        Color auraTint = golden ? auraColor : sleeping ? manaColor : healColor;
        Aura(visible && (golden || sleeping || restHealing), auraTint);
        Color color = default;
        bool outline = false;
        if (heal < healTime && Mathf.Repeat(heal / healTime * outlineBlinks, 1f) < 0.6f) { color = healColor; outline = true; }
        else if (mana < manaTime && Mathf.Repeat(mana / manaTime * outlineBlinks, 1f) < 0.6f) { color = manaColor; outline = true; }
        else if (golden)
        {
            // Asleep, rested to the full: gold, shimmering, steady.
            color = Color.Lerp(shieldColor, shieldShine, Mathf.PingPong(Time.time * 2f, 1f));
            outline = true;
        }
        else if (PlayerBuffs.ShieldShowing || PlayerBuffs.Sword)
        {
            // A buff's gold outranks resting's slow green or purple (its
            // last seconds' blinks included).
            color = Color.Lerp(shieldColor, shieldShine, Mathf.PingPong(Time.time * 6f, 1f));
            outline = (PlayerBuffs.ShieldShowing && Showing(PlayerBuffs.ShieldUntil)) || (PlayerBuffs.Sword && Showing(PlayerBuffs.SwordUntil));
        }
        else if (_player && _player.IsSleeping && stats && (stats.health < stats.MaxHP || stats.mana < stats.MaxMP))
        {
            // Asleep, healing and getting mana back: purple, blinking slowly.
            color = manaColor;
            outline = Mathf.Repeat(Time.time / restBlinkSeconds, 1f) < 0.5f;
        }
        else if (_player && _player.IsResting && stats && stats.health < stats.MaxHP)
        {
            // Resting and healing: green, blinking slowly (with the health bar's pulse).
            color = healColor;
            outline = Mathf.Repeat(Time.time / restBlinkSeconds, 1f) < 0.5f;
        }
        else if (_player && _player.IsCasting)
        {
            color = shieldColor;
            outline = Mathf.Repeat(Time.time * 8f, 1f) < 0.55f;
        }

        float pixel = _body && _body.sprite ? 1f / _body.sprite.pixelsPerUnit : 0.01f;
        Show(_outline[0], visible && outline, color, -1, new Vector3(-pixel, 0f));
        Show(_outline[1], visible && outline, color, -1, new Vector3(pixel, 0f));
        Show(_outline[2], visible && outline, color, -1, new Vector3(0f, pixel));
        Show(_outline[3], visible && outline, color, -1, new Vector3(0f, -pixel));

    }

    // ARcade: the aura - a soft glow behind her, brightest at her
    // and rising off the top of her like a flame, pulsing slowly, with gold
    // motes drifting up out of it; in her art's pixels (point-filtered), so
    // it's pixel art too. It fades in and out.
    [Header("Aura (resting: green; asleep: purple; asleep and full: gold)")]
    public Color auraColor = new Color32(255, 172, 20, 255);
    [Tooltip("The aura's size in her art's pixels, across and up.")]
    public Vector2Int auraPixels = new Vector2Int(40, 56);
    public float auraFadeSeconds = 0.6f;
    public int motes = 6;

    SpriteRenderer _aura;
    readonly System.Collections.Generic.List<SpriteRenderer> _motes = new System.Collections.Generic.List<SpriteRenderer>();
    float _auraLevel;
    Color _auraTint;
    static Sprite s_auraSprite, s_moteSprite;

    void Aura(bool on, Color tint)
    {
        // (Into a new colour gently, as she goes from resting to asleep to full.)
        _auraTint = _auraLevel <= 0f ? tint : Color.Lerp(_auraTint, tint, Time.deltaTime * 3f);
        _auraLevel = Mathf.MoveTowards(_auraLevel, on ? 1f : 0f, Time.deltaTime / auraFadeSeconds);
        bool showing = _auraLevel > 0f && _body && _body.sprite;
        if (!showing)
        {
            if (_aura && _aura.enabled) { _aura.enabled = false; foreach (var m in _motes) m.enabled = false; }
            return;
        }
        float ppu = _body.sprite.pixelsPerUnit;
        if (!_aura)
        {
            _aura = Copy("Aura");
            for (int i = 0; i < motes; i++) _motes.Add(Copy("Aura Mote " + i));
        }
        // Where she lies: the middle of her drawn frame across, its bottom.
        var b = _body.sprite.bounds;
        var foot = new Vector3(b.center.x * (_body.flipX ? -1f : 1f), b.min.y + 2f / ppu, 0f);
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.4f);
        _aura.sprite = AuraSprite(ppu);
        _aura.flipX = false;
        Paint(_aura, _auraTint * new Color(1f, 1f, 1f, pulse * _auraLevel), -3);
        _aura.transform.localPosition = foot;
        _aura.transform.localScale = new Vector3(1f, 0.95f + 0.05f * Mathf.Sin(Time.time * 3.1f), 1f);
        // Motes: each rises from her through the aura and fades, in turn.
        for (int i = 0; i < _motes.Count; i++)
        {
            var m = _motes[i];
            float t = Mathf.Repeat(Time.time / 2.2f + i / (float)_motes.Count, 1f);
            float x = (Mathf.PerlinNoise(i * 3.7f, Time.time * 0.3f) - 0.5f) * auraPixels.x * 0.6f;
            float y = Mathf.Round(t * auraPixels.y);
            m.sprite = MoteSprite(ppu);
            m.flipX = false;
            Paint(m, Color.Lerp(_auraTint, Color.white, 0.5f) * new Color(1f, 1f, 1f, (1f - t) * _auraLevel), 2);
            m.transform.localPosition = foot + new Vector3(Mathf.Round(x) / ppu, y / ppu, 0f);
        }
    }

    void Paint(SpriteRenderer copy, Color color, int order)
    {
        copy.enabled = true;
        _block ??= new MaterialPropertyBlock();
        copy.GetPropertyBlock(_block);
        _block.SetColor(ColorId, color);
        copy.SetPropertyBlock(_block);
        copy.sortingLayerID = _body.sortingLayerID;
        copy.sortingOrder = _body.sortingOrder + order;
    }

    // A flame-like glow, pivoted at its bottom middle: strongest low in the
    // middle, narrowing and fading as it rises; stepped into a few levels,
    // like hand-shaded pixel art.
    Sprite AuraSprite(float ppu)
    {
        if (s_auraSprite) return s_auraSprite;
        int w = auraPixels.x, h = auraPixels.y;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Golden Aura" };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = y / (float)(h - 1);
                float half = Mathf.Lerp(0.5f, 0.12f, v);   // narrower toward the top
                float u = Mathf.Abs((x + 0.5f) / w - 0.5f) / half;
                float a = Mathf.Clamp01(1.4f * Mathf.Clamp01(1f - u * u) * Mathf.Pow(1f - v, 1.1f)) * 0.85f;
                a = Mathf.Floor(a * 5f) / 5f;   // a few flat levels
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        s_auraSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), ppu);
        return s_auraSprite;
    }

    static Sprite MoteSprite(float ppu)
    {
        if (s_moteSprite) return s_moteSprite;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Golden Mote" };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);
        s_moteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), ppu);
        return s_moteSprite;
    }

    // On, except blinking through a buff's last seconds.
    bool Showing(float until)
    {
        float left = until - Time.time;
        return left > buffWarning || Mathf.Repeat(Time.time * 8f, 1f) < 0.5f;
    }

    // Her sprite in `color`, `order` before or after her, nudged by `offset` (her sprite's units).
    void Show(SpriteRenderer copy, bool on, Color color, int order, Vector3 offset)
    {
        if (copy.enabled != on) copy.enabled = on;
        if (!on) return;
        copy.sprite = _body.sprite;
        copy.flipX = _body.flipX;
        copy.flipY = _body.flipY;
        _block ??= new MaterialPropertyBlock();
        copy.GetPropertyBlock(_block);
        _block.SetColor(ColorId, color);
        copy.SetPropertyBlock(_block);
        copy.sortingLayerID = _body.sortingLayerID;
        copy.sortingOrder = _body.sortingOrder + order;
        copy.transform.localPosition = offset;
    }
}
