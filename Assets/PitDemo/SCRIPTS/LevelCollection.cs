using System.Collections.Generic;
using UnityEngine;

// ARcade: what each ENDLESS KNIGHT level has her collect to open its portal
// (FruitTracker's row, made over for it): level 1 the fruit (the chests'),
// level 2 fifteen gold keys (from the necromancers), level 3 fourteen eggs
// (from the giant toads), level 4 fifteen potions (from the elder dragons) -
// as many as level 1's fruit; each boss drops the next one she hasn't got
// yet this run (RoadBoss). And past level 1, candy in place of the fruit and
// gems (chests, orange slices, bosses' showers: see the candy below) - the
// same health and mana, not part of any row. The art is in
// Resources/Collect (unity-helpers/collect_art.py: the 32x32 key pack,
// Frostwindz's Fantasy Pixel Art Eggs, Pixel Potions, acursedpixel's candy, the food icons).
public static class LevelCollection
{
    public class Set
    {
        public string name;
        /// The row's stills (each item's identity: FruitTracker.Collect).
        public Sprite[] icons;
        /// Each item's pickup frames (one: a still), and their speed.
        public Sprite[][] frames;
        public float fps;
        /// Each pickup's size (ChestGem.scale: the fruit's is 0.8 at 16 px).
        public float scale;
        /// How many a boss drops.
        public int perBoss;
    }

    static readonly Dictionary<int, Set> s_sets = new Dictionary<int, Set>();

    /// One item a boss (a row of 15: fifteen bosses to fill it).
    const int PerBoss = 1;

    /// Level `level`'s set (null: level 1's fruit, as built).
    public static Set For(int level)
    {
        if (level <= 1) return null;
        if (s_sets.TryGetValue(level, out var set) && set != null && set.icons.Length > 0 && set.icons[0]) return set;
        set = level switch
        {
            2 => Stills("KEYS", "Keys", 0.8f * 0.85f),
            3 => Stills("EGGS", "Eggs", 0.8f * 1.25f * 3f * 0.75f * 14f / 38f, EggOrder),   // (big: the toad's prize - each egg 38 px tall in its 52, as big as the old 14 px ones were)
            _ => Potions(),
        };
        s_sets[level] = set;
        return set;
    }

    /// The eggs in the order the toads drop them (Wayne's: fegg_N Fantasy Pixel Art Eggs cut to the old egg's shape,
    /// oegg_N the old Fantasy_Eggs - egg_shape.py) - fourteen of them.
    static readonly string[] EggOrder = { "oegg_1", "oegg_4", "fegg_11", "fegg_7", "fegg_6", "fegg_10", "fegg_1", "fegg_13", "oegg_11", "fegg_17", "fegg_4", "fegg_15", "oegg_41", "oegg_44" };

    // Every sprite in Resources/Collect/`folder`, in `order` (any not in it after, in name order).
    static Set Stills(string name, string folder, float scale, string[] order = null)
    {
        var icons = Resources.LoadAll<Sprite>("Collect/" + folder);
        int Rank(Sprite sp) { int i = order == null ? -1 : System.Array.IndexOf(order, sp.name); return i < 0 ? int.MaxValue : i; }
        System.Array.Sort(icons, (a, b) => Rank(a) != Rank(b) ? Rank(a).CompareTo(Rank(b)) : string.CompareOrdinal(a.name, b.name));
        var frames = System.Array.ConvertAll(icons, icon => new[] { icon });
        return new Set { name = name, icons = icons, frames = frames, fps = 0f, scale = scale, perBoss = PerBoss };
    }

    // The potions: each one's still (its first frame, NAME-icon) for the row, and its bubbling loop (NAME-strip:
    // its frames left to right, each as wide as the still).
    static Set Potions()
    {
        var all = Resources.LoadAll<Sprite>("Collect/Potions");
        var icons = System.Array.FindAll(all, sp => sp.name.EndsWith("-icon"));
        System.Array.Sort(icons, (a, b) => string.CompareOrdinal(a.name, b.name));
        var frames = new Sprite[icons.Length][];
        for (int i = 0; i < icons.Length; i++)
        {
            string file = icons[i].name.Substring(0, icons[i].name.Length - "-icon".Length);
            var strip = Resources.Load<Texture2D>("Collect/Potions/" + file + "-strip");
            int w = (int)icons[i].rect.width;
            if (!strip || w <= 0) { frames[i] = new[] { icons[i] }; continue; }
            int count = strip.width / w;
            frames[i] = new Sprite[count];
            for (int f = 0; f < count; f++)
                frames[i][f] = Sprite.Create(strip, new Rect(f * w, 0, w, strip.height), new Vector2(0.5f, 0.5f), 100f);
        }
        // (The pack's loops: 70 ms a frame.)
        return new Set { name = "POTIONS", icons = icons, frames = frames, fps = 1000f / 70f, scale = 0.8f * 0.55f, perBoss = PerBoss };
    }

    // ---- The candy past level 1 (Resources/Collect/Candy; collect_art.py) ----
    // Food chests (where the fruit was: health): the five gummy bears, one a chest, then the mixed candy.
    // Magic chests (where the gems were: mana): five neon jelly beans, one a chest, then the mixed candy.
    // Mixed: the white jawbreaker, the peppermints, the wrapped mints - round in turn, never two chests alike.
    // The orange slices' spots: candy corn (level 2: candy corn in its slice's colour, Road2: CornFor;
    // level 3: the flowers - black rose, red rose, hibiscus, peony: Flowers3 and Road3; level 4: Wayne's mushroom in its slice's
    // shade, Road4 and MushroomFor) - a group of five each.
    static Sprite[] s_food, s_magic, s_mixed, s_flowers;
    static Sprite s_corn, s_peony;
    static int s_foodChests, s_magicChests;

    static bool Candy()
    {
        if (SpatialEmulator.Demo.PitDemoGame.Level <= 1) return false;
        if (s_mixed == null || s_mixed.Length == 0 || !s_mixed[0])
        {
            s_food = Sorted("Collect/Candy/Food");
            s_magic = Sorted("Collect/Candy/Magic");
            s_mixed = Sorted("Collect/Candy/Mixed");
            var flowers = new List<Sprite>(Sorted("Collect/Candy/Flowers3"));
            foreach (var f in Sorted("Collect/Candy/Road3")) if (!f.name.StartsWith("peony_")) flowers.Add(f);   // (the peony's shades: PeonyFor)
            s_flowers = flowers.ToArray();
            s_peony = Resources.Load<Sprite>("Collect/Candy/Road3/peony");
            s_corn = Resources.Load<Sprite>("Collect/Candy/Road/candy_corn");
        }
        return s_mixed.Length > 0;
    }

    static Sprite[] Sorted(string path)
    {
        var all = Resources.LoadAll<Sprite>(path);
        System.Array.Sort(all, (a, b) => string.CompareOrdinal(a.name, b.name));
        return all;
    }

    /// A new run: the chests start from their first gummy bear and jelly bean again (EndlessKnightDirector).
    public static void ResetRun() { s_foodChests = s_magicChests = 0; s_group = -1; s_claims.Clear(); }

    /// The next food chest's candy (past level 1; level 1: null).
    public static Sprite NextFoodCandy()
    {
        if (!Candy()) return null;
        int n = s_foodChests++;
        return n < s_food.Length ? s_food[n] : s_mixed[(n - s_food.Length) % s_mixed.Length];
    }

    /// The next magic chest's candy (past level 1; level 1: null) - its mixed turns half the round from the food
    /// chests', so the two kinds aren't alike either.
    public static Sprite NextMagicCandy()
    {
        if (!Candy()) return null;
        int n = s_magicChests++;
        return n < s_magic.Length ? s_magic[n] : s_mixed[(n - s_magic.Length + s_mixed.Length / 2) % s_mixed.Length];
    }

    /// A boss's shower of magic candy, one piece (past level 1; level 1: null).
    public static Sprite MagicCandy()
    {
        if (!Candy()) return null;
        int pick = Random.Range(0, s_magic.Length + s_mixed.Length);
        return pick < s_magic.Length ? s_magic[pick] : s_mixed[pick - s_magic.Length];
    }

    /// A candy pickup's size (ChestGem.scale): a fruit's (0.8 at 16 px), the gummy bears, jelly beans and candy
    /// corn half again as big.
    public static float CandyScale(Sprite candy)
    {
        if (!candy) return 0.8f;
        string n = candy.name;
        bool big = n.StartsWith("gummy") || n.StartsWith("jellybean") || n.StartsWith("candy_corn") || n.StartsWith("flower_");
        return 0.8f * 16f / Mathf.Max(16f, candy.rect.height) * (big ? 1.5f : 1f);
    }

    /// What an orange slice holds past level 1 (level 1: null): candy corn - on level 2 in the slice's own colour
    /// (PitCollectable: CornFor), on level 3 the flowers (the black rose, the red rose,
    /// the hibiscus, the peony); each group of five one of them, the next group the next. `groupY`: the height
    /// it came out at - a group's own (each group has its own random height: EndlessKnightDirector.
    /// QueueOrangeGroup), so a new one is a new group.
    public static Sprite RoadCandy(float groupY)
    {
        if (!Candy()) return null;
        foreach (var (y, treat) in s_claims) if (Mathf.Abs(groupY - y) <= 0.0005f) return treat;   // (chosen as it was queued)
        int level = SpatialEmulator.Demo.PitDemoGame.Level;
        Sprite[] turns;
        if (level == 3 && s_peony) turns = s_peonyOnly ??= new[] { s_peony };   // (level 3: the peony only, in its slice's shade - PeonyFor)
        else turns = null;   // (level 2's corn and level 4's mushroom: in their slices' shades - CornFor, MushroomFor)
        if (turns == null || turns.Length == 0) return s_corn;
        if (s_group < 0 || Mathf.Abs(groupY - s_groupY) > 0.0005f || Time.time - s_groupTime > 4f) { s_group++; s_groupY = groupY; }
        s_groupTime = Time.time;
        return turns[s_group % turns.Length];
    }

    /// A road treat's size, past the candy corn's (PitCollectable): level 4's mushroom (23 px tall in its 32) as big as the corn,
    /// the roses and hibiscus as big as the peony (drawn 25 px tall in their 32), the peony its 29 px flower
    /// as big as before the red rose's leaves went under it (34 px with them).
    public static float RoadScale(Sprite treat) =>
        !treat ? 1f : treat.name.StartsWith("mushroom") ? 1.4f
        : treat.name.EndsWith("_rose") || treat.name == "hibiscus" ? 1.28f : treat.name.StartsWith("peony") ? 34f / 29f : 1f;

    /// A road treat's glow behind it (null: none).
    public static Sprite RoadGlow(Sprite treat) => null;

    /// The next group's treat, chosen as the director queues it - each of its pieces then the same (RoadCandy, by its height). Null on level 1.
    public static Sprite ClaimRoadCandy(float groupY)
    {
        if (!Candy()) return null;
        int level = SpatialEmulator.Demo.PitDemoGame.Level;
        Sprite[] turns;
        if (level == 3 && s_peony) turns = s_peonyOnly ??= new[] { s_peony };   // (level 3: the peony only, in its slice's shade - PeonyFor)
        else turns = null;   // (level 2's corn and level 4's mushroom: in their slices' shades - CornFor, MushroomFor)
        var treat = turns == null || turns.Length == 0 ? s_corn : turns[++s_group % turns.Length];
        s_claims.Add((groupY, treat));
        if (s_claims.Count > 12) s_claims.RemoveAt(0);
        return treat;
    }
    static readonly List<(float, Sprite)> s_claims = new List<(float, Sprite)>();

    /// Level 2's candy corn in its orange slice's colour (Wayne's; Road2's corn_<colour>, corn_colors.py - the orange
    /// one the corn itself), and which colour it is (CornColours; -1: not a slice). `slice`: the slice's own sprite.
    static Sprite[] s_peonyOnly;

    /// Level 3's peony in its orange slice's colour (Wayne's; Road3's peony_<colour>, peony_colors.py), and which colour
    /// it is (CornColours: its petals' burst - FoeFx.Petals). The plain orange slices' groups take turns with the
    /// peony as drawn (its pinks and oranges: a pink burst) - `groupY` the group's (all of one group alike).
    public static Sprite PeonyFor(Sprite slice, float groupY, out int colour)
    {
        colour = 0;
        if (!Candy()) return s_peony;
        string n = slice ? slice.name : "";
        for (int i = CornColours.Length - 1; i >= 0; i--)
            if (n.StartsWith(CornColours[i])) { colour = i; break; }
        if (colour == 0 && Mathf.RoundToInt(groupY * 1000f) % 2 == 0) { colour = 2; return s_peony; }
        if (CornColours[colour] == "green") { colour = 2; return s_peony; }   // (no yellow peony: the drawn one in its place)
        var peony = Resources.Load<Sprite>("Collect/Candy/Road3/peony_" + CornColours[colour]);
        return peony ? peony : s_peony;
    }

    /// Level 4's mushroom in its orange slice's colour (Road4's mushroom_<colour>: mushroom_colors.py), and which colour it is.
    public static Sprite MushroomFor(Sprite slice, out int colour)
    {
        colour = 0;
        string n = slice ? slice.name : "";
        for (int i = CornColours.Length - 1; i >= 0; i--)
            if (n.StartsWith(CornColours[i])) { colour = i; break; }
        var mushroom = Resources.Load<Sprite>("Collect/Candy/Road4/mushroom_" + CornColours[colour]);
        return mushroom ? mushroom : s_corn;
    }

    public static Sprite CornFor(Sprite slice, out int colour)
    {
        colour = -1;
        if (!slice || !Candy()) return s_corn;
        string n = slice.name;
        for (int i = CornColours.Length - 1; i >= 0; i--)   // (plain "orange" last: every one of them ends in it)
            if (n.StartsWith(CornColours[i])) { colour = i; break; }
        if (colour <= 0) return s_corn;
        var corn = Resources.Load<Sprite>("Collect/Candy/Road2/corn_" + CornColours[colour]);
        return corn ? corn : s_corn;
    }
    /// A candy pickup's sound (chests' sweets, the road's candy, level 2's corn): the two in turn.
    public static AudioClip CandySound()
    {
        if (s_candySounds == null)
            s_candySounds = new[] { Resources.Load<AudioClip>("Collect/Candy/Sounds/candy_1"), Resources.Load<AudioClip>("Collect/Candy/Sounds/candy_2") };
        return s_candySounds[s_candySound++ % s_candySounds.Length];
    }
    static AudioClip[] s_candySounds;
    static int s_candySound;

    public static readonly string[] CornColours = { "orange", "red", "pink", "purple", "blue", "green" };

    static int s_group = -1;
    static float s_groupY, s_groupTime;
}
