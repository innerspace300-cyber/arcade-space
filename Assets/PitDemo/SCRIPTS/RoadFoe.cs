using System.Collections.Generic;
using UnityEngine;

/// ARcade: a boss on the road - the Slime Boss (level 1) or a RoadBoss (the
/// giant toad, the dragon, the necromancer) - for what deals with whichever
/// it is: the director, its health bar, the fairy's ice spikes, being solid.
public interface IRoadBoss
{
    SpriteRenderer Body { get; }
    /// Its body, for standing against (the player stops at its edge).
    Bounds BodyBounds { get; }
    /// A recolour its health bar takes (null: none).
    Material BodyRecolor { get; }
    int MaxHealth { get; }
    int Health { get; }
    bool IsDead { get; }
    /// A little damage and a push back (the fairy's ice spikes).
    void Chip(int damage, float push);
}

/// ARcade: what the road's foes (RoadEnemy, RoadBoss) share - being hit by
/// the knight's blows, hurting her, and how they die (FoeFx).
public static class RoadFoe
{
    /// The bosses on the road now (dead ones too, until they're gone).
    public static List<IRoadBoss> Bosses()
    {
        var all = new List<IRoadBoss>();
        foreach (var slime in Object.FindObjectsByType<EnemyStateMachineSlimeBoss>(FindObjectsSortMode.None)) all.Add(slime);
        foreach (var boss in RoadBoss.All) if (boss) all.Add(boss);
        return all;
    }

    /// A kill's points (level 1: a foe 1000, a boss 5000; each level on, that many more - level 2 double,
    /// level 4 four times), as bonus points: they don't bring the next boss sooner (TreasureChest.BonusPoints).
    /// A foe a boss summoned is worth a quarter (no farming the necromancer).
    public static void KillPoints(bool boss, bool summoned = false)
    {
        int level = Mathf.Clamp(SpatialEmulator.Demo.PitDemoGame.Level, 1, 4);
        int points = (boss ? BossPoints : FoePoints) * level / (summoned ? 4 : 1);
        if (!PitScoreManager.Instance) return;
        PitScoreManager.Instance.AddScore(points);
        TreasureChest.BonusPoints += points;
    }
    public const int FoePoints = 1000, BossPoints = 5000;

    /// Still there (a destroyed boss's interface isn't null, but its object is).
    public static bool Here(IRoadBoss boss) => boss != null && (boss as Object) != null;

    /// A blow landing (the knight's sword, heavy: her Holy Slash): every foe its area reaches is hit.
    public static void HitAll(Bounds area, int damage, bool heavy)
    {
        RoadEnemy.HitAll(area, heavy);
        foreach (var boss in RoadBoss.All.ToArray())
            if (boss && boss.Overlaps(area)) boss.OnHit(damage, heavy);
    }

    /// Where a sprite's pixels are drawn (its outline, not its canvas).
    public static Bounds Drawn(SpriteRenderer r)
    {
        var sprite = r ? r.sprite : null;
        if (!sprite) return r ? r.bounds : default;
        Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
        foreach (var v in sprite.vertices)
        {
            var world = r.transform.TransformPoint(new Vector3(r.flipX ? -v.x : v.x, r.flipY ? -v.y : v.y, 0f));
            min = Vector3.Min(min, world);
            max = Vector3.Max(max, world);
        }
        var drawn = new Bounds();
        drawn.SetMinMax(min, max);
        return drawn;
    }

    static StatsControl s_stats;

    /// Hurts the knight (not under her Shield Buff), knocking her up and back.
    public static void HurtPlayer(int damage, float knockHeight = 0.5f, float knockBack = 0.4f, float fromX = float.NaN)
    {
        var player = EndlessKnightDirector.Player;
        if (!player || player.IsDead) return;
        if (PlayerBuffs.Shield) { PlayerBuffs.Blocked(fromX); return; }   // (it glances off her shield)
        if (!s_stats) s_stats = Object.FindAnyObjectByType<StatsControl>();
        if (!s_stats || s_stats.GetCurrentHP() <= 0) return;
        // (IncreaseHP ignores a change that would go below 0: the last hit takes what's left.)
        s_stats.IncreaseHP(-Mathf.Min(damage, s_stats.GetCurrentHP()));
        player.Knockback(knockHeight, knockBack);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Heavy);
    }

    /// The knight's drawn body.
    public static Bounds PlayerBody()
    {
        var player = EndlessKnightDirector.Player;
        var art = player ? player.transform.Find("ARIANA") : null;
        var sr = art ? art.GetComponent<SpriteRenderer>() : null;
        return sr ? Drawn(sr) : default;
    }

    /// One of the characters' pixels, world units.
    /// Slid right, if need be, till all of `body` (a boss's drawn body, just spawned) is past the
    /// picture's right edge: it comes on screen only as she goes to it, never popping in.
    public static void Offscreen(Transform root, Bounds body)
    {
        var cam = SpatialEmulator.Demo.PitDemoGame.ActorsCamera;
        if (!cam || !cam.orthographic || !root) return;
        float edge = cam.transform.position.x + cam.orthographicSize * cam.aspect + 0.15f;
        if (body.min.x < edge) root.position += Vector3.right * (edge - body.min.x);
    }

    public static float Pixel => SpikeTrap.PixelSize > 0f ? SpikeTrap.PixelSize : 0.02f;
}
