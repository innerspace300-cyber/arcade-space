using UnityEngine;

// ARcade: an ENDLESS KNIGHT level's own background in place of level 1's
// synthwave sky, sun, mountains and palms - level 2's Starry Night
// (SPRITES/STARRY NIGHT), level 3's Bamboo Forest (SPRITES/BAMBOO FOREST).
// It scrolls like level 1's: with the knight's walking
// (PlayerScriptARIANAClips.LastScrollDelta, the same amount that scrubs
// level 1's background), each layer at its own speed - the far ones
// barely, the near ones nearly with the road. Each layer tiles side to
// side, as many copies as cover the picture, shifted round as it scrolls.
// Its path is the road's surface in place of level 1's sidewalk: its
// solid ground scaled to the sidewalk's depth (anything drawn above that,
// grass and rocks, standing up over the road's top edge), moving exactly
// with the road (the knight's AccumulatedFrames,
// EndlessKnightDirector.RoadUnitsPerFrame) - and so do its props (lanterns,
// a shrine, rocks), standing on the road's top edge behind everyone.
// Level 2 also has a star falling in its sky now and then.
// Built by PitDemoGame once its cameras have the picture's size (Build);
// on each level's prefab with its sprites set.
public class ParallaxLevelBackground : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public Sprite sprite;
        [Tooltip("Depth group (Unity layer) it's drawn on.")]
        public string group;
        [Tooltip("Picture widths it moves per background loop (level 1's road: about 3).")]
        public float speed;
        [Tooltip("With layersMatchPath: this one (the sky) from the canvas's bottom up to the picture's top instead, all of it in view (Moonlit's moon).")]
        public bool fitView;
        [Tooltip("Drawn this much bigger (its top kept where it was): wider than the picture, so what's in it (level 2's planet) is never in view twice.")]
        public float zoom = 1f;
        [Tooltip("Its own brightness on top of artBrightness (level 2's near trees and dead tree, out of the dark; 0: as 1).")]
        public float brightness = 1f;
        [Tooltip("It in each boss arena's time of day (arenaBackTimes' order, the drawn one first; none: as drawn) - SetWalkTime.")]
        public Sprite[] times;
    }

    [System.Serializable]
    public class Prop
    {
        public Sprite sprite;
        [Tooltip("Where along the road, in picture widths (it comes round again every propPeriod).")]
        public float at;
        [Tooltip("Bigger or smaller than the path's pixels.")]
        public float scale = 1f;
        [Tooltip("Just the once (where it's put), not round again every propPeriod.")]
        public bool once;
        [Tooltip("Put where the knight starts (`at` then from her, in picture widths).")]
        public bool atKnight;
        [Tooltip("The level's standing gate (a portal arriving here opens in it, LevelPortal).")]
        public bool isGate;
        [Tooltip("Behind the path (its tall grass in front of it), not on it.")]
        public bool behindPath;
        [Tooltip("A glow behind it (its shape blurred: more canvas all round, its pivot at the prop's feet), tinted - a gate's neon.")]
        public Sprite glow;
        public Color glowTint = Color.white;
        [Tooltip("Little things in the air round it (GateMotes): the bamboo gate's sparks, the graveyard gate's flies.")]
        public GateMotes.Kind motes;
        public Color motesColor = Color.white;
        [Tooltip("Smoke curling up off it, in this colour (clear: none) - the death gate's.")]
        public Color smoke = Color.clear;
        [Tooltip("Burning embers along its outline (GateEmbers): sets of spots flickering on their own, and their glow.")]
        public Sprite[] embers;
        public Sprite emberGlow;
        [Tooltip("Nothing grows round it: the path's grass cleared from above the road this many of its widths across (0: none) - level 4's death gate.")]
        public float clearing;
    }

    [System.Serializable]
    public class Stretch
    {
        [Tooltip("A clump (its pivot at its foot), repeated side by side in runs.")]
        public Sprite sprite;
        public string group;
        [Tooltip("Picture widths it moves per background loop.")]
        public float speed = 1.5f;
        [Tooltip("Drawn after the layers in its group by this much (sorting).")]
        public int order = 10;
        [Tooltip("Bigger or smaller than the path's pixels.")]
        public float scale = 1f;
        [Tooltip("Runs, in clumps: a positive count of clumps, then a negative count of clump widths of gap, and so on; then it repeats.")]
        public int[] runs = { 4, -3, 2, -4, 3, -2 };
        [Tooltip("How much each clump overlaps the next (of its width).")]
        [Range(0f, 0.6f)] public float overlap = 0.25f;
    }

    [Tooltip("Back to front.")]
    public Layer[] layers;

    [Tooltip("How far below the road's top edge the art's ground goes (picture heights) - its grass under the road.")]
    public float sinkBelowRoad = 0.04f;
    [Tooltip("Where the art's ground is, as a share of its height up from its bottom (Bamboo: where its stems end); it goes on the road's top edge.")]
    public float groundLine = 0f;

    [Tooltip("The layers drawn as the art has them, on one canvas with the path (Moonlit's: every layer the same size): the path art's height and pixels, its ground line on the road's top edge.")]
    public bool layersMatchPath;

    [Header("Path (in place of the sidewalk)")]
    public Sprite pathArt;
    [Tooltip("The path art's rows (from its bottom): its solid ground, and all of it (the grass and rocks above).")]
    public int pathGroundRows = 37, pathTotalRows = 70;
    [Tooltip("Its solid ground drawn this much deeper, down from the road's top edge (what's above it as it is) - more ground to walk on, the shadows on it (level 3's: 1.8).")]
    public float groundStretch = 1f;
    [Tooltip("Shadows of the bamboo overhead lying across the ground, over everything on it (the knight too), in this colour (clear: none) - swaying a little in the wind, staying on the road as it scrolls.")]
    public Color canopyShadow = Color.clear;
    [Tooltip("This background layer bends in the same wind as the canopy shadows, its tops swaying, its roots still (level 3's black bamboo; -1: none).")]
    public int swayLayer = -1;
    [Tooltip("Where its stalks stand in its art (x: across its width, 0-1; y: a stalk's width, the same): the canopy shadows laid out under them.")]
    public Vector2[] canopyStalks;
    [Tooltip("Where its stalks' roots are, up from the layer's bottom (a share of its height).")]
    public float swayRoots = 0.19f;

    [Header("Stretches (runs of a clump with gaps, its own layer)")]
    public Stretch[] stretches;

    [Header("The art's colour")]
    [Tooltip("Its layers, path and props drawn more saturated / brighter (1: as drawn) - a dark night picked out a little.")]
    public float artSaturation = 1f, artBrightness = 1f;
    [Tooltip("The path's own brightness on top of artBrightness (0: as 1).")]
    public float pathBrightness = 1f;

    [Header("Shadows (the level's light)")]
    [Tooltip("The characters' shadows' colour here (clear: level 1's sunset pink).")]
    public Color shadowTint = Color.clear;
    [Tooltip("Their opacity, scaled (a night's: faint).")]
    public float shadowOpacity = 1f;
    [Tooltip("Its ground like still water: the characters (the knight, the witches, foes, bosses) reflected in it, rippling, in reflectionTint - other things keep their shadows (level 4).")]
    public bool reflections;
    public Color reflectionTint = new Color(0.65f, 0.8f, 1f, 0.45f);
    [Tooltip("The light on the characters themselves (LevelLit): their colours multiplied by it - a moonlit night's dimmer and cooler (white: as drawn).")]
    public Color characterLight = Color.white;

    [Header("Campfire")]
    [Tooltip("A soft glow round the campfire (clear: none).")]
    public Color campfireGlow = Color.clear;
    public float campfireGlowSize = 1.6f;   // of the fire's height

    [Header("Props on the road")]
    public Prop[] props;
    [Tooltip("Picture widths before the props come round again.")]
    public float propPeriod = 3f;
    [Tooltip("How far down into the road their feet go (path pixels), so they stand in the grass.")]
    public float propSink = 2f;

    [Header("Boss arena (level 3's shrine courtyard)")]
    [Tooltip("Coming along the road ahead of the boss (BossArena): its back (sky to the floor's back edge; pivot its bottom middle) on the nearest layer's group, its floor (pivot its top middle) on the road's - the boss fought in it.")]
    public Sprite arenaBack;
    public Sprite arenaFloor;
    [Tooltip("The same courtyard at other times of day (unity-helpers/arena_times.py: its sky regraded, the rest to match, the lanterns lit after dark) - each arena the next, after arenaBack's own: golden hour, dusk, night, day... A floor for each back, in the same order.")]
    public Sprite[] arenaBackTimes, arenaFloorTimes;
    [Tooltip("The arenas' times in turn (0: the drawn one, then arenaBackTimes' by number; none: that order) - level 3's morning, sunset, dusk, night.")]
    public int[] arenaTimeOrder;
    int _arenas;
    [Tooltip("How far down the floor art her feet stand (its rows).")]
    public int arenaFeetRow = 15;
    [Tooltip("The floor art's rows above the back's bottom (the same rows again): covering the path's top edge, which stands a little above the floor's back line.")]
    public int arenaFloorOverlap = 10;
    [Tooltip("Its ways in and out: a column of the level's own forest (pivot its roots, its right side fading), standing over each edge; its roots' row from its bottom... from its top.")]
    public Sprite arenaEdge;
    public int arenaEdgeRoots = 146;
    [Tooltip("Clumps of bright bamboo (pivot their roots) over its ends, on along its back line.")]
    public Sprite arenaTuft;

    /// Its far layers held still (BossArena: she's in it).
    [System.NonSerialized] public bool HoldLayers;
    [Tooltip("The stand in front of the road at its ends: sparser, in its colours (none: arenaTuft).")]
    public Sprite arenaFrontTuft;
    [Tooltip("A solid wall of bamboo (no gaps) behind the arena's ways in and out, the stretch nearest it.")]
    public Sprite arenaWall;
    [Tooltip("Its ways in and out in each arena's time of day (the drawn one first, then arenaBackTimes' in turn; none: as drawn) - regraded as its own bamboo is (unity-helpers/arena_bamboo.py).")]
    public Sprite[] arenaEdgeTimes, arenaTuftTimes, arenaWallTimes;
    [Tooltip("The ways' bamboo in a walk's own look where it isn't its arena's (level 3's dusk walk: the night's colours with the sunset's; none: the arena's time's).")]
    public Sprite[] walkWallTimes, walkTuftTimes;
    [Tooltip("Its way out's fade into the walk's look: from x of the picture's width out from its edge to y (none: as its way in, ArenaWayBlend's) - level 3's dusk walk's purple soon after it.")]
    public Vector2 arenaExitFade = new Vector2(0f, 1f);
    [Tooltip("Along that fade, at most x of the arena's look (all of it right at its edge, down to x over y of the picture's width) - level 3's dusk walk: mostly purple (none: all of it).")]
    public Vector2 arenaExitPeak = new Vector2(0.4f, 0.06f);
    [Tooltip("The path, and the characters' light, in each arena's time too (the walk on from it till the next: SetWalkTime; none / clear: as drawn).")]
    public Sprite[] pathArtTimes;
    public Color[] characterLightTimes;
    [Tooltip("The road in each arena's own time (arenaBackTimes' order, the drawn one first; none: the walk's) - under its ways, fading into the walk's (BossArena.Blend).")]
    public Texture2D[] arenaPathTimes;
    [Tooltip("Its props' sheet (propArt) in each time (none: as drawn) - level 3's lanterns, rocks and grass at night.")]
    public Texture2D propArt;
    public Texture2D[] propArtTimes;

    // ARcade: the walk on from a boss arena in its time of day, till the next one (level 3's frog arenas, unity-helpers/
    // walk_times.py) - swapped while the arena fills the picture (BossArena), so it's never seen to change.
    int _walkTime;
    [Tooltip("The walk's time it starts in, as drawn (level 3's morning: its sunset walk, the drawn arena's, has its own look).")]
    public int startWalkTime;
    public void SetWalkTime(int pick)
    {
        if (pick == _walkTime || _tiles == null) return;
        _walkTime = pick;
        Sprite Timed(Sprite[] each, Sprite drawn) => each != null && pick < each.Length && each[pick] ? each[pick] : drawn;
        for (int i = 0; i < layers.Length; i++)
        {
            if (_tiles[i] == null) continue;
            var sprite = Timed(layers[i].times, layers[i].sprite);
            foreach (var t in _tiles[i]) if (t && t.TryGetComponent<SpriteRenderer>(out var sr)) sr.sprite = sprite;
        }
        var path = Timed(pathArtTimes, pathArt);
        if (_path != null && path && !_groundArena)
            foreach (var t in _path)
                if (t) foreach (var sr in t.GetComponentsInChildren<SpriteRenderer>(true)) sr.sprite = CutFrom(sr.sprite, path.texture);
        // Its props off the time's own sheet (cut the same; as drawn if it has none).
        var props = propArtTimes != null && pick < propArtTimes.Length ? propArtTimes[pick] : null;
        foreach (Transform t in transform)
            if (t.name.StartsWith("PROP "))
                foreach (var sr in t.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    var drawn = Drawn(sr.sprite);
                    if (!drawn || drawn.texture != propArt) continue;   // (its sheet's only: not the gate, its glows)
                    sr.sprite = props ? CutFrom(drawn, props) : drawn;
                }
        LevelLit.Light = characterLightTimes != null && pick < characterLightTimes.Length && characterLightTimes[pick].a > 0f ? characterLightTimes[pick] : characterLight;
    }

    // ARcade: the road under an arena's ways in its time, fading out into the walk's road either side (in: the walk
    // now; out: the arena's own walk) - till the arena's gone (EndGroundBlend).
    BossArena _groundArena;
    readonly System.Collections.Generic.Dictionary<SpriteRenderer, Material> _groundWas = new System.Collections.Generic.Dictionary<SpriteRenderer, Material>();
    Texture WalkPath(int walk) => (pathArtTimes != null && walk < pathArtTimes.Length && pathArtTimes[walk] ? pathArtTimes[walk] : pathArt).texture;
    void BlendGround(BossArena arena, int pick)
    {
        var art = arenaPathTimes != null && pick < arenaPathTimes.Length ? arenaPathTimes[pick] : null;
        if (!art || _path == null || !arena.Blend || !pathArt) return;
        EndGroundBlend(_groundArena);
        _groundArena = arena;
        var block = new MaterialPropertyBlock();
        Texture inAlt = WalkPath(_walkTime), outAlt = WalkPath(pick);
        foreach (var t in _path)
            if (t) foreach (var sr in t.GetComponentsInChildren<SpriteRenderer>(true))
            {
                _groundWas[sr] = sr.sharedMaterial;
                sr.sprite = CutFrom(sr.sprite, art);
                sr.sharedMaterial = arena.Blend;
                sr.GetPropertyBlock(block);
                block.SetTexture(BossArena.AltTexId, inAlt);
                block.SetTexture(BossArena.AltTexRId, outAlt);
                sr.SetPropertyBlock(block);
            }
    }

    // ARcade: the far layers too, the last of the walk before the arena fading into its time (its fade on out past its
    // ways: ArenaWayBlend's _Start below 0) - in its time already (as SetWalkTime leaves them), the walk's to the left.
    BossArena _farArena;
    readonly System.Collections.Generic.Dictionary<SpriteRenderer, Material> _farWas = new System.Collections.Generic.Dictionary<SpriteRenderer, Material>();
    void BlendFar(BossArena arena, int pick)
    {
        if (!arena.Blend || _tiles == null) return;
        EndFarBlend();
        _farArena = arena;
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < layers.Length; i++)
        {
            if (_tiles[i] == null) continue;
            Sprite At(int t) => layers[i].times != null && t < layers[i].times.Length && layers[i].times[t] ? layers[i].times[t] : layers[i].sprite;
            Sprite walk = At(_walkTime), there = At(pick);
            if (!walk || !there || walk.texture == there.texture) continue;
            var blend = i == swayLayer ? arena.SwayBlend : arena.Blend;
            foreach (var t in _tiles[i])
                if (t && t.TryGetComponent<SpriteRenderer>(out var sr))
                {
                    _farWas[sr] = sr.sharedMaterial;
                    sr.sprite = there;
                    sr.sharedMaterial = blend;
                    sr.GetPropertyBlock(block);
                    block.SetTexture(BossArena.AltTexId, walk.texture);
                    block.SetTexture(BossArena.AltTexRId, there.texture);
                    sr.SetPropertyBlock(block);
                }
        }
    }

    // Its own material back (its sprite the arena's time, the walk's now).
    void EndFarBlend()
    {
        _farArena = null;
        foreach (var (sr, was) in _farWas)
        {
            if (!sr) continue;
            sr.sharedMaterial = was;
            sr.SetPropertyBlock(null);
        }
        _farWas.Clear();
    }

    /// Gone by off the left: the road and far layers under its way in in the walk on from it too.
    public void TakeEntrance(BossArena arena)
    {
        if (arena == _farArena) EndFarBlend();   // (in its time already, either side)
        if (arena == _groundArena) foreach (var sr in _groundWas.Keys) if (sr) BossArena.Forward(sr);
    }

    /// The arena's gone: the road the walk's own again.
    public void EndGroundBlend(BossArena arena)
    {
        if (arena && arena == _farArena) EndFarBlend();
        if (!arena || arena != _groundArena) return;
        _groundArena = null;
        var path = WalkPath(_walkTime);
        foreach (var (sr, was) in _groundWas)
        {
            if (!sr) continue;
            sr.sharedMaterial = was;
            sr.SetPropertyBlock(null);
            sr.sprite = CutFrom(sr.sprite, path);
        }
        _groundWas.Clear();
    }

    // A piece of the path (its crop, its ground rows) or a prop cut the same from another time's art.
    readonly System.Collections.Generic.Dictionary<(Sprite, Texture), Sprite> _cuts = new System.Collections.Generic.Dictionary<(Sprite, Texture), Sprite>();
    readonly System.Collections.Generic.Dictionary<Sprite, Sprite> _drawnOf = new System.Collections.Generic.Dictionary<Sprite, Sprite>();
    Sprite Drawn(Sprite piece) => piece && _drawnOf.TryGetValue(piece, out var o) ? o : piece;
    Sprite CutFrom(Sprite piece, Texture art)
    {
        if (!piece || !art) return piece;
        var original = Drawn(piece);   // (always cut from the drawn one's rect)
        if (original.texture == art) return original;
        if (_cuts.TryGetValue((original, art), out var cut)) return cut;
        cut = Sprite.Create((Texture2D)art, original.rect, new Vector2(original.pivot.x / original.rect.width, original.pivot.y / original.rect.height),
            original.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        cut.name = original.name;
        _cuts[(original, art)] = cut;
        _drawnOf[cut] = original;
        return cut;
    }

    public bool HasArena => arenaBack && arenaFloor && _path != null;

    /// The arena, coming in from the right (EndlessKnightDirector: as the boss is due).
    public BossArena SpawnArena()
    {
        if (!HasArena) return null;
        var road = _path[0].GetComponent<SpriteRenderer>();
        string near = layers.Length > 0 ? layers[layers.Length - 1].group : "Default";
        // The floor over the path and its props, under the characters' shadows (SpriteShadow: -990).
        // (this one's time of day: the drawn one first, then each of the others in turn)
        Sprite back = arenaBack, floor = arenaFloor;
        int times = arenaBackTimes != null && arenaFloorTimes != null ? Mathf.Min(arenaBackTimes.Length, arenaFloorTimes.Length) : 0;
        int turn = _arenas++ % (times + 1);
        int pick = arenaTimeOrder != null && turn < arenaTimeOrder.Length ? Mathf.Clamp(arenaTimeOrder[turn], 0, times) : turn;
        if (pick > 0 && arenaBackTimes[pick - 1] && arenaFloorTimes[pick - 1]) { back = arenaBackTimes[pick - 1]; floor = arenaFloorTimes[pick - 1]; }
        Sprite Timed(Sprite[] each, Sprite drawn) => each != null && pick < each.Length && each[pick] ? each[pick] : drawn;
        var tuft = Timed(arenaTuftTimes, arenaTuft);
        var arena = BossArena.Make(transform, _view, back, floor, arenaFeetRow, near,
            road.gameObject.layer, road.sortingLayerID, -991, Timed(arenaEdgeTimes, arenaEdge), arenaEdgeRoots, arenaFloorOverlap, tuft,
            arenaFrontTuft == arenaTuft ? tuft : arenaFrontTuft, Timed(arenaWallTimes, arenaWall));
        if (arena)
        {
            arena.WalkTime = pick;
            // Its ways fading from the walk's look into its time: in from the walk now, out to the walk it leaves behind it.
            System.Collections.Generic.Dictionary<Texture, Texture> WalkLook(int walk)
            {
                bool asDrawn = layers.Length == 0 || layers[0].times == null || walk >= layers[0].times.Length || !layers[0].times[walk] || layers[0].times[walk] == layers[0].sprite;
                var map = new System.Collections.Generic.Dictionary<Texture, Texture>();
                foreach (var (each, walkEach, plain) in new[] { (arenaWallTimes, walkWallTimes, arenaWall), (arenaTuftTimes, walkTuftTimes, arenaTuft), (arenaTuftTimes, walkTuftTimes, arenaFrontTuft) })
                {
                    var here = Timed(each, plain);
                    var there = asDrawn ? plain
                        : walkEach != null && walk < walkEach.Length && walkEach[walk] ? walkEach[walk]
                        : each != null && walk < each.Length && each[walk] ? each[walk] : plain;
                    if (here && there) map[here.texture] = there.texture;
                }
                return map;
            }
            arena.BlendWays(Resources.Load<Material>("ArenaWayBlend"), WalkLook(_walkTime), WalkLook(pick), arenaExitFade, arenaExitPeak);
            BlendGround(arena, pick);
            BlendFar(arena, pick);
        }
        return arena;
    }

    [Header("Sky body (level 2's planet)")]
    [Tooltip("Drawn once, whole, in the sky (on the first layer's group, in front of it), where it's put - not tiled with the sky.")]
    public Sprite skyBody;
    [Tooltip("Its height, in picture heights.")]
    public float skyBodyHeight = 0.3f;
    [Tooltip("Its middle, as a share of the picture across and up.")]
    public Vector2 skyBodyAt = new Vector2(0.3f, 0.75f);

    [Header("Fairies in the far trees (level 2's fairy forest)")]
    [Tooltip("Their frames: fairyFramesEach to a fairy, one fairy after another.")]
    public Sprite[] fairyFrames;
    public int fairyFramesEach = 8;
    [Tooltip("The layers (their indexes) they flit about in, moving with them.")]
    public int[] fairyLayers;
    public int fairiesPerLayer = 5;
    [Tooltip("Their size, as a share of the picture's height (the nearest layer's; farther ones smaller).")]
    public float fairySize = 0.05f;
    public Color fairyGlow = new Color(1f, 0.7f, 1f, 0.45f);

    [Header("Falling star (level 2)")]
    public Sprite[] fallingStarFrames;
    public float fallingStarFps = 10f;
    public Vector2 fallingStarEvery = new Vector2(5f, 11f);   // seconds, random between

    // Synthwave renderers this replaces (level 1's names, under RETRO PARALLAX).
    static readonly string[] Replaced = { "BG Back", "BG sun", "BG Mountains Copy", "BG palms back", "BG Palms" };

    PlayerScriptARIANAClips _player;
    Transform[][] _tiles;
    float[] _offsets, _tileWidths, _layerCenters;
    float _pictureWidth, _centerY, _z;
    SpriteRenderer _star;
    float _nextStar, _starTime = -1f;
    Bounds _view;

    /// The picture (the cameras' view), the road's top edge, and the sidewalk's bottom (world units).
    public void Build(Bounds view, float roadTop, Transform background, PlayerScriptARIANAClips player,
        SpriteRenderer road = null, float sidewalkBottom = float.NaN)
    {
        SpriteShadow.CanopyMasks = false;   // (till this level's canopy says otherwise)
        _view = view;
        _pictureWidth = view.size.x;
        _player = player;
        var body = player ? player.transform.Find("ARIANA") : null;
        var bodySprite = body ? body.GetComponent<SpriteRenderer>() : null;
        _knightX = bodySprite ? bodySprite.bounds.center.x : view.center.x;
        if (background)
        {
            if (background.TryGetComponent<Renderer>(out var merged)) merged.enabled = false;   // (the flattened synthwave)
            foreach (string name in Replaced)
            {
                var t = background.Find(name);
                if (t && t.TryGetComponent<Renderer>(out var r)) r.enabled = false;
            }
        }

        // The art from the top of the picture down, its ground line on the road's top edge.
        float ground = roadTop - view.size.y * sinkBelowRoad;
        float height = (view.max.y - ground) / Mathf.Max(0.05f, 1f - groundLine);
        float bottom = ground - height * groundLine;
        if (layersMatchPath && pathArt && !float.IsNaN(sidewalkBottom) && pathGroundRows > 0)
        {
            // One canvas: the path art's (its ground rows the sidewalk's depth), its bottom under the road.
            float pixel = (roadTop - sidewalkBottom) / pathGroundRows;
            // Shrunk about its ground line (on the road) if its top would run past the
            // picture's (a wide strip on a 4:3 screen): all of it in view.
            float above = (pathArt.rect.height - pathGroundRows) * pixel;
            float fit = Mathf.Min(1f, (view.max.y - roadTop) / Mathf.Max(0.001f, above));
            height = pathArt.rect.height * pixel * fit;
            bottom = roadTop - pathGroundRows * pixel * fit;
        }
        _centerY = bottom + height * 0.5f;
        _z = view.min.z + 1f;
        _tiles = new Transform[layers.Length][];
        _walkTime = startWalkTime;
        _offsets = new float[layers.Length];
        _tileWidths = new float[layers.Length];
        _layerCenters = new float[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            var layer = layers[i];
            if (!layer.sprite) continue;
            int unityLayer = LayerMask.NameToLayer(layer.group);
            float layerHeight = layer.fitView ? view.max.y - bottom : height;
            float layerTop = bottom + layerHeight;
            float zoom = Mathf.Max(1f, layer.zoom);
            layerHeight *= zoom;
            float scale = layerHeight / layer.sprite.bounds.size.y;
            _tileWidths[i] = layer.sprite.bounds.size.x * scale;
            int copies = CopiesFor(_tileWidths[i]);
            _tiles[i] = new Transform[copies];
            for (int copy = 0; copy < copies; copy++)
            {
                var go = new GameObject($"BG {i + 1}{(char)('A' + copy)}");
                go.layer = unityLayer;   // (set: PitDemoGame leaves non-Default layers alone)
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = layer.sprite;
                sr.sortingOrder = -200 + i;   // back to front within a group
                _tiles[i][copy] = go.transform;
            }
            _layerCenters[i] = layerTop - layerHeight * 0.5f;   // (zoomed from its top)
            if (i == swayLayer)
            {
                // Bending in the wind (SpriteSway), in time with the shadows.
                var bend = Resources.Load<Material>("BambooSway");
                if (bend) foreach (var t in _tiles[i]) t.GetComponent<SpriteRenderer>().sharedMaterial = bend;
                float layerBottom = layerTop - layerHeight;
                Shader.SetGlobalFloat("_SwayBase", layerBottom + layerHeight * swayRoots);
                Shader.SetGlobalFloat("_SwayHeight", layerHeight * (1f - swayRoots));
                _swaying = true;
            }
            Place(_tiles[i], _tileWidths[i], 0f, _layerCenters[i]);
        }
        if (road && pathArt && !float.IsNaN(sidewalkBottom)) BuildPath(road, roadTop, sidewalkBottom);
        if (road && stretches != null && _pathPixel > 0f) BuildStretches(roadTop);
        if (road && _path != null && canopyShadow.a > 0f) BuildCanopy(road);
        if (campfireGlow.a > 0f) GlowCampfire();
        if (fairyFrames != null && fairyFrames.Length > 0 && fairyLayers != null && fairyLayers.Length > 0) MakeFairies(view, bottom, height);
        if (artSaturation != 1f || artBrightness != 1f) Colour();
        if (skyBody)
        {
            var go = new GameObject("SKY BODY");
            go.layer = LayerMask.NameToLayer(layers.Length > 0 ? layers[0].group : "Default");
            go.transform.SetParent(transform, false);
            float scale = skyBodyHeight * view.size.y / skyBody.bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.transform.position = new Vector3(view.min.x + view.size.x * skyBodyAt.x, view.min.y + view.size.y * skyBodyAt.y, _z - 0.005f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = skyBody;
            sr.sortingOrder = -200 + 2;   // (in front of the sky and its falling star)
        }
        if (fallingStarFrames != null && fallingStarFrames.Length > 0)
        {
            var go = new GameObject("FALLING STAR");
            go.layer = LayerMask.NameToLayer(layers.Length > 0 ? layers[0].group : "Default");
            go.transform.SetParent(transform, false);
            float scale = height / (layers.Length > 0 && layers[0].sprite ? layers[0].sprite.bounds.size.y : 1f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            _star = go.AddComponent<SpriteRenderer>();
            _star.sortingOrder = -200 + 1;   // over the sky only
            _star.enabled = false;
            _nextStar = Time.time + Random.Range(fallingStarEvery.x, fallingStarEvery.y);
        }
    }

    // Enough copies side by side to cover the picture whatever the shift.
    int CopiesFor(float tileWidth) => tileWidth > 0f ? Mathf.CeilToInt(_pictureWidth / tileWidth) + 1 : 1;

    // Copies side by side from the picture's left edge, shifted left by the
    // offset (wrapped into one tile): the picture's always covered.
    void Place(Transform[] tiles, float tileWidth, float offset, float centerY)
    {
        float x = Mathf.Repeat(offset, tileWidth);
        for (int copy = 0; copy < tiles.Length; copy++)
            tiles[copy].position = new Vector3(_view.min.x - x + (copy + 0.5f) * tileWidth, centerY, _z);
    }

    // ---- The path and its props ----

    Transform[] _path;
    Transform[] _propCopies;
    float _pathWidth, _pathY, _pathZ, _pathPixel, _knightX;

    /// The level's standing gate (a prop marked isGate), and one of its art pixels in world units.
    public Transform StandingGate { get; private set; }
    public float StandingGatePixel { get; private set; }

    void BuildPath(SpriteRenderer road, float roadTop, float sidewalkBottom)
    {
        var art = pathArt.rect;
        int total = Mathf.Min(pathTotalRows, (int)art.height);
        // The bottom `total` rows of the art, its pivot on the ground's top edge.
        var crop = Sprite.Create(pathArt.texture, new Rect(art.x, art.y, art.width, total),
            new Vector2(0.5f, pathGroundRows / (float)total), pathArt.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        crop.name = "LEVEL PATH";
        float scale = (roadTop - sidewalkBottom) / (pathGroundRows / pathArt.pixelsPerUnit);
        _pathPixel = scale / pathArt.pixelsPerUnit;   // one path pixel, world units
        _pathWidth = art.width / pathArt.pixelsPerUnit * scale;
        _pathY = roadTop;
        _pathZ = road.transform.position.z;
        // Stretched deeper: the ground rows on their own (from the road's top edge down), the rest as it is.
        Sprite above = null, below = null;
        if (groundStretch > 1f && total > pathGroundRows)
        {
            above = Sprite.Create(pathArt.texture, new Rect(art.x, art.y + pathGroundRows, art.width, total - pathGroundRows),
                new Vector2(0.5f, 0f), pathArt.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            below = Sprite.Create(pathArt.texture, new Rect(art.x, art.y, art.width, pathGroundRows),
                new Vector2(0.5f, 1f), pathArt.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
        _path = new Transform[CopiesFor(_pathWidth)];
        for (int copy = 0; copy < _path.Length; copy++)
        {
            var go = new GameObject($"PATH {copy + 1}");
            go.layer = road.gameObject.layer;   // the road's depth group (the characters')
            go.transform.SetParent(transform, false);
            // (All ground, nothing above it - pivoted on its top edge: just drawn deeper.)
            go.transform.localScale = new Vector3(scale, scale * (!below && total <= pathGroundRows ? Mathf.Max(1f, groundStretch) : 1f), 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = above ? above : crop;
            sr.sortingLayerID = road.sortingLayerID;
            sr.sortingOrder = road.sortingOrder;
            _path[copy] = go.transform;
            if (below)
            {
                var ground = new GameObject("GROUND").AddComponent<SpriteRenderer>();
                ground.gameObject.layer = go.layer;
                ground.transform.SetParent(go.transform, false);
                ground.transform.localScale = new Vector3(1f, groundStretch, 1f);
                ground.sprite = below;
                ground.sortingLayerID = sr.sortingLayerID;
                ground.sortingOrder = sr.sortingOrder;
            }
        }
        road.enabled = false;   // level 1's sidewalk

        if (props != null && props.Length > 0)
        {
            _propCopies = new Transform[props.Length];
            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                if (!prop.sprite) continue;
                var go = new GameObject("PROP " + prop.sprite.name);
                go.layer = road.gameObject.layer;
                go.transform.SetParent(transform, false);
                float s = _pathPixel * prop.sprite.pixelsPerUnit * prop.scale;
                go.transform.localScale = new Vector3(s, s, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = prop.sprite;   // (pivot: its feet)
                sr.sortingLayerID = road.sortingLayerID;
                sr.sortingOrder = prop.behindPath ? road.sortingOrder - 1 - i   // (behind the path's grass)
                    : road.sortingOrder + 1 + i;   // over the path, behind everyone on it (in list order)
                // A standing gate a little further up: the swirl opening in it (just behind it) and that
                // swirl's ground mask stay clear of the path's own sorting.
                if (prop.isGate && !prop.behindPath) sr.sortingOrder = road.sortingOrder + 1 + props.Length + 3;
                _propCopies[i] = go.transform;
                if (prop.glow)
                {
                    var glow = new GameObject("GLOW").AddComponent<SpriteRenderer>();
                    glow.gameObject.layer = go.layer;
                    glow.transform.SetParent(go.transform, false);
                    glow.transform.localPosition = new Vector3(0f, 0f, 0.001f);
                    glow.sprite = prop.glow;
                    glow.color = prop.glowTint;
                    glow.sortingLayerID = sr.sortingLayerID;
                    glow.sortingOrder = sr.sortingOrder - 10;   // (behind it and a portal's swirl in it)
                }
                GateMotes.Add(sr, prop.motes, prop.motesColor);
                if (prop.smoke.a > 0f) GateMotes.Add(sr, GateMotes.Kind.Smoke, prop.smoke);
                GateEmbers.Add(sr, prop.embers, prop.emberGlow);
                if (prop.clearing > 0f) Clearing(go.transform, sr, prop.clearing, roadTop, road.sortingLayerID, road.sortingOrder);
                if (prop.isGate) { StandingGate = go.transform; StandingGatePixel = s / prop.sprite.pixelsPerUnit; }
            }
        }
        PlacePath();
    }

    // Every renderer of its art through the hue-shift shader, saturation and brightness up.
    void Colour()
    {
        var shader = Shader.Find("SpatialEmulator/SpriteHueShift");
        if (!shader) return;
        var look = new Material(shader) { name = "Level Colour" };
        look.SetFloat("_Hue", 0f);
        look.SetFloat("_Saturation", artSaturation);
        look.SetFloat("_Brightness", artBrightness);
        foreach (var r in GetComponentsInChildren<SpriteRenderer>(true)) r.sharedMaterial = look;
        // Layers (and the path) brighter than the rest: their own copy.
        Material Brighter(float by)
        {
            var own = new Material(look) { name = "Level Colour x" + by };
            own.SetFloat("_Brightness", artBrightness * by);
            return own;
        }
        for (int i = 0; i < layers.Length && _tiles != null; i++)
        {
            float by = layers[i].brightness;
            if (by <= 0f || Mathf.Approximately(by, 1f) || _tiles[i] == null) continue;
            var own = Brighter(by);
            foreach (var t in _tiles[i]) if (t) foreach (var r in t.GetComponentsInChildren<SpriteRenderer>(true)) r.sharedMaterial = own;
        }
        if (_path != null && pathBrightness > 0f && !Mathf.Approximately(pathBrightness, 1f))
        {
            var own = Brighter(pathBrightness);
            foreach (var t in _path) if (t) foreach (var r in t.GetComponentsInChildren<SpriteRenderer>(true)) r.sharedMaterial = own;
        }
    }

    // ---- fairies far off among the trees ----
    class FarFairy { public SpriteRenderer art, glow; public int layer, sheet; public float x, y, phase, wander, size; }
    readonly System.Collections.Generic.List<FarFairy> _fairies = new System.Collections.Generic.List<FarFairy>();

    void MakeFairies(Bounds view, float bottom, float height)
    {
        int sheets = Mathf.Max(1, fairyFrames.Length / Mathf.Max(1, fairyFramesEach));
        float fastest = 0.01f;
        foreach (int li in fairyLayers) if (li >= 0 && li < layers.Length) fastest = Mathf.Max(fastest, layers[li].speed);
        foreach (int li in fairyLayers)
        {
            if (li < 0 || li >= layers.Length) continue;
            float depth = 0.45f + 0.55f * layers[li].speed / fastest;   // (farther: smaller)
            for (int n = 0; n < fairiesPerLayer; n++)
            {
                var go = new GameObject("FAR FAIRY");
                go.layer = LayerMask.NameToLayer(layers[li].group);
                go.transform.SetParent(transform, false);
                var f = new FarFairy { layer = li, sheet = Random.Range(0, sheets), phase = Random.value * 10f, wander = Random.Range(0.6f, 1.4f) };
                f.x = Random.Range(0f, _pictureWidth * 2f);
                f.y = bottom + height * Random.Range(0.25f, 0.75f);
                f.size = height * fairySize * depth;
                f.art = go.AddComponent<SpriteRenderer>();
                f.art.sortingOrder = -200 + li;
                var glow = new GameObject("GLOW").AddComponent<SpriteRenderer>();
                glow.gameObject.layer = go.layer;
                glow.transform.SetParent(go.transform, false);
                glow.sprite = FoeFx.GlowSprite;
                glow.sortingOrder = -200 + li;
                glow.color = fairyGlow;
                f.glow = glow;
                _fairies.Add(f);
            }
        }
    }

    void Fairies()
    {
        if (_fairies.Count == 0) return;
        float w = _pictureWidth * 2f, left = _view.min.x - _pictureWidth * 0.5f;
        foreach (var f in _fairies)
        {
            if (!f.art) continue;
            float t = Time.time * f.wander + f.phase;
            // With its layer, wandering a little, bobbing.
            float x = left + Mathf.Repeat(f.x - _offsets[f.layer] + Mathf.Sin(t * 0.7f) * _pictureWidth * 0.08f, w);
            float y = f.y + Mathf.Sin(t * 1.9f) * f.size * 0.6f;
            f.art.transform.position = new Vector3(x, y, _z - 0.02f);
            var frame = fairyFrames[(f.sheet * fairyFramesEach + (int)(Time.time * 12f + f.phase * 3f) % fairyFramesEach) % fairyFrames.Length];
            f.art.sprite = frame;
            f.art.flipX = Mathf.Cos(t * 0.7f) < 0f;
            float k = f.size / Mathf.Max(0.001f, frame.bounds.size.y);
            f.art.transform.localScale = new Vector3(k, k, 1f);
            float g = f.size * 2.6f / Mathf.Max(0.001f, FoeFx.GlowSprite.bounds.size.y) / k;
            f.glow.transform.localScale = new Vector3(g, g, 1f);
            float pulse = 0.7f + 0.3f * Mathf.Sin(t * 5f);
            f.glow.color = new Color(fairyGlow.r, fairyGlow.g, fairyGlow.b, fairyGlow.a * pulse);
        }
    }

    static Sprite s_square;

    // A clearing round a prop: a mask over the path's grass above the road's
    // top edge, moving with the prop; the path drawn only outside it (and
    // nothing else touched: its sorting range is just the path's).
    void Clearing(Transform prop, SpriteRenderer art, float widths, float roadTop, int layerId, int pathOrder)
    {
        if (!s_square) s_square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 4f);
        var go = new GameObject("CLEARING");
        go.layer = prop.gameObject.layer;
        go.transform.SetParent(prop, false);
        var mask = go.AddComponent<SpriteMask>();
        mask.sprite = s_square;
        mask.isCustomRangeActive = true;
        mask.frontSortingLayerID = mask.backSortingLayerID = layerId;
        mask.frontSortingOrder = pathOrder;
        mask.backSortingOrder = pathOrder - 1;
        var world = prop.lossyScale;
        go.transform.localScale = new Vector3(art.bounds.size.x * widths / world.x, 20f / world.y, 1f);
        // (From its feet, sunk propSink path pixels into the road: up from the road's top edge.)
        go.transform.localPosition = new Vector3(0f, propSink * _pathPixel / world.y, 0f);
        if (_path != null)
            foreach (var t in _path) foreach (var r in t.GetComponentsInChildren<SpriteRenderer>()) r.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
    }

    // ---- the bamboo's shadows across the ground ----
    Transform[] _canopy, _canopyStand;
    float _canopyWidth;
    bool _swaying;

    // The wind: a few pixels to and fro, slow with a quicker flutter - the shadows and the bamboo both.
    float Sway => (Mathf.Sin(Time.time * 0.6f) * 3f + Mathf.Sin(Time.time * 1.7f) * 1.2f) * _pathPixel;

    void BuildCanopy(SpriteRenderer road)
    {
        // Laid out under the black bamboo's own stalks (canopyStalks: where they stand in its art), as wide
        // as one of its tiles, scrolling with it - each shadow stays under its stalk.
        bool underBamboo = swayLayer >= 0 && swayLayer < layers.Length && _tiles[swayLayer] != null;
        _canopyWidth = underBamboo ? _tileWidths[swayLayer] : 256 * _pathPixel;
        int w = Mathf.Max(16, Mathf.RoundToInt(_canopyWidth / _pathPixel));
        _canopyWidth = w * _pathPixel;
        int ground = pathGroundRows;
        var groundTex = Shade(w, ground, Mathf.Min(ground, 14), 0.5f, 7);
        // And on whoever stands in it - her, the foes, the bosses, the chests (only inside their shapes,
        // SpriteShadow's masks): the stalks' shade upright, as tall as they are and more.
        int tall = Mathf.RoundToInt(1.6f / Mathf.Max(1e-4f, _pathPixel));
        var standTex = Shade(w, tall, tall, 0.12f, 11);
        float scale = _pathPixel * pathArt.pixelsPerUnit;
        var groundSprite = Sprite.Create(groundTex, new Rect(0, 0, w, ground), new Vector2(0.5f, 1f), pathArt.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        var standSprite = Sprite.Create(standTex, new Rect(0, 0, w, tall), new Vector2(0.5f, 0f), pathArt.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        int copies = CopiesFor(_canopyWidth) + 1;
        _canopy = new Transform[copies];
        _canopyStand = new Transform[copies];
        for (int copy = 0; copy < copies; copy++)
        {
            var go = new GameObject($"CANOPY SHADOW {copy + 1}");
            go.layer = road.gameObject.layer;
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(scale, scale * Mathf.Max(1f, groundStretch), 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = groundSprite;
            sr.color = canopyShadow;
            sr.sortingLayerName = "Default";   // (the characters' own: drawn after TopLayer)
            sr.sortingOrder = 600;             // (over the knight, her slash, the foes and the chests)
            _canopy[copy] = go.transform;

            var stand = new GameObject($"CANOPY SHADE {copy + 1}");
            stand.layer = road.gameObject.layer;
            stand.transform.SetParent(transform, false);
            stand.transform.localScale = new Vector3(scale, scale, 1f);
            var shade = stand.AddComponent<SpriteRenderer>();
            shade.sprite = standSprite;
            shade.color = new Color(canopyShadow.r, canopyShadow.g, canopyShadow.b, canopyShadow.a * 0.8f);
            shade.sortingLayerName = "Default";
            shade.sortingOrder = SpriteShadow.CanopyShadeOrder;
            shade.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;   // (only on the characters)
            _canopyStand[copy] = stand.transform;
        }
        SpriteShadow.CanopyMasks = true;
    }

    // The stalks' shadows, `h` pixels tall (only the top `lit` rows), each leaning `lean` px a row, with
    // their joints' gaps, and clumps of leaves' shadow among them.
    Texture2D Shade(int w, int h, int lit, float lean, int seed)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        var px = new Color32[w * h];
        var rand = new System.Random(seed);
        var on = new Color32(255, 255, 255, 255);
        var stalks = canopyStalks != null && canopyStalks.Length > 0 ? canopyStalks : null;
        int count = stalks != null ? stalks.Length : 14;
        for (int s = 0; s < count; s++)
        {
            float x0 = stalks != null ? stalks[s].x * w : (float)rand.NextDouble() * w;
            int width = stalks != null ? Mathf.Max(2, Mathf.RoundToInt(stalks[s].y * w)) : 2 + rand.Next(3);
            int joint = 5 + rand.Next(4);
            for (int y = h - lit; y < h; y++)
            {
                if ((y + s) % joint == 0) continue;   // (a joint: a gap)
                int x = Mathf.RoundToInt(x0 + (h - 1 - y) * lean);
                for (int k = 0; k < width; k++) px[y * w + ((x + k) % w + w) % w] = on;
            }
        }
        for (int c = 0; c < count + 6; c++)
        {
            int cx = rand.Next(w), cy = h - 1 - rand.Next(lit);
            int r = 2 + rand.Next(4);
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r * 2; dx <= r * 2; dx++)
                    if ((dx * dx) / 4f + dy * dy <= r * r && rand.NextDouble() < 0.7)
                    {
                        int y = cy + dy;
                        if (y >= h - lit && y < h) px[y * w + ((cx + dx) % w + w) % w] = on;
                    }
        }
        tex.SetPixels32(px); tex.Apply();
        return tex;
    }

    void PlaceCanopy()
    {
        // With the black bamboo as it scrolls (or the road), swaying with it in the wind.
        bool underBamboo = swayLayer >= 0 && swayLayer < layers.Length && _tiles[swayLayer] != null;
        float scroll = underBamboo ? _offsets[swayLayer] : RoadOffset;
        float x = Mathf.Repeat(scroll - Sway, _canopyWidth);
        for (int copy = 0; copy < _canopy.Length; copy++)
        {
            float cx = _view.min.x - x + (copy + 0.5f) * _canopyWidth;
            _canopy[copy].position = new Vector3(cx, _pathY, _pathZ - 0.01f);
            _canopyStand[copy].position = new Vector3(cx, _pathY, _pathZ - 0.012f);
        }
    }

    float RoadOffset => _player ? _player.AccumulatedFrames * EndlessKnightDirector.RoadUnitsPerFrame : 0f;

    void PlacePath()
    {
        // Exactly the road's scroll: everything on it stays on it.
        float offset = RoadOffset;
        float x = Mathf.Repeat(offset, _pathWidth);
        for (int copy = 0; copy < _path.Length; copy++)
            _path[copy].position = new Vector3(_view.min.x - x + (copy + 0.5f) * _pathWidth, _pathY, _pathZ);

        if (_propCopies == null) return;
        // Each prop once every propPeriod picture widths of road; it comes
        // round from off the right as the last one leaves the left.
        float period = Mathf.Max(propPeriod, 1.5f) * _pictureWidth;
        float y = _pathY - propSink * _pathPixel;
        for (int i = 0; i < _propCopies.Length; i++)
        {
            if (!_propCopies[i]) continue;
            var prop = props[i];
            float at = prop.at * _pictureWidth + (prop.atKnight ? _knightX - _view.min.x : 0f);
            float along = at - offset;   // from the picture's left edge
            float px = prop.once ? _view.min.x + along
                : _view.min.x + Mathf.Repeat(along + period * 0.25f, period) - period * 0.25f;
            _propCopies[i].position = new Vector3(px, y, _pathZ - 0.001f);
        }
    }

    // ---- Stretches ----

    Transform[][] _stretchClumps;
    float[] _stretchOffsets, _stretchLengths;
    float[][] _stretchSlots;
    float _stretchY;

    void BuildStretches(float roadTop)
    {
        _stretchY = roadTop;
        _stretchClumps = new Transform[stretches.Length][];
        _stretchOffsets = new float[stretches.Length];
        _stretchLengths = new float[stretches.Length];
        _stretchSlots = new float[stretches.Length][];
        for (int k = 0; k < stretches.Length; k++)
        {
            var st = stretches[k];
            if (!st.sprite || st.runs == null || st.runs.Length == 0) continue;
            float s = _pathPixel * st.sprite.pixelsPerUnit * st.scale;
            float width = st.sprite.bounds.size.x * s, step = width * (1f - st.overlap);
            // Where each clump goes along one repeat of the runs.
            var slots = new System.Collections.Generic.List<float>();
            float x = 0f;
            foreach (int run in st.runs)
            {
                if (run > 0) for (int c = 0; c < run; c++) { slots.Add(x + width * 0.5f); x += step; }
                else x += -run * width;
            }
            float length = Mathf.Max(x, width);
            int repeats = Mathf.CeilToInt(_pictureWidth / length) + 1;
            _stretchLengths[k] = length;
            _stretchSlots[k] = slots.ToArray();
            _stretchClumps[k] = new Transform[slots.Count * repeats];
            int unityLayer = LayerMask.NameToLayer(st.group);
            for (int i = 0; i < _stretchClumps[k].Length; i++)
            {
                var go = new GameObject($"STRETCH {k + 1}.{i + 1}");
                go.layer = unityLayer;
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(s, s, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = st.sprite;   // (pivot: its foot)
                sr.sortingOrder = -200 + layers.Length + st.order;
                _stretchClumps[k][i] = go.transform;
            }
            PlaceStretch(k);
        }
    }

    void PlaceStretch(int k)
    {
        var clumps = _stretchClumps[k];
        if (clumps == null) return;
        var slots = _stretchSlots[k];
        float length = _stretchLengths[k];
        float x0 = _view.min.x - Mathf.Repeat(_stretchOffsets[k], length);
        for (int i = 0; i < clumps.Length; i++)
        {
            int rep = i / slots.Length, slot = i % slots.Length;
            clumps[i].position = new Vector3(x0 + rep * length + slots[slot], _stretchY, _z - 0.002f * (k + 1));
        }
    }

    // ---- The campfire's glow ----

    static Sprite s_glow;

    void GlowCampfire()
    {
        var root = transform.root;
        var fire = root.Find("BONFIRE");
        var fireArt = fire ? fire.GetComponent<SpriteRenderer>() : null;
        if (!fireArt) return;
        if (!s_glow)
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            s_glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
        var go = new GameObject("CAMPFIRE GLOW");
        go.layer = fire.gameObject.layer;
        go.transform.SetParent(fire, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s_glow;
        sr.color = campfireGlow;
        sr.sortingLayerID = fireArt.sortingLayerID;
        sr.sortingOrder = fireArt.sortingOrder - 1;   // behind its flames
        float size = fireArt.bounds.size.y * campfireGlowSize;
        go.transform.position = new Vector3(fireArt.bounds.center.x, fireArt.bounds.min.y + fireArt.bounds.size.y * 0.35f, fire.position.z + 0.001f);
        var parentScale = fire.lossyScale;
        go.transform.localScale = new Vector3(size / parentScale.x, size / parentScale.y, 1f);
        go.AddComponent<CampfireGlowPulse>();
    }

    void LateUpdate()
    {
        if (_tiles == null) return;
        // The same scroll that scrubs level 1's background (a loop = 1).
        float delta = _player ? _player.LastScrollDelta : 0f;
        float layerDelta = HoldLayers ? 0f : delta;   // (a boss arena in the way: they stay still)
        for (int i = 0; i < layers.Length; i++)
        {
            if (_tiles[i] == null) continue;
            _offsets[i] += layerDelta * layers[i].speed * _pictureWidth;
            Place(_tiles[i], _tileWidths[i], _offsets[i], _layerCenters[i]);
        }
        if (_path != null) PlacePath();
        if (_canopy != null) PlaceCanopy();
        if (_swaying) Shader.SetGlobalFloat("_BambooSway", _pathPixel > 0f ? Sway : 0f);
        if (_stretchClumps != null)
            for (int k = 0; k < stretches.Length; k++)
            {
                _stretchOffsets[k] += delta * stretches[k].speed * _pictureWidth;
                PlaceStretch(k);
            }
        FallingStar();
        Fairies();
    }

    void FallingStar()
    {
        if (!_star) return;
        if (_starTime < 0f)
        {
            if (Time.time < _nextStar) return;
            _starTime = 0f;
            // Somewhere in the top half of the sky.
            float x = Random.Range(_view.min.x + _view.size.x * 0.1f, _view.max.x - _view.size.x * 0.1f);
            float y = Random.Range(_view.center.y + _view.size.y * 0.15f, _view.max.y - _view.size.y * 0.1f);
            _star.transform.position = new Vector3(x, y, _z - 0.01f);
            _star.enabled = true;
        }
        _starTime += Time.deltaTime;
        int frame = (int)(_starTime * fallingStarFps);
        if (frame >= fallingStarFrames.Length)
        {
            _star.enabled = false;
            _starTime = -1f;
            _nextStar = Time.time + Random.Range(fallingStarEvery.x, fallingStarEvery.y);
            return;
        }
        _star.sprite = fallingStarFrames[frame];
    }
}

/// The campfire's glow, breathing like the flames.
public class CampfireGlowPulse : MonoBehaviour
{
    SpriteRenderer _renderer;
    float _alpha;
    void Start() { _renderer = GetComponent<SpriteRenderer>(); _alpha = _renderer.color.a; }
    void Update()
    {
        if (!_renderer) return;
        var c = _renderer.color;
        c.a = _alpha * (0.8f + 0.2f * Mathf.Sin(Time.time * 5f) + 0.05f * Mathf.Sin(Time.time * 13f));
        _renderer.color = c;
    }
}

/// ARcade: keeps a thing on the road (the campfire, the witch) exactly where
/// it is on it as the road scrolls - the knight's AccumulatedFrames times
/// EndlessKnightDirector.RoadUnitsPerFrame, as the spikes, chests and every
/// level's path move - in place of THE PIT's own drift for them
/// (VisualPropSpawnSystem's multiplier, which slid against the road).
/// Applied last each frame. Added by PitDemoGame.
public class RoadPin : MonoBehaviour
{
    float _x;
    bool _set;

    void LateUpdate()
    {
        var player = EndlessKnightDirector.Player;
        if (!player) return;
        float road = player.AccumulatedFrames * EndlessKnightDirector.RoadUnitsPerFrame;
        if (!_set) { _x = transform.position.x + road; _set = true; }   // (its place on the road, at the start)
        transform.position = new Vector3(_x - road, transform.position.y, transform.position.z);
    }
}

/// ARcade: one of level 1's background layers as a strip, in place of its
/// animation's frames (PitDemoGame.LockLevelOneLayers - the mountains, both
/// rows of palms, the road's sidewalk band): copies side by side, the art as
/// it was at the start (its first frame: the strip's first columns), on the
/// same layer and sorting, at the same size - gliding by exactly the distance
/// walked (loopPixels a loop of the art, its pixels), where the frames went in
/// 3 or 4 pixel steps, several a picture at a sprint, and juddered.
public class RoadStrip : MonoBehaviour
{
    Transform[] _copies;
    float _tile, _start, _pixel, _loop;

    /// hide: its frames' renderer hidden (false: just made clear - its bounds still read, the road's).
    public void Build(SpriteRenderer frames, Sprite strip, float loopPixels, bool hide = true)
    {
        if (hide) frames.enabled = false;   // (its animation goes on scrubbing it, unseen)
        else frames.color = Color.clear;
        var t = frames.transform;
        _pixel = t.lossyScale.x / strip.pixelsPerUnit;   // one of its pixels, world units
        _tile = strip.rect.width * _pixel;
        _loop = loopPixels;
        // The strip's first column where the first frame's was.
        _start = t.position.x + (strip.pivot.x - frames.sprite.pivot.x) * _pixel;
        _copies = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject(frames.name + " STRIP " + (i + 1));
            go.layer = frames.gameObject.layer;
            go.transform.SetParent(t.parent, false);
            go.transform.localPosition = t.localPosition;
            go.transform.localRotation = t.localRotation;
            go.transform.localScale = t.localScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = strip;
            sr.sortingLayerID = frames.sortingLayerID;
            sr.sortingOrder = frames.sortingOrder;
            sr.sharedMaterial = frames.sharedMaterial;
            _copies[i] = go.transform;
        }
        LateUpdate();
    }

    void LateUpdate()
    {
        if (_copies == null) return;
        var player = EndlessKnightDirector.Player;
        float walked = player ? player.AccumulatedFrames / 100f * _loop * _pixel : 0f;   // (ScrubBackground: 100 of the counter a loop)
        float x = _start - Mathf.Repeat(walked, _tile);
        for (int i = 0; i < _copies.Length; i++)
        {
            var p = _copies[i].position;
            _copies[i].position = new Vector3(x + (i - 1) * _tile, p.y, p.z);
        }
    }
}
