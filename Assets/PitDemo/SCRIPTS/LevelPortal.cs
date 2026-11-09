using System.Collections;
using SpatialEmulator.Controls;
using SpatialEmulator.Demo;
using TMPro;
using UnityEngine;

// ARcade: the way out of the loop. Once a run has picked up every fruit
// (FruitTracker - after its fruit rain, the first time), the road clears
// and the witch arrives in a yellow strike beside the knight, like after a
// death. She says well done and offers a portal to the next level
// (DemoDialogue.AskPortal - YES is the only answer): she casts, a blue strike lands behind them and the
// portal opens there (its Activate, then Idle looping), framed by the game's
// fruit standing round it like a door frame, jiggling. The knight steps in (UP in front of it), shrinks
// into it, it closes, and the next level starts (PitDemoGame.GoToLevel) with
// her score - where she steps out of the same portal, which closes behind her.
// On the level's root (with DeathRespawn), set up by PortalBuilder.
[DefaultExecutionOrder(500)]   // after the knight's own LateUpdate (her sprite's place)
public class LevelPortal : MonoBehaviour
{
    [Tooltip("The next level's prefab (none: no portal from this one).")]
    public GameObject nextLevel;
    [Tooltip("This level's number.")]
    public int level = 1;
    public Sprite[] activateFrames, idleFrames, closeFrames;
    public float fps = 10f;
    [Tooltip("Size, in the characters' pixels to the art's.")]
    public float portalScale = 1.15f;
    [Tooltip("Where it opens: this far to the witch's right (world units, her middle to its middle) - but never past the right edge of the picture (its fruit just inside it).")]
    public float portalOffset = 1.6f;
    [Tooltip("How solid its frames are drawn (a little see-through).")]
    [Range(0f, 1f)] public float portalOpacity = 0.56f;
    [Tooltip("Its glow: a soft pink halo round it, pulsing.")]
    public Color glowColor = new Color32(255, 60, 200, 255);
    [Range(0f, 1f)] public float glowStrength = 0.85f;
    [Header("Fruit round its edge")]
    [Tooltip("The whole fruit pack (scrimsy's 16 px icons, every kind and colour) - shuffled round it.")]
    public Sprite[] ringFruit;
    public float fruitScale = 0.85f;
    [Tooltip("Rings of fruit: each this many of its art pixels out from its frame's edge (below 0: inside it) - far enough apart to see every fruit - and how close together the fruit sit round each (art pixels apart).")]
    public float[] rings = { 3f };
    public float fruitSpacing = 10f;
    [Tooltip("Every fruit round it at once, in order, shared between the rings by their lengths (overlapping as close as they must). Off: as many as fit at fruitSpacing.")]
    public bool fitAllFruit = true;

    /// Its rings (art pixels out from its frame's edge).
    public float[] Rings(int fruitCount) => rings;

    /// How many fruit fit round a ring at the spacing.
    public int RingPlaces(float ring) => Mathf.Max(6, Mathf.RoundToInt(Portal.RingPath(ring, out _, out _) * 100f / Mathf.Max(1f, fruitSpacing)));
    float[] RingsNow => Rings(ringFruit != null && ringFruit.Length > 0 ? ringFruit.Length : 15);
    [Tooltip("The fruit's little jiggle: how far each turns either way (degrees) and bobs (art pixels).")]
    public float jiggleDegrees = 6f, jigglePixels = 0.6f;

    [Header("Its own torii, where she starts (level 1's - its neon sun and palms; later levels': the level before's way on)")]
    [Tooltip("Level 1's: she comes out of it as a new game starts, then the witch's welcome; it stands there, closed, after.")]
    public Sprite homeGate;
    public Sprite homeGateGlowArt;
    public Color homeGateGlow = new Color(0.3f, 0.6f, 1f), homeGateSwirl = new Color(0.4f, 0.7f, 1f);
    [Tooltip("A retro grid in its doorway (clear: none).")]
    public Color homeGateGrid = Color.clear;
    /// Coming out of level 1's own torii as a new game starts (the witch's welcome waits for it: PitDemoGame).
    public static bool IntroArriving;

    [Header("Gate (a torii in place of the portal's swirl)")]
    [Tooltip("This level's way out is through this gate (level 2's, to the bamboo forest); the next level's knight walks out of it too.")]
    public Sprite gate;
    public float gateScale = 1.25f;
    public Color gateGlow = new Color32(255, 200, 90, 255);
    [Tooltip("The swirl inside this level's gate, tinted (white: as drawn).")]
    public Color gateSwirlTint = Color.white;
    [Tooltip("The swirl's own frames in this level's gate (another of the pack's palettes - level 2's green); empty: the portal's.")]
    public Sprite[] gateActivateFrames, gateIdleFrames, gateCloseFrames;
    [Tooltip("A glow lighting this level's gate from behind (the gate's shape blurred, more canvas all round, its pivot where the gate's foot is), pulsing; tinted by gateBacklight - the fairy gate's neon blue.")]
    public Sprite gateBacklightArt;
    public Color gateBacklight = Color.white;
    [Tooltip("Little things in the air round this level's gate (GateMotes): sparks, flies.")]
    public GateMotes.Kind gateMotes;
    public Color gateMotesColor = Color.white;
    [Tooltip("Smoke curling up off this level's gate, in this colour (clear: none) - the death gate's dark grey.")]
    public Color gateSmoke = Color.clear;
    [Tooltip("Burning embers along this level's gate's outline (GateEmbers): sets of spots flickering on their own, and their glow (none: no embers).")]
    public Sprite[] gateEmbers;
    public Sprite gateEmberGlow;
    [Tooltip("Little fairies flitting round the gate's outer edge: their animations' frames, fairyFrames to a fairy (empty: none).")]
    public Sprite[] gateFairies;
    public int fairyFrames = 8, fairyCount = 10;
    [Tooltip("No longer used: a gate opening where she stands now clears the campfire itself (ClearOfFire).")]
    public float arrivalOffset;
    // The gate she went through (and its look), for the next level's arrival.
    static Sprite s_arriveGate;
    static Color s_arriveSmoke;
    static Sprite[] s_arriveEmbers, s_stayEmbers;
    static Sprite s_arriveEmberGlow, s_stayEmberGlow;
    static (Color glow, Color swirl, Sprite[] fairies, int frames, int count, Sprite[] activate, Sprite[] idle, Sprite[] close, Color backlight, Sprite backlightArt, GateMotes.Kind motes, Color motesColor) s_arriveLook;
    [Header("Stepping in")]
    [Tooltip("How near its middle she must stand to step in (world units).")]
    public float enterRange = 0.12f;
    public float shrinkTime = 0.6f;
    public AudioClip openSound, enterSound;

    // Through the portal: the next level's knight steps out of one.
    static bool s_arriveByPortal;
    // ARcade: the ways back. Each level past the first keeps the portal she came in by open, by the campfire, to go back
    // through to the level before - where she comes out of its own way on, open too: back and forth as she likes.
    static bool s_arriveBack;   // (she's coming back through one)
    static readonly GameObject[] s_prefabs = new GameObject[10];   // (each level's prefab, by its number: level 1's none, the demo's own)
    // The way on and the way back side by side: their half widths apart and a clear gap (100 of the phone's ~924 px picture).
    const float GatesApart = 0.11f;   // (picture widths)
    float BackGap
    {
        get
        {
            var view = PitDemoGame.HudCamera;
            float width = view ? view.orthographicSize * 2f * view.aspect : 4f;
            return GateHalf(HomeGateArt, width) + GateHalf(gate, width) + GatesApart * width;
        }
    }
    bool CanGoBack => level >= 2 && level - 1 < s_prefabs.Length && (level - 1 <= 1 || s_prefabs[level - 1]);
    LevelPortal BackLook => level - 1 >= 2 ? (s_prefabs[level - 1] ? s_prefabs[level - 1].GetComponentInChildren<LevelPortal>(true) : null)
        : level - 1 == 1 && PitDemoGame.DemoPrefab ? PitDemoGame.DemoPrefab.GetComponentInChildren<LevelPortal>(true) : null;
    // This level's own torii, where she starts: the level before's way on (its way back), or level 1's homeGate.
    Sprite BackGateArt => BackLook ? BackLook.gate : null;
    Sprite HomeGateArt => level <= 1 ? homeGate : BackGateArt;
    // Level 1's own torii's look on a portal standing in it.
    void LookOfHome(Portal portal)
    {
        portal.gateGlowColor = homeGateGlow;
        portal.swirlTint = homeGateSwirl;
        portal.backlightArt = homeGateGlowArt;
        portal.backlight = Color.white;
        portal.grid = homeGateGrid;
        portal.smoke = Color.clear;
        portal.embers = null;
    }
    // The gate she came into this level through (and its look), stood again where it was - by the
    // campfire - each time the level starts over after a death: scenery only, no swirl.
    static Sprite s_stayGate;
    static Color s_staySmoke;
    static int s_stayLevel;
    static (Color glow, Color swirl, Sprite[] fairies, int frames, int count, Sprite[] activate, Sprite[] idle, Sprite[] close, Color backlight, Sprite backlightArt, GateMotes.Kind motes, Color motesColor) s_stayLook;

    /// ARcade: the level's all in place - the road scrolled to where she arrives, clear of the campfire - and its picture
    /// can be shown (every start of a run holds it dark till then: MobileRetroDepthLayerStack).
    public static bool LaidOut;
    bool _arriving, _arrivePlaced;
    int _gatesComing;   // (the gates opening as the level starts, not yet drawn: Open, GateDrawn)
    IEnumerator MarkLaidOut()
    {
        for (int i = 0; i < 3; i++) yield return null;   // the demo lays itself out first
        while (_clearing || (_arriving && !_arrivePlaced) || _gatesComing > 0) yield return null;
        yield return null;   // (and draws it so)
        LaidOut = true;
    }

    // A gate opened as the level starts: built (its Start) and drawn two frames on.
    IEnumerator GateDrawn()
    {
        yield return null;
        yield return null;
        _gatesComing = Mathf.Max(0, _gatesComing - 1);
    }

    DeathRespawn _witch;
    PlayerScriptARIANAClips _player;
    Transform _body;
    SpriteRenderer _bodySprite;
    bool _hidden, _visiting;

    void Awake()
    {
        _witch = GetComponent<DeathRespawn>();
        _player = GetComponentInChildren<PlayerScriptARIANAClips>(true);
        _body = _player ? _player.transform.Find("ARIANA") : null;
        _bodySprite = _body ? _body.GetComponent<SpriteRenderer>() : null;
        // (every level's prefab by its number, for the ways back: level 1 knows them all, along its chain)
        if (level <= 1)
        {
            var on = this;
            for (int k = 0; on && on.nextLevel && k < 8; k++)
            {
                if (on.level + 1 < s_prefabs.Length) s_prefabs[on.level + 1] = on.nextLevel;
                on = on.nextLevel.GetComponentInChildren<LevelPortal>(true);
            }
        }
        else if (level < s_prefabs.Length && PitDemoGame.LevelPrefab) s_prefabs[level] = PitDemoGame.LevelPrefab;
        bool intro = level <= 1 && homeGate && PitDemoGame.NewGame && !s_arriveByPortal;
        PitDemoGame.NewGame = false;
        if (intro)
        {
            // A new game: out of level 1's own torii, its portal open (under the fade in), then the witch's welcome; it
            // closes behind her and stands - and the way on to its left, if it's open.
            s_arriveGate = homeGate;
            s_arriveLook = (homeGateGlow, homeGateSwirl, null, 0, 0, null, null, null, Color.white, homeGateGlowArt, GateMotes.Kind.None, Color.white);
            s_arriveSmoke = Color.clear;
            s_arriveEmbers = null;
            s_arriveEmberGlow = null;
            s_arriveBack = false;
            s_arriveByPortal = true;
            IntroArriving = true;
        }
        if (s_arriveByPortal)
        {
            s_arriveByPortal = false;
            _hidden = true;
            HidePlayer();
            _arriving = true;
            _gatesComing++;
            StartCoroutine(Arrive());
            if (intro && nextLevel && PlayerPrefs.GetInt(OpenedKey, 0) == 1) { _gatesComing++; StartCoroutine(Waiting()); }
        }
        else
        {
            bool onward = nextLevel && PlayerPrefs.GetInt(OpenedKey, 0) == 1;
            bool home = level <= 1 && homeGate;   // (level 1's own torii, standing closed where she starts)
            bool stay = !home && s_stayGate && s_stayLevel == PitDemoGame.Level;
            var scenery0 = GetComponentInChildren<ParallaxLevelBackground>(true);
            if ((onward || CanGoBack || stay || home) && !(scenery0 && scenery0.StandingGate))
                StartCoroutine(ClearFire(HomeGateArt ? HomeGateArt : onward ? gate : s_stayGate));
            if (CanGoBack) { _gatesComing++; StartCoroutine(WayBack(onward)); }   // (the way back - this level's own torii - open where she starts)
            else if (home) { _gatesComing++; StartCoroutine(StandHome(0f)); }
            else if (stay) { _gatesComing++; StartCoroutine(StandArrivalGate()); }
            if (onward) { _gatesComing++; StartCoroutine(Waiting()); }
        }
        StartCoroutine(MarkLaidOut());
    }

    void OnEnable() { FruitTracker.AllCollected += OnAllFruit; CrystalPrize.Collected += OnCrystal; }
    void OnDisable() { FruitTracker.AllCollected -= OnAllFruit; CrystalPrize.Collected -= OnCrystal; }

    // ARcade: the dark crystal's fed on all she gathered (the final dragon's): the witch comes, tells her what it was, and sends her
    // back to the beginning - her lightning on the knight, and level 1 again.
    void OnCrystal() { if (_witch && _player && !_ending) StartCoroutine(Ending()); }
    bool _ending;
    const float EndingBoltFade = 0.25f;   // (the knight's gone this long before the ending's bolt ends)
    IEnumerator Ending()
    {
        _ending = true;
        _visiting = true;
        PitDemoGame.ControlsLocked = true;
        EndlessKnightDirector.Hold = true;
        yield return new WaitForSeconds(1.2f);
        while (DemoDialogue.Showing || PitDemoGame.IsPaused) yield return null;
        float z = _player.transform.position.z;
        var witch = _witch.MakeWitch(BodyX + _witch.witchOffset);
        witch.gameObject.AddComponent<RoadAnchor>();
        var arrival = _witch.Strike(_witch.arriveStrike, new Vector3(witch.bounds.center.x, SpikeTrap.GroundY, z));
        yield return new WaitForSeconds(_witch.Impact(arrival));
        witch.enabled = true;
        var idle = StartCoroutine(Idle(witch));
        yield return new WaitForSeconds(0.8f);
        DemoDialogue.ShowEnding();
        while (DemoDialogue.Showing) yield return null;
        // Her lightning on the knight, as the knight casts the magic sword: she's seen through the bolt till it fades.
        StopCoroutine(idle);
        _player.CastSwordForShow();
        var cast = _witch.castFrames;
        float castLength = cast != null && cast.Length > 0 ? cast.Length / _witch.castFps : 0f;
        // (as a death's: the blue strike on her feet, her gone the moment it hits, the bolt played out)
        bool struck = false, hit = false;
        float impactAt = float.MaxValue, boltEnd = float.MaxValue;
        for (float t = 0f; t < castLength || !struck || Time.time < boltEnd; t += Time.deltaTime)
        {
            int f = (int)(t * _witch.castFps);
            if (t < castLength) witch.sprite = cast[Mathf.Min(f, cast.Length - 1)];
            else _witch.Idle(witch, t - castLength);
            if (!struck && (f >= _witch.castStrikeFrame || castLength == 0f))
            {
                struck = true;
                var bolt = _witch.Strike(_witch.strike, new Vector3(BodyX, SpikeTrap.PlayerFeetY, z));
                impactAt = Time.time + _witch.Impact(bolt);
                boltEnd = Time.time + (bolt ? bolt.frames.Length / bolt.fps : 0f);
            }
            if (struck && !hit && Time.time >= impactAt)
            {
                hit = true;
                SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Heavy);
            }
            if (struck && Time.time >= boltEnd - EndingBoltFade) _hidden = true;
            yield return null;
        }
        _hidden = true;
        yield return new WaitForSeconds(_witch.vanishTime);
        DeathRespawn.ArriveByLightning();   // (back at the campfire in her strike, as after a death)
        CrystalPrize.Coming = false;
        DemoDialogue.ReplayOpening = true;   // (the new knight gets the witch's welcome back)
        PitDemoGame.GoToLevel(null, 1);
    }

    void LateUpdate()
    {
        if (_hidden) HidePlayer();
        ApplyEffect();
    }
    void HidePlayer() { if (_bodySprite) _bodySprite.enabled = false; }

    // (The last level, with no next one, has her portal go back to level 1: the loop goes round, score and all.)
    void OnAllFruit(bool rained)
    {
        if (_visiting || !_witch || !_player || CrystalPrize.Coming) return;   // (the final dragon's fallen: no witch till the crystal's taken, no gate at all)
        StartCoroutine(Visit(rained));
    }

    float BodyX => _bodySprite ? _bodySprite.bounds.center.x : _player.transform.position.x;

    // ARcade: a gate opening where she stands stands clear of the campfire (to its left, half its width and a
    // little more) - the road scrolled back till it is (as her walking would), so she's still in it.
    const float FireMargin = 0.03f, PlainHalf = 0.12f;   // (picture widths)
    float ClearOfFire(Sprite gate, float further = 0f)
    {
        var fire = transform.root.Find("BONFIRE");
        var art = fire ? fire.GetComponent<SpriteRenderer>() : null;
        var view = PitDemoGame.HudCamera;
        if (!art || !view) return 0f;
        float width = view.orthographicSize * 2f * view.aspect;
        // (level 1's own torii by the torii itself, its right post at the fire - its palms behind may reach past)
        bool home = gate && gate == homeGate;
        float half = home ? HomeToriiHalf / gate.pixelsPerUnit * gateScale * (SpikeTrap.PixelSize / 0.01f * portalScale) : GateHalf(gate, width);
        return Mathf.Max(0f, further + half + (home ? 0f : FireMargin * width) - (art.bounds.min.x - BodyX));
    }
    // A gate's half width in the world (its art at gateScale, on a portal at its own scale); a plain portal's, as a picture share.
    float GateHalf(Sprite gate, float pictureWidth) => gate ? gate.bounds.extents.x * gateScale * (SpikeTrap.PixelSize / 0.01f * portalScale) : PlainHalf * pictureWidth;
    const float HomeToriiHalf = 34f;   // (level 1's own torii's half width without its palms, in its pixels)
    bool _clearing;
    IEnumerator ClearFire(Sprite gate)
    {
        _clearing = true;
        for (int i = 0; i < 3; i++) yield return null;   // the demo lays itself out first
        float back = _bodySprite ? ClearOfFire(gate) : 0f;
        if (back > 0f && EndlessKnightDirector.RoadUnitsPerFrame > 0f)
        {
            _player.ScrollRoad(-back / EndlessKnightDirector.RoadUnitsPerFrame);
            yield return null;
        }
        _clearing = false;
    }

    IEnumerator Visit(bool rained)
    {
        _visiting = true;
        // She stands still from the last fruit till the portal's open (then walks into it) - and nothing's
        // left on the road to get at her meanwhile.
        PitDemoGame.ControlsLocked = true;
        EndlessKnightDirector.Hold = true;
        var clearing = GetComponentInChildren<EndlessKnightDirector>(true);
        if (clearing) clearing.ClearRoad();
        // After the fruit rain; not over another box, and not while she's down.
        yield return new WaitForSeconds(rained ? 5f : 1f);
        while (DemoDialogue.Showing || PitDemoGame.IsPaused) yield return null;
        if (_player.IsDead) { _visiting = false; PitDemoGame.ControlsLocked = false; EndlessKnightDirector.Hold = false; yield break; }
        EndlessKnightDirector.Hold = true;
        var director = GetComponentInChildren<EndlessKnightDirector>(true);
        if (director) director.ClearRoad();

        // The witch, in a yellow strike, to her right.
        float z = _player.transform.position.z;
        var witch = _witch.MakeWitch(BodyX + _witch.witchOffset);
        witch.gameObject.AddComponent<RoadAnchor>();
        var arrival = _witch.Strike(_witch.arriveStrike, new Vector3(witch.bounds.center.x, SpikeTrap.GroundY, z));
        yield return new WaitForSeconds(_witch.Impact(arrival));
        witch.enabled = true;
        var idle = StartCoroutine(Idle(witch));
        yield return new WaitForSeconds(0.8f);

        // Her offer.
        int answer = 0;   // 1 yes (the only answer)
        DemoDialogue.AskPortal(nextLevel ? level + 1 : 1, () => answer = 1);
        while (DemoDialogue.Showing && answer == 0) yield return null;
        if (_player.IsDead) { PitDemoGame.ControlsLocked = false; yield break; }

        if (answer != 1)
        {
            // (The box gave way to another before the yes: she goes as she came, and the run goes on.)
            yield return new WaitForSeconds(0.3f);
            var leaving = _witch.Strike(_witch.arriveStrike, new Vector3(witch.bounds.center.x, SpikeTrap.GroundY, z));
            yield return new WaitForSeconds(_witch.Impact(leaving));
            StopCoroutine(idle);
            Destroy(witch.gameObject);
            EndlessKnightDirector.Hold = false;
            PitDemoGame.ControlsLocked = false;
            _visiting = false;
            yield break;
        }

        // YES: she casts; the bolt lands behind them and opens the portal.
        StopCoroutine(idle);
        // To the witch's right, near the edge of the picture (kept relative
        // to her as the road scrolls).
        float fromWitch = portalOffset;
        var view = PitDemoGame.HudCamera;
        if (view)
        {
            float outer = 0f;
            foreach (float ring in RingsNow) outer = Mathf.Max(outer, ring);
            float reach = ((21f + 1f + outer) * portalScale + 8f * fruitScale) * SpikeTrap.PixelSize;   // its middle to its outer fruit's far edge
            float right = view.transform.position.x + view.orthographicSize * view.aspect - reach;
            fromWitch = Mathf.Max(0.45f, Mathf.Min(fromWitch, right - witch.bounds.center.x));   // (never over her)
        }
        // In a boss arena (level 3's courtyard): over its own torii, in its middle, standing in its place.
        var arena = FindAnyObjectByType<BossArena>();
        if (arena && arena.MiddleX > arena.ViewMinX && arena.MiddleX < arena.ViewMaxX) fromWitch = arena.MiddleX - witch.bounds.center.x;
        Portal portal = null;
        var cast = _witch.castFrames;
        float castLength = cast != null && cast.Length > 0 ? cast.Length / _witch.castFps : 0f;
        bool struck = false, hit = false;
        float impactAt = float.MaxValue;
        for (float t = 0f; t < castLength || portal == null; t += Time.deltaTime)
        {
            int f = (int)(t * _witch.castFps);
            if (t < castLength) witch.sprite = cast[Mathf.Min(f, cast.Length - 1)];
            else _witch.Idle(witch, t - castLength);
            if (!struck && (f >= _witch.castStrikeFrame || castLength == 0f))
            {
                struck = true;
                var bolt = _witch.Strike(_witch.strike, new Vector3(witch.bounds.center.x + fromWitch, SpikeTrap.GroundY, z));
                impactAt = Time.time + _witch.Impact(bolt);
                if (bolt) bolt.gameObject.AddComponent<RoadAnchor>();
            }
            if (struck && portal == null && Time.time >= impactAt)
                portal = Open(witch.bounds.center.x + fromWitch, z, true, gate: gate);
            yield return null;
        }
        idle = StartCoroutine(Idle(witch));
        while (!portal.IsOpen) yield return null;
        PitDemoGame.ControlsLocked = false;   // (hers again: UP in front of it)

        // Opened once, it stays open: it waits at the start of every run of
        // this level from now on (till RESTART wipes the progress).
        PlayerPrefs.SetInt(OpenedKey, 1);
        PlayerPrefs.Save();
        yield return StepIn(portal);
    }

    /// The portal's been opened (ARcade's RESTART wipes it).
    public const string OpenKey = "EndlessKnight.PortalOpen";
    /// This level's (level 1's is OpenKey, as it always was; level 2's OpenKey + "2" ...).
    string OpenedKey => level <= 1 ? OpenKey : OpenKey + level;

    // She steps in (UP in front of it), shrinks into it, it closes, and the next level starts.
    IEnumerator StepIn(Portal portal)
    {
        var hint = Hint(portal);
        while (true)
        {
            if (_player.IsDead) yield break;
            bool inFront = Mathf.Abs(BodyX - portal.MiddleX) < enterRange;   // (her drawn middle at its very middle)
            if (hint) hint.enabled = inFront ? Blink() : true;
            if (inFront && !PitDemoGame.IsPaused && ArcadeInput.IsPressed((uint)RetroPadButton.Up)) break;
            yield return null;
        }
        if (hint) Destroy(hint.gameObject);
        PitDemoGame.ControlsLocked = true;
        PitAudio.PlayClip(enterSound);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Heavy);   // through

        // Shrinking and fading into it, about her middle.
        _effectSize = _effectAlpha = 1f;
        BeginEffect();
        for (float t = 0f; t < shrinkTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / shrinkTime);
            _effectSize = 1f - k;
            _effectAlpha = 1f - k;
            yield return null;
        }
        EndEffect();   // (she stays hidden)
        yield return new WaitForSeconds(0.25f);
        yield return portal.Close();
        yield return new WaitForSeconds(0.3f);
        s_arriveByPortal = true;
        s_arriveGate = portal.gate;
        s_arriveSmoke = portal.smoke;
        s_arriveEmbers = portal.embers;
        s_arriveEmberGlow = portal.emberGlow;
        s_arriveLook = (portal.gateGlowColor, portal.swirlTint, portal.fairies, portal.fairyFrames, portal.fairyCount, portal.activateFrames, portal.idleFrames, portal.closeFrames, portal.backlight, portal.backlightArt, portal.motes, portal.motesColor);
        if (portal.back)
        {
            // Back to the level before: she comes out of its way on (Arrive), left open.
            s_arriveBack = true;
            PitDemoGame.GoToLevel(level - 1 <= 1 ? null : s_prefabs[level - 1], level - 1);
        }
        else if (portal.toLevel) PitDemoGame.GoToLevel(portal.toLevel, portal.toLevelNumber);
        else if (nextLevel) PitDemoGame.GoToLevel(nextLevel, level + 1);
        else PitDemoGame.GoToLevel(null, 1);   // (the last level's: back to the first, its prefab the demo's own)
    }

    // Opened before: at the start of the run it's there behind her, open,
    // waiting for her to step in (or walk on).
    IEnumerator Waiting()
    {
        for (int i = 0; i < 3; i++) yield return null;   // the demo lays itself out first
        while (_clearing) yield return null;   // (and the road's back from the fire)
        if (!_bodySprite) yield break;
        _visiting = true;   // (no visit from the witch to open it again)
        while (_arriving && !_arrivePlaced) yield return null;
        var portal = Open(BodyX - (HomeGateArt ? BackGap : 0f), _player.transform.position.z, true, gate: gate);   // (left of this level's own torii)
        while (!portal.IsOpen) yield return null;
        yield return StepIn(portal);
    }

    // Her shrinking or growing about her middle, and fading: a stand-in -
    // her sprite as it is each frame, drawn in a plain sprite material (hers
    // ignores the sprite's colour) - in her place while it lasts, her own
    // sprite hidden (her animations own its transform). Applied last thing
    // each frame, at `_effectSize` of her and `_effectAlpha` seen.
    bool _effect;
    float _effectSize = 1f, _effectAlpha = 1f;
    SpriteRenderer _standIn;
    static Material s_fadeMaterial;

    void BeginEffect()
    {
        if (!s_fadeMaterial) s_fadeMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Knight Fade" };
        var go = new GameObject("Knight (portal)");
        go.layer = _body.gameObject.layer;
        go.transform.SetParent(_body.parent, false);
        _standIn = go.AddComponent<SpriteRenderer>();
        _standIn.sharedMaterial = s_fadeMaterial;
        _standIn.sortingLayerID = _bodySprite.sortingLayerID;
        _standIn.sortingOrder = _bodySprite.sortingOrder;
        _hidden = true;
        HidePlayer();
        _effect = true;
        ApplyEffect();
    }

    void EndEffect()
    {
        _effect = false;
        if (_standIn) Destroy(_standIn.gameObject);
    }

    void ApplyEffect()
    {
        if (!_effect || !_standIn) return;
        var t = _standIn.transform;
        _standIn.sprite = _bodySprite.sprite;
        _standIn.flipX = _bodySprite.flipX;
        t.localPosition = _body.localPosition;
        t.localRotation = _body.localRotation;
        t.localScale = _body.localScale;
        // Her middle at full size; then her at her size, with it there.
        var middle = _standIn.bounds.center;
        t.localScale = _body.localScale * Mathf.Max(0.001f, _effectSize);
        t.position += middle - _standIn.bounds.center;
        // (in the level's light, as she is once she's out: no brighter, then a pop dark - LevelLit)
        var lit = LevelLit.Light;
        _standIn.color = new Color(lit.r, lit.g, lit.b, Mathf.Clamp01(_effectAlpha));
        _standIn.enabled = _effectSize > 0.01f && _effectAlpha > 0.01f;
    }

    // Blinking, for the UP hint in front of the portal.
    static bool Blink() => Mathf.Repeat(Time.unscaledTime * 2.5f, 1f) < 0.6f;

    IEnumerator Idle(SpriteRenderer witch)
    {
        for (float t = 0f; witch; t += Time.deltaTime)
        {
            _witch.Idle(witch, t);
            yield return null;
        }
    }

    // In the new level: the portal opens where she stands, she steps out of
    // it, and it closes behind her.
    IEnumerator Arrive()
    {
        for (int i = 0; i < 3; i++) yield return null;   // the demo lays itself out first
        // ARcade: not behind the game list (a cabinet just placed opens on it) - no portal, no sound,
        // till ENDLESS KNIGHT's picked and the list's gone (another game picked: the demo goes, and this with it).
        while (SpatialEmulator.UI.GamePicker.IsOpen) yield return null;
        if (!_bodySprite) { _arrivePlaced = true; _gatesComing = 0; yield break; }
        EndlessKnightDirector.Hold = true;
        PitDemoGame.ControlsLocked = true;
        var arriveGate = s_arriveGate;
        var arriveLook = s_arriveLook;
        s_arriveGate = null;
        bool cameBack = s_arriveBack;
        s_arriveBack = false;
        // (remembered, to stand again if the level starts over)
        s_stayGate = arriveGate;
        s_stayLook = arriveLook;
        s_staySmoke = s_arriveSmoke;
        s_stayEmbers = s_arriveEmbers;
        s_stayEmberGlow = s_arriveEmberGlow;
        s_stayLevel = PitDemoGame.Level;
        // A level with a gate of its own standing (level 3's torii): the swirl opens in that.
        var scenery = GetComponentInChildren<ParallaxLevelBackground>(true);
        var standing = arriveGate && scenery && !cameBack ? scenery.StandingGate : null;
        // She steps out of it wherever it stands - she can't walk that far
        // left, so the road scrolls back (as her walking would) till the gate
        // is where she is (her body's own place: its sprite's hidden, and a
        // hidden renderer's bounds aren't hers).
        if (standing && _body && EndlessKnightDirector.RoadUnitsPerFrame > 0f)
        {
            _player.ScrollRoad((standing.position.x - _body.position.x) / EndlessKnightDirector.RoadUnitsPerFrame);
            yield return null;   // (the road, its gate and everything on it laid out there)
        }
        else if (EndlessKnightDirector.RoadUnitsPerFrame > 0f && ClearOfFire(cameBack && nextLevel && HomeGateArt ? HomeGateArt : arriveGate,
                     cameBack && nextLevel && HomeGateArt ? BackGap : 0f) is float back && back > 0f)
        {
            _player.ScrollRoad(-back / EndlessKnightDirector.RoadUnitsPerFrame);
            yield return null;
        }
        if (cameBack && nextLevel && CanGoBack) { _gatesComing++; StartCoroutine(WayBack(true, now: true)); }   // (beside it, there from the start)
        else if (cameBack && nextLevel && homeGate && level <= 1) { _gatesComing++; StartCoroutine(StandHome(BackGap)); }   // (level 1's own torii, right of it)
        _arrivePlaced = true;   // (laid out: MarkLaidOut)
        bool fromHome = arriveGate && arriveGate == homeGate && level <= 1;   // (a new game: out of level 1's own torii)
        var portal = Open(standing ? standing.position.x : BodyX, _player.transform.position.z, true, gate: arriveGate);
        if (fromHome) portal.grid = homeGateGrid;
        if (arriveGate)
        {
            // As it looked where she went in.
            portal.gateGlowColor = arriveLook.glow;
            portal.swirlTint = arriveLook.swirl;
            portal.fairies = arriveLook.fairies;
            portal.fairyFrames = arriveLook.frames;
            portal.fairyCount = arriveLook.count;
            portal.activateFrames = arriveLook.activate;
            portal.idleFrames = arriveLook.idle;
            portal.closeFrames = arriveLook.close;
            portal.backlight = arriveLook.backlight;
            portal.backlightArt = arriveLook.backlightArt;
            portal.motes = arriveLook.motes;
            portal.motesColor = arriveLook.motesColor;
            portal.smoke = s_arriveSmoke;
            portal.embers = s_arriveEmbers;
            portal.emberGlow = s_arriveEmberGlow;
        }
        if (standing)
        {
            portal.standingGate = true;   // (no gate of its own)
            portal.gatePixel = scenery.StandingGatePixel;
            portal.gateRenderer = standing.GetComponent<SpriteRenderer>();
        }
        while (!portal.IsOpen) yield return null;
        yield return new WaitForSeconds(0.2f);
        PitAudio.PlayClip(enterSound);
        // Growing and fading in out of it, about her middle.
        _effectSize = _effectAlpha = 0f;
        BeginEffect();
        for (float t = 0f; t < shrinkTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / shrinkTime);
            _effectSize = k;
            _effectAlpha = k;
            yield return null;
        }
        EndEffect();
        _hidden = false;
        _bodySprite.enabled = true;
        PitDemoGame.ControlsLocked = false;
        EndlessKnightDirector.Hold = false;
        // It stays open: come back from the next level, it's this one's way on (and the way back opens beside it);
        // come in from the level before, it's the way back there.
        if (cameBack && nextLevel)
        {
            yield return StepIn(portal);
            yield break;
        }
        if (fromHome) IntroArriving = false;   // (out: the witch's welcome)
        if (!cameBack && CanGoBack)
        {
            portal.back = true;
            yield return StepIn(portal);
            yield break;
        }
        if (fromHome) yield break;   // (level 1's own torii stays open - its grid, its sparks - going nowhere)
        yield return new WaitForSeconds(0.6f);
        yield return portal.Close();
        if (!portal.gate) Destroy(portal.gameObject);   // (a gate stays standing, its swirl gone)
    }

    // The way back to the level before, open by the campfire where she came in - as the way she came through looked
    // (that level's own gate, or the plain portal) - a little to the left of this level's way on if that's open too.
    IEnumerator WayBack(bool besideOnward, bool now = false)
    {
        if (!now)
        {
            for (int i = 0; i < 3; i++) yield return null;   // the demo lays itself out first
            while (_hidden || _clearing) yield return null;
        }
        if (!_bodySprite) yield break;
        var scenery = GetComponentInChildren<ParallaxLevelBackground>(true);
        var standing = scenery ? scenery.StandingGate : null;
        var prev = BackLook;
        float x = BodyX;   // (clear of the campfire: ClearFire, or Arrive's)
        if (standing) x = standing.position.x;   // (a level with a gate of its own standing: in that)
        else if (besideOnward && now) x += BackGap;   // (come back: right of the way on she stepped out of)
        var portal = Open(x, _player.transform.position.z, false, quiet: true, gate: BackGateArt, lookOf: prev);
        if (standing)
        {
            portal.standingGate = true;
            portal.gatePixel = scenery.StandingGatePixel;
            portal.gateRenderer = standing.GetComponent<SpriteRenderer>();
        }
        portal.back = true;
        while (!portal.IsOpen) yield return null;
        yield return StepIn(portal);
    }

    // Level 1's own torii standing, open (its grid, its sparks) but going nowhere - where she starts (`right`: that far
    // right of her, the way on she's come back by).
    IEnumerator StandHome(float right)
    {
        for (int i = 0; i < 3; i++) yield return null;   // the demo lays itself out first
        while (_clearing || (_arriving && !_arrivePlaced)) yield return null;
        if (!_bodySprite) { _gatesComing = Mathf.Max(0, _gatesComing - 1); yield break; }
        var portal = Open(BodyX + right, _player.transform.position.z, false, quiet: true, gate: homeGate);
        LookOfHome(portal);
    }

    // Started over after a death on a level she came into by a gate: that gate stands where she came
    // out of it, by the campfire, as scenery - its glow, its fairies and motes, no swirl, no way in.
    IEnumerator StandArrivalGate()
    {
        for (int i = 0; i < 3; i++) yield return null;   // the demo lays itself out first
        if (!_bodySprite) yield break;
        var scenery = GetComponentInChildren<ParallaxLevelBackground>(true);
        if (scenery && scenery.StandingGate) { _gatesComing--; yield break; }   // (a level with its own gate standing has it already)
        while (_clearing) yield return null;
        float x = BodyX;   // (clear of the campfire: ClearFire)
        var portal = Open(x, _player.transform.position.z, false, quiet: true, gate: s_stayGate);
        var look = s_stayLook;
        portal.gateGlowColor = look.glow;
        portal.swirlTint = look.swirl;
        portal.fairies = look.fairies;
        portal.fairyFrames = look.frames;
        portal.fairyCount = look.count;
        portal.activateFrames = look.activate;
        portal.idleFrames = look.idle;
        portal.closeFrames = look.close;
        portal.backlight = look.backlight;
        portal.backlightArt = look.backlightArt;
        portal.motes = look.motes;
        portal.motesColor = look.motesColor;
        portal.smoke = s_staySmoke;
        portal.embers = s_stayEmbers;
        portal.emberGlow = s_stayEmberGlow;
        portal.sceneryOnly = true;
    }

    // Exits (the witch's, the waiting one, the secret one) are this level's
    // gate if it has one; an arrival, the gate she came through.
    Portal Open(float x, float z, bool withFruit, bool quiet = false, Sprite gate = null, LevelPortal lookOf = null)
    {
        var look = lookOf ? lookOf : this;
        if (_gatesComing > 0) StartCoroutine(GateDrawn());
        var go = new GameObject("PORTAL");
        // (on the characters' layer - the campfire's: under her own layer, so she's always over it, and the campfire
        // drawn over its gate - not hers)
        int actors = LayerMask.NameToLayer("PitActors");
        go.layer = actors >= 0 ? actors : _bodySprite ? _bodySprite.gameObject.layer : gameObject.layer;
        go.transform.SetParent(transform, true);
        go.transform.position = new Vector3(x, SpikeTrap.GroundY, z + 0.01f);
        var portal = go.AddComponent<Portal>();
        portal.owner = this;
        portal.openAtOnce = _gatesComing > 0;   // (there as the level starts: already open, under its fade in)
        portal.gate = gate;
        if (gate) withFruit = false;   // (a gate stands alone)
        if (gate == look.gate && gate)
        {
            // The level's own gate, its own look.
            portal.gateGlowColor = look.gateGlow;
            portal.swirlTint = look.gateSwirlTint;
            portal.fairies = look.gateFairies;
            portal.fairyFrames = look.fairyFrames;
            portal.fairyCount = look.fairyCount;
            portal.activateFrames = look.gateActivateFrames;
            portal.idleFrames = look.gateIdleFrames;
            portal.closeFrames = look.gateCloseFrames;
            portal.backlight = look.gateBacklight;
            portal.backlightArt = look.gateBacklightArt;
            portal.motes = look.gateMotes;
            portal.motesColor = look.gateMotesColor;
            portal.smoke = look.gateSmoke;
            portal.embers = look.gateEmbers;
            portal.emberGlow = look.gateEmberGlow;
        }
        var tracker = GetComponentInChildren<FruitTracker>(true);
        portal.fruit = !withFruit ? null : ringFruit != null && ringFruit.Length > 0 ? ringFruit : tracker ? tracker.fruit : null;
        if (!quiet) PitAudio.PlayClip(openSound);
        return portal;
    }

    TextMeshPro Hint(Portal portal)
    {
        var hud = GetComponentInChildren<HudJuice>(true);
        if (!hud || !hud.healthText) return null;
        var go = new GameObject("Enter Hint");
        go.layer = portal.gameObject.layer;
        go.transform.SetParent(portal.transform, false);
        var text = go.AddComponent<TextMeshPro>();
        text.font = hud.healthText.font;
        text.text = "PUSH UP";
        text.fontSize = 0.5f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = new Color32(255, 214, 64, 255);
        text.sortingLayerID = SortingLayer.NameToID("TopLayer");
        text.sortingOrder = 50;
        text.rectTransform.sizeDelta = new Vector2(0.6f, 0.1f);
        // Over its top, at the art's own pixel size (2 characters' pixels to the font's 1).
        go.transform.localScale = Vector3.one * (SpikeTrap.PixelSize * 2f / 0.01f / Mathf.Max(1e-4f, portal.transform.lossyScale.x));
        // (Clear of the outer ring of fruit.)
        float outer = 0f;
        foreach (float ring in RingsNow) outer = Mathf.Max(outer, ring);
        go.transform.position = new Vector3(portal.transform.position.x, portal.Top + (outer + 1f) * portal.transform.lossyScale.y * 0.01f + 14f * SpikeTrap.PixelSize, portal.transform.position.z - 0.01f);
        return text;
    }

    // ---- the portal ----

    public class Portal : MonoBehaviour
    {
        public LevelPortal owner;
        /// Where it goes (none: the owner's next level).
        public GameObject toLevel;
        public bool back;   // (the way back to the level before)
        /// An 80s retro grid filling its doorway, rolling (clear: none) - level 2's way back to level 1.
        public Color grid = Color.clear;
        SpriteRenderer _grid;
        static System.Collections.Generic.Dictionary<(int, int), Sprite[]> s_grids;
        Sprite[] _gridFrames;
        float _gridLift, _gridBob;

        // Each torii's doorway (in its own pixels: up off the ground, how tall, how wide) and its grid's colour - its own (each
        // from the middle of its feet, where the ground meets them; behind its gate, so up under its lintel is fine).
        // A plain portal: its swirl art's opening (its pixels).
        static (float lift, float tall, float wide, Color color) Doorway(Sprite gate)
        {
            string n = gate ? gate.name : "";
            if (n.StartsWith("torii_synthwave")) return (5f, 43f, 27f, new Color(1f, 0.3f, 0.8f, 0.85f));   // level 1's: neon pink, down to the ground (the middle of its feet)
            if (n.StartsWith("torii_purple_forest")) return (5f, 40f, 26f, new Color(0.8f, 0.5f, 1f, 0.85f));   // the fairy gate: violet
            if (n.StartsWith("torii_red_bamboo")) return (5f, 45f, 28f, new Color(0.75f, 1f, 0.3f, 0.85f));     // the bamboo gate: its lime neon
            if (n.StartsWith("torii_black_ruined"))   // the ruined gate: ember orange (on level 4, its feet whole on the ground: down to just touching it)
                return PitDemoGame.Level == 4 ? (2f, 50f, 30f, new Color(1f, 0.45f, 0.15f, 0.85f)) : (5f, 47f, 30f, new Color(1f, 0.45f, 0.15f, 0.85f));
            if (gate) return (2f, gate.rect.height * 0.55f, gate.rect.width * 0.25f, Color.clear);
            return (4f, 80f, 38f, Color.clear);
        }
        /// Already open as it's made (a gate there as the level starts: none seen opening - Open).
        public bool openAtOnce;
        public int toLevelNumber;
        public Sprite[] fruit;
        /// A gate (owner.gate's art) round the swirl: standing whole from the start, the swirl and its warm glow opening inside it.
        public Sprite gate;
        /// The gate's already standing in the level (its scenery): none drawn here, the swirl sized to that one (gatePixel: its art pixel, world units).
        public bool standingGate;
        public float gatePixel;
        public SpriteRenderer gateRenderer;
        public Color gateGlowColor = new Color32(255, 200, 90, 255);
        public Color swirlTint = Color.white;
        public Sprite[] fairies;
        public int fairyFrames = 8, fairyCount = 10;
        /// Its swirl's frames (a gate's own palette); empty: the owner's.
        public Sprite[] activateFrames, idleFrames, closeFrames;
        /// A glow behind the gate (its art; none: no glow), tinted.
        public Sprite backlightArt;
        public Color backlight = Color.white;
        /// Little things in the air round the gate.
        public GateMotes.Kind motes;
        public Color motesColor = Color.white;
        /// Smoke curling up off the gate (clear: none).
        public Color smoke = Color.clear;
        /// Burning embers along its outline (none: no embers), and their glow.
        public Sprite[] embers;
        public Sprite emberGlow;
        /// Just the gate standing (by the campfire after a death): no swirl or its glow, ever.
        public bool sceneryOnly;

        void LateUpdate()
        {
            if (!sceneryOnly) return;
            if (_renderer) _renderer.enabled = false;
            if (_halo) _halo.enabled = false;
        }
        SpriteRenderer _backlight;
        Sprite[] Activate => activateFrames != null && activateFrames.Length > 0 ? activateFrames : owner.activateFrames;
        Sprite[] Idle => idleFrames != null && idleFrames.Length > 0 ? idleFrames : owner.idleFrames;
        Sprite[] Shut => closeFrames != null && closeFrames.Length > 0 ? closeFrames : owner.closeFrames;
        SpriteRenderer[] _fairyArt;
        float[] _fairyPhase, _fairySpeed;
        int[] _fairySheet;
        Vector2 _gateHalf;   // the gate's half width and height (local units)
        /// Its fruit rings come first, then it grows out of its middle and fades in among them.
        public bool ringsFirst = true;
        const float RingTime = 0.7f;
        float Delay => ringsFirst ? RingTime : 0f;

        // The art (64 x 128): its frame from x 11 to 53, y 3 to 90 up from the bottom.
        const float ArtHeight = 87f, ArtHalfWidth = 21f;   // (its sprites' pivot: the art's bottom middle)
        // The swirl inside a gate: as tall as its opening (52 of its 70 px, under the crossbeam).
        float SwirlInGate => 52f * owner.gateScale / ArtHeight;
        float _artBase = 1f;
        const int Sorting = -985;   // over the road and the shadows, under everyone on it
        const int FruitSorting = 40;   // its fruit over everyone

        // Its art on a child of its own, so it can shrink away as it closes
        // (before its fruit go); its glow, a pink halo over it, on the art.
        Transform _art;
        SpriteRenderer _renderer, _halo;
        Transform[] _fruit;
        SpriteRenderer[] _fruitArt;
        float[] _rings;
        int[] _ring, _ringCounts, _ringStart;
        float _start, _closeAt = -1f;
        static Sprite s_halo;

        public bool IsOpen => _renderer && Time.time - _start >= Delay + Length(Activate);
        public float Middle => transform.position.y + (ArtHeight * 0.5f) * Pixel;
        public float Top => transform.position.y + ArtHeight * Pixel;
        /// Its middle across (its gate's, if it stands in one).
        public float MiddleX => _gateArt ? _gateArt.bounds.center.x : transform.position.x;
        float Pixel => transform.lossyScale.y * 0.01f;

        float Length(Sprite[] frames) => frames != null ? frames.Length / owner.fps : 0f;

        SpriteRenderer Piece(string name, Transform parent, Material material, int order)
        {
            var go = new GameObject(name);
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = material;
            sr.sortingLayerName = "TopLayer";
            sr.sortingOrder = order;
            return sr;
        }

        void Start()
        {
            transform.localScale = Vector3.one * (SpikeTrap.PixelSize / 0.01f * owner.portalScale);
            gameObject.AddComponent<RoadAnchor>();
            _start = Time.time - (openAtOnce ? Delay + Length(Activate) : 0f);
            var plain = new Material(Shader.Find("Sprites/Default")) { name = "Portal" };
            _art = new GameObject("Art").transform;
            _art.gameObject.layer = gameObject.layer;
            _art.SetParent(transform, false);
            _renderer = Piece("Frames", _art, plain, Sorting);
            _renderer.color = new Color(1f, 1f, 1f, owner.portalOpacity * swirlTint.a);
            // (its doorway's retro grid: every portal's, in its torii's own colour - on the portal itself in the gate's own
            // pixels, drawn over the gate inside its doorway; a plain portal's over its swirl - its swirl faint behind it)
            if (!sceneryOnly)
            {
                var door = Doorway(gate);
                if (grid.a <= 0f) grid = door.color.a > 0f ? door.color : new Color(owner.glowColor.r, owner.glowColor.g, owner.glowColor.b, 0.85f);
                swirlTint.a = Mathf.Min(swirlTint.a, 0.4f);
                _grid = Piece("Grid", gate ? transform : _art, plain, Sorting + 1);   // (a gate's behind its art: Sorting + 2)
                // (texels a gate pixel; a gate pixel in the grid's parent's units)
                float unit = !gate ? 0.01f : standingGate && gatePixel > 0f ? gatePixel / Mathf.Max(1e-5f, transform.lossyScale.y) : 0.01f * owner.gateScale;
                float density = gate ? GridDensity : 1f;
                s_grids ??= new System.Collections.Generic.Dictionary<(int, int), Sprite[]>();
                var size = (Mathf.RoundToInt(door.wide * density), Mathf.RoundToInt(door.tall * density));
                if (!s_grids.TryGetValue(size, out _gridFrames)) s_grids[size] = _gridFrames = GridFrames(size.Item1, size.Item2);
                _grid.transform.localScale = Vector3.one * (unit * 100f / density);   // (a texel a sprite pixel at 100 a unit)
                _gridLift = door.lift * unit;
                _gridBob = 0.5f * unit;
            }
            if (gate)
            {
                // The gate stands as it is, whole from the start; the swirl
                // (and its glow) open and close inside it, sized to its opening, behind its posts.
                if (!standingGate)
                {
                    var post = Piece("Gate", transform, plain, Sorting + 2);
                    post.sprite = gate;
                    post.transform.localScale = Vector3.one * owner.gateScale;
                    _artBase = SwirlInGate;
                    _gateHalf = new Vector2(gate.bounds.size.x * 0.5f, gate.bounds.size.y * 0.5f) * owner.gateScale;
                    MakeFairies(plain);
                }
                else
                {
                    // The scenery's gate: its opening (52 of its art pixels) in this portal's art pixels.
                    _artBase = gatePixel > 0f ? 52f * gatePixel / (Pixel * ArtHeight) : SwirlInGate;
                }
                _art.localScale = Vector3.one * _artBase;
                _renderer.sortingOrder = Sorting - 2;
                _gateArt = standingGate ? gateRenderer : transform.Find("Gate") ? transform.Find("Gate").GetComponent<SpriteRenderer>() : null;
            }
            // (Over its frames - drawn behind them it doesn't show - tinting them pinker.)
            _halo = Piece("Halo", _art, plain, gate ? Sorting - 1 : Sorting + 1);
            if (standingGate && gateRenderer)
            {
                // Just behind the scenery gate's posts, on its sorting.
                _renderer.sortingLayerID = _halo.sortingLayerID = gateRenderer.sortingLayerID;
                _renderer.sortingOrder = gateRenderer.sortingOrder - 2;
                _halo.sortingOrder = gateRenderer.sortingOrder - 2;
                // (its grid between them and its posts: behind the gate, over its swirl)
                if (_grid) { _grid.sortingLayerID = gateRenderer.sortingLayerID; _grid.sortingOrder = gateRenderer.sortingOrder - 1; }
            }
            _halo.sprite = Halo();
            _halo.transform.localPosition = new Vector3(0f, ArtHeight * 0.5f * 0.01f, 0f);
            if (_gateArt)
            {
                if (backlightArt && !standingGate) Backlight(plain);
                if (!standingGate) GateMotes.Add(_gateArt, motes, motesColor);   // (a standing gate has its own)
                if (_grid && gate && gate.name.StartsWith("torii_synthwave"))
                {
                    // (level 1's own torii: neon pink sparks bursting out of its doorway)
                    GateMotes.Add(_gateArt, GateMotes.Kind.Bursts, new Color(1f, 0.25f, 0.75f, 1f));
                }
                if (!standingGate && smoke.a > 0f) GateMotes.Add(_gateArt, GateMotes.Kind.Smoke, smoke);
                if (!standingGate) GateEmbers.Add(_gateArt, embers, emberGlow);
                CutAtGround();
            }

            if (fruit != null && fruit.Length > 0)
            {
                // Its fruit round it in order, each in its own place (Orbit).
                var list = new System.Collections.Generic.List<Transform>();
                var ringOf = new System.Collections.Generic.List<int>();
                var renderers = new System.Collections.Generic.List<SpriteRenderer>();
                s_fruitHalf = 8f * owner.fruitScale / owner.portalScale;   // (a 16 px fruit)
                _rings = owner.Rings(fruit.Length);
                _ringCounts = new int[_rings.Length];
                _ringStart = new int[_rings.Length];
                int placed = 0;
                for (int r = 0; r < _rings.Length; r++)
                {
                    float ring = _rings[r];
                    // As many as fit round it at the spacing (its edge measured) -
                    // or, with every fruit round it, a share of them by its
                    // length, so all the rings are as full.
                    int count = owner.RingPlaces(ring);
                    if (owner.fitAllFruit && placed < fruit.Length)
                    {
                        float total = 0f, before = 0f;
                        for (int k = 0; k < _rings.Length; k++) { float len = RingPath(_rings[k], out _, out _); total += len; if (k < r) before += len; }
                        float upTo = before + RingPath(ring, out _, out _);
                        count = Mathf.Max(1, Mathf.RoundToInt(fruit.Length * upTo / total) - placed);
                    }
                    _ringStart[r] = placed;
                    placed += count;
                    _ringCounts[r] = count;
                    for (int i = 0; i < count; i++)
                    {
                        var sr = Piece("Fruit", transform, plain, FruitSorting + r);
                        sr.transform.localScale = Vector3.one * (owner.fruitScale / owner.portalScale);
                        list.Add(sr.transform);
                        renderers.Add(sr);
                        ringOf.Add(r);
                    }
                }
                _fruit = list.ToArray();
                _fruitArt = renderers.ToArray();
                _ring = ringOf.ToArray();
            }
            Update();
        }

        SpriteRenderer _gateArt;
        static Sprite s_square;

        // The glow behind the gate: its own art, on it (the same size and
        // foot), behind its swirl.
        void Backlight(Material material)
        {
            _backlight = Piece("Backlight", _gateArt.transform, material, _gateArt.sortingOrder - 6);   // (behind its swirl too)
            _backlight.sortingLayerID = _gateArt.sortingLayerID;
            _backlight.sprite = backlightArt;
            _backlight.transform.localPosition = new Vector3(0f, 0f, 0.001f);
        }

        // The swirl, its glow and the haze stop at the gate's feet (the
        // ground): a mask from there up, the rest of the picture above it.
        void CutAtGround()
        {
            if (!s_square) s_square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 4f);
            var go = new GameObject("Ground cut");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            var mask = go.AddComponent<SpriteMask>();
            mask.sprite = s_square;
            var world = transform.lossyScale;
            // (No wider than the gate: it stays as long as the gate does.)
            go.transform.localScale = new Vector3(_gateArt.bounds.size.x * 1.6f / world.x, 50f / world.y, 1f);
            // (from where its grid starts, the ground: the middle of its feet - its haze no lower than its grid)
            var art = _gateArt.sprite;
            float lift = art ? Doorway(art).lift * _gateArt.bounds.size.y / art.rect.height : 0f;
            go.transform.position = new Vector3(_gateArt.bounds.center.x, _gateArt.bounds.min.y + lift, transform.position.z);
            int low = int.MaxValue, high = int.MinValue;
            foreach (var sr in new[] { _renderer, _halo, _backlight })
                if (sr) { sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask; low = Mathf.Min(low, sr.sortingOrder); high = Mathf.Max(high, sr.sortingOrder); }
            // (Just these: not a level's grass, cleared round a gate by a mask of its own.)
            mask.isCustomRangeActive = true;
            mask.frontSortingLayerID = mask.backSortingLayerID = _renderer.sortingLayerID;
            mask.frontSortingOrder = high;
            mask.backSortingOrder = low - 1;
        }

        // A soft glow the shape of its frame: white, fading out from the
        // frame's edge (made once).
        static Sprite Halo()
        {
            if (s_halo) return s_halo;
            const int w = 48, h = 80;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Distance out from the middle, in a squarish (superellipse) shape: 1 at the edge.
                    float u = Mathf.Abs((x + 0.5f) / w * 2f - 1f), v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                    float d = Mathf.Pow(Mathf.Pow(u, 4f) + Mathf.Pow(v, 4f), 0.25f);
                    // Strongest round the frame's edge (about halfway out),
                    // lighter over its middle so its swirl shows through.
                    float a = Mathf.Sqrt(Mathf.Clamp01(1f - d)) * Mathf.Lerp(0.4f, 1f, Mathf.SmoothStep(0f, 1f, d / 0.5f));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            // Twice as wide as the frame (its art pixels: 100 to a unit).
            s_halo = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w / (ArtHalfWidth * 2f * 2f) * 100f);
            return s_halo;
        }

        void Update()
        {
            if (!_renderer) return;
            float age = Time.time - _start;
            float artAge = Mathf.Max(0f, age - Delay);   // (after its rings, if they come first)
            float open = Length(Activate);
            // Activate, then Idle round and round - or Close, once it's closing.
            if (_closeAt >= 0f)
                _renderer.sprite = Frame(Shut, Time.time - _closeAt, false);
            else if (artAge < open)
                _renderer.sprite = Frame(Activate, artAge, false);
            else
                _renderer.sprite = Frame(Idle, artAge - open, true);

            // Closing: the portal shrinks away into its middle first, its fruit after.
            float shut = _closeAt >= 0f ? Mathf.Clamp01((Time.time - _closeAt) / Mathf.Max(0.01f, Length(Shut))) : 0f;
            float size = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(shut / 0.6f));
            // Rings first: it grows and fades in out of its middle as it activates.
            float appear = ringsFirst ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(artAge / Mathf.Max(0.01f, open))) : 1f;
            if (age < Delay) appear = 0f;
            size *= appear;
            _renderer.color = new Color(swirlTint.r, swirlTint.g, swirlTint.b, owner.portalOpacity * swirlTint.a * appear);   // (its alpha: a gate's swirl fainter)
            _art.localScale = Vector3.one * Mathf.Max(0.001f, size * _artBase);
            _art.localPosition = new Vector3(0f, ArtHeight * 0.5f * 0.01f * _artBase * (1f - size), 0f);

            // The glow, pulsing - coming up as it opens.
            float glow = owner.glowStrength * Mathf.Clamp01(artAge / Mathf.Max(0.01f, open)) * (0.8f + 0.2f * Mathf.Sin(Time.time * 4f));
            var c = gate ? gateGlowColor : owner.glowColor;
            _halo.color = new Color(c.r, c.g, c.b, glow);
            _halo.enabled = size > 0.01f;
            if (_backlight) _backlight.color = new Color(backlight.r, backlight.g, backlight.b, backlight.a * (0.85f + 0.15f * Mathf.Sin(Time.time * 2.2f)));
            _renderer.enabled = size > 0.01f;
            if (_grid)
            {
                // Its retro grid filling the doorway, rolling away from her into it, bobbing a little, as bright as the swirl's come in.
                _grid.sprite = _gridFrames[_gridFrames.Length - 1 - (int)(Time.time * GridFps) % _gridFrames.Length];   // (backwards: rolling away, into the doorway)
                _grid.color = new Color(grid.r, grid.g, grid.b, grid.a * appear * (1f - shut));
                // (in the doorway: on top of its feet, bobbing half a pixel, up to just under its crossbeam)
                float loop = 4f * GridFrameCount / GridFps;
                _grid.transform.localPosition = new Vector3(0f, _gridLift + _gridBob * (1f + Mathf.Sin(Time.time * Mathf.PI * 2f / loop)), -0.002f);
                _grid.enabled = size > 0.01f;
            }
            if (_fruit != null) Orbit(age, open, Mathf.Clamp01((shut - 0.35f) / 0.65f));
            Fairies();
        }

        // The doorway's grid frames (white: tinted): an 80s retro grid floor filling the doorway to its top (as wide as
        // 56 of the swirl art's 87 px from its bottom middle - a little wider than its art, its edges behind the gate's
        // posts), rolling - its squares in solid lines a pixel wide, smaller further off, in a fog thickening toward the far end.
        const int GridFrameCount = 16;
        const float GridFps = 56f;   // (a row on every ~0.29 s; the bob in step with every fourth loop: seamless)
        const float GridDensity = 1.8f;   // (1.8 texels a gate pixel: a line about one of the picture's pixels thick - its 320 px
                                          // render's ~1.9 to a gate pixel; any finer and lines fell between them and broke up)
        static Sprite[] GridFrames(int w, int h)
        {
            const float rows = 4f, cols = 4f, horizon = 1.3f;   // (square cells; the horizon above its top: the floor fills it)
            // A line's brightness at a row: brighter near, into the fog far off; the fog itself, thickening up top.
            float Fog(int y) => Mathf.Pow((y + 0.5f) / h, 1.6f) * 45f;   // (light: the grid through it)
            float Lit(int y)
            {
                float v = (y + 0.5f) / h, t = (horizon - v) / horizon;
                return Mathf.Min(255f, Mathf.Max(Fog(y), (110f + 145f * Mathf.Min(1f, t * 1.6f)) * (1f - 0.55f * v)));
            }
            var all = new Sprite[GridFrameCount];
            var lit = new float[w * h];
            for (int f = 0; f < GridFrameCount; f++)
            {
                float roll = f / (float)GridFrameCount;
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) lit[y * w + x] = Fog(y);
                // its rows: solid lines across, one every 1/rows deep from the nearest, rolling nearer
                for (int n = Mathf.CeilToInt(rows + roll); ; n++)
                {
                    float depth = (n - roll) / rows, v = horizon * (1f - 1f / depth);
                    if (v >= 1f) break;
                    int y = (int)(v * h);
                    if (y < 0) continue;
                    for (int x = 0; x < w; x++) lit[y * w + x] = Mathf.Max(lit[y * w + x], Lit(y));
                }
                // its columns: unbroken lines leaning in to the vanishing point (every step from one row up to the next filled)
                for (int c = -(int)(cols * 3); c <= (int)(cols * 3); c++)
                {
                    int prev = int.MinValue;
                    for (int y = 0; y < h; y++)
                    {
                        float v = (y + 0.5f) / h, depth = horizon / (horizon - v);
                        int x = Mathf.FloorToInt((2f * c / (cols * depth) + 1f) * 0.5f * w);
                        int lo = prev == int.MinValue ? x : Mathf.Min(prev, x), hi = prev == int.MinValue ? x : Mathf.Max(prev, x);
                        for (int xx = Mathf.Max(0, lo); xx <= Mathf.Min(w - 1, hi); xx++) lit[y * w + xx] = Mathf.Max(lit[y * w + xx], Lit(y));
                        prev = x;
                    }
                }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[w * h];
                for (int k = 0; k < px.Length; k++) px[k] = new Color32(255, 255, 255, (byte)Mathf.Clamp(lit[k], 0f, 255f));
                tex.SetPixels32(px); tex.Apply();
                all[f] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f, 0, SpriteMeshType.FullRect);   // (sized by its transform)
            }
            return all;
        }

        // ---- The fairies round a gate ----
        // Little ones (the knight's fairy's art, small), each looping round
        // the gate on its own wide oval - out past its posts and over its top,
        // across in front of its swirl - at its own speed and way round,
        // bobbing, wings beating, never down into the ground.

        void MakeFairies(Material material)
        {
            if (fairies == null || fairies.Length < fairyFrames || fairyFrames <= 0 || fairyCount <= 0) return;
            int sheets = fairies.Length / fairyFrames;
            _fairyArt = new SpriteRenderer[fairyCount];
            _fairyPhase = new float[fairyCount];
            _fairySpeed = new float[fairyCount];
            _fairySheet = new int[fairyCount];
            _fairyReach = new Vector2[fairyCount];
            for (int i = 0; i < fairyCount; i++)
            {
                _fairyArt[i] = Piece("Fairy", transform, material, Sorting + 3);   // (over the gate and its swirl)
                _fairyArt[i].transform.localScale = Vector3.one * FairySize;
                _fairyArt[i].gameObject.AddComponent<LevelLit>();   // (in the level's light, like everyone)
                _fairyPhase[i] = i / (float)fairyCount * Mathf.PI * 2f + Random.value * 0.4f;
                _fairySpeed[i] = Random.Range(0.35f, 0.7f) * (Random.value < 0.5f ? -1f : 1f);   // radians a second
                _fairySheet[i] = i % sheets;
                _fairyReach[i] = new Vector2(Random.Range(0.75f, 1.35f), Random.Range(0.55f, 0.95f));
            }
        }

        const float FairySize = 0.32f;   // of the fairy art's 32 px
        const float FairyLowest = 0.3f;   // the lowest they come (of the gate's height)
        Vector2[] _fairyReach;   // each one's oval: of the gate's half width, and of its half height

        void Fairies()
        {
            if (_fairyArt == null) return;
            float middle = _gateHalf.y * 1.05f, lowest = _gateHalf.y * 2f * FairyLowest;
            for (int i = 0; i < _fairyArt.Length; i++)
            {
                var art = _fairyArt[i];
                if (!art) continue;
                float a = _fairyPhase[i] + Time.time * _fairySpeed[i];
                float t = Time.time * 2.3f + i * 1.7f;
                var at = new Vector2(Mathf.Cos(a) * _gateHalf.x * _fairyReach[i].x,
                                     middle + Mathf.Sin(a) * _gateHalf.y * _fairyReach[i].y);
                at += new Vector2(Mathf.Sin(t) * 0.04f, Mathf.Sin(t * 1.6f) * 0.05f);   // their own little loops
                at.y = Mathf.Max(lowest, at.y);
                art.transform.localPosition = new Vector3(at.x, at.y, -0.002f);
                int frame = (int)(Time.time * 12f + i * 3) % fairyFrames;
                art.sprite = fairies[_fairySheet[i] * fairyFrames + frame];
                // Facing the way they're heading.
                art.flipX = -Mathf.Sin(a) * _fairySpeed[i] < 0f;
            }
        }

        Sprite Frame(Sprite[] frames, float t, bool loop)
        {
            if (frames == null || frames.Length == 0) return null;
            int f = (int)(t * owner.fps);
            return frames[loop ? f % frames.Length : Mathf.Min(f, frames.Length - 1)];
        }

        // A ring's path: a door frame round it - up its left side from the
        // ground, across its top, down its right side to the ground -
        // sampled finely, with how far along each point is, so the fruit can
        // be spaced evenly along it. Returns its length (local units).
        const int PathPoints = 160;
        static readonly System.Collections.Generic.Dictionary<float, (Vector2[] points, float[] along)> _paths = new System.Collections.Generic.Dictionary<float, (Vector2[], float[])>();
        /// The fruit's half-size, in the portal's art pixels (set by the portal; for the frame's foot).
        static float s_fruitHalf = 6f;

        public static float RingPath(float ring, out Vector2[] points, out float[] along)
        {
            if (!_paths.TryGetValue(ring, out var path))
            {
                float halfW = (ArtHalfWidth + 1f + ring) * 0.01f, top = (ArtHeight * 0.5f + 1f + ring) * 0.01f;
                float foot = (-ArtHeight * 0.5f + s_fruitHalf) * 0.01f;   // (from its middle: the lowest fruit on the ground)
                // Its corners, then the points evenly along its three sides.
                var corners = new[] { new Vector2(-halfW, foot), new Vector2(-halfW, top), new Vector2(halfW, top), new Vector2(halfW, foot) };
                float length = 0f;
                for (int k = 1; k < corners.Length; k++) length += Vector2.Distance(corners[k - 1], corners[k]);
                var pts = new Vector2[PathPoints + 1];
                var dist = new float[PathPoints + 1];
                for (int k = 0; k <= PathPoints; k++)
                {
                    float d = k / (float)PathPoints * length;
                    int side = 1;
                    float before = 0f;
                    while (side < corners.Length - 1 && before + Vector2.Distance(corners[side - 1], corners[side]) < d)
                    {
                        before += Vector2.Distance(corners[side - 1], corners[side]);
                        side++;
                    }
                    float seg = Vector2.Distance(corners[side - 1], corners[side]);
                    pts[k] = Vector2.Lerp(corners[side - 1], corners[side], seg > 0f ? Mathf.Clamp01((d - before) / seg) : 0f);
                    dist[k] = d;
                }
                path = (pts, dist);
                _paths[ring] = path;
            }
            points = path.points;
            along = path.along;
            return along[PathPoints];
        }

        // The point `share` of the way round a ring's path.
        static Vector2 Along(Vector2[] points, float[] along, float share)
        {
            float d = Mathf.Clamp01(share) * along[PathPoints];
            int lo = 0, hi = PathPoints;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (along[mid] <= d) lo = mid; else hi = mid; }
            float t = Mathf.InverseLerp(along[lo], along[hi], d);
            return Vector2.Lerp(points[lo], points[hi], t);
        }

        // The fruit standing still round its door frame, upright, each
        // jiggling a little; coming in as it opens and drawn into its middle
        // as it closes.
        void Orbit(float age, float open, float shut)
        {
            float grow = Mathf.Clamp01(age / Mathf.Max(0.01f, ringsFirst ? RingTime : open));
            float reach = grow * (1f - shut);
            float midY = ArtHeight * 0.5f * 0.01f;   // (its pivot is the art's bottom)
            int first = 0;
            for (int i = 0; i < _fruit.Length; i++)
            {
                int r = _ring[i];
                if (i > 0 && r != _ring[i - 1]) first = i;
                RingPath(_rings[r], out var points, out var along);
                // Evenly along its frame, ends on the ground; every one in its own place, in order.
                int slot = i - first, count = _ringCounts[r];
                float share = count > 1 ? slot / (float)(count - 1) : 0.5f;
                var at = Along(points, along, share);
                _fruitArt[i].sprite = fruit[(_ringStart[r] + slot) % fruit.Length];
                // A little jiggle: each its own beat.
                float beat = Time.time * 9f + i * 1.7f;
                float wobble = Mathf.Sin(beat) * owner.jiggleDegrees;
                float bob = Mathf.Sin(beat * 0.5f + 1f) * owner.jigglePixels * 0.01f;
                _fruit[i].localPosition = new Vector3(at.x * reach, midY + (at.y + bob) * reach, -0.001f);
                _fruit[i].localRotation = Quaternion.Euler(0f, 0f, wobble);
                _fruit[i].gameObject.SetActive(reach > 0.05f);
            }
        }

        public IEnumerator Close()
        {
            _closeAt = Time.time;
            yield return new WaitForSeconds(Length(Shut));
            if (_art) _art.gameObject.SetActive(false);
            if (_fruit != null) foreach (var f in _fruit) if (f) f.gameObject.SetActive(false);
        }
    }
}

// ARcade: stays put on the road as it scrolls (the witch and the portal on
// their visit); Moved is how far it has been carried so far.
public class RoadAnchor : MonoBehaviour
{
    float _last;
    public float Moved { get; private set; }

    void Start() { if (EndlessKnightDirector.Player) _last = EndlessKnightDirector.Player.AccumulatedFrames; }

    void Update()
    {
        var player = EndlessKnightDirector.Player;
        if (!player) return;
        float now = player.AccumulatedFrames;
        float dx = (now - _last) * EndlessKnightDirector.RoadUnitsPerFrame;
        _last = now;
        transform.position += Vector3.left * dx;
        Moved -= dx;
    }
}
