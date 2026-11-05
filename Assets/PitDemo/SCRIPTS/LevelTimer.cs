using TMPro;
using UnityEngine;

// ARcade: ENDLESS KNIGHT's level clock - a little panel in the frame's top left, in the HUD's own style (its
// checker inside its band border, its SCORE titles' pixel font and colour, the score's digits): TIME, running
// from the first move of each run of a level (stopped by a death - the run starts again, and it with it, and
// while the game's paused or a dialogue's up), and BEST under it, her best for this level. The level's cleared
// when the run's row is full (FruitTracker.AllCollected: the witch's portal): the clock stops; a new best
// flashes gold in the BEST row (kept in PlayerPrefs) - and the time goes to the level's Game Center time board
// (hundredths of a second, lowest best). PitDemoGame adds it to each run.
public class LevelTimer : MonoBehaviour
{
    /// The level's Game Center board (App Store Connect: Elapsed Time to the hundredth of a second, low to high).
    public static string Board(int level) => SpatialEmulator.Games.GameCenter.LevelTimeBoard(level);

    const string BestKey = "EndlessKnight.BestTime";   // + the level: hundredths of a second
    const float ResultSeconds = 8f;
    const float EdgeMargin = 0.02f;   // (share of the HUD's view in from its left edge; the same distance down from the top)
    static readonly Color NewBestColor = new Color(1f, 0.82f, 0.18f);
    // The panel, in the HUD canvas's units: its size, its rows' middles down from its top, the text sizes.
    const float PanelWidth = 84f, PanelHeight = 34f, Row1 = 10f, Row2 = 24f, LabelSize = 6.03f, DigitSize = 8f;
    const float LabelLine = 6.79f;   // (the HUD's health numbers' line height, at LabelSize)
    const float Inset = 1.5f;   // (the border's width: the checker inside it)

    TMP_Text _time1, _best1, _timeLabel, _bestLabel, _title;
    RectTransform _rect;
    UnityEngine.UI.Image _checker, _border, _bandChecker, _bandBorder;
    Camera _camera;
    Transform _panel;
    Color _digitColor;
    float _time, _resultUntil;
    bool _done, _started, _newBest;
    int _best;

    void Start()
    {
        var manager = PitScoreManager.Instance ? PitScoreManager.Instance : GetComponentInChildren<PitScoreManager>(true);
        var score = manager ? manager.scoreText : null;
        var canvas = score ? score.GetComponentInParent<Canvas>(true) : null;
        if (!score || !canvas) return;
        // The HUD's pieces it's styled on (HudStyle recolours them with the cabinet's style; this follows).
        var hudCanvas = canvas.transform;
        _bandChecker = Find<UnityEngine.UI.Image>(hudCanvas, "Checker");
        _bandBorder = Find<UnityEngine.UI.Image>(hudCanvas, "Band Border");
        _title = Find<TMP_Text>(hudCanvas, "Score Title");

        var root = new GameObject("LEVEL TIME PANEL", typeof(RectTransform));
        root.layer = score.gameObject.layer;
        _rect = (RectTransform)root.transform;
        _rect.SetParent(hudCanvas, false);
        _rect.pivot = new Vector2(0f, 1f);
        _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        _rect.SetAsLastSibling();
        var back = Image(_rect, "Back", Inset);   // (the band's dark under its checker)
        back.color = new Color(0.07f, 0.03f, 0.16f, 0.9f);
        _checker = Image(_rect, "Checker", Inset);
        _border = Image(_rect, "Border", 0f);

        _timeLabel = Label(_rect, "TIME", Row1);
        _bestLabel = Label(_rect, "BEST", Row2);
        _time1 = Digits(score, _rect, "TIME VALUE", Row1);
        _best1 = Digits(score, _rect, "BEST VALUE", Row2);
        _digitColor = _time1.color;
        // (Wayne's: each label centred between the border and the clock's digits - "00:00:00" wide - its left edge
        // on a whole unit, left-aligned: the pixel font centred on a half unit came out garbled)
        float digitsLeft = PanelWidth - 5f - _time1.GetPreferredValues("00:00:00").x;
        _labelMiddle = (Inset + digitsLeft) * 0.5f;   // (placed there each frame: Place)
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;

        _best = PlayerPrefs.GetInt(BestKey + SpatialEmulator.Demo.PitDemoGame.Level, 0);
        _camera = canvas.worldCamera;
        _panel = score.transform.parent;
        Restyle();
        Place();
    }

    static T Find<T>(Transform under, string name) where T : Component
    {
        foreach (var c in under.GetComponentsInChildren<T>(true)) if (c.name == name) return c;
        return null;
    }

    UnityEngine.UI.Image Image(RectTransform parent, string name, float inset)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.raycastTarget = false;
        return image;
    }

    // A row's title, the HUD's SCORE title's pixel font and colour, at the panel's left (centred in its room: Start).
    TMP_Text Label(RectTransform parent, string text, float row)
    {
        var go = new GameObject(text + " LABEL", typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        // (Laid out as the HUD's health numbers are - a line's height, bottom-left - its bottom-left corner the
        // point put on a whole pixel: Place. The HUD is drawn small, a pixel of the font about one of the picture's,
        // and between pixels its one-pixel strokes went missing: TIME read as "-M:".)
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = Vector2.zero;
        r.sizeDelta = new Vector2(24f, LabelLine);
        r.anchoredPosition = new Vector2(5f, -row - LabelLine * 0.5f);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (_title) { t.font = _title.font; t.fontSharedMaterial = _title.fontSharedMaterial; }
        t.fontSize = LabelSize;
        t.alignment = TextAlignmentOptions.BottomLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        t.text = text;
        return t;
    }

    // A row's clock, the score's digits (its font, colour, a glow), at the panel's right - both rows one size.
    TMP_Text Digits(TMP_Text score, RectTransform parent, string name, float row)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f);
        r.sizeDelta = new Vector2(56f, 12f);
        r.anchoredPosition = new Vector2(-5f, -row + 6f);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = score.font;
        t.color = score.color;
        t.fontSize = DigitSize;
        t.alignment = TextAlignmentOptions.MidlineRight;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        var glow = t.fontMaterial;   // (its own copy)
        glow.EnableKeyword("GLOW_ON");
        glow.SetColor("_GlowColor", new Color(score.color.r, score.color.g, score.color.b, 0.6f));
        glow.SetFloat("_GlowOuter", 0.4f);
        glow.SetFloat("_GlowPower", 0.7f);
        return t;
    }

    // The band's checker and border, the SCORE title's colour: as HudStyle has them now (the cabinet's style) - but
    // its border always black (Wayne's: blending in, whatever the style).
    void Restyle()
    {
        if (_bandChecker && _checker) { _checker.sprite = _bandChecker.sprite; _checker.type = _bandChecker.type; _checker.color = _bandChecker.color; _checker.pixelsPerUnitMultiplier = _bandChecker.pixelsPerUnitMultiplier; }
        else if (_checker) _checker.color = new Color(0f, 0f, 0f, 0.62f);
        if (_bandBorder && _border) { _border.sprite = _bandBorder.sprite; _border.type = _bandBorder.type; _border.color = Color.black; _border.pixelsPerUnitMultiplier = _bandBorder.pixelsPerUnitMultiplier; }
        else if (_border) _border.enabled = false;
        if (_title)
        {
            if (_timeLabel) _timeLabel.color = _title.color;
            if (_bestLabel) _bestLabel.color = _title.color;
        }
    }

    void OnEnable() => FruitTracker.AllCollected += Cleared;
    void OnDisable() => FruitTracker.AllCollected -= Cleared;

    // Top left of what the HUD's camera sees (its canvas's own size isn't to be trusted before it's drawn).
    void Place()
    {
        if (!_camera || !_rect) return;
        var canvas = _rect.GetComponentInParent<Canvas>();
        float distance = canvas ? canvas.planeDistance : 1f;
        _rect.position = _camera.ViewportToWorldPoint(new Vector3(EdgeMargin, 1f - EdgeMargin * _camera.aspect, distance));
        // On whole pixels, it and its titles: the pixel font between pixels came out garbled (the HUD's own
        // titles sit on them).
        Snap(_rect);
        // The titles' font a whole number of the picture's pixels to its pixel (FontPixel of its size) - just under
        // one, its one-pixel strokes went missing - each centred in its room, its corner on a whole pixel.
        float perUnit = Mathf.Abs(_camera.WorldToScreenPoint(_rect.TransformPoint(Vector3.right)).x - _camera.WorldToScreenPoint(_rect.position).x);
        foreach (var label in new[] { _timeLabel, _bestLabel })
        {
            if (!label) continue;
            if (perUnit > 0f) label.fontSize = Mathf.Max(1f, Mathf.Round(LabelSize * FontPixel * perUnit)) / (FontPixel * perUnit);
            var r = label.rectTransform;
            r.anchoredPosition = new Vector2(_labelMiddle - label.GetPreferredValues(label.text).x * 0.5f, r.anchoredPosition.y);
            Snap(r);
        }
    }
    float _labelMiddle = 15f;
    const float FontPixel = 4f / 32f;   // (the HUD's pixel font: 4 of its 32 to a pixel)

    void Snap(Transform t)
    {
        var at = _camera.WorldToScreenPoint(t.position);
        t.position = _camera.ScreenToWorldPoint(new Vector3(Mathf.Round(at.x), Mathf.Round(at.y), at.z));
    }

    void Update()
    {
        if (!_time1) return;
        Place();
        Restyle();
        // Shown with the score's panel, not over a dialogue (the box goes across the top).
        bool show = (!_panel || _panel.gameObject.activeInHierarchy) && !SpatialEmulator.Demo.DemoDialogue.Showing;
        if (_rect.gameObject.activeSelf != show) _rect.gameObject.SetActive(show);
        // Not started till she's moved (the stick or the pad), stopped for good if she dies (the run starts over).
        var player = EndlessKnightDirector.Player;
        if (player && player.IsDead) _done = true;
        if (!_started && PitInput.Move.sqrMagnitude > 0f) _started = true;
        if (_started && !_done && !SpatialEmulator.Demo.PitDemoGame.IsPaused && !SpatialEmulator.Demo.DemoDialogue.Showing)
            _time += Time.deltaTime;
        _time1.text = Format(_time);
        // Her saved best goes to the level's board once Game Center's signed in (a best set before the board
        // existed, or offline, never got there; Game Center keeps the lowest, so sending it again is harmless).
        int level = SpatialEmulator.Demo.PitDemoGame.Level;
        if (_best > 0 && level >= 1 && level < s_sentBest.Length && !s_sentBest[level] && SpatialEmulator.Games.GameCenter.SignedIn)
        {
            s_sentBest[level] = true;
            SpatialEmulator.Games.GameCenter.Submit(_best, Board(level));
        }
        _best1.text = _best > 0 ? Format(_best / 100f) : "--:--:--";
        // A new best: the BEST row flashing gold a while.
        bool flash = _newBest && Time.time < _resultUntil;
        _best1.color = flash && Mathf.Repeat(Time.time * 3f, 1f) < 0.65f ? NewBestColor : _digitColor;
        if (_bestLabel && _title) _bestLabel.color = flash ? NewBestColor : _title.color;
    }

    static readonly bool[] s_sentBest = new bool[8];

    void Cleared(bool firstTime)
    {
        if (_done || !_started) return;
        _done = true;
        int level = SpatialEmulator.Demo.PitDemoGame.Level;
        long hundredths = (long)Mathf.Round(_time * 100f);
        _newBest = _best <= 0 || hundredths < _best;
        if (_newBest)
        {
            _best = (int)hundredths;
            PlayerPrefs.SetInt(BestKey + level, _best);
            PlayerPrefs.Save();
        }
        _resultUntil = Time.time + ResultSeconds;
        SpatialEmulator.Games.GameCenter.Submit(hundredths, Board(level));
    }

    /// mm:ss:hh - six digits, as an arcade clock.
    public static string Format(float seconds) => SpatialEmulator.Games.GameCenter.FormatTime((long)Mathf.Round(seconds * 100f));
}
