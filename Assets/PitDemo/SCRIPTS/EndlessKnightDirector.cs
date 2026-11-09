using System.Collections.Generic;
using UnityEngine;

// EndlessKnightDirector — ENDLESS KNIGHT's gameplay, on top of THE PIT's
// spawn systems (it only rewrites their entry lists as the player advances):
//   • no small enemies: the enemy spawner is kept for the boss only;
//   • orange slices come in groups of 5, each group a random colour at a
//     random height (jump to reach the high ones), moving with the road, with
//     a random gap between groups, forever (the other fruit is the chests');
//   • spike traps sit on the road at equal distances (SpikeTrap), moving
//     with the road (sometimes in pairs), and bomb guys (BombGuy) walk in, in
//     most of the gaps and sometimes just ahead of a trap; high orange groups
//     often have spikes or a bomb guy under them;
//   • the Slime Boss drops in when the score reaches bossScore, and again
//     bossScoreStep points after each one is beaten (chest and gem points
//     don't count), with two bomb guys walking in during the fight. Each
//     one after the first comes in a new colour with more health; all of
//     them move slowly. When one dies, the bomb guys on screen go off
//     (harmlessly) and nothing new comes until its chest and all the loot
//     from it are gone, and a second more.
// Distances are in the player's AccumulatedFrames (100 per loop of the
// background), like the spawn systems' trigger frames.
public class EndlessKnightDirector : MonoBehaviour
{
    public PlayerScriptARIANAClips player;
    public CollectableSpawnSystem collectables;
    public ParallaxSpawnSystem enemies;
    public GameObject spikePrefab;
    public GameObject bombPrefab;
    [Tooltip("Chance of a bomb guy in each gap between spike traps.")]
    [Range(0f, 1f)] public float bombChance = 1f;
    [Tooltip("Chance of a second bomb guy in a gap, a quarter of the way along it.")]
    [Range(0f, 1f)] public float extraBombChance = 0.5f;
    [Tooltip("Chance of a bomb guy coming just ahead of a spike trap.")]
    [Range(0f, 1f)] public float bombBeforeSpikeChance = 0.6f;
    [Tooltip("Chance of a hazard (spikes or a bomb guy) under a high orange group.")]
    [Range(0f, 1f)] public float hazardUnderOrangesChance = 0.6f;
    [Tooltip("Seconds between bomb guys while the Slime Boss is alive.")]
    public float bossBombInterval = 3f;
    [Tooltip("Bomb guys sent in during each boss fight.")]
    public int bossBombs = 2;

    [Header("Oranges")]
    const int GroupSize = 5;
    [Tooltip("Frames between one group's last slice and the next group's first.")]
    public Vector2 groupGap = new Vector2(70f, 130f);
    public float firstOrange = 30f;
    [Tooltip("Height range of a low group above the road, in world units: reached walking (random per group).")]
    public Vector2 orangeHeight = new Vector2(0.5f, 0.75f);
    [Tooltip("Height range of a high group: out of reach unless the player jumps.")]
    public Vector2 highOrangeHeight = new Vector2(1f, 1.3f);
    [Range(0f, 1f)] public float highOrangeChance = 0.5f;
    [Tooltip("Distance between the slices in a group, in world units.")]
    public float orangeSpacing = 0.3f;

    [Header("Spikes")]
    public float firstSpike = 90f;
    [Tooltip("Frames between spike traps (equal distances).")]
    public float spikeSpacing = 120f;
    [Tooltip("Chance a spike trap has a second one right beside it.")]
    [Range(0f, 1f)] public float doubleSpikeChance = 0.45f;
    [Tooltip("Chance a spike trap is a row of three instead (a double jump to clear).")]
    [Range(0f, 1f)] public float tripleSpikeChance = 0.2f;
    [Tooltip("ARcade: level 3 - chance of a spinning saw blade between one spike trap and the next (SpikeTrap.saw).")]
    [Range(0f, 1f)] public float sawChance = 0.5f;
    [Tooltip("The road art moves this many pixels per frame of the player's scroll counter.")]
    public float roadPixelsPerFrame = 7.2f;   // (720 px a loop of the art: 3 and 4 px steps)

    [Header("Boss")]
    public int bossScore = 1000;
    public int bossScoreStep = 1000;
    [Tooltip("Its walking speed (world units a second; THE PIT's was 2).")]
    public float bossSpeed = 0.5f;
    [Tooltip("How far away it keeps coming after the player (THE PIT's was 3: backing off a little left it standing).")]
    public float bossChaseRadius = 6f;
    [Tooltip("Each boss after the first has this much more health than the one before (fraction of the first's).")]
    public float bossHealthStep = 0.5f;
    [Tooltip("Recolours the bosses after the first (SpatialEmulator/SpriteHueShift).")]
    public Material bossHueMaterial;
    [Tooltip("Organic splashes blood is made of (Blood FX's slime bursts), turned to each foe's colour (FoeFx.Gore).")]
    public GameObject[] goreBursts;
    [Tooltip("Bursts of liquid in mid-air (Blood FX's slime essence): the necromancer's magic blood (FoeFx.Potion).")]
    public GameObject[] liquidBursts;
    [Tooltip("ARcade: played (with a gold pixel burst) when something hits her under the Shield Buff - a ricochet.")]
    public AudioClip shieldBlockSound;
    [Tooltip("The neon colours (HSV hue, degrees) of the bosses after the first, in turn: green, pink, aqua, orange, red, blue, purple.")]
    public float[] bossHues = { 125f, 320f, 180f, 28f, 355f, 215f, 278f };
    [Tooltip("Each boss is this much bigger than the one before (fraction of the first's size), up to bossMaxSize.")]
    public float bossGrowth = 0.1f;
    public float bossMaxSize = 1.6f;
    [Tooltip("After a boss dies: seconds before anything new comes, once its chest and loot are gone.")]
    public float quietAfterBoss = 1f;

    /// ARcade: every Slime Boss drawn this much bigger than THE PIT's.
    public const float BossScale = 1.15f;

    // The hues of the Slime Boss's own art and of its (purple) blood, measured.
    const float BossArtHue = 278f, BloodArtHue = 263f;

    [Tooltip("How far ahead of the player new entries are queued, in frames.")]
    public float lookAhead = 200f;

    float _nextBossBomb;
    int _bossBombsSent;
    // ARcade: this level's own foes and boss (LevelFoes), if it has them, in place of bomb guys and the Slime Boss.
    LevelFoes _foes;
    GameObject[] _foeTemplates;

    readonly List<CollectableSpawnEntry> _orangeTemplates = new List<CollectableSpawnEntry>();
    ParallaxSpawnEntry _bossTemplate;
    float _nextOrange, _nextSpike;
    int _nextBossScore;
    bool _bossSpawned;
    float _spikeDrift = 0.25f;
    Transform _groundPoint, _endPoint;

    /// The player, and how far the road moves (world units) per frame of
    /// its scroll counter - for effects that stay put on the road (PitSpriteFx).
    public static PlayerScriptARIANAClips Player;
    public static float RoadUnitsPerFrame;
    /// ARcade: while true nothing new comes down the road and no boss comes
    /// (the witch's portal visit, LevelPortal); afterwards a quiet, then on as after a boss.
    public static bool Hold;

    void Awake()
    {
        if (!player) player = GetComponentInChildren<PlayerScriptARIANAClips>(true);
        if (!collectables) collectables = GetComponentInChildren<CollectableSpawnSystem>(true);
        if (!enemies) enemies = GetComponentInChildren<ParallaxSpawnSystem>(true);

        // THE PIT's orange entries become the colour templates; its dino and
        // boss entries go, keeping the boss as a template.
        foreach (var e in collectables.spawnEntries)
            if (e.prefab) _orangeTemplates.Add(e);
        foreach (var e in enemies.spawnEntries)
            if (e.prefab && e.prefab.name.ToUpperInvariant().Contains("SLIME")) _bossTemplate = e;
        collectables.spawnEntries = new List<CollectableSpawnEntry>();
        enemies.spawnEntries = new List<ParallaxSpawnEntry>();

        _foes = GetComponentInParent<LevelFoes>(true);   // (on the level's prefab)
        if (_foes && _foes.road != null && _foes.road.Length > 0)
        {
            _foeTemplates = new GameObject[_foes.road.Length];
            for (int i = 0; i < _foes.road.Length; i++)
            {
                var t = new GameObject("FOE TEMPLATE " + _foes.road[i].name);
                t.transform.SetParent(transform, false);
                t.AddComponent<SpriteRenderer>();
                var e = t.AddComponent<RoadEnemy>();
                e.template = true;
                e.foe = _foes.road[i];
                _foeTemplates[i] = t;
            }
        }
        if (bombPrefab) RoadEnemy.BlastFrom = bombPrefab.GetComponent<BombGuy>();
        RoadEnemy.HueMaterial = bossHueMaterial;
        FoeFx.Bursts = goreBursts;
        FoeFx.AirBursts = liquidBursts;
        PlayerBuffs.BlockSound = shieldBlockSound;
        _groundPoint = transform.Find("SPAWN POINT MAIN");
        _endPoint = transform.Find("COLLECTABLE END POINT");
        _nextOrange = firstOrange;
        _nextSpike = firstSpike;
        _nextBossScore = bossScore;
        CrystalPrize.Coming = false;
        TreasureChest.RunCount = 0;
        LevelCollection.ResetRun();   // (the chests' candy from the first gummy bear and jelly bean again)
        TreasureChest.BonusPoints = 0;
        Hold = false;
    }

    void Start()
    {
        // Where the road is, and how fast it moves, for the spikes.
        var body = player.transform.Find("ARIANA") ? player.transform.Find("ARIANA").GetComponent<SpriteRenderer>() : null;
        if (body && body.sprite)
        {
            SpikeTrap.GroundY = body.bounds.min.y + SpikeTrap.PlayerDrop;   // (the road line: she walks below it)
            SpikeTrap.LaneZ = player.transform.position.z;
            // (Her pixels as the game was built: she's drawn bigger now.)
            SpikeTrap.PixelSize = body.bounds.size.x / body.sprite.rect.width / SpatialEmulator.Demo.PitDemoGame.CharacterScale;
        }
        var road = player.backgroundAnimator ? player.backgroundAnimator.transform.Find("BG road") : null;
        var roadRenderer = road ? road.GetComponent<SpriteRenderer>() : null;
        if (roadRenderer && roadRenderer.sprite && collectables.worldUnitsPerFrame > 0f)
        {
            float roadPixel = roadRenderer.bounds.size.x / roadRenderer.sprite.rect.width;
            _spikeDrift = roadPixelsPerFrame * roadPixel / collectables.worldUnitsPerFrame;
        }
        Player = player;
        RoadUnitsPerFrame = _spikeDrift * collectables.worldUnitsPerFrame;

    }

    // After a boss's chest: where the bomb guys and spike traps come (frames
    // on from the quiet's end), and how long before the oranges come back.
    static readonly float[] AfterChestBombs = { 10f, 70f, 135f };
    static readonly float[] AfterChestSpikes = { 35f, 100f };
    const float AfterChestStretch = 150f;

    [Tooltip("ARcade: frames of road after a spike trap before any bomb guy or foe - room to land over the spikes, then swing.")]
    public float clearAfterSpikes = 55f;
    readonly List<float> _spikeFrames = new List<float>();

    float _nextCleanup;
    void Update()
    {
        float frames = player.AccumulatedFrames;

        // Missed oranges are only hidden by CollectableSpawnSystem; remove
        // the ones long gone off the left edge.
        if (Time.time >= _nextCleanup)
        {
            _nextCleanup = Time.time + 1f;
            float left = player.transform.position.x - 30f;
            foreach (var orange in FindObjectsByType<PitCollectable>(FindObjectsSortMode.None))
                if (orange.transform.position.x < left) Destroy(orange.gameObject);
        }

        TrackBosses();
        ArenaDone();
        if (_arena) ArenaFruit();
        if (Hold)
        {
            _quietLeft = Mathf.Max(_quietLeft, quietAfterBoss);
            BlockAtBoss();
            return;
        }
        if (_quietLeft > 0f)
        {
            // The quiet after a boss: until its chest and everything in it
            // are gone (picked up or blinked out), and a second more; then
            // things come in again right at the edge of the picture.
            if (LootAbout()) _quietLeft = quietAfterBoss;
            else _quietLeft -= Time.deltaTime;
            if (_quietLeft <= 0f)
            {
                // From here, not from where the cleared queue had got to
                // (up to lookAhead frames on - ten seconds of walking): first
                // a stretch of hazards - three bomb guys and two spike traps
                // between the chest and the next oranges - then on as usual.
                if (HasBombs) foreach (float at in AfterChestBombs) QueueBomb(frames + at);
                if (spikePrefab) foreach (float at in AfterChestSpikes) QueueHazard(spikePrefab, frames + at);
                _nextOrange = frames + AfterChestStretch;
                _nextSpike = frames + AfterChestStretch + spikeSpacing * 0.5f;
            }
        }
        else if (FruitAllowed)   // (none on the way to an arena, in it, nor in its way out)
        {
            while (_nextOrange < frames + lookAhead && _orangeTemplates.Count > 0) QueueOrangeGroup();
            while (_nextSpike < frames + lookAhead && spikePrefab) QueueSpike();
        }

        BlockAtBoss();

        // More bomb guys walk in during the boss fight.
        if (_bossSpawned && !_bossDead && HasBombs && !BossSummons && _bossBombsSent < bossBombs && Time.time >= _nextBossBomb && Time.time > _bossSpawnTime + 3f)
        {
            _bossBombsSent++;
            _nextBossBomb = Time.time + bossBombInterval * Random.Range(0.75f, 1.25f);
            QueueBomb(frames, single: true);
        }

        // Points collected (chests and their gems aside).
        int score = (PitScoreManager.Instance ? PitScoreManager.Instance.GetScore() : 0) - TreasureChest.BonusPoints;
        if (!_bossSpawned && (_bossTemplate != null || HasOwnBoss) && score >= _nextBossScore)
        {
            // A level with an arena: it comes first, along the road; the boss once she's in it.
            if (!_levelBackground) _levelBackground = FindAnyObjectByType<ParallaxLevelBackground>();
            // (the last one, left behind on the road - once she's walked well past it: walking back into it, or a long
            // way left, never swaps it for the next or the walk's time with it)
            if (_arena && _arena.Released && _arena.RightReach + _arena.Lead < _arena.ViewMinX - 0.5f) { Destroy(_arena.gameObject); _arena = null; }
            if (_levelBackground && _levelBackground.HasArena && !_arena) { _arena = _levelBackground.SpawnArena(); ClearAhead(); }
            // (the boss as soon as it's locked in place - she's never walked anywhere or held: its middle's a wall)
            if (!_arena || (_arena.Locked && !_arena.Released)) SpawnBoss(frames);
        }
        else if (_bossSpawned && RoadFoe.Bosses().Count == 0
                 && Time.time > _bossSpawnTime + 2f)
            _bossSpawned = false;   // gone (the next one was set when it died)
    }

    // The level's boss arena (BossArena), on its way or fought in; let go once its boss is beaten and its loot gone.
    ParallaxLevelBackground _levelBackground;
    BossArena _arena;
    float _bossDiedAt = -1f;

    // No fruit in a boss's arena: from its coming till its way out is all in view, past the boss.
    bool FruitAllowed => !_arena || (_arena.Released && _arena.RightReach < _arena.ViewMaxX);

    void ArenaFruit()
    {
        if (FruitAllowed) return;
        // Fruit lined up on the road ahead, still to come out: not now.
        for (int i = collectables.spawnEntries.Count - 1; i >= 0; i--)
        {
            var e = collectables.spawnEntries[i];
            if (e.prefab == null || e.spawnedCount >= e.spawnCount || !_orangeTemplates.Exists(t => t.prefab == e.prefab)) continue;
            if (e.spawnPoint && e.spawnPoint != _groundPoint) Destroy(e.spawnPoint.gameObject);
            collectables.spawnEntries.RemoveAt(i);
        }
        // And any already out in it, or in its bamboo.
        foreach (var fruit in FindObjectsByType<PitCollectable>(FindObjectsSortMode.None))
        {
            float x = fruit.transform.position.x;
            if (x > _arena.LeftReach - 0.1f && x < _arena.RightReach + 0.1f) Destroy(fruit.gameObject);
        }
    }

    void ArenaDone()
    {
        if (!_arena || !_arena.Locked || _arena.Released || _bossDiedAt < _arena.LockedAt) return;
        _arena.Release();   // (its boss beaten: the road's free again, its loot moving with it)
    }

    // What's queued on the road ahead and not yet out (foes, bomb guys, spikes): none in the arena.
    void ClearAhead()
    {
        collectables.spawnEntries.RemoveAll(e => e.spawnedCount == 0 && e.prefab != null
            && (e.prefab == bombPrefab || IsTrap(e.prefab) || (_foeTemplates != null && System.Array.IndexOf(_foeTemplates, e.prefab) >= 0)));
    }

    float _bossSpawnTime;
    float _quietLeft;
    int _bossLevel;         // bosses so far this run
    bool _bossDead;
    readonly HashSet<IRoadBoss> _knownBosses = new HashSet<IRoadBoss>();

    // New bosses get their level's colour, health and speed; a dying one clears the road.
    void TrackBosses()
    {
        if (!_bossSpawned) return;
        foreach (var any in RoadFoe.Bosses())
        {
            if (any is RoadBoss own)
            {
                // (Set up as it was sent: SpawnBoss.)
                if (_knownBosses.Add(own))
                {
                    _bossDead = false;
                    var hudBar = FindAnyObjectByType<HudJuice>();
                    BossHealthBar.Attach(own, hudBar && hudBar.healthText ? hudBar.healthText.font : null);
                }
                if (own.IsDead && !_bossDead) { _bossDead = true; BossKilled(); }
                continue;
            }
            var boss = (EnemyStateMachineSlimeBoss)any;
            if (_knownBosses.Add(boss))
            {
                _bossLevel++;
                _bossDead = false;
                boss.movementSpeed = bossSpeed;
                boss.detectionRadius = bossChaseRadius;
                boss.enemyHealth = Mathf.RoundToInt(boss.enemyHealth * (1f + bossHealthStep * (_bossLevel - 1)));
                boss.MaxHealth = boss.enemyHealth;
                var shadow = SpriteShadow.Cast(boss.Body);   // its shadow on the road, from its
                if (shadow) { shadow.fromRoadLine = true; shadow.groundFrom = boss.BodyCollider; }   // feet (its drips hang below them)
                if (_bossLevel > 1 && bossHueMaterial && bossHues.Length > 0)
                {
                    // Neon: turned to the next colour, saturated and bright.
                    float hue = bossHues[(_bossLevel - 2) % bossHues.Length];
                    boss.Recolor(Neon(hue - BossArtHue, 1.35f, 1.3f), Neon(hue - BloodArtHue, 1.3f, 1.1f));
                    boss.impactColor = Color.HSVToRGB(Mathf.Repeat(hue / 360f, 1f), 0.7f, 1f);
                }
                boss.Grow(BossScale * Mathf.Min(bossMaxSize, 1f + bossGrowth * (_bossLevel - 1)));
                // Its health bar over its head, sized to it as grown.
                var hud = FindAnyObjectByType<HudJuice>();
                BossHealthBar.Attach(boss, hud && hud.healthText ? hud.healthText.font : null, SlimeName(_bossLevel));
            }
            if (boss.IsDead && !_bossDead)
            {
                _bossDead = true;
                BossKilled();
            }
        }
    }

    // The dead boss (its chest is still to come), its chest, or gems or fruit
    // from it. A chest the player walked past without kicking stops counting
    // a little way behind them.
    bool LootAbout()
    {
        foreach (var boss in RoadFoe.Bosses())
            if (boss.IsDead) return true;
        float x = _playerBody ? _playerBody.bounds.center.x : player.transform.position.x;
        foreach (var chest in FindObjectsByType<TreasureChest>(FindObjectsSortMode.None))
            if (chest.Opened || chest.transform.position.x > x - 2f) return true;
        if (FairyAlly.Current && FairyAlly.Current.Waiting) return true;
        return FindAnyObjectByType<ChestGem>() != null;
    }

    Material Neon(float hueTurn, float saturation, float brightness)
    {
        var look = new Material(bossHueMaterial);
        look.SetFloat("_Hue", Mathf.Repeat(hueTurn / 360f, 1f));
        look.SetFloat("_Saturation", saturation);
        look.SetFloat("_Brightness", brightness);
        return look;
    }

    void BossKilled()
    {
        _bossDiedAt = Time.time;
        // The next boss comes bossScoreStep points on.
        int score = (PitScoreManager.Instance ? PitScoreManager.Instance.GetScore() : 0) - TreasureChest.BonusPoints;
        _nextBossScore = score + bossScoreStep;

        // Bomb guys on screen go off where they are, harmlessly; ones off it just go.
        float x = _playerBody ? _playerBody.bounds.center.x : player.transform.position.x;
        foreach (var bomb in FindObjectsByType<BombGuy>(FindObjectsSortMode.None))
        {
            if (Mathf.Abs(bomb.transform.position.x - x) < 2.5f) bomb.Detonate();
            else Destroy(bomb.gameObject);
        }
        foreach (var foe in RoadEnemy.All.ToArray()) if (foe) foe.Vanish(Mathf.Abs(foe.transform.position.x - x) < 2.5f);

        // Nothing new until the player has walked on for a while.
        for (int i = collectables.spawnEntries.Count - 1; i >= 0; i--)
        {
            var e = collectables.spawnEntries[i];
            if (e.spawnedCount > 0) continue;
            if (e.spawnPoint && e.spawnPoint != _groundPoint) Destroy(e.spawnPoint.gameObject);
            collectables.spawnEntries.RemoveAt(i);
        }
        _quietLeft = quietAfterBoss;
    }

    /// ARcade: the road cleared for the witch's portal visit (LevelPortal):
    /// bomb guys go off harmlessly, spike traps and oranges not yet passed
    /// go, and nothing queued comes; Hold keeps it clear.
    public void ClearRoad()
    {
        float x = _playerBody ? _playerBody.bounds.center.x : player.transform.position.x;
        foreach (var bomb in FindObjectsByType<BombGuy>(FindObjectsSortMode.None))
        {
            if (Mathf.Abs(bomb.transform.position.x - x) < 2.5f) bomb.Detonate();
            else Destroy(bomb.gameObject);
        }
        foreach (var spike in FindObjectsByType<SpikeTrap>(FindObjectsSortMode.None))
            if (!spike.template && spike.transform.position.x > x - 0.6f) Destroy(spike.gameObject);
        foreach (var foe in RoadEnemy.All.ToArray()) if (foe) foe.Vanish(Mathf.Abs(foe.transform.position.x - x) < 2.5f);
        foreach (var orange in FindObjectsByType<PitCollectable>(FindObjectsSortMode.None))
            if (orange.transform.position.x > x) Destroy(orange.gameObject);
        for (int i = collectables.spawnEntries.Count - 1; i >= 0; i--)
        {
            var e = collectables.spawnEntries[i];
            if (e.spawnedCount > 0) continue;
            if (e.spawnPoint && e.spawnPoint != _groundPoint) Destroy(e.spawnPoint.gameObject);
            collectables.spawnEntries.RemoveAt(i);
        }
        // A boss on its way (not yet come) doesn't: it comes at its points after.
        int bosses = enemies.spawnEntries.RemoveAll(e => e.spawnedCount == 0);
        if (bosses > 0 && RoadFoe.Bosses().Count == 0)
        {
            _bossSpawned = false;
        }
    }

    // The Slime Boss is solid: the player stops at the near edge of its body
    // (its hit collider, so the sword still reaches) instead of walking into it.
    SpriteRenderer _playerBody;
    void BlockAtBoss()
    {
        // Where she is: her position (not her drawn frame, which her spells'
        // animations move about) and her body's middle off it, measured once
        // as she stands (mirrored as she turns).
        if (!_playerBody && player.transform.Find("ARIANA")) _playerBody = player.transform.Find("ARIANA").GetComponent<SpriteRenderer>();
        float facing = Mathf.Sign(player.transform.localScale.x);
        if (float.IsNaN(_bodyOffset) && _playerBody && _playerBody.enabled && _playerBody.sprite)
            _bodyOffset = (_playerBody.bounds.center.x - player.transform.position.x) * facing;
        float offset = float.IsNaN(_bodyOffset) ? 0f : _bodyOffset * facing;
        float x = player.transform.position.x + offset;

        int blocked = 0;
        float wallMin = float.NegativeInfinity, wallMax = float.PositiveInfinity;
        if (_bossSpawned)
        {
            foreach (var boss in RoadFoe.Bosses())
            {
                if (boss.IsDead || !boss.Body) continue;
                var body = boss.BodyBounds;
                // The side she met it on, for as long as it lives: she stays there.
                if (!_bossSide.TryGetValue(boss, out int side))
                    _bossSide[boss] = side = x < body.center.x ? -1 : 1;
                if (side < 0)
                {
                    if (x > body.min.x - 0.05f) blocked = 1;
                    wallMax = Mathf.Min(wallMax, body.min.x - 0.05f - offset);
                }
                else
                {
                    if (x < body.max.x + 0.05f) blocked = -1;
                    wallMin = Mathf.Max(wallMin, body.max.x + 0.05f - offset);
                }
            }
        }
        if (_arena && _arena.Locked && !_arena.Released)
        {
            // In the arena: she stays in the picture.
            wallMin = Mathf.Max(wallMin, _arena.ViewMinX + 0.08f - offset);
            wallMax = Mathf.Min(wallMax, _arena.ViewMaxX - 0.08f - offset);
        }
        // Its torii (its middle) a wall from the moment it's on the road till its boss is beaten: no way past it.
        if (_arena && !_arena.Released)
        {
            if (!_arena.Locked && x > _arena.MiddleX - 0.3f) _arena.LockNow();   // (up to it already: in place here, its boss coming)
            wallMax = Mathf.Min(wallMax, _arena.MiddleX - 0.04f - offset);
            if (x > _arena.MiddleX - 0.06f && blocked == 0) blocked = 1;
        }
        player.BlockedDirection = blocked;
        player.WallMin = wallMin;
        player.WallMax = wallMax;
    }

    float _bodyOffset = float.NaN;
    readonly Dictionary<IRoadBoss, int> _bossSide = new Dictionary<IRoadBoss, int>();

    void QueueOrangeGroup()
    {
        var template = _orangeTemplates[Random.Range(0, _orangeTemplates.Count)];
        int size = GroupSize;
        // Never on top of (or crowding) the group before: not till it's all out and a clear gap after it.
        _nextOrange = Mathf.Max(_nextOrange, _orangesClear);
        // Each group gets its own spawn point at a random height, at the
        // right edge where THE PIT's spawn points are.
        var point = new GameObject("Orange Group Spawn").transform;
        point.SetParent(transform, false);
        float x = _groundPoint ? _groundPoint.position.x : template.spawnPoint.position.x;
        bool high = true;   // (always out of reach on foot: she jumps for them - Wayne's call; highOrangeChance unused)
        var range = high ? highOrangeHeight : orangeHeight;
        point.position = new Vector3(x, SpikeTrap.PlayerFeetY + Random.Range(range.x, range.y), SpikeTrap.LaneZ);   // (at her reach)
        float roadPerFrame = _spikeDrift * collectables.worldUnitsPerFrame;
        float spacing = roadPerFrame > 0f ? orangeSpacing / roadPerFrame : template.spawnFrameSpacing;
        if (SpatialEmulator.Demo.PitDemoGame.Level <= 2) spacing *= 1.3f;   // (level 1's slices and level 2's candy corn, half again as big: a little room between them)
        else if (SpatialEmulator.Demo.PitDemoGame.Level == 4) spacing *= 1.6f;   // (level 4's mushrooms: well apart)
        LevelCollection.ClaimRoadCandy(point.position.y);   // (its treat chosen now)
        collectables.spawnEntries.Add(new CollectableSpawnEntry
        {
            prefab = template.prefab,
            spawnPoint = point,
            triggerFrame = _nextOrange,
            spawnCount = size,
            spawnFrameSpacing = spacing,
            frameVisibilityRange = 400f,
            parallaxDriftMultiplier = _spikeDrift,   // locked to the road
            reflectionPositionOffset = template.reflectionPositionOffset,
            endPoint = _endPoint,
        });
        // Something under the high ones: spikes (sometimes a pair) or a bomb guy.
        if (high && Random.value < hazardUnderOrangesChance)
        {
            float under = _nextOrange + size * spacing * Random.Range(0.3f, 0.7f);
            if (HasBombs && Random.value < 0.35f) QueueBomb(under, single: true);
            else if (spikePrefab)
            {
                QueueHazard(spikePrefab, under);
                if (Random.value < 0.4f) QueueHazard(spikePrefab, under + SpikeWidthFrames());
            }
        }
        _nextOrange += size * spacing + Random.Range(groupGap.x, groupGap.y);
        _orangesClear = _nextOrange;
    }

    float _orangesClear;   // (the road frame the last group queued is all out and past its gap: QueueOrangeGroup)

    void QueueSpike()
    {
        QueueHazard(spikePrefab, _nextSpike);
        // Sometimes a second trap right behind it, or two: one long row to
        // clear (a row of three takes a double jump).
        float row = Random.value;
        int extra = row < tripleSpikeChance ? 2 : row < tripleSpikeChance + doubleSpikeChance ? 1 : 0;
        if (SpatialEmulator.Demo.PitDemoGame.Level == 4) extra = Mathf.Min(extra, 1);   // (level 4's bone arms: two at most, never three)
        if (RoadUnitsPerFrame > 0f)
            for (int i = 1; i <= extra; i++)
                QueueHazard(spikePrefab, _nextSpike + i * SpikeWidthFrames());
        // ARcade: on level 3, sometimes a saw blade between it and the next one (room to land first; the toads
        // queued below move on past it, as past the spikes).
        if (SawTemplate() && Random.value < sawChance)
        {
            float at = _nextSpike + spikeSpacing * Random.Range(0.4f, 0.5f);
            QueueHazard(_sawTemplate, at);
            // (sometimes two in a row, nearly touching: a double jump)
            if (Random.value < 0.5f && RoadUnitsPerFrame > 0f) QueueHazard(_sawTemplate, at + SpikeTrap.SawWidth * 1.05f / RoadUnitsPerFrame);
        }
        // Bomb guys - only between bosses (a Slime Boss sends its own two):
        // sometimes one just ahead of it, to deal with first, one halfway
        // to the next spike trap, and sometimes another before that.
        bool bossFight = _bossSpawned && !_bossDead;
        if (HasBombs && !bossFight)
        {
            if (Random.value < bombBeforeSpikeChance) QueueBomb(_nextSpike - Random.Range(15f, 30f), single: true);
            if (Random.value < bombChance) QueueBomb(_nextSpike + spikeSpacing * 0.5f);
            if (Random.value < extraBombChance && !_foes) QueueBomb(_nextSpike + spikeSpacing * 0.7f);
        }
        _nextSpike += spikeSpacing;
    }

    // ARcade: level 3's saw blade - a copy of the spike trap made a saw (SpikeTrap.saw), kept hidden here as the
    // template the road's saws are copied from.
    GameObject _sawTemplate;

    bool SawTemplate()
    {
        if (_sawTemplate) return true;
        if (SpatialEmulator.Demo.PitDemoGame.Level != 3 || !spikePrefab) return false;
        _sawTemplate = Instantiate(spikePrefab, transform);
        _sawTemplate.name = "SAW TEMPLATE";
        _sawTemplate.GetComponent<SpriteRenderer>().enabled = false;
        var trap = _sawTemplate.GetComponent<SpikeTrap>();
        trap.saw = true;
        trap.template = true;
        return true;
    }

    bool IsTrap(GameObject prefab) => prefab && (prefab == spikePrefab || prefab == _sawTemplate);

    /// A spike trap or bomb guy entering at the right edge at frame `trigger`, moving with the road.
    void QueueHazard(GameObject prefab, float trigger)
    {
        // (no traps while a boss is in - none round it or its summoned foes: Wayne's)
        if (IsTrap(prefab) && _bossSpawned && !_bossDead) return;
        // (level 4's bone arms: each well clear of the next, and never three together - two at most)
        if (IsTrap(prefab) && prefab == spikePrefab && SpatialEmulator.Demo.PitDemoGame.Level == 4)
        {
            float gap = SpikeWidthFrames();
            for (int guard = 0; guard < 6; guard++)
            {
                bool moved = false;
                foreach (float f in _spikeFrames) if (Mathf.Abs(f - trigger) < gap * 0.95f) { trigger = f + gap; moved = true; break; }
                if (!moved) break;
            }
            int near = 0;
            foreach (float f in _spikeFrames) if (Mathf.Abs(f - trigger) <= gap * 1.6f) near++;
            if (near >= 2) return;
        }
        if (IsTrap(prefab))
        {
            _spikeFrames.Add(trigger);
            if (_spikeFrames.Count > 64) _spikeFrames.RemoveAt(0);
            // Bomb guys or foes already waiting just past it (queued earlier, under oranges) move on past it.
            foreach (var e in collectables.spawnEntries)
                if (e.spawnedCount == 0 && e.prefab && !IsTrap(e.prefab) && IsFoe(e.prefab) && e.triggerFrame >= trigger - ClearBefore && e.triggerFrame < trigger + ClearAfter)
                    e.triggerFrame = trigger + ClearAfter;
        }
        collectables.spawnEntries.Add(new CollectableSpawnEntry
        {
            prefab = prefab,
            spawnPoint = _groundPoint,
            triggerFrame = trigger,
            spawnCount = 1,
            frameVisibilityRange = 200f,
            parallaxDriftMultiplier = _spikeDrift,
            endPoint = _endPoint,
        });
    }

    /// Frames of road scroll that move one spike trap's width.
    float SpikeWidthFrames()
    {
        var trap = spikePrefab ? spikePrefab.GetComponent<SpikeTrap>() : null;
        float width = SpikeTrap.SpikesWidthPixels * SpikeTrap.PixelSize * (trap ? trap.scale : 0.55f);
        // (level 4's bone arms, in a group: well apart, not one on top of the next)
        if (SpatialEmulator.Demo.PitDemoGame.Level == 4) width = SpikeTrap.BoneArmGapPixels * SpikeTrap.PixelSize;
        return RoadUnitsPerFrame > 0f ? width / RoadUnitsPerFrame : 3f;
    }

    // ARcade: bomb guys, or this level's own foes in their place.
    bool HasBombs => _foeTemplates != null || bombPrefab;
    bool HasOwnBoss => _foes && _foes.boss != null && _foes.boss.looks != null && _foes.boss.looks.Length > 0;
    bool BossSummons => HasOwnBoss && _foes.boss.maxSummons > 0;

    /// A bomb guy at `at`, or a group of this level's foes (single: just one).
    // Pushed on past any spike trap just before it (or one of a row), so she has room to land and swing.
    bool IsFoe(GameObject prefab) => prefab == bombPrefab || (_foeTemplates != null && System.Array.IndexOf(_foeTemplates, prefab) >= 0);

    // (level 4's foes well clear of its hell hands either side: they get confusing tangled up with them)
    bool HandsOut => SpatialEmulator.Demo.PitDemoGame.Level == 4 && RoadUnitsPerFrame > 0f;
    float ClearBefore => HandsOut ? RoadEnemy.HandKeepClear / RoadUnitsPerFrame : 6f;
    float ClearAfter => HandsOut ? Mathf.Max(clearAfterSpikes, RoadEnemy.HandKeepClear / RoadUnitsPerFrame) : clearAfterSpikes;

    float ClearOfSpikes(float at)
    {
        for (int pass = 0; pass < 4; pass++)
        {
            bool moved = false;
            foreach (float s in _spikeFrames)
                if (at >= s - ClearBefore && at < s + ClearAfter) { at = s + ClearAfter; moved = true; }
            if (!moved) break;
        }
        return at;
    }

    void QueueBomb(float at, bool single = false)
    {
        at = ClearOfSpikes(at);
        bool boss = _bossSpawned && !_bossDead;
        if (_foeTemplates == null || (!boss && bombPrefab && Random.value < _foes.bombShare)) { if (bombPrefab) QueueHazard(bombPrefab, at); return; }
        // (a swarm, if that's what comes: level 3's rats, packed close)
        if (!single && _foes.road != null)
        {
            int first = PickFoe();
            int most = first < _foes.road.Length ? _foes.road[first].swarm : 0;
            if (most > 0)
            {
                var kinds = new System.Collections.Generic.List<int>();
                for (int k = 0; k < _foes.road.Length && k < _foeTemplates.Length; k++) if (_foes.road[k].swarm > 0) kinds.Add(k);
                int size = Random.Range(most / 2 + 1, most + 1);
                for (int i = 0; i < size; i++)
                    QueueHazard(_foeTemplates[kinds[Random.Range(0, kinds.Count)]], at + i * _foes.groupSpacing * 0.45f * Random.Range(0.8f, 1.25f));
                return;
            }
        }
        int count = single ? 1 : Random.Range(1, Mathf.Max(1, _foes.groupMax) + 1);
        var inGroup = new int[_foeTemplates.Length];
        int must = -1;   // (a kind every group has one of - level 4's revenants - comes first)
        for (int k = 0; k < _foes.road.Length && k < _foeTemplates.Length; k++) if (_foes.road[k].onePerGroup) { must = k; break; }
        for (int i = 0; i < count; i++)
        {
            // (no more of one kind in a group than its maxPerGroup - level 4's one revenant among the skeletons)
            int pick = i == 0 && must >= 0 ? must : PickFoe();
            for (int tries = 0; tries < 8 && FullUp(pick, inGroup); tries++) pick = PickFoe();
            if (FullUp(pick, inGroup)) continue;
            inGroup[pick]++;
            QueueHazard(_foeTemplates[pick], ClearOfSpikes(at + i * _foes.groupSpacing * Random.Range(0.8f, 1.25f)));
        }
    }

    bool FullUp(int pick, int[] inGroup)
    {
        int most = _foes.road != null && pick < _foes.road.Length ? _foes.road[pick].maxPerGroup : 0;
        return most > 0 && inGroup[pick] >= most;
    }

    int PickFoe()
    {
        var w = _foes.roadWeights;
        if (w == null || w.Length != _foeTemplates.Length) return Random.Range(0, _foeTemplates.Length);
        float total = 0f; foreach (float x in w) total += Mathf.Max(0f, x);
        float r = Random.value * total;
        for (int i = 0; i < w.Length; i++) { r -= Mathf.Max(0f, w[i]); if (r <= 0f) return i; }
        return 0;
    }

    // ARcade: in the arena, she's walked to its middle and held there; the boss comes once she's stood there a
    // moment (or a few seconds on, wherever she's got to) - and she's hers again as it leaps in.
    bool InMiddle()
    {
        var player = Player;
        if (!player) return true;
        float middle = (_arena.ViewMinX + _arena.ViewMaxX) * 0.5f;
        player.HoldX = middle;
        float off = player.transform.position.x - middle;
        bool there = off > -0.06f && off < 0.3f;   // (a step past it is there: PlayerScriptARIANAClips.HoldX)
        if (!there) _heldSince = -1f;
        else if (_heldSince < 0f) _heldSince = Time.time;
        return (there && Time.time - _heldSince > 0.35f) || Time.time - _arena.LockedAt > 4f;
    }
    float _heldSince = -1f;

    // Level 1's Slime Bosses: SLIMER I to IX, then BIG SLIMER (and BIG SLIMER II, III... on a run that goes on).
    static string SlimeName(int n) => n < 10 ? "SLIMER " + RoadBoss.Roman(n) : n == 10 ? "BIG SLIMER" : "BIG SLIMER " + RoadBoss.Roman(n - 9);

    void SpawnBoss(float frames)
    {
        if (Player) Player.HoldX = float.NaN;
        _heldSince = -1f;
        // Its own two bomb guys only: ones queued ahead, not yet out, stay away - and no traps round it (Wayne's):
        // none queued, and any out ahead of her gone in a puff.
        collectables.spawnEntries.RemoveAll(e => (e.prefab == bombPrefab || IsTrap(e.prefab) || (_foeTemplates != null && System.Array.IndexOf(_foeTemplates, e.prefab) >= 0)) && e.spawnedCount == 0);
        float ahead = player ? player.transform.position.x + 0.6f : float.MinValue;
        foreach (var trap in FindObjectsByType<SpikeTrap>(FindObjectsSortMode.None))
            if (!trap.template && trap.transform.position.x > ahead)
            {
                FoeFx.Poof(trap.transform.position + Vector3.up * 0.05f, 6, 0.8f);
                Destroy(trap.gameObject);
            }
        _bossSpawned = true;
        _bossSpawnTime = Time.time;
        _bossBombsSent = 0;
        if (HasOwnBoss) { SpawnOwnBoss(); return; }
        enemies.spawnEntries.Add(new ParallaxSpawnEntry
        {
            prefab = _bossTemplate.prefab,
            spawnPoint = _bossTemplate.spawnPoint,
            triggerFrame = frames,
            spawnCount = 1,
            aiTakeoverDistance = _bossTemplate.aiTakeoverDistance,
            parallaxDriftMultiplier = _bossTemplate.parallaxDriftMultiplier,
        });
    }

    // ARcade: this level's boss (LevelFoes.Boss): walking in from the right
    // edge; each one after the first in the next of its colours, bigger, with
    // more health - as the Slime Boss's; its chest and sounds the Slime Boss's.
    int _ownBosses, _mainCount, _altCount;

    void SpawnOwnBoss()
    {
        _bossLevel++;
        // Two of its own taking turns (or altRun of the second, then the first - level 4: four vikings, then the dragon), each its own colours and growth.
        bool alt = _foes.altBoss != null && _foes.altBoss.looks != null && _foes.altBoss.looks.Length > 0
            && (_foes.altRun > 0 ? _ownBosses % (_foes.altRun + 1) < _foes.altRun : _ownBosses % 2 == 1);   // (or a run of the second, then the first)
        _ownBosses++;
        var spec = alt ? _foes.altBoss : _foes.boss;
        int nth = alt ? ++_altCount : ++_mainCount;
        var go = new GameObject("BOSS " + spec.name);
        go.layer = LayerMask.NameToLayer("PitActors");
        float half = 2.7f;
        var cam = SpatialEmulator.Demo.PitDemoGame.ActorsCamera;
        if (cam && cam.orthographic) half = cam.orthographicSize * cam.aspect;
        go.transform.position = new Vector3((cam ? cam.transform.position.x : player.transform.position.x) + half + 0.6f, SpikeTrap.PlayerFeetY, SpikeTrap.LaneZ);
        var boss = go.AddComponent<RoadBoss>();
        boss.boss = spec;
        boss.look = spec.looks[(nth - 1) % spec.looks.Length];
        boss.nth = nth;
        float growth = spec.growth > 0f ? spec.growth : bossGrowth, most = spec.maxSize > 0f ? spec.maxSize : bossMaxSize;
        boss.size = Mathf.Min(most, 1f + growth * Mathf.Max(0, nth - 1 - spec.growAfter + (spec.growAfter > 0 ? 1 : 0)));   // (growAfter: the same size till then)
        boss.Setup(Mathf.RoundToInt(spec.health * (1f + bossHealthStep * (nth - 1))));
        var slime = _bossTemplate != null && _bossTemplate.prefab ? _bossTemplate.prefab.GetComponentInChildren<EnemyStateMachineSlimeBoss>(true) : null;
        if (slime)
        {
            boss.chestPrefab = slime.chestPrefab;
            boss.hurtSounds = slime.hurtSounds;
            boss.deathSounds = slime.deathSounds;
        }
    }
}
