using UnityEngine;

// ARcade: the final dragon's prize (RoadBoss: its look that calls up zombies) - Frakassets' dark crystal
// (Resources/Collect/Prize/crystal_dark: 24 frames of 64 px in a strip), exploding up out of the dragon as it falls,
// spinning as it comes and slowing, then hovering there above the road a few seconds - bobbing,
// its purple bloom breathing round it and pixel sparks drifting off it - then flying to the knight, faster and
// faster, and bursting into her (Collected: the witch's ending, LevelPortal).
public class CrystalPrize : MonoBehaviour
{
    const int Frames = 24, Cell = 64;
    const float Fps = 14f, Size = 1.2f;            // (Size: her pixels per crystal pixel)
    const float RiseSeconds = 1.1f, HoverSeconds = 6f, HoverPixels = 50f, SpinTurns = 3f;   // (RiseSeconds: its burst out of the dragon)
    /// It's gone into her.
    public static System.Action Collected;
    /// The final dragon's fallen and its crystal's not yet gone into her: the run's over bar that (LevelPortal: no fruit visit, no gate).
    public static bool Coming;
    public const int Points = 50000;
    static readonly Color Purple = new Color(0.72f, 0.3f, 1f, 1f);
    static Sprite[] s_frames;

    SpriteRenderer _sr, _bloom, _core;
    Vector3 _from;
    float _born, _lastFrames, _homeSpeed, _nextSpark;
    PlayerScriptARIANAClips _player;
    SpriteRenderer _playerBody;

    /// Out of the fallen dragon at `at`, on its layer.
    public static void Spawn(Vector3 at, int layer)
    {
        if (s_frames == null)
        {
            var tex = Resources.Load<Texture2D>("Collect/Prize/crystal_dark");
            if (!tex) return;
            tex.filterMode = FilterMode.Point;
            s_frames = new Sprite[Frames];
            for (int i = 0; i < Frames; i++)
                s_frames[i] = Sprite.Create(tex, new Rect(i * Cell, 0, Cell, Cell), new Vector2(0.5f, 0.5f), 100f);
        }
        var go = new GameObject("CRYSTAL PRIZE");
        go.layer = layer;
        go.transform.position = at;
        go.AddComponent<CrystalPrize>();
        // (it bursts out: a blast of its purple and white, sparks and blood flung out of the dragon)
        FoeFx.MagicBurst(at, Purple, 3f, ChestGem.OverKnightOrder + 3);
        FoeFx.MagicBurst(at, Color.white, 1.6f, ChestGem.OverKnightOrder + 4);
        FoeFx.Gore(at, Purple, 1.6f, 22);
        for (int i = 0; i < 24; i++) FoeFx.Sparkle(at + (Vector3)Random.insideUnitCircle * 0.5f, i % 3 == 0 ? Color.white : Purple, 1f);
        var sound = Resources.Load<AudioClip>("Collect/Traps/Sounds/hell_hand_explode");
        if (sound) PitAudio.PlayClip(sound, 1f);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Heavy);
    }

    void Start()
    {
        _from = transform.position;
        _born = Time.time;
        _player = EndlessKnightDirector.Player;
        var body = _player ? _player.transform.Find("ARIANA") : null;
        _playerBody = body ? body.GetComponent<SpriteRenderer>() : null;
        _lastFrames = _player ? _player.AccumulatedFrames : 0f;
        transform.localScale = Vector3.one * (SpikeTrap.PixelSize / 0.01f * Size);
        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = s_frames[0];
        _sr.sortingLayerName = ChestGem.KnightLayer;
        _sr.sortingOrder = ChestGem.OverKnightOrder + 2;
        // Its bloom: a wide soft purple glow and a hotter core, breathing (Update).
        _bloom = Glow("BLOOM", 2.6f, ChestGem.OverKnightOrder);
        _core = Glow("CORE", 1.3f, ChestGem.OverKnightOrder + 1);
        var sparks = GateMotes.Add(_sr, GateMotes.Kind.Sparks, Purple);
        if (sparks) sparks.count = 24;
        FoeFx.MagicBurst(transform.position, Purple, 1.4f, ChestGem.OverKnightOrder + 3);
    }

    SpriteRenderer Glow(string name, float scale, int order)
    {
        var g = new GameObject(name).AddComponent<SpriteRenderer>();
        g.gameObject.layer = gameObject.layer;
        g.transform.SetParent(transform, false);
        g.sprite = FoeFx.GlowSprite;
        // (the glow sprite's size to the crystal's: its 64 px, times `scale`)
        g.transform.localScale = Vector3.one * (Cell / 100f * scale / FoeFx.GlowSprite.bounds.size.x);
        g.sortingLayerName = ChestGem.KnightLayer;
        g.sortingOrder = order;
        return g;
    }

    void Update()
    {
        if (!_player || !_playerBody) { Destroy(gameObject); return; }
        float age = Time.time - _born;
        _sr.sprite = s_frames[(int)(age * Fps) % Frames];
        float breathe = 0.5f + 0.5f * Mathf.Sin(age * 4f);
        _bloom.color = new Color(Purple.r, Purple.g, Purple.b, 0.35f + 0.2f * breathe);
        _core.color = new Color(0.9f, 0.6f, 1f, 0.45f + 0.25f * breathe);

        // (with the road, as the knight walks: it hovers over its spot)
        float now = _player.AccumulatedFrames;
        float slide = (now - _lastFrames) * EndlessKnightDirector.RoadUnitsPerFrame;
        _lastFrames = now;
        _from.x -= slide;
        var p = transform.position;
        float px = SpikeTrap.PixelSize;
        var hover = new Vector3(_from.x, SpikeTrap.PlayerFeetY + HoverPixels * px, _from.z - 0.02f);
        if (age < RiseSeconds + HoverSeconds)
        {
            float k = Mathf.Clamp01(age / RiseSeconds);
            float ease = 1f + 2.7f * Mathf.Pow(k - 1f, 3f) + 1.7f * Mathf.Pow(k - 1f, 2f);   // (flung out, past its place, and back)
            p = Vector3.LerpUnclamped(_from, hover, ease) + Vector3.up * Mathf.Sin(age * 2.4f) * 3f * px * ease;
            // Spinning as it comes down, slowing to upright as it settles.
            transform.rotation = Quaternion.Euler(0f, 0f, SpinTurns * 360f * (1f - ease) * (1f - ease));
            // Pixel sparks drifting off it, purple and white.
            if (Time.time >= _nextSpark)
            {
                _nextSpark = Time.time + 0.06f;
                var b = _sr.bounds;
                var at = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), p.z - 0.01f);
                FoeFx.Sparkle(at, Random.value < 0.75f ? Purple : Color.white, Random.Range(0.5f, 0.9f));
            }
        }
        else
        {
            // Off to her middle, speeding up, shrinking a little as it goes into her.
            var to = (Vector3)(Vector2)_playerBody.bounds.center - p;
            to.z = 0f;
            _homeSpeed = Mathf.Min(16f, Mathf.Max(1.5f, _homeSpeed) + 22f * Time.deltaTime);
            float step = _homeSpeed * Time.deltaTime;
            if (to.magnitude <= Mathf.Max(step, 0.05f)) { Absorb(); return; }
            p += to.normalized * step;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * (SpikeTrap.PixelSize / 0.01f * Size * Mathf.Clamp(to.magnitude / 0.8f, 0.45f, 1f));
        }
        transform.position = p;
    }

    void Absorb()
    {
        var at = _playerBody.bounds.center;
        FoeFx.MagicBurst(at, Purple, 2.2f, ChestGem.OverKnightOrder + 3);
        FoeFx.MagicBurst(at, Color.white, 1.2f, ChestGem.OverKnightOrder + 4);
        for (int i = 0; i < 14; i++) FoeFx.Sparkle(at + (Vector3)Random.insideUnitCircle * 0.25f, i % 3 == 0 ? Color.white : Purple, 0.9f);
        var sound = Resources.Load<AudioClip>("Collect/Sounds/row_item");
        if (sound) PitAudio.PlayClip(sound, 1f);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Heavy);
        if (PitScoreManager.Instance) PitScoreManager.Instance.AddScore(Points);
        Collected?.Invoke();
        Destroy(gameObject);
    }
}
