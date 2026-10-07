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
    public Vector2 groupGap = new Vector2(30f, 110f);
    public float firstOrange = 30f;
    [Tooltip("Height range of a low group above the road, in world units: reached walking (random per group).")]
    public Vector2 orangeHeight = new Vector2(0.5f, 0.75f);
    [Tooltip("Height range of a high group: out of reach unless the player jumps.")]
    public Vector2 highOrangeHeight = new Vector2(0.95f, 1.35f);
    [Range(0f, 1f)] public float highOrangeChance = 0.5f;
    [Tooltip("Distance between the slices in a group, in world units.")]
    public float orangeSpacing = 0.3f;

    [Header("Spikes")]
    public float firstSpike = 90f;
    [Tooltip("Frames between spike traps (equal distances).")]
    public float spikeSpacing = 80f;
    [Tooltip("Chance a spike trap has a second one right beside it.")]
    [Range(0f, 1f)] public float doubleSpikeChance = 0.45f;
    [Tooltip("Chance a spike trap is a row of three instead (a double jump to clear).")]
    [Range(0f, 1f)] public float tripleSpikeChance = 0.2f;
    [Tooltip("The road art moves this many pixels per frame of the player's scroll counter.")]
    public float roadPixelsPerFrame = 7f;

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

        _groundPoint = transform.Find("SPAWN POINT MAIN");
        _endPoint = transform.Find("COLLECTABLE END POINT");
        _nextOrange = firstOrange;
        _nextSpike = firstSpike;
        _nextBossScore = bossScore;
        TreasureChest.RunCount = 0;
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
    static readonly float[] AfterChestBombs = { 10f, 40f, 70f };
    static readonly float[] AfterChestSpikes = { 25f, 55f };
    const float AfterChestStretch = 90f;

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
                if (bombPrefab) foreach (float at in AfterChestBombs) QueueHazard(bombPrefab, frames + at);
                if (spikePrefab) foreach (float at in AfterChestSpikes) QueueHazard(spikePrefab, frames + at);
                _nextOrange = frames + AfterChestStretch;
                _nextSpike = frames + AfterChestStretch + spikeSpacing * 0.5f;
            }
        }
        else
        {
            while (_nextOrange < frames + lookAhead && _orangeTemplates.Count > 0) QueueOrangeGroup();
            while (_nextSpike < frames + lookAhead && spikePrefab) QueueSpike();
        }

        BlockAtBoss();

        // More bomb guys walk in during the boss fight.
        if (_bossSpawned && !_bossDead && bombPrefab && _bossBombsSent < bossBombs && Time.time >= _nextBossBomb && Time.time > _bossSpawnTime + 3f)
        {
            _bossBombsSent++;
            _nextBossBomb = Time.time + bossBombInterval * Random.Range(0.75f, 1.25f);
            QueueHazard(bombPrefab, frames);
        }

        // Points collected (chests and their gems aside).
        int score = (PitScoreManager.Instance ? PitScoreManager.Instance.GetScore() : 0) - TreasureChest.BonusPoints;
        if (!_bossSpawned && _bossTemplate != null && score >= _nextBossScore) SpawnBoss(frames);
        else if (_bossSpawned && FindObjectsByType<EnemyStateMachineSlimeBoss>(FindObjectsSortMode.None).Length == 0
                 && Time.time > _bossSpawnTime + 2f)
            _bossSpawned = false;   // gone (the next one was set when it died)
    }

    float _bossSpawnTime;
    float _quietLeft;
    int _bossLevel;         // bosses so far this run
    bool _bossDead;
    readonly HashSet<EnemyStateMachineSlimeBoss> _knownBosses = new HashSet<EnemyStateMachineSlimeBoss>();

    // New bosses get their level's colour, health and speed; a dying one clears the road.
    void TrackBosses()
    {
        if (!_bossSpawned) return;
        foreach (var boss in FindObjectsByType<EnemyStateMachineSlimeBoss>(FindObjectsSortMode.None))
        {
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
                }
                boss.Grow(BossScale * Mathf.Min(bossMaxSize, 1f + bossGrowth * (_bossLevel - 1)));
                // Its health bar over its head, sized to it as grown.
                var hud = FindAnyObjectByType<HudJuice>();
                BossHealthBar.Attach(boss, hud && hud.healthText ? hud.healthText.font : null);
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
        foreach (var boss in FindObjectsByType<EnemyStateMachineSlimeBoss>(FindObjectsSortMode.None))
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
            if (spike.transform.position.x > x - 0.6f) Destroy(spike.gameObject);
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
        if (bosses > 0 && FindObjectsByType<EnemyStateMachineSlimeBoss>(FindObjectsSortMode.None).Length == 0)
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
            foreach (var boss in FindObjectsByType<EnemyStateMachineSlimeBoss>(FindObjectsSortMode.None))
            {
                if (boss.IsDead || !boss.BodyCollider) continue;
                var body = boss.BodyCollider.bounds;
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
        player.BlockedDirection = blocked;
        player.WallMin = wallMin;
        player.WallMax = wallMax;
    }

    float _bodyOffset = float.NaN;
    readonly Dictionary<EnemyStateMachineSlimeBoss, int> _bossSide = new Dictionary<EnemyStateMachineSlimeBoss, int>();

    void QueueOrangeGroup()
    {
        var template = _orangeTemplates[Random.Range(0, _orangeTemplates.Count)];
        int size = GroupSize;
        // Each group gets its own spawn point at a random height, at the
        // right edge where THE PIT's spawn points are.
        var point = new GameObject("Orange Group Spawn").transform;
        point.SetParent(transform, false);
        float x = _groundPoint ? _groundPoint.position.x : template.spawnPoint.position.x;
        bool high = Random.value < highOrangeChance;
        var range = high ? highOrangeHeight : orangeHeight;
        point.position = new Vector3(x, SpikeTrap.PlayerFeetY + Random.Range(range.x, range.y), SpikeTrap.LaneZ);   // (at her reach)
        float roadPerFrame = _spikeDrift * collectables.worldUnitsPerFrame;
        float spacing = roadPerFrame > 0f ? orangeSpacing / roadPerFrame : template.spawnFrameSpacing;
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
            if (bombPrefab && Random.value < 0.35f) QueueHazard(bombPrefab, under);
            else if (spikePrefab)
            {
                QueueHazard(spikePrefab, under);
                if (Random.value < 0.4f) QueueHazard(spikePrefab, under + SpikeWidthFrames());
            }
        }
        _nextOrange += size * spacing + Random.Range(groupGap.x, groupGap.y);
    }

    void QueueSpike()
    {
        QueueHazard(spikePrefab, _nextSpike);
        // Sometimes a second trap right behind it, or two: one long row to
        // clear (a row of three takes a double jump).
        float row = Random.value;
        int extra = row < tripleSpikeChance ? 2 : row < tripleSpikeChance + doubleSpikeChance ? 1 : 0;
        if (RoadUnitsPerFrame > 0f)
            for (int i = 1; i <= extra; i++)
                QueueHazard(spikePrefab, _nextSpike + i * SpikeWidthFrames());
        // Bomb guys - only between bosses (a Slime Boss sends its own two):
        // sometimes one just ahead of it, to deal with first, one halfway
        // to the next spike trap, and sometimes another before that.
        bool bossFight = _bossSpawned && !_bossDead;
        if (bombPrefab && !bossFight)
        {
            if (Random.value < bombBeforeSpikeChance) QueueHazard(bombPrefab, _nextSpike - Random.Range(15f, 30f));
            if (Random.value < bombChance) QueueHazard(bombPrefab, _nextSpike + spikeSpacing * 0.5f);
            if (Random.value < extraBombChance) QueueHazard(bombPrefab, _nextSpike + spikeSpacing * 0.25f);
        }
        _nextSpike += spikeSpacing;
    }

    /// A spike trap or bomb guy entering at the right edge at frame `trigger`, moving with the road.
    void QueueHazard(GameObject prefab, float trigger)
    {
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
        return RoadUnitsPerFrame > 0f ? width / RoadUnitsPerFrame : 3f;
    }

    void SpawnBoss(float frames)
    {
        // Its own two bomb guys only: ones queued ahead, not yet out, stay away.
        collectables.spawnEntries.RemoveAll(e => e.prefab == bombPrefab && e.spawnedCount == 0);
        _bossSpawned = true;
        _bossSpawnTime = Time.time;
        _bossBombsSent = 0;
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
}
