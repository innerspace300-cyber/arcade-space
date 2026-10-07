using TMPro;
using UnityEngine;

// ARcade: REST (and beside it C with a down arrow, the key to press), in
// the HUD's pixel font, over the knight's head while she
// could rest (PlayerScriptARIANAClips.CanRest: stood idle a few seconds with
// health or mana to get back) - C then sits her down (PitDemoGame). It
// fades in, and goes the moment the stick moves (or she rests). On the HUD's
// layer, like the boss's health bar, over everything on the road.
public class RestPrompt : MonoBehaviour
{
    const int Sorting = 2100;
    const float FadeIn = 0.25f, FadeOut = 0.08f;
    const float GapPixels = 4f;   // over the top of her head

    /// REST is up (C sits her down).
    public static bool Showing;

    PlayerScriptARIANAClips _player;
    SpriteRenderer _body;
    TextMeshPro _label, _key;
    SpriteRenderer _arrow;
    float _alpha, _headY;
    bool _placed;

    public static RestPrompt Attach(PlayerScriptARIANAClips player, TMP_FontAsset font)
    {
        if (!player || !font) return null;
        var go = new GameObject("Rest Prompt");
        go.layer = LayerMask.NameToLayer(SpatialEmulator.Demo.PitDemoGame.HudLayer);
        var prompt = go.AddComponent<RestPrompt>();
        prompt._player = player;
        var ariana = player.transform.Find("ARIANA");
        prompt._body = ariana ? ariana.GetComponent<SpriteRenderer>() : player.GetComponentInChildren<SpriteRenderer>();
        // (The pixel font's pixels are 0.00625 here; the arrow's 7 x 5 too.)
        // REST, and beside it the key to press - C and the dialogue's bobbing
        // down arrow, in its PRESS C cyan - one line, centred over her head.
        prompt._label = prompt.Text("REST", font, new Vector3(-7f * FontPixel, 0f, 0f), new Color32(232, 224, 255, 255), TextAlignmentOptions.Bottom);
        prompt._key = prompt.Text("C", font, new Vector3(5f * FontPixel, 0f, 0f), Cyan, TextAlignmentOptions.Bottom);
        var arrow = new GameObject("Arrow");
        arrow.layer = go.layer;
        arrow.transform.SetParent(go.transform, false);
        prompt._arrow = arrow.AddComponent<SpriteRenderer>();
        prompt._arrow.sprite = Resources.Load<Sprite>("Dialogue/DialogPointer");
        if (!s_material) s_material = new Material(Shader.Find("Sprites/Default")) { name = "Rest Prompt" };
        prompt._arrow.sharedMaterial = s_material;
        arrow.transform.localScale = Vector3.one * 0.625f;   // (its 7 x 5 pixels at the font's)
        prompt._arrow.color = Cyan;
        prompt._arrow.sortingLayerID = SortingLayer.NameToID("Default");
        prompt._arrow.sortingOrder = Sorting;
        return prompt;
    }

    static readonly Color Cyan = new Color32(0, 220, 255, 255);   // (DemoDialogue's PRESS C)
    const float Size = 2f;   // picture pixels to each of the font's (whole, so it stays crisp)
    const float FontPixel = 0.00625f;   // one of the pixel font's pixels at size 0.5, unscaled (8 to its 0.05 line)
    static Material s_material;

    TextMeshPro Text(string text, TMP_FontAsset font, Vector3 at, Color color, TextAlignmentOptions align)
    {
        var child = new GameObject(text);
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        var label = child.AddComponent<TextMeshPro>();
        label.font = font;
        label.text = text;
        label.fontSize = 0.5f;   // the pixel font's 8 px at one art pixel each (as the boss's name)
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.alignment = align;
        label.rectTransform.sizeDelta = new Vector2(0.6f, 0.1f);
        label.rectTransform.pivot = new Vector2(0.5f, 0f);
        label.rectTransform.localPosition = at;
        label.color = new Color(color.r, color.g, color.b, 0f);
        label.sortingLayerID = SortingLayer.NameToID("Default");
        label.sortingOrder = Sorting;
        return label;
    }

    void LateUpdate()
    {
        if (!_player || !_body) { Destroy(gameObject); return; }
        // (Not while the witch talks, or the spell wheel is open: C is theirs then.)
        bool show = _player.CanRest && !SpatialEmulator.Demo.DemoDialogue.Showing && !SpatialEmulator.Demo.PitDemoGame.ControlsLocked
            && !(SpatialEmulator.Demo.SpellWheel.Instance && SpatialEmulator.Demo.SpellWheel.Instance.IsOpen);
        Showing = show;
        // Where her head is as she stands (taken as it comes up, so it
        // doesn't follow her idle's breathing).
        if (show && !_placed) { _headY = DrawnTop(_body) - _body.transform.position.y; _placed = true; }
        if (!show && _alpha <= 0f) _placed = false;
        _alpha = Mathf.MoveTowards(_alpha, show ? 1f : 0f, Time.deltaTime / (show ? FadeIn : FadeOut));
        if (!show && _alpha > 0f && _player.IsResting) _alpha = 0f;   // (sat down: gone at once)
        // A slow pulse, so it reads as something to press; the arrow bobs (whole pixels).
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 4f);
        float a = _alpha * pulse;
        foreach (var label in new[] { _label, _key })
        {
            var c = label.color;
            c.a = a;
            label.color = c;
            label.enabled = _alpha > 0f;
        }
        var ac = _arrow.color;
        ac.a = a;
        _arrow.color = ac;
        _arrow.enabled = _alpha > 0f;
        _arrow.transform.localPosition = new Vector3(11f * FontPixel + FontPixel * 0.5f, 0.025f - FontPixel * 0.5f + (Mathf.Repeat(Time.time * 2f, 1f) < 0.5f ? FontPixel : 0f), 0f);
        // Each of the font's pixels Size of the picture's, on its pixel grid
        // (so its one-pixel strokes - C's - show, crisp).
        var cam = SpatialEmulator.Demo.PitDemoGame.HudCamera;
        float px = cam && cam.targetTexture ? cam.orthographicSize * 2f / cam.targetTexture.height : 0f;
        if (px > 0f) transform.localScale = Vector3.one * (px * Size / FontPixel);
        var p = _body.transform.position;
        var at = new Vector3(p.x, p.y + _headY + GapPixels * SpikeTrap.PixelSize, p.z - 0.01f);
        if (px > 0f)
        {
            var origin = cam.transform.position;
            at.x = origin.x + Mathf.Round((at.x - origin.x) / px) * px;
            at.y = origin.y + Mathf.Round((at.y - origin.y) / px) * px;
        }
        transform.position = at;
    }

    void OnDestroy() => Showing = false;

    // The top of a sprite's drawn pixels (its frame has clear canvas above).
    static float DrawnTop(SpriteRenderer r)
    {
        if (!r.sprite) return r.bounds.max.y;
        float top = float.MinValue;
        foreach (var v in r.sprite.vertices)
            top = Mathf.Max(top, r.transform.TransformPoint(new Vector3(v.x, r.flipY ? -v.y : v.y, 0f)).y);
        return top;
    }
}
