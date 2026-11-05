using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ARcade: ENDLESS KNIGHT's fruit collection, a row of every fruit the chests
// hold, over the health bar - past level 1, that level's own set instead
// (LevelCollection: keys, eggs, potions, which its bosses drop). Each starts as a dark silhouette and lights up
// (with a little pop) the first time one like it is picked up (ChestGem).
// The row starts again with each run - until a run collects every one: from
// then on they all stay lit, deaths and restarts included (PlayerPrefs) -
// and completing it rains every fruit down the picture to a win jingle,
// with a score bonus (FruitRain).
// EndlessKnightBuilder builds the row and fills in the fruit.
public class FruitTracker : MonoBehaviour
{
    public Image[] slots;
    [Tooltip("The fruit, one per slot - the pickups' own sprites.")]
    public Sprite[] fruit;
    public Color missingColor = new Color(0.28f, 0.18f, 0.42f, 0.65f);
    public float popTime = 0.25f;
    public float popScale = 1.4f;
    [Header("Completing the set")]
    public AudioClip winSound;
    public int completeBonus = 5000;

    const string CompleteKey = "EndlessKnight.FruitComplete";   // (level 1's; later levels' + their number)
    /// The running level's row.
    public static FruitTracker Instance { get; private set; }

    public static event System.Action<Sprite> Collected;
    /// ARcade: this run has picked up every fruit (true: the first time ever,
    /// so it's raining fruit) - the witch comes to open the portal (LevelPortal).
    public static event System.Action<bool> AllCollected;
    /// A fruit picked up (ChestGem).
    public static void Collect(Sprite sprite) => Collected?.Invoke(sprite);

    bool[] _have;
    string _key;

    /// The first `count` of the row she hasn't got yet this run (what a boss drops next: RoadBoss).
    public List<int> Missing(int count)
    {
        var missing = new List<int>();
        for (int i = 0; i < _have.Length && missing.Count < count; i++) if (!_have[i]) missing.Add(i);
        return missing;
    }
    float[] _popAt;
    int _wide = -1;
    const float WideSize = 1.25f;
    bool _complete;

    void Awake()
    {
        Instance = this;
        // Past level 1: that level's own set (LevelCollection) in the row's first slots, the rest hidden.
        int level = SpatialEmulator.Demo.PitDemoGame.Level;
        var set = LevelCollection.For(level);
        if (set != null)
        {
            fruit = set.icons;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i]) continue;
                bool used = i < fruit.Length;
                slots[i].gameObject.SetActive(used);
                if (used) { slots[i].sprite = fruit[i]; slots[i].preserveAspect = true; }
                // (a wide item among tall ones - level 2's jewelled last key - drawn bigger, its top clear)
                if (used && fruit[i] && fruit[i].rect.width > fruit[i].rect.height * 0.6f && set.icons.Length > 1 && fruit[0].rect.width < fruit[0].rect.height * 0.6f) _wide = i;
            }
            slots = System.Array.FindAll(slots, s => s && s.gameObject.activeSelf);
            _key = CompleteKey + level;
        }
        else _key = CompleteKey;
        _have = new bool[slots.Length];
        _popAt = new float[slots.Length];
        for (int i = 0; i < _popAt.Length; i++) _popAt[i] = -10f;
        _complete = PlayerPrefs.GetInt(_key, 0) == 1;
        Refresh();
    }

    void OnEnable() => Collected += OnCollected;
    void OnDisable() => Collected -= OnCollected;

    void OnCollected(Sprite sprite)
    {
        int i = System.Array.IndexOf(fruit, sprite);
        if (i < 0 || i >= slots.Length || _have[i]) return;
        _have[i] = true;
        if (!_complete) _popAt[i] = Time.time;
        bool all = System.Array.TrueForAll(_have, h => h);
        if (!_complete && all)
        {
            // Every fruit: they stay lit for good, and all pop together.
            _complete = true;
            PlayerPrefs.SetInt(_key, 1);
            PlayerPrefs.Save();
            for (int k = 0; k < _popAt.Length; k++) _popAt[k] = Time.time;
            FruitRain.Play(fruit, winSound, completeBonus, SpatialEmulator.Demo.PitDemoGame.Level == 3 ? 0.6f : 1f);   // it rains fruit (level 3's big eggs smaller)
            SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Success);
            Refresh();
            AllCollected?.Invoke(true);
            return;
        }
        Refresh();
        if (all) AllCollected?.Invoke(false);
    }

    void Refresh()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i]) slots[i].color = _complete || _have[i] ? Color.white : missingColor;
    }

    void Update()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i]) continue;
            float t = (Time.time - _popAt[i]) / popTime;
            float s = t >= 0f && t < 1f ? Mathf.Lerp(popScale, 1f, t) : 1f;
            slots[i].rectTransform.localScale = Vector3.one * (s * (i == _wide ? WideSize : 1f));
        }
    }
}
