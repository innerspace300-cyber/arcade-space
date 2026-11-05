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

    /// Viking food: she's big (BigScale) until BigUntil - her sword's blows BigDamage times as hard.
    public static float BigUntil = -1f;
    public static bool Big => Time.time < BigUntil;
    public const float BigScale = 1.45f;
    public const int BigDamage = 5;
    /// Her size now, growing or shrinking (PlayerScriptARIANAClips).
    public static float BigNow = 1f;
    /// Played as she shrinks back.
    public static AudioClip ShrinkSound;
    /// Food eaten: big (for longer, if she already is), and a flash of colour over her.
    public static System.Action<Color> Ate;
    public static void GoBig(float seconds, Color flash, AudioClip shrink)
    {
        BigUntil = Mathf.Max(BigUntil, Time.time) + (Big ? seconds * 0.5f : seconds);
        if (shrink) ShrinkSound = shrink;
        Ate?.Invoke(flash);
    }

    /// Played as a blow glances off her Shield Buff (Blocked).
    public static AudioClip BlockSound;
    static float s_nextBlock;

    /// Something would have hurt her (a foe, a boss, a trap, a bomb) but her Shield Buff took it: a gold
    /// pixel burst off her body, toward where it came from, and a ricochet. (Not more than one every 0.2 s.)
    public static void Blocked(float fromX = float.NaN)
    {
        if (!Shield || Time.time < s_nextBlock) return;
        var player = EndlessKnightDirector.Player;
        if (!player || player.IsDead) return;
        s_nextBlock = Time.time + 0.2f;
        var body = RoadFoe.PlayerBody();
        if (body.size == Vector3.zero) return;
        float side = float.IsNaN(fromX) ? (player.transform.localScale.x < 0f ? -1f : 1f) : Mathf.Sign(fromX - body.center.x);
        var at = new Vector3(body.center.x + side * body.extents.x * 0.45f, body.center.y + Random.Range(-0.15f, 0.2f) * body.extents.y, body.center.z - 0.02f);
        FoeFx.GoldBurst(at, new Vector2(side, 0.35f));
        if (BlockSound) PitAudio.PlayClip(BlockSound, 0.8f);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Light);
    }

    public static void Reset() { SwordFrom = SwordUntil = ShieldFrom = ShieldUntil = -1f; BigUntil = -1f; }
}
