using TMPro;
using UnityEngine;

// ARcade: a Slime Boss's health bar, over its head - Pixel UI's boss bar
// (BossBarA: its angled black back, the fill - recoloured to the boss's
// body's purple, and turned with a recoloured boss's own colours - a white fill that
// trails it down after each hit, and the grey frame over them; 9-sliced to
// the same width for every boss, Resources/BossBar) with its name (SLIMER) under its right
// end, both clear over its head. It holds one height over the boss's feet (not bobbing with its frames). It follows the boss on the road, at the characters' pixel size, in
// the characters' depth layer; it fades away when the boss dies.
// Added to each boss by EndlessKnightDirector.
[DefaultExecutionOrder(300)]   // after the boss has moved
public class BossHealthBar : MonoBehaviour
{
    [Tooltip("Its width, in its art's pixels (the same for every boss, however big).")]
    public float barWidth = 44f;
    [Tooltip("Gap over the top of its head, in the characters' pixels (below 0: its frames' outlines run a little over its head; this sits it a few pixels over the drawn top).")]
    public float gapPixels = 0f;
    [Tooltip("More gap for the smallest bosses (the first ones), easing to none by the mid-sized ones.")]
    public float smallGapPixels = 4f;
    const float SmallUntil = 2.9f, SmallFade = 0.5f;   // body scales: the first boss is 2.36, a mid-sized one 3.06
    [Tooltip("Seconds the white trail waits after a hit before following the red down, and how fast it does (bar widths a second).")]
    public float trailDelay = 0.35f, trailSpeed = 0.6f;
    public float fadeTime = 0.6f;

    // The art (pixels): the back and frame 8 high with 7-px slanted ends,
    // the fills 4 high, 6 px in from each end and 1 down from the top.
    const float BarHeight = 8f, FillHeight = 4f, FillInset = 6f, FillTop = 1f, FillMin = 7f;
    const int Sorting = 2000;   // over the boss and everything on the road

    static Material s_material;
    static float ArtPixel => SpikeTrap.PixelSize * 2f;
    EnemyStateMachineSlimeBoss _boss;
    SpriteRenderer _back, _trail, _fill, _frame;
    TextMeshPro _label;
    const float TorsoOffset = 0.136f;
    bool _headFromIdle;
    float _width, _headOverFeet, _trailFrac = 1f, _hitAt = -10f, _shownFrac = 1f, _deadAt = -1f;

    public static BossHealthBar Attach(EnemyStateMachineSlimeBoss boss, TMP_FontAsset font)
    {
        var go = new GameObject("Boss Health Bar");
        // On the front layer (the HUD's), over everything.
        go.layer = LayerMask.NameToLayer(SpatialEmulator.Demo.PitDemoGame.HudLayer);
        var bar = go.AddComponent<BossHealthBar>();
        bar._boss = boss;
        bar.Build(font);
        return bar;
    }

    SpriteRenderer Piece(string name, string art, int order)
    {
        var go = new GameObject(name);
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        // (A new renderer's default sprite material is the 2D renderer's
        // lit one, which this pipeline doesn't draw.)
        if (!s_material) s_material = new Material(Shader.Find("Sprites/Default")) { name = "Boss Bar" };
        sr.sharedMaterial = s_material;
        sr.sprite = Resources.Load<Sprite>("BossBar/" + art);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.sortingLayerName = "Default";
        sr.sortingOrder = Sorting + order;
        return sr;
    }

    void Build(TMP_FontAsset font)
    {
        // Two of the characters' pixels to the art's one, like the rest of
        // the game's pixel art (the art is 100 px a unit).
        transform.localScale = Vector3.one * (ArtPixel / 0.01f);
        _back = Piece("Back", "BossBarBackground", 0);
        _trail = Piece("Trail", "BossBarFollowFill", 1);
        _fill = Piece("Fill", "BossBarFill", 2);
        // A recoloured boss's body is turned: so is its fill (in its body's colour).
        if (_boss.BodyRecolor) _fill.sharedMaterial = _boss.BodyRecolor;
        _frame = Piece("Frame", "BossBarForeground", 3);

        // As wide as the boss's body (its collider) and a bit, in art pixels;
        // its head's height over its feet, from its sprite as it comes in.
        var body = _boss.BodyCollider ? _boss.BodyCollider.bounds : _boss.Body.bounds;
        _width = barWidth;
        _headOverFeet = DrawnTop(_boss.Body) - body.min.y;

        _back.size = _frame.size = new Vector2(_width, BarHeight) * 0.01f;

        if (font)
        {
            var label = new GameObject("Name");
            label.layer = gameObject.layer;
            label.transform.SetParent(transform, false);
            _label = label.AddComponent<TextMeshPro>();
            _label.font = font;
            _label.text = "SLIMER";
            _label.fontSize = 0.5f;   // the pixel font's 8 px to the em at one art pixel each (measured)
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.color = new Color32(232, 224, 255, 255);
            _label.sortingLayerID = SortingLayer.NameToID("Default");
            _label.sortingOrder = Sorting + 4;
            // Under the bar's right end.
            _label.alignment = TextAlignmentOptions.TopRight;
            _label.rectTransform.sizeDelta = new Vector2(0.6f, 0.1f);
            _label.rectTransform.pivot = new Vector2(1f, 1f);
            _label.rectTransform.localPosition = new Vector3((_width * 0.5f - 8f) * 0.01f, -(BarHeight * 0.5f + 1f) * 0.01f, 0f);
        }
        Place();
        Show(1f);
    }

    // The top of a sprite's drawn pixels (its frame has clear canvas above).
    static float DrawnTop(SpriteRenderer r)
    {
        if (!r.sprite) return r.bounds.max.y;
        float top = float.MinValue;
        foreach (var v in r.sprite.vertices)
            top = Mathf.Max(top, r.transform.TransformPoint(new Vector3(v.x, r.flipY ? -v.y : v.y, 0f)).y);
        return top;
    }

    void Place()
    {
        var body = _boss.BodyCollider ? _boss.BodyCollider.bounds : _boss.Body.bounds;
        // Centred over its torso, which sits off its collider's middle - the
        // same in every boss (one art), so it's a share of its scale,
        // mirrored as it turns (measured: where most of its pixels are).
        float torso = -TorsoOffset * _boss.Body.transform.lossyScale.x;
        // A set height over its feet - just over its head (as it stands, not
        // following its frames as they raise its club and slump), its name
        // under it over the top of its head; never off the top of the picture.
        // (The smallest bosses' a few pixels higher, easing to none by the mid-sized.)
        float small = Mathf.Clamp01((SmallUntil - _boss.Body.transform.lossyScale.y) / SmallFade);
        float y = body.min.y + _headOverFeet + (gapPixels + smallGapPixels * small) * SpikeTrap.PixelSize + BarHeight * 0.5f * ArtPixel;
        var cam = SpatialEmulator.Demo.PitDemoGame.HudCamera;
        // (The biggest bosses' heads come near the top: there it stays as
        // high as it goes, its name under it over their heads.)
        if (cam) y = Mathf.Min(y, cam.transform.position.y + cam.orthographicSize - (BarHeight * 0.5f + 2f) * ArtPixel);
        transform.position = new Vector3(body.center.x + torso, y, _boss.Body.transform.position.z - 0.01f);
    }

    // The red down to `frac` of the bar's inside, its right end following it in.
    void Show(float frac)
    {
        float inside = _width - 2f * FillInset;
        SetFill(_fill, frac, inside);
        SetFill(_trail, _trailFrac, inside);
    }

    void SetFill(SpriteRenderer fill, float frac, float inside)
    {
        fill.enabled = frac > 0.001f;
        float w = Mathf.Max(FillMin, Mathf.Round(inside * Mathf.Clamp01(frac)));
        fill.size = new Vector2(w, FillHeight) * 0.01f;
        float left = -_width * 0.5f + FillInset;
        fill.transform.localPosition = new Vector3((left + w * 0.5f) * 0.01f, (BarHeight * 0.5f - FillTop - FillHeight * 0.5f) * 0.01f, 0f);
    }

    void LateUpdate()
    {
        if (!_boss) { Destroy(gameObject); return; }
        // Its head: the top of its standing frames (the tallest seen), not
        // its attacks' raised club.
        var frame = _boss.Body.sprite;
        if (frame && frame.name.StartsWith("idle"))
        {
            var feet = (_boss.BodyCollider ? _boss.BodyCollider.bounds : _boss.Body.bounds).min.y;
            float head = DrawnTop(_boss.Body) - feet;
            if (!_headFromIdle || head > _headOverFeet) _headOverFeet = head;
            _headFromIdle = true;
        }
        Place();
        float frac = _boss.MaxHealth > 0 ? Mathf.Clamp01((float)_boss.enemyHealth / _boss.MaxHealth) : 0f;
        if (frac < _shownFrac - 0.0001f) _hitAt = Time.time;
        _shownFrac = frac;
        // The white trail waits, then runs down to the red.
        if (_trailFrac < frac) _trailFrac = frac;
        else if (Time.time - _hitAt > trailDelay) _trailFrac = Mathf.MoveTowards(_trailFrac, frac, trailSpeed * Time.deltaTime);
        Show(frac);

        // Gone: it fades away.
        if (_boss.IsDead && _deadAt < 0f) _deadAt = Time.time;
        if (_deadAt >= 0f)
        {
            float a = 1f - (Time.time - _deadAt) / fadeTime;
            if (a <= 0f) { Destroy(gameObject); return; }
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>()) sr.color = new Color(1f, 1f, 1f, a);
            if (_label) _label.alpha = a;
        }
    }
}
