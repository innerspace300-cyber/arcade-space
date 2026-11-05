using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TimeCrystal
{
    // ARcade: THE TIME CRYSTAL's enemies (Pixel Spaceships for SHMUP 2,
    // turned to face down), made by TcStageDirector: each with its own
    // health, size and colour (its sparks and explosion), flashing as it's
    // hit, going up in an animated explosion with sparks, scored, sometimes
    // leaving a gem or a bomb - and the gold carriers always a power-up.
    // The player's shots (TcShot) do their damage; a bomb (TcBombs) kills it.
    public class TcEnemy : MonoBehaviour
    {
        public static readonly List<TcEnemy> All = new List<TcEnemy>();

        public int Health, MaxHealth, Score;
        public Color Color;
        /// Chance it leaves a power-up (TcWeapons) as it goes; 1 = always.
        public float PowerUpChance;
        public bool DropsPowerUp { set => PowerUpChance = value ? 1f : 0f; }
        public float GemChance = 0.35f;
        /// Its sounds: TcSfx's "<Voice>_shot", "_hit", "_death".
        public string Voice;
        const float WideLaserChance = 0.25f, OtherGunChance = 0.35f;
        // Every ordinary enemy's gun (TcSfx; not the gold carriers' burst or the bosses').
        static readonly string[] OtherGuns = { "popcorn_shot", "dart_shot", "fighter_shot", "violet_shot", "gunship_shot", "heavy_shot", "wing_shot", "bomber_shot" };

        /// One of its shots (TcDanmaku, while its pattern runs). The boss's
        /// each sound like their own kind of projectile.
        public void PlayShot(BulletKind kind)
        {
            if (string.IsNullOrEmpty(Voice)) return;
            string clip = Voice + "_shot";
            if (this is TcBoss)
                clip = kind switch
                {
                    BulletKind.Fireball => "boss_fireball",
                    BulletKind.Bolt => "boss_bolt",
                    BulletKind.Missile => "boss_missile",
                    BulletKind.BigOrb => "boss_orb",
                    BulletKind.Kunai => "boss_kunai",
                    BulletKind.Lightning => "boss_lightning",
                    _ => "boss_shot",
                };
            // The gold carriers' (power-ups and bombs): Science Fiction Laser Rampant Shots, a burst, so
            // never on top of itself. The rest, now and then, the Wide Laser Shot instead of their own.
            if (Voice == "carrier" && !(this is TcBoss)) { TcSfx.Play(clip, 0.6f, 1.2f); return; }
            if (!(this is TcBoss))
            {
                // A mix: its own sound under half the time, the Wide Laser Shot a quarter, else any of the others' guns.
                float roll = Random.value;
                if (roll < WideLaserChance) { TcSfx.Play("wide_laser_shot", 0.6f, 0.3f); return; }   // (a long one: kept apart)
                if (roll > 1f - OtherGunChance) clip = OtherGuns[Random.Range(0, OtherGuns.Length)];
            }
            TcSfx.Play(clip, 0.6f, 0.07f);
        }

        /// Its gun ports: art pixels from its middle, as drawn (+x right, +y
        /// toward its front, which faces down). Shots leave from them.
        public Vector2[] Ports;
        int _port;

        /// A port's place in the world (or just under its middle if it has none).
        public Vector3 Port(int i)
        {
            if (Ports == null || Ports.Length == 0) return transform.position + Vector3.down * 0.25f * transform.localScale.y;
            var p = Ports[((i % Ports.Length) + Ports.Length) % Ports.Length];
            var at = transform.TransformPoint(new Vector3(p.x, -p.y, 0f) / TcArt.PixelsPerUnit);
            // A spark or two at the muzzle.
            TcFx.Burst(at, Color, 2, 1.2f, 0.05f, 0.12f, Vector2.down);
            return at;
        }

        /// Its ports in turn (left, right, ...).
        public Vector3 NextPort() => Port(_port++);

        protected SpriteRenderer _r;
        float _flash;
        bool _dead;

        public bool IsDead => _dead;
        /// Gone for good from here on (a Die of its own: TcTimeCrystal).
        protected void MarkDead() => _dead = true;

        /// A ship of TcArt's `art` at `at`, `scale` times its pixels, `health` hits.
        public static TcEnemy Make(string art, Vector3 at, int health, float scale, Color color, int score)
        {
            var go = new GameObject(art.ToUpperInvariant());
            go.transform.SetParent(TcWorld.Root, false);
            go.transform.position = new Vector3(at.x, at.y, TcWorld.Origin.z);
            go.tag = "ENEMY";
            var e = go.AddComponent<TcEnemy>();
            e.Setup(art, health, scale, color, score);
            return e;
        }

        protected void Setup(string art, int health, float scale, Color color, int score)
        {
            Health = MaxHealth = health;
            Score = score;
            Color = color;
            _r = TcArt.Renderer(gameObject, 12);
            gameObject.layer = LayerMask.NameToLayer("TcEnemy");
            var frames = TcArt.Frames(art);
            if (frames != null && frames.Length > 1) gameObject.AddComponent<TcAnim>().Play(frames, 14f);
            else _r.sprite = TcArt.Frame(art);
            _r.flipY = true;   // (the art faces up)
            transform.localScale = Vector3.one * scale;


            var body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            var col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = _r.sprite ? _r.sprite.bounds.extents.x * 0.6f : 0.3f;
        }

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        /// The nearest enemy (or the boss) within `range` of `from`.
        public static Transform Nearest(Vector3 from, float range)
        {
            Transform best = null;
            float bestD = range * range;
            foreach (var e in All)
            {
                if (!e || e._dead) continue;
                float d = (e.transform.position - from).sqrMagnitude;
                if (d < bestD && TcWorld.Field.Contains((Vector2)e.transform.position)) { bestD = d; best = e.transform; }
            }
            return best;
        }

        void OnTriggerEnter2D(Collider2D other) => Touch(other);
        void OnTriggerStay2D(Collider2D other) { if (other.GetComponent<TcShot>() is TcShot s && s.Pierce) Touch(other); }

        void Touch(Collider2D other)
        {
            if (_dead || !Hittable) return;
            var shot = other.GetComponent<TcShot>();
            if (!shot) return;
            var at = other.transform.position;
            at.y = Mathf.Min(at.y, transform.position.y);
            if (shot.Land(this, at))
            {
                // Bits of the ship knocked off where it's hit, and a few sparks of its colour.
                TcFx.Debris(_r.sprite, at, transform.localScale.x, 2 + Mathf.Min(shot.Damage, 3));
                TcFx.Burst(at, Color, 4, 2.6f, 0.05f, 0.3f);
                Hurt(shot.Damage);
            }
        }

        /// It can be hit (in the picture: not before it's on screen).
        protected virtual bool Hittable => TcWorld.Field.Contains((Vector2)transform.position);

        public virtual void Hurt(int damage)
        {
            if (_dead) return;
            Health -= damage;
            _flash = 0.06f;
            // Every enemy: the same little pop while it holds out (its own
            // bang is for going up).
            TcSfx.Play("enemy_hit", 0.55f, 0.05f);
            if (Health <= 0) Die();
        }

        /// A bomb: gone, scored, like being shot down.
        public virtual void Kill()
        {
            if (!_dead) { Health = 0; Die(); }
        }

        protected virtual void Update()
        {
            if (_flash > 0f)
            {
                _flash -= Time.deltaTime;
                _r.color = _flash > 0f ? new Color(1f, 0.45f, 0.45f) : Color.white;
            }
            // Badly hurt big ones smoke.
            if (MaxHealth >= 10 && Health < MaxHealth * 0.35f && Random.value < Time.deltaTime * 25f)
                TcFx.Trail(transform.position + (Vector3)(Random.insideUnitCircle * 0.3f * transform.localScale.x), new Color(0.6f, 0.6f, 0.7f, 0.8f), 0.12f, Vector2.up * 0.8f, 0.5f);
        }

        protected virtual void Die()
        {
            _dead = true;
            // What it had in the air turns to coins (a boss's: GoingDown).
            if (!(this is TcBoss)) TcDanmaku.ToCoins(this);
            // ...and coins burst out of it, more from bigger ships (a boss: lots).
            TcCoin.Burst(transform.position, this is TcBoss ? 150 : Mathf.Clamp(2 + MaxHealth / 2, 2, 30), transform.localScale.x);
            float size = transform.localScale.x;
            bool big = MaxHealth >= 10;
            // (Bigger than they were: about 40%.)
            if (!(this is TcBoss))   // (a boss's end is its own: TcBoss.GoingDown)
            {
                TcFx.Explode(transform.position, big ? "explosion_large" : "explosion_small", big ? 0.78f * size : 1f * size, Color, big ? 40 : 20);
                // A falling pixel blast in one of the stage's colours (not the rainbow confetti).
                TcFx.StageBlast(transform.position, (big ? 1.2f : 0.55f) * size);
            }
            if (!string.IsNullOrEmpty(Voice)) TcSfx.Play(Voice + "_death", 1f, 0.05f);
            else if (TcStageDirector.Instance) TcStageDirector.Instance.PlayDestroyed(big);
            if (ScoreManager.Instance) ScoreManager.Instance.AddScore(Score);
            var dir = TcStageDirector.Instance;
            if (dir) dir.Popup(transform.position, Score);
            if (dir && Random.value < GemChance) dir.DropGem(transform.position, this is TcBoss boss ? boss.Level : -1);
            if (TcBombs.Instance) TcBombs.Instance.MaybeDrop(transform.position);
            if (Random.value < PowerUpChance) TcPowerUp.Drop(transform.position, TcPowerUp.Any);
            Destroy(gameObject);
        }
    }

    /// The boss: one big ship (Plane 06, powered up, three times its pixels)
    /// with a lot of health and a health bar over it (Pixel UI's boss bar,
    /// as ENDLESS KNIGHT's) - its attacks get wilder as it's worn down
    /// (TcStageDirector's BossPattern reads Phase), and it goes out in a
    /// chain of explosions.
    public class TcBoss : TcEnemy
    {
        public static TcBoss Current { get; private set; }
        /// Which: 0, 1, 2 (stage 1, 2, 3's) - its gems are its own (TcStageDirector.DropGem).
        public int Level { get; private set; }
        public string BarName = "SENTRY-01";
        /// Clear rows of art over its drawn top (facing down, as drawn).
        float TopPadding = 8f;

        SpriteRenderer _back, _trail, _fill, _frame;
        TextMeshPro _label;
        Transform _bar;
        float _trailFrac = 1f, _hitAt = -10f;
        bool _exploding;

        /// 0, 1, 2: worn down past two thirds, one third.
        public int Phase => Health > MaxHealth * 2 / 3 ? 0 : Health > MaxHealth / 3 ? 1 : 2;

        // The three: stage 1, 2 and 3's (their art, size, name, colour, score,
        // clear rows over the drawn top, and guns - art pixels from the
        // middle, the front down: 0, 1 cannons; 2 nose; 3, 4 inner ports;
        // 5, 6 wingtips - the same roles, so the stage's patterns fit each).
        struct Kind { public string art, name; public float scale, pad; public Color color; public int score; public Vector2[] ports; }
        static readonly Kind[] Kinds =
        {
            // Ship 35: the teal pods, the nose, the front edges by it, the wingtips.
            new Kind { art = "enemy_boss", name = "SENTRY-01", scale = 3f, pad = 8f, color = TcFx.Cyan, score = 50000, ports = new[]
                { new Vector2(-16f, 6f), new Vector2(16f, 6f), new Vector2(0f, 26f), new Vector2(-6f, 18f), new Vector2(6f, 18f), new Vector2(-42f, -15f), new Vector2(42f, -15f) } },
            // Ship 25: the two long prongs, the blue pod, the blue strips, the wingtips.
            new Kind { art = "enemy_boss2", name = "HIVE NODE-7", scale = 4.2f, pad = 7f, color = TcFx.Violet, score = 80000, ports = new[]
                { new Vector2(-6f, 24f), new Vector2(6f, 24f), new Vector2(0f, 7f), new Vector2(-11f, 9f), new Vector2(11f, 9f), new Vector2(-31f, -10f), new Vector2(31f, -10f) } },
            // Ship 43: the side spikes, the nose, the engine pods, the round turrets' wings.
            new Kind { art = "enemy_boss3", name = "OVERSEER-X9", scale = 4.6f, pad = 9f, color = TcFx.Orange, score = 120000, ports = new[]
                { new Vector2(-16f, 13f), new Vector2(16f, 13f), new Vector2(0f, 24f), new Vector2(-8f, -3f), new Vector2(8f, -3f), new Vector2(-28f, -11f), new Vector2(28f, -11f) } },
        };

        public static TcBoss MakeBoss(Vector3 at, int health, TMP_FontAsset font, int level = 0)
        {
            var kind = Kinds[Mathf.Clamp(level, 0, Kinds.Length - 1)];
            var go = new GameObject("BOSS");
            go.transform.SetParent(TcWorld.Root, false);
            go.transform.position = new Vector3(at.x, at.y, TcWorld.Origin.z);
            go.tag = "ENEMY";
            var boss = go.AddComponent<TcBoss>();
            boss.Setup(kind.art, health, kind.scale, kind.color, kind.score);
            boss.Voice = "boss";
            boss.Ports = kind.ports;
            boss.BarName = kind.name;
            boss.TopPadding = kind.pad;
            boss.Level = Mathf.Clamp(level, 0, Kinds.Length - 1);
            boss.GemChance = 1f;
            boss.DropsPowerUp = true;
            boss.BuildBar(font);
            Current = boss;
            return boss;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Current == this) Current = null;
        }

        // The bar, at two art pixels to one of the picture's (BossBar art is 100 px a unit).
        const float BarWidth = 88f, BarHeight = 8f, FillHeight = 4f, FillInset = 6f, FillTop = 1f, FillMin = 7f;
        const float ArtScale = 2f / TcArt.PixelsPerUnit / 0.01f;
        const float BarSize = 1.15f;   // (its bar and name 15% bigger: easier to read)
        bool _barShown = true;   // (on with the boss: it comes in with it)

        void BuildBar(TMP_FontAsset font)
        {
            _bar = new GameObject("BOSS BAR").transform;
            _bar.SetParent(TcWorld.Root, false);
            _bar.localScale = Vector3.one * ArtScale * BarSize;
            _back = Piece("Back", "BossBarBackground", 0);
            _trail = Piece("Trail", "BossBarFollowFill", 1);
            _trail.enabled = false;   // (never shown: only the coloured fill, going down - UpdateBar)
            // Its fill in its own neon (the light strip, tinted), glowing round it.
            _fill = Piece("Fill", "BossBarFollowFill", 2);
            _fill.color = Palette[0];
            _frame = Piece("Frame", "BossBarForeground", 5);
            _back.size = _frame.size = new Vector2(BarWidth, BarHeight) * 0.01f;
            // Its inner bloom: a brighter band inside the fill and a hot line through its middle.
            _glowFar = Glow("Bloom", 903, 0f);
            _glowNear = Glow("Core", 904, 0f);
            if (font)
            {
                var label = new GameObject("NAME");
                label.layer = TcArt.ActorsLayer;
                label.transform.SetParent(_bar, false);
                _label = label.AddComponent<TextMeshPro>();
                _label.font = font;
                _label.text = BarName;
                _label.fontSize = 0.45f;
                _label.textWrappingMode = TextWrappingModes.NoWrap;
                _label.color = Color.Lerp(Color.white, Palette[0], 0.45f);   // (its name a touch of its colour)
                _label.sortingOrder = 908;
                _label.alignment = TextAlignmentOptions.Bottom;
                // As wide as the bar, centred over it (its size set so it spans it).
                float across = BarWidth * 0.01f;
                float wide = _label.GetPreferredValues(BarName).x;
                if (wide > 0f) _label.fontSize *= across / wide;
                _label.rectTransform.sizeDelta = new Vector2(across, _label.GetPreferredValues(BarName).y);
                _label.rectTransform.pivot = new Vector2(0.5f, 0f);
                _label.rectTransform.localPosition = new Vector3(0f, (BarHeight * 0.5f + 1f) * 0.01f, 0f);
            }
            PlaceBar();
        }

        SpriteRenderer _glowNear, _glowFar;

        // A strip of light inside the moving fill (sized to it each frame: GlowFill).
        SpriteRenderer Glow(string name, int order, float pad)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_bar, false);
            var sr = TcArt.Renderer(go, order);
            sr.sprite = TcArt.Pixel;
            return sr;
        }

        // Inside the fill: `inset` art pixels in from its ends, `height` tall, through its middle.
        void GlowFill(SpriteRenderer glow, float inset, float height)
        {
            var size = _fill.size / 0.01f;
            glow.transform.localPosition = _fill.transform.localPosition;
            glow.transform.localScale = new Vector3(Mathf.Max(0f, size.x + inset * 2f), height, 1f) * 0.01f;
        }

        SpriteRenderer Piece(string name, string art, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_bar, false);
            var sr = TcArt.Renderer(go, 900 + order);
            sr.sprite = Resources.Load<Sprite>("BossBar/" + art);
            sr.drawMode = SpriteDrawMode.Sliced;
            return sr;
        }

        void PlaceBar()
        {
            if (!_bar) return;
            // Its drawn top (its frame has TopPadding clear rows over it, as drawn), and the bar just over that, riding on it.
            float top = _r.bounds.max.y - TopPadding / TcArt.PixelsPerUnit * transform.localScale.y;
            float y = top + 0.28f * BarSize;
            _bar.position = new Vector3(transform.position.x, y, TcWorld.Origin.z - 0.05f);
            float frac = Mathf.Clamp01((float)Health / MaxHealth);
            if (_trailFrac < frac) _trailFrac = frac;
            else if (Time.time - _hitAt > 0.35f) _trailFrac = Mathf.MoveTowards(_trailFrac, frac, 0.5f * Time.deltaTime);
            float inside = BarWidth - 2f * FillInset;
            SetFill(_fill, frac, inside);
            _trail.enabled = false;   // (no white trail of the health just lost)
            if (!_barShown)
            {
                _back.enabled = _trail.enabled = _fill.enabled = _frame.enabled = _glowNear.enabled = _glowFar.enabled = false;
                if (_label) _label.enabled = false;
            }
            else
            {
                if (!_back.enabled)
                {
                    _back.enabled = _frame.enabled = _glowNear.enabled = _glowFar.enabled = true;
                    if (_label) _label.enabled = true;
                }
                // The glow breathes (and flares when it's hit).
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 4f) + (Time.time - _hitAt < 0.1f ? 0.4f : 0f);
                // (Turned right up: the band fills most of the bar, its core near white.)
                _glowFar.color = TcFx.WithAlpha(Color.Lerp(Palette[0], Color.white, 0.6f), Mathf.Min(1f, 1.2f * pulse));
                _glowNear.color = TcFx.WithAlpha(Color.Lerp(Palette[0], Color.white, 0.95f), 1f);
                GlowFill(_glowFar, -0.5f, 3f);
                GlowFill(_glowNear, -1.5f, 1.5f);
                _glowNear.enabled = _glowFar.enabled = _fill.enabled;
            }
        }

        void SetFill(SpriteRenderer fill, float frac, float inside)
        {
            fill.enabled = frac > 0.001f;
            float w = Mathf.Max(FillMin, Mathf.Round(inside * Mathf.Clamp01(frac)));
            fill.size = new Vector2(w, FillHeight) * 0.01f;
            float left = -BarWidth * 0.5f + FillInset;
            fill.transform.localPosition = new Vector3((left + w * 0.5f) * 0.01f, (BarHeight * 0.5f - FillTop - FillHeight * 0.5f) * 0.01f, 0f);
        }

        protected override bool Hittable => !_exploding && transform.position.y < TcWorld.Field.yMax - 0.5f;

        // Every DropEvery of its health gone, a power-up falls from it (a
        // long fight: plenty of them).
        const float DropEvery = 0.09f;
        float _nextDrop = 1f - DropEvery;

        public override void Hurt(int damage)
        {
            if (_exploding) return;
            _hitAt = Time.time;
            if (Health - damage <= 0) { Health = 0; StartCoroutine(GoingDown()); return; }
            base.Hurt(damage);
            while (Health <= _nextDrop * MaxHealth && _nextDrop > 0f)
            {
                _nextDrop -= DropEvery;
                TcPowerUp.Drop(transform.position + Vector3.down * 0.8f + Vector3.right * Random.Range(-1f, 1f), TcPowerUp.Any);
            }
        }

        public override void Kill()
        {
            // A bomb only dents it.
            Hurt(Mathf.Max(1, MaxHealth / 20));
        }

        protected override void Update()
        {
            base.Update();
            PlaceBar();
        }

        IEnumerator GoingDown()
        {
            _exploding = true;
            // Every bullet on screen turns to coins, in its colour.
            TcDanmaku.ToCoins(null);
            var dir = TcStageDirector.Instance;
            // Going down: small blasts of its neon pixels all over it (each its
            // own bang), coins spurting out every few.
            string[] bangs = { "boss_chain", "heavy_death", "gunship_death", "bomber_death", "carrier_death" };
            for (int i = 0; i < 22; i++)
            {
                // Somewhere on it as it is now, and riding along with it as it moves.
                var b = _r.bounds;
                var at = new Vector3(Random.Range(b.min.x + 0.3f, b.max.x - 0.3f), Random.Range(b.min.y + 0.4f, b.max.y - 0.4f), transform.position.z);
                TcFx.Explode(at, "explosion_small", 0.8f, Palette[0], 10, 22f, transform);
                TcFx.NeonNova(at, Random.Range(0.5f, 1.1f), Palette, flash: false);
                TcSfx.Play(bangs[Random.Range(0, bangs.Length)], 0.8f, 0.08f);
                if (i % 3 == 0) TcCoin.Burst(at, 6, 1.5f);
                if (i % 5 == 4 && dir) dir.DropGem(at, Level);
                transform.position += (Vector3)(Random.insideUnitCircle * 0.06f);
                yield return new WaitForSeconds(0.12f);
            }
            // The end: the big nova of its neon pixels, a deep extra boom, gems everywhere.
            TcFx.NeonNova(transform.position, 4f, Palette);
            TcSfx.Play("player_death", 0.9f, 0.1f);
            for (int i = 0; i < 15; i++) if (dir) dir.DropGem(transform.position + (Vector3)(Random.insideUnitCircle * 2.2f), Level);
            if (_bar) Destroy(_bar.gameObject);
            TcPowerUp.Drop(transform.position + Vector3.left * 0.8f, TcPowerUp.Any);
            base.Die();   // (its boss_death bang, score, gems and a power-up)
        }

        // Its going-down colours: green neon for the first, blue the second, pink the third.
        Color[] Palette => Level == 1 ? TcFx.BlueNeon : Level == 2 ? TcFx.PinkNeon : TcFx.GreenNeon;

        protected override void Die()
        {
            if (!_exploding) { SpatialEmulator.Haptics.Rumble(0.8f, 0.3f, 1.2f); StartCoroutine(GoingDown()); }   // (the boss going down)
            else { SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Success); base.Die(); }
        }

        void OnDestroy()
        {
            if (_bar) Destroy(_bar.gameObject);
        }
    }
}
