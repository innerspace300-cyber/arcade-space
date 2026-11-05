using UnityEngine;

// ARcade: a character in the level's light (ParallaxLevelBackground.characterLight
// - level 2's moonlit night: dimmer and cooler; level 3's green daylight
// through the bamboo; level 1 as drawn). Its sprite's colour multiplied by
// the light, on top of whatever the game sets it to (the Slime Boss's frost
// blue each frame) - never twice over. Added with a character's shadow
// (SpriteShadow), so to the knight, the witch, bomb guys, bosses and chests;
// not the campfire (it's the light).
[DefaultExecutionOrder(310)]   // after the characters (and their shadows) have set their colours
public class LevelLit : MonoBehaviour
{
    /// The level's light on its characters (white: as drawn).
    public static Color Light = Color.white;

    SpriteRenderer _sprite;
    Color _base = Color.white, _set = Color.white;
    bool _started;

    static Material s_solid;
    static readonly int ColorId = Shader.PropertyToID("_Color");
    SpriteRenderer _night;
    MaterialPropertyBlock _block;
    Color _lit;

    void Start()
    {
        _sprite = GetComponent<SpriteRenderer>();
        // A shader graph that ignores the sprite's colour (the knight's): her own shader kept,
        // and over her a copy of her sprite multiplying what's drawn by the light (SpriteMultiply).
        if (_sprite && Light != Color.white && _sprite.sharedMaterial && _sprite.sharedMaterial.shader.name.StartsWith("Shader Graphs/"))
        {
            if (!s_solid) s_solid = Resources.Load<Material>("LevelLitMultiply");   // (SpatialEmulator/SpriteMultiply)
            if (!s_solid) return;
            var go = new GameObject("NIGHT");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            _night = go.AddComponent<SpriteRenderer>();
            _night.sharedMaterial = s_solid;
            _block = new MaterialPropertyBlock();
            // Multiplying her by the light, as the others' colours are.
            _block.SetColor(ColorId, Light);
            _night.SetPropertyBlock(_block);
        }
    }

    void LateUpdate()
    {
        if (!_sprite && !(_sprite = GetComponent<SpriteRenderer>())) return;
        if (_night)
        {
            if (_block != null && _lit != Light) { _lit = Light; _block.SetColor(ColorId, Light); _night.SetPropertyBlock(_block); }   // (a new light: level 3's arena times)
            // Following her frame, facing, draw order and visibility (just over her).
            _night.sprite = _sprite.sprite;
            _night.flipX = _sprite.flipX;
            _night.flipY = _sprite.flipY;
            _night.sortingLayerID = _sprite.sortingLayerID;
            _night.sortingOrder = _sprite.sortingOrder + 1;
            _night.enabled = _sprite.enabled && _sprite.sprite;
            return;
        }
        var now = _sprite.color;
        if (!_started || now != _set) { _base = now; _started = true; }   // (the game's own colour, set since)
        _set = new Color(_base.r * Light.r, _base.g * Light.g, _base.b * Light.b, _base.a);
        _sprite.color = _set;
    }
}
