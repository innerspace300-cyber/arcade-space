using System.Collections.Generic;
using UnityEngine;

// ARcade: a small foe on the road in place of level 1's bomb guys (its look
// and ways from LevelFoes.Foe) - one blow from the knight finishes it (or
// `hits` blows: each flashes it white and splashes its blood). They keep
// `padding` apart on the road, the ones behind waiting their turn.
//   Swooping Leaper (level 1's birbs): flies up first, then dives down at her
//   at an angle, on past her to the road.
//   Leaper (level 3's frogs): hops toward her and, near enough, leaps at her
//   in an arc; landing on her (its body into hers) hurts and knocks her back,
//   then it hops back off a little and comes again.
//   Charger (level 2's goats): ambles toward her and, near enough, lowers
//   its head and charges fast - running into her hurts and knocks her back -
//   on past her a little, then turns and comes again.
//   Shambler (level 4's revenants and skeletons, the necromancer's zombies):
//   shuffles slowly at her and, within reach, swings - slowly, so there's
//   time to get out of the way; the blow lands on its hit frame.
// Dying, it plays its death frames and bursts (FoeFx): bones, glowing blood,
// and for a revenant a blast of its own that can hurt her too.
// Spawned and moved with the road by EndlessKnightDirector (through
// CollectableSpawnSystem), or summoned by a boss (selfAnchored: it keeps
// itself on the road).
public class RoadEnemy : MonoBehaviour
{
    public LevelFoes.Foe foe;
    /// Summoned (not moved with the road by the spawn system): keeps itself on the road.
    public bool selfAnchored;
    /// Comes up out of the ground first (behind the road's surface), the necromancer's summoning.
    public bool rise;
    /// Drawn with this material (its colours turned: the toad's brood in the toad's own).
    public Material recolor;
    const int OverKnightOrder = 210;

    /// Thrown out (the toad's babies): flies on this, falls, lands, then is itself.
    public void Launch(Vector2 velocity) { _flyVelocity = velocity; _flying = true; }
    Vector2 _flyVelocity;
    bool _flying;
    /// The look the director's spawner copies (it draws nothing and does nothing); copies of it aren't.
    [System.NonSerialized] public bool template;
    /// For the aqua blast: the bomb guy's explosion and the hue material (EndlessKnightDirector).
    public static BombGuy BlastFrom;
    public static Material HueMaterial;

    public static readonly List<RoadEnemy> All = new List<RoadEnemy>();

    SpriteRenderer _sr;
    float _lastRoad;
    float _leapStart = -1f, _fromX, _toX, _nextAttack, _attackStart = -1f, _deadAt = -1f, _hurtUntil;
    bool _hitDealt, _facingRight;
    float _backOffUntil, _riseStart = -1f, _nextIdleSound;
    int _order;
    const float RiseTime = 1.1f;
    Transform _ground;
    float _nextDirt;
    static Sprite s_square;

    // A mask over everything above the ground's line here: drawn only above it while it climbs out.
    void MakeGroundLine()
    {
        if (!s_square) s_square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 4f);
        var go = new GameObject("GROUND LINE");
        go.layer = gameObject.layer;
        var mask = go.AddComponent<SpriteMask>();
        mask.sprite = s_square;
        mask.isCustomRangeActive = true;
        mask.frontSortingLayerID = mask.backSortingLayerID = _sr.sortingLayerID;
        mask.frontSortingOrder = _sr.sortingOrder;
        mask.backSortingOrder = _sr.sortingOrder - 1;
        go.transform.localScale = new Vector3(4f, 4f, 1f);
        go.transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z);
        _ground = go.transform;
    }

    void EndRise()
    {
        _sr.maskInteraction = SpriteMaskInteraction.None;
        if (_ground) Destroy(_ground.gameObject);
    }

    void OnDestroy()
    {
        All.Remove(this);
        if (_ground) Destroy(_ground.gameObject);
    }

    // ---- hit: a white flash (its shape in white over it, a moment) ----
    static Material s_solid;
    static readonly int ColorId = Shader.PropertyToID("_Color");
    SpriteRenderer _flash;
    MaterialPropertyBlock _flashBlock;
    float _flashUntil, _safeUntil;
    int _hitsLeft;

    // Its shape in one flat colour (per renderer): the hit flash and the outline.
    static bool Solid()
    {
        if (!s_solid)
        {
            var shader = Shader.Find("SpatialEmulator/SpriteSolid");
            if (shader) s_solid = new Material(shader) { name = "Foe Solid" };
        }
        return s_solid;
    }

    void Flash(float seconds = 0.12f) => Flash(new Color(1f, 1f, 1f, 0.9f), seconds);

    void Flash(Color colour, float seconds)
    {
        if (!_flash)
        {
            if (!Solid()) return;
            var go = new GameObject("FLASH");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            _flash = go.AddComponent<SpriteRenderer>();
            _flash.sharedMaterial = s_solid;
        }
        if (_flashBlock == null) _flashBlock = new MaterialPropertyBlock();
        _flashBlock.SetColor(ColorId, colour);
        _flash.SetPropertyBlock(_flashBlock);
        _flashUntil = Time.time + seconds;
    }

    // ---- glowing eyes: its eye-glow sheet's frame for the frame it's on, over it ----
    SpriteRenderer _eyes;
    readonly Dictionary<string, Sprite> _eyeFrames = new Dictionary<string, Sprite>();
    static string Cell(string name) { int i = name.LastIndexOf("_r"); return i >= 0 ? name.Substring(i) : name; }

    void MakeEyes()
    {
        foreach (var s in foe.eyeGlow) if (s) _eyeFrames[Cell(s.name)] = s;
        var go = new GameObject("EYES");
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.0008f);
        _eyes = go.AddComponent<SpriteRenderer>();
        if (foe.headFire.a > 0f) MakeFire();
    }

    // ---- fire on its head (foe.headFire): pixel embers rising off the rim of its skull - its skull and eye left to be
    // seen - floating up, swaying, getting smaller and fading away. Drawn over its sprite ----
    const int FirePixels = 14;
    const float SkullRim = 0.058f, SkullBack = 0.02f;   // (its art's 100ths: its skull's radius, and its middle behind its eye)
    static Sprite s_firePixel;
    SpriteRenderer[] _fire;
    float[] _fireBorn, _fireLife, _fireAngle, _fireRise, _firePhase;
    readonly Dictionary<Sprite, Vector2> _eyeSpots = new Dictionary<Sprite, Vector2>();

    void MakeFire()
    {
        if (!s_firePixel)
        {
            var t = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            t.SetPixel(0, 0, Color.white);
            t.Apply();
            s_firePixel = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
        _fire = new SpriteRenderer[FirePixels];
        _fireBorn = new float[FirePixels]; _fireLife = new float[FirePixels]; _fireAngle = new float[FirePixels];
        _fireRise = new float[FirePixels]; _firePhase = new float[FirePixels];
        for (int i = 0; i < FirePixels; i++)
        {
            var go = new GameObject("HEAD FIRE");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            _fire[i] = go.AddComponent<SpriteRenderer>();
            _fire[i].sprite = s_firePixel;
            _fireBorn[i] = Time.time - Random.value;   // (staggered)
        }
    }

    // Its head this frame: the middle of the eye on its eye sheet's frame (the sprite's outline hugs the eye).
    Vector2 EyeSpot(Sprite eye)
    {
        if (_eyeSpots.TryGetValue(eye, out var spot)) return spot;
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        foreach (var v in eye.vertices) { min = Vector2.Min(min, v); max = Vector2.Max(max, v); }
        return _eyeSpots[eye] = (min + max) * 0.5f;
    }

    void HeadFire()
    {
        if (_fire == null) return;
        bool on = _eyes.enabled && _eyes.sprite;
        for (int i = 0; i < FirePixels; i++) _fire[i].enabled = on;
        if (!on) return;
        var spot = EyeSpot(_eyes.sprite);
        float way = _sr.flipX ? -1f : 1f, px = RoadFoe.Pixel, lossy = Mathf.Abs(transform.lossyScale.x);
        var head = transform.TransformPoint(new Vector3((spot.x - SkullBack) * way, spot.y, -0.0012f));
        var hot = Color.Lerp(foe.headFire, Color.white, 0.7f);
        var deep = new Color(foe.headFire.r * 0.4f, foe.headFire.g * 0.5f, foe.headFire.b * 0.55f);
        for (int i = 0; i < FirePixels; i++)
        {
            float age = Time.time - _fireBorn[i];
            if (age >= _fireLife[i])
            {
                _fireBorn[i] = Time.time; age = 0f;
                _fireLife[i] = Random.Range(0.5f, 1f);
                _fireAngle[i] = Random.Range(-25f, 205f) * Mathf.Deg2Rad;   // (round the sides and top of its skull)
                _fireRise[i] = Random.Range(16f, 32f);
                _firePhase[i] = Random.value * 10f;
            }
            float k = age / _fireLife[i];
            var rim = new Vector3(Mathf.Cos(_fireAngle[i]), Mathf.Sin(_fireAngle[i]), 0f) * SkullRim * lossy;
            var at = head + rim + new Vector3(Mathf.Sin(age * 7f + _firePhase[i]) * px * 2f * k, _fireRise[i] * px * k, 0f);
            var ember = _fire[i];
            ember.transform.position = at;
            float side = Mathf.Max(1f, Mathf.Lerp(3.5f, 1f, k)) * px / lossy;   // (smaller as it goes)
            ember.transform.localScale = new Vector3(side, side, 1f);
            var c = k < 0.3f ? Color.Lerp(hot, foe.headFire, k / 0.3f) : Color.Lerp(foe.headFire, deep, (k - 0.3f) / 0.7f);
            c.a = (1f - k * k) * _sr.color.a;   // (fading away)
            ember.color = c;
            ember.sortingLayerID = _sr.sortingLayerID;
            ember.sortingOrder = _sr.sortingOrder + 4;
            ember.maskInteraction = _sr.maskInteraction;
        }
    }

    void Eyes()
    {
        if (!_eyes) return;
        Sprite frame = null;
        bool on = _sr.enabled && _sr.sprite && _eyeFrames.TryGetValue(Cell(_sr.sprite.name), out frame);
        _eyes.enabled = on;
        if (!on) return;
        _eyes.sprite = frame;
        _eyes.flipX = _sr.flipX;
        _eyes.sortingLayerID = _sr.sortingLayerID;
        _eyes.sortingOrder = _sr.sortingOrder + 2;
        _eyes.maskInteraction = _sr.maskInteraction;
        float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f + _phase * 3f);
        _eyes.color = new Color(1f, 1f, 1f, foe.eyeGlowHidden ? 0f : flicker * _sr.color.a);
    }

    float _lifted;   // (drawn liftPixels up, just for the frame's drawing: put back before it moves)

    void LateUpdate()
    {
        if (!template && foe != null && foe.liftPixels > 0f && _lifted == 0f)
        {
            _lifted = foe.liftPixels * RoadFoe.Pixel;
            transform.position += Vector3.up * _lifted;
            var shadow = _sr ? _sr.GetComponent<SpriteShadow>() : null;
            if (shadow) shadow.standLift = _lifted;   // (its reflection stays where it was)
        }
        if (_sr) { Eyes(); HeadFire(); }
        if (!_flash || !_sr) return;
        _flash.sprite = _sr.sprite;
        _flash.flipX = _sr.flipX;
        _flash.sortingLayerID = _sr.sortingLayerID;
        _flash.sortingOrder = _sr.sortingOrder + 1;
        _flash.maskInteraction = _sr.maskInteraction;
        _flash.enabled = _sr.enabled && Time.time < _flashUntil;
    }

    void Start()
    {
        if (template) return;
        All.Add(this);
        _sr = GetComponent<SpriteRenderer>();
        if (!_sr) _sr = gameObject.AddComponent<SpriteRenderer>();
        if (recolor) _sr.sharedMaterial = recolor;
        // Over the knight - on her layer, past her 150 and her glow, under her attacks (219, hers seen landing on it)
        // and the pickups (ChestGem's 230) - so its blows are seen landing on her, as the Slime Boss's are. (Its outline, glow, eyes and flash
        // go by its order.)
        _sr.sortingLayerName = ChestGem.KnightLayer;
        _sr.sortingOrder = OverKnightOrder;
        if (foe.tint != Color.white && foe.tint.a > 0f) _sr.color = foe.tint;
        var outline = SpriteOutline.Add(_sr, foe.outline, foe.burning ? 0.7f : foe.outlineHalo.a > 0f ? 0.9f : 0.4f, foe.outlineHalo);
        if (outline) { outline.fire = foe.burning; outline.reach = foe.auraReach; }
        if (foe.bloom.a > 0f)
        {
            // A neon bloom along its edges (its shape, glowing out a few pixels).
            var glow = outline ? outline : SpriteOutline.Add(_sr, Color.clear, 0f, foe.bloom);
            if (glow) { glow.bloom = foe.bloom; glow.bloomPixels = foe.bloomPixels; }
        }   // (set before its Start: its halo's sprite)
        if (foe.eyeGlow != null && foe.eyeGlow.Length > 0) MakeEyes();
        if (foe.sparks.a > 0f) { var sparks = GateMotes.Add(_sr, GateMotes.Kind.Sparks, foe.sparks); sparks.fromBody = true; sparks.count = 20; }
        if (foe.smoke.a > 0f) { var smoke = GateMotes.Add(_sr, GateMotes.Kind.Smoke, foe.smoke); smoke.fromBody = true; smoke.count = 6; }
        var look = foe.walk != null && foe.walk.Length > 0 ? foe.walk : foe.idle;
        if (look != null && look.Length > 0) Show(look[0]);
        if (!_flying) transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, SpikeTrap.LaneZ - 0.005f);
        transform.localScale = Vector3.one * (RoadFoe.Pixel / 0.01f * foe.scale * Random.Range(Mathf.Min(foe.scaleJitter.x, foe.scaleJitter.y), Mathf.Max(foe.scaleJitter.x, foe.scaleJitter.y)));
        _lastRoad = Road;
        _nextAttack = Time.time + Random.Range(0.3f, 1f);
        _phase = Random.value * 3f;
        _nextIdleSound = Time.time + Random.Range(1f, 4f);
        _hitsLeft = Mathf.Max(1, foe.hits);
        if (!rise && !_flying) InLine();
        if (rise)
        {
            // Climbing up out of the ground where it stands: nothing of it below the ground's line
            // (a mask above it), the earth thrown up round it, moaning.
            _riseStart = Time.time;
            _order = _sr.sortingOrder;
            MakeGroundLine();
            _sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            FoeFx.Dirt(new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z - 0.01f), 14, RoadFoe.Drawn(_sr).extents.x * 0.6f);
            if (foe.riseSounds != null && foe.riseSounds.Length > 0) PitAudio.PlayClip(foe.riseSounds[Random.Range(0, foe.riseSounds.Length)], 0.7f);
            return;
        }
        CastShadow();
    }

    void CastShadow()
    {
        var shadow = SpriteShadow.Cast(_sr);   // its shadow on the road (and the level's light on it)
        if (shadow) shadow.raisePixels = foe.shadowRaise;
    }

    static float Road => EndlessKnightDirector.Player ? EndlessKnightDirector.Player.AccumulatedFrames * EndlessKnightDirector.RoadUnitsPerFrame : 0f;
    float PlayerX => EndlessKnightDirector.Player ? EndlessKnightDirector.Player.transform.position.x : transform.position.x;

    /// The knight's blow lands on `area`: every one it reaches is hit (a heavy one - the Holy Slash - finishes it).
    public static void HitAll(Bounds area, bool heavy = false)
    {
        foreach (var e in All.ToArray())
        {
            if (!heavy) continue;   // (her sword's blow lands when its slash comes round in front: SwordLanded, in Update)
            if (e && e._deadAt < 0f && e._sr && Overlap(e.Struck(), area)) e.Hit(999, -1);
        }
    }

    // Her swing's slash has come round in front of her (not still wound up behind) - or it's the Holy Slash.
    static bool SwordLanded
    {
        get
        {
            var p = EndlessKnightDirector.Player;
            if (!p) return false;
            float age = p.SwingAge;
            return age < 0f || age >= PlayerScriptARIANAClips.SwordLands;
        }
    }

    static int Swing => EndlessKnightDirector.Player ? EndlessKnightDirector.Player.SwingCount : -1;
    int _lastSwing = -1;

    /// Struck (`blows` of its hits) - by her sword's swing number `swing` (one blow a swing, however long
    /// the blow stays live; -1: not her sword): dies at its last, else flashes white, bleeds, and is knocked back.
    public void Hit(int blows = 1, int swing = -1)
    {
        if (PlayerBuffs.Big && blows > 0) blows = 999;   // (big on viking food: one blow does it)
        if (_deadAt >= 0f) return;
        if (swing >= 0) { if (swing == _lastSwing) return; _lastSwing = swing; }
        else if (Time.time < _safeUntil) return;
        _safeUntil = Time.time + 0.15f;
        if (foe.hitSound) Sound(foe.hitSound, foe.hitVolume);   // (every blow lands with it, the last too: you know you hit)
        if (foe.impactSound) PitAudio.PlayClip(foe.impactSound, foe.hitVolume * foe.impactVolume);
        if (foe.hitCries != null && foe.hitCries.Length > 0) PitAudio.PlayClip(foe.hitCries[Random.Range(0, foe.hitCries.Length)], 1f);
        Impact();
        _hitsLeft -= blows;
        Flash(swing < 0 && blows >= 999 ? HeavyFlash : 0.12f);   // (the last blow too: a Holy Slash's longer, to show through the slash over it)
        if (_hitsLeft <= 0) { Die(); return; }
        var at = RoadFoe.Drawn(_sr).center;
        if (foe.blood.a > 0f) FoeFx.Gore(at, foe.blood, 0.6f, Mathf.Max(3, foe.hitBlood), follow: transform);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Light);
        if (_riseStart < 0f && !_flying)
        {
            // Knocked back: slid away from her along the ground, quick then slowing - out of a leap too,
            // dropped flat to the road first - and no attack for a while after (no hitting straight back).
            if (_leapStart >= 0f)
            {
                _leapStart = -1f; _diving = false;
                transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z);
            }
            _knockDir = transform.position.x > PlayerX ? 1f : -1f;
            _knockLeft = foe.knockBack > 0f ? foe.knockBack : 0.12f;
            _attackStart = -1f; _chargeStart = -1f;
            _nextAttack = Mathf.Max(_nextAttack, Time.time + 1.2f);
            _backOffUntil = Mathf.Max(_backOffUntil, Time.time + 0.5f);
        }
    }

    float _bumpSafe;

    // Come in off the right of the picture: behind the last of its group still coming in (or anyone it would
    // land on), a little gap between - so they walk on one after the other, not all in a heap pushed apart.
    void InLine()
    {
        var cam = SpatialEmulator.Demo.PitDemoGame.ActorsCamera;
        if (!cam || !cam.orthographic) return;
        float edge = cam.transform.position.x + cam.orthographicSize * cam.aspect;
        if (transform.position.x < edge - 0.05f) return;   // (placed on the road deliberately: left there)
        float me = RoadFoe.Drawn(_sr).extents.x;
        for (int pass = 0; pass < 3; pass++)
        {
            bool moved = false;
            foreach (var o in All)
            {
                if (!o || o == this || o.template || !o._sr || o._deadAt >= 0f || o._riseStart >= 0f || o._flying) continue;
                float need = me + RoadFoe.Drawn(o._sr).extents.x + Mathf.Max(foe.padding, o.foe.padding) + Random.Range(0.15f, 0.35f);
                float x = transform.position.x, ox = o.transform.position.x;
                if (Mathf.Abs(ox - x) < need) { transform.position = new Vector3(ox + need, transform.position.y, transform.position.z); moved = true; }
            }
            if (!moved) break;
        }
    }

    // Knocked into her (a frog): one of its hits gone - at its last it bursts, else it flashes, bleeds and
    // slides back off her along the ground.
    void Bumped()
    {
        _bumpSafe = Time.time + 0.6f;
        _hitsLeft--;
        if (_hitsLeft <= 0) { Die(); return; }
        if (foe.hitSound) Sound(foe.hitSound, foe.hitVolume);
        // (no blood splash sound: that's for her blows)
        if (foe.hitCries != null && foe.hitCries.Length > 0) PitAudio.PlayClip(foe.hitCries[Random.Range(0, foe.hitCries.Length)], 1f);
        Impact();
        Flash();
        if (foe.blood.a > 0f && _sr) FoeFx.Gore(RoadFoe.Drawn(_sr).center, foe.blood, 0.6f, Mathf.Max(3, foe.hitBlood), follow: transform);
        if (_leapStart >= 0f)
        {
            _leapStart = -1f; _diving = false;
            transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z);
        }
        _knockDir = transform.position.x > PlayerX ? 1f : -1f;
        _knockLeft = foe.knockBack > 0f ? foe.knockBack : 0.12f;
        _nextAttack = Mathf.Max(_nextAttack, Time.time + 1.2f);
        _backOffUntil = Mathf.Max(_backOffUntil, Time.time + 0.5f);
    }

    // Her blow landing on it: neon sparks in its colour off its side toward her, flung away from her
    // (and bones knocked off a skeleton, feathers off a birb).
    void Impact()
    {
        if (!_sr) return;
        var body = RoadFoe.Drawn(_sr);
        float away = transform.position.x >= PlayerX ? 1f : -1f;
        var at = new Vector3(body.center.x - away * body.extents.x * 0.3f, body.center.y, body.center.z - 0.02f);
        var color = foe.impactColor.a > 0f ? foe.impactColor : foe.blood.a > 0f ? foe.blood : foe.outline.a > 0f ? foe.outline : new Color(1f, 0.9f, 0.6f);
        FoeFx.Impact(at, color, new Vector2(away, 0.45f), follow: transform, over: _sr);   // (over it: it's over the knight now)
        FoeFx.Over(_sr, () =>   // (its goo, bones and feathers over it too)
        {
            if (foe.impactGore && foe.blood.a > 0f) FoeFx.Gore(at, foe.blood, 0.75f, 7);
            if (foe.hitBones > 0) FoeFx.Bones(at, foe.hitBones, false, foe.boneSpread * 0.8f);
            if (foe.feathers) FoeFx.Feathers(at, foe.featherColor, 5);
        });
    }

    // Another foe just ahead of it, in its way: never now - foes walk on through each other.
    // Level 4's foes keep clear of its hell hands: none walks up to one, or on past it.
    public const float HandKeepClear = 2.5f;   // (world units)
    bool Blocked(float dir)
    {
        // (a swarm's rats: none walks up into the back of the one ahead)
        if (foe.swarm > 0)
        {
            float gap = SwarmGap;
            foreach (var o in All)
            {
                if (!o || o == this || o.template || o._deadAt >= 0f || o._flying || o.foe == null || o.foe.swarm <= 0) continue;
                float ahead = (o.transform.position.x - transform.position.x) * dir;
                if (ahead > 0f && ahead < gap) return true;
            }
        }
        if (SpatialEmulator.Demo.PitDemoGame.Level != 4) return false;
        foreach (var hand in SpikeTrap.Tentacles)
        {
            if (!hand) continue;
            float ahead = (hand.transform.position.x - transform.position.x) * dir;
            if (ahead > 0f && ahead < HandKeepClear) return true;
        }
        return false;
    }

    static bool Overlap(Bounds a, Bounds b) => !(a.max.x < b.min.x || a.min.x > b.max.x || a.max.y < b.min.y || a.min.y > b.max.y);

    /// Still up and about (not dying, not still rising out of the ground): one the fairy can shoot.
    public bool Targetable => _sr && _deadAt < 0f && _riseStart < 0f;
    public SpriteRenderer Art => _sr;

    /// Hit (the fairy's ice spike): as a blow from her sword.
    public void Kill() => Hit();

    /// Gone harmlessly (the road cleared when a boss dies).
    public void Vanish(bool burst = true)
    {
        if (_deadAt >= 0f && !_fuse) return;
        if (burst) Burst(harmless: true);
        Destroy(gameObject);
    }

    void Update()
    {
        if (_lifted != 0f) { transform.position -= Vector3.up * _lifted; _lifted = 0f; }
        if (template || !_sr || !EndlessKnightDirector.Player) return;
        float road = Road;
        if (selfAnchored) transform.position += Vector3.left * (road - _lastRoad);
        _lastRoad = road;
        if (transform.position.x < PlayerX - 30f) { Destroy(gameObject); return; }

        if (_deadAt >= 0f) { Dying(); return; }

        if (_knockLeft > 0f)
        {
            // Sliding back from her blow (nothing else till it stops).
            float step = Mathf.Min(_knockLeft, Mathf.Max(0.4f, _knockLeft * 12f) * Time.deltaTime);
            transform.position += Vector3.right * _knockDir * step;
            _knockLeft -= step;
            Animate(foe.hurt != null && foe.hurt.Length > 0 ? foe.hurt : foe.idle != null && foe.idle.Length > 0 ? foe.idle : foe.walk, 0f);
            return;
        }

        if (_flying)
        {
            _flyVelocity.y -= 6f * Time.deltaTime;
            transform.position += (Vector3)(_flyVelocity * Time.deltaTime);
            // (facing the way it's flung - never flying in backwards)
            if (Mathf.Abs(_flyVelocity.x) > 0.01f)
            {
                _facingRight = _flyVelocity.x > 0f;
                _sr.flipX = foe.artFacesRight ? !_facingRight : _facingRight;
            }
            Animate(foe.attack != null && foe.attack.Length > 0 ? foe.attack : foe.walk, Time.time);
            if (transform.position.y <= SpikeTrap.PlayerFeetY && _flyVelocity.y < 0f)
            {
                _flying = false;
                transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z);
                _nextAttack = Time.time + Random.Range(0.3f, 0.8f);
            }
            return;
        }

        if (_riseStart >= 0f)
        {
            float k = Mathf.Clamp01((Time.time - _riseStart) / RiseTime);
            float height = RoadFoe.Drawn(_sr).size.y;
            transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY - height * (1f - k * (2f - k)), transform.position.z);
            transform.position += new Vector3(Mathf.Sin(Time.time * 40f) * RoadFoe.Pixel * 0.6f * (1f - k), 0f, 0f);   // (clawing its way out)
            if (_ground) _ground.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z);
            if (k < 0.7f && Time.time >= _nextDirt)
            {
                _nextDirt = Time.time + 0.12f;
                FoeFx.Dirt(new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z - 0.01f), 3, RoadFoe.Drawn(_sr).extents.x * 0.5f);
            }
            Animate(foe.idle != null && foe.idle.Length > 0 ? foe.idle : foe.walk, Time.time);
            if (k >= 1f)
            {
                _riseStart = -1f;
                EndRise();
                transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z);
                CastShadow();
                _nextAttack = Time.time + 0.6f;
            }
            return;
        }

        // Now and then its sound (the frogs' ribbits), while it's near.
        if (foe.idleSounds != null && foe.idleSounds.Length > 0 && Time.time >= _nextIdleSound)
        {
            _nextIdleSound = Time.time + Random.Range(2.5f, 5f);
            if (Mathf.Abs(PlayerX - transform.position.x) < 3f) PitAudio.PlayClip(foe.idleSounds[Random.Range(0, foe.idleSounds.Length)], 0.45f);
        }

        // Into her blow while it's playing (it walks or leaps into her swing): dies.
        if (_dodgeStart < 0f && EndlessKnightDirector.Player.ActiveAttackArea(out var blow) && SwordLanded && InSwordReach() && Overlap(Struck(), blow)) { Hit(1, Swing); if (_deadAt >= 0f) return; }

        // Landed on from above: stomped, whatever it's doing.
        if (Time.time >= _bumpSafe && Touching(foe.touchPixels) && Stomped() && _deadAt >= 0f) return;

        float dx = PlayerX - transform.position.x;
        _facingRight = dx > 0f;
        _sr.flipX = foe.artFacesRight ? !_facingRight : _facingRight;

        if (foe.style == LevelFoes.Style.Leaper) Leaper(dx);
        else if (foe.style == LevelFoes.Style.Charger) Charger(dx);
        else Shambler(dx);
    }

    void Animate(Sprite[] frames, float t, bool loop = true)
    {
        if (frames == null || frames.Length == 0) return;
        int i = (int)(t * foe.fps);
        Show(frames[loop ? i % frames.Length : Mathf.Min(i, frames.Length - 1)]);
    }

    // A frame on it.
    void Show(Sprite frame) => _sr.sprite = frame;

    void Leaper(float dx)
    {
        float px = RoadFoe.Pixel;
        if (_leapStart >= 0f && foe.swoop) { Swoop(); return; }
        if (_leapStart >= 0f && Time.time < _leapStart)
        {
            // Crouched, a moment, before it springs (time for her to swing first).
            Animate(foe.idle != null && foe.idle.Length > 0 ? foe.idle : foe.walk, 0f);
            _toX = PlayerX;
            return;
        }
        if (_leapStart >= 0f)
        {
            // In the air: an arc to where she stood as it leapt.
            float t = Mathf.Min(1f, (Time.time - _leapStart) / foe.leapTime);
            float s = t * t * (3f - 2f * t);
            float x = Mathf.Lerp(_fromX, _toX, s);
            transform.position = new Vector3(x, SpikeTrap.PlayerFeetY + Mathf.Sin(Mathf.PI * t) * foe.leapHeight, transform.position.z);
            Animate(foe.attack, Time.time - _leapStart, false);
            if (!_hitDealt && Touching(foe.touchPixels))
            {
                _hitDealt = true;
                if (HurtHer(0.45f, 0.35f) && foe.burstOnContact) { Bumped(); return; }
            }
            if (t >= 1f)
            {
                _leapStart = -1f;
                _nextAttack = Time.time + foe.attackCooldown;
                _backOffUntil = Time.time + 0.6f;
            }
            return;
        }
        // Hopping: toward her, or back off a little after a leap.
        bool back = Time.time < _backOffUntil;
        float dir = back ? -Mathf.Sign(dx) : Mathf.Sign(dx);
        if (back || !Blocked(dir)) transform.position += Vector3.right * dir * foe.walkSpeed * Time.deltaTime;
        Separate();
        float hop = foe.hopBounce > 0f ? Mathf.Abs(Mathf.Sin((Time.time + _phase) * Mathf.PI * foe.hopRate)) * foe.hopBounce : 0f;   // (a spring in its step)
        transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY + hop, transform.position.z);
        Animate(foe.walk, Time.time + _phase);
        if (!back && Mathf.Abs(dx) < foe.leapDistance && Time.time >= _nextAttack && Time.time >= s_nextLeap && !OneLeaping())
        {
            s_nextLeap = Time.time + 0.45f;   // (one at a time, not all landing on her at once)
            _leapStart = Time.time + (foe.swoop || foe.burstOnContact ? 0f : foe.leapWindup);   // (the frogs just leap)
            if (foe.attackSound) Sound(foe.attackSound, foe.attackVolume);
            _fromX = transform.position.x;
            _toX = PlayerX;
            _hitDealt = false;
            _diving = false;
        }
        if (foe.burstOnContact && Time.time >= _bumpSafe && Touching(foe.touchPixels))
        {
            // Run into, or landed on from above: it hurts her, and takes it as a blow itself (bursting at its last).
            if (HurtHer(0.45f, 0.35f)) Bumped();
            return;
        }
        if (Touching(foe.touchPixels) && Time.time >= _nextAttack)
        {
            HurtHer(0.45f, 0.35f);
            _nextAttack = Time.time + foe.attackCooldown;
            _backOffUntil = Time.time + 0.6f;
        }
    }

    float _phase;   // (its own step in its animation)
    float _knockLeft, _knockDir;

    bool _diving;
    Vector2 _diveDir;

    // The birb's swoop: up into the air, a little back from her; then a dive at her
    // body, at an angle, on past her down to the road.
    void Swoop()
    {
        float t = Time.time - _leapStart;
        float feet = SpikeTrap.PlayerFeetY;
        Animate(foe.attack, t);
        if (!_diving)
        {
            float k = Mathf.Min(1f, t / Mathf.Max(0.05f, foe.swoopRise));
            float up = 1f - (1f - k) * (1f - k);
            float back = Mathf.Sign(_toX - _fromX);
            transform.position = new Vector3(_fromX - back * 0.15f * up, feet + foe.leapHeight * up, transform.position.z);
            if (k >= 1f)
            {
                // At her as she is now: her middle.
                var her = RoadFoe.PlayerBody();
                var aim = new Vector2(PlayerX, Mathf.Lerp(her.min.y, her.center.y, 0.5f));
                _diveDir = (aim - (Vector2)transform.position).normalized;
                if (_diveDir.y > -0.25f) _diveDir = new Vector2(Mathf.Sign(_diveDir.x) * 0.97f, -0.25f).normalized;   // (always downward)
                _diving = true;
            }
        }
        else
        {
            transform.position += (Vector3)(_diveDir * foe.diveSpeed * Time.deltaTime);
            _sr.flipX = foe.artFacesRight ? _diveDir.x < 0f : _diveDir.x > 0f;
        }
        if (!_hitDealt && Touching(foe.touchPixels))
        {
            _hitDealt = true;
            HurtHer(0.45f, 0.35f);
        }
        if ((_diving && transform.position.y <= feet) || t > foe.swoopRise + 2f)
        {
            transform.position = new Vector3(transform.position.x, feet, transform.position.z);
            _leapStart = -1f;
            _diving = false;
            _nextAttack = Time.time + foe.attackCooldown;
            _backOffUntil = Time.time + 0.4f;
        }
    }

    float _chargeStart = -1f, _chargeDir, _chargeEndX;

    void Charger(float dx)
    {
        if (_chargeStart >= 0f)
        {
            float t = Time.time - _chargeStart;
            if (t < 0.25f)
            {
                // Head down, a moment: then off.
                Animate(foe.idle, 0f);
                _sr.flipX = foe.artFacesRight ? _chargeDir < 0f : _chargeDir > 0f;
                return;
            }
            if (Blocked(_chargeDir)) { _chargeStart = -1f; _nextAttack = Time.time + foe.attackCooldown * 0.5f; return; }   // (not into another)
            transform.position += Vector3.right * _chargeDir * foe.chargeSpeed * Time.deltaTime;
            _sr.flipX = foe.artFacesRight ? _chargeDir < 0f : _chargeDir > 0f;
            Animate(foe.attack, t);
            if (!_hitDealt && Touching(foe.touchPixels)) { _hitDealt = true; HurtHer(0.5f, 0.5f); }
            bool past = _chargeDir > 0f ? transform.position.x >= _chargeEndX : transform.position.x <= _chargeEndX;
            if (past || t > 3f) { _chargeStart = -1f; _nextAttack = Time.time + foe.attackCooldown; }
            return;
        }
        Separate();
        bool blocked = Blocked(Mathf.Sign(dx));
        if (Mathf.Abs(dx) > 0.15f && !blocked) transform.position += Vector3.right * Mathf.Sign(dx) * foe.walkSpeed * Time.deltaTime;
        if (blocked) Animate(foe.idle != null && foe.idle.Length > 0 ? foe.idle : foe.walk, Time.time + _phase);
        else Animate(foe.walk, Time.time + _phase);
        if (!blocked && Mathf.Abs(dx) < foe.chargeDistance && Time.time >= _nextAttack)
        {
            _chargeStart = Time.time;
            if (foe.attackSound) Sound(foe.attackSound, foe.attackVolume);
            _chargeDir = Mathf.Sign(dx);
            _chargeEndX = PlayerX + _chargeDir * foe.chargeOvershoot;
            _hitDealt = false;
        }
    }

    bool _walking;
    float _stride, _dodgeStart = -1f, _dodgeDir, _dodgeDone, _dodgeHop;
    int _seenSwing = -1;

    // Now and then, as her swing starts near it, it hops back out of her reach (untouched in the air).
    bool Dodging(float dx)
    {
        var player = EndlessKnightDirector.Player;
        int swing = player ? player.SwingCount : -1;
        if (_dodgeStart < 0f && swing != _seenSwing)
        {
            bool fresh = _seenSwing >= 0;
            _seenSwing = swing;
            if (fresh && foe.dodgeChance > 0f && Mathf.Abs(dx) < foe.reach * 1.8f && Random.value < foe.dodgeChance)
            {
                _dodgeStart = Time.time;
                _dodgeDir = -Mathf.Sign(dx);
                _dodgeDone = _dodgeHop = 0f;
            }
        }
        if (_dodgeStart < 0f) return false;
        const float DodgeTime = 0.32f;
        float k = Mathf.Clamp01((Time.time - _dodgeStart) / DodgeTime);
        // (in steps on top of the road's own moving of it)
        float done = foe.dodgeDistance * (1f - (1f - k) * (1f - k));
        float hop = Mathf.Sin(k * Mathf.PI) * foe.dodgeDistance * 0.35f;
        transform.position += new Vector3(_dodgeDir * (done - _dodgeDone), hop - _dodgeHop, 0f);
        _dodgeDone = done; _dodgeHop = hop;
        Animate(foe.idle != null && foe.idle.Length > 0 ? foe.idle : foe.walk, 0f);
        if (k >= 1f)
        {
            _dodgeStart = -1f;
            transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z);
            _nextAttack = Mathf.Max(_nextAttack, Time.time + 0.25f);
        }
        return true;
    }

    void Shambler(float dx)
    {
        // She's dead: no more blows, its swing dropped - it just stands there.
        if (EndlessKnightDirector.Player.IsDead)
        {
            _attackStart = -1f;
            Animate(foe.idle != null && foe.idle.Length > 0 ? foe.idle : foe.walk, Time.time + _phase);
            return;
        }
        if (_attackStart >= 0f)
        {
            float t = Time.time - _attackStart;
            Animate(foe.attack, t, false);
            int frame = (int)(t * foe.fps);
            if (!_hitDealt && frame >= foe.hitFrame && frame < foe.hitFrame + Mathf.Max(1, foe.hitFrames) && InReach())   // (only while the blow lands)
            {
                _hitDealt = true;
                HurtHer(0.4f, 0.4f);
            }
            if (foe.attack == null || frame >= foe.attack.Length)
            {
                _attackStart = -1f;
                _nextAttack = Time.time + foe.attackCooldown;
            }
            return;
        }
        if (Dodging(dx)) return;
        Separate();   // (eased apart if they've come to stand in each other)
        // (walking on till it's well in reach, standing till she's out of its swing's reach: no flicker between
        // the two - and never standing short of her, neither walking nor swinging, with her standing still)
        _walking = Mathf.Abs(dx) > foe.reach * (_walking ? 0.72f : 0.8f) && !Blocked(Mathf.Sign(dx));
        if (_walking)
        {
            transform.position += Vector3.right * Mathf.Sign(dx) * foe.walkSpeed * Time.deltaTime;
            // Its steps with the ground it covers (not the clock): no sliding, no skipping.
            _stride += Time.deltaTime;
            Animate(foe.walk, _stride + _phase);
        }
        else
        {
            Animate(foe.idle != null && foe.idle.Length > 0 ? foe.idle : foe.walk, Time.time + _phase);
            if (Time.time >= _nextAttack && Mathf.Abs(dx) <= foe.reach * 0.8f && foe.explodeChance > 0f && Random.value < foe.explodeChance)
            {
                // Now and then, in reach of her, it blows itself up instead (its fuse first: time to get clear).
                _selfDestruct = true;
                Die();
                return;
            }
            if (Time.time >= _nextAttack && Mathf.Abs(dx) <= foe.reach * 0.8f)
            {
                _attackStart = Time.time;
                _hitDealt = false;
                if (foe.attackSound) Sound(foe.attackSound, foe.attackVolume);
            }
        }
    }

    static float s_nextLeap, s_nextHurt;

    // Another leaper (a frog) already in the air.
    bool OneLeaping()
    {
        foreach (var o in All) if (o && o != this && o._leapStart >= 0f && o._deadAt < 0f) return true;
        return false;
    }

    // Easing apart foes standing in each other: not now - they pass through each other - but for a swarm (the rats,
    // level 3's and the gazer's): each gets out from on top of the next, so they stay a row of rats, not a pile.
    float SwarmGap => foe.bodyPixels * RoadFoe.Pixel * foe.scale * 1.15f;
    void Separate()
    {
        if (foe.swarm <= 0) return;
        float gap = SwarmGap, x = transform.position.x;
        foreach (var o in All)
        {
            if (!o || o == this || o.template || o._deadAt >= 0f || o._flying || o.foe == null || o.foe.swarm <= 0) continue;
            float apart = x - o.transform.position.x;
            if (Mathf.Abs(apart) >= gap) continue;
            float away = apart != 0f ? Mathf.Sign(apart) : Mathf.Repeat(_phase * 7f, 2f) < 1f ? 1f : -1f;
            transform.position += Vector3.right * away * 1.8f * Time.deltaTime;
            return;
        }
    }

    // Her on it: landing on its head from above, it's stomped (false); else she's hurt (true).
    bool HurtHer(float knockHeight, float knockBack)
    {
        if (Stomped()) return false;
        if (Time.time < s_nextHurt) return true;   // (not a pile of them hurting her all at once)
        s_nextHurt = Time.time + 0.5f;
        if (foe.hurtPlayerSound && !PlayerBuffs.Shield && EndlessKnightDirector.Player && !EndlessKnightDirector.Player.IsDead) PitAudio.PlayClip(foe.hurtPlayerSound, 0.8f);
        RoadFoe.HurtPlayer(foe.damage, knockHeight, knockBack, transform.position.x);
        return true;
    }

    // Its attack, hit and death sounds, at a random pitch if it has one (LevelFoes' soundPitch).
    void Sound(AudioClip clip, float volume)
    {
        if (foe.soundPitch > 0f) PitAudio.Voice(clip, volume, Random.Range(1f - foe.soundPitch, 1f + foe.soundPitch));
        else PitAudio.PlayClip(clip, volume);
    }

    static AudioClip s_bounce;

    // Coming down on its head (her boots over its middle, on her way down): it takes a blow - flashing, no blood
    // splash - and she bounces up off it with a boing, unhurt. Only once her boots are down in its head a few
    // pixels (their solid pixels, not the clear canvas round them); till then, above it, she isn't hurt either.
    const float StompSink = 3f;   // (art pixels)
    float _dropThrough = -1f;
    bool Stomped()
    {
        var player = EndlessKnightDirector.Player;
        if (!player || _deadAt >= 0f || !_sr || !_sr.sprite) return false;
        if (Time.time < _dropThrough) return true;   // (she's dropping on through it after her last bounce: no hurt, no second blow)
        // (a frog leaping, or a birb diving, up into her from below is landed on too, her still on her way up: above
        // it, she's never hurt by it)
        bool leaper = foe.style == LevelFoes.Style.Leaper;
        if (!player.IsComingDown && !(leaper && player.IsInAir)) return false;
        var art = player.transform.Find("ARIANA");
        var herSr = art ? art.GetComponent<SpriteRenderer>() : null;
        if (!herSr || !herSr.sprite) return false;
        float boots = art.position.y + SpriteShadow.OpaqueBottom(herSr.sprite) * art.lossyScale.y;
        float sy = Mathf.Abs(_sr.transform.lossyScale.y);
        float head = _sr.transform.position.y + SpriteShadow.OpaqueTop(_sr.sprite) * sy;
        float feet = _sr.transform.position.y + SpriteShadow.OpaqueBottom(_sr.sprite) * sy;
        if (boots < (head + feet) * 0.5f) return false;
        if (boots > head - StompSink * RoadFoe.Pixel) return true;   // (still coming down onto it)
        // (four bounces in a row at most: the next still lands its blow, but she drops on down to the road)
        if (player.Bounces < PlayerScriptARIANAClips.MaxBounces)
        {
            player.Bounce(1f);
            if (!s_bounce) s_bounce = Resources.Load<AudioClip>("Collect/Sounds/stomp_bounce");
            if (s_bounce) PitAudio.PlayClip(s_bounce, 1f);
        }
        else _dropThrough = Time.time + 0.8f;
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Light);
        _hitDealt = true;
        _bumpSafe = Time.time + 0.3f;
        _hitsLeft--;
        if (_hitsLeft <= 0) { Die(); return true; }
        if (foe.hitCries != null && foe.hitCries.Length > 0) PitAudio.PlayClip(foe.hitCries[Random.Range(0, foe.hitCries.Length)], 1f);
        Flash();
        if (_leapStart >= 0f)
        {
            _leapStart = -1f; _diving = false;
            transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, transform.position.z);
        }
        _nextAttack = Mathf.Max(_nextAttack, Time.time + 1f);
        return true;
    }

    // Near enough her for her sword: only once it's swordReach from where she stands (so it dies against
    // her blade, not far off in the air) - a swooping birb too, climbing or diving: cut down before it hits her.
    bool InSwordReach()
    {
        if (foe.swordReach <= 0f) return true;
        // From where she stands (not her drawn frame - mid-swing it takes in the whole sword's sweep).
        var it = RoadFoe.Drawn(_sr);
        float x = PlayerX;
        float gap = x < it.min.x ? it.min.x - x : x > it.max.x ? x - it.max.x : 0f;
        return gap <= foe.swordReach;
    }

    // Where her blows find it: its drawn body and hitPadding round it.
    Bounds Struck()
    {
        var b = RoadFoe.Drawn(_sr);
        if (foe.bodyPixels > 0f)
        {
            // Just its body, about where it stands (its sword reaching out isn't it).
            float half = foe.bodyPixels * 0.5f * Mathf.Abs(transform.lossyScale.x) / (_sr.sprite ? _sr.sprite.pixelsPerUnit : 100f);
            b.SetMinMax(new Vector3(transform.position.x - half, b.min.y, b.min.z), new Vector3(transform.position.x + half, b.max.y, b.max.z));
        }
        if (foe.hitPadding > 0f) b.Expand(foe.hitPadding * 2f);
        return b;
    }

    // Its blow's reach, in front of it, against her body.
    bool InReach()
    {
        var body = RoadFoe.Drawn(_sr);
        var her = RoadFoe.PlayerBody();
        float front = _facingRight ? body.max.x : body.min.x;
        float near = _facingRight ? her.min.x : her.max.x;
        float gap = _facingRight ? near - front : front - near;
        bool across = gap < foe.reach * 0.5f && (_facingRight ? her.max.x > body.center.x : her.min.x < body.center.x);
        bool up = her.min.y < body.max.y;   // (jumped clear over it, she's not hit)
        return across && up;
    }

    // Its drawn body touchPixels deep into hers, across and up.
    bool Touching(float pixels)
    {
        var her = RoadFoe.PlayerBody();
        var it = RoadFoe.Drawn(_sr);
        if (foe.attackPixels > 0f)
        {
            // Just its body, about its feet (not the empty room in its art round it).
            float half = foe.attackPixels * 0.5f * Mathf.Abs(transform.lossyScale.x) / (_sr.sprite ? _sr.sprite.pixelsPerUnit : 100f);
            it.SetMinMax(new Vector3(transform.position.x - half, it.min.y, it.min.z), new Vector3(transform.position.x + half, it.max.y, it.max.z));
            // ...and into her body itself, its middle (not her hair, cape or sword out to the side).
            float core = her.extents.x * foe.herBodyShare;
            her.SetMinMax(new Vector3(her.center.x - core, her.min.y, her.min.z), new Vector3(her.center.x + core, her.max.y, her.max.z));
            // (a swooping birb: down into her middle, not just brushing her head)
            if (foe.swoop) her.SetMinMax(her.min, new Vector3(her.max.x, her.center.y + her.extents.y * 0.15f, her.max.z));
        }
        float across = Mathf.Min(her.max.x, it.max.x) - Mathf.Max(her.min.x, it.min.x);
        float up = Mathf.Min(her.max.y, it.max.y) - Mathf.Max(her.min.y, it.min.y);
        float depth = pixels * RoadFoe.Pixel;
        return across >= depth && up >= depth;
    }

    void Die()
    {
        if (_deadAt >= 0f) return;
        _deadAt = Time.time;
        if (!_selfDestruct) RoadFoe.KillPoints(boss: false, summoned: selfAnchored);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Medium);
        if (_riseStart >= 0f) { _riseStart = -1f; EndRise(); }
        if (foe.blast || _selfDestruct) { _fuse = true; if (foe.attackSound) PitAudio.PlayClip(foe.attackSound, 0.5f); return; }   // (its fuse burns first: time to get clear)
        // The finishing blow knocks it back too, as it goes.
        if (foe.knockBack > 0f && _riseStart < 0f)
        {
            _knockDir = transform.position.x > PlayerX ? 1f : -1f;
            _knockLeft = foe.knockBack;
        }
        bool flicker = (foe.death == null || foe.death.Length == 0) && foe.hurt != null && foe.hurt.Length > 0;
        if (!flicker) Burst(harmless: false);   // (with a flicker first, it bursts at its end)
        else if (foe.deathSound) Sound(foe.deathSound, foe.deathVolume);
        if (!flicker && (foe.death == null || foe.death.Length == 0))
        {
            // No death frames: its burst at once, and its body flung back, flashing white, gone in a moment.
            _flung = true;
            Flash(new Color(1f, 1f, 1f, 0.9f), Mathf.Max(FlingTime, _flashUntil - Time.time));
        }
    }

    void Burst(bool harmless, bool sound = true)
    {
        var at = _sr ? RoadFoe.Drawn(_sr).center : transform.position;
        if (foe.bones) FoeFx.Bones(at, foe.boneCount, foe.skull, foe.boneSpread);
        if (foe.blood.a > 0f) FoeFx.Gore(at, foe.blood, foe.bloodSize, foe.bloodCount);
        if (foe.poof) FoeFx.Poof(at);
        if (foe.feathers) FoeFx.Feathers(at, foe.featherColor);
        if (foe.blast || _selfDestruct) FoeFx.Blast(BlastFrom, transform.position, foe.blastHue, HueMaterial, foe.blastDamage, !harmless, tight: true);
        if (sound && foe.deathSound) Sound(foe.deathSound, foe.deathVolume);
    }

    const float FlickerTime = 0.4f, FuseTime = 0.55f, FlingTime = 0.16f, HeavyFlash = 0.22f;
    bool _fuse, _flung, _selfDestruct;

    void Dying()
    {
        float t = Time.time - _deadAt;
        if (_fuse)
        {
            // Like the bomb guy's fuse: flashing, faster as it burns down, shaking; then the blast.
            float burnt = Mathf.Clamp01(t / FuseTime);
            bool lit = ((int)(t * Mathf.Lerp(6f, 18f, burnt))) % 2 == 0;
            // (in its own magic's colour - its glowing blood's - half see-through, on and off)
            var magic = foe.blood.a > 0f ? foe.blood : foe.outline.a > 0f ? foe.outline : new Color(1f, 0.15f, 0.1f);
            if (lit) Flash(new Color(magic.r, magic.g, magic.b, 0.5f), 0.05f);
            else _flashUntil = 0f;
            var stand = foe.hurt != null && foe.hurt.Length > 0 ? foe.hurt : foe.idle != null && foe.idle.Length > 0 ? foe.idle : foe.walk;
            if (stand != null && stand.Length > 0) Show(stand[0]);
            transform.position = new Vector3(transform.position.x + Mathf.Sin(t * 70f) * RoadFoe.Pixel * 0.5f * burnt, SpikeTrap.PlayerFeetY, transform.position.z);
            if (t >= FuseTime)
            {
                _fuse = false;
                _flashUntil = 0f;
                _deadAt = Time.time;
                Burst(harmless: false);
                if (foe.death == null || foe.death.Length == 0) Destroy(gameObject);
            }
            return;
        }
        if (_knockLeft > 0f)
        {
            // Knocked back as it dies.
            float step = Mathf.Min(_knockLeft, Mathf.Max(0.6f, _knockLeft * 14f) * Time.deltaTime);
            transform.position += Vector3.right * _knockDir * step;
            _knockLeft -= step;
        }
        if (_flung)
        {
            _sr.color = new Color(_sr.color.r, _sr.color.g, _sr.color.b, Mathf.Clamp01(1f - t / FlingTime));
            if (t >= FlingTime) Destroy(gameObject);
            return;
        }
        if (foe.death == null || foe.death.Length == 0)
        {
            // Hit: its hurt frames, flashing, a moment - then it bursts.
            Show(foe.hurt[Mathf.Min((int)(t * 12f), foe.hurt.Length - 1)]);
            _sr.enabled = ((int)(t * 20f)) % 2 == 0 || t < 0.08f;
            if (t >= FlickerTime) { Burst(harmless: false, sound: false); Destroy(gameObject); }
            return;
        }
        Animate(foe.death, t, false);
        int frames = foe.death != null ? foe.death.Length : 0;
        float end = frames / Mathf.Max(1f, foe.fps);
        if (t > end) _sr.color = new Color(_sr.color.r, _sr.color.g, _sr.color.b, Mathf.Clamp01(1f - (t - end) / 0.4f));
        if (t > end + 0.4f) Destroy(gameObject);
    }
}
