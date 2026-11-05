using System.Collections.Generic;
using UnityEngine;
using PitDemoGame = SpatialEmulator.Demo.PitDemoGame;

// ARcade: a level's boss (LevelFoes.Boss) in place of the Slime Boss - the
// giant toad (level 3), the elder dragon (level 4), the necromancer (level 2).
// Each one after the first in a run is the next of its colours, bigger and
// with more health (EndlessKnightDirector, as the Slime Boss's). It walks in
// from the right, on the road (it keeps itself on it as it scrolls), and:
//   comes up to her and attacks when she's in reach - its windup frames
//   first if it has any (the dragon's roar: time to get away), its blow live
//   on its hit frames, in front of it and below hitHeight (a jump clears it);
//   or keeps back from her (keepAway) and casts: summoning its foes when
//   fewer than maxSummons are about (the necromancer's zombies), or a
//   fireball that flies at her along the road (jump it).
// A magic shield (shieldHits) takes the blows first; when it breaks, its
// health does. Dying, it plays its death frames, bursts (blood, bones) and
// leaves the Slime Boss's treasure chest.
public class RoadBoss : MonoBehaviour, IRoadBoss
{
    public LevelFoes.Boss boss;
    public LevelFoes.BossLook look;
    public GameObject chestPrefab;
    public AudioClip[] hurtSounds, deathSounds;
    public float size = 1f;
    /// Which of its kind this is (1: the first): each its own magic colours (the necromancer's shield and death blasts).
    public int nth = 1;

    // Its magic's colour turned for the nth of it.
    Color Turned(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        var turned = Color.HSVToRGB(Mathf.Repeat(h + 0.17f * (nth - 1), 1f), s, v);
        turned.a = c.a;
        return turned;
    }
    Color ShieldColor => Turned(boss.shieldColor);
    Color DeathBlood => look != null && look.blood.a > 0f ? look.blood : boss.deathBlood;
    Color HitBlood => look != null && look.blood.a > 0f && boss.hitBlood.a > 0f ? look.blood : boss.hitBlood;
    float BlastHue => boss.deathBlastHue + 61f * (nth - 1);

    public static readonly List<RoadBoss> All = new List<RoadBoss>();

    SpriteRenderer _sr, _shield;
    int _health, _maxHealth, _shieldLeft;
    float _lastRoad, _deadAt = -1f, _actStart = -1f, _nextAttack, _nextSummon, _nextCast, _flashUntil, _hurtUntil, _chestAt = -1f, _noFlinchUntil;
    int _chips;
    enum Act { None, Windup, Attack, Summon, Cast, Dodge, Berserk, Breath, Leap, Rise }
    Act _act;
    bool _dealt, _facingRight, _berserk;
    float _speed = 1f, _baseScale, _dodgeFromX, _dodgeToX;
    float _hopStart = -1f, _hopDir, _hopDone, _leapFromX, _leapToX, _leapSide;
    int _leapsLanded, _leapAt = -1;
    bool _faced;
    Sprite[] _attackFrames;
    int _hitFrom, _hitTo;
    SpriteRenderer _aura, _breath;
    bool _breathNext;
    Vector3 _shook;
    readonly List<RoadEnemy> _summoned = new List<RoadEnemy>();

    public SpriteRenderer Body => _sr;
    public Bounds BodyBounds
    {
        get
        {
            // Its middle (not its tail, wings or tongue): standing against it. Measured once, on its
            // standing frame (StandingBody) - not on whatever frame it's in, whose tail and wings swing
            // in and out: her wall and its reach stay put as it walks, nothing jostles.
            if (!_measured && _sr && look != null) StandingBody();
            if (!_measured) { var d = RoadFoe.Drawn(_sr); d.size = new Vector3(d.size.x * 0.55f, d.size.y, d.size.z); return d; }
            float mirror = _sr.flipX ? -1f : 1f;
            var a = transform.TransformPoint(new Vector3(_standMin.x * mirror, _standMin.y, 0f));
            var c = transform.TransformPoint(new Vector3(_standMax.x * mirror, _standMax.y, 0f));
            var b = new Bounds();
            b.SetMinMax(Vector3.Min(a, c), Vector3.Max(a, c));
            b.size = new Vector3(b.size.x * 0.55f, b.size.y, b.size.z);
            return b;
        }
    }
    bool _measured;
    Vector2 _standMin, _standMax;

    // Its standing frame's outline (its idle's first frame), in its own units.
    void StandingBody()
    {
        var frame = look.idle != null && look.idle.Length > 0 ? look.idle[0] : _sr.sprite;
        if (!frame) return;
        Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
        foreach (var v in frame.vertices) { min = Vector2.Min(min, v); max = Vector2.Max(max, v); }
        _standMin = min; _standMax = max;
        _measured = true;
    }
    public Material BodyRecolor => null;
    public int MaxHealth => _maxHealth;
    public int Health => Mathf.Max(0, _health);
    public bool IsDead => _deadAt >= 0f;
    // Its rocks' colours in this look (ROCKY's own stone; else the boss's) - and, read once, its gems for the next Rocks call.
    Color RockLight { get { Gems(); return look != null && look.rockLight.a > 0f ? look.rockLight : boss.smashLight; } }
    Color RockDark => look != null && look.rockDark.a > 0f ? look.rockDark : boss.smashDark;
    void Gems()
    {
        if (look != null && look.gemLight.a > 0f)
            FoeFx.NextGems = new FoeFx.RockLook { gem = look.gemLight, gemDark = look.gemDark.a > 0f ? look.gemDark : new Color(look.gemLight.r * 0.6f, look.gemLight.g * 0.6f, look.gemLight.b * 0.6f), kind = look.gemKind };
    }
    bool _cleared;   // (killed off as the final dragon falls: no points for it)

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public void Setup(int health)
    {
        _health = _maxHealth = health;
        _shieldLeft = boss.shieldHits;
    }

    void Awake()
    {
        _sr = gameObject.GetComponent<SpriteRenderer>();
        if (!_sr) _sr = gameObject.AddComponent<SpriteRenderer>();
    }

    public const int OverKnightOrder = 205;

    void Start()
    {
        // Over the knight - her layer, past her 150 and glow (under her attacks, 219) - as its attacks land on her (its breath,
        // aura and shield go by it; its shots, beam and orb over her too); under the road's own foes (210).
        _sr.sortingLayerName = ChestGem.KnightLayer;
        _sr.sortingOrder = OverKnightOrder;
        Show(look.walk != null && look.walk.Length > 0 ? look.walk : look.idle, 0f);
        _hoverFrom = Time.time;
        transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY + Hover, SpikeTrap.LaneZ - 0.004f);
        transform.localScale = Vector3.one * (RoadFoe.Pixel / 0.01f * boss.scale * size);
        _baseScale = transform.localScale.x;
        _lastRoad = Road;
        _nextAttack = Time.time + 1.5f;
        _nextSummon = Time.time + 1.5f;
        _nextCast = Time.time + 3f;
        if (boss.entryLeapHeight > 0f) { _leapFromX = transform.position.x; Begin(Act.Leap); }
        var shadow = SpriteShadow.Cast(_sr);
        if (shadow) { shadow.raisePixels = boss.shadowRaise; shadow.fromRoadLine = true; }   // (from the road, not from swooshes hanging below its feet)
        if (RoadEnemy.HueMaterial && (look.hue != 0f || look.saturation != 1f || look.brightness != 1f))
        {
            // This look's colours, turned from the one art.
            var colours = new Material(RoadEnemy.HueMaterial) { name = "Boss Colours" };
            colours.SetFloat("_Hue", Mathf.Repeat(look.hue / 360f, 1f));
            colours.SetFloat("_Saturation", look.saturation);
            colours.SetFloat("_Brightness", look.brightness);
            colours.SetFloat("_MaskHue", Mathf.Repeat(look.maskHue / 360f, 1f));
            colours.SetFloat("_MaskWidth", look.maskWidth / 360f);
            _sr.sharedMaterial = colours;
        }
        SpriteOutline.Add(_sr, boss.outline, 0.3f);
        var auraColor = look.aura.a > 0f ? look.aura : boss.aura;
        var sparkColor = look.sparks.a > 0f ? look.sparks : boss.sparkColor;
        if (auraColor.a > 0f) { var aura = SpriteOutline.Add(_sr, Color.clear, 0.8f, auraColor); if (aura) { aura.full = true; aura.reach = boss.auraReach; } }
        if (sparkColor.a > 0f) { var sparks = GateMotes.Add(_sr, GateMotes.Kind.Sparks, sparkColor); sparks.fromBody = true; sparks.count = 22; }
        if (_shieldLeft > 0) MakeShield();
        if (boss.riseEntry) BeginRise();
        else RoadFoe.Offscreen(transform, RoadFoe.Drawn(_sr));   // (all of it past the picture's edge as it comes)
    }

    // Rocky's entry: just ahead of her (to her right, room for its reach), sunk under the road with a
    // mask above the road's line so none of it shows below it, then grinding up (Act.Rise).
    void BeginRise()
    {
        // (always ahead of her, to her right - the way on - whichever way she faces: she can't walk on past it
        // till it's beaten)
        float x = PlayerX + (boss.reach + BodyBounds.extents.x + 0.15f);
        transform.position = new Vector3(x, SpikeTrap.PlayerFeetY - RoadFoe.Drawn(_sr).size.y, transform.position.z);
        _riseX = x; _riseRoad = Road;
        if (!s_square) s_square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 4f);
        var go = new GameObject("BOSS GROUND LINE");
        go.layer = gameObject.layer;
        var mask = go.AddComponent<SpriteMask>();
        mask.sprite = s_square;
        mask.isCustomRangeActive = true;
        mask.frontSortingLayerID = mask.backSortingLayerID = _sr.sortingLayerID;
        mask.frontSortingOrder = _sr.sortingOrder;
        mask.backSortingOrder = _sr.sortingOrder - 1;
        go.transform.localScale = new Vector3(8f, 8f, 1f);
        go.transform.position = new Vector3(x, SpikeTrap.PlayerFeetY, transform.position.z);
        _riseMask = go.transform;
        _sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        _facingRight = PlayerX > x;
        _sr.flipX = boss.artFacesRight ? !_facingRight : _facingRight;
        if (boss.riseSound) PitAudio.PlayClip(boss.riseSound, 1f);
        Begin(Act.Rise);
    }
    Transform _riseMask;
    float _riseX, _riseRoad, _nextRiseFx;
    static Sprite s_square;

    void OnDestroy()
    {
        if (_riseMask) Destroy(_riseMask.gameObject);
        foreach (var v in _deathVoices) if (v) Destroy(v.gameObject);   // (its cries end as it's gone)
    }

    // This look's own ways, or the boss's (the final dragon: purple fire, more claws, zombies).
    float AttackCooldown => look.attackCooldown > 0f ? look.attackCooldown : boss.attackCooldown;
    float BreathShare => look.breathShare >= 0f ? look.breathShare : boss.breathShare;
    Color LavaColor => look.lavaColor.a > 0f ? look.lavaColor : boss.lavaColor;
    LevelFoes.Foe SummonFoe => look.maxSummons > 0 ? look.summonFoe : boss.summon;
    int MaxSummons => look.maxSummons > 0 ? look.maxSummons : boss.maxSummons;
    float SummonCooldown => look.maxSummons > 0 ? look.summonCooldown : boss.summonCooldown;

    // How high it flies over the road (0: it walks on it): its floatHeight, drifting up floatRise and back down
    // (out of her reach up there, till she jumps), bobbing a little all the while.
    float Hover
    {
        get
        {
            if (boss.floatHeight <= 0f) return 0f;
            float t = Time.time - _hoverFrom;
            float drift = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f / Mathf.Max(1f, boss.floatPeriod));
            return boss.floatHeight + boss.floatRise * drift + RoadFoe.Pixel * 2f * Mathf.Sin(t * 3.1f);
        }
    }
    float _hoverFrom;

    static float Road => EndlessKnightDirector.Player ? EndlessKnightDirector.Player.AccumulatedFrames * EndlessKnightDirector.RoadUnitsPerFrame : 0f;
    static float PlayerX => EndlessKnightDirector.Player ? EndlessKnightDirector.Player.transform.position.x : 0f;

    void Show(Sprite[] frames, float t, bool loop = true)
    {
        if (frames == null || frames.Length == 0) return;
        int i = (int)(t * boss.fps * _speed);
        _sr.sprite = frames[loop ? i % frames.Length : Mathf.Min(i, frames.Length - 1)];
    }

    int Frame => (int)((Time.time - _actStart) * boss.fps * _speed);

    // A hit: its shape flashing white for the moment of _flashUntil, as the road's foes do - the blow that fells it too.
    const float HeavyFlash = 0.22f;   // (seconds: a Holy Slash's, to show through the slash drawn over it)
    SpriteRenderer _whiteFlash;
    MaterialPropertyBlock _whiteBlock;
    static Material s_whiteFlash;
    static readonly int WhiteColorId = Shader.PropertyToID("_Color");

    void LateUpdate()
    {
        if (boss == null || !_sr) return;
        bool on = Time.time < _flashUntil;
        if (!_whiteFlash)
        {
            if (!on) return;
            if (!s_whiteFlash)
            {
                var shader = Shader.Find("SpatialEmulator/SpriteSolid");
                if (!shader) return;
                s_whiteFlash = new Material(shader) { name = "Boss Flash" };
            }
            var go = new GameObject("White Flash");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            _whiteFlash = go.AddComponent<SpriteRenderer>();
            _whiteFlash.sharedMaterial = s_whiteFlash;
            _whiteBlock = new MaterialPropertyBlock();
            _whiteBlock.SetColor(WhiteColorId, new Color(1f, 1f, 1f, 0.9f));
            _whiteFlash.SetPropertyBlock(_whiteBlock);
        }
        _whiteFlash.sprite = _sr.sprite;
        _whiteFlash.flipX = _sr.flipX;
        _whiteFlash.sortingLayerID = _sr.sortingLayerID;
        _whiteFlash.sortingOrder = _sr.sortingOrder + 1;
        _whiteFlash.maskInteraction = _sr.maskInteraction;
        _whiteFlash.enabled = on && _sr.enabled;
    }

    void Update()
    {
        if (!_sr || !EndlessKnightDirector.Player) return;
        float road = Road;
        transform.position += Vector3.left * (road - _lastRoad);   // (on the road)
        _leapFromX -= road - _lastRoad;
        _leapToX -= road - _lastRoad;
        _lastRoad = road;
        _summoned.RemoveAll(e => !e);

        var tint = Color.white;
        if (_act == Act.Berserk) tint = ((int)(Time.time * 14f)) % 2 == 0 ? new Color(1f, 0.25f, 0.2f, 1f) : new Color(1f, 0.6f, 0.55f, 1f);   // (flushing red)
        if (_aura) Aura();
        if (_sr.color != tint && !IsDead) _sr.color = tint;
        if (_shield) ShieldLook();

        if (boss.floatHeight > 0f)
        {
            // (flying: at its height - dead, sinking down onto the road)
            float y = IsDead ? Mathf.MoveTowards(transform.position.y, SpikeTrap.PlayerFeetY, 1.2f * Time.deltaTime) : SpikeTrap.PlayerFeetY + Hover;
            // A beam coming (the cacodaemon): it flies to her height first - its mouth level with her middle - and
            // holds still while it fires; then back up to its float.
            if (boss.beam && !IsDead)
            {
                bool lining = _act == Act.Cast || Time.time >= _nextCast - 1.2f;
                if (lining)
                {
                    var her = RoadFoe.PlayerBody();
                    // (any height - a flyer, it may sink into the road if that's where its mouth needs to be)
                    y = her.center.y - boss.castFrom.y * transform.lossyScale.y * 0.01f;
                }
                y = _beaming ? transform.position.y : Mathf.MoveTowards(transform.position.y, y, (lining ? 3.5f : 1.2f) * Time.deltaTime);
            }
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
        }
        if (IsDead) { Dying(); return; }
        EntryRats();
        // (killed in the air mid-leap: its leap finished, landed, then it dies)
        if (_dieWhenLanded && _act != Act.Leap && _act != Act.Dodge)
        {
            _dieWhenLanded = false;
            transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z);
            Die();
            return;
        }

        // Into her blow while it's playing: hit (once a swing).
        var player = EndlessKnightDirector.Player;
        float dx = PlayerX - BodyBounds.center.x;
        if (_act == Act.None)
        {
            if (!boss.fixedFacing || !_faced) _facingRight = dx > 0f;   // (a fixed one: once, the side she's on)
            _faced = true;
            _sr.flipX = boss.artFacesRight ? !_facingRight : _facingRight;
        }

        switch (_act)
        {
            case Act.Windup:
                Show(look.windup, Time.time - _actStart, false);
                if (Frame >= look.windup.Length) Begin(_breathNext ? Act.Breath : Act.Attack);
                return;
            case Act.Attack:
                Show(_attackFrames, Time.time - _actStart, false);
                if (!_dealt && Frame >= _hitFrom && Frame <= _hitTo && InReach()) { _dealt = true; if (!PlayerBuffs.Shield) HitFx(); RoadFoe.HurtPlayer(Harder(boss.damage), boss.knockHeight, boss.knockBack, transform.position.x); }
                if (!_smashed && _smashFrame >= 0 && Frame >= _smashFrame) Smash();
                if (Frame >= Length(_attackFrames)) { _act = Act.None; _nextAttack = Time.time + AttackCooldown / _speed * Random.Range(0.85f, 1.2f); }
                return;
            case Act.Breath:
            {
                Show(look.breath != null && look.breath.Length > 0 ? look.breath : look.attack, Time.time - _actStart, false);
                int f = Frame;
                if (!_breath) MakeBreath();
                bool on = f < boss.breath.Length;
                _breath.enabled = on;
                if (on)
                {
                    _breath.sprite = boss.breath[f];
                    _breath.flipX = false;
                    _breath.transform.localScale = new Vector3(_facingRight ? 1f : -1f, 1f, 1f);   // (mirrored about the mouth)
                    float px = transform.lossyScale.x * 0.01f;   // one of its art's pixels
                    float side = _facingRight ? 1f : -1f;
                    var mouth = new Vector3(transform.position.x + side * boss.breathMouth.x * px, transform.position.y + boss.breathMouth.y * px, transform.position.z - 0.003f);
                    _breath.transform.position = mouth;
                    if (!_dealt && f >= boss.breathFrom && f <= boss.breathTo)
                    {
                        // The fire: along the road from near the mouth out to its reach, low -
                        // jumped over, or out past its reach, she's clear.
                        float near = mouth.x + side * boss.breathNear * px, far = mouth.x + side * boss.breathReach * px;
                        var her = RoadFoe.PlayerBody();
                        bool across = her.max.x > Mathf.Min(near, far) && her.min.x < Mathf.Max(near, far);
                        if (across && her.min.y < SpikeTrap.PlayerFeetY + boss.breathHeight) { _dealt = true; RoadFoe.HurtPlayer(Harder(boss.breathDamage), 0.6f, 0.7f, transform.position.x); }
                    }
                    Lava(side);
                }
                if (f >= Mathf.Max(boss.breath.Length, Length(look.breath)))
                {
                    _breath.enabled = false;
                    _act = Act.None;
                    _nextAttack = Time.time + AttackCooldown / _speed * Random.Range(0.85f, 1.2f);
                }
                return;
            }
            case Act.Rise:
            {
                // Grinding up out of the road: nothing of it below the road's line (the mask), shaking, rocks
                // and earth thrown up round it, a rumble; then it's out.
                float k = Mathf.Clamp01((Time.time - _actStart) / Mathf.Max(0.1f, boss.riseTime));
                float height = RoadFoe.Drawn(_sr).size.y;
                float x = _riseX - (Road - _riseRoad);   // (on the road as it scrolls)
                transform.position = new Vector3(x + Mathf.Sin(Time.time * 45f) * RoadFoe.Pixel * 1.2f * (1f - k),
                    SpikeTrap.PlayerFeetY - height * (1f - k * (2f - k)), transform.position.z);
                if (_riseMask) _riseMask.position = new Vector3(x, SpikeTrap.PlayerFeetY, transform.position.z);
                Show(look.idle, Time.time);
                if (k < 0.85f && Time.time >= _nextRiseFx)
                {
                    _nextRiseFx = Time.time + 0.14f;
                    var at = new Vector3(x, SpikeTrap.PlayerFeetY, transform.position.z - 0.01f);
                    FoeFx.Rocks(at, RockLight, RockDark, 5, BodyBounds.extents.x);
                    FoeFx.Dirt(at, 4, BodyBounds.extents.x);
                    if (Mathf.Abs(PlayerX - x) < 3f) SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Light);
                }
                if (k >= 1f)
                {
                    transform.position = new Vector3(x, SpikeTrap.PlayerFeetY, transform.position.z);
                    _sr.maskInteraction = SpriteMaskInteraction.None;
                    if (_riseMask) Destroy(_riseMask.gameObject);
                    FoeFx.Rocks(transform.position, RockLight, RockDark, 14, BodyBounds.extents.x);
                    if (boss.smashSound && !boss.riseSound) PitAudio.PlayClip(boss.smashSound, 1f);
                    SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Heavy);
                    _act = Act.None;
                    _nextAttack = Mathf.Max(_nextAttack, Time.time + 0.8f);
                }
                return;
            }
            case Act.Leap:
            {
                // Its entry: great leaps from where it came in, down in front of her - its airborne hop frames on
                // each, a thud of dust as each lands. Each leap's way is set as it takes off (its share of the way
                // to in front of her then): it never follows her about in mid-air (Wayne's).
                int leaps = Mathf.Max(1, boss.entryLeaps);
                float all = Mathf.Clamp01((Time.time - _actStart) / Mathf.Max(0.1f, boss.entryLeapTime));
                int leap = Mathf.Min(leaps - 1, (int)(all * leaps));
                float k = all >= 1f ? 1f : all * leaps - leap;
                if (leap > _leapsLanded && all < 1f)
                {
                    _leapsLanded = leap;
                    FoeFx.Dirt(new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z), 10, BodyBounds.size.x);
                    EntryBrood(leaps);
                    SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Medium);
                }
                if (leap != _leapAt)
                {
                    if (_leapAt >= 0) _leapFromX = _leapToX;   // (from where the last one landed)
                    _leapAt = leap;
                    _leapSide = _leapFromX >= PlayerX ? 1f : -1f;
                    float land = PlayerX + _leapSide * (boss.reach * 0.8f + BodyBounds.extents.x);
                    _leapToX = _leapFromX + (land - _leapFromX) / (leaps - leap);
                }
                float side = _leapSide;
                float x = Mathf.Lerp(_leapFromX, _leapToX, k * k * (3f - 2f * k));
                transform.position = new Vector3(x, SpikeTrap.PlayerFeetY + Mathf.Sin(Mathf.PI * k) * boss.entryLeapHeight, transform.position.z);
                _facingRight = side < 0f;
                _sr.flipX = boss.artFacesRight ? !_facingRight : _facingRight;
                int n = Length(look.walk);
                if (n > 0) _sr.sprite = look.walk[Mathf.Clamp(k < 0.08f ? 0 : k > 0.92f ? n - 1 : boss.hopFrom + (int)(k * (boss.hopTo - boss.hopFrom)), 0, n - 1)];
                if (all >= 1f)
                {
                    transform.position = new Vector3(x, SpikeTrap.PlayerFeetY, transform.position.z);
                    FoeFx.Dirt(transform.position, 16, BodyBounds.size.x);
                    EntryBrood(1, all: true);
                    SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Heavy);
                    _act = Act.None;
                    _nextAttack = Mathf.Max(_nextAttack, Time.time + 0.5f);
                }
                return;
            }
            case Act.Dodge:
            {
                // A jump back out of her reach (nothing touches it in the air).
                float k = Mathf.Clamp01((Time.time - _actStart) / (boss.jumpBackTime / _speed));
                float x = Mathf.Lerp(_dodgeFromX, _dodgeToX, 1f - (1f - k) * (1f - k));
                transform.position = new Vector3(x, SpikeTrap.PlayerFeetY + Mathf.Sin(Mathf.PI * k) * boss.jumpBackHeight, transform.position.z);
                Show(look.idle, 0f);
                if (k >= 1f) { _act = Act.None; transform.position = new Vector3(x, SpikeTrap.PlayerFeetY, transform.position.z); }
                return;
            }
            case Act.Berserk:
            {
                // Roaring, shaking as it grows and flushes red; then the red glow, faster.
                float k = Mathf.Clamp01((Time.time - _actStart) / 1.1f);
                float grow = Mathf.Lerp(1f, boss.berserkGrowth, Mathf.SmoothStep(0f, 1f, k));
                transform.localScale = Vector3.one * _baseScale * grow;
                float shake = RoadFoe.Pixel * 2f * (1f - k * 0.5f);
                transform.position -= _shook;   // (shaking about where it stands)
                _shook = new Vector3(Random.Range(-shake, shake), 0f, 0f);
                transform.position += _shook;
                Show(look.idle, Time.time);
                if (k >= 1f) { transform.position -= _shook; _shook = Vector3.zero; _act = Act.None; _berserk = true; _speed = boss.berserkSpeed; MakeAura(); }
                return;
            }
            case Act.Summon:
                Show(look.summon != null && look.summon.Length > 0 ? look.summon : look.cast, Time.time - _actStart, false);
                int summonLength = Length(look.summon != null && look.summon.Length > 0 ? look.summon : look.cast);
                if (!_dealt && (Frame >= boss.summonFrame || summonLength == 0)) { _dealt = true; Summon(); }   // (no casting frames - the dragon: at once)
                if (Frame >= summonLength) { _act = Act.None; _nextSummon = Time.time + SummonCooldown; _nextCast = Mathf.Max(_nextCast, Time.time + boss.castAfterSummon); }
                return;
            case Act.Cast:
                Show(look.cast, Time.time - _actStart, false);
                if (!_dealt && Frame >= boss.castFrame) { _dealt = true; Fireball(); }
                if (Frame >= Length(look.cast)) { _act = Act.None; _nextCast = boss.beam ? _actStart + boss.castCooldown : Time.time + boss.castCooldown * Random.Range(0.85f, 1.2f); }   // (a beam: every castCooldown on the dot)
                return;
        }

        // Nothing on: move, then maybe start something.
        float gap = Gap();
        float move = 0f;
        if (boss.keepAway > 0f)
        {
            if (Mathf.Abs(dx) > boss.keepAway * 1.3f) move = Mathf.Sign(dx);
            else if (Mathf.Abs(dx) < boss.keepAway * 0.7f) move = -Mathf.Sign(dx);
        }
        else if (gap > boss.reach * 0.6f) move = Mathf.Sign(dx);
        if (boss.forwardOnly && move != 0f && Mathf.Sign(move) != Mathf.Sign(dx)) move = 0f;   // (never backing off)
        if (_beaming) move = 0f;   // (still while its beam's on)
        // (Don't walk off the right of the picture backing away.)
        if (boss.hops && Length(look.walk) > 0)
        {
            // In hops, each one seen through once begun: its ground frames still, then up and along in
            // a smooth arc on its airborne ones (as fast as its walk on average), down, and the next.
            int n = look.walk.Length;
            if (_hopStart < 0f && Mathf.Abs(move) > 0f) { _hopStart = Time.time; _hopDir = move; _hopDone = 0f; }
            if (_hopStart >= 0f)
            {
                float rate = boss.fps * _speed * Mathf.Max(0.1f, boss.hopPace);
                float t = (Time.time - _hopStart) * rate;   // (in frames)
                float air = Mathf.Max(1, boss.hopTo - boss.hopFrom);
                float k = Mathf.Clamp01((t - boss.hopFrom) / air);
                float along = k * k * (3f - 2f * k);
                float distance = boss.walkSpeed * _speed * n / rate;   // (a hop's worth of walking)
                transform.position += Vector3.right * _hopDir * distance * (along - _hopDone);
                _hopDone = along;
                transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY + Mathf.Sin(Mathf.PI * k) * boss.hopHeight, transform.position.z);
                _sr.sprite = look.walk[Mathf.Min((int)t, n - 1)];
                if (t >= n) { _hopStart = -1f; transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z); }
                return;   // (nothing else till it's down)
            }
            transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z);
        }
        else
        {
            transform.position += Vector3.right * move * boss.walkSpeed * _speed * Time.deltaTime;
        }
        if (Time.time < _hurtUntil && look.hurt != null && look.hurt.Length > 0) Show(look.hurt, _hurtUntil - Time.time);
        else if (Mathf.Abs(move) > 0f) Show(look.walk, Time.time);
        else Show(look.idle, Time.time);

        bool onScreen = Mathf.Abs(dx) < 3.2f;
        if (!onScreen) return;
        if (MaxSummons > 0 && _summoned.Count < MaxSummons && Time.time >= _nextSummon) { Begin(Act.Summon); return; }
        if ((Length(boss.projectile) > 0 || boss.projectileCount > 1) && Time.time >= _nextCast) { Begin(Act.Cast); return; }   // (a shotgun's shots are its own)
        if (boss.dodgeChance > 0f && !boss.forwardOnly && gap < boss.reach * 1.4f && Random.value < boss.dodgeChance * Time.deltaTime * (_berserk ? 1.5f : 1f)) { Dodge(); return; }
        float breathReach = boss.breath != null && boss.breath.Length > 0 ? (boss.breathMouth.x + boss.breathReach) * transform.lossyScale.x * 0.01f * 0.85f : 0f;
        bool close = gap <= boss.reach, inFire = breathReach > 0f && gap > boss.reach * 0.5f && gap <= breathReach;
        if (boss.keepAway <= 0f && Time.time >= _nextAttack && ((close && Length(look.attack) > 0) || inFire))
        {
            // Close: claws (or fire, now and then); a little way off: fire.
            _breathNext = inFire && (!close || Random.value < BreathShare);
            if (look.windup != null && look.windup.Length > 0)
            {
                Begin(Act.Windup);
                if (boss.windupSound) PitAudio.PlayClip(boss.windupSound, 0.9f);
                if (_breathNext && boss.breathTellSound) PitAudio.PlayClip(boss.breathTellSound, 1f);   // (fire coming: jump, or get back)
            }
            else Begin(_breathNext ? Act.Breath : Act.Attack);
        }
    }

    static int Length(Sprite[] frames) => frames != null ? frames.Length : 0;

    void Dodge()
    {
        Begin(Act.Dodge);
        float away = _facingRight ? -1f : 1f;
        _dodgeFromX = transform.position.x;
        _dodgeToX = _dodgeFromX + away * boss.jumpBackDistance;
    }

    void Begin(Act act)
    {
        if (_hopStart >= 0f) { _hopStart = -1f; if (act != Act.Dodge) transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z); }
        if (act == Act.Attack)
        {
            bool two = look.attack2 != null && look.attack2.Length > 0;
            bool second = two && (boss.alternateAttacks ? !_secondLast : Random.value < 0.5f);
            _secondLast = second;
            _smashFrame = second ? boss.smashFrame2 : boss.smashFrame;
            _smashed = false;
            _attackFrames = second ? look.attack2 : look.attack;
            _hitFrom = second ? boss.hitFrom2 : boss.hitFrom;
            _hitTo = second ? boss.hitTo2 : boss.hitTo;
        }
        _act = act;
        _actStart = Time.time;
        _dealt = false;
        if (act == Act.Attack && boss.attackSound) PitAudio.PlayClip(boss.attackSound, 0.8f);
        if ((act == Act.Attack || act == Act.Windup) && boss.attackYells != null && boss.attackYells.Length > 0)
            PitAudio.PlayClip(boss.attackYells[Random.Range(0, boss.attackYells.Length)], 0.9f);   // (a battle yell, every swing)
        if (act == Act.Breath && boss.breathSound) PitAudio.PlayClip(boss.breathSound, 0.9f);
        if (act == Act.Summon && boss.summonSound) PitAudio.PlayClip(boss.summonSound, 0.9f);
        if (act == Act.Cast && boss.castSound) PitAudio.PlayClip(boss.castSound, 0.8f);
    }

    bool _secondLast, _smashed;
    int _smashFrame = -1;

    // Its fist on the road: rocks bursting up in front of it, a heavy shake.
    void Smash()
    {
        _smashed = true;
        float px = transform.lossyScale.x * 0.01f, side = _facingRight ? 1f : -1f;
        var at = new Vector3(transform.position.x + side * boss.smashReach * px, SpikeTrap.PlayerFeetY, transform.position.z - 0.01f);
        FoeFx.Rocks(at, RockLight, RockDark, 16, 18f * px);
        if (boss.smashSound) PitAudio.PlayClip(boss.smashSound, 1f);
        if (Mathf.Abs(PlayerX - at.x) < 3f) SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Heavy);
    }

    // Between the front of its body and hers.
    float Gap()
    {
        var b = BodyBounds;
        var her = RoadFoe.PlayerBody();
        return _facingRight ? her.min.x - b.max.x : b.min.x - her.max.x;
    }

    // Its blow: reach in front of its body, below hitHeight over the road.
    bool InReach()
    {
        var her = RoadFoe.PlayerBody();
        float gap = Gap();
        bool over = her.min.y > SpikeTrap.PlayerFeetY + boss.hitHeight;   // jumped clear
        return gap <= boss.reach && !over && (_facingRight ? her.max.x > BodyBounds.min.x : her.min.x < BodyBounds.max.x);
    }

    void Summon()
    {
        var summon = SummonFoe;
        if (summon == null) return;
        int count = Mathf.Max(0, MaxSummons - _summoned.Count);
        var b = BodyBounds;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("SUMMONED " + summon.name);
            go.layer = gameObject.layer;
            // Behind her (the way she isn't facing): she has to turn round to deal with them.
            var knight = EndlessKnightDirector.Player;
            float behind = knight && knight.transform.localScale.x < 0f ? 1f : -1f;
            go.transform.position = new Vector3(PlayerX + behind * (0.7f + i * 0.45f + Random.Range(0f, 0.2f)), SpikeTrap.PlayerFeetY, transform.position.z);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "TopLayer";
            sr.sortingOrder = 6;
            var e = go.AddComponent<RoadEnemy>();
            e.foe = summon;
            e.selfAnchored = true;
            e.rise = true;   // (up out of the ground)
            _summoned.Add(e);
            FoeFx.Blood(go.transform.position + Vector3.up * 0.05f, ShieldColor, 8);   // (its magic where it comes up)
        }
    }

    void Fireball()
    {
        if (boss.beam) { StartCoroutine(Beam()); return; }
        if (boss.projectileCount > 1) { StartCoroutine(Shotgun()); return; }
        var b = RoadFoe.Drawn(_sr);
        var go = new GameObject("FIREBALL");
        go.layer = gameObject.layer;
        float side = _facingRight ? 1f : -1f;
        go.transform.position = new Vector3(_facingRight ? b.max.x : b.min.x, SpikeTrap.PlayerFeetY + boss.projectileHeight, transform.position.z - 0.01f);
        go.transform.localScale = Vector3.one * (RoadFoe.Pixel / 0.01f * boss.projectileScale);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = ChestGem.KnightLayer;
        sr.sortingOrder = OverKnightOrder + 9;   // (its shot, over her)
        var shot = go.AddComponent<BossShot>();
        shot.frames = boss.projectile;
        shot.velocity = side * boss.projectileSpeed;
        shot.damage = Harder(boss.projectileDamage);
    }

    // A shotgun burst of magic from its casting hand: a flash there, then projectileCount shots fanned
    // out at her as she is now (dodge them, or jump over where they come down), in its colours, its size -
    // all at once, or in volleys of volleySize, one shot after the other, each volley aimed at her anew.
    System.Collections.IEnumerator Shotgun()
    {
        var color = Turned(boss.shieldColor);
        int n = Mathf.Max(2, boss.projectileCount);
        int volley = boss.volleySize > 0 ? Mathf.Min(boss.volleySize, n) : n;
        for (int first = 0; first < n; first += volley)
        {
            if (first > 0) yield return new WaitForSeconds(boss.volleyGap);
            if (IsDead || !EndlessKnightDirector.Player) yield break;
            float px = transform.lossyScale.x * 0.01f, side = _facingRight ? 1f : -1f;
            var from = new Vector3(transform.position.x + side * boss.castFrom.x * px, transform.position.y + boss.castFrom.y * px, transform.position.z - 0.01f);
            var her = RoadFoe.PlayerBody();
            var aim = ((Vector2)her.center - (Vector2)from).normalized;
            if (Mathf.Sign(aim.x) != side) aim = new Vector2(side, -0.2f).normalized;   // (never back behind it)
            FoeFx.MagicBurst(from, color, 0.7f * size);
            if (boss.volleySound) PitAudio.PlayClip(boss.volleySound, 0.9f);
            else if (boss.castSound) PitAudio.PlayClip(boss.castSound, 0.8f);
            int count = Mathf.Min(volley, n - first);
            for (int i = 0; i < count; i++)
            {
                if (i > 0 && boss.volleySize > 0) { yield return new WaitForSeconds(boss.shotGap); if (IsDead) yield break; }
                float turn = (count > 1 ? Mathf.Lerp(-boss.spreadDegrees * 0.5f, boss.spreadDegrees * 0.5f, i / (count - 1f)) : 0f) + Random.Range(-3f, 3f);
                var go = new GameObject("MAGIC SHOT");
                go.layer = gameObject.layer;
                go.transform.position = from;
                var shot = go.AddComponent<MagicShot>();
                shot.velocity = (Vector2)(Quaternion.Euler(0f, 0f, turn) * aim) * boss.projectileSpeed * Random.Range(0.9f, 1.1f);
                shot.damage = Harder(boss.projectileDamage);
                shot.color = color;
                shot.size = boss.projectileScale * size;
                shot.launchSound = boss.volleySound ? null : boss.shotSound;
                shot.launchDelay = boss.volleySize > 0 ? 0f : i * 0.035f;
                shot.impactSound = boss.shotImpactSound;
            }
        }
    }

    // The cacodaemon's beam: a blazing line straight out level from its mouth, at the height it's at (Wayne's), on
    // past the picture's edge - a white-hot core in a glow of its colour, flickering - hurting her once if it crosses
    // her (jump it, or duck under it where it floats high).
    System.Collections.IEnumerator Beam()
    {
        if (!EndlessKnightDirector.Player) yield break;
        var color = Turned(boss.shieldColor);
        color.a = 1f;
        float px = transform.lossyScale.x * 0.01f, side = _facingRight ? 1f : -1f;
        Vector3 From() => new Vector3(transform.position.x + side * boss.castFrom.x * px, transform.position.y + boss.castFrom.y * px, transform.position.z - 0.012f);
        var from = From();
        var aim = new Vector2(side, 0f);
        const float length = 9f;   // (well past the picture's edge)
        // Stopping where it is, screaming, and charging half a second - her moment to jump or duck - then firing.
        _beaming = true;
        if (boss.beamScream) PitAudio.PlayClip(boss.beamScream, 1f);
        FoeFx.MagicBurst(From(), color, 0.5f * size);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            if (IsDead) { _beaming = false; yield break; }
            if (Random.value < 0.35f) FoeFx.Impact(From(), color, new Vector2(side, 0.3f), 0.4f, 2);
            yield return null;
        }
        from = From();
        FoeFx.MagicBurst(from, color, 0.8f * size);
        if (boss.beamSound) PitAudio.PlayClip(boss.beamSound, 1f);
        else if (boss.volleySound) PitAudio.PlayClip(boss.volleySound, 0.9f);

        if (!s_white) s_white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
        SpriteRenderer Line(string name, int order)
        {
            var go = new GameObject(name);
            go.layer = gameObject.layer;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = s_white;
            r.sortingLayerName = ChestGem.KnightLayer;
            r.sortingOrder = OverKnightOrder + order - 20;   // (its beam, over her)
            return r;
        }
        _beaming = true;
        var glow = Line("BEAM GLOW", 31);
        var core = Line("BEAM", 32);
        float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        float width = boss.beamWidth * RoadFoe.Pixel;
        bool hit = false;
        for (float t = 0f; t < boss.beamSeconds; t += Time.deltaTime)
        {
            if (IsDead || !glow) break;
            from = From();
            // Shooting out fast, holding, thinning away at the end.
            float reach = length * Mathf.Clamp01(t / 0.08f);
            float k = t < boss.beamSeconds - 0.15f ? 1f : (boss.beamSeconds - t) / 0.15f;
            float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 60f);
            foreach (var (r, w, c) in new[] { (glow, width * 2.6f, new Color(color.r, color.g, color.b, 0.55f)), (core, width, Color.Lerp(color, Color.white, 0.75f)) })
            {
                r.transform.position = from;
                r.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                r.transform.localScale = new Vector3(reach, w * k * flicker, 1f);
                r.color = c;
            }
            if (Random.value < 0.25f) FoeFx.Impact(from, color, new Vector2(side, 0.3f), 0.5f, 3);   // (crackling at its mouth)
            // Crossing her: once.
            if (!hit && reach > 0f)
            {
                var b = RoadFoe.PlayerBody();
                Vector2 p = b.center, a = from;
                float along = Mathf.Clamp(Vector2.Dot(p - a, aim), 0f, reach);
                float off = Vector2.Distance(a + aim * along, p);
                if (off < width * 1.3f + Mathf.Min(b.extents.x, b.extents.y))
                {
                    hit = true;
                    RoadFoe.HurtPlayer(Harder(boss.beamDamage), 0.4f, 0.5f, transform.position.x);
                }
            }
            yield return null;
        }
        if (glow) Destroy(glow.gameObject);
        if (core) Destroy(core.gameObject);
        _beaming = false;
    }
    static Sprite s_white;
    bool _beaming;

    /// The name on its health bar.
    public string BarName
    {
        get
        {
            if (look != null && !string.IsNullOrEmpty(look.barName)) return look.barName;
            string name = !string.IsNullOrEmpty(boss.barName) ? boss.barName : (boss.name ?? "").ToUpperInvariant();
            if (!boss.numberBar || nth <= 1) return name;
            if (name.StartsWith("THE ")) name = name.Substring(4);   // (THE GAZER, GAZER II, GAZER III...)
            return name + " " + Roman(nth);
        }
    }

    // A blow of its, harder for each of its kind after the first (damageStep).
    int Harder(int damage) => Mathf.RoundToInt(damage * (1f + Mathf.Max(0f, boss.damageStep) * (nth - 1)));

    // 2 -> II, 14 -> XIV (the numbered bosses' appearances).
    public static string Roman(int n)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var (value, numeral) in new[] { (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") })
            while (n >= value) { sb.Append(numeral); n -= value; }
        return sb.ToString();
    }

    public bool Overlaps(Bounds area)
    {
        // (shielded, the orb takes the blows: it reaches out round it, so a swing finds it)
        // (Rocky: its body only - its frames are wide with the dust and rock round it)
        var b = _shieldLeft > 0 && _shield ? _shield.bounds : boss.rockDeath ? BodyBounds : RoadFoe.Drawn(_sr);
        return !(b.max.x < area.min.x || b.min.x > area.max.x || b.max.y < area.min.y || b.min.y > area.max.y);
    }

    public void OnHit(int damage, bool heavy)
    {
        if (IsDead || _act == Act.Dodge || _act == Act.Berserk || _act == Act.Rise) return;   // (in the air, roaring or still coming up: untouched)
        if (PlayerBuffs.Big) damage = Mathf.RoundToInt(damage * (heavy ? boss.bigHolyMultiplier : boss.bigHitMultiplier));   // (her big blows: the dragon takes a chunk)
        // Sword Buffed, three light attacks fell any boss (as they do the Slime Boss) - a magic shield shattered
        // by the first, which still counts.
        if (PlayerBuffs.Sword && !heavy)
        {
            damage = Mathf.Max(damage, Mathf.CeilToInt(_maxHealth / 3f));
            if (_shieldLeft > 0) { _shieldLeft = 0; BreakShield(); }
        }
        // (the elder dragon, while she's big: a fifth of it at most a blow - five big blows at least to fell it)
        if (PlayerBuffs.Big && IsDragon) damage = Mathf.Min(damage, Mathf.CeilToInt(_maxHealth / 5f));
        if (_shieldLeft > 0)
        {
            _shieldLeft = Mathf.Max(0, _shieldLeft - (heavy ? 2 : 1));
            _shieldFlash = Time.time + 0.15f;
            _shieldWobble = Time.time;
            SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Light);
            if (_shieldLeft == 0) { BreakShield(); return; }
            var cracks = boss.shieldHitSounds;
            if (cracks != null && cracks.Length > 0) PitAudio.PlayClip(cracks[_shieldCrackSound++ % cracks.Length], 1f);
            else if (boss.shieldHitSound) PitAudio.PlayClip(boss.shieldHitSound, 0.75f);
            // Shards and a ripple where the blow struck it (its side toward her), and a new crack in it.
            if (_shield)
            {
                var b = _shield.bounds;
                float toward = EndlessKnightDirector.Player && EndlessKnightDirector.Player.transform.position.x < b.center.x ? -1f : 1f;
                var at = new Vector3(b.center.x + toward * b.extents.x * 0.85f, b.center.y + Random.Range(-0.2f, 0.25f) * b.extents.y, b.center.z - 0.01f);
                FoeFx.ShieldHit(at, ShieldColor, new Vector2(toward, 0.4f));
                FoeFx.Glass(at, ShieldColor, 5, new Vector2(toward, 0.3f), 0.6f);   // (a few chips of it)
                Crack(new Vector2(toward * Mathf.Sign(_shield.transform.lossyScale.x) * Random.Range(0.35f, 0.85f), Random.Range(-0.7f, 0.7f)));   // (each blow its own crack, somewhere on her side)
            }
            return;
        }
        Hurt(damage);
        if (heavy) _flashUntil = Time.time + HeavyFlash;
    }

    public void Chip(int damage, float push)
    {
        if (IsDead || _shieldLeft > 0) return;
        Hurt(damage, chip: true);
        transform.position += Vector3.right * (_facingRight ? -push : push) * 0.3f;
    }

    // Its blow landing on her: its HIT burst over her, on her side toward it - in her depth group, over
    // her and her sword's slash and everything else there.
    void HitFx()
    {
        var player = EndlessKnightDirector.Player;
        var art = player ? player.transform.Find("ARIANA") : null;
        var sr = art ? art.GetComponent<SpriteRenderer>() : null;
        if (!boss.hitFx || !sr) return;
        var her = RoadFoe.Drawn(sr);
        float toward = transform.position.x >= her.center.x ? 1f : -1f;
        var at = new Vector3(her.center.x + toward * her.extents.x * 0.4f, her.center.y + her.extents.y * 0.15f, her.center.z - 0.05f);
        var fx = Instantiate(boss.hitFx, at, Quaternion.identity);
        foreach (var t in fx.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = art.gameObject.layer;
        foreach (var r in fx.GetComponentsInChildren<Renderer>(true)) { r.sortingLayerID = sr.sortingLayerID; r.sortingOrder = 400; }
    }

    void Hurt(int damage, bool chip = false)
    {
        _health -= damage;
        if (boss.rockDeath && !chip)
        {
            // (Rocky: big rocks breaking off where she struck, tumbling down, and their crack)
            var b = HitBody();
            var at = new Vector3(b.center.x, b.center.y - RockHitDrop * RoadFoe.Pixel, transform.position.z - 0.01f);   // (the middle of its hit box)
            FoeFx.Rocks(at, RockLight, RockDark, 8, b.extents.x * 0.35f, bigShare: 0.55f, bigSize: 1.4f, ground: SpikeTrap.PlayerFeetY);
            if (boss.smashSound && Time.time >= _nextRockSound) { _nextRockSound = Time.time + 0.15f; PitAudio.PlayClip(boss.smashSound, 0.55f); }
        }
        if (IsViking)
        {
            // (the vikings bleed neon aquamarine, like level 4's skeletons)
            if (!chip) FoeFx.Gore(RoadFoe.Drawn(_sr).center, VikingBlood, 1f, 10, follow: transform);
        }
        else if (HitBlood.a > 0f && !chip) FoeFx.Gore(HitPoint(out var gore) ? gore : RoadFoe.Drawn(_sr).center, HitBlood, 1f, 8, follow: transform);
        if (HitBlood.a > 0f && boss.hitSpray > 0 && !IsViking)
        {
            // Glowing pixel drops of its blood sprayed out of its side toward her.
            var b = RoadFoe.Drawn(_sr);
            float toward = PlayerX >= b.center.x ? 1f : -1f;
            var from = HitPoint(out var spot) ? spot : new Vector3(b.center.x + toward * b.extents.x * 0.4f, b.center.y, b.center.z - 0.02f);
            FoeFx.Over(_sr, () => FoeFx.Blood(from, HitBlood, chip ? boss.hitSpray / 3 : boss.hitSpray, 1.2f, 1.2f));   // (over it, as it's over her)
        }
        // Neon sparks off it where the blow lands (its side toward her), flung away from her - its blood's colour.
        {
            var b = HitBody();
            float away = PlayerX >= b.center.x ? -1f : 1f;
            var spark = HitBlood.a > 0f ? HitBlood : new Color(1f, 0.85f, 0.5f);
            var hitAt = boss.rockDeath ? new Vector3(b.center.x, b.center.y - RockHitDrop * RoadFoe.Pixel, b.center.z - 0.02f)   // (Rocky: the middle of its hit box, down a little - its body, not its head)
                : HitPoint(out var point) ? point
                : new Vector3(b.center.x - away * b.extents.x * 0.4f, b.center.y + Random.Range(-0.15f, 0.15f) * b.extents.y, b.center.z - 0.02f);
            FoeFx.Impact(hitAt, spark, new Vector2(away, 0.45f), chip ? 0.7f : 1.4f, chip ? 4 : 14, transform, _sr);   // (over it, as it's over her)
        }
        _flashUntil = Time.time + 0.12f;
        // (the fairy's spikes: every tenth, a second without flinching - the gazer's eye open again a while)
        if (chip && ++_chips % 10 == 0) { _noFlinchUntil = Time.time + 1f; _hurtUntil = 0f; }
        if (_act == Act.None && !(chip && Time.time < _noFlinchUntil)) _hurtUntil = Time.time + 0.3f;
        if (boss.hitSound) PitAudio.PlayClip(boss.hitSound, 0.9f);   // (the blow landing)
        var hurts = boss.hurtSounds != null && boss.hurtSounds.Length > 0 ? boss.hurtSounds : hurtSounds;
        if (hurts != null && hurts.Length > 0) PitAudio.PlayClip(hurts[Random.Range(0, hurts.Length)], 0.7f);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Medium);
        if (_health <= 0) { Die(); return; }
        if (boss.berserkAt > 0f && !_berserk && _health <= _maxHealth * boss.berserkAt)
        {
            Begin(Act.Berserk);
            if (boss.roarSound) PitAudio.PlayClip(boss.roarSound, 1f);
            SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Heavy);
            return;
        }
        // Sometimes it jumps back out of the way of what comes next.
        if (boss.dodgeChance > 0f && !boss.forwardOnly && _act == Act.None && Random.value < 0.35f) Dodge();
    }

    bool _dieWhenLanded;
    float _fall;
    void Die()
    {
        // ARcade: killed in the air - the big frog's leaps in, its jumps back - it finishes the leap and lands first
        // (untouched meanwhile: nothing touches it in the air), then dies on the ground.
        bool airborne = boss.floatHeight <= 0f && transform.position.y > SpikeTrap.PlayerFeetY + 0.01f;
        if (airborne && (_act == Act.Leap || _act == Act.Dodge)) { _dieWhenLanded = true; return; }
        if (_dieWhenLanded) return;
        // (with a last gout of blood from its mouth first, its death frames wait)
        _deadAt = Time.time + Mathf.Max(0f, boss.deathSpew, boss.deathShake);
        if (!_cleared) RoadFoe.KillPoints(boss: true);
        _act = Act.None;
        _sr.color = Color.white;
        if (boss.deathShake > 0f && boss.deathCry) DeathVoice(boss.deathCry);   // (its cry as it shakes; its death sound as it bursts)
        else if (boss.deathSound) DeathVoice(boss.deathSound);
        else if (deathSounds != null && deathSounds.Length > 0) PitAudio.PlayClip(deathSounds[Random.Range(0, deathSounds.Length)], 0.8f);
        if (boss.deathScreams != null && boss.deathScreams.Length > 0) StartCoroutine(Screams());
        if (_aura) Destroy(_aura.gameObject);
        if (_breath) Destroy(_breath.gameObject);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Rigid);
        foreach (var e in _summoned) if (e) e.Vanish();
        if (_shield) Destroy(_shield.gameObject);
        var motes = GetComponent<GateMotes>();
        if (motes) Destroy(motes);
        // Gollux: gone at once in its rocks (Wayne's) - no burst of magic.
        if (boss.rockDeath) { RockDeath(); return; }
        // A big death: its magic bursting out of it at once, crowding its middle (then more as it falls).
        if (boss.deathBurst > 1f && _sr && boss.deathSpew <= 0f && boss.deathShake <= 0f) BigDeath();
    }

    // Shaking side to side as it dies (k: how hard, 0-1; below 0: still again).
    float _shakeOff;
    void Shake(float k)
    {
        transform.position -= Vector3.right * _shakeOff;
        _shakeOff = k < 0f ? 0f : Random.Range(-1f, 1f) * RoadFoe.Pixel * (1f + 3f * k) * size;
        transform.position += Vector3.right * _shakeOff;
    }

    float _nextSpew;
    bool _spewDone;
    int _volleyLeft = -1;
    float _nextVolley;

    // Its dying volley: bursts of its blood across its body, staggered through its death frames and
    // its fading, the last of them while it's still there.
    void Volley(float t)
    {
        if (boss.deathVolley <= 0 || DeathBlood.a <= 0f || !_sr) return;
        float span = Length(look.death) / Mathf.Max(1f, boss.fps) + 0.5f;   // (its death frames, and most of its fade)
        if (_volleyLeft < 0) { _volleyLeft = boss.deathVolley; _nextVolley = Time.time; }
        if (_volleyLeft == 0 || Time.time < _nextVolley) return;
        _volleyLeft--;
        _nextVolley = Time.time + span / boss.deathVolley * Random.Range(0.6f, 1.3f);
        var b = RoadFoe.Drawn(_sr);
        var at = new Vector3(b.center.x + Random.Range(-0.4f, 0.4f) * b.size.x, b.min.y + Random.Range(0.15f, 0.7f) * b.size.y, b.center.z);
        Bleed(at, Random.Range(0.9f, 1.4f), 8);
    }

    // A burst of its blood: plain splashes (bloodSplash, its sound every other one), or its glowing gore.
    int _splashes;
    void Bleed(Vector3 at, float size, int drops)
    {
        // (all of it over its body)
        if (IsViking) { FoeFx.Over(_sr, () => FoeFx.Gore(at, VikingBlood, size, drops)); return; }   // (neon aquamarine, like level 4's skeletons)
        if (!boss.bloodSplash) { FoeFx.Over(_sr, () => FoeFx.Gore(at, DeathBlood, size, drops)); return; }
        FoeFx.Over(_sr, () => FoeFx.Splash(at, DeathBlood, size));
        if (boss.magicBurstSound && _splashes++ % 2 == 0) PitAudio.PlayClip(boss.magicBurstSound, 0.6f);
    }

    void Dying()
    {
        // (died mid-hop or otherwise up off the road, not a flyer: down onto it first - the rest held till it lands)
        if (boss.floatHeight <= 0f && transform.position.y > SpikeTrap.PlayerFeetY + 0.001f)
        {
            _fall += 6f * Time.deltaTime;
            float y = Mathf.Max(SpikeTrap.PlayerFeetY, transform.position.y - _fall * Time.deltaTime);
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
            _deadAt += Time.deltaTime;
            if (y <= SpikeTrap.PlayerFeetY) FoeFx.Dirt(transform.position, 8, BodyBounds.size.x);
            return;
        }
        float t = Time.time - _deadAt;
        if (t >= 0f) Volley(t);
        if (t < 0f && boss.deathSpew <= 0f)
        {
            // Shaking, harder and harder, crying out: then it bursts.
            Shake(Mathf.Clamp01(1f + t / Mathf.Max(0.01f, boss.deathShake)));
            if (look.hurt != null && look.hurt.Length > 0) _sr.sprite = look.hurt[0];
            if (!_spewDone && t + Time.deltaTime >= 0f)
            {
                _spewDone = true;
                Shake(-1f);
                if (boss.deathShake > 0f && boss.deathCry && boss.deathSound) DeathVoice(boss.deathSound);
                if (boss.deathBurst > 1f) BigDeath();
            }
            return;
        }
        if (t < 0f)
        {
            // Its last gout: mouth open, blood pouring out of it along the road, the way it's facing.
            var open = look.breath != null && look.breath.Length > 0 ? look.breath : look.attack;
            if (open != null && open.Length > 0) _sr.sprite = open[Mathf.Min(2, open.Length - 1)];
            if (Time.time >= _nextSpew)
            {
                _nextSpew = Time.time + 0.05f;
                float px = transform.lossyScale.x * 0.01f, side = _facingRight ? 1f : -1f;
                var mouth = new Vector3(transform.position.x + side * boss.breathMouth.x * px, transform.position.y + boss.breathMouth.y * px, transform.position.z);
                float k = 1f + t / Mathf.Max(0.01f, boss.deathSpew);   // (0 to 1 as it pours)
                var at = mouth + new Vector3(side * Mathf.Lerp(0.1f, 0.9f, Random.value) * (0.6f + k), -Random.Range(0f, 0.25f) * (0.5f + k), 0f);
                Bleed(at, 1.1f, 6);
            }
            if (!_spewDone && t + Time.deltaTime >= 0f)
            {
                // Then it bursts, its blood all through it.
                _spewDone = true;
                if (boss.deathBurst > 1f) BigDeath();
            }
            return;
        }
        Show(look.death, t, false);
        float end = Length(look.death) / Mathf.Max(1f, boss.fps);
        if (_chestAt < 0f && t >= end)
        {
            _chestAt = Time.time;
            var at = HitBody().center;   // (Rocky: the middle of its hit box - its frame's mostly clear room)
            float burst = Mathf.Max(1f, boss.deathBurst);
            // (a big death's bursts all went off as it died - BigDeath: none now, as its body fades)
            if (boss.rockDeath) { }   // (gone in its rocks as it died: Die)
            else if (burst <= 1f)
            {
                if (DeathBlood.a > 0f) Bleed(at, 1.6f, 20);
                if (boss.deathBones) FoeFx.Over(_sr, () => FoeFx.Bones(at, 12, true, 1f));
            }
            if (chestPrefab && boss.dropChest) Instantiate(chestPrefab, at, Quaternion.identity);
            // (the dragons: a spray of gold - points only - and their gems, as gems; the final one, gold alone and lots of it)
            bool final = IsDragon && look != null && look.maxSummons > 0;
            if (IsDragon) Gold(at, gameObject.layer, final ? GoldCount * 2 : GoldCount);
            if (look != null && look.goldenApples > 0) GoldenApples(at, gameObject.layer, look.goldenApples, look.appleSound);
            if (boss.gemPrefab && !final) StartCoroutine(Gems(at));
            DropSkyChests();   // (if it didn't burst)
            if (boss.treasurePrefab) Treasure(at);
            if (boss.foods != null && boss.foods.Length > 0) StartCoroutine(Food(at));
            if (boss.deathSpawn != null && boss.deathSpawnCount > 0) Brood(at);
            RowDrops(at);
        }
        if (_chestAt >= 0f && boss.rockDeath)
        {
            if (Time.time - _chestAt > 1.6f) Destroy(gameObject);   // (gone once its rocks have come down)
        }
        else if (_chestAt >= 0f)
        {
            float k = Mathf.Clamp01(1f - (Time.time - _chestAt) / 0.8f);
            _sr.color = new Color(1f, 1f, 1f, k);
            foreach (var v in _deathVoices) if (v) v.volume = k;   // (its cries fade with it)
            if (k <= 0f) Destroy(gameObject);
        }
    }

    // Its death cries, on voices of their own: they fade as its body does, and stop as it's gone (OnDestroy).
    readonly System.Collections.Generic.List<AudioSource> _deathVoices = new System.Collections.Generic.List<AudioSource>();
    void DeathVoice(AudioClip clip) { var v = PitAudio.Voice(clip, 1f); if (v) _deathVoices.Add(v); }

    // The road's swarm coming in with it (entryRats: the gazer's rats), one at a time, out ahead of it, once it's in view.
    int _ratsLeft = -1;
    float _nextRat;
    void EntryRats()
    {
        if (_ratsLeft == 0 || boss.entryRats <= 0) return;
        if (_ratsLeft < 0) _ratsLeft = Mathf.Min(boss.entryRatsMax > 0 ? boss.entryRatsMax : int.MaxValue, boss.entryRats * nth);
        if (Time.time < _nextRat) return;
        var cam = SpatialEmulator.Demo.PitDemoGame.ActorsCamera;
        if (cam && Mathf.Abs(transform.position.x - cam.transform.position.x) > cam.orthographicSize * cam.aspect) return;
        var foes = FindAnyObjectByType<LevelFoes>();
        var kinds = foes && foes.road != null ? System.Array.FindAll(foes.road, f => f.swarm > 0) : null;
        if (kinds == null || kinds.Length == 0) { _ratsLeft = 0; return; }
        _nextRat = Time.time + 0.3f;
        _ratsLeft--;
        Brood(new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z), 1, towardHer: true, kind: kinds[Random.Range(0, kinds.Length)]);
    }

    // Its brood hopping in with it (entryBrood): some out of it as each of its entry leaps lands, the rest as the last one does.
    int _entryBred;
    void EntryBrood(int leapsLeft, bool all = false)
    {
        if (boss.deathSpawn == null || boss.entryBrood <= 0 || nth < 2) return;
        int total = Mathf.Min(boss.entryBroodMax > 0 ? boss.entryBroodMax : int.MaxValue, boss.entryBrood * (nth - 1));
        int now = all ? total - _entryBred : Mathf.CeilToInt((total - _entryBred) / (float)Mathf.Max(1, leapsLeft));
        if (now <= 0) return;
        _entryBred += now;
        Brood(new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z), now, towardHer: true);
    }

    // Its brood thrown out of it, both ways, landing and coming for her.
    void Brood(Vector3 at, int count = -1, bool towardHer = false, LevelFoes.Foe kind = null)
    {
        Material colours = null;
        if (RoadEnemy.HueMaterial && (look.broodHue != 0f || look.broodSaturation != 1f || look.broodBrightness != 1f))
        {
            colours = new Material(RoadEnemy.HueMaterial) { name = "Brood Colours" };
            colours.SetFloat("_Hue", Mathf.Repeat(look.broodHue / 360f, 1f));
            colours.SetFloat("_Saturation", look.broodSaturation);
            colours.SetFloat("_Brightness", look.broodBrightness);
        }
        // (thrown out its far side, away from her - not down on top of her)
        var body = RoadFoe.Drawn(_sr);
        float side = PlayerX < body.center.x ? 1f : -1f;
        var away = new Vector3(side > 0f ? body.max.x : body.min.x, at.y, at.z);
        if (count < 0) count = boss.deathSpawnCount;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("BROOD " + (kind != null ? kind.name : boss.deathSpawn.name));
            go.layer = gameObject.layer;
            go.transform.position = new Vector3(away.x, at.y, transform.position.z);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "TopLayer";
            sr.sortingOrder = 6;
            var e = go.AddComponent<RoadEnemy>();
            e.foe = kind ?? BroodColors.For(boss.deathSpawn, _sr.sprite);   // (a Giant Toad's babies in its colours)
            e.recolor = colours;
            e.selfAnchored = true;
            if (towardHer)
            {
                // (hopping out ahead of it on its way in, toward her, some short, some long)
                float to = PlayerX < at.x ? -1f : 1f;
                go.transform.position = new Vector3(at.x, at.y, transform.position.z);
                e.Launch(new Vector2(to * Random.Range(0.4f, 1.6f), Random.Range(1.4f, 2.4f)));
            }
            else if (boss.broodEverywhere)
            {
                // All over the courtyard: both ways, some near, some far.
                go.transform.position = new Vector3(body.center.x, at.y, transform.position.z);
                e.Launch(new Vector2((i % 2 == 0 ? 1f : -1f) * Random.Range(0.3f, 2.6f), Random.Range(1.8f, 3.4f)));
            }
            else e.Launch(new Vector2(side * Random.Range(0.5f, 1.4f), Random.Range(1.6f, 2.8f)));
        }
    }

    // Gollux's death, the moment it dies: its body gone, blown apart into rocks - mostly big chunks - flung out
    // every way from all over it, and a little dust.
    void RockDeath()
    {
        var body = RoadFoe.Drawn(_sr);
        // (out of the middle of it - not its whole frame - and gentler)
        for (int i = 0; i < 4; i++)
        {
            var at = new Vector3(body.center.x + Random.Range(-0.2f, 0.2f) * body.size.x, body.center.y + Random.Range(-0.2f, 0.2f) * body.size.y, transform.position.z - 0.01f);
            FoeFx.Rocks(at, RockLight, RockDark, 7, body.extents.x * 0.2f, blast: true, bigShare: 0.6f, bigSize: 1.2f, size: 0.45f);
        }
        FoeFx.Poof(body.center, 3, 0.5f);
        if (boss.smashSound) PitAudio.PlayClip(boss.smashSound, 0.8f);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Medium);
        _sr.enabled = false;
        _sr.color = Color.clear;
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;   // (its outline, aura and shadow too)
    }

    // A big death, all at once as it dies: its burst of blood (or magic), its bones, smoke, and a
    // volley of blasts crowding its middle - every one while its body's still there to burst.
    void BigDeath()
    {
        DropSkyChests();   // (its chests fall as it bursts)
        var body = RoadFoe.Drawn(_sr);
        float burst = boss.deathBurst;
        if (DeathBlood.a > 0f && !boss.magicDeath) Bleed(body.center, 1.6f * Mathf.Sqrt(burst), Mathf.RoundToInt(20 * burst));
        FoeFx.Over(_sr, () =>
        {
            if (boss.deathBones) FoeFx.Bones(body.center, Mathf.RoundToInt(12 * burst), true, Mathf.Sqrt(burst));
            FoeFx.Poof(body.center, Mathf.RoundToInt(6 * burst), burst);
        });
        StartCoroutine(Blasts(body, Mathf.RoundToInt(3 * burst), 0.35f, 0f));
    }

    // A volley of blasts across its body (harmless), its blood with each.
    // (spread: how far from its middle they go off, a share of its body; gap: seconds between them)
    System.Collections.IEnumerator Blasts(Bounds body, int count, float spread = 1f, float gap = 0.12f)
    {
        for (int i = 0; i < count; i++)
        {
            var at = new Vector3(body.center.x + Random.Range(-1f, 1f) * body.extents.x * spread,
                                 spread >= 1f ? body.center.y : body.min.y + body.size.y * Random.Range(0.15f, 0.45f), body.center.z);   // (its blast rises from there)
            if (boss.magicDeath)
            {
                FoeFx.Potion(at, Turned(boss.shieldColor), 1.3f);   // (a magic potion of blood bursting)
                if (boss.magicBurstSound && i % 2 == 0) PitAudio.PlayClip(boss.magicBurstSound, 0.55f);
            }
            else if (DeathBlood.a > 0f) Bleed(at + Vector3.up * 0.1f, 1.4f, 10);   // (bursts of its blood, not explosions)
            else FoeFx.Blast(RoadEnemy.BlastFrom, at, BlastHue, RoadEnemy.HueMaterial, 0, false, onGround: spread >= 1f);
            if (boss.magicDeath && boss.deathBlood.a > 0f) FoeFx.Blood(at + Vector3.up * 0.2f, boss.deathBlood, 14, 1.4f, 1.3f);
            if (gap > 0f) yield return new WaitForSeconds(gap);
        }
    }

    // ARcade: FAFNIR's golden apples - the fruit pack's apple in gold, thrown up out of it sparkling: each eaten heals
    // AppleHeal of her bar.
    const float AppleHeal = 0.2f;
    static Sprite s_apple;
    static void GoldenApples(Vector3 at, int layer, int count, AudioClip sound)
    {
        if (!s_apple) s_apple = Resources.Load<Sprite>("Collect/Gold/golden_apple");
        if (!s_apple) return;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("GOLDEN APPLE");
            go.layer = layer;
            go.transform.position = at + new Vector3(Random.Range(-0.1f, 0.1f), 0.15f, -0.013f - i * 0.0005f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s_apple;
            sr.sortingLayerName = "TopLayer";
            sr.sortingOrder = 23;
            var item = go.AddComponent<ChestGem>();
            item.frames = new[] { s_apple };
            item.fps = 0f;
            item.scale = 0.8f;   // (a fruit's: 16 px)
            item.healShare = AppleHeal;
            item.collectSound = sound;
            item.burst = new Color(1f, 0.85f, 0.3f);
            item.scoreValue = 250;
            item.lifetime = 20f;
            item.velocity = new Vector2((i % 2 == 0 ? 1f : -1f) * Random.Range(0.4f, 1.4f), Random.Range(2.6f, 3.8f));
            item.Quick();
            var sparks = GateMotes.Add(sr, GateMotes.Kind.Sparks, new Color(1f, 0.85f, 0.35f));
            if (sparks) sparks.count = 6;
        }
    }

    // ARcade: the dragons' gold - Wayne's coin, small, sprayed high out of it in a fountain, each worth points only (no
    // health, no mana), picked up with a chink and a burst of gold.
    const int GoldCount = 60, GoldPoints = 200;
    static Sprite s_coin;
    static AudioClip s_coinSound;
    static void Gold(Vector3 at, int layer, int count)
    {
        if (!s_coin) s_coin = Resources.Load<Sprite>("Collect/Gold/coin");
        if (!s_coinSound) s_coinSound = Resources.Load<AudioClip>("Collect/Sounds/coin_pickup");
        if (!s_coin) return;
        var gold = new Color(1f, 0.82f, 0.3f);
        FoeFx.MagicBurst(at, gold, 1.6f);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("DRAGON GOLD");
            go.layer = layer;
            go.transform.position = at + new Vector3(Random.Range(-0.08f, 0.08f), 0.1f, -0.012f - (i % 40) * 0.0005f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s_coin;
            sr.sortingLayerName = "TopLayer";
            sr.sortingOrder = 22;
            var item = go.AddComponent<ChestGem>();
            item.frames = new[] { s_coin };
            item.fps = 0f;
            item.scale = 0.3f;
            item.collectSound = s_coinSound;
            item.burst = gold;
            item.scoreValue = GoldPoints;
            item.lifetime = 20f;
            item.velocity = new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(2.8f, 4.8f));
            item.Quick();
        }
    }

    // Its food thrown out (its set: burgers, burritos, pizzas... one set each in turn), high and
    // fanning out, sparkling: eaten hurt, it heals her; eaten at full health, she grows big.
    System.Collections.IEnumerator Food(Vector3 at)
    {
        FoodFrom(boss, nth, at, gameObject.layer);
        yield break;
    }

    // `source`'s food (its nth set: burgers, burritos, pizzas...), thrown out at `at` to be picked up.
    static void FoodFrom(LevelFoes.Boss source, int nth, Vector3 at, int layer)
    {
        var boss = source;
        var set = boss.foods[(nth - 1) % boss.foods.Length];
        if (set == null || set.items == null || set.items.Length == 0) return;
        for (int i = 0; i < boss.foodCount; i++)
        {
            var go = new GameObject("VIKING FOOD");
            go.layer = layer;
            go.transform.position = at + new Vector3(Random.Range(-0.1f, 0.1f), 0.1f, -0.012f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = set.items[i % set.items.Length];
            sr.sortingLayerName = "TopLayer";
            sr.sortingOrder = 22;
            var item = go.AddComponent<ChestGem>();
            item.frames = new[] { sr.sprite };
            item.fps = 0f;
            item.scale = boss.foodScale;
            item.healShare = boss.foodHeal;
            item.bigSeconds = boss.foodBigSeconds;
            item.collectSound = boss.foodSound;
            item.bigSound = boss.growSound;
            item.shrinkSound = boss.shrinkSound;
            item.scoreValue = 250;
            item.lifetime = 20f;
            item.velocity = new Vector2((i % 2 == 0 ? 1f : -1f) * Random.Range(0.3f, 1.3f), Random.Range(2.4f, 3.6f));
            item.Quick();
            item.restSeconds = 0.6f;   // (down on the road a moment, to be seen, before it's off to her)
            var sparks = GateMotes.Add(sr, GateMotes.Kind.Sparks, new Color(0.5f, 1f, 0.45f));
            if (sparks) sparks.count = 8;
        }
    }

    static AudioClip s_eggBounce;
    static AudioClip EggBounce => s_eggBounce ? s_eggBounce : s_eggBounce = Resources.Load<AudioClip>("Collect/Sounds/egg_bounce");
    static AudioClip s_eggCollect;
    static AudioClip EggCollect => s_eggCollect ? s_eggCollect : s_eggCollect = Resources.Load<AudioClip>("Collect/Sounds/egg_collect");
    static AudioClip s_rowSound;
    static AudioClip RowSound => s_rowSound ? s_rowSound : s_rowSound = Resources.Load<AudioClip>("Collect/Sounds/row_item");

    // What the level's row still needs (LevelCollection: a key, an egg, two potions): the next ones she hasn't got
    // this run, popping up beside it and flying to her like the rest, sparkling.
    void RowDrops(Vector3 at)
    {
        var set = LevelCollection.For(PitDemoGame.Level);
        var row = FruitTracker.Instance;
        if (set == null || !row) return;
        var missing = row.Missing(set.perBoss);
        for (int n = 0; n < missing.Count; n++)
        {
            int i = missing[n];
            var go = new GameObject(set.name + " " + i);
            go.layer = gameObject.layer;
            go.transform.position = at + new Vector3(-0.15f + 0.3f * n, 0.15f, -0.013f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = set.frames[i][0];
            var item = go.AddComponent<ChestGem>();
            item.frames = set.frames[i];
            item.fps = set.fps;
            item.scale = set.scale;
            item.trackAs = set.icons[i];
            // (its boss's cha-ching - or, for one with none, the row's own: Resources/Collect/Sounds)
            item.collectSound = boss.treasureSound ? boss.treasureSound : boss.gemSound ? boss.gemSound : RowSound;
            item.scoreValue = 1000;
            item.lifetime = 30f;
            item.velocity = new Vector2((n % 2 == 0 ? -1f : 1f) * Random.Range(0.2f, 0.5f), Random.Range(3f, 3.6f));
            item.Quick();
            if (PitDemoGame.Level == 3)
            {
                // (level 3's egg: heavy, bouncing on the road three times, a knock each, before it's off to her)
                item.gravity *= 1.7f;
                item.velocity.y *= 1.25f;
                item.bounce = 0.6f;
                item.bounces = 3;
                item.bounceSound = EggBounce;
                item.sitOnBottom = true;   // (on the road on its bottom, not sunk to its middle)
                // (and picked up: its own pop and chime, little copies of it bursting out of it)
                if (EggCollect) item.collectSound = EggCollect;
                item.miniEggs = new[] { set.frames[i][0] };
            }
            if (PitDemoGame.Level == 4)
            {
                // (level 4's potion: big, and sitting on the sidewalk a second, to be seen, before it's hers)
                item.scale *= 2f;
                item.restSeconds = 1f;
            }
            var sparks = GateMotes.Add(sr, GateMotes.Kind.Sparks, new Color(1f, 0.85f, 0.35f));
            if (sparks) sparks.count = 10;
        }
    }

    // Its special treasure: one big spinning gem, sparkling, worth treasureValue.
    void Treasure(Vector3 at)
    {
        // Hopping out beside the chest (to its right, not over it), bouncing there to be picked up.
        var gem = Instantiate(boss.treasurePrefab, at + new Vector3(0.3f, 0.15f, -0.012f), Quaternion.identity);
        if (gem.TryGetComponent<ChestGem>(out var item))
        {
            item.velocity = new Vector2(0.35f, 3.2f);
            item.Quick();
            if (boss.treasureSound) item.collectSound = boss.treasureSound;   // (a cha-ching as she takes it)
            item.scoreValue = boss.treasureValue;
            item.scale *= boss.treasureScale;
            item.lifetime = Mathf.Max(item.lifetime, 20f);
            item.manaShare = 1f;
            item.Sweeten(1.8f);   // (a big sweet past level 1)
        }
        if (gem.TryGetComponent<SpriteRenderer>(out var sr))
        {
            var sparks = GateMotes.Add(sr, GateMotes.Kind.Sparks, new Color(1f, 0.9f, 0.5f));
            if (sparks) sparks.count = 14;
        }
    }

    // Its screams as it dies, one after another, loud.
    System.Collections.IEnumerator Screams()
    {
        foreach (var scream in boss.deathScreams)
        {
            yield return new WaitForSeconds(boss.screamGap);
            if (scream) PitAudio.PlayClip(scream, 1f);
        }
    }

    // Treasure chests falling out of the sky where its body was, one after another, spread across
    // it; then its victory fanfare.
    const int CrystalChestLook = 6, CrystalChestLoot = 8;   // (TREASURE CHEST's purple crystal; its pink gems and amethysts)

    // As it bursts: its chests out of the sky (they and the fanfare outlive it: run on her, not on it).
    bool _chestsDropped;
    // ARcade: level 4's elder dragons - gold sprayed out as they die (Gold) and one chest out of the sky, of the vikings' food.
    /// Its health bar and name this much higher (her pixels): the first Giant Toad's sat too low.
    public float BarLiftPixels => boss != null && boss.name == "Giant Toad" && nth == 1 ? 15f : 0f;
    public bool IsDragon => SpatialEmulator.Demo.PitDemoGame.Level == 4 && boss != null && boss.name == "Elder Dragon";
    float _nextRockSound;
    const float RockHitDrop = 50f;   // (her pixels: Rocky's hit rocks and sparks this far under its hit box's middle)

    // Where a blow lands on it: Rocky's hit box (the body its blows are checked against: Overlaps); any other boss, what's drawn.
    Bounds HitBody() => boss.rockDeath ? BodyBounds : RoadFoe.Drawn(_sr);

    /// Where everything hitting it lands (its hitAt), if it has one - off the gazer's eye.
    public bool HitPoint(out Vector3 at)
    {
        at = default;
        if (boss == null || boss.hitAt == Vector2.zero) return false;
        float px = transform.lossyScale.x * 0.01f, side = _facingRight ? 1f : -1f;
        at = new Vector3(transform.position.x + side * boss.hitAt.x * px, transform.position.y + boss.hitAt.y * px, transform.position.z - 0.02f);
        return true;
    }
    static readonly Color VikingBlood = new Color(0.25f, 1f, 0.95f, 1f);
    bool IsViking => SpatialEmulator.Demo.PitDemoGame.Level == 4 && boss != null && boss.name == "Fantasy #25";

    void DropSkyChests()
    {
        int chests = IsDragon ? 1 : look != null ? look.skyChests : 0;
        if (_chestsDropped || look == null || chests <= 0 || !chestPrefab || !EndlessKnightDirector.Player) return;
        _chestsDropped = true;
        System.Action<Vector3> spill = null;
        if (IsDragon)
        {
            var vikings = FindAnyObjectByType<LevelFoes>()?.altBoss;
            int layer = gameObject.layer, n = nth;
            if (vikings != null && vikings.foods != null && vikings.foods.Length > 0) spill = at => FoodFrom(vikings, n, at, layer);
        }
        // (the final dragon - the one calling up zombies - leaves no chest: its dark crystal explodes out of it: CrystalPrize)
        bool finalDragon = look.maxSummons > 0;
        EndlessKnightDirector.Player.StartCoroutine(SkyChests(chestPrefab, RoadFoe.Drawn(_sr), finalDragon ? 0 : chests, look.victorySound, spill));
        if (finalDragon)
        {
            CrystalPrize.Coming = true;
            CrystalPrize.Spawn(RoadFoe.Drawn(_sr).center, gameObject.layer);
            // (and that's the end of the run: nothing more comes, and what's out goes)
            EndlessKnightDirector.Hold = true;
            var director = FindAnyObjectByType<EndlessKnightDirector>();
            if (director) director.ClearRoad();
            foreach (var other in RoadFoe.Bosses()) if (other is RoadBoss rb && rb && rb != this && !rb.IsDead) { rb._cleared = true; rb.OnHit(99999, true); }
        }
    }

    static System.Collections.IEnumerator SkyChests(GameObject chestPrefab, Bounds body, int count, AudioClip victory, System.Action<Vector3> spill = null)
    {
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(body.min.x, body.max.x, (i + 0.5f) / count);
            TreasureChest.ForcedLook = CrystalChestLook; TreasureChest.ForcedLoot = CrystalChestLoot;   // (all the crystal chest, with its gems)
            TreasureChest.ForcedSpill = spill;   // (or, the dragons', the vikings' food)
            Instantiate(chestPrefab, new Vector3(x, body.center.y, body.center.z), Quaternion.identity);
            TreasureChest.ForcedLook = TreasureChest.ForcedLoot = -1;
            TreasureChest.ForcedSpill = null;
            yield return new WaitForSeconds(0.4f);
        }
        yield return new WaitForSeconds(0.9f);
        if (victory) PitAudio.PlayClip(victory, 1f);
    }

    // Its gems bursting out all at once, high and fanning to both sides, each worth gemValue.
    System.Collections.IEnumerator Gems(Vector3 at)
    {
        for (int i = 0; i < boss.gemCount; i++)
        {
            var kinds = boss.gemPrefabs != null && boss.gemPrefabs.Length > 0 ? boss.gemPrefabs : null;
            var kind = kinds != null && i % (kinds.Length + 1) != 0 ? kinds[i % (kinds.Length + 1) - 1] : boss.gemPrefab;
            if (!kind) kind = boss.gemPrefab;
            var gem = Instantiate(kind, at + new Vector3(0f, 0.1f, -0.01f - i * 0.001f), Quaternion.identity);
            if (boss.gemSound && i % 6 == 0) PitAudio.PlayClip(boss.gemSound, 0.5f);   // (all at once now: a few, not thirty)
            if (gem.TryGetComponent<ChestGem>(out var item))
            {
                item.scale *= boss.gemScale;
                item.velocity = boss.gemFountain
                    ? new Vector2(Random.Range(-0.45f, 0.45f), Random.Range(3.2f, 4.6f))   // (a fountain of them)
                    : new Vector2(Random.Range(-1.3f, 1.3f), Random.Range(3f, 4.6f));
                item.scoreValue = boss.gemValue;
                item.manaShare = 1f / Mathf.Max(1, boss.gemCount);
                if (!IsDragon) item.Sweeten();   // (sweets past level 1 - the dragons' gems kept gems)
                item.Quick();
            }
        }
        yield break;
    }

    // Its fire's lava: glowing drops off the flames as they are this frame, dripping down onto the
    // road and pooling there, cooling.
    float _nextLava;
    void Lava(float side)
    {
        if (LavaColor.a <= 0f || !_breath || !_breath.sprite || Time.time < _nextLava) return;
        _nextLava = Time.time + 0.02f;
        var sprite = _breath.sprite;
        var v = sprite.vertices;
        var tris = sprite.triangles;
        if (tris.Length < 3) return;
        for (int n = 0; n < 2; n++)
        {
            int t = Random.Range(0, tris.Length / 3) * 3;
            float a = Random.value, b = Random.value;
            if (a + b > 1f) { a = 1f - a; b = 1f - b; }
            var local = v[tris[t]] + a * (v[tris[t + 1]] - v[tris[t]]) + b * (v[tris[t + 2]] - v[tris[t]]);
            var at = _breath.transform.TransformPoint(local);
            at.z = transform.position.z - 0.004f;
            FoeFx.Lava(at, LavaColor, new Vector2(side * Random.Range(0.05f, 0.7f), Random.Range(-0.4f, 0.5f)), size);
        }
    }

    // The fire from its mouth, turned from the art's green to fire.
    void MakeBreath()
    {
        var go = new GameObject("BREATH");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        _breath = go.AddComponent<SpriteRenderer>();
        _breath.sortingLayerID = _sr.sortingLayerID;
        _breath.sortingOrder = _sr.sortingOrder + 2;
        if (RoadEnemy.HueMaterial)
        {
            var fire = new Material(RoadEnemy.HueMaterial);
            fire.SetFloat("_Hue", Mathf.Repeat((look.breathHue != 0f ? look.breathHue : boss.breathHueTurn) / 360f, 1f));
            fire.SetFloat("_Saturation", 1.4f);
            fire.SetFloat("_Brightness", 1.35f);
            _breath.sharedMaterial = fire;
        }
        if (look.breathGlow.a > 0f) { var neon = SpriteOutline.Add(_breath, Color.clear, 0.7f, look.breathGlow); if (neon) { neon.full = true; neon.reach = 0.12f; } }   // (its fire's neon glow)
    }

    // ---- berserk: a red glow round it, pulsing ----
    void MakeAura()
    {
        var go = new GameObject("BERSERK");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        _aura = go.AddComponent<SpriteRenderer>();
        _aura.sprite = FoeFx.GlowSprite;
        _aura.sortingLayerID = _sr.sortingLayerID;
        _aura.sortingOrder = _sr.sortingOrder - 1;
        Aura();
    }

    void Aura()
    {
        var b = RoadFoe.Drawn(_sr);
        var world = transform.lossyScale;
        _aura.transform.position = new Vector3(b.center.x, b.center.y, transform.position.z + 0.002f);
        float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 6f);
        _aura.transform.localScale = new Vector3(b.size.x * 1.7f * pulse / Mathf.Max(1e-4f, Mathf.Abs(world.x)), b.size.y * 1.5f * pulse / Mathf.Max(1e-4f, world.y), 1f) * (1f / FoeFx.GlowSprite.bounds.size.x);
        var c = boss.berserkGlow;
        _aura.color = new Color(c.r, c.g, c.b, c.a * pulse);
    }

    // ---- the magic shield: a glowing bubble round it ----
    float _shieldFlash, _shieldWobble = -10f;
    int _shieldCrackSound;
    static Sprite s_bubble;
    SpriteRenderer _cracks;
    Texture2D _crackTex;

    // A crack spreading across the bubble from where it was struck (`from`: -1..1 across its face):
    // a jagged line in from there, forking - more of them the more blows it has taken.
    void Crack(Vector2 from)
    {
        const int n = 64;
        if (!_cracks)
        {
            _crackTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            _crackTex.SetPixels32(new Color32[n * n]);
            var go = new GameObject("CRACKS");
            go.layer = gameObject.layer;
            go.transform.SetParent(_shield.transform, false);
            _cracks = go.AddComponent<SpriteRenderer>();
            _cracks.sprite = Sprite.Create(_crackTex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            _cracks.sortingLayerID = _sr.sortingLayerID;
            _cracks.sortingOrder = _shield.sortingOrder + 1;
        }
        var start = new Vector2((from.x * 0.5f + 0.5f) * n, (Mathf.Clamp(from.y, -0.8f, 0.8f) * 0.5f + 0.5f) * n);
        var heading = (Vector2)(Quaternion.Euler(0f, 0f, Random.Range(-50f, 50f)) * (new Vector2(n / 2f, n / 2f) - start).normalized);
        Line(start, heading, 6 + Random.Range(0, 3), 2);
        _crackTex.Apply();
    }

    void Line(Vector2 p, Vector2 heading, int steps, int forks)
    {
        const int n = 64;
        for (int i = 0; i < steps; i++)
        {
            heading = (Vector2)(Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f)) * heading);
            var q = p + heading * Random.Range(3f, 6f);
            for (float t = 0f; t <= 1f; t += 0.1f)
            {
                var d = Vector2.Lerp(p, q, t);
                int x = (int)d.x, y = (int)d.y;
                if (x < 0 || y < 0 || x >= n || y >= n) return;
                if (new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).magnitude > n * 0.47f) continue;   // (on the bubble only)
                _crackTex.SetPixel(x, y, Color.white);
                if (x + 1 < n) _crackTex.SetPixel(x + 1, y, Color.white);   // (two pixels thick: it shows on a big orb)
            }
            if (forks > 0 && Random.value < 0.35f) Line(q, (Vector2)(Quaternion.Euler(0f, 0f, Random.value < 0.5f ? 40f : -40f) * heading), steps - i - 1, forks - 1);
            p = q;
        }
    }

    void MakeShield()
    {
        if (!s_bubble)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                    // A magic orb: a bright rim, filling stronger toward its edge, and a shine at its upper left.
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(d - 0.93f) / 0.07f);
                    float fill = d < 0.93f ? 0.14f + 0.36f * d * d * d : 0f;
                    float shine = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.33f, n * 0.7f)) / (n * 0.12f));
                    float a = Mathf.Clamp01(Mathf.Max(rim, fill) + shine * shine * 0.7f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply();
            s_bubble = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
        var go = new GameObject("SHIELD");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        _shield = go.AddComponent<SpriteRenderer>();
        _shield.sprite = s_bubble;
        _shield.sortingLayerID = _sr.sortingLayerID;
        _shield.sortingOrder = _sr.sortingOrder + 1;
        ShieldLook();
    }

    void ShieldLook()
    {
        var b = RoadFoe.Drawn(_sr);
        var world = transform.lossyScale;
        float w = Mathf.Max(b.size.x, b.size.y) * 1.08f, h = w;   // (a round orb all round it)
        _shield.transform.position = new Vector3(b.center.x, b.center.y, transform.position.z - 0.002f);
        // Struck: it wobbles, squashing and springing back.
        float since = Time.time - _shieldWobble;
        float wob = since < 0.4f ? Mathf.Sin(since * 40f) * 0.08f * (1f - since / 0.4f) : 0f;
        _shield.transform.localScale = new Vector3(w * (1f - wob) / Mathf.Max(1e-4f, Mathf.Abs(world.x)), h * (1f + wob) / Mathf.Max(1e-4f, world.y), 1f);
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
        var c = ShieldColor;
        float a = Time.time < _shieldFlash ? 1f : c.a * pulse * (0.4f + 0.6f * _shieldLeft / Mathf.Max(1f, boss.shieldHits));
        _shield.color = Time.time < _shieldFlash ? new Color(1f, 1f, 1f, a) : new Color(c.r, c.g, c.b, a);
        if (_cracks) _cracks.color = new Color(Mathf.Lerp(c.r, 1f, 0.6f), Mathf.Lerp(c.g, 1f, 0.6f), Mathf.Lerp(c.b, 1f, 0.6f), Time.time < _shieldFlash ? 1f : 0.85f);
    }

    void BreakShield()
    {
        var at = RoadFoe.Drawn(_sr).center;
        FoeFx.Blood(at, ShieldColor, 22);
        FoeFx.ShieldHit(at, ShieldColor, Vector2.up, 28);   // (shattering)
        // Shattering like glass: shards of it all round it, flung out and raining down onto the road, glinting.
        var round = _shield ? _shield.bounds : RoadFoe.Drawn(_sr);
        FoeFx.Glass(new Vector3(round.center.x, round.center.y, round.center.z - 0.01f), ShieldColor, 34, Vector2.zero, 1f, round.extents.x * 0.8f);
        if (boss.shieldBreakSound) PitAudio.PlayClip(boss.shieldBreakSound, 1f);
        else if (boss.shieldHitSound) PitAudio.PlayClip(boss.shieldHitSound, 0.75f);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Heavy);
        if (_shield) Destroy(_shield.gameObject);
    }
}

// ARcade: a boss's fireball: flies along the road at her, animating; hits
// her once (jump it), then bursts. Kept on the road as it scrolls.
public class BossShot : MonoBehaviour
{
    public Sprite[] frames;
    public float velocity = 1.5f;
    public int damage = 15;
    public float fps = 12f;
    SpriteRenderer _sr;
    float _start, _lastRoad;

    void Start()
    {
        _sr = GetComponent<SpriteRenderer>();
        _start = Time.time;
        _lastRoad = EndlessKnightDirector.Player ? EndlessKnightDirector.Player.AccumulatedFrames * EndlessKnightDirector.RoadUnitsPerFrame : 0f;
        _sr.flipX = velocity < 0f;
    }

    void Update()
    {
        if (!EndlessKnightDirector.Player) { Destroy(gameObject); return; }
        float road = EndlessKnightDirector.Player.AccumulatedFrames * EndlessKnightDirector.RoadUnitsPerFrame;
        transform.position += Vector3.left * (road - _lastRoad) + Vector3.right * velocity * Time.deltaTime;
        _lastRoad = road;
        if (frames != null && frames.Length > 0) _sr.sprite = frames[(int)((Time.time - _start) * fps) % frames.Length];
        var me = RoadFoe.Drawn(_sr);
        var her = RoadFoe.PlayerBody();
        me.Expand(new Vector3(-me.size.x * 0.4f, -me.size.y * 0.4f, 0f));
        if (me.Intersects(new Bounds(her.center, new Vector3(her.size.x, her.size.y, 100f))))
        {
            RoadFoe.HurtPlayer(damage, 0.45f, 0.4f, transform.position.x);
            FoeFx.Blood(me.center, new Color(1f, 0.5f, 0.15f, 1f), 10);
            Destroy(gameObject);
            return;
        }
        if (Time.time - _start > 6f) Destroy(gameObject);
    }
}

// ARcade: one of the necromancer's shotgun of magic shots: a glowing pixel orb in its colour with a
// neon bloom round it, flying straight (kept on the road as it scrolls), leaving a trail of sparkles;
// it bursts on her (hurting her) or on the ground (where she can jump over it), or fades out.
public class MagicShot : MonoBehaviour
{
    public Vector2 velocity;
    public int damage = 8;
    public Color color = Color.green;
    public float size = 0.5f;
    public AudioClip launchSound, impactSound;
    public float launchDelay;

    SpriteRenderer _orb, _glow;
    bool _launched;

    // Its burst - where it hit her or the ground: a small burst of its magic, a ring of sparkles, its sound.
    void Impact(Vector3 at)
    {
        FoeFx.MagicBurst(at, color, 0.6f);
        for (int i = 0; i < 6; i++) FoeFx.Sparkle(at + (Vector3)Random.insideUnitCircle * RoadFoe.Pixel * 4f, i % 2 == 0 ? Color.white : color, 1.2f);
        if (impactSound) PitAudio.PlayClip(impactSound, 0.4f);
    }
    float _start, _lastRoad, _nextTrail;
    static Sprite s_orb;

    static Sprite Orb()
    {
        if (s_orb) return s_orb;
        string[] rows = { ".LLL.", "LWWWL", "LWWWL", "LWWWL", ".LLL." };
        var tex = new Texture2D(5, 5, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[25];
        for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
            {
                char c = rows[4 - y][x];
                px[y * 5 + x] = c == 'W' ? new Color32(255, 255, 255, 255) : c == 'L' ? new Color32(200, 200, 200, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px); tex.Apply();
        return s_orb = Sprite.Create(tex, new Rect(0, 0, 5, 5), new Vector2(0.5f, 0.5f), 1f);
    }

    void Start()
    {
        _start = Time.time;
        _lastRoad = EndlessKnightDirector.Player ? EndlessKnightDirector.Player.AccumulatedFrames * EndlessKnightDirector.RoadUnitsPerFrame : 0f;
        float px = RoadFoe.Pixel * Mathf.Max(0.5f, size * 2f);   // (its pixels: bigger with a bigger necromancer)
        _orb = new GameObject("ORB").AddComponent<SpriteRenderer>();
        _orb.gameObject.layer = gameObject.layer;
        _orb.transform.SetParent(transform, false);
        _orb.transform.localScale = Vector3.one * px;
        _orb.sprite = Orb();
        _orb.color = Color.Lerp(color, Color.white, 0.55f);
        _orb.sortingLayerName = ChestGem.KnightLayer;
        _orb.sortingOrder = RoadBoss.OverKnightOrder + 11;
        _glow = new GameObject("BLOOM").AddComponent<SpriteRenderer>();
        _glow.gameObject.layer = gameObject.layer;
        _glow.transform.SetParent(transform, false);
        _glow.transform.localScale = Vector3.one * px * 2.2f;
        _glow.sprite = FoeFx.GlowSprite;
        _glow.color = new Color(color.r, color.g, color.b, 0.85f);
        _glow.sortingLayerName = ChestGem.KnightLayer;
        _glow.sortingOrder = RoadBoss.OverKnightOrder + 10;
    }

    void Update()
    {
        if (!EndlessKnightDirector.Player) { Destroy(gameObject); return; }
        float road = EndlessKnightDirector.Player.AccumulatedFrames * EndlessKnightDirector.RoadUnitsPerFrame;
        transform.position += Vector3.left * (road - _lastRoad) + (Vector3)(velocity * Time.deltaTime);
        _lastRoad = road;
        if (!_launched && Time.time - _start >= launchDelay) { _launched = true; if (launchSound) PitAudio.PlayClip(launchSound, 0.35f); }
        float pulse = 0.8f + 0.2f * Mathf.Sin((Time.time - _start) * 30f);
        if (_glow) _glow.color = new Color(color.r, color.g, color.b, 0.85f * pulse);
        if (Time.time >= _nextTrail)
        {
            _nextTrail = Time.time + 0.035f;
            FoeFx.Sparkle(transform.position, Random.value < 0.4f ? Color.white : color, Mathf.Max(0.6f, size * 1.5f));
        }
        // On her: it bursts, hurting her.
        var her = RoadFoe.PlayerBody();
        float r = RoadFoe.Pixel * Mathf.Max(0.5f, size * 2f) * 2f;
        var p = transform.position;
        if (p.x + r > her.min.x && p.x - r < her.max.x && p.y + r > her.min.y && p.y - r < her.max.y)
        {
            RoadFoe.HurtPlayer(damage, 0.35f, 0.3f, transform.position.x);
            Impact(p);
            Destroy(gameObject);
            return;
        }
        // On the ground (she can jump over where they come down), or gone.
        if (p.y <= SpikeTrap.PlayerFeetY) { Impact(new Vector3(p.x, SpikeTrap.PlayerFeetY, p.z)); Destroy(gameObject); return; }
        if (Time.time - _start > 4f) Destroy(gameObject);
    }
}
