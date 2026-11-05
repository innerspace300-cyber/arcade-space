using System.Collections.Generic;
using UnityEngine;

// ARcade: the baby frogs bursting out of a Giant Toad in its own colours - its look's sheet (Pink - in place of
// the pack's brown - Red, Blue, Green) - each colour the baby frog's sheet baked again (Resources/Brood/frog_pink,
// _red, _blue, _green: its greens, pale belly and outline in that toad's, its yellow eyes as they are; the same
// frames, by name). RoadBoss.Brood throws out a copy of its baby frog drawn from that sheet.
public static class BroodColors
{
    static readonly Dictionary<(LevelFoes.Foe, string), LevelFoes.Foe> s_copies = new Dictionary<(LevelFoes.Foe, string), LevelFoes.Foe>();

    /// The brood thrown out of a boss drawn with this frame: `baby` in that Giant Toad's colours (as it is: not a Giant Toad's).
    public static LevelFoes.Foe For(LevelFoes.Foe baby, Sprite bossFrame)
    {
        string sheet = bossFrame && bossFrame.texture ? bossFrame.texture.name : "";
        if (baby == null || !sheet.StartsWith("Giant Toad")) return baby;
        string colour = sheet.Contains("Red") ? "red" : sheet.Contains("Blue") ? "blue" : sheet.Contains("Green") ? "green" : sheet.Contains("Pink") ? "pink" : "";
        if (colour == "") return baby;
        if (s_copies.TryGetValue((baby, colour), out var copy)) return copy;
        var frames = new Dictionary<string, Sprite>();
        foreach (var s in Resources.LoadAll<Sprite>("Brood/frog_" + colour)) frames[s.name] = s;
        if (frames.Count == 0) return s_copies[(baby, colour)] = baby;
        copy = baby.Copy();
        copy.idle = Swap(baby.idle, frames);
        copy.walk = Swap(baby.walk, frames);
        copy.attack = Swap(baby.attack, frames);
        copy.death = Swap(baby.death, frames);
        copy.hurt = Swap(baby.hurt, frames);
        return s_copies[(baby, colour)] = copy;
    }

    static Sprite[] Swap(Sprite[] from, Dictionary<string, Sprite> frames)
    {
        if (from == null) return null;
        var to = new Sprite[from.Length];
        for (int i = 0; i < from.Length; i++) to[i] = from[i] && frames.TryGetValue(from[i].name, out var s) ? s : from[i];
        return to;
    }
}
