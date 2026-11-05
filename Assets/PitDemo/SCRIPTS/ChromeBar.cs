using UnityEngine;
using UnityEngine.UI;

// ARcade: ENDLESS KNIGHT's health bar fill as rainbow liquid chrome (ChromeBar.shader):
// its own copy of the material, told where its sprite sits in its sheet; the rainbow
// flowing (faster for a moment on a hit or a heal - Kick), and a glint sweeping across
// every few seconds. Paused with the game. On an animated image (the flask's splash, its
// hit, its break and drips) it follows the frames, and chromes only their white.
[RequireComponent(typeof(Image))]
public class ChromeBar : MonoBehaviour
{
    public Material chrome;
    public float glintEvery = 3.5f, glintTime = 0.6f;
    [Tooltip("1: the bar; less: a dimmer one behind it (the damage trail).")]
    public float brightness = 1f, flow = 1f, hueShift = 0f, hueRange = 1f;
    [Tooltip("Only the art's pale pixels chromed (its gold glass and frame kept).")]
    public bool onlyPale;

    static float s_kick;
    static int s_kickFrame;
    Image _image;
    Material _mat;
    Sprite _shownSprite;
    float _phase, _glintAt;

    /// A hit or a heal: the liquid surges.
    public static void Kick(float amount = 1f) => s_kick = Mathf.Max(s_kick, amount);

    void OnEnable()
    {
        _image = GetComponent<Image>();
        if (!chrome || !_image) return;
        if (!_mat) _mat = new Material(chrome) { hideFlags = HideFlags.DontSave };
        _image.material = _mat;
        _mat.SetFloat("_Bright", brightness);
        _mat.SetFloat("_HueShift", hueShift);
        _mat.SetFloat("_HueRange", hueRange);
        _mat.SetFloat("_OnlyPale", onlyPale ? 1f : 0f);
        _shownSprite = null;
        _image.SetMaterialDirty();
        _glintAt = Time.time + 1f;
    }

    // Where its sprite sits in its texture (each frame of an animation its own).
    static Vector4 RectOf(Sprite s)
    {
        if (!s || !s.texture) return new Vector4(0f, 0f, 1f, 1f);
        var r = s.textureRect;
        float w = s.texture.width, h = s.texture.height;
        return new Vector4(r.x / w, r.y / h, r.width / w, r.height / h);
    }

    void Update()
    {
        if (!_mat) return;
        float dt = Time.deltaTime;
        _phase += dt * flow * (1f + s_kick * 5f);
        if (s_kickFrame != Time.frameCount) { s_kickFrame = Time.frameCount; s_kick = Mathf.MoveTowards(s_kick, 0f, dt * 1.5f); }
        if (Time.time > _glintAt + glintTime) _glintAt = Time.time + glintEvery;
        float g = (Time.time - _glintAt) / glintTime;
        // (under its Mask the image draws with a stencil copy of the material, made once: set that too)
        var shown = _image.materialForRendering;
        var bar = BarInCanvas();
        foreach (var m in shown && shown != _mat ? new[] { _mat, shown } : new[] { _mat })
        {
            m.SetFloat("_Phase", _phase);
            m.SetFloat("_Glint", g >= 0f && g <= 1f ? g : -1f);
            if (_image.sprite != _shownSprite) m.SetVector("_Rect", RectOf(_image.sprite));
            m.SetVector("_Bar", bar);
        }
        _shownSprite = _image.sprite;
    }

    // A tiled bar (its sprite repeated along it): where the whole of it sits in its canvas,
    // and how big one of the art's pixels is there - so the chrome runs along all of it.
    Vector4 BarInCanvas()
    {
        var canvas = _image.canvas;
        if (_image.type != Image.Type.Tiled || !canvas || _image.pixelsPerUnit <= 0f) return Vector4.zero;
        var rt = _image.rectTransform;
        Rect r = rt.rect;
        float x0 = canvas.transform.InverseTransformPoint(rt.TransformPoint(new Vector3(r.xMin, r.center.y))).x;
        float x1 = canvas.transform.InverseTransformPoint(rt.TransformPoint(new Vector3(r.xMax, r.center.y))).x;
        float w = x1 - x0;
        if (w <= 0f || r.width <= 0f) return Vector4.zero;
        return new Vector4(x0, w, w / r.width / _image.pixelsPerUnit, 0f);
    }

    void OnDestroy()
    {
        if (_mat) Destroy(_mat);
    }
}
