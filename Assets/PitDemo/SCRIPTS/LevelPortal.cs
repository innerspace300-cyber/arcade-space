using System.Collections;
using SpatialEmulator.Controls;
using SpatialEmulator.Demo;
using TMPro;
using UnityEngine;

// ARcade: the way out of the loop. Once a run has picked up every fruit
// (FruitTracker - after its fruit rain, the first time), the road clears
// and the witch arrives in a yellow strike beside the knight, like after a
// death. She congratulates her and offers a portal to the next loop
// (DemoDialogue.AskPortal): NO, she leaves the same way and the run goes on
// for a hi score; YES, she casts, a blue strike lands behind them and the
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
    [Header("Stepping in")]
    [Tooltip("How near its middle she must stand to step in (world units).")]
    public float enterRange = 0.22f;
    public float shrinkTime = 0.6f;
    public AudioClip openSound, enterSound;

    // Through the portal: the next level's knight steps out of one.
    static bool s_arriveByPortal;

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
        if (s_arriveByPortal)
        {
            s_arriveByPortal = false;
            _hidden = true;
            HidePlayer();
            StartCoroutine(Arrive());
        }
        else if (nextLevel && PlayerPrefs.GetInt(OpenKey, 0) == 1) StartCoroutine(Waiting());
    }

    void OnEnable() => FruitTracker.AllCollected += OnAllFruit;
    void OnDisable() => FruitTracker.AllCollected -= OnAllFruit;

    void LateUpdate()
    {
        if (_hidden) HidePlayer();
        ApplyEffect();
    }
    void HidePlayer() { if (_bodySprite) _bodySprite.enabled = false; }

    void OnAllFruit(bool rained)
    {
        if (!nextLevel || _visiting || !_witch || !_player) return;
        StartCoroutine(Visit(rained));
    }

    float BodyX => _bodySprite ? _bodySprite.bounds.center.x : _player.transform.position.x;

    IEnumerator Visit(bool rained)
    {
        _visiting = true;
        // After the fruit rain; not over another box, and not while she's down.
        yield return new WaitForSeconds(rained ? 5f : 1f);
        while (DemoDialogue.Showing || PitDemoGame.IsPaused) yield return null;
        if (_player.IsDead) { _visiting = false; yield break; }
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
        int answer = 0;   // 1 yes, -1 no
        DemoDialogue.AskPortal(() => answer = 1, () => answer = -1);
        while (DemoDialogue.Showing && answer == 0) yield return null;
        if (_player.IsDead) yield break;

        if (answer != 1)
        {
            // NO (or the box gave way to another): she goes as she came, and the run goes on.
            yield return new WaitForSeconds(0.3f);
            var leaving = _witch.Strike(_witch.arriveStrike, new Vector3(witch.bounds.center.x, SpikeTrap.GroundY, z));
            yield return new WaitForSeconds(_witch.Impact(leaving));
            StopCoroutine(idle);
            Destroy(witch.gameObject);
            EndlessKnightDirector.Hold = false;
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
        Portal portal = null;
        var cast = _witch.castFrames;
        float castLength = cast != null && cast.Length > 0 ? cast.Length / _witch.castFps : 0f;
        bool struck = false;
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
                portal = Open(witch.bounds.center.x + fromWitch, z, true);
            yield return null;
        }
        idle = StartCoroutine(Idle(witch));
        while (!portal.IsOpen) yield return null;

        // Opened once, it stays open: it waits at the start of every run of
        // this level from now on (till RESTART wipes the progress).
        PlayerPrefs.SetInt(OpenKey, 1);
        PlayerPrefs.Save();
        yield return StepIn(portal);
    }

    /// The portal's been opened (ARcade's RESTART wipes it).
    public const string OpenKey = "EndlessKnight.PortalOpen";

    // She steps in (UP in front of it), shrinks into it, it closes, and the next level starts.
    IEnumerator StepIn(Portal portal)
    {
        var hint = Hint(portal);
        while (true)
        {
            if (_player.IsDead) yield break;
            bool inFront = Mathf.Abs(BodyX - portal.transform.position.x) < enterRange;
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
        PitDemoGame.GoToLevel(nextLevel, level + 1);
    }

    // Opened before: at the start of the run it's there behind her, open,
    // waiting for her to step in (or walk on).
    IEnumerator Waiting()
    {
        for (int i = 0; i < 3; i++) yield return null;   // the demo lays itself out first
        if (!_bodySprite) yield break;
        _visiting = true;   // (no visit from the witch to open it again)
        var portal = Open(BodyX, _player.transform.position.z, true);
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
        _standIn.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_effectAlpha));
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
        if (!_bodySprite) yield break;
        EndlessKnightDirector.Hold = true;
        PitDemoGame.ControlsLocked = true;
        var portal = Open(BodyX, _player.transform.position.z, true);
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
        yield return new WaitForSeconds(0.6f);
        yield return portal.Close();
        Destroy(portal.gameObject);
    }

    Portal Open(float x, float z, bool withFruit)
    {
        var go = new GameObject("PORTAL");
        go.layer = _bodySprite ? _bodySprite.gameObject.layer : gameObject.layer;
        go.transform.SetParent(transform, true);
        go.transform.position = new Vector3(x, SpikeTrap.GroundY, z + 0.01f);
        var portal = go.AddComponent<Portal>();
        portal.owner = this;
        var tracker = GetComponentInChildren<FruitTracker>(true);
        portal.fruit = !withFruit ? null : ringFruit != null && ringFruit.Length > 0 ? ringFruit : tracker ? tracker.fruit : null;
        PitAudio.PlayClip(openSound);
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
        public Sprite[] fruit;
        /// Its fruit rings come first, then it grows out of its middle and fades in among them.
        public bool ringsFirst = true;
        const float RingTime = 0.7f;
        float Delay => ringsFirst ? RingTime : 0f;

        // The art (64 x 128): its frame from x 11 to 53, y 3 to 90 up from the bottom.
        const float ArtHeight = 87f, ArtHalfWidth = 21f;   // (its sprites' pivot: the art's bottom middle)
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

        public bool IsOpen => _renderer && Time.time - _start >= Delay + Length(owner.activateFrames);
        public float Middle => transform.position.y + (ArtHeight * 0.5f) * Pixel;
        public float Top => transform.position.y + ArtHeight * Pixel;
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
            _start = Time.time;
            var plain = new Material(Shader.Find("Sprites/Default")) { name = "Portal" };
            _art = new GameObject("Art").transform;
            _art.gameObject.layer = gameObject.layer;
            _art.SetParent(transform, false);
            _renderer = Piece("Frames", _art, plain, Sorting);
            _renderer.color = new Color(1f, 1f, 1f, owner.portalOpacity);
            // (Over its frames - drawn behind them it doesn't show - tinting them pinker.)
            _halo = Piece("Halo", _art, plain, Sorting + 1);
            _halo.sprite = Halo();
            _halo.transform.localPosition = new Vector3(0f, ArtHeight * 0.5f * 0.01f, 0f);
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
            float open = Length(owner.activateFrames);
            // Activate, then Idle round and round - or Close, once it's closing.
            if (_closeAt >= 0f)
                _renderer.sprite = Frame(owner.closeFrames, Time.time - _closeAt, false);
            else if (artAge < open)
                _renderer.sprite = Frame(owner.activateFrames, artAge, false);
            else
                _renderer.sprite = Frame(owner.idleFrames, artAge - open, true);

            // Closing: the portal shrinks away into its middle first, its fruit after.
            float shut = _closeAt >= 0f ? Mathf.Clamp01((Time.time - _closeAt) / Mathf.Max(0.01f, Length(owner.closeFrames))) : 0f;
            float size = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(shut / 0.6f));
            // Rings first: it grows and fades in out of its middle as it activates.
            float appear = ringsFirst ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(artAge / Mathf.Max(0.01f, open))) : 1f;
            if (age < Delay) appear = 0f;
            size *= appear;
            _renderer.color = new Color(1f, 1f, 1f, owner.portalOpacity * appear);
            _art.localScale = Vector3.one * Mathf.Max(0.001f, size);
            _art.localPosition = new Vector3(0f, ArtHeight * 0.5f * 0.01f * (1f - size), 0f);

            // The glow, pulsing - coming up as it opens.
            float glow = owner.glowStrength * Mathf.Clamp01(artAge / Mathf.Max(0.01f, open)) * (0.8f + 0.2f * Mathf.Sin(Time.time * 4f));
            var c = owner.glowColor;
            _halo.color = new Color(c.r, c.g, c.b, glow);
            _halo.enabled = size > 0.01f;
            _renderer.enabled = size > 0.01f;
            if (_fruit != null) Orbit(age, open, Mathf.Clamp01((shut - 0.35f) / 0.65f));
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
            yield return new WaitForSeconds(Length(owner.closeFrames));
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
