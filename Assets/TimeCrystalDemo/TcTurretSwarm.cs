using System.Collections.Generic;
using UnityEngine;

namespace TimeCrystal
{
    // RIO's bomb: a turret barrage. Five combat drones (SovDigital's Combat Drone, Resources/TcDrone: 12 frames
    // x 8 directions, rows S SE E NE N NW W SW) fly out to an arc round him - W, NW, N, NE, E - facing the player
    // (their red-eyed fronts), then lock on: each on the nearest enemy (one no other drone has, if it can),
    // spinning round a turn and on to whichever of its eight directions points at it, then firing minigun
    // rounds (pj1_laser_small_orange) from its two gun pods in turn (its recoiling attack animation), a
    // stream that keeps on for a moment after the target's gone while it finds the next. Once nothing's
    // left on screen they're gone in a burst - unless a boss is there: they keep on at it till time's up
    // (it only loses a share of its health: TcBombFx.TurretBarrage). A thin red lock-on laser from each gun
    // pod to its target while it's on it; a servo whir as it spins round searching, a two-tone beep as it
    // locks on (Resources/TcSfx turret_*: unity-helpers/turret_sfx.py). Driven by Step
    // (TcBombFx.TurretBarrage; the Editor's previews call it too).
    public class TcTurretSwarm : MonoBehaviour
    {
        const float Radius = 1.7f;                 // (the arc round RIO)
        const float FlyOut = 0.25f, FireFrom = 0.3f, Vanish = 0.2f;
        const float BossTime = 2.6f;              // (with only a boss left: this long, then off)
        public const float Longest = 10f;         // (up at the most, whatever's left)
        const float ShotsPerSecond = 36f, BulletSpeed = 22f, Spread = 4f;
        const float KeepFiring = 0.35f;           // (on along its last aim this long after its target goes)
        const float AtLeast = 0.8f;               // (firing this long at the least, even if all's gone at once)
        const string BulletArt = "pj1_laser_small_orange";   // (a slug with a flash behind it, RIO's orange)
        const float BulletFps = 14f;
        static readonly Vector3 BulletScale = new Vector3(1.4f, 0.85f, 1f);   // (drawn out along its way: a tracer)
        const float TurnStep = 0.035f;            // (seconds per 45-degree step as it spins round)
        public const float HitRadius = 0.45f;     // (a tracer this near an enemy hits it: TcBombFx)
        const float FramePixelsPerUnit = 162f;    // (the 148 x 128 frames: a drone about two thirds of a ship)
        /// The Editor's previews: stand-in targets.
        public static List<Transform> PreviewTargets;

        // The arc, left to right: W, NW, N, NE, E (degrees).
        static readonly float[] ArcAngles = { 180f, 135f, 90f, 45f, 0f };

        class Drone
        {
            public SpriteRenderer r;
            public Vector3 home;
            public int dir;            // (0 E, 1 NE, 2 N, 3 NW, 4 W, 5 SW, 6 S, 7 SE: 45-degree steps)
            public Transform target;
            public float shotClock, animClock, turnClock, lostAt = -9f;
            public Vector3 lastAim;
            public int pod, spin;
            public SpriteRenderer[] lasers;   // (one from each gun pod: the lock-on)
            public Transform lockedOn;
            public bool whirred;      // (spin: 45-degree steps still to go round before it settles on its target)
        }

        struct Bullet { public SpriteRenderer r; public Vector3 pos, vel; public float age; }

        readonly List<Drone> _drones = new List<Drone>();
        readonly List<Bullet> _bullets = new List<Bullet>();
        System.Action<Vector2> _hit;
        Vector3 _from;
        float _t, _until = float.MaxValue;
        static Sprite[] s_bullet;
        // The guns: a heavy machine gun's burst (freesound "Heavy Machine Gun 50 Caliber") when they open up, and
        // once more if they're still firing when its rattle's done - twice at the most. Hits synthesized
        // (unity-helpers/minigun_sfx.py).
        const string GunSound = "heavy_mg";
        const float GunRattle = 1f;   // (its burst; the rest is its echo)
        int _gunPlays, _hitSound;
        float _gunAgain;
        GunVoice _gun;

        // The guns' own voice (not TcSfx's shared one), so it can be faded: it dies away a moment after the last
        // round's fired - when the drones stop shooting or are gone.
        class GunVoice : MonoBehaviour
        {
            const float FadeAfter = 0.12f, FadeTime = 0.25f;
            public AudioSource source;
            public float lastShot;
            public static GunVoice Make()
            {
                var go = new GameObject("DRONE GUNS");
                go.transform.SetParent(TcWorld.Root, false);
                var v = go.AddComponent<GunVoice>();
                v.source = go.AddComponent<AudioSource>();
                v.source.playOnAwake = false;
                v.source.spatialBlend = 0f;
                SpatialEmulator.GameAudio.Fit(v.source);   // (the SFX volume)
                return v;
            }
            public void Play(AudioClip clip)
            {
                source.volume = 1f;
                source.PlayOneShot(clip, 1f);
            }
            void Update()
            {
                if (Time.time - lastShot < FadeAfter) return;
                source.volume = Mathf.MoveTowards(source.volume, 0f, Time.deltaTime / FadeTime);
                if (source.volume <= 0f) Destroy(gameObject);
            }
        }
        static Sprite[][] s_idle, s_attack;   // [sheet row][frame]

        public bool Done { get; private set; }

        public static TcTurretSwarm Start(Vector3 from, System.Action<Vector2> hit)
        {
            var go = new GameObject("RIO TURRETS");
            go.transform.SetParent(TcWorld.Root, false);
            var s = go.AddComponent<TcTurretSwarm>();
            s._from = from;
            s._hit = hit;
            var f = TcWorld.Field;
            foreach (var deg in ArcAngles)
            {
                var a = deg * Mathf.Deg2Rad;
                var home = from + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Radius;
                home.x = Mathf.Clamp(home.x, f.xMin + 0.5f, f.xMax - 0.5f);
                home.y = Mathf.Clamp(home.y, f.yMin + 0.5f, f.yMax - 0.5f);
                var rgo = new GameObject("RIO TURRET");
                rgo.transform.SetParent(go.transform, false);
                var r = TcArt.Renderer(rgo, 935);
                r.sharedMaterial = TcArt.SpriteMaterial;
                rgo.transform.position = from;
                var lasers = new SpriteRenderer[2];
                for (int k = 0; k < 2; k++)
                {
                    var lgo = new GameObject("LOCK-ON LASER");
                    lgo.transform.SetParent(go.transform, false);
                    lasers[k] = TcArt.Renderer(lgo, 933);
                    lasers[k].sprite = TcArt.Pixel;
                    lasers[k].enabled = false;
                }
                // Facing the player (S: its front) to start; one full turn round before it settles on its first target.
                s._drones.Add(new Drone { r = r, home = home, dir = 6, spin = 8, animClock = Random.Range(0f, 1f), lasers = lasers });
            }
            return s;
        }

        void Update() => Step(Time.deltaTime);

        public void Step(float dt)
        {
            if (Done) return;
            _t += dt;
            Load();
            foreach (var d in _drones)
            {
                if (!d.r) continue;
                // Out to its place on the arc, popping up to size; then shrinking away at the end.
                float k = Mathf.Clamp01(_t / FlyOut);
                float ease = 1f - (1f - k) * (1f - k);
                d.r.transform.position = Vector3.Lerp(_from, d.home, ease) + new Vector3(0f, 0f, TcWorld.Origin.z - _from.z);
                float size = Mathf.Lerp(0.4f, 1f, ease);
                if (_t > _until) size *= Mathf.Clamp01(1f - (_t - _until) / Vanish);
                d.r.transform.localScale = Vector3.one * size;

                bool firing = _t >= FireFrom && _t < _until;
                if (firing)
                {
                    if (!Alive(d.target))
                    {
                        if (d.target) d.lostAt = _t;
                        d.target = Nearest(d);
                    }
                    // Its target just gone: the stream keeps on along its last aim a moment.
                    if (!d.target && d.spin == 0 && _t - d.lostAt < KeepFiring && d.lastAim != Vector3.zero) Fire(d, d.lastAim, dt);
                    if (d.target)
                    {
                        var to = d.target.position - d.r.transform.position;
                        float ang = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
                        int want = Mathf.RoundToInt(Mathf.Repeat(ang, 360f) / 45f) & 7;
                        // Turning a direction at a time (the opening spin first, the way it'll end up going), firing once it's on.
                        d.turnClock += dt;
                        while (d.turnClock >= TurnStep && (d.spin > 0 || d.dir != want))
                        {
                            d.turnClock -= TurnStep;
                            int way = ((want - d.dir + 8) % 8) <= 4 ? 1 : -1;
                            if (d.spin > 0) { d.spin--; if (d.dir == want && d.spin == 0) break; }
                            d.dir = (d.dir + way + 8) % 8;
                        }
                        if (d.turnClock > TurnStep) d.turnClock = TurnStep;
                        if (!d.whirred && d.spin > 0) { d.whirred = true; TcSfx.Play("turret_servo", 0.55f, 0.06f); }   // (searching: spinning round)
                        if (d.spin == 0 && d.dir == want)
                        {
                            if (d.lockedOn != d.target) { d.lockedOn = d.target; TcSfx.Play("turret_lock", 0.25f, 0.12f); }
                            d.lastAim = to.normalized;
                            Fire(d, to, dt);
                        }
                    }
                }
                Lasers(d, firing && d.target && d.spin == 0 && d.lockedOn == d.target);
                // Its frames: the recoiling attack while it fires, idle otherwise, in the way it faces.
                d.animClock += dt;
                bool shooting = firing && d.spin == 0 && (d.target || _t - d.lostAt < KeepFiring);
                var set = shooting ? s_attack : s_idle;
                float fps = shooting ? 24f : 16f;
                if (set != null)
                {
                    var frames = set[Row(d.dir)];
                    d.r.sprite = frames[(int)(d.animClock * fps) % frames.Length];
                }
            }
            MoveBullets(dt);
            // Up till every ordinary enemy on screen's down (a short barrage at the least); with only a boss
            // left, BossTime and off (it only loses a share); never past Longest.
            if (_until == float.MaxValue && _t > FireFrom + AtLeast)
            {
                bool others = AnyLeft(false), boss = AnyLeft(true);
                if ((!others && (!boss || _t >= BossTime)) || _t >= Longest) _until = _t;
            }
            if (_t > _until + Vanish && _bullets.Count == 0)
            {
                foreach (var d in _drones) if (d.r) TcFx.Burst(d.r.transform.position, TcFx.Orange, 10, 4f, 0.06f, 0.35f);
                Done = true;
                Destroy(gameObject);
            }
        }

        // The lock-on: a thin red line from each gun pod's end to the target, flickering.
        void Lasers(Drone d, bool on)
        {
            if (d.lasers == null) return;
            for (int k = 0; k < 2; k++)
            {
                var l = d.lasers[k];
                if (!l) continue;
                l.enabled = on;
                if (!on) continue;
                var to = d.target.position - d.r.transform.position; to.z = 0f;
                var aim = to.normalized;
                var side = new Vector3(-aim.y, aim.x, 0f);
                float s = d.r.transform.localScale.x;
                var a = d.r.transform.position + aim * 0.24f * s + side * (k == 0 ? 0.11f : -0.11f) * s;
                var b = d.target.position; b.z = a.z;
                var span = b - a;
                float len = span.magnitude, px = TcArt.Pixel.bounds.size.x;
                l.transform.position = (a + b) * 0.5f;
                l.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
                l.transform.localScale = new Vector3(len / px, 0.022f / px, 1f);
                l.color = new Color(1f, 0.15f, 0.2f, Random.Range(0.45f, 0.85f));
            }
        }

        // The sheets' rows top to bottom: S, SE, E, NE, N, NW, W, SW.
        static int Row(int dir) => dir switch { 0 => 2, 1 => 3, 2 => 4, 3 => 5, 4 => 6, 5 => 7, 6 => 0, _ => 1 };

        void Fire(Drone d, Vector3 to, float dt)
        {
            d.shotClock += dt * ShotsPerSecond;
            while (d.shotClock >= 1f)
            {
                d.shotClock -= 1f;
                var aim = to.normalized;
                var side = new Vector3(-aim.y, aim.x, 0f);
                // From the end of one gun pod, then the other.
                d.pod ^= 1;
                var muzzle = d.r.transform.position + aim * 0.24f * d.r.transform.localScale.x + side * (d.pod == 0 ? 0.11f : -0.11f) * d.r.transform.localScale.x;
                float jitter = Random.Range(-Spread, Spread) * Mathf.Deg2Rad;
                var dir = new Vector3(aim.x * Mathf.Cos(jitter) - aim.y * Mathf.Sin(jitter), aim.x * Mathf.Sin(jitter) + aim.y * Mathf.Cos(jitter), 0f);
                Shoot(muzzle, dir);
                TcFx.Burst(muzzle, TcFx.Yellow, 2, 2f, 0.04f, 0.08f, (Vector2)dir * 3f);
                if (_gunPlays < 2 && _t >= _gunAgain && TcWorld.Root)
                {
                    _gunPlays++;
                    _gunAgain = _t + GunRattle;
                    if (!_gun) _gun = GunVoice.Make();
                    _gun.Play(TcSfx.Clip(GunSound));   // (the loudest of its sounds: the guns)
                }
                if (_gun) _gun.lastShot = Time.time;
            }
        }

        void Shoot(Vector3 at, Vector3 dir)
        {
            if (s_bullet == null || s_bullet.Length == 0 || !s_bullet[0]) s_bullet = TcArt.Frames(BulletArt);
            var go = new GameObject("RIO BOLT");
            go.transform.SetParent(transform, false);
            var r = TcArt.Renderer(go, 934);
            r.sprite = s_bullet != null && s_bullet.Length > 0 ? s_bullet[0] : TcArt.Pixel;
            go.transform.localScale = BulletScale;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);   // (the art points right)
            go.transform.position = at;
            _bullets.Add(new Bullet { r = r, pos = at, vel = dir * BulletSpeed });
        }

        void MoveBullets(float dt)
        {
            var f = TcWorld.Field;
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var b = _bullets[i];
                b.pos += b.vel * dt;
                bool gone = !f.Contains((Vector2)b.pos);
                if (!gone && HitsSomething(b.pos))
                {
                    _hit?.Invoke(b.pos);
                    TcFx.Burst(b.pos, TcFx.Orange, 4, 3f, 0.05f, 0.2f);
                    _hitSound ^= 1;
                    TcSfx.Play(_hitSound == 0 ? "round_hit_1" : "round_hit_2", 0.2f, 0.09f);   // (under the guns)
                    gone = true;
                }
                if (gone) { if (b.r) Destroy(b.r.gameObject); _bullets.RemoveAt(i); continue; }
                b.age += dt;
                if (b.r)
                {
                    b.r.transform.position = b.pos;
                    if (s_bullet != null && s_bullet.Length > 1) b.r.sprite = s_bullet[(int)(b.age * BulletFps) % s_bullet.Length];
                }
                _bullets[i] = b;
            }
        }

        IEnumerable<Transform> Targets()
        {
            if (PreviewTargets != null) { foreach (var t in PreviewTargets) yield return t; yield break; }
            foreach (var e in TcEnemy.All) if (e) yield return e.transform;
        }

        bool HitsSomething(Vector3 at)
        {
            foreach (var t in Targets())
                if (Alive(t) && ((Vector2)(t.position - at)).sqrMagnitude < HitRadius * HitRadius) return true;
            return false;
        }

        // Anything still up on screen: the bosses, or the rest.
        bool AnyLeft(bool bosses)
        {
            var f = TcWorld.Field;
            foreach (var t in Targets())
                if (Alive(t) && f.Contains((Vector2)t.position) && (t.GetComponent<TcBoss>() != null) == bosses) return true;
            return false;
        }

        static bool Alive(Transform t)
        {
            if (!t || !t.gameObject.activeInHierarchy) return false;
            var e = t.GetComponent<TcEnemy>();
            return !e || e.Health > 0;
        }

        // The nearest live enemy in the picture, one no other drone's on if there is one.
        Transform Nearest(Drone me)
        {
            var f = TcWorld.Field;
            Transform best = null, any = null;
            float d = float.MaxValue, dAny = float.MaxValue;
            foreach (var t in Targets())
            {
                if (!Alive(t) || !f.Contains((Vector2)t.position)) continue;
                float dist = ((Vector2)(t.position - me.r.transform.position)).sqrMagnitude;
                bool taken = false;
                foreach (var o in _drones) if (o != me && o.target == t) { taken = true; break; }
                if (!taken && dist < d) { d = dist; best = t; }
                if (dist < dAny) { dAny = dist; any = t; }
            }
            return best ? best : any;
        }

        // The two sheets cut into sprites once.
        /// The sheets cut and the rounds' frames looked up ahead, so pressing bomb doesn't hitch: TcBombFx.Prewarm.
        public static void Prewarm()
        {
            Load();
            if (s_bullet == null || s_bullet.Length == 0 || !s_bullet[0]) s_bullet = TcArt.Frames(BulletArt);
            TcSfx.Clip(GunSound);
        }

        static void Load()
        {
            if (s_idle == null) s_idle = Cut(Resources.Load<Texture2D>("TcDrone/idle"));
            if (s_attack == null) s_attack = Cut(Resources.Load<Texture2D>("TcDrone/attack"));
        }

        static Sprite[][] Cut(Texture2D sheet)
        {
            if (!sheet) return null;
            const int w = 148, h = 128, rows = 8;
            int cols = sheet.width / w;
            var all = new Sprite[rows][];
            for (int row = 0; row < rows; row++)
            {
                all[row] = new Sprite[cols];
                for (int c = 0; c < cols; c++)
                    all[row][c] = Sprite.Create(sheet, new Rect(c * w, sheet.height - (row + 1) * h, w, h), new Vector2(0.5f, 0.55f), FramePixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            return all;
        }
    }
}
