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
    [Tooltip("World units up at which a shadow has faded away.")]
    public float fadeHeight = 1.2f;
    [Tooltip("Its size against the character's (1: as wide) - smaller for small things; set each frame by a floating thing to grow and shrink as it bobs.")]
    public float size = 1f;
    [Tooltip("Finds its feet from the frame's pixels, not its outline - for art whose outline has clear canvas under it (the chests).")]
    public bool measurePixels;

    // Just over the road (BG road: TopLayer -995), under everything on it.
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
        if (!s_material)
        {
            var shader = Shader.Find("SpatialEmulator/SpriteSolid");
            if (shader) s_material = new Material(shader) { name = "Sprite Shadow" };
        }
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
        float squash = length * Mathf.Lerp(0.6f, 1f, fade) * size;
        float shrink = Mathf.Lerp(0.7f, 1f, fade) * size;
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
        pivotY += raisePixels / sprite.pixelsPerUnit * scaleY;
        st.position = new Vector3(t.position.x, pivotY, t.position.z + 0.001f);

        _shadow.GetPropertyBlock(_block);
        _block.SetColor(ColorId, new Color(tint.r, tint.g, tint.b, opacity * fade));
        _shadow.SetPropertyBlock(_block);
    }

    static float OutlineBottom(Sprite sprite)
    {
        float bottom = float.MaxValue;
        foreach (var v in sprite.vertices) bottom = Mathf.Min(bottom, v.y);
        return bottom;
    }

    static readonly System.Collections.Generic.Dictionary<Sprite, float> s_opaqueBottoms = new System.Collections.Generic.Dictionary<Sprite, float>();

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
            int w = read.width, row = -1;
            for (int y = 0; y < read.height && row < 0; y++)
                for (int x = 0; x < w; x++)
                    if (px[y * w + x].a > 128) { row = y; break; }
            Destroy(read);
            // (Rows up from the frame's rect, its pivot measured from there too.)
            if (row >= 0) bottom = (row - sprite.pivot.y) / sprite.pixelsPerUnit;
        }
        s_opaqueBottoms[sprite] = bottom;
        return bottom;
    }

    static float NonZero(float v) => Mathf.Abs(v) < 1e-5f ? (v < 0f ? -1e-5f : 1e-5f) : v;

    void OnDestroy()
    {
        if (_shadow) Destroy(_shadow.gameObject);
    }
}
