using System.Collections.Generic;
using UnityEngine;

// ENDLESS KNIGHT's fairy: a helper from every 3rd treasure chest
// (TreasureChest). She rises out of the kicked-open chest and waits there,
// bobbing, for the player to come and collect her (she blinks out if left
// too long); collected, she floats to the player's shoulder, then for
// lifetime seconds shoots ice spikes at the
// Slime Boss and the bomb guys while they're on screen - flying up a
// little and beating her wings faster as she fires. A spike chips the boss
// (a little damage and a push back, EnemyStateMachineSlimeBoss.Chip) and
// sets a bomb guy off where he stands (harmlessly, BombGuy.Detonate). Then
// she blinks out, until the next fairy chest. She's gone the moment the
// player dies.
//
// Each fairy chest brings her in the next colour (her spikes, shot sound and
// hit bursts to match), and tapping her in AR (PitDemoGame.TryTap) changes
// it too.
//
// She hangs from the player's root, not their sprite, which the attack
// animations move about (EndlessKnightBuilder moves her there), and she's
// animated here, not by an Animator, so her look can change. She keeps her
// place (behind the player's right shoulder) and faces the same way when
// the player turns around, which flips the player's root: she's placed
// after the player moves (LateUpdate, late in the frame), unmirrored.
//
// ARcade: on level 3 she's a flying black cat demon instead, on level 4 an orange one (Fantasy #8, Resources/Collect/
// CatDemon, their pixels the knight's size): the same chests, the same visits, but in place of ice spikes it meows,
// opens its mouth and fires a laser beam - a thin pixel-art neon line, as THE TIME CRYSTAL's drones' lock-on, purple
// or green, with bloom and sparks at its mouth and where it strikes, and THE TIME CRYSTAL's laser sound - turning
// onto the nearest target and destroying any foe it touches.
// Bosses are drained, not felled: each beam takes a set share of their health (three beams at most, a dragon five).
[DefaultExecutionOrder(100)]
public class FairyAlly : MonoBehaviour
{
    [System.Serializable]
    public class Look
    {
        public Sprite[] frames;
        public Sprite[] spikeFrames;
        [Tooltip("Played on each shot.")]
        public AudioClip shotSound;
        [Tooltip("The burst where a spike hits (Blood FX's ClotBurst in the spike's colour).")]
        public Sprite[] impactFrames;
    }

    public Look[] looks;
    public float fps = 10f;
    [Tooltip("Wing beats while shooting.")]
    public float shootingFps = 22f;

    [Header("Visits")]
    [Tooltip("Seconds she stays, from being collected.")]
    public float lifetime = 30f;
    [Tooltip("Seconds she waits over the chest to be collected before she leaves.")]
    public float waitTime = 12f;
    public AudioClip collectSound;
    [Tooltip("She blinks for her last this many seconds, fading out over the last one.")]
    public float blinkTime = 3f;
    [Tooltip("Rising out of the chest: how high (her pixels) and how long; then the flight to the player.")]
    public float risePixels = 28f;
    public float riseTime = 0.9f, flyTime = 1f;
    [Tooltip("As she rises she drifts this far (her pixels) away from the player, who has to walk over to collect her.")]
    public float driftPixels = 45f;
    public AudioClip arriveSound;

    [Header("Ice spikes")]
    public float spikeFps = 15f;
    [Tooltip("Seconds between spikes while a target is on screen: a machine gun.")]
    public float fireInterval = 0.1f;
    [Tooltip("Flight speed, in her pixels per second.")]
    public float spikeSpeed = 420f;
    public int spikeDamage = 1;
    [Tooltip("How far each spike pushes the boss back, in her pixels (it can't walk forward under fire).")]
    public float knockback = 3f;
    [Tooltip("Scatter: each spike's aim is off by up to this many degrees, and it leaves from up to this many of her pixels above or below.")]
    public float spreadDegrees = 4f;
    public float spreadPixels = 3f;
    [Tooltip("How far she flies up (her pixels) while shooting.")]
    public float shootingLiftPixels = 14f;
    [Tooltip("Sorting layer and order: over the Slime Boss (on Default), under the Holy Slash.")]
    public string spikeSortingLayer = "Default";
    public int spikeSortingOrder = 150;
    const int OverFoesOrder = 222;   // ARcade: her spikes and bursts, over the foes (205-217) and the knight's blows (219)
    public float impactFps = 24f;
    [Tooltip("The sparks where a spike breaks (FoeFx.Impact).")]
    public Color impactColor = new Color(0.55f, 0.9f, 1f);
    [Tooltip("Played when a spike hits the boss (one at random).")]
    public AudioClip[] hitSounds;
    [Range(0f, 1f)] public float shotVolume = 0.35f;
    [Range(0f, 1f)] public float hitVolume = 0.5f;
    public AudioClip tapSound;

    // A machine gun's sounds each start by cutting off the oldest still
    // ringing (they run a second or two), rather than piling up.
    const int ShotVoices = 3, HitVoices = 3;
    AudioSource[] _shotVoices, _hitVoices;
    int _nextShotVoice, _nextHitVoice;

    static int s_look, s_visits;

    SpriteRenderer _renderer;
    PlayerScriptARIANAClips _player;
    Vector3 _home;
    enum State { Away, Rising, Waiting, Flying, Helping }
    State _state;
    bool _here => _state != State.Away;
    float _since, _nextFire, _nextLook, _shootingUntil, _lift, _roadFrames;
    Vector3 _riseFrom, _waitAt, _flightFrom, _flightPosition;
    bool _inFlight => _state != State.Helping;
    SpriteRenderer _playerBody;

    /// Over the chest, not yet collected (EndlessKnightDirector holds new
    /// spawns meanwhile, as for gems).
    public bool Waiting => _state == State.Rising || _state == State.Waiting;
    IRoadBoss _boss;
    readonly List<BombGuy> _bombs = new List<BombGuy>();
    readonly HashSet<BombGuy> _shotAt = new HashSet<BombGuy>();
    readonly HashSet<RoadEnemy> _foesShotAt = new HashSet<RoadEnemy>();   // (the levels' own small foes: one spike in the air at each - then another, till it's dead)

    /// Its spike at `foe` has landed (or gone): she can shoot at it again if it's still up.
    public void SpikeDone(RoadEnemy foe) { if (foe) _foesShotAt.Remove(foe); }
    readonly HashSet<SpikeTrap> _trapsShotAt = new HashSet<SpikeTrap>();   // (level 2's tentacles: one spike each)
    public void SpikeDone(SpikeTrap trap) { if (trap) _trapsShotAt.Remove(trap); }

    public static FairyAlly Current { get; private set; }

    /// Out and about (she can be tapped).
    public bool Visible => _here && _renderer.enabled;

    void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        var animator = GetComponent<Animator>();
        if (animator) animator.enabled = false;
        Current = this;
        _home = transform.localPosition;
        _shotVoices = Voices(ShotVoices);
        _hitVoices = Voices(HitVoices);
        Leave();
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
        if (_beam) Destroy(_beam);
    }

    // ---- level 4's cat demon ----

    static int Level => SpatialEmulator.Demo.PitDemoGame.Level;
    bool Cat => Level >= 3 && _catIdle != null;
    const float CatFps = 10f, CatChargeFps = 12f, CatRecoverFps = 14f;
    const float BeamSeconds = 1.4f, CatRest = 0.9f, BeamLength = 16f, BeamTurn = 360f;   // (seconds; world units; degrees a second)
    const int CatChargeFrames = 5, CatRecoverFrom = 5, CatFrames_ = 11;
    static Color BeamColor => Level == 3 ? new Color(0.72f, 0.2f, 1f) : new Color(0.25f, 1f, 0.35f);
    static readonly Vector2 CatMouth = new Vector2(-1f, 14f);   // (its art's pixels from its middle, at full stretch)
    Sprite[] _catIdle, _catMove, _catAttack;
    bool _catFiring, _catLeft;
    const int BeamChips = 7;
    const float BossShare = 1f / 3f, DragonShare = 1f / 5f;
    int _beamChips;
    float _catFrom, _beamAngle, _nextChip;
    GameObject _beam;
    SpriteRenderer _beamGlow, _beamLine, _beamCore, _mouthGlow, _hitGlow;
    float _nextSpark;
    int _beamLevel, _catLevel;
    static Sprite s_beamPixel, s_mouthPixel;
    static AudioClip s_meow;
    // Hard pixels bursting at its mouth, over where the beam starts.
    const int MouthPixels = 8;
    readonly SpriteRenderer[] _mouthPixels = new SpriteRenderer[MouthPixels];
    float _nextMouthStep;
    TimeCrystal.TcLaserVoice _laser;
    bool _laserOn;

    void LoadCat()
    {
        _catLeft = false;
        if (Level < 3) return;
        if (_catIdle != null && _catLevel == Level) return;
        _catLevel = Level;
        // (one of its pixels the size of one of the knight's)
        float perUnit = Mathf.Abs(transform.lossyScale.y) / RoadFoe.Pixel;
        string kind = Level == 3 ? "black_" : "";
        _catIdle = CatSheet(kind + "idle", perUnit);
        _catMove = CatSheet(kind + "move", perUnit);
        _catAttack = CatSheet(kind + "attack", perUnit);
        if (_catIdle == null || _catMove == null || _catAttack == null) { _catIdle = null; return; }
    }

    static Sprite[] CatSheet(string name, float perUnit)
    {
        var sheet = Resources.Load<Texture2D>("Collect/CatDemon/cat_" + name);
        if (!sheet) return null;
        var frames = new Sprite[sheet.width / 64];
        for (int i = 0; i < frames.Length; i++)
            frames[i] = Sprite.Create(sheet, new Rect(i * 64, 0, 64, 64), new Vector2(0.5f, 0.5f), perUnit);
        return frames;
    }

    Sprite CatFrames(Sprite[] frames) => frames[(int)(Time.time * CatFps) % frames.Length];

    // Its mouth, in the world: stretched wide over the attack frames.
    Vector3 Mouth()
    {
        var at = CatMouth * Pixel;
        return transform.position + new Vector3(_catLeft ? -at.x : at.x, at.y, -0.01f);
    }

    // The nearest thing on screen to burn: a level's small foe, a hell hand or the boss.
    bool CatTarget(out Vector3 at)
    {
        at = default;
        var from = transform.position;
        float best = float.MaxValue;
        if (RoadFoe.Here(_boss) && !_boss.IsDead && _boss.Body && OnScreen(_boss.Body.bounds))
        {
            at = _boss is RoadBoss own && own.HitPoint(out var point) ? point : RoadFoe.Drawn(_boss.Body).center;
            best = Mathf.Abs(at.x - from.x);
        }
        foreach (var f in RoadEnemy.All)
        {
            if (!f || !f.Targetable || !OnScreen(f.Art.bounds)) continue;
            float d = Mathf.Abs(f.Art.bounds.center.x - from.x);
            if (d < best) { best = d; at = f.Art.bounds.center; }
        }
        foreach (var t in SpikeTrap.Tentacles)
        {
            if (!t || !t.Targetable || !OnScreen(t.Art.bounds)) continue;
            float d = Mathf.Abs(t.Art.bounds.center.x - from.x);
            if (d < best) { best = d; at = RoadFoe.Drawn(t.Art).center; }
        }
        return best < float.MaxValue;
    }

    void CatAttack()
    {
        if (Time.time >= _nextLook)
        {
            _nextLook = Time.time + 0.25f;
            _boss = null;
            foreach (var b in RoadFoe.Bosses()) if (!b.IsDead) { _boss = b; break; }
        }
        bool target = CatTarget(out var aim);
        if (!_catFiring)
        {
            // Facing what it's after - behind the knight too - or, with nothing about, the way she faces.
            _catLeft = target ? aim.x < transform.position.x : !_player.FacingRight;
            if (Time.time < _nextFire || !target) return;
            _catFiring = true;
            _catFrom = Time.time;
            _beamChips = 0;
            _catLeft = aim.x < transform.position.x;
            var to = aim - transform.position;
            _beamAngle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        }
        _shootingUntil = Time.time + 0.2f;
        float t = Time.time - _catFrom, charge = CatChargeFrames / CatChargeFps;
        if (t < charge)
        {
            _renderer.sprite = _catAttack[Mathf.Min((int)(t * CatChargeFps), CatChargeFrames - 1)];
            return;
        }
        if (t < charge + BeamSeconds)
        {
            // Mouth wide, shuddering, the beam out of it.
            _renderer.sprite = _catAttack[(int)(t * 16f) % 2 == 0 ? 4 : 3];
            Beam(target, aim);
            return;
        }
        StopBeam();
        int frame = CatRecoverFrom + (int)((t - charge - BeamSeconds) * CatRecoverFps);
        if (frame < CatFrames_) { _renderer.sprite = _catAttack[frame]; return; }
        _catFiring = false;
        _nextFire = Time.time + CatRest;
    }

    // Hard pixel lines over a soft glow, in this level's colour.
    // Hovering (her pixels): one slow, smooth rise and fall - easing down to a small one while it's firing.
    const float BobFps = 0.6f, BobPixels = 7f, FiringBob = 0.3f, BobEase = 1.5f;
    float _bobSize = 1f;
    float CatBob()
    {
        if (!Cat) return 0f;
        _bobSize = Mathf.MoveTowards(_bobSize, _catFiring ? FiringBob : 1f, BobEase * Time.deltaTime);
        return Mathf.Sin(Time.time * BobFps * Mathf.PI * 2f) * BobPixels * _bobSize;
    }

    // Its meow, a little higher or lower each time: on being picked up, and with every beam.
    static void Meow()
    {
        if (!s_meow) s_meow = Resources.Load<AudioClip>("Collect/CatDemon/cat_meow");
        var voice = PitAudio.Voice(s_meow, 1f);
        if (voice) voice.pitch = Random.Range(0.92f, 1.16f);   // (no lower: its voice is removed at the clip's own length)
    }

    void MakeBeam()
    {
        if (_beam) Destroy(_beam);
        _beamLevel = Level;
        if (!s_beamPixel)
        {
            var t = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            t.SetPixel(0, 0, Color.white);
            t.Apply();
            s_beamPixel = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            s_mouthPixel = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
        _beam = new GameObject("CAT BEAM");
        _beam.layer = LayerMask.NameToLayer("PitActors");
        var colour = BeamColor;
        _beamGlow = BeamPart("GLOW", FoeFx.GlowSprite, OverFoesOrder - 1, new Color(colour.r, colour.g, colour.b, 0.45f));
        _beamLine = BeamPart("LINE", s_beamPixel, OverFoesOrder, colour);
        _beamCore = BeamPart("CORE", s_beamPixel, OverFoesOrder + 1, Color.Lerp(colour, Color.white, 0.75f));
        _mouthGlow = BeamPart("MOUTH", FoeFx.GlowSprite, OverFoesOrder + 2, new Color(colour.r, colour.g, colour.b, 0.8f));
        _hitGlow = BeamPart("STRIKE", FoeFx.GlowSprite, OverFoesOrder + 2, new Color(colour.r, colour.g, colour.b, 0.85f));
        for (int i = 0; i < MouthPixels; i++) _mouthPixels[i] = BeamPart("MOUTH PIXEL", s_mouthPixel, OverFoesOrder + 3 + (i == 0 ? 1 : 0), colour);
    }

    SpriteRenderer BeamPart(string name, Sprite sprite, int order, Color colour)
    {
        var go = new GameObject(name);
        go.layer = _beam.layer;
        go.transform.SetParent(_beam.transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = colour;
        sr.sortingLayerName = ChestGem.KnightLayer;
        sr.sortingOrder = order;
        return sr;
    }

    void Beam(bool target, Vector3 aim)
    {
        if (!_beam || _beamLevel != Level) MakeBeam();
        _beam.SetActive(true);
        if (!_laserOn)
        {
            _laserOn = true;
            if (_laser == null) _laser = new TimeCrystal.TcLaserVoice(transform, TimeCrystal.TcSfx.Clip("laser_beam"));
            _laser.Start();
            Meow();   // (a loud MEOW with every beam, over the laser's own sound)
        }
        _laser.Update(true);
        var mouth = Mouth();
        if (target)
        {
            // Swinging onto it, not snapping.
            var to = aim - mouth;
            _beamAngle = Mathf.MoveTowardsAngle(_beamAngle, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg, BeamTurn * Time.deltaTime);
        }
        var way = new Vector3(Mathf.Cos(_beamAngle * Mathf.Deg2Rad), Mathf.Sin(_beamAngle * Mathf.Deg2Rad), 0f);
        _catLeft = way.x < 0f;
        _beam.transform.SetPositionAndRotation(mouth, Quaternion.Euler(0f, 0f, _beamAngle));
        // All it touches.
        float px = Pixel, nearest = float.MaxValue, strikeAt = 0f;
        foreach (var f in RoadEnemy.All.ToArray())
            if (f && f.Targetable && OnBeam(mouth, way, RoadFoe.Drawn(f.Art), ref nearest, ref strikeAt)) f.Hit(999);
        foreach (var t in SpikeTrap.Tentacles.ToArray())
            if (t && t.Targetable && OnBeam(mouth, way, RoadFoe.Drawn(t.Art), ref nearest, ref strikeAt)) t.Kill();
        if (RoadFoe.Here(_boss) && !_boss.IsDead && _boss.Body && OnBeam(mouth, way, RoadFoe.Drawn(_boss.Body), ref nearest, ref strikeAt)
            && Time.time >= _nextChip && _beamChips < BeamChips)
        {
            // A beam drains a set share of a boss, fast, however long it stays on it: three to fell any boss, five a dragon.
            _nextChip = Time.time + 0.1f;
            _beamChips++;
            var own = _boss as RoadBoss;
            int chip = own ? Mathf.CeilToInt(own.MaxHealth * (own.IsDragon ? DragonShare : BossShare) / BeamChips) : spikeDamage;
            _boss.Chip(chip, Mathf.Sign(way.x) * knockback * px);
        }

        // Two hard lines, the length of it (to the middle of what it strikes), over its bloom.
        bool hitting = nearest < float.MaxValue;
        float length = hitting ? strikeAt : BeamLength, glowUnit = FoeFx.GlowSprite.bounds.size.x, flicker = 1f + 0.15f * Mathf.Sin(Time.time * 40f);
        var colour = BeamColor;
        _beamLine.transform.localScale = new Vector3(length, 3f * px, 1f);
        _beamCore.transform.localScale = new Vector3(length, 1f * px, 1f);
        _beamGlow.transform.localPosition = Vector3.right * length * 0.5f;
        _beamGlow.transform.localScale = new Vector3(length / glowUnit * 1.3f, 18f * px / glowUnit * flicker, 1f);
        _mouthGlow.transform.localScale = Vector3.one * (18f * px / glowUnit * flicker);
        _hitGlow.enabled = hitting;
        if (hitting)
        {
            _hitGlow.transform.localPosition = Vector3.right * length;
            _hitGlow.transform.localScale = Vector3.one * (26f * px / glowUnit * flicker);
        }
        // A block of hard pixels over the start of the beam, flickering in steps.
        _mouthPixels[0].transform.localScale = new Vector3(6f * px, 6f * px, 1f);
        _mouthPixels[0].color = Color.Lerp(colour, Color.white, 0.8f);
        if (Time.time >= _nextMouthStep)
        {
            _nextMouthStep = Time.time + 0.05f;
            for (int i = 1; i < MouthPixels; i++)
            {
                var spot = Random.insideUnitCircle * 7f;
                _mouthPixels[i].transform.localPosition = new Vector3(Mathf.Round(spot.x) * px, Mathf.Round(spot.y) * px, 0f);
                float side = Random.Range(2, 5) * px;
                _mouthPixels[i].transform.localScale = new Vector3(side, side, 1f);
                _mouthPixels[i].color = i % 3 == 0 ? Color.white : colour;
            }
        }
        // Pixel sparks at its mouth, and flung off where it strikes.
        if (Time.time >= _nextSpark)
        {
            _nextSpark = Time.time + 0.05f;
            FoeFx.Sparkle(mouth + (Vector3)Random.insideUnitCircle * 3f * px, Random.value < 0.6f ? colour : Color.white, 0.6f);
            if (hitting)
            {
                var strike = mouth + way * length;
                FoeFx.Impact(strike + Vector3.back * 0.02f, colour, new Vector2(-way.x, 0.4f), 0.5f, 3);
                FoeFx.Sparkle(strike + (Vector3)Random.insideUnitCircle * 5f * px, Random.value < 0.6f ? colour : Color.white, 0.8f);
            }
        }
    }

    // The beam crosses these bounds (flat on the screen), and it's ahead of the mouth.
    static bool OnBeam(Vector3 from, Vector3 way, Bounds b, ref float nearest, ref float strikeAt)
    {
        var middle = b.center;
        var size = b.size;
        size.z = 100f;
        b.size = size;
        if (!b.IntersectRay(new Ray(new Vector3(from.x, from.y, b.center.z), new Vector3(way.x, way.y, 0f)), out float distance) || distance > BeamLength) return false;
        if (distance < nearest)
        {
            nearest = distance;
            // (on along it as far as the thing's middle)
            strikeAt = Mathf.Max(distance, Vector2.Dot((Vector2)(middle - from), (Vector2)way));
        }
        return true;
    }

    void StopBeam()
    {
        if (_beam) _beam.SetActive(false);
        if (_laserOn && _laser != null) { _laserOn = false; _laser.Update(false); _laser.Stop(); }
    }

    void Start()
    {
        _player = GetComponentInParent<PlayerScriptARIANAClips>();
        var body = _player ? _player.transform.Find("ARIANA") : null;
        _playerBody = body ? body.GetComponent<SpriteRenderer>() : null;
        // ARcade: her shadow on the road below her, fainter the higher she flies.
        var cast = SpriteShadow.Cast(_renderer);
        if (cast) cast.fadeHeight = 2f;
    }

    // Blinking out: on half the time, then less and less over the last second.
    void Blink(float left)
    {
        _renderer.enabled = left >= blinkTime || Mathf.Repeat(Time.time * 10f, 1f) < 0.5f * Mathf.Clamp01(left);
    }

    // Where the road has moved her since last frame (while she's over it).
    void RideRoad(ref Vector3 p)
    {
        if (!_player) return;
        float now = _player.AccumulatedFrames;
        p.x -= (now - _roadFrames) * EndlessKnightDirector.RoadUnitsPerFrame;
        _roadFrames = now;
    }

    bool Reached()
    {
        if (!_playerBody) return false;
        var b = _playerBody.bounds;
        var p = _renderer.bounds.center;
        return Mathf.Abs(p.x - b.center.x) < b.extents.x * 0.7f + 0.08f && p.y < b.max.y + 0.3f;
    }

    /// From a fairy chest, opened at `from`: she rises out of it in the next colour.
    public void Arrive(Vector3 from)
    {
        if (looks != null && looks.Length > 0) s_look = s_visits++ % looks.Length;
        LoadCat();
        _state = State.Rising;
        _since = Time.time;
        _riseFrom = from;
        _lift = 0f;
        _renderer.enabled = true;
        _flightPosition = from;
        transform.position = from;
        if (_player) _roadFrames = _player.AccumulatedFrames;
        PitAudio.PlayClip(arriveSound, 0.8f);
    }

    void Leave()
    {
        StopBeam();
        _catFiring = false;
        _state = State.Away;
        _renderer.enabled = false;
        transform.localPosition = _home;
        _shotAt.Clear();
    }

    public Bounds Bounds => _renderer ? _renderer.bounds : new Bounds(transform.position, Vector3.zero);

    /// The next colour, in order.
    public void NextLook()
    {
        if (looks == null || looks.Length == 0) return;
        s_look = (s_look + 1) % looks.Length;
        PitAudio.PlayClip(tapSound, 0.8f);
    }

    Look CurrentLook => looks != null && looks.Length > 0 ? looks[s_look % looks.Length] : null;

    // One of her pixels, in the world.
    float Pixel => _renderer && _renderer.sprite ? Mathf.Abs(transform.lossyScale.y) / _renderer.sprite.pixelsPerUnit : 0.01f;

    void Update()
    {
        if (_laser != null && !_laserOn) _laser.Update(false);   // (its shut-off fading out)
        if (!_here) return;
        if (!_player || _player.IsDead) { Leave(); return; }

        float age = Time.time - _since;
        bool shooting = Time.time < _shootingUntil;
        var look = CurrentLook;
        if (Cat) { if (!_catFiring) _renderer.sprite = CatFrames(_state == State.Helping ? _catIdle : _catMove); }
        else if (look != null && look.frames != null && look.frames.Length > 0)
            _renderer.sprite = look.frames[(int)(Time.time * (shooting ? shootingFps : fps)) % look.frames.Length];

        switch (_state)
        {
            case State.Rising:   // up out of the chest
            {
                RideRoad(ref _riseFrom);
                float away = _playerBody && _playerBody.bounds.center.x > _riseFrom.x + 0.05f ? -1f : 1f;
                var top = _riseFrom + Vector3.up * (risePixels * Pixel) + Vector3.right * (away * driftPixels * Pixel);
                float k = Mathf.Clamp01(age / riseTime);
                float up = 1f - (1f - k) * (1f - k);
                _flightPosition = new Vector3(Mathf.Lerp(_riseFrom.x, top.x, k), Mathf.Lerp(_riseFrom.y, top.y, up), _riseFrom.z);
                if (k >= 1f) { _state = State.Waiting; _since = Time.time; _waitAt = top; }
                return;
            }
            case State.Waiting:  // bobbing over it, to be collected
            {
                RideRoad(ref _waitAt);
                _flightPosition = _waitAt + Vector3.up * (Mathf.Sin(age * 3f) * 3f * Pixel);
                Blink(waitTime - age);
                if (age > waitTime) { Leave(); return; }
                if (!Reached()) return;
                _renderer.enabled = true;
                if (Cat) Meow();
                else PitAudio.PlayClip(collectSound, 0.9f);
                _state = State.Flying;
                _since = Time.time;
                _flightFrom = _flightPosition;
                return;
            }
            case State.Flying:   // over to the player's shoulder
            {
                float k = Mathf.Clamp01(age / flyTime);
                k = k * k * (3f - 2f * k);
                _flightPosition = Vector3.Lerp(_flightFrom, HomeWorld(), k) + Vector3.up * (Mathf.Sin(Mathf.PI * k) * 10f * Pixel);
                if (age >= flyTime) { _state = State.Helping; _since = Time.time; _nextFire = Time.time; }
                return;
            }
        }

        // Helping: lifetime seconds from being collected, blinking out at the end.
        float left = lifetime * (Cat ? 2f : 1f) - flyTime - (Time.time - _since);   // (the cats stay twice as long)
        if (left <= 0f) { Leave(); return; }
        Blink(left);

        // Up a little while shooting.
        _lift = Mathf.MoveTowards(_lift, shooting ? shootingLiftPixels : 0f, 60f * Time.deltaTime);

        if (Cat) { CatAttack(); return; }
        if (Time.time < _nextFire) return;
        if (Time.time >= _nextLook)
        {
            _nextLook = Time.time + 0.25f;
            _boss = null;
            foreach (var b in RoadFoe.Bosses()) if (!b.IsDead) { _boss = b; break; }
            _bombs.Clear();
            _bombs.AddRange(FindObjectsByType<BombGuy>(FindObjectsSortMode.None));
            _shotAt.RemoveWhere(b => !b);
            _foesShotAt.RemoveWhere(f => !f);
            _trapsShotAt.RemoveWhere(t => !t);
        }

        // The nearest thing on screen to shoot: the boss, a bomb guy or a level's small foe
        // (one spike each is enough).
        var from = _renderer.bounds.center;
        float best = float.MaxValue;
        IRoadBoss boss = null;
        BombGuy bomb = null;
        if (RoadFoe.Here(_boss) && !_boss.IsDead && _boss.Body && OnScreen(_boss.Body.bounds))
        {
            best = Mathf.Abs(_boss.Body.bounds.center.x - from.x);
            boss = _boss;
        }
        foreach (var b in _bombs)
        {
            if (!b || _shotAt.Contains(b) || !b.TryGetComponent<SpriteRenderer>(out var sr) || !OnScreen(sr.bounds)) continue;
            float d = Mathf.Abs(sr.bounds.center.x - from.x);
            if (d < best) { best = d; bomb = b; boss = null; }
        }
        RoadEnemy foe = null;
        foreach (var f in RoadEnemy.All)
        {
            if (!f || !f.Targetable || _foesShotAt.Contains(f) || !OnScreen(f.Art.bounds)) continue;
            float d = Mathf.Abs(f.Art.bounds.center.x - from.x);
            if (d < best) { best = d; foe = f; bomb = null; boss = null; }
        }
        SpikeTrap trap = null;
        foreach (var t in SpikeTrap.Tentacles)
        {
            if (!t || !t.Targetable || _trapsShotAt.Contains(t) || !OnScreen(t.Art.bounds)) continue;
            float d = Mathf.Abs(t.Art.bounds.center.x - from.x);
            if (d < best) { best = d; trap = t; foe = null; bomb = null; boss = null; }
        }
        if (foe) _foesShotAt.Add(foe);
        if (trap) _trapsShotAt.Add(trap);
        if (boss == null && !bomb && !foe && !trap) return;
        if (bomb) _shotAt.Add(bomb);
        _nextFire = Time.time + fireInterval;
        _shootingUntil = Time.time + 0.4f;
        Shoot(look, boss, bomb, foe, trap);
    }

    // Her spot by the player as if they faced right, whichever way they face.
    Vector3 HomeWorld()
    {
        var parent = transform.parent;
        if (!parent) return _home;
        var scale = parent.lossyScale;
        return parent.position + parent.rotation * Vector3.Scale(_home, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
    }

    void LateUpdate()
    {
        if (!_here) return;
        // Unmirrored: her own x scale cancels the player's flip.
        var parent = transform.parent;
        if (parent)
        {
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * Mathf.Sign(parent.lossyScale.x) * (Cat && _catLeft ? -1f : 1f);   // (a cat faces its way by its scale: flipX never showed on the phone)
            transform.localScale = s;
        }
        transform.position = _inFlight ? _flightPosition : HomeWorld() + Vector3.up * ((_lift + CatBob()) * Pixel);
    }

    static bool OnScreen(Bounds b)
    {
        var cam = SpatialEmulator.Demo.PitDemoGame.ActorsCamera;
        if (!cam) cam = Camera.main;
        if (!cam) return true;
        float halfWidth = cam.orthographic ? cam.orthographicSize * cam.aspect : float.MaxValue;
        return b.max.x > cam.transform.position.x - halfWidth && b.min.x < cam.transform.position.x + halfWidth;
    }

    void Shoot(Look look, IRoadBoss boss, BombGuy bomb, RoadEnemy foe = null, SpikeTrap trap = null)
    {
        if (look == null || look.spikeFrames == null || look.spikeFrames.Length == 0) return;
        var go = new GameObject("ICE SPIKE");
        go.layer = gameObject.layer;
        go.transform.position = _renderer.bounds.center + Vector3.back * 0.01f
            + Vector3.up * (Random.Range(-spreadPixels, spreadPixels) * Pixel);
        go.transform.localScale = Vector3.one * (Pixel * look.spikeFrames[0].pixelsPerUnit);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = ChestGem.KnightLayer;
        sr.sortingOrder = OverFoesOrder;   // (over the foes, the bosses and the knight's blows)
        sr.sprite = look.spikeFrames[0];
        var spike = go.AddComponent<IceSpike>();
        spike.frames = look.spikeFrames;
        spike.fps = spikeFps;
        spike.speed = spikeSpeed * Pixel;
        spike.damage = spikeDamage;
        spike.push = knockback * Pixel;
        spike.boss = boss;
        spike.bomb = bomb;
        spike.foe = foe;
        spike.trap = trap;
        spike.fairy = this;
        spike.look = look;
        spike.jitter = Random.Range(-spreadDegrees, spreadDegrees);
        PlayOn(_shotVoices, ref _nextShotVoice, look.shotSound, shotVolume);
    }

    /// A spike broke on its target at `at` (IceSpike): the burst, and on
    /// the boss the ice punch (a bomb has its own bang).
    public void Hit(Look look, Vector3 at, bool sound, SpriteRenderer spike = null)
    {
        if (sound && hitSounds != null && hitSounds.Length > 0)
            PlayOn(_hitVoices, ref _nextHitVoice, hitSounds[Random.Range(0, hitSounds.Length)], hitVolume);
        // Icy sparks where it breaks, flung on the way it was flying.
        FoeFx.Impact(at + Vector3.back * 0.02f, impactColor, new Vector2(at.x >= transform.position.x ? 1f : -1f, 0.3f), 0.8f, 6, over: spike);   // (over the foe, with the spike)
        if (look == null || look.impactFrames == null || look.impactFrames.Length == 0) return;
        var go = new GameObject("ICE BURST");
        go.layer = gameObject.layer;
        go.transform.position = at + Vector3.back * 0.02f;
        go.transform.localScale = Vector3.one * (Pixel * look.impactFrames[0].pixelsPerUnit);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f));
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = ChestGem.KnightLayer;
        sr.sortingOrder = OverFoesOrder + 1;
        sr.flipX = Random.value < 0.5f;
        var fx = go.AddComponent<PitSpriteFx>();
        fx.frames = look.impactFrames;
        fx.fps = impactFps;
        fx.lockToRoad = true;   // stays where it hit, on the road's scroll
        sr.sprite = look.impactFrames[0];
    }

    AudioSource[] Voices(int count)
    {
        var voices = new AudioSource[count];
        for (int i = 0; i < count; i++)
        {
            // (each on an object of its own: the SFX volume's filter reaches one source an object)
            var go = new GameObject("FAIRY VOICE");
            go.transform.SetParent(transform, false);
            voices[i] = go.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
            voices[i].spatialBlend = 0f;
            SpatialEmulator.GameAudio.Fit(voices[i]);
        }
        return voices;
    }

    static void PlayOn(AudioSource[] voices, ref int next, AudioClip clip, float volume)
    {
        if (!clip || voices == null) return;
        var voice = voices[next];
        next = (next + 1) % voices.Length;
        voice.Stop();
        voice.clip = clip;
        voice.volume = volume;
        voice.pitch = Random.Range(0.94f, 1.06f);
        voice.Play();
    }
}

// One of the fairy's ice spikes: it flies into the middle of its target
// (the Slime Boss or a bomb guy), turning to follow it, and breaks there.
public class IceSpike : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 15f;
    public float speed = 2f;
    public int damage = 1;
    public float push;
    public IRoadBoss boss;
    public BombGuy bomb;
    public RoadEnemy foe;
    public SpikeTrap trap;
    [Tooltip("Degrees its aim is off by (the fairy's scatter).")]
    public float jitter;
    public FairyAlly fairy;
    public FairyAlly.Look look;

    void OnDestroy() { if (fairy && foe) fairy.SpikeDone(foe); if (fairy && trap) fairy.SpikeDone(trap); }

    SpriteRenderer _renderer;
    Vector3 _direction = Vector3.right;
    float _start;

    void Start()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _start = Time.time;
        Steer();
    }

    bool TargetAlive => trap ? trap.Targetable : foe ? foe.Targetable : bomb ? true : RoadFoe.Here(boss) && !boss.IsDead && boss.Body && boss.Body.sprite;

    // The middle of what's drawn of it.
    Vector3 Middle()
    {
        if (trap) return RoadFoe.Drawn(trap.Art).center;
        if (foe) return RoadFoe.Drawn(foe.Art).center;
        if (bomb) return bomb.TryGetComponent<SpriteRenderer>(out var sr) ? sr.bounds.center : bomb.transform.position;
        if (boss is EnemyStateMachineSlimeBoss) return EnemyStateMachineSlimeBoss.DrawnBounds(boss.Body).center;
        // (where on it they break: its hitAt, if it has one - the gazer's body, off its eye)
        return boss is RoadBoss own && own.HitPoint(out var at) ? at : RoadFoe.Drawn(boss.Body).center;
    }

    void Steer()
    {
        if (!TargetAlive) return;
        var to = Middle() - transform.position;
        to.z = 0f;
        if (to.sqrMagnitude > 1e-6f) _direction = Quaternion.Euler(0f, 0f, jitter) * to.normalized;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg);
    }

    void Update()
    {
        if (Time.time - _start > 3f) { Destroy(gameObject); return; }
        if (frames != null && frames.Length > 0)
            _renderer.sprite = frames[(int)((Time.time - _start) * fps) % frames.Length];
        Steer();
        transform.position += _direction * speed * Time.deltaTime;

        if (!TargetAlive) return;
        // Reaching the middle, or about to pass it.
        var middle = Middle();
        var to = middle - transform.position;
        to.z = 0f;
        float step = speed * Time.deltaTime;
        if (to.sqrMagnitude > step * step && Vector3.Dot(to, _direction) > 0f) return;
        var at = new Vector3(middle.x, middle.y, transform.position.z);
        if (trap) trap.Kill();
        else if (foe) foe.Kill();
        else if (bomb) bomb.Detonate();
        else boss.Chip(damage, Mathf.Sign(_direction.x) * push);
        if (fairy) fairy.Hit(look, at, sound: !bomb, GetComponent<SpriteRenderer>());
        Destroy(gameObject);
    }
}
