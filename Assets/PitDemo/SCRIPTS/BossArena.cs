using UnityEngine;

// ARcade: a boss's arena (level 3's shrine courtyard, ParallaxLevelBackground
// arenaBack / arenaFloor): enough points for the boss and it comes along
// the road from the right - its sky, hills and shrines on the near
// background layer, its stone floor on the road's under everyone - moving
// with the road as she walks into it. Once it fills the picture it stays
// put (Locked): the road stops scrolling and she walks about in it, and the
// boss comes (EndlessKnightDirector). The boss beaten, it's let go (Release):
// the road scrolls on, it with it.
public class BossArena : MonoBehaviour
{
    /// The courtyard art's dithered ends (shrine_arena_back), art pixels: they fade out to nothing.
    const float DitherPixels = 50f;

    public bool Locked { get; private set; }
    /// Its time of day: the level's walk takes it on while this fills the picture (ParallaxLevelBackground.SetWalkTime).
    [System.NonSerialized] public int WalkTime = -1;
    public bool Released { get; private set; }
    public float LockedAt { get; private set; } = -1f;
    /// The picture's left and right edges, for her to walk between while it's locked.
    public float ViewMinX => _view.min.x;
    public float ViewMaxX => _view.max.x;
    /// Its middle (its torii's): she can't get past it till its boss is beaten (EndlessKnightDirector.BlockAtBoss).
    public float MiddleX => X;
    /// Its reach either side, its bamboo's included (world x).
    public float LeftReach => X + _left;
    public float RightReach => X - _left;

    Bounds _view;
    float _width, _lockX, _lastRoad, _left, _wayReach;   // (_left: its leftmost reach from its middle, its bamboo's)

    static float Road => EndlessKnightDirector.Player ? EndlessKnightDirector.Player.AccumulatedFrames * EndlessKnightDirector.RoadUnitsPerFrame : 0f;

    /// Its two pieces under `parent`: the back on `backLayer` (the near background), the floor on
    /// the road's own group and sorting (`floorOrder`), sized so the art's top is the picture's top
    /// and her feet stand `feetRow` rows down its floor; coming in just past the right edge.
    public static BossArena Make(Transform parent, Bounds view, Sprite back, Sprite floor, int feetRow,
        string backLayer, int floorLayer, int floorSortingLayer, int floorOrder, Sprite edge = null, int edgeRoots = 0, int floorOverlap = 0, Sprite tuft = null, Sprite frontTuft = null, Sprite wall = null)
    {
        var go = new GameObject("BOSS ARENA");
        go.layer = floorLayer;
        go.transform.SetParent(parent, false);
        var arena = go.AddComponent<BossArena>();
        arena._view = view;
        float feet = SpikeTrap.PlayerFeetY;
        // World units per art pixel: from the picture's top down to her feet.
        float px = (view.max.y - feet) / (back.rect.height + feetRow);
        float split = view.max.y - back.rect.height * px;
        float z = parent.position.z;
        arena._width = back.rect.width * px;

        var b = new GameObject("ARENA BACK").AddComponent<SpriteRenderer>();
        b.gameObject.layer = LayerMask.NameToLayer(backLayer);
        b.transform.SetParent(go.transform, false);
        b.transform.localScale = Vector3.one * (px * back.pixelsPerUnit);
        b.transform.position = new Vector3(0f, split, view.min.z + 0.9f);   // (pivot: its bottom middle)
        b.sprite = back;
        b.sortingOrder = 100;   // over the level's own layers in its group

        var f = new GameObject("ARENA FLOOR").AddComponent<SpriteRenderer>();
        f.gameObject.layer = floorLayer;
        f.transform.SetParent(go.transform, false);
        f.transform.localScale = Vector3.one * (px * floor.pixelsPerUnit);
        f.transform.position = new Vector3(0f, split + floorOverlap * px, z);   // (pivot: its top middle - a few of the back's rows again, over the road's top edge)
        f.sprite = floor;
        f.sortingLayerID = floorSortingLayer;
        f.sortingOrder = floorOrder;

        // Its ways in and out: stands of bamboo out from each edge - in front of the road (she walks through
        // them) and on it behind her - a sparse grove, the level's far layers seen through it, none of it
        // over the courtyard's art.
        if (frontTuft || tuft)
        {
            var frontArt = frontTuft ? frontTuft : tuft;
            float roots = feet - 0.28f;
            float fs = (view.max.y - roots) / frontArt.rect.height * frontArt.pixelsPerUnit * 1.05f;
            float frontWidth = frontArt.rect.width / frontArt.pixelsPerUnit * fs;
            float reach = 0.05f + frontWidth * 1.4f;
            arena._wayReach = reach;
            float groundRoots = feet + 0.06f;
            float gs = (view.max.y - groundRoots) / frontArt.rect.height * frontArt.pixelsPerUnit * 1.04f;
            float gw = frontArt.rect.width / frontArt.pixelsPerUnit * gs;
            int stands = Mathf.CeilToInt(reach / (0.5f * frontWidth * 0.82f));
            for (int side = -1; side <= 1; side += 2)
            {
                float edgeX = side * arena._width * 0.5f;   // (from its middle)
                // A solid wall of bamboo (no gaps) behind the stretch nearest the courtyard, from the canopy down to
                // the floor's back line - behind the courtyard art (never over it: its lanterns show), in under its
                // dithered end (its holes show bamboo, not the level's far layers), the far stretch left open.
                if (wall)
                {
                    float wroots = split - (view.max.y - split) / Mathf.Max(1, edgeRoots) * 4f;
                    float top = view.max.y + 0.05f;
                    float tws = (top - wroots) / wall.rect.height * wall.pixelsPerUnit;
                    float tw = wall.rect.width / wall.pixelsPerUnit * tws;
                    float under = DitherPixels * px;   // (the courtyard art's dithered end)
                    int tiles = Mathf.Max(1, Mathf.CeilToInt((reach * 0.45f + under) / (tw * 0.98f)));
                    for (int n = 0; n < tiles; n++)
                    {
                        var t = new GameObject("ARENA WAY WALL").AddComponent<SpriteRenderer>();
                        t.gameObject.layer = b.gameObject.layer;
                        t.transform.SetParent(go.transform, false);
                        t.transform.localScale = new Vector3((n % 2 == 0 ? 1f : -1f) * tws, tws, 1f);   // (mirrored every other: seamless)
                        t.transform.position = new Vector3(edgeX + side * (-under + (n + 0.5f) * tw * 0.98f), (top + wroots) * 0.5f, view.min.z + 0.91f);
                        t.sprite = wall;
                        t.sortingOrder = 99;   // (behind the courtyard art, 100)
                    }
                    // ...and over its dithered end itself, just as wide (no checkered strip; its solid art, the
                    // lanterns, untouched).
                    float texels = Mathf.Min(wall.rect.width, under / tw * wall.rect.width);
                    var cut = Sprite.Create(wall.texture, new Rect(wall.rect.x, wall.rect.y, texels, wall.rect.height), new Vector2(0.5f, 0.5f), wall.pixelsPerUnit);
                    var strip = new GameObject("ARENA WAY SEAM").AddComponent<SpriteRenderer>();
                    strip.gameObject.layer = b.gameObject.layer;
                    strip.transform.SetParent(go.transform, false);
                    strip.transform.localScale = new Vector3(tws, tws, 1f);
                    strip.transform.position = new Vector3(edgeX - side * under * 0.5f, (top + wroots) * 0.5f, view.min.z + 0.89f);
                    strip.sprite = cut;
                    strip.sortingOrder = 101;   // (over the courtyard art's end)
                }
                // (two in front, one after the other: the first just off the picture once it locks - the fight has all of it)
                for (int n = 0; n < 2; n++)
                {
                    var front = new GameObject("ARENA BAMBOO FRONT").AddComponent<SpriteRenderer>();
                    front.gameObject.layer = floorLayer;
                    front.transform.SetParent(go.transform, false);
                    front.transform.localScale = new Vector3(side * fs, fs, 1f);   // (its sunlit side toward the courtyard)
                    front.transform.position = new Vector3(edgeX + side * (0.05f + n * frontWidth * 0.82f), roots, z - 0.05f - n * 0.001f);
                    front.sprite = frontArt;
                    front.sortingLayerName = "Default";
                    front.sortingOrder = 300 + n;   // (over the knight, 150, and her slash, 190)
                }
                // (on the road just behind where everyone walks, all along the way in and out, from the courtyard art's edge out)
                for (int n = 0; n < stands; n++)
                {
                    var stand = new GameObject("ARENA BAMBOO GROUND").AddComponent<SpriteRenderer>();
                    stand.gameObject.layer = floorLayer;
                    stand.transform.SetParent(go.transform, false);
                    stand.transform.localScale = new Vector3((n % 2 == 0 ? -side : side) * gs, gs, 1f);
                    stand.transform.position = new Vector3(edgeX + side * (gw * 0.45f + n * 0.5f * frontWidth * 0.82f), groundRoots, z - 0.002f - n * 0.001f);
                    stand.sprite = frontArt;
                    stand.sortingLayerID = floorSortingLayer;
                    stand.sortingOrder = floorOrder + 3 + n;   // (over the floor and the path, under the characters)
                }
            }
        }
        // Coming in from just past the right edge (its way in too); staying put once centred in the picture.
        // (all of it - its bamboo out in front too - past the edge: nothing pops into view)
        arena._x = 0f;
        float left = float.MaxValue;
        foreach (var r in go.GetComponentsInChildren<SpriteRenderer>()) left = Mathf.Min(left, r.bounds.min.x);
        arena.SetX(view.max.x + 0.15f - left);
        arena._left = left;
        arena._lockX = view.center.x;
        arena._lastRoad = Road;
        return arena;
    }

    // Its middle; all its pieces moved with it.
    float _x;
    void SetX(float x)
    {
        float dx = x - _x;
        foreach (Transform t in transform) t.position += Vector3.right * dx;
        _x = x;
        if (_blend) _blend.SetFloat(CenterXId, _x);
        if (_swayBlend) _swayBlend.SetFloat(CenterXId, _x);
    }

    // ARcade: its ways in and out fading from the walk's look (out along them) into its own time (by the courtyard):
    // each piece's art in the walk's time (`entrance`: the walk she came by, left; `exit`: the one she'll go on by,
    // right - by its own art's texture), mixed in by SpriteTimeBlend.
    Material _blend;
    /// Its ways' blend (SpriteTimeBlend: its middle kept up to date as it moves) - the level's road under them takes it too.
    public Material Blend => _blend;
    /// The same, bending in the wind (the level's swaying far bamboo).
    public Material SwayBlend => _swayBlend;
    Material _swayBlend;
    /// How far its fade reaches on out past its ways (into the walk's far layers and road): it comes in that much
    /// further off to the right, and is gone only that much further behind her - never seen to start or stop.
    public float Lead { get; private set; }
    static readonly int CenterXId = Shader.PropertyToID("_CenterX");
    public static readonly int AltTexId = Shader.PropertyToID("_AltTex"), AltTexRId = Shader.PropertyToID("_AltTexR");
    public void BlendWays(Material blend, System.Collections.Generic.IDictionary<Texture, Texture> entrance, System.Collections.Generic.IDictionary<Texture, Texture> exit,
        Vector2 exitFade = default, Vector2 exitPeak = default)
    {
        if (!blend) return;
        _blend = new Material(blend) { name = "Arena Way Blend" };
        _blend.SetFloat("_HalfWidth", _width * 0.5f);
        _blend.SetFloat("_Reach", Mathf.Max(0.01f, _wayReach));
        // Its way out: from exitFade.x of the picture's width out from its edge to .y, the walk's look (none: as its way in).
        float reach = Mathf.Max(0.01f, _wayReach), wide = _view.size.x;
        float from = exitFade.y > exitFade.x ? 1f - exitFade.y * wide / reach : _blend.GetFloat("_Start");
        float over = exitFade.y > exitFade.x ? (exitFade.y - exitFade.x) * wide / reach : _blend.GetFloat("_Span");
        _blend.SetFloat("_StartR", from);
        _blend.SetFloat("_SpanR", over);
        // Of its fade, at most exitPeak.x the arena's look, from all of it at its edge over .y of the picture's width (none: all of it).
        _blend.SetFloat("_PeakR", exitPeak.x > 0f ? exitPeak.x : 1f);
        _blend.SetFloat("_NearR", exitPeak.x > 0f ? Mathf.Max(1e-4f, exitPeak.y * wide / reach) : 1e-4f);
        _blend.SetFloat(CenterXId, _x);
        _swayBlend = new Material(_blend) { name = "Arena Way Blend (sway)" };
        _swayBlend.SetFloat("_Sway", 1f);
        Lead = Mathf.Max(0f, -_blend.GetFloat("_Start")) * _wayReach;
        if (!Locked) SetX(_x + Lead);
        var block = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!r.sprite || !(r.name.StartsWith("ARENA WAY") || r.name.StartsWith("ARENA BAMBOO"))) continue;
            var tex = r.sprite.texture;
            Texture inAlt = null, outAlt = null;
            if (entrance == null || !entrance.TryGetValue(tex, out inAlt) || !inAlt) inAlt = tex;
            if (exit == null || !exit.TryGetValue(tex, out outAlt) || !outAlt) outAlt = tex;
            r.sharedMaterial = _blend;
            r.GetPropertyBlock(block);
            block.SetTexture(AltTexId, inAlt);
            block.SetTexture(AltTexRId, outAlt);
            r.SetPropertyBlock(block);
        }
    }

    float X => _x;

    /// She's come up to its middle before it's settled in the picture: it stays put where it is (its boss comes now).
    public void LockNow()
    {
        if (Locked) return;
        Locked = true;
        LockedAt = Time.time;
    }

    /// Its boss beaten: the road takes it on again.
    public void Release()
    {
        if (!Locked || Released) return;
        Released = true;
        _lastRoad = Road;
    }

    void Update()
    {
        var player = EndlessKnightDirector.Player;
        if (!player) { Destroy(gameObject); return; }
        if (WalkTime >= 0 && Locked && (Covers() || Released)) TakeTime();   // (or, never filling it, as its boss falls)
        // Its time taken, and gone by off the left: its way in too (never back into the walk before it, walking back).
        if (WalkTime < 0 && Locked && !_entranceTaken && X - _width * 0.5f < _view.min.x) TakeEntrance();
        float road = Road;
        float moved = road - _lastRoad;
        _lastRoad = road;
        if (!Locked || Released)
        {
            float x = X - moved;
            if (!Locked && x <= _lockX)
            {
                // In: it fills the picture - the road stops here.
                x = _lockX;
                Locked = true;
                LockedAt = Time.time;
            }
            SetX(x);
        }
        player.ScrollLocked = Locked && !Released;
        ClearAround();
        // (Let go, it stays on the road - back there if she walks back - till it's well behind her.)
        if (Released && (X + _width * 0.5f < _view.min.x - 25f || X - _width * 0.5f > _view.max.x + 25f)) Destroy(gameObject);
    }

    // Nothing of the level's own on top of it: its props (statues, rocks, lanterns) and its runs of
    // tall grass are hidden while they're over it (its faded ends aside), and back once it's gone by.
    readonly System.Collections.Generic.List<GameObject> _hidden = new System.Collections.Generic.List<GameObject>();
    void ClearAround()
    {
        var level = transform.parent;
        if (!level) return;
        float fade = _width * 0.06f, min = X - _width * 0.5f + fade, max = X + _width * 0.5f - fade;
        var gate = level.GetComponent<ParallaxLevelBackground>() ? level.GetComponent<ParallaxLevelBackground>().StandingGate : null;
        foreach (Transform t in level)
        {
            if (t == transform || t == gate || !(t.name.StartsWith("PROP ") || t.name.StartsWith("STRETCH "))) continue;
            var sr = t.GetComponent<SpriteRenderer>();
            if (!sr || !sr.sprite) continue;
            float sx = t.lossyScale.x, cx = t.position.x + sr.sprite.bounds.center.x * sx, half = sr.sprite.bounds.extents.x * Mathf.Abs(sx);
            bool prop = t.name.StartsWith("PROP ");
            bool over = cx + half > (prop ? min - fade - _wayReach : min) && cx - half < (prop ? max + fade + _wayReach : max);   // (its props hidden along its ways in and out too)
            if (over && t.gameObject.activeSelf) { t.gameObject.SetActive(false); _hidden.Add(t.gameObject); }
            else if (!over && !t.gameObject.activeSelf && _hidden.Remove(t.gameObject)) t.gameObject.SetActive(true);
        }
    }

    // All of the picture behind it (its back, side to side): the walk can change unseen.
    bool Covers()
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (var r in GetComponentsInChildren<SpriteRenderer>()) { min = Mathf.Min(min, r.bounds.min.x); max = Mathf.Max(max, r.bounds.max.x); }
        return min <= _view.min.x && max >= _view.max.x;
    }

    void TakeTime()
    {
        var level = transform.parent ? transform.parent.GetComponent<ParallaxLevelBackground>() : null;
        if (level) level.SetWalkTime(WalkTime);
        WalkTime = -1;
    }

    // Its way in faded into the walk it leaves by (as its way out), not the one it came from: the ways' bamboo here,
    // the level's road and far layers (ParallaxLevelBackground.TakeEntrance).
    bool _entranceTaken;
    void TakeEntrance()
    {
        _entranceTaken = true;
        foreach (var r in GetComponentsInChildren<SpriteRenderer>(true)) if (r.sharedMaterial == _blend) Forward(r);
        var level = transform.parent ? transform.parent.GetComponent<ParallaxLevelBackground>() : null;
        if (level) level.TakeEntrance(this);
    }

    /// Its walk's look to the left the same as to the right (its way out's).
    public static void Forward(Renderer r)
    {
        var block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        var right = block.GetTexture(AltTexRId);
        if (right) { block.SetTexture(AltTexId, right); r.SetPropertyBlock(block); }
    }

    void OnDestroy()
    {
        var level = transform.parent ? transform.parent.GetComponent<ParallaxLevelBackground>() : null;
        if (level) level.EndGroundBlend(this);
        if (level) level.HoldLayers = false;
        foreach (var go in _hidden) if (go) go.SetActive(true);
        var player = EndlessKnightDirector.Player;
        if (player) player.ScrollLocked = false;
    }
}
