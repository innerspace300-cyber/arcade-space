using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TimeCrystal
{
    // ARcade: THE TIME CRYSTAL's bombs, one for each pilot (TcPilots). Each
    // clears the picture its own way, hitting what it reaches as it goes,
    // and everything at the end:
    //  - NOVA: MISSILE STORM - a swarm of blue missiles curling out from the
    //    ship to every enemy and all over, each a blast, a wave rising behind;
    //  - BLAZE: TWIN DRAGONS - red and gold Chinese dragons spiralling out, then breathing fire beside the ship;
    //  - VIPER: SNAKE SWARM - energy snakes coiled round the ship, then shooting out every way, hissing;
    //  - RIO: TURRET BARRAGE - five combat drones round him, spinning round onto targets and firing miniguns.
    public static class TcBombFx
    {
        /// Every bomb's art drawn and sounds loaded ahead (when the game starts), so the first press doesn't hitch
        /// (BLAZE's dragons were drawn and their long roar loaded right then).
        public static void Prewarm()
        {
            TcDragonPair.Prewarm();
            TcSnakeSwarm.Prewarm();
            TcTurretSwarm.Prewarm();
            _ = TcBubbleArt.Frames;
            foreach (var name in new[] { "dragon_roar", "dragon_fire", "snake_hiss", "turret_deploy", "turret_servo", "turret_lock",
                                         "minigun_1", "minigun_2", "minigun_3", "round_hit_1", "round_hit_2", "boss_lightning" })
            {
                var clip = TcSfx.Clip(name);
                if (clip && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            }
        }

        public static IEnumerator Run(MonoBehaviour host, int pilot, Vector3 from)
        {
            var hit = new HashSet<TcEnemy>();
            var color = TcPilots.All[Mathf.Clamp(pilot, 0, TcPilots.All.Length - 1)].Color;
            TcFx.ScreenFlash(color, 0.35f, 0.4f);
            switch (pilot)
            {
                case 1: yield return DragonFire(hit, from); break;
                case 2: yield return SnakeSwarm(hit, from); break;
                case 3: yield return TurretBarrage(hit, from); break;
                default: yield return MissileStorm(host, hit, from); break;
            }
            // Whatever's left goes - except after BLAZE's dragons: only what their fire visibly reaches burns.
            if (pilot != 1) Strike(hit, p => true);
        }

        // Every enemy where `inside` says, hit once this bomb (a boss is only dented); bullets there gone.
        static void Strike(HashSet<TcEnemy> hit, System.Func<Vector2, bool> inside)
        {
            var f = TcWorld.Field;
            foreach (var enemy in TcEnemy.All.ToArray())
            {
                if (!enemy || hit.Contains(enemy)) continue;
                Vector2 p = enemy.transform.position;
                if (!f.Contains(p) || !inside(p)) continue;
                hit.Add(enemy);
                enemy.Kill();
            }
            if (TcWorld.Root)
                foreach (var old in TcWorld.Root.GetComponentsInChildren<EnemyShooter2D>())
                    if (f.Contains((Vector2)old.transform.position) && inside(old.transform.position)) old.Kill();
            TcDanmaku.ClearWhere(inside);
        }

        static SpriteRenderer Thing(string name, string art, float scale, int order, float fps = 12f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(TcWorld.Root, false);
            go.transform.localScale = Vector3.one * scale;
            var r = TcArt.Renderer(go, order);
            var frames = TcArt.Frames(art);
            if (frames != null && frames.Length > 1) go.AddComponent<TcAnim>().Play(frames, fps);
            else if (frames != null && frames.Length == 1) r.sprite = frames[0];
            else r.sprite = TcArt.Pixel;
            return r;
        }

        // ---- NOVA: MISSILE STORM ----
        static IEnumerator MissileStorm(MonoBehaviour host, HashSet<TcEnemy> hit, Vector3 from)
        {
            var f = TcWorld.Field;
            var targets = new List<Vector3>();
            foreach (var e in TcEnemy.All) if (e && f.Contains((Vector2)e.transform.position)) targets.Add(e.transform.position);
            while (targets.Count < 26) targets.Add(new Vector3(Random.Range(f.xMin + 0.5f, f.xMax - 0.5f), Random.Range(f.yMin + 2f, f.yMax - 0.5f), TcWorld.Origin.z));
            for (int i = 0; i < targets.Count && i < 40; i++)
                host.StartCoroutine(Missile(hit, from, targets[i], i * 0.03f, i % 2 == 0 ? 1f : -1f));
            // A wave rising up the picture behind them.
            for (float t = 0f; t < 1.3f; t += Time.deltaTime)
            {
                float line = f.yMin + f.height * (t / 1.1f);
                Strike(hit, p => p.y < line);
                if (Random.value < 0.6f) TcFx.Burst(new Vector3(Random.Range(f.xMin, f.xMax), line, TcWorld.Origin.z), TcFx.Cyan, 3, 2f, 0.06f, 0.3f);
                yield return null;
            }
        }

        static IEnumerator Missile(HashSet<TcEnemy> hit, Vector3 from, Vector3 to, float delay, float side)
        {
            yield return new WaitForSeconds(delay);
            if (!TcWorld.Root) yield break;
            var r = Thing("BOMB MISSILE", "pj1_scifi_missile_small_blue", 1.4f, 940);
            var bend = from + (Vector3)(new Vector2(side * Random.Range(2f, 4f), Random.Range(0.5f, 2.5f)));
            TcSfx.Play("boss_missile", 0.5f, 0.06f);
            Vector3 last = from;
            float time = Random.Range(0.45f, 0.75f);
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float k = t / time; k = k * k;
                Vector3 p = (1 - k) * (1 - k) * from + 2 * (1 - k) * k * bend + k * k * to;
                Vector3 v = p - last;
                if (v.sqrMagnitude > 0.000001f) r.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
                r.transform.position = new Vector3(p.x, p.y, TcWorld.Origin.z);
                TcFx.Trail(p, TcFx.WithAlpha(TcFx.Cyan, 0.8f), 0.06f, Vector2.zero, 0.25f);
                last = p;
                yield return null;
            }
            Object.Destroy(r.gameObject);
            TcFx.NeonNova(to, 0.7f, TcFx.BlueNeon, flash: false);
            TcFx.Explode(to, "explosion_small", 1f, TcFx.Cyan, 8);
            TcSfx.Play("boss_chain", 0.6f, 0.05f);
            Strike(hit, p => (p - (Vector2)to).sqrMagnitude < 1.6f * 1.6f);
        }

        // ---- BLAZE: TWIN DRAGONS (TcDragonPair) ----
        // Two Chinese dragons spiralling out of the ship, then breathing fire beside it as it goes: an ordinary
        // enemy burns down (the big ones take longer), a boss loses a share of its health at the most.
        const float DragonBossShare = 0.15f;

        static IEnumerator DragonFire(HashSet<TcEnemy> hit, Vector3 from)
        {
            TcSfx.Play("dragon_roar", 0.6f, 0f);   // (AudioJungle's "The Golden Dragon", cut to its first 1.6 seconds: a short roar as they come in)
            var owed = new Dictionary<TcEnemy, float>();
            int bossLost = 0;
            float fireSound = 0f;
            bool firstBlast = true;
            var dragons = TcDragonPair.Start(from, (t, amount) =>
            {
                var e = t ? t.GetComponent<TcEnemy>() : null;
                if (!e || e.Health <= 0) return;
                owed.TryGetValue(e, out float d);
                d += amount;
                int whole = Mathf.FloorToInt(d + 1e-4f);
                if (whole > 0)
                {
                    d -= whole;
                    if (e is TcBoss boss)
                    {
                        hit.Add(boss);
                        int most = Mathf.Max(1, Mathf.RoundToInt(boss.MaxHealth * DragonBossShare));
                        whole = Mathf.Min(whole, most - bossLost);
                        if (whole > 0) { bossLost += whole; boss.Hurt(whole); }
                    }
                    else e.Hurt(whole);
                }
                owed[e] = d;
            });
            for (float t = 0f; dragons && !dragons.Done && t < TcDragonPair.Longest + 4f; t += Time.deltaTime)
            {
                if (dragons.Breathing)
                {
                    fireSound -= Time.deltaTime;
                    if (fireSound <= 0f) { fireSound = 0.9f; TcSfx.Play("dragon_fire", firstBlast ? 0.5f : 0.25f, 0.5f); firstBlast = false; }   // (the first blast loud, the rest under it)
                }
                yield return null;
            }
            if (dragons) Object.Destroy(dragons.gameObject);
        }

        // ---- VIPER: SNAKE SWARM (TcSnakeSwarm) ----
        // Energy snakes coiled round the ship, crackling, then out in every direction, hissing - anything
        // a head passes is struck.
        static IEnumerator SnakeSwarm(HashSet<TcEnemy> hit, Vector3 from)
        {
            TcSfx.Play("snake_hiss", 1f, 0f);
            var swarm = TcSnakeSwarm.Start(from, at => Strike(hit, q => (q - at).sqrMagnitude < TcSnakeSwarm.Bite * TcSnakeSwarm.Bite));   // (on contact)
            bool zapped = false;
            for (float t = 0f; swarm && !swarm.Done && t < 4f; t += Time.deltaTime)
            {
                if (!zapped && t >= TcSnakeSwarm.CoilSeconds) { zapped = true; TcSfx.Play("boss_lightning", 0.8f, 0f); TcSfx.Play("snake_hiss", 0.7f, 0f); }
                yield return null;
            }
            if (swarm) Object.Destroy(swarm.gameObject);
        }

        // ---- RIO: TURRET BARRAGE (TcTurretSwarm) ----
        // Five combat drones on an arc round him, locking on and hosing the enemies with minigun tracers.
        const float BossShare = 0.12f;   // (the most of a boss's health RIO's turrets take)
        const float RoundDamage = 1f / 5f;   // (an ordinary enemy's hit points a turret round takes: five rounds a point)

        static IEnumerator TurretBarrage(HashSet<TcEnemy> hit, Vector3 from)
        {
            TcSfx.Play("turret_deploy", 1f, 0f);   // (ratchets, a servo whirring up, the guns clunking out: unity-helpers/turret_sfx.py)
            // A round's hit, on the one enemy it struck: an ordinary one loses a fifth of a hit point a round (so
            // the smallest take five, the big ones whole streams) - its own death when it's out; a boss a
            // little a round, a share of its health at the most over the whole barrage (and no bomb dent
            // after: it's counted as hit).
            int bossLost = 0;
            var owed = new Dictionary<TcEnemy, float>();
            var turrets = TcTurretSwarm.Start(from, at =>
            {
                foreach (var e in TcEnemy.All.ToArray())
                {
                    if (!e || e.Health <= 0 || ((Vector2)e.transform.position - at).sqrMagnitude > TcTurretSwarm.HitRadius * TcTurretSwarm.HitRadius) continue;
                    if (e is TcBoss boss)
                    {
                        hit.Add(boss);
                        int most = Mathf.Max(1, Mathf.RoundToInt(boss.MaxHealth * BossShare));
                        if (bossLost < most)
                        {
                            int lose = Mathf.Min(most - bossLost, Mathf.Max(1, boss.MaxHealth / 250));
                            bossLost += lose;
                            boss.Hurt(lose);
                        }
                    }
                    else
                    {
                        owed.TryGetValue(e, out float d);
                        d += RoundDamage;
                        int whole = Mathf.FloorToInt(d + 1e-4f);
                        if (whole > 0) { d -= whole; e.Hurt(whole); }
                        owed[e] = d;
                    }
                    break;   // (one enemy a round)
                }
            });
            for (float t = 0f; turrets && !turrets.Done && t < TcTurretSwarm.Longest + 2f; t += Time.deltaTime) yield return null;
            if (turrets) Object.Destroy(turrets.gameObject);
        }
    }
}
