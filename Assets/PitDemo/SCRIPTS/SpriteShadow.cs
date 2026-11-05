using UnityEngine;

// ARcade: a cast shadow for a character - the sunset is behind the road, so
// it falls toward the viewer: a copy of the character's current frame (the
// exact pose, every animation), a flat sunset-tinted dark and see-through, flipped and
// squashed down onto the road from where its feet meet the ground. It stays
// on the ground when the character leaves it (a jump, a bomb guy's leap),
// shrinking and fading the higher they go. Drawn just over the road, under
// every character, on the characters' depth layer (so it stays at their feet
// when the layers are spread).
// Added to the knight and the witch by PitDemoGame, to bomb guys (BombGuy)
// and Slime Bosses (EndlessKnightDirector) as they come.
[DefaultExecutionOrder(300)]   // after the characters have moved and animated
public class SpriteShadow : MonoBehaviour
{
    [Tooltip("The character's sprite (default: this object's).")]
    public SpriteRenderer source;
    [Tooltip("Its height, flipped onto the road (1 = as tall as the character).")]
    public float length = 0.9f;
    [Range(0f, 1f)] public float opacity = 0.7f;
    [Tooltip("Its colour: tinted by the pink sunset behind them.")]
    public Color tint = new Color32(72, 6, 64, 255);
    [Tooltip("Starts on the road line (where the knight stands) even if the sprite's lowest pixels hang below it - the Slime Boss's drips.")]
    public bool fromRoadLine;
    [Tooltip("With fromRoadLine: the line is the bottom of this collider (the character's feet) instead of the knight's.")]
    public Collider groundFrom;
    [Tooltip("The ground's height, if not the knight's road line (NaN: the road line) - e.g. a chest's landing spot.")]
    public float groundAt = float.NaN;
    [Tooltip("Raised this many of the character's pixels, up under its feet (the knight's lowest pixels sit a little below where she stands).")]
    public float raisePixels;
    [Tooltip("While this is true, from the frame's lowest solid pixels after all (not the road line) - the knight's Great Heal plate, drawn under her boots, would hide the shadow's top.")]
    public System.Func<bool> fromDrawnBottom;
    [Tooltip("With fromRoadLine: the line is this far below the road's (world units) - the knight walks down on the sidewalk.")]
    public float belowRoad;
    [Tooltip("World units the character's drawn lifted off where it stands (RoadEnemy liftPixels): not a jump - its reflection stays put.")]
    public float standLift;
    [Tooltip("World units up at which a shadow has faded away.")]
    public float fadeHeight = 1.2f;
    [Tooltip("Its size against the character's (1: as wide) - smaller for small things; set each frame by a floating thing to grow and shrink as it bobs.")]
    public float size = 1f;
    [Tooltip("Finds its feet from the frame's pixels, not its outline - for art whose outline has clear canvas under it (the chests).")]
    public bool measurePixels;
    [Tooltip("The character itself in the level's light too (LevelLit) - not a light source (the campfire).")]
    public bool lit = true;

    /// The level's light (ParallaxLevelBackground: a night's shadows near black and faint): in place
    /// of each shadow's own tint (clear: its own), and its opacity scaled.
    public static Color LevelTint = Color.clear;
    public static float LevelOpacity = 1f;
    /// A level whose ground is like still water (ParallaxLevelBackground.reflections - level 4): the
    /// characters (the knight, the witches, foes, bosses) cast reflections instead - their sprite flipped
    /// below their feet at full height, tinted ReflectionTint, rippling and fading with depth.
    public static bool Reflections;
    public static Color ReflectionTint = new Color(0.65f, 0.8f, 1f, 0.45f);
    /// The water's edge the reflections start at: this far (a share of the picture's height) below the road line
    /// where they stand - a little lower than a shadow, a gap between each and its reflection.
    public static float ReflectionDrop = 0.04f;
    /// ...and this many of the characters' pixels further down.
    public static float ReflectionDropPixels = 4f;
    static Material s_reflection;
    static readonly int RectId = Shader.PropertyToID("_Rect");
    bool _character;

    // Just over the road (BG road: TopLayer -995), under everything on it.
    /// A level with the bamboo's shade (ParallaxLevelBackground's canopy): every character carries a mask
    /// of its own shape, so the shade shows on it, and only on it.
    public static bool CanopyMasks;
    /// The canopy's shade on the characters draws at this order (Default), inside their masks.
    public const int CanopyShadeOrder = 610;
    SpriteMask _shapeMask;

    // Its shape as a mask (its frame, facing, place, every frame) - for the canopy's shade on it.
    void ShapeMask()
    {
        if (!_shapeMask)
        {
            var go = new GameObject("SHADE MASK");
            go.layer = source.gameObject.layer;
            go.transform.SetParent(source.transform, false);
            _shapeMask = go.AddComponent<SpriteMask>();
            _shapeMask.isCustomRangeActive = true;
            _shapeMask.frontSortingLayerID = _shapeMask.backSortingLayerID = UnityEngine.SortingLayer.NameToID("Default");
            _shapeMask.backSortingOrder = CanopyShadeOrder - 1;
            _shapeMask.frontSortingOrder = CanopyShadeOrder;
            _shapeMask.alphaCutoff = 0.5f;
        }
        _shapeMask.sprite = source.sprite;
        _shapeMask.transform.localScale = new Vector3(source.flipX ? -1f : 1f, source.flipY ? -1f : 1f, 1f);
        _shapeMask.enabled = source.enabled;
    }

    const string SortingLayer = "TopLayer";
    const int SortingOrder = -990;

    static Material s_material;
    static readonly int ColorId = Shader.PropertyToID("_Color");

    SpriteRenderer _shadow;
    MaterialPropertyBlock _block;
    Sprite _measured;
    float _bottomLocal;   // the frame's lowest drawn pixel, in its own units

    /// A shadow for `renderer` (once).
    public static SpriteShadow Cast(SpriteRenderer renderer)
    {
        if (!renderer) return null;
        var shadow = renderer.GetComponent<SpriteShadow>();
        if (!shadow) shadow = renderer.gameObject.AddComponent<SpriteShadow>();
        shadow.source = renderer;
        return shadow;
    }

    void Start()
    {
        if (!source) source = GetComponent<SpriteRenderer>();
        if (lit && source && !source.GetComponent<LevelLit>()) source.gameObject.AddComponent<LevelLit>();
        if (!s_material)
        {
            var shader = Shader.Find("SpatialEmulator/SpriteSolid");
            if (shader) s_material = new Material(shader) { name = "Sprite Shadow" };
        }
        // A character (one that's reflected, on a level with reflections): the knight, a witch, a foe, a boss.
        _character = source && (source.GetComponentInParent<PlayerScriptARIANAClips>() || source.GetComponentInParent<RoadEnemy>()
            || source.GetComponentInParent<RoadBoss>() || source.GetComponentInParent<BombGuy>()
            || source.GetComponentInParent<EnemyStateMachineSlimeBoss>() || source.name.Contains("WITCH") || transform.name.Contains("WITCH"));
        if (!s_reflection) s_reflection = Resources.Load<Material>("SpriteReflection");   // (a material: its shader kept in the build)
        var go = new GameObject("Shadow");
        go.layer = source ? source.gameObject.layer : gameObject.layer;
        go.transform.SetParent(transform, false);
        _shadow = go.AddComponent<SpriteRenderer>();
        if (s_material) _shadow.sharedMaterial = s_material;
        _shadow.sortingLayerName = SortingLayer;
        _shadow.sortingOrder = SortingOrder;
        _block = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        if (!_shadow) return;
        bool on = source && source.enabled && source.gameObject.activeInHierarchy && source.sprite;
        if (CanopyMasks && source && lit) ShapeMask();   // (not a light source: the campfire)
        else if (_shapeMask) _shapeMask.enabled = false;
        if (_shadow.enabled != on) _shadow.enabled = on;
        if (!on) return;

        var sprite = source.sprite;
        if (sprite != _measured)
        {
            // Its feet: the lowest point of the frame's outline (not the
            // sprite's rect - some have clear canvas under them).
            _measured = sprite;
            _bottomLocal = measurePixels ? OpaqueBottom(sprite) : OutlineBottom(sprite);
        }
        // (Under Great Heal's plate: from its lowest solid pixels - its outline
        // takes in the faint glow far under it.)
        bool underDrawn = fromDrawnBottom != null && fromDrawnBottom();
        float bottomLocal = underDrawn ? OpaqueBottom(sprite) : _bottomLocal;
        _shadow.sprite = sprite;
        _shadow.flipX = source.flipX;
        _shadow.gameObject.layer = source.gameObject.layer;

        var t = source.transform;
        float scaleY = Mathf.Abs(t.lossyScale.y);
        float feet = t.position.y + (source.flipY ? -bottomLocal : bottomLocal) * scaleY;
        // The ground: where the feet are, unless they're off it (the road's line).
        float roadLine = (groundFrom ? groundFrom.bounds.min.y : !float.IsNaN(groundAt) ? groundAt : SpikeTrap.GroundY) - belowRoad;
        bool fromLine = fromRoadLine && !underDrawn;
        float ground = SpikeTrap.GroundY == 0f && !groundFrom && float.IsNaN(groundAt) ? feet : fromLine ? roadLine : Mathf.Min(feet, roadLine);
        float height = Mathf.Max(0f, feet - ground);
        float fade = Mathf.Clamp01(1f - height / Mathf.Max(0.01f, fadeHeight));

        // Flipped and squashed: the frame's feet row lands on the ground and
        // the rest of it lies below, toward the viewer. Shrinks as they rise.
        // (A reflection: not squashed - all of it, as tall as they are, mirrored.)
        bool reflect = Reflections && _character && s_reflection;
        var material = reflect ? s_reflection : s_material;
        if (material && _shadow.sharedMaterial != material) _shadow.sharedMaterial = material;
        if (reflect)
        {
            // Mirrored in the water: its feet (the frame's lowest solid pixels, not its outline - some have
            // clear canvas under them) on the water's edge below the road line, lower still as it rises.
            float solid = OpaqueBottom(sprite);
            float solidFeet = t.position.y + (source.flipY ? -solid : solid) * scaleY;
            var cam = SpatialEmulator.Demo.PitDemoGame.ActorsCamera;
            float drop = (cam && cam.orthographic ? cam.orthographicSize * 2f * ReflectionDrop : 0.15f) + ReflectionDropPixels * RoadFoe.Pixel;
            float road = SpikeTrap.GroundY != 0f ? SpikeTrap.GroundY : solidFeet;
            float up = Mathf.Max(0f, solidFeet - standLift - road);
            float water = road - drop - up;   // (the same water line for all, whatever their size - growing, she and the vikings keep theirs)
            bottomLocal = solid;
            ground = water;
            height = up;
            fade = Mathf.Clamp01(1f - height / Mathf.Max(0.01f, fadeHeight));
            fromLine = false;
        }
        float squash = (reflect ? 1f : length * Mathf.Lerp(0.6f, 1f, fade)) * size;
        float shrink = (reflect ? 1f : Mathf.Lerp(0.7f, 1f, fade)) * size;
        var st = _shadow.transform;
        st.rotation = Quaternion.identity;
        // (In world terms: as wide as the character, turned the way it's
        // turned - some turn by mirroring their whole transform - and
        // flipped and squashed upright.)
        var parentScale = transform.lossyScale;
        st.localScale = new Vector3(shrink * t.lossyScale.x / NonZero(parentScale.x), -squash * scaleY / NonZero(parentScale.y), 1f);
        // Its feet row (local _bottomLocal) flipped to above the pivot: put the pivot below the ground by that much.
        // (From the road line, the flipped frame starts with its row level
        // with that line - as much of it hidden behind the sprite as hangs
        // below it.)
        float anchorBottom = fromLine ? bottomLocal + Mathf.Max(0f, ground - feet) / Mathf.Max(1e-5f, scaleY) : bottomLocal;
        float pivotY = ground + anchorBottom * squash * scaleY;
        if (!reflect) pivotY += raisePixels / sprite.pixelsPerUnit * scaleY;
        st.position = new Vector3(t.position.x, pivotY, t.position.z + 0.001f);

        _shadow.GetPropertyBlock(_block);
        if (reflect)
        {
            // Its frame's place in its sheet: the ripple and the fade run down from its feet.
            var tex = sprite.texture;
            var r = sprite.textureRect;
            float vBottom = tex ? (r.y + (bottomLocal * sprite.pixelsPerUnit + sprite.pivot.y)) / tex.height : 0f;
            float vHeight = tex ? (r.yMax / tex.height - vBottom) : 1f;
            _block.SetVector(RectId, new Vector4(vBottom, Mathf.Max(1e-4f, vHeight), 0f, 0f));
            _block.SetColor(ColorId, new Color(ReflectionTint.r, ReflectionTint.g, ReflectionTint.b, ReflectionTint.a * fade));
        }
        else
        {
            var c = LevelTint.a > 0f ? LevelTint : tint;
            _block.SetColor(ColorId, new Color(c.r, c.g, c.b, opacity * LevelOpacity * fade));
        }
        _shadow.SetPropertyBlock(_block);
    }

    static float OutlineBottom(Sprite sprite)
    {
        float bottom = float.MaxValue;
        foreach (var v in sprite.vertices) bottom = Mathf.Min(bottom, v.y);
        return bottom;
    }

    static readonly System.Collections.Generic.Dictionary<Sprite, float> s_opaqueBottoms = new System.Collections.Generic.Dictionary<Sprite, float>();
    static readonly System.Collections.Generic.Dictionary<Sprite, float> s_opaqueTops = new System.Collections.Generic.Dictionary<Sprite, float>();

    /// ARcade: the highest row of a frame's solid pixels, in its own units from its pivot (OpaqueBottom's
    /// other end: a foe's head, not the clear canvas over it).
    public static float OpaqueTop(Sprite sprite)
    {
        if (!sprite) return 0f;
        if (!s_opaqueTops.ContainsKey(sprite)) OpaqueBottom(sprite);
        return s_opaqueTops.TryGetValue(sprite, out float top) ? top : 0f;
    }

    /// The lowest row of a frame's (mostly) solid pixels, in its own units
    /// from its pivot - read back from the GPU once per frame of art.
    public static float OpaqueBottom(Sprite sprite)
    {
        if (!sprite) return 0f;
        if (s_opaqueBottoms.TryGetValue(sprite, out float known)) return known;
        float bottom = OutlineBottom(sprite);
        var tex = sprite.texture;
        if (tex)
        {
            var rect = sprite.rect;   // (its own sheet, not packed in an atlas)
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var read = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
            read.ReadPixels(rect, 0, 0);
            read.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = read.GetPixels32();
            int w = read.width, row = -1, topRow = -1;
            for (int y = 0; y < read.height && row < 0; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 128) { row = y; break; }
            for (int y = read.height - 1; y >= 0 && topRow < 0; y--)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 128) { topRow = y; break; }
            if (topRow >= 0) s_opaqueTops[sprite] = (topRow + 1 - sprite.pivot.y) / sprite.pixelsPerUnit;
            Destroy(read);
            // (Rows up from the frame's rect, its pivot measured from there too.)
            if (row >= 0) bottom = (row - sprite.pivot.y) / sprite.pixelsPerUnit;
        }
        s_opaqueBottoms[sprite] = bottom;
        if (!s_opaqueTops.ContainsKey(sprite)) { float top = float.MinValue; foreach (var v in sprite.vertices) top = Mathf.Max(top, v.y); s_opaqueTops[sprite] = top; }
        return bottom;
    }

    static float NonZero(float v) => Mathf.Abs(v) < 1e-5f ? (v < 0f ? -1e-5f : 1e-5f) : v;

    void OnDestroy()
    {
        if (_shadow) Destroy(_shadow.gameObject);
    }
}
