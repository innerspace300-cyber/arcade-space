using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ARcade: ENDLESS KNIGHT's HUD comes alive, like the health bar pack's demo:
//  - the health and mana values as text ("114 / 128", the max in the bar's
//    colour);
//  - a hit shakes the health bar and flashes its frame white;
//  - healing blinks a green outline round the frame (not at full health:
//    then only the player's outline flashes, PlayerGlow);
//  - mana coming in flashes purple over the part of the mana bar it fills;
//  - HI SCORE: the best score ever on this device (PlayerPrefs), 0000000
//    until there is one.
// The overlays copy the frame's and the fill's rectangles each frame
// (DynamicAttrBar sizes those at run time). EndlessKnightBuilder adds this to
// the HUD and wires it.
public class HudJuice : MonoBehaviour
{
    public PlayerStats stats;
    public TMP_Text healthText, manaText, hiScoreText;
    public RectTransform healthBar;
    public Image healthBorder, healthFlash, healthOutline;
    public Image manaFill, manaFlash, manaBorder, manaOutline;

    [Header("Hit")]
    public float shakeTime = 0.35f;
    [Tooltip("How far the bar shakes, in the canvas's units.")]
    public float shakeDistance = 2f;
    public float flashTime = 0.12f;
    [Header("Heal")]
    public Color healColor = new Color32(96, 232, 96, 255);
    public float healTime = 0.7f;
    public int healBlinks = 3;
    [Tooltip("One slow pulse of the outline, while she rests and heals.")]
    public float restPulseSeconds = 1.6f;
    [Header("Mana")]
    public Color manaColor = new Color32(176, 96, 255, 255);
    public float manaTime = 0.6f;

    public string healthMaxColor = "#F0344C", manaMaxColor = "#4A9BFF";

    const string HiScoreKey = "EndlessKnight.HiScore";

    int _health = int.MinValue, _mana = int.MinValue, _maxHealth, _maxMana, _hiScore = -1, _shownScore = -1;
    float _hitAt = -10f, _healAt = -10f, _manaAt = -10f, _manaFullAt = -10f, _manaFrom, _manaTo;
    Vector2 _barHome;
    bool _homed;

    void Start()
    {
        if (!stats) stats = FindAnyObjectByType<PlayerStats>();
        _hiScore = PlayerPrefs.GetInt(HiScoreKey, 0);
        ShowHiScore();
        foreach (var image in new[] { healthFlash, healthOutline, manaFlash, manaOutline }) if (image) image.enabled = false;
    }

    void OnEnable()
    {
        StatsControl.Healed += OnHealed;
        StatsControl.ManaGained += OnMana;
    }

    void OnDisable()
    {
        StatsControl.Healed -= OnHealed;
        StatsControl.ManaGained -= OnMana;
    }

    // (Called before the heal lands: a heal onto a full bar doesn't flash it.)
    void OnHealed()
    {
        if (stats && stats.health < stats.MaxHP) _healAt = Time.time;
    }

    void OnMana() { }

    void LateUpdate()
    {
        if (!stats) return;
        if (!_homed && healthBar) { _barHome = healthBar.anchoredPosition; _homed = true; }

        // Values, and what changed.
        int health = stats.health, mana = stats.mana;
        if (_health != int.MinValue)
        {
            if (health < _health)
            {
                _hitAt = Time.time;
                if (_health != int.MinValue) SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Heavy);   // hurt
            }
            if (mana > _mana)
            {
                // A run of pickups keeps one flash, from where it began.
                float max = Mathf.Max(1, stats.MaxMP);
                if (Time.time - _manaAt > manaTime) _manaFrom = _mana / max;
                _manaTo = mana / max;
                _manaAt = Time.time;
            }
        }
        if (health != _health || stats.MaxHP != _maxHealth)
        {
            _health = health; _maxHealth = stats.MaxHP;
            if (healthText) healthText.text = $"{Mathf.Max(0, health)} / <color={healthMaxColor}>{_maxHealth}</color>";
        }
        if (mana != _mana || stats.MaxMP != _maxMana)
        {
            _mana = mana; _maxMana = stats.MaxMP;
            if (manaText) manaText.text = $"{Mathf.Max(0, mana)} / <color={manaMaxColor}>{_maxMana}</color>";
        }

        Hit();
        Heal();
        ManaFlash();
        HiScore();
    }

    // Shaking, the frame flashing white.
    void Hit()
    {
        float t = Time.time - _hitAt;
        if (healthBar && _homed)
        {
            if (t < shakeTime)
            {
                float fade = 1f - t / shakeTime;
                healthBar.anchoredPosition = _barHome + new Vector2(
                    Mathf.Round(Random.Range(-1f, 1f)) * shakeDistance * fade,
                    Mathf.Round(Random.Range(-1f, 1f)) * shakeDistance * 0.5f * fade);
            }
            else healthBar.anchoredPosition = _barHome;
        }
        if (healthFlash)
        {
            bool on = t < flashTime;
            if (on) Match(healthFlash.rectTransform, healthBorder ? healthBorder.rectTransform : null, 0);
            healthFlash.enabled = on;
        }
    }

    // A green outline blinking round the frame - or, while she rests and
    // heals (PlayerScriptARIANAClips.IsResting), pulsing slowly.
    void Heal()
    {
        if (!healthOutline) return;
        float t = Time.time - _healAt;
        bool on = t < healTime && Mathf.Repeat(t / healTime * healBlinks, 1f) < 0.6f;
        var player = EndlessKnightDirector.Player;
        if (!on && player && player.IsResting && stats && stats.health < stats.MaxHP)
        {
            Match(healthOutline.rectTransform, healthBorder ? healthBorder.rectTransform : null, 1f / healthOutline.pixelsPerUnit);
            var slow = healColor;
            slow.a = 0.25f + 0.75f * (0.5f - 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f / restPulseSeconds));
            healthOutline.color = slow;
            healthOutline.enabled = true;
            return;
        }
        if (on)
        {
            // One art pixel bigger all round (the canvas's sprite scale).
            Match(healthOutline.rectTransform, healthBorder ? healthBorder.rectTransform : null, 1f / healthOutline.pixelsPerUnit);
            healthOutline.color = healColor;
        }
        healthOutline.enabled = on;
    }

    // Purple over the stretch of the bar the mana filled, pulsing out; or
    // the bar's outline blinking purple when it was full already.
    void ManaFlash()
    {
        if (manaOutline)
        {
            float f = Time.time - _manaFullAt;
            bool outline = f < healTime && Mathf.Repeat(f / healTime * healBlinks, 1f) < 0.6f;
            var color = manaColor;
            // Asleep and getting mana back: the outline pulses slowly.
            var player = EndlessKnightDirector.Player;
            if (!outline && player && player.IsSleeping && stats && stats.mana < stats.MaxMP)
            {
                outline = true;
                color.a = 0.25f + 0.75f * (0.5f - 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f / restPulseSeconds));
            }
            if (outline)
            {
                Match(manaOutline.rectTransform, manaBorder ? manaBorder.rectTransform : null, 1f / manaOutline.pixelsPerUnit);
                manaOutline.color = color;
            }
            manaOutline.enabled = outline;
        }
        if (!manaFlash || !manaFill) return;
        float t = Time.time - _manaAt;
        bool on = t < manaTime && _manaTo > _manaFrom;
        manaFlash.enabled = on;
        if (!on) return;
        var fill = manaFill.rectTransform;
        var flash = manaFlash.rectTransform;
        flash.anchorMin = fill.anchorMin; flash.anchorMax = fill.anchorMax; flash.pivot = fill.pivot;
        float width = fill.rect.width;
        flash.sizeDelta = new Vector2(width * (_manaTo - _manaFrom), fill.rect.height);
        flash.anchoredPosition = fill.anchoredPosition + new Vector2(width * _manaFrom, 0f);
        var c = manaColor;
        c.a = Mathf.Repeat(t * 8f, 1f) < 0.5f ? 1f : 0.55f;
        c.a *= 1f - Mathf.Clamp01((t - manaTime * 0.5f) / (manaTime * 0.5f));
        manaFlash.color = c;
    }

    void HiScore()
    {
        var scores = PitScoreManager.Instance;
        if (!scores) return;
        int score = scores.GetScore();
        if (score <= _hiScore) return;
        _hiScore = score;
        PlayerPrefs.SetInt(HiScoreKey, _hiScore);
        ShowHiScore();
    }

    void ShowHiScore()
    {
        if (!hiScoreText || _shownScore == _hiScore) return;
        _shownScore = _hiScore;
        hiScoreText.text = Mathf.Clamp(_hiScore, 0, 9999999).ToString("D7");
    }

    // Laid over `target` (a sibling), grown by `grow` all round.
    static void Match(RectTransform overlay, RectTransform target, float grow)
    {
        if (!target) return;
        overlay.anchorMin = target.anchorMin; overlay.anchorMax = target.anchorMax; overlay.pivot = target.pivot;
        overlay.sizeDelta = target.rect.size + Vector2.one * (grow * 2f);
        overlay.anchoredPosition = target.anchoredPosition + new Vector2((target.pivot.x * 2f - 1f) * grow, (target.pivot.y * 2f - 1f) * grow);
        overlay.localScale = target.localScale;
    }

    void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
    void OnDestroy() => PlayerPrefs.Save();
}
