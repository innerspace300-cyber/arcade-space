using UnityEngine;

// ENDLESS KNIGHT's spike trap (free trap assets, Trap_Spike.png: 27 frames).
// It sits sunk in the road with only the tips showing (frames 0-6), and
// fires (frames 7-26) when the player comes within triggerDistance; once
// it's back down it re-arms when the player leaves that range, and fires
// again on the way back. The damage zone is the spikes themselves, up to
// the tips of the current frame (tipPixels): a player over the trap whose
// feet are below the tips is hurt - running into it or landing on it -
// at most once every hitCooldown seconds, and knocked up and back,
// blinking. Clear it by jumping high enough. Spawned and moved with the
// road by EndlessKnightDirector (through CollectableSpawnSystem).
public class SpikeTrap : MonoBehaviour
{
    public Sprite[] frames;
    [Tooltip("Height of each frame's tips above the road, in the sheet's pixels.")]
    public int[] tipPixels;
    [Tooltip("Size relative to the characters' pixels (the sheet's spikes are big next to them).")]
    public float scale = 0.55f;
    [Tooltip("Frames the idle loop plays (the tips peeking out).")]
    public int idleFrames = 7;
    public float idleFps = 8f;
    public float fireFps = 22f;
    [Tooltip("Fires when the player is this close (world units, ahead of the trap's centre).")]
    public float triggerDistance = 1f;
    [Tooltip("Half the width of the spikes (43 px in the sheet) plus a little for the player's feet.")]
    public float hitMargin = 0.05f;
    [Tooltip("ARcade: her feet this many of the characters' pixels under its tips still clear them (an easier jump).")]
    public float jumpClearancePixels = 3f;
    public const int SpikesWidthPixels = 43;
    /// Level 4's bone arms in a group: this far apart (her pixels) - each one's hand clear of the next.
    public const int BoneArmGapPixels = 70;
    [Tooltip("Health lost per hit (the player has 125).")]
    public int damage = 15;
    [Tooltip("Seconds before the spikes can hurt again.")]
    public float hitCooldown = 0.5f;
    [Tooltip("Knockback: how high and how far back the player is thrown.")]
    public float knockHeight = 0.6f, knockBack = 0.5f;
    [Tooltip("Blood spray played at the player's feet on a hit (Blood FX 2.0, BloodEruption).")]
    public GameObject bloodPrefab;

    /// The road's height and the player's lane depth (set by EndlessKnightDirector).
    public static float GroundY, LaneZ;
    /// ARcade: how far below the road line the knight walks (down on the
    /// sidewalk; PitDemoGame.PlayerDropPixels) - her feet are at GroundY - PlayerDrop.
    public static float PlayerDrop;
    public static float PlayerFeetY => GroundY - PlayerDrop;
    /// World size of one pixel of the characters' art (set by EndlessKnightDirector).
    public static float PixelSize = 0.01f;
    /// Hits dealt by all spike traps (for testing).
    public static int Hits;

    SpriteRenderer _renderer;
    PlayerScriptARIANAClips _player;
    SpriteRenderer _playerBody;
    StatsControl _stats;
    float _fireTime = -1f;
    float _pixel = 0.01f;
    float _lastHit = -99f;
    bool _armed = true;

    // ARcade: level 2's and level 4's versions, each a thing that sticks up out of the road and slaps down flat along
    // it toward her (idle, then firing: the same beats, so the same frames do the same things): level 2 a neon aqua
    // tentacle (Resources/Collect/Traps/tentacle_strip.png, 96x56 frames: unity-helpers/tentacle.py) whose tip sways
    // out of a hole; level 4 a cursed bone arm (bonearm_sheet.png, 347x201 frames in rows of 7:
    // unity-helpers/bonearm.py) - femurs, a palm and spine-bone fingers - whose fingertips ripple out of a heap of
    // dirt and burst out throwing dirt, 0.35 of her pixel size (its femurs at twice their art's; it was a quarter - too hard to see). Each frame
    // hurts across its own reach (left/right of its root, mirrored when it slaps the other way) up to its own top -
    // never higher than the spikes' (29 of her pixels), so the same jump clears it.
    class Grab
    {
        public string sheet;
        public int w, h, cols, rootX, roadRow;
        public float scale;
        /// Dirt thrown up as it bursts out (FoeFx.Dirt pieces; 0: none).
        public int burstDirt;
        public int[] tops, left, right;
        public Sprite[] frames;
        /// The frames that matter to its attack: its slap landing (-1: none), its swing, its going back.
        public int slap = GrabSlap, swingFrom = GrabSwingFrom, swingTo = GrabSwingTo, backFrom = BoneArmBackFrom;
        /// A level 4 hand: thrown back hard, one hit a go, gravestones behind it, smashed by a big knight.
        public bool arm;
        /// Its art reaches right (else left).
        public bool facesRight;
        /// Its blood (clear: none of its own) - a hell hand's colour.
        public Color blood;
    }
    const int GrabIdle = 6, GrabSlap = 14, GrabTopCap = 29;
    /// Its swipe's reach past what's drawn, toward her (her pixels), while it swings down, lies there and lifts.
    const int GrabSwipeReach = 12, GrabSwingFrom = 12, GrabSwingTo = 19;
    static readonly Grab s_tentacle = new Grab
    {
        sheet = "tentacle_strip", w = 96, h = 56, cols = 26, rootX = 60, roadRow = 3, scale = 1f,
        tops = new[] { 11, 11, 12, 11, 10, 10, 16, 28, 37, 39, 34, 29, 40, 39, 15, 15, 15, 15, 24, 33, 35, 32, 26, 20, 14, 12 },
        left = new[] { -3, -5, -4, -3, -3, -3, -3, -5, -7, -7, -7, -7, -7, -17, -38, -38, -37, -38, -29, -17, -6, -7, -8, -5, -3, -3 },
        right = new[] { 3, 3, 3, 3, 4, 3, 3, 5, 10, 12, 22, 29, 16, 7, 7, 7, 7, 7, 7, 7, 6, 6, 5, 4, 3, 3 },
    };
    static readonly Grab s_boneArm = new Grab
    {
        sheet = "bonearm_sheet_glow", w = 347, h = 201, cols = 7, rootX = 241, roadRow = 2, scale = 0.35f, burstDirt = 24, arm = true,
        tops = new[] { 69, 69, 68, 69, 68, 67, 69, 94, 172, 194, 177, 155, 166, 162, 45, 43, 49, 49, 49, 49, 49, 49, 49, 48, 40, 67 },   // (idle: frame 23's whole hand, sunk 23 px into the heap, waving from the wrist, its fingers trailing - frames 6 and 25 too)
        left = new[] { -32, -32, -34, -38, -34, -32, -31, -18, -27, -29, -41, -49, -75, -155, -234, -233, -230, -230, -230, -230, -195, -155, -115, -80, -52, -32 },
        right = new[] { 20, 21, 21, 21, 21, 21, 27, 43, 57, 64, 89, 100, 57, 12, -35, -35, -35, -35, -35, -35, -18, -7, -13, -7, 7, 21 },
    };
    // ARcade: level 4's hell hands (Super Pixel Monsters Pack 2: one sheet a colour, hellhand_red etc, 128x64 frames
    // in rows of 8: unity-helpers/hellhand_sheets.py), one colour each at random, in place of the bone arm - which
    // is kept as it was, for going back to: level 4 uses it if HellHands is false.
    const bool HellHands = true;
    // Frames: a heap of dirt, then bursting up out of it (appear); idle; attack; sinking back (disappear). 1.3 of her pixel size.
    const int HandBurst = 8, HandIdle = 17, HandAttack = 33, HandLeave = 52, HandEnd = 62;
    const float HandFps = 15f, HandBurstFps = 30f, HandAttackFps = 26f;   // (the pack's own; its burst and attack quicker, to snap out at her)
    static Grab Hand(string colour, Color blood) => new Grab
    {
        sheet = "hellhand_" + colour, w = 128, h = 64, cols = 8, rootX = 63, roadRow = 2, scale = 1.3f, burstDirt = 18,
        arm = true, facesRight = true, blood = blood,
        slap = -1, swingFrom = HandAttack + 5, swingTo = HandAttack + 9, backFrom = HandAttack + 11,
        tops = HandTops, left = HandLeft, right = HandRight,
    };
    static readonly int[] HandTops = { 4, 6, 9, 9, 6, 9, 6, 9, 30, 43, 47, 49, 49, 52, 52, 52, 52, 51, 52, 51, 50, 47, 45, 45, 46, 48, 49, 50, 49, 48, 46, 48, 49, 49, 53, 51, 50, 50, 45, 37, 37, 39, 38, 36, 34, 33, 32, 34, 41, 44, 48, 49, 52, 49, 45, 40, 36, 30, 22, 14, 8, 4 };
    static readonly int[] HandLeft = { -10, -13, -14, -14, -13, -14, -13, -14, -29, -21, -25, -27, -27, -28, -28, -28, -19, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -17, -20, -18, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -14, -13, -10 };
    static readonly int[] HandRight = { 10, 12, 12, 12, 12, 12, 12, 12, 30, 22, 26, 28, 28, 29, 29, 29, 20, 13, 13, 14, 14, 14, 13, 13, 13, 13, 14, 15, 15, 13, 13, 13, 13, 13, 13, 13, 13, 13, 45, 58, 60, 61, 61, 27, 25, 26, 30, 34, 27, 20, 15, 13, 13, 13, 13, 13, 13, 13, 13, 12, 12, 10 };
    static readonly Grab[] s_hellHands =
    {
        Hand("red", new Color(1f, 0.15f, 0.1f, 1f)), Hand("green", new Color(0.35f, 1f, 0.2f, 1f)),
        Hand("blue", new Color(0.2f, 0.55f, 1f, 1f)), Hand("violet", new Color(0.7f, 0.25f, 1f, 1f)),
    };
    enum HandPhase { Hidden, Appear, Idle, Attack, Leave, Dying }
    const float HandFlash = 0.14f;  // (and flashing white first, for this long, held where it was struck)
    const int HandOverKnight = 228; // (attacking: drawn over her - her 219 attacking, and her glow)
    SpriteRenderer _whiteFlash;
    static Material s_whiteFlash;
    // Its sounds (Resources/Collect/Traps/Sounds): up out of the ground, its lunge, its death scream, going up from the hole.
    static AudioClip s_handBurst, s_handAttack, s_handDeath, s_handExplode;
    const float HandHearing = 8f;   // (near enough to hear, world units)
    int _baseSortLayer, _baseOrder;
    bool _sortKept;
    HandPhase _hand;
    float _handFrom;
    bool _handBurst;
    Grab _grab;
    bool IsHand => _grab != null && _grab.blood.a > 0f;
    int _lastFrame = -1;

    // ARcade: the bone arm's cracks as it bends into its slap (Wayne's two, at random: Resources/Collect/Traps/Sounds),
    // and ghost flames licking up off its bones and hand - purple neon wisps from the top edges of what's drawn
    // in each frame (s_boneEdges, read out of the sheet once), rising, flickering and fading.
    static AudioClip[] s_boneCracks;
    // (and the tentacle's slaps: one of the three Tentacle Hits, at random, as each one lands)
    static AudioClip[] s_tentacleSlaps;
    static Vector2[][] s_boneEdges;   // (per frame: its bones' top-edge pixels, from its root on the road, in its art's pixels)
    const int GhostFlames = 20;
    // Its blood: neon pink spraying out of its hole as it bursts up to attack, and out of it as it's smashed
    // (the purple glow and bloom on its bones are in its art: unity-helpers/bonearm_purple.py).
    static readonly Color TentacleSlime = new Color(0.78f, 0.3f, 1f, 1f);   // (level 2's tentacle: purple neon)
    static readonly Color BoneBlood = new Color(1f, 0.16f, 0.72f, 1f);
    // (purple neon, its art's own glow - strong, so it shows on its white bones)
    static readonly Color GhostCore = new Color(0.8f, 0.35f, 1f, 1f), GhostEdge = new Color(0.45f, 0.1f, 0.9f, 1f);
    static Sprite s_pixel;   // (a hard square: each flame's pixel ember)
    SpriteRenderer[] _embers;
    SpriteRenderer[] _flames;
    Vector3[] _flameFrom;
    float[] _flameBorn, _flameLife, _flamePhase, _flameSize;

    static void LoadBoneEdges(Texture2D sheet, Grab grab)
    {
        if (s_boneEdges != null || !sheet.isReadable) return;
        var px = sheet.GetPixels32();
        int n = grab.tops.Length;
        s_boneEdges = new Vector2[n][];
        var edges = new System.Collections.Generic.List<Vector2>();
        for (int f = 0; f < n; f++)
        {
            edges.Clear();
            int x0 = f % grab.cols * grab.w, y0 = sheet.height - (f / grab.cols + 1) * grab.h;
            for (int y = 0; y < grab.h - 1; y++)
                for (int x = 0; x < grab.w; x++)
                {
                    int i = (y0 + y) * sheet.width + x0 + x;
                    if (px[i].a > 200 && px[i + sheet.width].a <= 200) edges.Add(new Vector2(x - grab.rootX, y - grab.roadRow));
                }
            // (a fair spread of them: no more than 160)
            var keep = new Vector2[Mathf.Min(160, edges.Count)];
            for (int k = 0; k < keep.Length; k++) keep[k] = edges[k * edges.Count / keep.Length];
            s_boneEdges[f] = keep;
        }
    }

    void Ghosts(int frame)
    {
        var edges = s_boneEdges != null && frame < s_boneEdges.Length ? s_boneEdges[frame] : null;
        if (_flames == null)
        {
            _flames = new SpriteRenderer[GhostFlames];
            _flameFrom = new Vector3[GhostFlames];
            _flameBorn = new float[GhostFlames]; _flameLife = new float[GhostFlames];
            _flamePhase = new float[GhostFlames]; _flameSize = new float[GhostFlames];
            _embers = new SpriteRenderer[GhostFlames];
            if (!s_pixel)
            {
                var t = new Texture2D(1, 1) { filterMode = FilterMode.Point }; t.SetPixel(0, 0, Color.white); t.Apply();
                s_pixel = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            for (int i = 0; i < GhostFlames; i++)
            {
                var em = new GameObject("GHOST EMBER").AddComponent<SpriteRenderer>();
                em.gameObject.layer = gameObject.layer;
                em.sprite = s_pixel;
                em.sortingLayerID = _renderer.sortingLayerID;
                em.sortingOrder = _renderer.sortingOrder - 1;   // (behind the arm, in front of its glow)
                em.color = Color.clear;
                _embers[i] = em;
                var go = new GameObject("GHOST FLAME");
                go.layer = gameObject.layer;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = FoeFx.GlowSprite;
                sr.sortingLayerID = _renderer.sortingLayerID;
                sr.sortingOrder = _renderer.sortingOrder - 2;   // (behind the arm)
                sr.color = Color.clear;
                _flames[i] = sr;
                _flameBorn[i] = Time.time - Random.value;   // (staggered)
                _flameLife[i] = 0f;
            }
        }
        float way = _renderer.flipX ? -1f : 1f;
        float px = PixelSize;   // (her pixels: the flames' size and rise)
        for (int i = 0; i < GhostFlames; i++)
        {
            var sr = _flames[i];
            float age = Time.time - _flameBorn[i];
            if (age >= _flameLife[i])
            {
                if (edges == null || edges.Length == 0) { sr.color = Color.clear; _embers[i].color = Color.clear; continue; }
                var e = edges[Random.Range(0, edges.Length)];
                _flameFrom[i] = new Vector3(e.x * way, e.y, 0f) / 100f;   // (on the arm: it goes where the arm goes)
                _flameBorn[i] = Time.time; age = 0f;
                _flameLife[i] = Random.Range(0.35f, 0.75f);
                _flamePhase[i] = Random.value * 10f;
                _flameSize[i] = Random.Range(4.5f, 7.5f);
            }
            float k = age / _flameLife[i];
            // Licking up: rising faster as it goes, a quick flicker side to side, narrowing to a tip and fading.
            var at = transform.TransformPoint(_flameFrom[i]) + new Vector3(Mathf.Sin(age * 22f + _flamePhase[i]) * px * 0.8f * k, px * 9f * k * (0.6f + k), 0f);
            at.z = LaneZ - 0.004f;
            sr.transform.position = at;
            float w = _flameSize[i] * (1f - k * 0.7f) * px / FoeFx.GlowSprite.bounds.size.x, h = w * 1.7f;   // (the glow's own size: its pixels at 1 per unit)
            sr.transform.localScale = new Vector3(w, h, 1f);
            var c = Color.Lerp(GhostCore, GhostEdge, k);
            c.a = Mathf.Sin(Mathf.Min(1f, k * 1.4f) * Mathf.PI) * (0.75f + 0.25f * Mathf.Sin(Time.time * 30f + _flamePhase[i]));
            sr.color = c;
            // Its pixel ember: a hard square of it, snapped to her pixels, shrinking as it goes.
            var em = _embers[i];
            float side = (k < 0.5f ? 2f : 1f) * px;
            em.transform.position = new Vector3(Mathf.Round(at.x / px) * px, Mathf.Round(at.y / px) * px, at.z - 0.001f);
            em.transform.localScale = new Vector3(side, side, 1f);
            var ec = Color.Lerp(GhostCore, Color.white, k < 0.2f ? 0.4f : 0f);
            ec.a = k < 0.85f ? 1f : 0f;
            em.color = ec;
        }
    }

    void OnDestroyFlames()
    {
        if (_flames == null) return;
        foreach (var f in _flames) if (f) Destroy(f.gameObject);
        if (_embers != null) foreach (var e in _embers) if (e) Destroy(e.gameObject);
    }

    // ARcade: level 2's tentacles on the road - the fairy shoots them (FairyAlly), one ice spike bursting each.
    public static readonly System.Collections.Generic.List<SpikeTrap> Tentacles = new System.Collections.Generic.List<SpikeTrap>();
    public bool Targetable => this && _renderer && _renderer.enabled
        && (_grab == s_tentacle || (IsHand && !_smashed && _popFrom >= 0f && _hand != HandPhase.Hidden && _hand != HandPhase.Dying));
    public SpriteRenderer Art => _renderer;
    static readonly Color TentacleGoo = new Color(0.25f, 1f, 0.9f, 1f);

    /// Burst by the fairy's spike: gone in a splash of its glowing goo.
    public void Kill()
    {
        if (!Targetable) return;
        if (IsHand) { StrikeHand(); return; }   // (the cat demon's laser: as the big knight's blow)
        var at = RoadFoe.Drawn(_renderer).center;
        FoeFx.Blood(new Vector3(at.x, at.y, LaneZ - 0.01f), TentacleGoo, 18);
        FoeFx.Dirt(new Vector3(transform.position.x, PlayerFeetY, LaneZ - 0.01f), 8, 6f * PixelSize);
        Destroy(gameObject);
    }

    void OnDestroy() { Tentacles.Remove(this); OnDestroyFlames(); }

    // ARcade: big (the viking food), any blow of hers - her sword's, her Holy Slash - breaks a bone arm.
    bool Struck()
    {
        if (!_renderer || !_renderer.enabled || !_player.ActiveAttackArea(out var blow)) return false;
        var arm = RoadFoe.Drawn(_renderer);
        return arm.size != Vector3.zero && arm.min.x < blow.max.x && arm.max.x > blow.min.x && arm.min.y < blow.max.y && arm.max.y > blow.min.y;
    }

    /// Smashed: gone in a burst of its bones and the earth it came up from, a crack, points - its graves left standing.
    bool _smashed;
    void Shatter()
    {
        var at = RoadFoe.Drawn(_renderer).center;
        if (IsHand)
        {
            // Its death bursting out where it stood.
            at = new Vector3(transform.position.x, PlayerFeetY + 24f * PixelSize, at.z);
            FoeFx.Gore(at, _grab.blood, 1.1f, 14);
        }
        else FoeFx.Bones(new Vector3(at.x, at.y, LaneZ - 0.01f), 12, skull: false);
        BloodSpray(new Vector3(at.x, at.y, LaneZ - 0.01f), IsHand ? _grab.blood : BoneBlood);
        FoeFx.Dirt(new Vector3(transform.position.x, PlayerFeetY, LaneZ - 0.01f), 14, 10f * PixelSize);
        if (IsHand) { if (s_handExplode) PitAudio.PlayClip(s_handExplode, 1f); }
        else if (s_boneCracks != null && s_boneCracks.Length > 0) PitAudio.PlayClip(s_boneCracks[Random.Range(0, s_boneCracks.Length)], 1f);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Medium);
        RoadFoe.KillPoints(boss: false);
        _smashed = true;
        _renderer.enabled = false;
        foreach (Transform t in transform) if (!t.name.StartsWith("GRAVESTONE")) t.gameObject.SetActive(false);
        OnDestroyFlames();   // (its ghost flames aren't its children: gone with it, not left hanging)
        _flames = null; _embers = null;
    }

    // The hand's blood: a hard neon spray in its colour, paler at its heart, in a burst of bloom.
    static void BloodSpray(Vector3 at, Color color)
    {
        FoeFx.Gore(at, color, 0.7f, 10);
        FoeFx.Blood(at, color, 48, 1f, 2f);
        FoeFx.Blood(at, Color.Lerp(color, Color.white, 0.4f), 20, 0.8f, 2.5f);
    }

    void UseGrab(Grab grab)
    {
        if (grab.frames == null || grab.frames.Length == 0 || !grab.frames[0])
        {
            var sheet = Resources.Load<Texture2D>("Collect/Traps/" + grab.sheet);
            if (!sheet) return;
            int n = grab.tops.Length;
            grab.frames = new Sprite[n];
            for (int i = 0; i < n; i++)
                grab.frames[i] = Sprite.Create(sheet, new Rect(i % grab.cols * grab.w, sheet.height - (i / grab.cols + 1) * grab.h, grab.w, grab.h),
                    new Vector2((float)grab.rootX / grab.w, (float)grab.roadRow / grab.h), 100f);
        }
        if (grab.arm)
        {
            if (grab == s_boneArm) LoadBoneEdges(grab.frames[0].texture, grab);
            if (s_boneCracks == null)
                s_boneCracks = new[] { Resources.Load<AudioClip>("Collect/Traps/Sounds/bone_arm_1"), Resources.Load<AudioClip>("Collect/Traps/Sounds/bone_arm_2") };
        }
        if (grab.blood.a > 0f && !s_handBurst)
        {
            s_handBurst = Resources.Load<AudioClip>("Collect/Traps/Sounds/hell_hand_burst");
            s_handAttack = Resources.Load<AudioClip>("Collect/Traps/Sounds/hell_hand_attack");
            s_handDeath = Resources.Load<AudioClip>("Collect/Traps/Sounds/hell_hand_death");
            s_handExplode = Resources.Load<AudioClip>("Collect/Traps/Sounds/hell_hand_explode");
        }
        if (grab == s_tentacle && s_tentacleSlaps == null)
        {
            var slaps = new System.Collections.Generic.List<AudioClip>();
            for (int i = 1; i <= 3; i++) { var c = Resources.Load<AudioClip>("Collect/Traps/Sounds/tentacle_slap_" + i); if (c) slaps.Add(c); }
            s_tentacleSlaps = slaps.ToArray();
        }
        frames = grab.frames;
        tipPixels = System.Array.ConvertAll(grab.tops, t => Mathf.Min(t, Mathf.RoundToInt(GrabTopCap / grab.scale)));
        idleFrames = GrabIdle;
        scale = grab.scale;
        _grab = grab;
    }

    // ARcade: level 3's spinning saw blade (the same free trap pack's Suriken.png, Resources/Collect/Traps/
    // saw_blade.png: 8 frames of 32 px), on the road between the spike traps and the toads
    // (EndlessKnightDirector.QueueSpike): always turning, only its top half above the road (its hub on her
    // feet's line - the rest sunk under it), at her pixel size. It hurts anywhere on its round edge - a jump over
    // it clears it - and throws her up and back like the spikes.
    [HideInInspector] public bool saw;
    /// The director's saw it copies (never on the road itself; its own copies aren't templates).
    [System.NonSerialized] public bool template;
    const int SawSize = 32;
    /// The saw's size against her pixels (Wayne's: much bigger than its art's 32 px).
    public const float SawScale = 1.8f;
    /// Its width on the road (world units): two in a row sit this far apart.
    public static float SawWidth => SawSize * SawScale * PixelSize;
    const float SawFps = 20f;
    static Sprite[] s_saw;

    void UseSaw()
    {
        if (s_saw == null || s_saw.Length == 0 || !s_saw[0])
        {
            var sheet = Resources.Load<Texture2D>("Collect/Traps/saw_blade");
            if (!sheet) return;
            s_saw = new Sprite[sheet.width / SawSize];
            for (int i = 0; i < s_saw.Length; i++)   // (each frame's top half, its bottom edge through the hub)
                s_saw[i] = Sprite.Create(sheet, new Rect(i * SawSize, SawSize / 2, SawSize, SawSize / 2), new Vector2(0.5f, 0f), 100f);
        }
        frames = s_saw;
        scale = SawScale;
    }

    void Start()
    {
        _renderer = GetComponent<SpriteRenderer>();
        if (template) { _renderer.enabled = false; return; }
        _renderer.enabled = true;   // (the saw template's is off)
        int level = SpatialEmulator.Demo.PitDemoGame.Level;
        if (saw) UseSaw();
        else if (level == 2) { UseGrab(s_tentacle); if (_grab == s_tentacle) Tentacles.Add(this); }
        else if (level == 4)
        {
            UseGrab(HellHands ? s_hellHands[Random.Range(0, s_hellHands.Length)] : s_boneArm);
            if (IsHand) Tentacles.Add(this);   // (a target for the cat demon's laser)
        }
        _player = FindAnyObjectByType<PlayerScriptARIANAClips>();
        var body = _player ? _player.transform.Find("ARIANA") : null;
        _playerBody = body ? body.GetComponent<SpriteRenderer>() : null;
        _stats = FindAnyObjectByType<StatsControl>();
        SpriteShadow.Cast(_renderer);   // ARcade: its shadow on the road as it springs up
        // Sit on the road in the player's lane, at the characters' pixel size
        // (the sprites are 100 px per unit).
        // (ARcade: down on the sidewalk, on the knight's line.)
        transform.position = new Vector3(transform.position.x, PlayerFeetY, LaneZ);
        transform.localScale = Vector3.one * (PixelSize / 0.01f * scale);
        _pixel = PixelSize * scale;
        if (_grab != null && _grab.arm) Gravestone();
        if (IsHand) { _standing = transform.localScale; Pop(0f); }
    }

    // ARcade: level 4's graves and each hell hand's heap start under the ground, and pop up out of it as she comes
    // along, throwing rocks, earth and dust: squashed flat on the road, up past their height, and settling.
    const float PopAt = 6f, PopAtSprint = 11f, PopTime = 0.38f;   // (how far off, world units; how long)
    const float HandBurstAtSprint = 1.8f;      // (and its hand up out of it from further off, of its reach)
    static readonly Color RockLight = new Color(0.55f, 0.47f, 0.4f), RockDark = new Color(0.3f, 0.25f, 0.22f);
    float _popFrom = -1f;
    Vector3 _standing;
    void Pop(float height) => transform.localScale = new Vector3(_standing.x, _standing.y * Mathf.Max(height, 0.0001f), _standing.z);
    // Its graves and heap all in the picture, with room to spare: it's seen coming up, not cut off at the edge.
    const float PopMarginPixels = 70f;
    bool WellOnScreen()
    {
        var cam = SpatialEmulator.Demo.PitDemoGame.ActorsCamera;
        if (!cam) return true;
        float half = cam.orthographicSize * cam.aspect - PopMarginPixels * PixelSize;
        return Mathf.Abs(transform.position.x - cam.transform.position.x) <= half;
    }
    void PopUp(float dx)
    {
        if (_popFrom < 0f)
        {
            // (she's sprinting at it: sooner, or she'd be past before it could reach for her)
            bool rushing = _player && _player.IsSprinting;
            if (rushing ? Mathf.Abs(dx) >= PopAtSprint : Mathf.Abs(dx) >= PopAt || !WellOnScreen()) { Pop(0f); return; }
            _popFrom = Time.time;
            var at = new Vector3(transform.position.x, PlayerFeetY, LaneZ - 0.01f);
            FoeFx.Rocks(at, RockLight, RockDark, 12, 0.35f);
            FoeFx.Dirt(at, 16, 12f * PixelSize);
            FoeFx.Poof(at + Vector3.up * 3f * PixelSize, 6, 0.8f);
            if (Mathf.Abs(dx) < HandHearing && s_handBurst) PitAudio.PlayClip(s_handBurst, 0.5f);
        }
        float k = Mathf.Clamp01((Time.time - _popFrom) / PopTime);
        Pop(k < 0.65f ? Mathf.Lerp(0f, 1.18f, k / 0.65f) : Mathf.Lerp(1.18f, 1f, (k - 0.65f) / 0.35f));
    }

    // ARcade: the bone arm's attack slower, to be seen: up and its slap at BoneArmFps; then, slower still
    // (BoneArmBackFps), dragging itself back along the road into its hole on its fingertips - stabbed into the
    // dirt as it slapped down (its wrist up off the road) - dirt kicked up at them as they scrape.
    const float BoneArmFps = 14f, BoneArmBackFps = 7f;
    const int BoneArmBackFrom = 18, BoneArmScrapeFrom = 20;
    int BoneArmFrame(float since)
    {
        float up = (BoneArmBackFrom - idleFrames) / BoneArmFps;
        if (since < up) return idleFrames + (int)(since * BoneArmFps);
        int frame = BoneArmBackFrom + (int)((since - up) * BoneArmBackFps);
        if (frame != _lastFrame && frame >= BoneArmScrapeFrom && frame < frames.Length && _player
            && Mathf.Abs(transform.position.x - _player.transform.position.x) < 6f)
        {
            float way = _renderer.flipX ? -1f : 1f;   // (its fingertips: its reach that way, from its root)
            FoeFx.Dirt(new Vector3(transform.position.x + _grab.left[frame] * way * _pixel, PlayerFeetY, LaneZ - 0.01f), 5, 3f * PixelSize);
        }
        return frame;
    }

    // ARcade: level 4's tell - gravestones standing just behind each bone arm (Wayne's five, at random,
    // Resources/Collect/Traps/Graves), so she sees one's there before it bursts up. At her pixel size.
    const float GraveRight = 22f, GraveLeft = 26f, GraveFar = 30f;   // (her pixels from its root)
    static Sprite[] s_graves;
    void Gravestone()
    {
        if (s_graves == null)
        {
            var list = new System.Collections.Generic.List<Sprite>();
            foreach (var tex in Resources.LoadAll<Texture2D>("Collect/Traps/Graves"))
            {
                tex.filterMode = FilterMode.Point;
                list.Add(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 100f));
            }
            s_graves = list.ToArray();
        }
        if (s_graves.Length == 0) return;
        // A little graveyard round it: one each side, behind it, and sometimes a third further back (a little darker),
        // all different.
        var order = new System.Collections.Generic.List<Sprite>(s_graves);
        for (int i = order.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }
        var spots = new System.Collections.Generic.List<(float x, float z, float shade)> { (GraveRight, 0.003f, 1f), (-GraveLeft, 0.004f, 1f) };
        if (Random.value < 0.5f) spots.Add((GraveRight + GraveFar, 0.006f, 0.75f));
        for (int n = 0; n < spots.Count && n < order.Count; n++)
        {
            var go = new GameObject("GRAVESTONE");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one / scale;   // (her pixel size, not the arm's)
            go.transform.position = new Vector3(transform.position.x + spots[n].x * PixelSize, PlayerFeetY, LaneZ + spots[n].z);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = order[n];
            sr.color = new Color(spots[n].shade, spots[n].shade, spots[n].shade, 1f);
            sr.sortingLayerID = _renderer.sortingLayerID;
            sr.sortingOrder = _renderer.sortingOrder - 3 - n;   // (behind the arm and its flames, the far one furthest back)
            sr.sharedMaterial = _renderer.sharedMaterial;
        }
    }

    void Update()
    {
        if (template || frames == null || frames.Length == 0 || !_player) return;
        float dx = transform.position.x - _player.transform.position.x;
        // Far behind (about 10 s of walking back): CollectableSpawnSystem only hides it.
        if (dx < -30f) { Destroy(gameObject); return; }
        if (saw) { Spin(dx); return; }
        // Smashed: gone for good - CollectableSpawnSystem switches its renderers back on as it scrolls back into view.
        if (_smashed) { if (_renderer.enabled) _renderer.enabled = false; return; }
        if (_grab != null && _grab.arm && PlayerBuffs.Big && !(IsHand && (_hand == HandPhase.Hidden || _hand == HandPhase.Dying)) && Struck())
        {
            if (!IsHand) { Shatter(); return; }
            StrikeHand();
        }

        int frame;
        bool inRange = Mathf.Abs(dx) < triggerDistance;
        if (IsHand) { PopUp(dx); frame = HandFrame(dx); }
        else if (_fireTime < 0f)
        {
            frame = (int)(Time.time * idleFps) % Mathf.Min(idleFrames, frames.Length);
            if (!inRange) _armed = true;
            else if (_armed)
            {
                _fireTime = Time.time; _armed = false; _attackHit = false;
                if (_grab != null) _renderer.flipX = dx < 0f;   // (it slaps toward her: she's past it, the other way - its art reaching left)
                if (_grab == s_tentacle && Mathf.Abs(dx) < 6f)
                {
                    // (purple neon slime shooting up out of its hole as it lashes out)
                    var hole = new Vector3(transform.position.x, PlayerFeetY + 2f * PixelSize, LaneZ - 0.01f);
                    FoeFx.Blood(hole, TentacleSlime, 36, 1f, 1.5f);
                    FoeFx.Blood(hole, Color.Lerp(TentacleSlime, Color.white, 0.35f), 16, 0.8f, 1.9f);
                }
                if (_grab != null && _grab.burstDirt > 0)
                {
                    // (bursting out of the road: its earth thrown up round it - its own drawn clods as well)
                    var at = new Vector3(transform.position.x, PlayerFeetY, LaneZ - 0.01f);
                    FoeFx.Dirt(at, _grab.burstDirt, 6f * PixelSize);
                    if (_grab == s_boneArm && Mathf.Abs(dx) < 6f)
                    {
                        // (neon pink blood shooting up out of its hole with it)
                        BloodSpray(at + Vector3.up * 2f * PixelSize, BoneBlood);
                    }
                    if (Mathf.Abs(dx) < 2f) SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Light);
                }
            }
        }
        else
        {
            float since = Time.time - _fireTime;
            frame = _grab == s_boneArm ? BoneArmFrame(since) : idleFrames + (int)(since * fireFps);
            if (frame >= frames.Length)
            {
                // Back down: idle again; it fires again once the player has
                // left the trigger range and comes back.
                frame = 0;
                _fireTime = -1f;
            }
        }
        _renderer.sprite = frames[frame];
        float way = _renderer.flipX ? -1f : 1f;
        if (_grab != null && frame == _grab.slap && _lastFrame != _grab.slap)
        {
            // The slap: a burst of dirt along the road where it lands.
            float mid = (_grab.left[frame] + _grab.right[frame]) * 0.5f * way * _pixel;
            FoeFx.Dirt(new Vector3(transform.position.x + mid, PlayerFeetY, LaneZ - 0.01f), 14, (_grab.right[frame] - _grab.left[frame]) * _pixel);
            if (Mathf.Abs(dx) < 2f) SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Light);
            if (_grab == s_tentacle && Mathf.Abs(dx) < 4f && s_tentacleSlaps != null && s_tentacleSlaps.Length > 0)
                PitAudio.PlayClip(s_tentacleSlaps[Random.Range(0, s_tentacleSlaps.Length)], 1f);
        }
        if (_grab != null && _grab.arm)
        {
            // (cracking as it bends into the slap - when it's near enough to hear)
            if (IsHand)
            {
                if (frame == _grab.swingFrom && _lastFrame != _grab.swingFrom && Mathf.Abs(dx) < HandHearing && s_handAttack) PitAudio.PlayClip(s_handAttack, 0.9f);
            }
            else if (frame == _grab.swingFrom && _lastFrame != _grab.swingFrom && Mathf.Abs(dx) < 4f && s_boneCracks != null)
                PitAudio.PlayClip(s_boneCracks[Random.Range(0, s_boneCracks.Length)], 0.9f);
            if (_grab == s_boneArm && Mathf.Abs(dx) < 6f) Ghosts(frame);
        }
        _lastFrame = frame;

        // The damage zone: the spikes' width, from the road up to this frame's tips.
        float tip = PlayerFeetY + (tipPixels != null && frame < tipPixels.Length ? tipPixels[frame] : 0) * _pixel;
        float halfWidth = SpikesWidthPixels * 0.5f * _pixel + hitMargin;
        bool over = Mathf.Abs(dx) < halfWidth;
        if (_grab != null)
        {
            // (her offset from its root, on its own side, in its pixels: inside this frame's reach)
            float at = -dx * way / _pixel;
            float reach = frame >= _grab.swingFrom && frame <= _grab.swingTo ? GrabSwipeReach / _grab.scale : 0f;
            over = at > _grab.left[frame] - reach - hitMargin / _pixel && at < _grab.right[frame] + hitMargin / _pixel;
        }
        // (the bone arm and the hell hands: its slap hurts her once, and not at all as it goes back; waiting, a hit's
        // once, then none for BoneArmIdleSafe seconds - a hell hand not at all while it's in the ground)
        if (over && _grab != null && _grab.arm)
        {
            if (_fireTime >= 0f) over = !_attackHit && frame < _grab.backFrom;
            else over = Time.time - _lastHit >= BoneArmIdleSafe;
            if (IsHand && (frame < HandIdle || frame >= HandLeave)) over = false;
        }
        if (over) TryHurt(tip);
    }

    // A hell hand: a heap of dirt till she's just outside its reach; bursting up out of it; its idle; attacking, again
    // and again, as long as she's inside its reach; sinking back into the ground once she's gone.
    const float HandReach = 61f;   // (its attack zone: as far as its lunge goes, its pixels)
    const float HandBurstAt = 1.15f, HandLeaveAt = 1.5f;   // (how far off it comes up and goes down: this many of its reach)
    int HandFrame(float dx)
    {
        float since = Time.time - _handFrom;
        int on = (int)(since * HandFps);
        float off = Mathf.Abs(dx), reach = HandReach * _pixel;
        switch (_hand)
        {
            case HandPhase.Hidden:
                bool sprinting = _player.IsSprinting;
                if (off >= reach * (sprinting ? HandBurstAtSprint : HandBurstAt) || Time.time - _popFrom < PopTime + (sprinting ? 0f : 0.3f)) return on % HandBurst;   // (a heap of earth, risen with the graves)
                Phase(HandPhase.Appear);
                _handBurst = false;
                _renderer.flipX = dx > 0f;   // (reaching toward her: its art reaches right)
                return HandBurst;
            case HandPhase.Appear:
                if (!_handBurst)
                {
                    _handBurst = true;
                    var at = new Vector3(transform.position.x, PlayerFeetY, LaneZ - 0.01f);
                    FoeFx.Dirt(at, _grab.burstDirt, 6f * PixelSize);
                    if (Mathf.Abs(dx) < HandHearing && s_handBurst) PitAudio.PlayClip(s_handBurst, 0.9f);
                    if (Mathf.Abs(dx) < 6f)
                    {
                        BloodSpray(at + Vector3.up * 2f * PixelSize, _grab.blood);
                        if (Mathf.Abs(dx) < 3f) SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Light);
                    }
                }
                on = (int)(since * HandBurstFps);
                if (HandBurst + on < HandIdle) return HandBurst + on;
                Phase(HandPhase.Idle);
                return HandIdle;
            case HandPhase.Idle:
                if (off < reach && !_player.IsDead)
                {
                    _attackHit = false;
                    _fireTime = Time.time;
                    _renderer.flipX = dx > 0f;
                    Phase(HandPhase.Attack);
                    return HandAttack;
                }
                if (off >= reach * HandLeaveAt) { Phase(HandPhase.Leave); return HandLeave; }
                return HandIdle + on % (HandAttack - HandIdle);
            case HandPhase.Attack:
                on = (int)(since * HandAttackFps);
                if (HandAttack + on < HandLeave && !_player.IsDead) return HandAttack + on;   // (she's dead: it stops there)
                _fireTime = -1f;
                Phase(HandPhase.Idle);
                return HandIdle;
            case HandPhase.Dying:
                WhiteFlash(since < HandFlash);
                if (since < HandFlash) return Mathf.Clamp(_lastFrame, 0, HandEnd - 1);
                Shatter();
                return HandEnd - 1;
            default:
                if (HandLeave + on < HandEnd) return HandLeave + on;
                Phase(HandPhase.Hidden);
                return 0;
        }
    }

    // Struck: white a moment, then gone in its death where it stood (HandFrame, Shatter).
    void StrikeHand()
    {
        if (_hand == HandPhase.Dying) return;
        Phase(HandPhase.Dying);
        _fireTime = -1f;
        if (s_handDeath) PitAudio.PlayClip(s_handDeath, 1f);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Light);
    }

    void Phase(HandPhase phase)
    {
        _hand = phase;
        _handFrom = Time.time;
        // Attacking (and struck): over the knight; else back with the road's traps.
        if (!_sortKept) { _sortKept = true; _baseSortLayer = _renderer.sortingLayerID; _baseOrder = _renderer.sortingOrder; }
        bool over = phase == HandPhase.Attack || phase == HandPhase.Dying;
        if (over) { _renderer.sortingLayerName = ChestGem.KnightLayer; _renderer.sortingOrder = HandOverKnight; }
        else { _renderer.sortingLayerID = _baseSortLayer; _renderer.sortingOrder = _baseOrder; }
    }

    // All white over it, its shape frame by frame.
    void WhiteFlash(bool on)
    {
        if (!on) { if (_whiteFlash) _whiteFlash.enabled = false; return; }
        if (!_whiteFlash)
        {
            if (!s_whiteFlash)
            {
                var shader = Shader.Find("SpatialEmulator/SpriteSolid");
                if (!shader) return;
                s_whiteFlash = new Material(shader) { name = "Hand Flash", color = Color.white };
            }
            var go = new GameObject("WHITE FLASH");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.0005f);
            _whiteFlash = go.AddComponent<SpriteRenderer>();
            _whiteFlash.sharedMaterial = s_whiteFlash;
        }
        _whiteFlash.sprite = _renderer.sprite;
        _whiteFlash.flipX = _renderer.flipX;
        _whiteFlash.sortingLayerID = _renderer.sortingLayerID;
        _whiteFlash.sortingOrder = _renderer.sortingOrder + 1;
        _whiteFlash.enabled = true;
    }

    // The saw: turning all the time; its damage zone its round edge (its hub on her feet's line), so the nearer
    // its middle she is, the higher she has to be.
    void Spin(float dx)
    {
        _renderer.sprite = frames[(int)(Time.time * SawFps) % frames.Length];
        float r = SawSize * 0.5f;
        float off = Mathf.Max(0f, Mathf.Abs(dx) - hitMargin) / _pixel;   // (her middle from its hub, in its pixels)
        if (off < r) TryHurt(PlayerFeetY + Mathf.Sqrt(r * r - off * off) * _pixel);
    }

    // Hurts her if her feet are under `tip` (and it hasn't just): her health, blood at her feet, and thrown back
    // (her hurt sound and flash come with the lost health).
    const float BoneArmIdleSafe = 2f;
    bool _attackHit;
    void TryHurt(float tip)
    {
        if (Time.time - _lastHit >= hitCooldown && _playerBody
            && _playerBody.bounds.min.y < tip - jumpClearancePixels * _pixel && _stats && _stats.GetCurrentHP() > 0)
        {
            _lastHit = Time.time;
            if (_fireTime >= 0f) _attackHit = true;
            Hits++;
            SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Heavy);   // (the phone, and the controller)
            // IncreaseHP ignores a change that would go below 0, so the last hit takes what's left.
            if (PlayerBuffs.Shield) PlayerBuffs.Blocked(transform.position.x);   // (the spikes glance off her shield)
            _stats.IncreaseHP(-Mathf.Min(damage, _stats.GetCurrentHP()));
            // (her white neon blood: HudJuice, on any hit)
            // (the tentacle's and the bone arm's slaps shove her back along the road, on her feet; the spikes and the
            // saw throw her up and back)
            if (_grab != null && _grab.arm) _player.Knockback(0.85f, knockBack * 3f);   // (the bone arm, the hell hands: thrown up and well back)
            else if (_grab != null) _player.Pushback(knockBack * 2.6f, 0.4f);
            else _player.Knockback(knockHeight, knockBack);
        }
    }
}
