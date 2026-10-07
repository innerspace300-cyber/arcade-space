using UnityEngine;

// ARcade: ENDLESS KNIGHT's spell buffs (the C button's Sword Buff and Shield
// Buff, PlayerScriptARIANAClips), each for buffSeconds after its cast:
//  - Sword: every attack does double damage, and a golden outline shimmers
//    round the player (PlayerGlow).
//  - Shield: nothing hurts the player or knocks them back - traps, bombs,
//    the Slime Boss (StatsControl, PlayerScriptARIANAClips.Knockback,
//    BombGuy) - and a golden outline shimmers round them (PlayerGlow), like
//    a star in Mario. It protects from the start of its cast.
// Cleared for each new run and on death.
public static class PlayerBuffs
{
    /// Times (Time.time) each buff shows from (its cast's end) and ends.
    public static float SwordFrom, SwordUntil, ShieldFrom, ShieldUntil;

    public static bool Sword => Time.time < SwordUntil && Time.time >= SwordFrom - 0.01f;
    public static bool Shield => Time.time < ShieldUntil;
    public static bool ShieldShowing => Time.time < ShieldUntil && Time.time >= ShieldFrom;

    public static void Reset() => SwordFrom = SwordUntil = ShieldFrom = ShieldUntil = -1f;
}
