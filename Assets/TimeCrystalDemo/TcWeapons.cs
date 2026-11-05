using System.Collections.Generic;
using UnityEngine;

namespace TimeCrystal
{
    public enum TcWeapon { Missile, Swarm, BigToon, Homing, Rock, Fire, Meteor, Ice, Laser, Seeker, Rockets }

    // ARcade: the player's guns. The base shot is Super Pixel Projectiles'
    // small sci-fi missile; a pickup (TcPowerUp) swaps it for one of seven
    // others for PowerUpSeconds, then it's back to the missile. Each has its
    // own projectile, rate, number of shots, speed and damage; every shot
    // leaves a pixel trail and a muzzle flash, and bursts where it lands.
    public static class TcWeapons
    {
        public const float PowerUpSeconds = 10f;

        public class Spec
        {
            public string Name, Strip;
            public float Fps = 14f, Rate, Speed, Size = 1f;
            public int Damage = 1;
            public bool Pierce, Homing, Spin;
            public Color Color;
            /// Each volley: (x offset from the ship's middle, angle off straight up in degrees).
            public (float x, float angle)[] Volley;
            /// Random spread added to each shot's angle (degrees).
            public float Scatter;

            float _across = -1f, _along;
            /// Its projectile's size (world units): across its flight, and along it.
            public float Across { get { Measure(); return _across; } }
            public float Along { get { Measure(); return _along; } }
            void Measure()
            {
                if (_across >= 0f) return;
                var f = TcArt.Frame(Strip);
                // (The art points right: its height is across the flight.)
                _along = f ? f.rect.width / TcArt.PixelsPerUnit * Size : 0.3f;
                _across = f ? f.rect.height / TcArt.PixelsPerUnit * Size : 0.2f;
                if (Spin) _along = _across = Mathf.Max(_along, _across);
            }

            /// Never two shots on top of each other: side by side, a gap
            /// between them (the volley's order kept, its middle on the
            /// nose); one after another, a gap between each and the next
            /// even with the ship flying forward at full speed.
            public const float Gap = 0.12f;
            public float SideBySide(int i)
            {
                int n = Volley.Length;
                if (n < 2) return 0f;
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var (x, _) in Volley) { lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x); }
                // As far apart as it was laid out, or its width and a gap if that's further.
                float step = Mathf.Max(Across + Gap, (hi - lo) / (n - 1));
                return (i - (n - 1) * 0.5f) * step;
            }
            public float RateFor(float shipSpeed) => Mathf.Max(Rate, (Along + Gap) / Mathf.Max(1f, Speed - shipSpeed));
        }

        public static readonly Dictionary<TcWeapon, Spec> Specs = new Dictionary<TcWeapon, Spec>
        {
            // Two streams from the wings, steady (the power-ups fire faster).
            [TcWeapon.Missile] = new Spec { Name = "MISSILE", Strip = "pj1_scifi_missile_small_orange", Rate = 0.17f, Speed = 15f, Damage = 1, Color = TcFx.Orange,
                Volley = new[] { (-0.2f, 0f), (0.2f, 0f) } },
            // More shots: a five-way fan.
            [TcWeapon.Swarm] = new Spec { Name = "SWARM", Strip = "pj1_toon_missile_small_violet", Rate = 0.09f, Speed = 14f, Damage = 1, Color = TcFx.Violet,
                Volley = new[] { (0f, 0f), (-0.12f, -9f), (0.12f, 9f), (-0.22f, -18f), (0.22f, 18f) } },
            // Big, slow-firing, hard-hitting.
            [TcWeapon.BigToon] = new Spec { Name = "BIG TOON", Strip = "pj1_toon_missile_large_red", Rate = 0.15f, Speed = 13f, Damage = 3, Color = TcFx.Red, Size = 0.9f,
                Volley = new[] { (0f, 0f), (-0.32f, -4f), (0.32f, 4f) } },
            // Missiles that steer to the nearest enemy.
            [TcWeapon.Homing] = new Spec { Name = "HOMING", Strip = "pj1_scifi_missile_large_green", Rate = 0.13f, Speed = 11f, Damage = 2, Homing = true, Color = TcFx.Green,
                Volley = new[] { (-0.3f, -35f), (0.3f, 35f), (-0.15f, -12f), (0.15f, 12f) } },
            // A wide random scatter of rocks.
            [TcWeapon.Rock] = new Spec { Name = "ROCK", Strip = "pj1_rock_small_blue", Rate = 0.11f, Speed = 13f, Damage = 1, Spin = true, Color = TcFx.Cyan,
                Volley = new[] { (0f, 0f), (-0.1f, -12f), (0.1f, 12f), (-0.2f, -24f), (0.2f, 24f), (-0.25f, -34f), (0.25f, 34f) } },
            // Rapid fire: a double stream, fast.
            [TcWeapon.Fire] = new Spec { Name = "FIRE", Strip = "pj1_fireball_small_orange", Rate = 0.045f, Speed = 18f, Damage = 1, Color = TcFx.Yellow, Size = 0.9f,
                Volley = new[] { (-0.1f, 0f), (0.1f, 0f) } },
            // Slow, huge damage, bursting on what it hits.
            [TcWeapon.Meteor] = new Spec { Name = "METEOR", Strip = "pj2_meteor_small_violet", Rate = 0.22f, Speed = 9f, Damage = 4, Color = TcFx.Pink, Size = 1.3f,
                Volley = new[] { (0f, 0f) } },
            // The fastest needles, three ways.
            [TcWeapon.Ice] = new Spec { Name = "ICE", Strip = "pj2_ice_spike_small_blue", Rate = 0.05f, Speed = 24f, Damage = 1, Color = TcFx.Cyan,
                Volley = new[] { (0f, 0f), (-0.15f, -7f), (0.15f, 7f) } },
        };

        // ---- The other pilots' starting guns (TcPilots) ----
        static TcWeapons()
        {
            // BLAZE: one fast red laser stream.
            Specs[TcWeapon.Laser] = new Spec { Name = "RAPID LASER", Strip = "pj1_laser_small_red", Rate = 0.085f, Speed = 22f, Damage = 1, Color = TcFx.Red,
                Volley = new[] { (0f, 0f) } };
            // VIPER: three small magic missiles that steer to the nearest enemy.
            Specs[TcWeapon.Seeker] = new Spec { Name = "SEEKER BOLTS", Strip = "pj1_magic_missile_small_green", Rate = 0.28f, Speed = 12f, Damage = 1, Homing = true, Color = TcFx.Green,
                Volley = new[] { (0f, 0f), (-0.15f, -14f), (0.15f, 14f) } };
            // RIO: two big slow rockets that hit twice as hard.
            Specs[TcWeapon.Rockets] = new Spec { Name = "HEAVY ROCKETS", Strip = "pj2_scifi_missile_small_orange", Rate = 0.32f, Speed = 12f, Damage = 2, Color = TcFx.Orange, Size = 1.2f,
                Volley = new[] { (-0.25f, 0f), (0.25f, 0f) } };
        }

        /// The power-ups an enemy can drop (not the base missile).
        public static readonly TcWeapon[] PowerUps =
            { TcWeapon.Swarm, TcWeapon.BigToon, TcWeapon.Homing, TcWeapon.Rock, TcWeapon.Fire, TcWeapon.Ice };   // (no METEOR, the comet: Wayne dropped it)
    }

    /// A player's shot: pooled, animated, trailing pixels; hits an enemy for
    /// its damage (TcEnemy / TcBoss call Land), bursting.
    public class TcShot : MonoBehaviour
    {
        static readonly List<TcShot> s_pool = new List<TcShot>();
        static Transform s_root;

        public int Damage;
        public bool Pierce;
        public Color Color;
        TcWeapons.Spec _spec;
        Rigidbody2D _body;
        SpriteRenderer _r;
        BoxCollider2D _box;
        TcAnim _anim;
        Vector2 _velocity;
        float _trailClock, _age;
        readonly HashSet<Object> _pierced = new HashSet<Object>();

        public static void Fire(TcWeapons.Spec spec, Vector3 from, float angle)
        {
            var shot = Get();
            shot.Launch(spec, from, angle);
        }

        static TcShot Get()
        {
            if (s_root != TcWorld.Root) { s_pool.Clear(); s_root = TcWorld.Root; }
            foreach (var s in s_pool) if (s && !s.gameObject.activeSelf) return s;
            var go = new GameObject("SHOT");
            go.SetActive(false);
            go.transform.SetParent(TcWorld.Root, false);
            go.tag = "BULLET";
            var shot = go.AddComponent<TcShot>();
            shot._r = TcArt.Renderer(go, 30);
            shot._r.color = new Color(1f, 1f, 1f, 0.85f);   // (a little see-through)
            go.layer = LayerMask.NameToLayer("TcBullet");
            shot._body = go.AddComponent<Rigidbody2D>();
            shot._body.gravityScale = 0f;
            shot._body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            shot._box = go.AddComponent<BoxCollider2D>();
            shot._box.isTrigger = true;
            shot._anim = go.AddComponent<TcAnim>();
            s_pool.Add(shot);
            return shot;
        }

        void Launch(TcWeapons.Spec spec, Vector3 from, float angle)
        {
            _spec = spec;
            Damage = spec.Damage;
            Pierce = spec.Pierce;
            Color = spec.Color;
            _pierced.Clear();
            _age = 0f;
            var frames = TcArt.Frames(spec.Strip);
            _anim.Play(frames, spec.Fps, loop: true);
            _anim.Scatter();
            transform.localScale = Vector3.one * spec.Size;
            if (frames != null && frames.Length > 0)
            {
                var b = frames[0].bounds.size;
                // The art points right; its hit box is most of its length, its body's width.
                _box.size = new Vector2(b.x * 0.8f, b.y * 0.6f);
            }
            float rad = (90f - angle) * Mathf.Deg2Rad;
            _velocity = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * spec.Speed;
            transform.position = new Vector3(from.x, from.y, TcWorld.Origin.z);
            Face();
            gameObject.SetActive(true);
            _body.linearVelocity = _velocity;
        }

        void Face()
        {
            float deg = _spec != null && _spec.Spin ? _age * 720f : Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, deg);
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_spec != null && _spec.Homing && _age > 0.08f) Steer();
            Face();

            // The trail: pixels off its tail.
            _trailClock -= Time.deltaTime;
            if (_trailClock <= 0f)
            {
                _trailClock = 0.025f;
                Vector2 back = -_velocity.normalized;
                var tail = transform.position + (Vector3)(back * 0.2f * transform.localScale.x);
                TcFx.Trail(tail + (Vector3)(Random.insideUnitCircle * 0.03f), Color.Lerp(Color, Color.white, 0.3f), Random.Range(0.05f, 0.09f), back * 0.6f);
            }

            var f = TcWorld.Field;
            var p = transform.position;
            if (p.y > f.yMax + 0.5f || p.y < f.yMin - 0.5f || p.x < f.xMin - 0.5f || p.x > f.xMax + 0.5f)
                gameObject.SetActive(false);
        }

        void Steer()
        {
            var target = TcEnemy.Nearest(transform.position, 7f);
            if (!target) return;
            Vector2 to = (Vector2)(target.position - transform.position);
            float want = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            float now = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
            float turn = Mathf.MoveTowardsAngle(now, want, 400f * Time.deltaTime) * Mathf.Deg2Rad;
            _velocity = new Vector2(Mathf.Cos(turn), Mathf.Sin(turn)) * _spec.Speed;
            _body.linearVelocity = _velocity;
        }

        /// It's hit `what`: true if it may do damage (a piercing shot hits each thing once).
        public bool Land(Object what, Vector3 at)
        {
            if (!gameObject.activeSelf) return false;
            if (Pierce)
            {
                if (!_pierced.Add(what)) return false;
                TcFx.Burst(at, Color, 6, 2.5f, 0.06f, 0.25f);
                return true;
            }
            TcFx.Burst(at, Color, 7, 2.5f, 0.06f, 0.22f, new Vector2(0f, -1f));
            gameObject.SetActive(false);
            return true;
        }
    }

    /// The player's ship (added by TimeCrystalGame): the pilot's jet's
    /// look and animations - thrust, banking as it moves sideways, a barrel
    /// roll on a pickup - and its guns (TcWeapons):
    /// the missile, or a power-up for a while, a bar under the ship counting it down.
    public class TcPlayerShip : MonoBehaviour
    {
        RetroShmupPlayerController _ship;
        SpriteRenderer _body;
        TcAnim _anim;
        AudioSource _gunSound;
        SpriteRenderer _timerBack, _timerFill;

        public TcWeapon Weapon { get; private set; } = TcWeapon.Missile;
        /// Its pilot's own gun (TcPilots): what it fires between power-ups.
        static TcWeapon BaseWeapon => TcPilots.Current.Weapon;
        float _weaponLeft, _fireClock, _rollUntil;
        bool _rolling;
        int _bank;   // -1, 0, 1

        public static TcPlayerShip Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            _ship = GetComponent<RetroShmupPlayerController>();
            _body = GetComponent<SpriteRenderer>();
            _gunSound = _ship ? _ship.shootAudioJ : null;
            transform.localScale = Vector3.one;
            var animator = GetComponent<Animator>();
            if (animator) animator.enabled = false;
            _anim = gameObject.AddComponent<TcAnim>();
            Weapon = BaseWeapon;
            _anim.Play(TcArt.Frames(TcPilots.Current.Ship + "_idle"), 16f);
            _body.sortingOrder = 20;

            // Its own thrust is in its frames: the old jets go.
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("JetNitro")) child.gameObject.SetActive(false);
                // (The jam build's two side pods go too: they flickered and distracted.)
                if (child.name.StartsWith("SUPPORT UNIT")) child.gameObject.SetActive(false);
            }
            BuildTimer();
            gameObject.AddComponent<TcLaser>();
            gameObject.AddComponent<TcJets>();
            gameObject.AddComponent<TcHitMarker>();
            gameObject.AddComponent<TcPlayerTop>();
            gameObject.AddComponent<TcDamageFlash>();
            gameObject.AddComponent<TcWarpTrail>();
        }

        // Its barrel roll's frames, rolling to the right (NOVA's strip is drawn rolling left: played backwards).
        static Sprite[] RollFrames()
        {
            return TcArt.Frames(TcPilots.Current.Ship + "_roll");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// A new pilot chosen (Choose Your Fighter): their jet and their gun, now.
        public void ApplyPilot()
        {
            Weapon = BaseWeapon;
            _weaponLeft = 0f;
            _bank = 0;
            if (GetComponent<TcLaser>() is { } laser) laser.Rebuild();
            if (GetComponent<TcJets>() is { } jets) jets.Rebuild();
            _anim.Play(TcArt.Frames(TcPilots.Current.Ship + "_idle"), 16f, restart: true);
        }

        void BuildTimer()
        {
            _timerBack = Bar("POWER TIMER", 21, new Color(0f, 0f, 0f, 0.6f));
            _timerFill = Bar("POWER FILL", 22, Color.white);
            _timerBack.transform.localScale = new Vector3(0.84f, 0.075f, 1f);
            ShowTimer(false);
        }

        SpriteRenderer Bar(string name, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, -0.62f, 0f);
            var sr = TcArt.Renderer(go, order);
            sr.sprite = TcArt.Pixel;
            sr.color = color;
            return sr;
        }

        void ShowTimer(bool on)
        {
            _timerBack.enabled = _timerFill.enabled = on;
        }

        public void PowerUp(TcWeapon weapon)
        {
            Weapon = weapon;
            _weaponLeft = TcWeapons.PowerUpSeconds;
            _rollUntil = Time.time + 0.5f;
            _anim.Play(RollFrames(), 16f, loop: false);
            var spec = TcWeapons.Specs[weapon];
            TcFx.Burst(transform.position, spec.Color, 24, 4f, 0.08f, 0.5f);
            if (TcStageDirector.Instance) TcStageDirector.Instance.Announce(spec.Name, spec.Color, size: 2f);   // (twice the size: Wayne's)
        }

        /// Back to its own gun now (the laser cancels a power-up: TcLaser).
        public void EndPowerUp()
        {
            Weapon = BaseWeapon;
            _weaponLeft = 0f;
        }

        TcLaser _laser;

        void Update()
        {
            if (!_ship) return;
            if (!_laser) _laser = GetComponent<TcLaser>();
            bool alive = _ship.CanAct;
            Animate();

            // The power-up running down; back to its own gun when it's out.
            if (Weapon != BaseWeapon)
            {
                _weaponLeft -= Time.deltaTime;
                if (_weaponLeft <= 0f) Weapon = BaseWeapon;
            }
            ShowTimer(Weapon != BaseWeapon && alive);
            if (Weapon != BaseWeapon)
            {
                float frac = Mathf.Clamp01(_weaponLeft / TcWeapons.PowerUpSeconds);
                float w = 0.8f * frac;
                _timerFill.transform.localScale = new Vector3(w, 0.04f, 1f);
                _timerFill.transform.localPosition = new Vector3(-0.4f + w * 0.5f, -0.62f, 0f);
                var c = TcWeapons.Specs[Weapon].Color;
                // The last two seconds it blinks.
                _timerFill.color = _weaponLeft < 2f && Mathf.Repeat(_weaponLeft * 6f, 1f) < 0.5f ? Color.white : c;
            }

            // (Not while the laser's on.)
            if (!alive || !TcWorld.Fire || (_laser && _laser.Firing)) { _fireClock = Mathf.Max(0f, _fireClock - Time.deltaTime); return; }
            var spec = TcWeapons.Specs[Weapon];
            _fireClock -= Time.deltaTime;
            if (_fireClock <= 0f)
            {
                _fireClock += spec.RateFor(_ship.ArcadeSpeed);
                if (_fireClock < 0f) _fireClock = 0f;
                Volley(spec);
            }
        }

        void Volley(TcWeapons.Spec spec)
        {
            var nose = transform.position + Vector3.up * 0.38f;
            // Left to right by angle, each its own width and a gap apart.
            var order = (System.ValueTuple<float, float>[])spec.Volley.Clone();
            System.Array.Sort(order, (a, b) => a.Item2 != b.Item2 ? a.Item2.CompareTo(b.Item2) : a.Item1.CompareTo(b.Item1));
            for (int i = 0; i < order.Length; i++)
            {
                var at = nose + Vector3.right * spec.SideBySide(i);
                TcShot.Fire(spec, at, order[i].Item2);
            }
            // A few sparks at the nose for the volley.
            TcFx.Burst(nose, spec.Color, 3, 2f, 0.05f, 0.12f, new Vector2(0f, 2f));
            if (_gunSound && _gunSound.clip) TcWorld.PlayShot(_gunSound, 0.07f);
        }

        /// Mid barrel roll (its underside up: the hit marker's hidden).
        public bool Rolling => Time.time < _rollUntil;

        /// Barrel rolls, `times` of them back to back (the warp between levels).
        public void BarrelRolls(int times)
        {
            const float one = 8f / 16f;   // (8 frames at 16 a second)
            _rollUntil = Time.time + one * times;
            _rolling = true;
            _anim.Play(RollFrames(), 16f, loop: true);
        }

        void Animate()
        {
            if (Time.time < _rollUntil) return;
            // The rolls done: back to flying level (or banking) whether the stick's moved or not.
            if (_rolling) { _rolling = false; _bank = int.MinValue; }
            int bank = TcWorld.Move.x < -0.2f ? -1 : TcWorld.Move.x > 0.2f ? 1 : 0;
            if (bank == _bank && !_anim.Done) return;
            _bank = bank;
            // ship_bank_a has its left wing tucked: turning left.
            string ship = TcPilots.Current.Ship;
            _anim.Play(TcArt.Frames(ship + (bank < 0 ? "_bank_a" : bank > 0 ? "_bank_b" : "_idle")), 16f);
        }
    }

    /// A power-up: a soap bubble (TcBubbleArt), its film drifting, the weapon's projectile
    /// turning inside it; it drifts down; the player's ship takes it.
    public class TcPowerUp : MonoBehaviour
    {
        TcWeapon _weapon;
        SpriteRenderer _inside, _bubble;
        float _t;
        bool _taken;

        /// A power-up at random.
        public static TcWeapon Any => TcWeapons.PowerUps[Random.Range(0, TcWeapons.PowerUps.Length)];

        public static void Drop(Vector3 at, TcWeapon weapon)
        {
            if (!TcWorld.Root) return;
            TcSfx.Play("powerup_drop", 0.7f, 0.15f);
            var go = new GameObject("POWER-UP " + weapon);
            go.transform.SetParent(TcWorld.Root, false);
            go.transform.position = new Vector3(at.x, at.y, TcWorld.Origin.z);
            var p = go.AddComponent<TcPowerUp>();
            p._weapon = weapon;
            var spec = TcWeapons.Specs[weapon];

            p._bubble = TcArt.Renderer(go, 10);
            var bubble = go.AddComponent<TcAnim>();
            bubble.Play(TcBubbleArt.Frames, 6f);   // (a soap bubble, its film drifting round: TcBubbleArt)
            go.transform.localScale = Vector3.one * 0.6f;

            // The weapon's projectile in the middle of its bubble, drawn over
            // everything on the ships' layer so it stands out (the same depth
            // as its bubble, so it stays centred in it in AR).
            var inside = new GameObject("INSIDE");
            inside.transform.SetParent(go.transform, false);
            p._inside = TcArt.Renderer(inside, 500);
            inside.AddComponent<TcAnim>().Play(TcArt.Frames(spec.Strip), spec.Fps);
            inside.transform.localScale = Vector3.one * 1.5f;

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.55f;
            go.layer = TcArt.ActorsLayer;
            go.AddComponent<CollectibleMover>().moveSpeed = 1.1f;
            go.AddComponent<TcOffField>();
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_inside) _inside.transform.localRotation = Quaternion.Euler(0f, 0f, 90f + 25f * Mathf.Sin(_t * 3f));
            transform.position += Vector3.right * Mathf.Sin(_t * 2.2f) * 0.6f * Time.deltaTime;
            if (Mathf.Repeat(_t, 0.12f) < Time.deltaTime)
                TcFx.Trail(transform.position + (Vector3)(Random.insideUnitCircle * 0.35f), TcWeapons.Specs[_weapon].Color, 0.07f, Vector2.up * 0.4f, 0.4f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_taken || !other.CompareTag("PLAYER")) return;
            var ship = other.GetComponent<TcPlayerShip>();
            if (!ship) return;
            _taken = true;
            ship.PowerUp(_weapon);
            if (TcStageDirector.Instance) TcStageDirector.Instance.PlayPowerUp();
            Destroy(gameObject);
        }
    }

    /// ARcade: the ship's hit circle shown (as in CAVE's games): a glowing pixel crystal, faceted
    /// and sharp-pointed, in the pilot's laser colour, as wide as the circle, pulsing - where a
    /// bullet has to reach to hit. Over the ship; gone while it can't be hit (down, respawning).
    public class TcHitMarker : MonoBehaviour
    {
        const float Pixel = 1f / TcArt.PixelsPerUnit * 2f;   // (the art's pixels, at the jets' 2x)
        RetroShmupPlayerController _ship;
        CircleCollider2D _hit;
        Transform _root;
        SpriteRenderer _core, _ring, _glow, _inner;
        int _pilot = -1;
        /// On Choose Your Fighter's big jet (TcFighterSelect): the player ship's hit circle, at that jet's size, always shown.
        public bool preview;
        public int orderBase;
        /// A preview's own pilot (the story's four ships), -1: the one flying.
        public int pilotOf = -1;
        int PilotIndex => pilotOf >= 0 ? pilotOf : TcPilots.Index;

        /// A marker on the fighter-select jet (its sorting `order` and up) - or one pilot's (`pilot`).
        public static void OnPreview(GameObject jet, int order, int pilot = -1)
        {
            var m = jet.AddComponent<TcHitMarker>();
            m.preview = true;
            m.orderBase = order;
            m.pilotOf = pilot;
        }

        void Start()
        {
            var owner = preview ? (TcPlayerShip.Instance ? TcPlayerShip.Instance.gameObject : null) : gameObject;
            _ship = owner ? owner.GetComponent<RetroShmupPlayerController>() : null;
            _hit = owner ? owner.GetComponent<CircleCollider2D>() : null;
            _root = new GameObject("HIT MARKER").transform;
            _root.SetParent(transform, false);
            _root.gameObject.layer = TcArt.ActorsLayer;
            int o = preview ? orderBase : 0;
            _glow = Part("GLOW", Halo(), o + 23);
            _inner = Part("INNER GLOW", Halo(), o + 24);
            _ring = Part("RING", null, o + 25);
            _core = Part("GLINT", Glint(), o + 26);
        }

        SpriteRenderer Part(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var sr = TcArt.Renderer(go, order);
            sr.sprite = sprite;
            return sr;
        }

        // A cut crystal as wide as the hit circle (half again as tall), in the pilot's colour: a four-pointed gem
        // with four straight sides (a rhombus) - each point two flat facets -
        // lit from the top left, see-through like glass (its outline more solid), with a white glint
        // over its heart (Glint).
        const float GemAlpha = 0.72f;
        const float GlintEvery = 3.5f;
        const float Size = 0.97f;        // (the gem a touch under the hit circle's width)   // (seconds between its glint's flares)
        Sprite Diamond(float radius, Color color)
        {
            // (Drawn at the picture's own pixels - half the ships' art pixels - so the facets read at this size.)
            const float Fine = Pixel * 0.5f;
            int w = Mathf.Max(11, Mathf.RoundToInt(radius * 2f / Fine) | 1);   // (odd: a middle pixel)
            int h = (Mathf.RoundToInt(w * 1.5f) | 1);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            color = Vivid(color);
            Color lightest = Color.Lerp(color, Color.white, 0.6f), light = Color.Lerp(color, Color.white, 0.3f), mid = color,
                  midDark = Color.Lerp(color, Color.black, 0.22f), dark = Color.Lerp(color, Color.black, 0.45f),
                  outline = Color.Lerp(color, Color.black, 0.7f);
            const float Shoulder = 0.5f;   // (where the facets meet the edge, along the diagonal: 0.5 - straight sides, a true rhombus)
            // Which facet (x, y) is in: -1 outside; else 0-7 (the quadrant times two, plus 1 on the half towards the
            // up/down point).
            int Facet(float x, float y)
            {
                float u = (x - cx) / (cx + 0.5f), v = (y - cy) / (cy + 0.5f);
                float au = Mathf.Abs(u), av = Mathf.Abs(v);
                int q = (u < 0 ? 0 : 1) + (v > 0 ? 0 : 2);   // (0 top left, 1 top right, 2 bottom left, 3 bottom right)
                if (av <= au && av <= Shoulder * (1f - au) / (1f - Shoulder)) return q * 2;
                if (au < av && au <= Shoulder * (1f - av) / (1f - Shoulder)) return q * 2 + 1;
                return -1;
            }
            // Lit from the top left: [facet] - top left's two lightest, bottom right's darkest.
            var tone = new[] { light, lightest, mid, light, midDark, mid, dark, midDark };
            // The table: a smaller rhombus set into its middle, flat and lighter, its edge cut sharp (a bright line
            // on its lit top left, a dark one on its bottom right) - the outer facets the bevel round it.
            const float Inset = 0.52f;
            float M(float x, float y) => Mathf.Abs(x - cx) / (cx + 0.5f) + Mathf.Abs(y - cy) / (cy + 0.5f);
            Color shine = new Color(1f, 1f, 1f, 1f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int fct = Facet(x, y);
                    Color c = Color.clear;
                    if (fct >= 0)
                    {
                        c = tone[fct]; c.a = GemAlpha;
                        bool upLeft = (x - cx) / (cx + 0.5f) - (y - cy) / (cy + 0.5f) < 0f;   // (the lit half)
                        if (M(x, y) < Inset)
                        {
                            c = upLeft ? mid : midDark; c.a = GemAlpha;
                            // The table's edge.
                            if (M(x - 1, y) >= Inset || M(x + 1, y) >= Inset || M(x, y - 1) >= Inset || M(x, y + 1) >= Inset)
                            { c = upLeft ? Color.white : outline; c.a = upLeft ? 1f : 0.9f; }
                        }
                        // The outline: a pixel in from the edge.
                        bool edge = Facet(x - 1, y) < 0 || Facet(x + 1, y) < 0 || Facet(x, y - 1) < 0 || Facet(x, y + 1) < 0;
                        if (edge) { c = outline; c.a = 0.95f; }
                        // The shine: the light caught white along the top-left edge (just inside the outline, its
                        // middle stretch), and a short streak across the table's top-left.
                        else if (x < cx && y > cy && M(x, y) > 0.8f && (Facet(x - 2, y) < 0 || Facet(x, y + 2) < 0))
                        {
                            float along = (cx - x) / (cx + 0.5f);   // (0 at the top point, 1 at the left one)
                            if (along > 0.2f && along < 0.75f) c = shine;
                        }
                        float tu = (x - cx) / (cx + 0.5f), tv = (y - cy) / (cy + 0.5f);
                        if (!edge && Mathf.Abs((tv - tu - 0.36f) * (cy + 0.5f)) < 0.75f && tu < -0.05f && tu > -0.3f) c = shine;
                    }
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f / Fine);
        }

        // The glint: a white four-pointed star of thin pixel arms fading out from a bright heart (it flares now and
        // then: LateUpdate).
        static Sprite s_glint;
        static Sprite Glint()
        {
            if (s_glint) return s_glint;
            const int n = 15; const float c = 7f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Abs(x - c), dy = Mathf.Abs(y - c), a = 0f;
                    if (dy == 0f) a = 1f - dx / (c + 1f);
                    if (dx == 0f) a = Mathf.Max(a, 1f - dy / (c + 1f));
                    if (dx == 1f && dy == 1f) a = 0.55f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a > 0f ? Mathf.Sqrt(a) : 0f));
                }
            tex.Apply();
            s_glint = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return s_glint;
        }

        // A pilot's colour, brighter and richer: full brightness, a third more saturated.
        static Color Vivid(Color c)
        {
            Color.RGBToHSV(c, out var hue, out var sat, out _);
            return Color.HSVToRGB(hue, Mathf.Min(1f, sat * 1.35f), 1f);
        }

        static Sprite s_halo;
        static Sprite Halo()
        {
            if (s_halo) return s_halo;
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                    float a = Mathf.Clamp01(1f - d); a *= a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            s_halo = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return s_halo;
        }

        void LateUpdate()
        {
            if (!_root || !_hit) return;
            var player = TcPlayerShip.Instance;
            // Hidden while it can't be hit, on Choose Your Fighter (the big jet shows it), and mid
            // barrel roll (its underside up).
            bool show = preview || (_ship && _ship.CanAct && !TcFighterSelect.Active && !TcStory.Active && !(player && player.Rolling));
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;
            var owner = _hit.transform;
            float k = preview ? transform.lossyScale.x / Mathf.Max(0.01f, owner.lossyScale.x) : 1f;   // (the big jet's size)
            float radius = _hit.radius * owner.lossyScale.x * k;
            if (_pilot != PilotIndex)
            {
                _pilot = PilotIndex;
                _ring.sprite = Diamond(radius, TcPilots.All[_pilot].Color);
            }
            // At the hit circle's middle, flat to the picture (whatever the ship's doing).
            _root.position = transform.position + owner.TransformVector(_hit.offset) * k;
            _root.rotation = Quaternion.identity;
            _root.localScale = new Vector3(1f / transform.lossyScale.x, 1f / transform.lossyScale.y, 1f);
            // Pulsing: the gem brightening into the bloom and back, its glow swelling with it.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            var b = Mathf.Lerp(1.1f, 1.65f, pulse);
            _ring.color = new Color(b, b, b, 1f);
            _ring.transform.localScale = Vector3.one * Size;
            // Its glow in its own colour, swelling and brightening with it.
            // (a wide soft one, and a tight bright one hugging the gem - over-bright, for the ships' bloom)
            var own = Vivid(TcPilots.All[_pilot].Color);
            _glow.transform.localScale = Vector3.one * radius * (3.4f + 1f * pulse);
            _glow.color = TcFx.WithAlpha(own, 0.45f + 0.25f * pulse);
            _inner.transform.localScale = Vector3.one * radius * (2.2f + 0.4f * pulse);
            _inner.color = new Color(own.r * 1.3f, own.g * 1.3f, own.b * 1.3f, 0.5f + 0.3f * pulse);
            // The glint: a small star on its heart, flaring big and bright and turning a little every so often.
            float g = Mathf.Repeat(Time.time, GlintEvery) / 0.35f, flare = g < 1f ? Mathf.Sin(g * Mathf.PI) : 0f;
            _core.transform.localScale = Vector3.one * radius * (0.45f + 1.5f * flare);
            _core.transform.localRotation = Quaternion.Euler(0f, 0f, 45f * flare * flare);
            float gb = 1f + 0.8f * flare;
            _core.color = new Color(gb, gb, gb, 0.75f + 0.25f * flare);
        }
    }
}
