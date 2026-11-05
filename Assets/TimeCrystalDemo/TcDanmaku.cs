using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TimeCrystal
{
    public enum OrbColor { Purple, Blue, Red, Orange, Green, Yellow }

    public enum BulletKind
    {
        Orb,       // round, animated (Super Pixel Projectiles' small blob)
        BigOrb,    // the large blob: slow and heavy
        Needle,    // the small energy bolt, pointed along its flight
        Kunai,     // a long thin blade, pointed
        Lightning, // the boss's crackling orb
        Fireball,  // the boss's: a fireball, pointed
        Bolt,      // the boss's: a ball of electricity
        Missile,   // the boss's: a magic missile, pointed
    }

    // ARcade: the enemies' bullets, in the manner of DoDonPachi: lots of
    // them, slow enough to weave through, bright - animated, from the
    // projectile packs, each leaving a short pixel trail of its colour
    // (TcFx) - with a small hit circle. They can curve
    // and speed up as they go (Fire's turn and accel). Pooled: one that
    // leaves the picture, hits the ship or is cleared is switched off and
    // used again. Angles are degrees from straight down, clockwise as seen
    // (positive = toward the right).
    public static class TcDanmaku
    {
        /// Hit within this radius (world units): small, as in CAVE's games.
        public const float HitRadius = 0.08f;
        /// Drawn at this share of their art's pixels (the hit circle stays HitRadius).
        public const float Scale = 0.65f;
        /// Every enemy bullet one colour, whatever a pattern asks for.
        public const OrbColor OneColor = OrbColor.Purple;

        static readonly List<TcBullet> s_all = new List<TcBullet>();
        static int s_cursor;
        static Transform s_player, s_root;

        public static void Reset()
        {
            s_all.Clear();
            s_cursor = 0;
            s_player = null;
        }

        public static Vector2 Dir(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), -Mathf.Cos(r));
        }

        public static Transform Player
        {
            get
            {
                if (!s_player && TcWorld.Root)
                {
                    var ship = TcWorld.Root.GetComponentInChildren<RetroShmupPlayerController>();
                    if (ship) s_player = ship.transform;
                }
                return s_player;
            }
        }

        /// The angle from `from` to the player's ship (straight down if there's none).
        public static float AimAt(Vector3 from)
        {
            var p = Player;
            if (!p) return 0f;
            Vector2 d = p.position - from;
            return Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg;
        }

        public static Color Neon(OrbColor c) => c switch
        {
            OrbColor.Purple => TcFx.Pink,
            OrbColor.Blue => TcFx.Cyan,
            OrbColor.Red => TcFx.Red,
            OrbColor.Orange => TcFx.Orange,
            OrbColor.Green => TcFx.Green,
            _ => TcFx.Yellow,
        };

        static string ColorName(OrbColor c) => c switch
        {
            OrbColor.Purple => "violet",
            OrbColor.Blue => "blue",
            OrbColor.Red => "red",
            OrbColor.Orange => "orange",
            OrbColor.Green => "green",
            _ => "yellow",
        };

        static string Strip(BulletKind kind, OrbColor color) => kind switch
        {
            BulletKind.BigOrb => "pj1_blob_large_" + ColorName(color),
            BulletKind.Needle => "pj1_energy_small_" + ColorName(color),
            BulletKind.Kunai => "pj2_meteor_small_" + ColorName(color),   // (a meteor now: the kunai looked like a bird)
            BulletKind.Lightning => "pj2_lightning_orb_small_" + ColorName(color),
            BulletKind.Fireball => "pj2_fireball_small_" + ColorName(color),
            BulletKind.Bolt => "pj1_electricity_large_" + ColorName(color),
            BulletKind.Missile => "pj1_magic_missile_large_" + ColorName(color),
            _ => "pj1_blob_small_" + ColorName(color),
        };

        /// One bullet. `turn`: degrees a second it curves (clockwise +);
        /// `accel`: units a second it gains (or loses, down to a crawl).
        /// Only into the lower half (down, toward the player) unless
        /// `anyWay` (an aimed shot goes wherever the player is): rings and
        /// spirals are half rings and half spirals, pointing down.
        /// The enemy whose pattern is running (TcEnemyGun): its shot sound plays.
        public static TcEnemy Shooter;

        public static void Fire(Vector3 from, float degrees, float speed, OrbColor color, BulletKind kind = BulletKind.Orb, float turn = 0f, float accel = 0f, bool anyWay = false)
        {
            if (!TcWorld.Root) return;
            if (!anyWay && Mathf.Abs(Mathf.DeltaAngle(0f, degrees)) > 90f) return;
            // The boss's bullets keep their own colours and their art's full
            // size (they're its weapons); everyone else's are one colour, small.
            bool boss = Shooter is TcBoss;
            if (Shooter) Shooter.PlayShot(kind);
            if (s_root != TcWorld.Root) { s_all.Clear(); s_cursor = 0; s_root = TcWorld.Root; }
            if (!boss) color = OneColor;
            var bullet = Get();
            bullet.Owner = Shooter;
            bullet.Launch(from, degrees, speed, kind, Strip(kind, color), Neon(color), turn, accel, boss ? 1f : Scale);
        }

        /// `n` bullets fanned over `spread` degrees around `center`.
        public static void NWay(Vector3 from, float center, int n, float spread, float speed, OrbColor color, BulletKind kind = BulletKind.Orb, float turn = 0f, float accel = 0f)
        {
            for (int i = 0; i < n; i++)
                Fire(from, n == 1 ? center : center - spread * 0.5f + spread * i / (n - 1), speed, color, kind, turn, accel, anyWay: true);
        }

        /// `n` bullets all the way round.
        public static void Ring(Vector3 from, int n, float offset, float speed, OrbColor color, BulletKind kind = BulletKind.Orb, float turn = 0f, float accel = 0f)
        {
            for (int i = 0; i < n; i++) Fire(from, offset + 360f * i / n, speed, color, kind, turn, accel);
        }

        /// Every bullet gone (a bomb, the boss going down), each with a little sparkle.
        /// Its bullets in the air turn to coins (TcCoin) - `owner`'s, or all of
        /// them if it's null (a boss going down) - in `strip`'s colour.
        public static void ToCoins(TcEnemy owner, string strip = "coin_gold")
        {
            foreach (var b in s_all)
                if (b && b.gameObject.activeSelf && (owner == null || b.Owner == owner))
                {
                    TcCoin.Spawn(b.transform.position, strip);
                    b.gameObject.SetActive(false);
                }
        }

        /// The bullets where `inside` says (a bomb's reach as it goes), gone with a sparkle.
        public static void ClearWhere(System.Func<Vector2, bool> inside)
        {
            foreach (var b in s_all)
                if (b && b.gameObject.activeSelf && inside(b.transform.position))
                {
                    TcFx.Burst(b.transform.position, b.Color, 2, 1.2f, 0.06f, 0.3f);
                    b.gameObject.SetActive(false);
                }
        }

        public static void ClearAll(bool sparkle)
        {
            foreach (var b in s_all)
                if (b && b.gameObject.activeSelf)
                {
                    if (sparkle) TcFx.Burst(b.transform.position, b.Color, 2, 1.2f, 0.06f, 0.3f);
                    b.gameObject.SetActive(false);
                }
        }

        public static int Live
        {
            get
            {
                int n = 0;
                foreach (var b in s_all) if (b && b.gameObject.activeSelf) n++;
                return n;
            }
        }

        static TcBullet Get()
        {
            int n = s_all.Count;
            for (int i = 0; i < n; i++)
            {
                int k = (s_cursor + i) % n;
                var b = s_all[k];
                if (b && !b.gameObject.activeSelf) { s_cursor = k + 1; return b; }
            }
            s_all.RemoveAll(b => !b);

            var go = new GameObject("BULLET");
            go.SetActive(false);
            go.transform.SetParent(TcWorld.Root, false);
            go.tag = "ENEMY-BULLET";
            var core = TcArt.Renderer(go, 52);
            core.color = new Color(1f, 1f, 1f, 0.7f);   // (see-through: the ships and the action show under them)
            go.layer = LayerMask.NameToLayer("TcEnemyBullet");
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            var anim = go.AddComponent<TcAnim>();
            var bullet = go.AddComponent<TcBullet>();
            bullet.Init(core, body, col, anim);
            s_all.Add(bullet);
            return bullet;
        }
    }

    public class TcBullet : MonoBehaviour
    {
        Rigidbody2D _body;
        SpriteRenderer _core;
        CircleCollider2D _collider;
        TcAnim _anim;
        float _angle, _speed, _turn, _accel, _trailClock, _age;
        bool _pointed;
        public Color Color { get; private set; }
        /// Who fired it (its bullets turn to coins when it goes down: TcDanmaku.ToCoins).
        public TcEnemy Owner;

        public void Init(SpriteRenderer core, Rigidbody2D body, CircleCollider2D col, TcAnim anim)
        {
            _core = core; _body = body; _collider = col; _anim = anim;
        }

        public void Launch(Vector3 from, float degrees, float speed, BulletKind kind, string strip, Color color, float turn, float accel, float scale)
        {
            _angle = degrees;
            _speed = speed;
            _turn = turn;
            _accel = accel;
            _age = 0f;
            Color = color;
            _pointed = kind == BulletKind.Needle || kind == BulletKind.Kunai || kind == BulletKind.Fireball || kind == BulletKind.Missile;
            _anim.Play(TcArt.Frames(strip), kind == BulletKind.Lightning ? 16f : 14f);
            _anim.Scatter();
            float hit = TcDanmaku.HitRadius * kind switch
            {
                BulletKind.BigOrb => 2.2f,
                BulletKind.Bolt => 1.6f,
                BulletKind.Lightning or BulletKind.Missile or BulletKind.Fireball => 1.3f,
                _ => 1f,
            };
            transform.localScale = Vector3.one * scale;
            _collider.radius = hit / scale;
            transform.position = new Vector3(from.x, from.y, TcWorld.Origin.z);
            Face();
            gameObject.SetActive(true);
            _body.linearVelocity = TcDanmaku.Dir(_angle) * _speed;
        }

        void Face()
        {
            // Pointed ones along their flight (the art points right); round ones turn slowly.
            float deg = _pointed ? _angle - 90f : _age * 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, deg);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            // A curve bends it for its first moments only; then it flies
            // straight on, so it always leaves the picture (never circles).
            if (_age > CurveSeconds) _turn = 0f;
            if (_turn != 0f || _accel != 0f)
            {
                _angle -= _turn * dt;
                _speed = Mathf.Max(0.4f, _speed + _accel * dt);
                _body.linearVelocity = TcDanmaku.Dir(_angle) * _speed;
            }
            Face();
            _trailClock -= dt;
            if (_trailClock <= 0f)
            {
                _trailClock = 0.045f;
                Vector2 back = -TcDanmaku.Dir(_angle);
                TcFx.Trail(transform.position + (Vector3)(back * 0.07f), Color, 0.05f, back * 0.3f, 0.16f);
            }
            var f = TcWorld.Field;
            var p = transform.position;
            if (p.y < f.yMin - 0.5f || p.y > f.yMax + 1f || p.x < f.xMin - 0.5f || p.x > f.xMax + 0.5f || _age > MaxSeconds)
                gameObject.SetActive(false);
        }

        const float CurveSeconds = 1f, MaxSeconds = 8f;
    }

    /// Flies an enemy through `Points` (world positions) at `Speed`,
    /// stopping `Waits[i]` seconds at each; past the last it keeps going the
    /// way it was heading until it leaves the picture (TcOffField).
    public class TcEnemyPath : MonoBehaviour
    {
        public Vector3[] Points;
        public float[] Waits;
        public float Speed = 2f;
        /// While holding, a gentle sway side to side (units).
        public float Sway;
        int _next;
        float _wait, _swayT;
        Vector3 _heading = Vector3.down, _holdAt;

        public bool Holding => _wait > 0f;

        void Update()
        {
            if (Points == null) return;
            float step = Speed * Time.deltaTime;
            if (_wait > 0f)
            {
                _wait -= Time.deltaTime;
                if (Sway > 0f)
                {
                    _swayT += Time.deltaTime;
                    transform.position = _holdAt + Vector3.right * Mathf.Sin(_swayT * 0.9f) * Sway;
                }
                return;
            }
            if (_next >= Points.Length) { transform.position += _heading * step; return; }
            var target = Points[_next];
            target.z = transform.position.z;
            var to = target - transform.position;
            if (to.magnitude <= step)
            {
                transform.position = target;
                _holdAt = target;
                _swayT = 0f;
                if (to.sqrMagnitude > 1e-6f) _heading = to.normalized;
                if (Waits != null && _next < Waits.Length) _wait = Waits[_next];
                _next++;
            }
            else
            {
                _heading = to.normalized;
                transform.position += _heading * step;
            }
        }
    }

    /// Runs an enemy's firing pattern (a coroutine from TcStageDirector); it
    /// stops when the enemy goes.
    public class TcEnemyGun : MonoBehaviour
    {
        public void Run(IEnumerator pattern) => StartCoroutine(Voiced(pattern, GetComponent<TcEnemy>()));

        // Runs the pattern with its enemy as TcDanmaku.Shooter while each
        // step runs (its bullets play its shot sound), nested patterns too.
        static IEnumerator Voiced(IEnumerator pattern, TcEnemy self)
        {
            while (true)
            {
                bool more;
                TcDanmaku.Shooter = self;
                try { more = pattern.MoveNext(); }
                finally { TcDanmaku.Shooter = null; }
                if (!more) yield break;
                if (pattern.Current is IEnumerator inner) yield return Voiced(inner, self);
                else yield return pattern.Current;
            }
        }

        /// In the picture (with a little margin): only then does it fire.
        public bool OnScreen
        {
            get
            {
                var f = TcWorld.Field;
                var p = transform.position;
                return p.x > f.xMin + 0.2f && p.x < f.xMax - 0.2f && p.y > f.yMin + 1f && p.y < f.yMax - 0.3f;
            }
        }
    }
}
