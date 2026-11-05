using UnityEngine;

// A gem from ENDLESS KNIGHT's treasure chest (Diamonds & Gems, 28 px, 9
// spin frames): shot out of the chest by TreasureChest, it arcs up, falls
// and bounces on the road once - then flies to the knight by itself, faster
// and faster, all the way into her middle, and is picked up there in a little
// burst of its colour (points + the diamond collect sound): play keeps
// flowing (a tester's idea).
public class ChestGem : MonoBehaviour
{
    public Sprite[] frames;
    [Tooltip("Spin speed; 0 = a still icon, one of the frames at random (fruit in all its colours).")]
    public float fps = 12f;
    [Tooltip("Health restored on pickup (fruit, like the orange slices).")]
    public int healAmount;
    [Tooltip("Share of the mana bar restored on pickup (gems; set by TreasureChest so one chest's gems fill it).")]
    public float manaShare;
    [Tooltip("Size relative to the characters' pixels.")]
    public float scale = 0.45f;
    public float gravity = 6f;
    [Tooltip("Share of the fall speed kept on each bounce.")]
    public float bounce = 0.45f;
    [Tooltip("Bounces on the road before it's down (0: the one), each with bounceSound - level 3's eggs.")]
    public int bounces;
    [Tooltip("It comes down on the road on its bottom (not its middle) - level 3's big eggs.")]
    public bool sitOnBottom;
    public AudioClip bounceSound;
    public int scoreValue = 100;
    public AudioClip collectSound;
    [Tooltip("Picked up: these, little, bursting out of it and tumbling to the road (level 3's eggs: the egg's own).")]
    public Sprite[] miniEggs;
    public float lifetime = 8f;
    [Tooltip("After its first bounce: how fast it sets off to the knight, how hard it speeds up, and its top speed (units a second).")]
    public float homeSpeed = 1.5f, homeAccel = 25f, homeMax = 14f;
    [Tooltip("Once down: how long it sits there, to be seen, before it sets off to the knight (level 4's potion).")]
    public float restSeconds;
    [Tooltip("Viking food: eaten hurt, it heals healShare of her bar; eaten at full health, she's big for bigSeconds (PlayerBuffs.GoBig, bigSound) - either way flashing flashColor.")]
    public float bigSeconds;
    public float healShare;
    public Color flashColor = new Color(0.35f, 1f, 0.35f, 1f);
    public AudioClip bigSound, shrinkSound;

    /// The row item it counts as (a level's key, egg or potion: LevelCollection) - null: a fruit counts as itself.
    [HideInInspector] public Sprite trackAs;

    /// Launch velocity, set by TreasureChest before Start.
    [HideInInspector] public Vector2 velocity;

    /// Past level 1, a gem becomes a sweet (LevelCollection.Sweet: a gummy bear, a jelly bean...) - its mana and
    /// points the same, drawn the size of a fruit (times `size`).
    [System.NonSerialized] public bool candy;   // (a sweet: the candy sounds in place of collectSound)
    public void Sweeten(float size = 1f, Sprite sweet = null)
    {
        if (!sweet) sweet = LevelCollection.MagicCandy();
        if (!sweet) return;
        frames = new[] { sweet };
        fps = 0f;
        candy = true;
        if (TryGetComponent<SpriteRenderer>(out var look)) look.sprite = sweet;
        scale = LevelCollection.CandyScale(sweet) * size;
    }

    /// A quicker fountain (after `velocity` is set): twice the gravity, launched a third faster - nearly as
    /// high, down a third sooner, so it's on its way to her sooner.
    public void Quick()
    {
        gravity *= 2f;
        velocity *= 1.35f;
    }

    SpriteRenderer _renderer;
    PlayerScriptARIANAClips _player;
    SpriteRenderer _playerBody;
    float _start, _lastFrames, _phase;
    bool _resting, _landed;
    float _landedAt = -1f;
    float _homeSpeed;
    const int OverKnight = 230;   // (the knight draws at 150, 219 attacking, her glow just over that; the foes 205-217)
    /// What's to show over the knight: her sorting layer (PlayerScriptARIANAClips puts her on Default) and this order.
    public const string KnightLayer = "Default";
    public const int OverKnightOrder = OverKnight;
    // Fruit warm gold, gems (mana) aqua-white, viking food green.
    /// Its burst as she picks it up (clear: by what it is) - the dragons' gold.
    [System.NonSerialized] public Color burst = Color.clear;
    Color BurstColor => burst.a > 0f ? burst : bigSeconds > 0f || healShare > 0f ? flashColor : healAmount > 0 ? new Color(1f, 0.82f, 0.35f) : new Color(0.55f, 0.95f, 1f);

    void Start()
    {
        _renderer = GetComponent<SpriteRenderer>();
        // Over the knight as it flies into her, its burst over that too: her sorting layer (she's moved onto
        // Default - the front one, after TopLayer - as she plays) and past her order (150, 219 attacking).
        _renderer.sortingLayerName = KnightLayer;
        _renderer.sortingOrder = OverKnight;
        _player = EndlessKnightDirector.Player ? EndlessKnightDirector.Player : FindAnyObjectByType<PlayerScriptARIANAClips>();
        var body = _player ? _player.transform.Find("ARIANA") : null;
        _playerBody = body ? body.GetComponent<SpriteRenderer>() : null;
        transform.localScale = Vector3.one * (SpikeTrap.PixelSize / 0.01f * scale);
        _start = Time.time;
        _phase = Random.value * 10f;
        if (fps <= 0f && frames != null && frames.Length > 0) _renderer.sprite = frames[Random.Range(0, frames.Length)];
        if (_player) _lastFrames = _player.AccumulatedFrames;
    }

    void Update()
    {
        if (!_player) return;
        float age = Time.time - _start;
        // (Once it's on its way to her, it always gets there.)
        if (!_landed && age > lifetime) { Destroy(gameObject); return; }
        // Blinks for its last two seconds.
        _renderer.enabled = _landed || age < lifetime - 2f || ((int)(age * 10f)) % 2 == 0;
        if (fps > 0f && frames != null && frames.Length > 0)
            _renderer.sprite = frames[(int)((Time.time + _phase) * fps) % frames.Length];

        // Moves with the road, plus its own flight.
        float now = _player.AccumulatedFrames;
        var p = transform.position;
        p.x -= (now - _lastFrames) * EndlessKnightDirector.RoadUnitsPerFrame;
        _lastFrames = now;
        bool sitting = _landed && Time.time - _landedAt < restSeconds;
        if (sitting) { }   // (down on the sidewalk a moment, to be seen, before it's off to her)
        else if (_landed && _playerBody)
        {
            // Off to the knight's middle, speeding up, a little arc up as it goes.
            var to = (Vector3)(Vector2)_playerBody.bounds.center - p;
            _homeSpeed = Mathf.Min(homeMax, (_homeSpeed <= 0f ? homeSpeed : _homeSpeed) + homeAccel * Time.deltaTime);
            float step = _homeSpeed * Time.deltaTime;
            if (to.magnitude <= step) p += to;
            else p += (to.normalized + Vector3.up * 0.25f * Mathf.Clamp01(to.magnitude)).normalized * step;
        }
        else if (!_resting)
        {
            float floor = SpikeTrap.PlayerFeetY;
            if (sitOnBottom && TryGetComponent<SpriteRenderer>(out var drawn)) floor += p.y - drawn.bounds.min.y;
            velocity.y -= gravity * Time.deltaTime;
            p += (Vector3)(velocity * Time.deltaTime);
            if (p.y <= floor && velocity.y < 0f && bounces > 0)
            {
                // (bouncing again, a little less each time - a knock on the road each one, quieter as they shrink)
                p.y = floor;
                if (bounceSound) PitAudio.PlayClip(bounceSound, Mathf.Clamp01(0.35f + Mathf.Abs(velocity.y) * 0.08f));
                bounces--;
                velocity = new Vector2(velocity.x * 0.75f, -velocity.y * bounce);
                if (velocity.y < 0.4f) bounces = 0;
            }
            else if (p.y <= floor && velocity.y < 0f)   // (down on the sidewalk, in line with the knight)
            {
                p.y = floor;
                if (bounceSound) PitAudio.PlayClip(bounceSound, 0.3f);
                _landed = true;
                _landedAt = Time.time;
                velocity = new Vector2(velocity.x * 0.6f, -velocity.y * bounce);
                if (velocity.y < 0.3f) _resting = true;
            }
        }
        transform.position = p;

        // Picked up once it has come down (not on the way out of the chest) - when it's reached her middle,
        // in a little burst of its colour.
        if (!_landed || !_playerBody) return;
        var b = _playerBody.bounds;
        if (((Vector2)p - (Vector2)b.center).magnitude < 0.06f)
        {
            FoeFx.MagicBurst(p, BurstColor, 0.45f, OverKnight);
            if (trackAs)
            {
                // The level's row (a key, an egg, a potion): a bigger burst, gold round white.
                FoeFx.MagicBurst(p, new Color(1f, 0.82f, 0.3f), 1f, OverKnight);
                FoeFx.MagicBurst(p, Color.white, 0.6f, OverKnight + 1);
            }
            if (miniEggs != null && miniEggs.Length > 0) FoeFx.MiniEggs(p, miniEggs);
            PitAudio.PlayClip(candy ? LevelCollection.CandySound() : collectSound, trackAs ? 1f : 0.8f);
            SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Tick);   // ARcade: picked up
            if (healAmount > 0) FindAnyObjectByType<StatsControl>()?.HealHP(healAmount);
            if (trackAs) FruitTracker.Collect(trackAs);                      // the level's row: its key, egg, potion
            else if (healAmount > 0) FruitTracker.Collect(_renderer.sprite);   // the fruit row over the health bar
            if (bigSeconds > 0f || healShare > 0f)
            {
                // Viking food: healing her first; once she's at full health, it makes her big.
                var fed = FindAnyObjectByType<StatsControl>();
                bool full = !fed || !fed.playerStats || fed.playerStats.health >= fed.playerStats.MaxHP;
                if (!full) { fed.HealHP(Mathf.CeilToInt(healShare * fed.playerStats.MaxHP)); PlayerBuffs.Ate?.Invoke(flashColor); }
                else if (bigSeconds > 0f) { PlayerBuffs.GoBig(bigSeconds, flashColor, shrinkSound); if (bigSound) PitAudio.PlayClip(bigSound, 1f); }
            }
            if (manaShare > 0f && FindAnyObjectByType<StatsControl>() is StatsControl control && control.playerStats)
                control.AddMP(Mathf.CeilToInt(manaShare * control.playerStats.MaxMP));
            if (scoreValue > 0) { PitScoreManager.Instance?.AddScore(scoreValue); TreasureChest.BonusPoints += scoreValue; }
            Destroy(gameObject);
        }
    }
}
