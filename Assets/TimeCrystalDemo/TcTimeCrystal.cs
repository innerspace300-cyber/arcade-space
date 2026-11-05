using System.Collections;
using UnityEngine;

namespace TimeCrystal
{
    // ARcade: THE TIME CRYSTAL itself, the end of a loop. When OVERSEER-X9 (the third
    // boss) goes down, the crystal it was guarding drops out of it (Creativekind's
    // Magic Crystal, light blue, Scale x its pixels) and floats to the middle of the
    // picture, bobbing. It can be shot (guns, laser, bombs: it's an enemy): every hit
    // knocks crystal shards off it and flashes it white. Shattered, it plays its
    // destruction (cracks, a white flash, dust), a blue copy of ENDLESS KNIGHT's bomb
    // blast goes up bigger than it, and it breaks into Diamonds blue gems that are
    // pulled into the ship (TcGemPull) for DiamondValue each. TcStageDirector waits
    // for it all, then the warp runs backwards: WARP DRIVE MALFUNCTION, back to stage 1.
    public class TcTimeCrystal : TcEnemy
    {
        public const float Scale = 3.4f;          // its 64 px art, at the picture's pixels
        public const int Diamonds = 50;
        public const int DiamondValue = 10000;
        public const int Points = 100000;         // for breaking it (its diamonds besides)
        // Each loop's crystal its own colour - its art, its shattering, its blast, its diamonds and its light:
        // light blue, then purple, green, red, round again.
        public struct Look { public string art, breaking, blast, gem; public Color color; public Color[] neon; }
        public static readonly Look[] Looks =
        {
            new Look { art = "time_crystal", breaking = "time_crystal_break", blast = "boom_blue", gem = "gem_blue", color = new Color(0.45f, 0.9f, 1f), neon = TcFx.BlueNeon },
            new Look { art = "time_crystal_purple", breaking = "time_crystal_break_purple", blast = "boom_purple", gem = "gem_purple", color = new Color(0.75f, 0.45f, 1f), neon = TcFx.PurpleNeon },
            new Look { art = "time_crystal_green", breaking = "time_crystal_break_green", blast = "boom_green", gem = "gem_emerald", color = new Color(0.4f, 1f, 0.5f), neon = TcFx.GreenNeon },
            new Look { art = "time_crystal_red", breaking = "time_crystal_break_red", blast = "boom_red", gem = "gem_red", color = new Color(1f, 0.4f, 0.35f), neon = TcFx.RedNeon },
        };
        public static Look LookFor(int loop) => Looks[(loop % Looks.Length + Looks.Length) % Looks.Length];
        Look _look;
        Color Ice => _look.color;

        /// Out, waiting to be broken (TcStageDirector waits on it).
        public static TcTimeCrystal Current { get; private set; }
        /// It's gone and its blast has finished.
        public static bool Done { get; private set; }

        Vector3 _home;
        float _age, _shards;
        SpriteRenderer _white;

        public static TcTimeCrystal Drop(Vector3 at, int health)
        {
            var go = new GameObject("THE TIME CRYSTAL");
            go.transform.SetParent(TcWorld.Root, false);
            go.transform.position = new Vector3(at.x, at.y, TcWorld.Origin.z);
            go.tag = "ENEMY";
            var c = go.AddComponent<TcTimeCrystal>();
            c._look = LookFor(TcStageDirector.Instance ? TcStageDirector.Instance.Loop : 0);
            c.Setup(c._look.art, health, Scale, c.Ice, Points);
            c._r.flipY = false;   // (drawn standing up)
            c._r.sortingOrder = 14;
            // Its hit flash: the shattering's all-white frame over it.
            var flash = new GameObject("WHITE");
            flash.transform.SetParent(go.transform, false);
            c._white = TcArt.Renderer(flash, 15);
            flash.layer = go.layer;   // (on the crystal's own layer - the effects layer draws under the enemies)
            var breaking = TcArt.Frames(c._look.breaking);
            if (breaking != null && breaking.Length > 5) c._white.sprite = breaking[5];
            c._white.color = new Color(1f, 1f, 1f, 0f);
            // Hit anywhere on the crystal itself (not its chains and braziers).
            var col = go.GetComponent<CircleCollider2D>();
            if (col) col.radius = 0.32f;
            var f = TcWorld.Field;
            c._home = new Vector3(f.center.x, f.yMin + f.height * 0.62f, TcWorld.Origin.z);
            Current = c;
            Done = false;
            TcSfx.Play("warp", 0.6f, 0.4f);
            TcFx.Burst(at, c.Ice, 40, 5f, 0.08f, 0.6f);
            return c;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Current == this) Current = null;
        }

        protected override void Update()
        {
            base.Update();
            if (_r) _r.color = Color.white;   // (its own white flash, not the ships' red one)
            _age += Time.deltaTime;
            // Floating down to the middle, then hanging there, bobbing and turning a little.
            var target = _home + Vector3.up * (0.12f * Mathf.Sin(_age * 1.6f));
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 1.4f);
            transform.rotation = Quaternion.Euler(0f, 0f, 3f * Mathf.Sin(_age * 0.9f));
            if (_white) _white.color = new Color(1f, 1f, 1f, Mathf.MoveTowards(_white.color.a, 0f, Time.deltaTime * 8f));
            // A few motes of its light drifting off it all the time.
            if (Random.value < Time.deltaTime * 12f)
                TcFx.Trail(transform.position + (Vector3)(Random.insideUnitCircle * 0.5f), TcFx.WithAlpha(Ice, 0.8f), 0.06f, Random.insideUnitCircle * 0.6f, 0.6f);
        }

        // Only once it's well into the picture (it drops in from the boss).
        protected override bool Hittable => TcWorld.Field.Contains((Vector2)transform.position) && _age > 0.6f;

        public override void Hurt(int damage)
        {
            if (IsDead) return;
            if (_white) _white.color = Color.white;
            // Shards knocked off it - more the harder it's hit (the laser hurts it a little at a time).
            _shards += damage * 3f;
            int n = Mathf.Min(12, (int)_shards);
            _shards -= n;
            for (int i = 0; i < n; i++)
            {
                var at = transform.position + (Vector3)(Random.insideUnitCircle * 0.45f);
                TcFx.Burst(at, Random.value < 0.4f ? Color.white : Ice, 1, Random.Range(2f, 6f), Random.Range(0.05f, 0.11f), Random.Range(0.4f, 0.8f), Vector2.down * 1.5f);
            }
            base.Hurt(damage);
        }

        protected override void Die()
        {
            if (IsDead) return;
            MarkDead();
            var at = transform.position;
            if (ScoreManager.Instance) ScoreManager.Instance.AddScore(Points);
            var dir = TcStageDirector.Instance;
            if (dir) dir.Popup(at, Points);
            dir?.StartCoroutine(Shatter(at, transform.localScale.x, _look));
            Destroy(gameObject);
        }

        static IEnumerator Shatter(Vector3 at, float scale, Look look)
        {
            var Ice = look.color;
            // Its destruction (cracks, the white flash, dust), where it was.
            var go = new GameObject("TIME CRYSTAL SHATTERING");
            go.transform.SetParent(TcWorld.Root, false);
            go.transform.position = at;
            go.transform.localScale = Vector3.one * scale;
            TcArt.Renderer(go, 14);
            var anim = go.AddComponent<TcAnim>();
            anim.DestroyAtEnd = true;
            anim.Play(TcArt.Frames(look.breaking), 14f, loop: false);
            TcFx.ScreenFlash(Color.white, 0.8f, 0.6f);
            TcSfx.Play("player_death", 1f, 0.05f);
            if (TcSpaceScroller.Instance) TcSpaceScroller.Instance.Flash(Ice, 3, 0.35f);
            // The blue blast, bigger than the crystal, and its neon pixels.
            TcFx.Explode(at + Vector3.down * 0.2f, look.blast, scale * 1.15f, Ice, 80, 18f);
            TcFx.NeonNova(at, 1.6f, look.neon);
            for (int i = 0; i < 40; i++)
                TcFx.Burst(at + (Vector3)(Random.insideUnitCircle * 0.6f), Random.value < 0.3f ? Color.white : Ice, 1, Random.Range(3f, 9f), Random.Range(0.06f, 0.14f), Random.Range(0.6f, 1.2f));
            // ...and its diamonds (its colour), flung out all over the picture, then pulled into the
            // ship a few at a time (so each one's points can be read).
            var dir = TcStageDirector.Instance;
            for (int i = 0; i < Diamonds; i++)
            {
                var gem = dir ? dir.DropGem(at + (Vector3)(Random.insideUnitCircle * 0.5f), 1, look.gem) : null;
                if (gem && gem.GetComponent<CollectableItem>() is { } item) item.scoreValue = DiamondValue;
                if (gem && gem.GetComponent<TcGemPull>() is { } pull) pull.Scatter(Random.Range(5f, 12f), 1.1f + 1.8f * i / Diamonds + Random.Range(0f, 0.3f));
                if (i % 10 == 9) yield return null;   // (spread over a few frames)
            }
            // Done once the blast has played (about 1.2 s at 18 a second).
            yield return new WaitForSeconds(1.4f);
            Done = true;
        }

        /// No crystal about: a new run, or the game's gone.
        public static void Clear() { Current = null; Done = false; }
    }
}
