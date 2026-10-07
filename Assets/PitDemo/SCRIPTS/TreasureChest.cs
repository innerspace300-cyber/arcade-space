using UnityEngine;

// ENDLESS KNIGHT's treasure chest (THE PIT's gold Animated Chest): the Slime
// Boss drops it where it dies. It falls onto the road just behind the
// player's lane and wiggles; when the player walks up to it they kick it
// (their KICK CHEST animation) and, on the kick, the lid pops open with a
// sparkle burst, a spray of gems (ChestGem) to pick up, the coin sound and
// bonus points. Once the gems are out it flashes and vanishes, so it's
// never in the way. It stays put on the road
// as it scrolls, like the effects (PitSpriteFx).
public class TreasureChest : MonoBehaviour
{
    // ARcade: DesireFantasy's 12 chests (6 frames each: shut, the lid's
    // flash, then open). A run's first chest is looks[0]; every chest after
    // it is one of the others, all different before any repeats.
    [System.Serializable]
    public class Look { public Sprite[] frames; }
    public Look[] looks;
    // What each chest holds, in turn by boss (the 13th boss's chest holds the
    // 1st's again): one kind of gem or fruit per chest.
    [System.Serializable]
    public class Loot
    {
        public string name;
        public GameObject[] items;
        public float rate = 1f;
        [Tooltip("Played as it opens in place of the coin sound, and as each item flies out (fruit chests).")]
        public AudioClip openSound, emitSound;
    }
    float _rate = 1f;
    Loot _loot;
    // ARcade: every 3rd chest of a run holds the fairy (FairyAlly) in place
    // of loot; the others hold the loot in turn.
    public const int FairyEvery = 3;
    bool _fairy;
    [Tooltip("Played as a chest with the fairy in it pops open (it starts with a door opening).")]
    public AudioClip fairyKickSound;
    [Tooltip("One of these, at random, as a chest without the fairy starts opening its lid.")]
    public AudioClip[] lidSounds;
    [Tooltip("Seconds it stays open after the fairy rises out, before it vanishes.")]
    public float fairyLinger = 1.2f;
    public Loot[] loot;
    GameObject[] _items;
    /// Chests dropped this run (EndlessKnightDirector resets it).
    public static int RunCount;
    bool _allFruit;   // (a testing switch's, now always off)
    /// Points from chests and their gems this run - they don't count toward the next boss.
    public static int BonusPoints;
    static int[] s_order;

    public Sprite[] idleFrames;
    public Sprite[] openFrames;
    public float idleFps = 8f;
    public float openFps = 12f;
    [Tooltip("Size relative to the characters' pixels.")]
    public float scale = 1f;
    [Tooltip("ARcade: it lands this far (world units) below the knight's foot line - a little down the sidewalk.")]
    public float sink = 0.14f;
    [Tooltip("The player kicks it when this close (world units).")]
    public float kickRange = 0.3f;
    [Tooltip("Seconds into the kick animation when the foot lands.")]
    public float kickImpact = 0.35f;
    [Tooltip("Drops in from this high above the road.")]
    public float dropHeight = 0.8f;
    public float dropTime = 0.35f;
    public int scoreValue = 2000;
    public GameObject burstPrefab;
    [Tooltip("ChestGem prefabs, picked at random for each gem shot out.")]
    public GameObject[] gemPrefabs;
    [Tooltip("Gems shot out per second, for as long as the coin sound plays.")]
    public float gemsPerSecond = 8f;
    public AudioClip coinSound;
    [Tooltip("Seconds it flashes before vanishing, after the last gem.")]
    public float vanishTime = 1f;

    SpriteRenderer _renderer;
    PlayerScriptARIANAClips _player;
    float _start, _kickTime = -1f, _openTime = -1f;
    float _lastFrames;

    // ARcade: set up the moment it's made (Awake), not in Start - dropped
    // mid-frame by the dying boss, it was drawn once before Start ran, as the
    // prefab's own sprite (THE PIT's gold chest) where it died, at its size.
    void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        PickLook();
        if (idleFrames != null && idleFrames.Length > 0) _renderer.sprite = idleFrames[0];
        _player = EndlessKnightDirector.Player ? EndlessKnightDirector.Player : FindAnyObjectByType<PlayerScriptARIANAClips>();
        transform.position = new Vector3(ClearOfSpikes(transform.position.x), SpikeTrap.GroundY - sink + dropHeight, SpikeTrap.LaneZ + 0.01f);
        transform.localScale = Vector3.one * (SpikeTrap.PixelSize / 0.01f * scale);
        _start = Time.time;
        // ARcade: its shadow on the road where it'll land, growing as it falls.
        var cast = SpriteShadow.Cast(_renderer);
        if (cast) { cast.measurePixels = true; cast.groundAt = SpikeTrap.GroundY - sink + DrawnBottom(); cast.fadeHeight = Mathf.Max(1.2f, dropHeight * 1.2f); }
        if (_player) _lastFrames = _player.AccumulatedFrames;
    }

    // How far its drawn bottom sits over where it stands (its frame has
    // clear canvas under it): its shadow starts right at the bottom of it.
    float DrawnBottom()
    {
        return SpriteShadow.OpaqueBottom(_renderer.sprite) * transform.lossyScale.y;
    }

    void Update()
    {
        if (!_player) return;
        // Stay put on the road.
        float now = _player.AccumulatedFrames;
        transform.position += Vector3.left * ((now - _lastFrames) * EndlessKnightDirector.RoadUnitsPerFrame);
        _lastFrames = now;

        float dx = transform.position.x - _player.transform.position.x;
        if (dx < -30f) { Destroy(gameObject); return; }

        // Falling in.
        float t = Mathf.Clamp01((Time.time - _start) / dropTime);
        float fall = 1f - t * t;
        float y = SpikeTrap.GroundY - sink + dropHeight * fall;
        var p = transform.position;
        transform.position = new Vector3(p.x, y, p.z);

        if (_openTime >= 0f)
        {
            int f = Mathf.Min(openFrames.Length - 1, (int)((Time.time - _openTime) * openFps));
            _renderer.sprite = openFrames[f];
            return;
        }
        _renderer.sprite = idleFrames[(int)(Time.time * idleFps) % idleFrames.Length];

        if (_kickTime < 0f)
        {
            if (t >= 1f && Mathf.Abs(dx) < kickRange && _player.KickChest())
                _kickTime = Time.time;
        }
        else if (Time.time - _kickTime >= kickImpact) Open();
    }

    [Tooltip("Road kept free of spikes on each side of the chest, beyond the traps' own half width - room to stand and kick it.")]
    public float spikeClearance = 0.55f;

    // Never on spikes: the nearest spot to where the boss died with no trap
    // under the chest or where the player stands to kick it.
    float ClearOfSpikes(float x)
    {
        var traps = FindObjectsByType<SpikeTrap>(FindObjectsSortMode.None);
        for (int step = 0; step <= 20; step++)
        {
            float offset = (step + 1) / 2 * 0.2f * (step % 2 == 0 ? 1f : -1f);
            float at = x + offset;
            bool clear = true;
            foreach (var trap in traps)
            {
                float halfWidth = SpikeTrap.SpikesWidthPixels * 0.5f * trap.transform.lossyScale.x * 0.01f;
                if (Mathf.Abs(trap.transform.position.x - at) < halfWidth + spikeClearance) { clear = false; break; }
            }
            if (clear) return at;
        }
        return x;
    }

    void PickLook()
    {
        int n = RunCount++;
        _fairy = (n + 1) % FairyEvery == 0;
        _allFruit = false;
        if (loot != null && loot.Length > 0 && !_fairy)
        {
            _loot = loot[(n - (n + 1) / FairyEvery) % loot.Length];
            _items = _loot.items;
            _rate = _loot.rate;
        }
        if (looks == null || looks.Length == 0) return;
        int index = 0;
        if (n > 0 && looks.Length > 1)
        {
            int others = looks.Length - 1;
            // A fresh shuffle of the others each time they've all been used.
            if ((n - 1) % others == 0 || s_order == null || s_order.Length != others)
            {
                s_order = new int[others];
                for (int i = 0; i < others; i++) s_order[i] = i + 1;
                for (int i = others - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (s_order[i], s_order[j]) = (s_order[j], s_order[i]); }
            }
            index = s_order[(n - 1) % others];
        }
        var frames = looks[index].frames;
        if (frames == null || frames.Length < 2) return;
        idleFrames = new[] { frames[0] };
        openFrames = frames[1..];
    }

    /// Kicked open (it vanishes once its loot is out).
    public bool Opened => _openTime >= 0f;

    void Open()
    {
        _openTime = Time.time;
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Medium);   // ARcade: the kick
        if (_fairy && FairyAlly.Current)
        {
            // A door into the fairy's world opens; she rises out of it (with
            // her own sound), and it goes.
            PitAudio.PlayClip(fairyKickSound);
            if (burstPrefab)
                Instantiate(burstPrefab, transform.position + Vector3.up * 0.15f + Vector3.back * 0.05f, Quaternion.identity);
            if (scoreValue > 0) { PitScoreManager.Instance?.AddScore(scoreValue); BonusPoints += scoreValue; }
            FairyAlly.Current.Arrive(transform.position + Vector3.up * 0.15f + Vector3.back * 0.03f);
            StartCoroutine(VanishAfter(fairyLinger));
            return;
        }
        // Its lid creaks open (one of the loot chest sounds) - and a gem
        // chest's coins pour; a fruit chest just the lid.
        if (lidSounds != null && lidSounds.Length > 0) PitAudio.PlayClip(lidSounds[Random.Range(0, lidSounds.Length)]);
        bool fruit = _loot != null && _loot.emitSound;
        if (!fruit) PitAudio.PlayClip(coinSound);
        if (burstPrefab)
            Instantiate(burstPrefab, transform.position + Vector3.up * 0.15f + Vector3.back * 0.05f, Quaternion.identity);
        if (scoreValue > 0) { PitScoreManager.Instance?.AddScore(scoreValue); BonusPoints += scoreValue; }
        if (_items == null || _items.Length == 0) _items = gemPrefabs;
        if (_allFruit && SpewAllFruit()) return;
        if (_items != null && _items.Length > 0) StartCoroutine(SpewGems());
        else StartCoroutine(Vanish());
    }

    // Testing: every fruit in the fruit row, one each, as a fountain - each
    // a fruit pickup (any of the chests' fruit) wearing that fruit.
    bool SpewAllFruit()
    {
        var tracker = FindAnyObjectByType<FruitTracker>();
        GameObject template = null;
        if (loot != null)
            foreach (var set in loot)
                foreach (var item in set.items)
                    if (item && item.TryGetComponent<ChestGem>(out var g) && g.healAmount > 0) template = template ? template : item;
        if (!tracker || tracker.fruit == null || !template) return false;
        StartCoroutine(SpewFruit(tracker.fruit, template));
        return true;
    }

    System.Collections.IEnumerator SpewFruit(Sprite[] fruit, GameObject template)
    {
        float gap = 0.12f;
        for (int i = 0; i < fruit.Length; i++)
        {
            var go = Instantiate(template, transform.position + new Vector3(0f, 0.15f, -0.01f - i * 0.001f), Quaternion.identity);
            var item = go.GetComponent<ChestGem>();
            item.frames = new[] { fruit[i] };
            item.velocity = new Vector2(Random.Range(-0.9f, 0.9f), Random.Range(3.0f, 4.2f));
            if (_loot != null && _loot.emitSound) PitAudio.PlayClip(_loot.emitSound, 0.6f);
            yield return new WaitForSeconds(gap);
        }
        yield return Vanish();
    }

    // A fountain of gems for as long as the coin sound plays.
    System.Collections.IEnumerator SpewGems()
    {
        float duration = coinSound ? coinSound.length : 3f;
        int count = Mathf.Max(1, Mathf.RoundToInt(duration * gemsPerSecond * _rate));
        for (int i = 0; i < count; i++)
        {
            var gem = Instantiate(_items[Random.Range(0, _items.Length)],
                transform.position + new Vector3(0f, 0.15f, -0.01f - (i % 50) * 0.001f), Quaternion.identity);
            // High into the air, fanning out evenly to both sides of the chest.
            var item = gem.GetComponent<ChestGem>();
            item.velocity = new Vector2(Random.Range(-0.9f, 0.9f), Random.Range(3.0f, 4.2f));
            // Gems (not fruit) restore mana: one chest's worth fills the bar,
            // with a few to spare.
            if (item.healAmount <= 0) item.manaShare = 1.25f / count;
            if (_loot != null && _loot.emitSound) PitAudio.PlayClip(_loot.emitSound, 0.6f);
            yield return new WaitForSeconds(duration / count);
        }
        yield return Vanish();
    }

    System.Collections.IEnumerator VanishAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        yield return Vanish();
    }

    System.Collections.IEnumerator Vanish()
    {
        for (float t = 0f; t < vanishTime; t += Time.deltaTime)
        {
            // Faster and faster.
            _renderer.enabled = Mathf.Repeat(t * Mathf.Lerp(6f, 16f, t / vanishTime), 1f) < 0.5f;
            yield return null;
        }
        Destroy(gameObject);
    }
}
