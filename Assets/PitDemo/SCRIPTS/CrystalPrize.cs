using UnityEngine;

// ARcade: the final dragon's prize (RoadBoss: its look that calls up zombies) - Frakassets' dark crystal
// (Resources/Collect/Prize/crystal_dark: 24 frames of 64 px in a strip), exploding up out of the dragon as it falls,
// spinning as it comes and slowing, then hovering there above the road a few seconds - bobbing,
// its purple bloom breathing round it and pixel sparks drifting off it - then it wakes: it drifts over the
// knight, spins up, and sucks everything she gathered out of her - fruit, flowers, mushrooms, candy corn, keys, eggs, spiralling
// into it (CrystalDrainItem) - swelling as it feeds. Then it hangs there, full (Collected: the witch's ending, LevelPortal).
public class CrystalPrize : MonoBehaviour
{
    const int Frames = 24, Cell = 64;
    const float Fps = 14f, Size = 1.2f;            // (Size: her pixels per crystal pixel)
    const float RiseSeconds = 1.1f, HoverSeconds = 6f, HoverPixels = 50f, SpinTurns = 3f;   // (RiseSeconds: its burst out of the dragon)
    const float MoveSeconds = 0.9f, FeastSeconds = 3.6f, ItemsPerSecond = 20f, SpinUpSeconds = 1.3f, FeastFps = 60f, FullFps = 20f;
    const float AheadPixels = 34f, OverPixels = 92f, FullGrowth = 0.28f;   // (its place over the knight, clear of her and the witch)
    /// It's fed on all she gathered.
    public static System.Action Collected;
    /// The final dragon's fallen and its crystal hasn't finished feeding: the run's over bar that (LevelPortal: no fruit visit, no gate).
    public static bool Coming;
    public const int Points = 50000;
    static readonly Color Purple = new Color(0.72f, 0.3f, 1f, 1f);
    static Sprite[] s_frames;

    SpriteRenderer _sr, _bloom, _core;
    Vector3 _from;
    float _born, _lastFrames, _nextSpark, _spin, _feastStart = -1f, _spawnDue, _pulse;
    Vector3 _spot;
    Sprite[] _items;
    int _spawned, _gulped, _total;
    bool _full;
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
        float feast = _feastStart < 0f ? -1f : Time.time - _feastStart;
        float fps = _full ? FullFps : feast < 0f ? Fps : Mathf.Lerp(Fps, FeastFps, feast / SpinUpSeconds);
        _spin += fps * Time.deltaTime;
        _sr.sprite = s_frames[(int)_spin % Frames];
        float breathe = 0.5f + 0.5f * Mathf.Sin(age * 4f);
        float fed = _total > 0 ? (float)_gulped / _total : 0f;
        _bloom.color = new Color(Purple.r, Purple.g, Purple.b, 0.35f + 0.2f * breathe + 0.2f * fed);
        _core.color = new Color(0.9f, 0.6f, 1f, 0.45f + 0.25f * breathe + 0.2f * fed);

        // (with the road, as the knight walks: it hovers over its spot)
        float now = _player.AccumulatedFrames;
        float slide = (now - _lastFrames) * EndlessKnightDirector.RoadUnitsPerFrame;
        _lastFrames = now;
        _from.x -= slide;
        var p = transform.position;
        float px = SpikeTrap.PixelSize;
        var hover = new Vector3(_from.x, SpikeTrap.PlayerFeetY + HoverPixels * px, _from.z - 0.02f);
        if (feast < 0f)
        {
            float k = Mathf.Clamp01(age / RiseSeconds);
            float ease = 1f + 2.7f * Mathf.Pow(k - 1f, 3f) + 1.7f * Mathf.Pow(k - 1f, 2f);   // (flung out, past its place, and back)
            p = Vector3.LerpUnclamped(_from, hover, ease) + Vector3.up * Mathf.Sin(age * 2.4f) * 3f * px * ease;
            // Spinning as it comes down, slowing to upright as it settles.
            transform.rotation = Quaternion.Euler(0f, 0f, SpinTurns * 360f * (1f - ease) * (1f - ease));
            // Pixel sparks drifting off it, purple and white.
            Sparks(p, 0.06f);
            // (not while she's down or anything's up: it waits for her)
            if (age >= RiseSeconds + HoverSeconds && !_player.IsDead && !SpatialEmulator.Demo.DemoDialogue.Showing
                && !SpatialEmulator.Demo.PitDemoGame.IsPaused && !SpatialEmulator.Demo.PitDemoGame.ControlsLocked)
                BeginFeast(p);
        }
        else
        {
            // Over to its place above her, then feeding.
            float m = Mathf.SmoothStep(0f, 1f, feast / MoveSeconds);
            p = Vector3.Lerp(_from, _spot, m) + Vector3.up * Mathf.Sin(age * 2.4f) * 3f * px;
            transform.rotation = Quaternion.identity;
            if (!_full)
            {
                Sparks(p, 0.03f);
                if (feast >= MoveSeconds)
                {
                    _spawnDue += ItemsPerSecond * Time.deltaTime;
                    while (_spawnDue >= 1f && _spawned < _total) { _spawnDue -= 1f; SpitOne(); }
                }
                if (_spawned >= _total && _gulped >= _total) Finish();
            }
        }
        _pulse = Mathf.MoveTowards(_pulse, 0f, Time.deltaTime * 1.2f);
        transform.localScale = Vector3.one * (px / 0.01f * Size * (1f + FullGrowth * fed + _pulse));
        transform.position = p;
    }

    void Sparks(Vector3 p, float every)
    {
        if (Time.time < _nextSpark) return;
        _nextSpark = Time.time + every;
        var b = _sr.bounds;
        var at = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y), p.z - 0.01f);
        FoeFx.Sparkle(at, Random.value < 0.75f ? Purple : Color.white, Random.Range(0.5f, 0.9f));
    }

    // It wakes: the road's cleared, she's held, and it picks out what it'll take.
    void BeginFeast(Vector3 p)
    {
        _feastStart = Time.time;
        _from = p;
        float px = SpikeTrap.PixelSize;
        _spot = new Vector3(_playerBody.bounds.center.x + AheadPixels * px, SpikeTrap.PlayerFeetY + OverPixels * px, p.z);
        SpatialEmulator.Demo.PitDemoGame.ControlsLocked = true;
        EndlessKnightDirector.Hold = true;
        var director = FindAnyObjectByType<EndlessKnightDirector>();
        if (director) director.ClearRoad();

        var items = new System.Collections.Generic.List<Sprite>();
        var fruit = FruitTracker.Instance ? FruitTracker.Instance.AllFruit : null;
        if (fruit != null) foreach (var f in fruit) if (f) items.Add(f);
        foreach (var path in new[] { "Collect/Candy/Flowers3", "Collect/Candy/Road3", "Collect/Candy/Road4", "Collect/Candy/Road", "Collect/Candy/Road2", "Collect/Keys", "Collect/Eggs" })
            items.AddRange(Resources.LoadAll<Sprite>(path));
        for (int i = items.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (items[i], items[j]) = (items[j], items[i]); }
        _items = items.ToArray();
        _total = _items.Length > 0 ? Mathf.RoundToInt(FeastSeconds * ItemsPerSecond) : 0;
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Medium);
    }

    // The next one, out of the knight.
    void SpitOne()
    {
        int n = _spawned++;
        var at = _playerBody.bounds.center + (Vector3)(Random.insideUnitCircle * 0.12f);
        at.z = transform.position.z - 0.01f;
        CrystalDrainItem.Spawn(_items[n % _items.Length], at, this, gameObject.layer, ChestGem.OverKnightOrder + 3);
    }

    /// One's reached it.
    public void Gulp(Vector3 at)
    {
        _gulped++;
        _pulse = Mathf.Max(_pulse, 0.05f);
        FoeFx.Sparkle(at, Purple, 0.7f);
        if (_gulped % 3 != 0) return;
        PitAudio.Voice(LevelCollection.CandySound(), 0.3f, Mathf.Lerp(0.85f, 1.7f, (float)_gulped / Mathf.Max(1, _total)));
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Tick);
    }

    // Full: a burst, and the witch comes.
    void Finish()
    {
        _full = true;
        _pulse = 0.2f;
        var at = transform.position;
        FoeFx.MagicBurst(at, Purple, 2.2f, ChestGem.OverKnightOrder + 3);
        FoeFx.MagicBurst(at, Color.white, 1.2f, ChestGem.OverKnightOrder + 4);
        for (int i = 0; i < 14; i++) FoeFx.Sparkle(at + (Vector3)Random.insideUnitCircle * 0.25f, i % 3 == 0 ? Color.white : Purple, 0.9f);
        var sound = Resources.Load<AudioClip>("Collect/Sounds/row_item");
        if (sound) PitAudio.PlayClip(sound, 1f);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Heavy);
        if (PitScoreManager.Instance) PitScoreManager.Instance.AddScore(Points);
        Collected?.Invoke();
    }
}

// One of her things on its way into the dark crystal: from where it left her, round and round, faster and closer, shrinking.
public class CrystalDrainItem : MonoBehaviour
{
    const float ItemPixels = 18f, Turns = 1.6f;
    CrystalPrize _crystal;
    Vector3 _start;
    float _born, _life, _radius, _angle, _size;

    public static void Spawn(Sprite sprite, Vector3 at, CrystalPrize crystal, int layer, int order)
    {
        var go = new GameObject("CRYSTAL FEAST");
        go.layer = layer;
        go.transform.position = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = ChestGem.KnightLayer;
        sr.sortingOrder = order;
        var item = go.AddComponent<CrystalDrainItem>();
        item._crystal = crystal;
        item._start = at;
        item._born = Time.time;
        item._life = Random.Range(0.95f, 1.5f);
        item._size = SpikeTrap.PixelSize * sprite.pixelsPerUnit * ItemPixels / Mathf.Max(sprite.rect.width, sprite.rect.height);
        go.transform.localScale = Vector3.one * item._size;
    }

    void Update()
    {
        if (!_crystal) { Destroy(gameObject); return; }
        float k = (Time.time - _born) / _life;
        var centre = _crystal.transform.position;
        if (k >= 1f) { _crystal.Gulp(centre); Destroy(gameObject); return; }
        if (_radius <= 0f)
        {
            var away = _start - centre;
            _radius = Mathf.Max(0.05f, ((Vector2)away).magnitude);
            _angle = Mathf.Atan2(away.y, away.x);
        }
        float reach = _radius * (1f - k * k) * (1f + 0.3f * Mathf.Sin(k * Mathf.PI));   // (swung out a little, then in)
        float angle = _angle + Turns * Mathf.PI * 2f * k * k;
        transform.position = centre + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * reach + Vector3.back * 0.01f;
        transform.localScale = Vector3.one * (_size * Mathf.Lerp(1f, 0.35f, k * k));
        transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
    }
}
