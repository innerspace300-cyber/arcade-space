using System.Collections.Generic;
using UnityEngine;

namespace TimeCrystal
{
    // BLAZE's bomb: twin Chinese dragons, red and gold (TcDragonArt). They fly up from below the picture to their
    // places either side of the ship and go where it goes (the player moves their heads; each body follows its
    // head and hangs behind it), heads facing forward, breathing a stream of fire at the nearest enemies,
    // spitting fireballs at them and spraying flame, all at once. Fire burns: an ordinary enemy loses health while it's in
    // it (small ones go at once, the big ones take longer); a boss only a share (TcBombFx.DragonFire). After StayUp seconds of
    // fire they fly off up the picture. Driven by Step (the
    // Editor's previews call it too).
    public class TcDragonPair : MonoBehaviour
    {
        const int Segments = 22;   // (a compact body: fewer plates, a shorter tail)
        const float Shrink = 0.75f * 0.7f * 0.7f * 0.7f;   // (all of a dragon smaller than first drawn: a quarter, then 30% three times)
        const float BodyWide = 1.15f * 1.35f;              // (the body's plates wider than they're drawn: a thick serpent)
        const float HeadSize = 1.45f * 1.45f * 0.75f;      // (the heads about 1.6 times the rest's scale)
        const float HeadTurn = 25f;
        // The neck: the body starts this far back under the head (its first plates tucked under the back of it), and
        // its first NeckPlates plates narrow towards the head so none sticks out past it.
        const float NeckGap = 0.75f * Shrink * HeadSize * 0.45f;
        const int NeckPlates = 3;                        // (how far a head pivots towards its target)
        const float Size = 1.5f * Shrink;
        const float BodySize = 1.5f;                    // (the body and its arms half again as big as the head's scale)
        const float Spacing = 0.19f * BodySize * Shrink;   // (the plates overlapping: one body, not beads)
        const float FlyIn = 0.9f;                          // (up from below the picture to their places)
        const float FireFrom = FlyIn, Leave = 1f;
        const float StayUp = 5f;                           // (seconds of fire before they fly off)
        public const float Longest = FlyIn + StayUp;
        const float Side = 1.1f, Ahead = 0.3f;              // (their places beside the ship)
        const float Turn = 60f;                           // (how far either side of straight up their fire aims)
        const float InnerGap = 0.3f;                      // (nothing of a dragon nearer the ship's middle than this)
        // More than one bomb out at once: each pair its own slot, further out and a little lower (a V), so they
        // never overlap; at the picture's sides they bunch up inside it, still SlotGap apart.
        const float SlotGap = 0.6f, SlotDrop = 0.25f, EdgeGap = 0.3f;
        static readonly List<TcDragonPair> s_out = new List<TcDragonPair>();
        int _slot;
        const float MouthAhead = 0.75f * Shrink * HeadSize;                   // (its mouth: just past its nose, the head's pivot at its eyes)
        const float FlameDps = 9f;                         // (one flame's burn a second; up to four at once over an enemy)
        const float ColumnDps = 30f, SprayDps = 10f, FireballDamage = 6f;
        const float ColumnHalfWidth = 0.3f, ColumnWiden = 0.075f;   // (half its width at the mouth; wider per unit up)
        const float SprayReach = 2.4f, SprayAngle = 38f;
        const float FlameTouch = 0.05f;                    // (a column flame's burn where it stops on an enemy)
        /// The Editor's previews: stand-in targets (no enemies there).
        public static List<Transform> PreviewTargets;
        /// Where the ship is (the previews set a fixed point).
        public static System.Func<Vector3> PreviewShip;

        class Dragon
        {
            public SpriteRenderer head;
            public SpriteRenderer[] body;
            public float[] bodyScale;
            public SpriteRenderer[] claws;
            public SpriteRenderer tuft;
            public Vector3[] seg;          // (positions: 0 the head, then back down the body)
            public float side;             // (-1 left, +1 right)
            public float nextFireball, sway;
            public Vector3 aim = Vector3.up;   // (where its head points: its fire goes that way)
            // Like the laser, its fire stops at the first enemy in its way: that enemy, how far along the aim it
            // stops, and the impact shown there until that enemy's dead.
            public Vector3 mouth;
            public Transform hit;
            public float stop = float.MaxValue;
            public GameObject impact;
        }

        class Flame { public SpriteRenderer r; public Vector3 pos, vel; public float age, life, grow, size; public bool fireball; public Dragon from; }

        readonly Dragon[] _dragons = new Dragon[2];
        readonly List<Flame> _flames = new List<Flame>();
        System.Action<Transform, float> _burn;
        float _t, _leaveAt = float.MaxValue;
        const float FireSize = 0.8f * 0.6f, FireAlpha = 0.7f;   // (the flames smaller, to suit the dragons, and 30% fainter than first made)
        // The fire, per look (TcDragonArt.Look): its three flame sprites, a tint over them, and the spray's and
        // bursts' two colours. Red-gold: orange, yellow, red. Neon green-gold: green and yellow. Neon blue-white: blue and white.
        static readonly string[][] FireArt =
        {
            new[] { "pj1_fireball_small_orange", "pj1_fireball_small_yellow", "pj1_fireball_small_red" },
            new[] { "pj1_fireball_small_green", "pj1_fireball_small_green", "pj1_fireball_small_yellow" },
            new[] { "pj1_fireball_small_blue", "pj1_fireball_small_blue", "pj1_fireball_small_yellow" },
        };
        static readonly Color[] FireTint = { Color.white, new Color(0.7f, 1f, 0.55f), new Color(0.82f, 0.93f, 1f) };
        static readonly Color[][] FireColors =
        {
            new[] { TcFx.Orange, TcFx.Yellow },
            new[] { new Color(0.35f, 1f, 0.25f), new Color(1f, 0.92f, 0.25f) },
            new[] { new Color(0.3f, 0.7f, 1f), new Color(0.92f, 0.97f, 1f) },
        };
        static readonly Sprite[][] s_fire = new Sprite[TcDragonArt.Looks][];
        static int s_nextLook;
        int _look;
        Sprite[] Fire(int look)
        {
            var f = s_fire[look];
            if (f == null || f.Length == 0 || !f[0]) f = s_fire[look] = new[] { First(FireArt[look][0]), First(FireArt[look][1]), First(FireArt[look][2]) };
            return f;
        }

        public bool Done { get; private set; }
        public bool Breathing => _t >= FireFrom && _t < _leaveAt;

        public static TcDragonPair Start(Vector3 from, System.Action<Transform, float> burn)
        {
            var go = new GameObject("BLAZE DRAGONS");
            go.transform.SetParent(TcWorld.Root, false);
            var s = go.AddComponent<TcDragonPair>();
            // The next look in turn: red-gold, neon green-gold, neon blue-white.
            s._look = s_nextLook;
            s_nextLook = (s_nextLook + 1) % TcDragonArt.Looks;
            TcDragonArt.Look = s._look;
            s._burn = burn;
            while (s_out.Exists(o => o && o._slot == s._slot)) s._slot++;
            s_out.Add(s);
            // In from below the picture, under their places either side of the ship.
            var f = TcWorld.Field;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                s._dragons[i] = s.Make(new Vector3(from.x + side * Side, f.yMin - 0.8f, from.z), side);
            }
            return s;
        }

        Dragon Make(Vector3 from, float side)
        {
            var d = new Dragon { side = side, seg = new Vector3[Segments + 1], body = new SpriteRenderer[Segments], bodyScale = new float[Segments], claws = new SpriteRenderer[4], sway = Random.Range(0f, 6f) };
            for (int i = 0; i <= Segments; i++) d.seg[i] = from + Vector3.down * Spacing * i;   // (all of it below the picture, trailing down)
            for (int i = Segments - 1; i >= 0; i--)
            {
                float k = i / (Segments - 1f);
                // Plates coral at the neck through orange to gold at the tail (TcDragonArt.Plate), shrinking to it.
                var r = Part("DRAGON BODY", TcDragonArt.Plate(k), 950 + (Segments - i));
                d.bodyScale[i] = Size * BodySize * Mathf.Lerp(1f, 0.4f, k);
                d.body[i] = r;
            }
            for (int k = 0; k < 4; k++) d.claws[k] = Part("DRAGON CLAW", TcDragonArt.Claw, 949);
            d.tuft = Part("DRAGON TAIL", TcDragonArt.Tuft, 949);
            d.head = Part("DRAGON HEAD", TcDragonArt.Head, 990);
            Lay(d);
            return d;
        }

        SpriteRenderer Part(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = TcArt.Renderer(go, order);
            r.sprite = sprite;
            return r;
        }

        Vector3 Ship()
        {
            if (PreviewShip != null) return PreviewShip();
            return TcPlayerShip.Instance ? TcPlayerShip.Instance.transform.position : transform.position;
        }

        void Update() => Step(Time.deltaTime);
        void OnDestroy() => s_out.Remove(this);

        public void Step(float dt)
        {
            if (Done) return;
            _t += dt;
            var ship = Ship();
            var f = TcWorld.Field;

            // Up for StayUp seconds of fire once they're in place, then off up the picture.
            if (_leaveAt == float.MaxValue && _t > FireFrom + StayUp) _leaveAt = _t;

            for (int i = 0; i < 2; i++)
            {
                var d = _dragons[i];
                Vector3 want;
                // Their places: either side of the ship (the player moves the heads; the bodies follow them).
                var place = ship + new Vector3(d.side * Side + Mathf.Sin(_t * 2.3f + d.sway) * 0.08f, Ahead + Mathf.Sin(_t * 3.1f + d.sway) * 0.06f, 0f);
                place += new Vector3(d.side * SlotGap * _slot, -SlotDrop * _slot, 0f);
                if ((place.x - ship.x) * d.side < InnerGap) place.x = ship.x + d.side * InnerGap;
                // Inside the picture, room left for the pairs further out than this one.
                int outside = 0;
                foreach (var o in s_out) if (o && o._slot > _slot) outside++;
                float edge = EdgeGap + SlotGap * outside;
                place.x = d.side < 0f ? Mathf.Max(place.x, f.xMin + edge) : Mathf.Min(place.x, f.xMax - edge);
                if (_t < FlyIn)
                {
                    // Up from below the picture to its place, weaving a little as it comes.
                    float k = Mathf.SmoothStep(0f, 1f, _t / FlyIn);
                    var start = new Vector3(place.x, f.yMin - 0.8f, place.z);
                    want = Vector3.Lerp(start, place, k) + Vector3.right * Mathf.Sin(_t * 9f) * 0.25f * (1f - k);
                }
                else if (_t < _leaveAt)
                {
                    want = place;
                    // Its fire aims at the nearest enemy (the head doesn't turn): up to Turn either side of straight up.
                    var target = Nearest(d.seg[0]);
                    var aimWant = Vector3.up;
                    if (target)
                    {
                        var to = target.position - (d.seg[0] + Vector3.up * MouthAhead); to.z = 0f;
                        if (to.sqrMagnitude > 1e-4f)
                        {
                            float ang = Mathf.Clamp(Vector3.SignedAngle(Vector3.up, to, Vector3.forward), -Turn, Turn) * Mathf.Deg2Rad;
                            aimWant = new Vector3(-Mathf.Sin(ang), Mathf.Cos(ang), 0f);
                        }
                    }
                    d.aim = Vector3.Slerp(d.aim, aimWant, Mathf.Clamp01(dt * 8f)).normalized;
                }
                else
                {
                    // Off up the picture, fast.
                    want = d.seg[0] + Vector3.up * 16f * dt;
                }
                // The head goes to its place (snappily, but not in one jump); the body follows as a chain.
                float follow = _t < FlyIn || _t >= _leaveAt ? 1f : Mathf.Clamp01(dt * 14f);
                d.seg[0] = Vector3.Lerp(d.seg[0], want, follow);
                Chain(d, dt, ship);
                Lay(d);
                d.mouth = d.seg[0] + HeadDir(d) * MouthAhead;
                Block(d);
                if (Breathing) Breathe(d, dt);
                ShowImpact(d, dt);
            }
            MoveFlames(dt);
            if (_t > _leaveAt + Leave && _flames.Count == 0 && _dragons[0].seg[Segments].y > f.yMax && _dragons[1].seg[Segments].y > f.yMax)
            {
                Done = true;
                Destroy(gameObject);
            }
        }

        // The body stays in line behind the head: straight down the picture from it, segment by segment, only a
        // gentle wave along it; when the head moves, each segment follows it across quickly (the further back,
        // a touch later) - so it never trails off to one side.
        void Chain(Dragon d, float dt, Vector3 ship)
        {
            for (int i = 1; i <= Segments; i++)
            {
                float k = i / (float)Segments;
                float wave = Mathf.Sin(_t * 4f - i * 0.5f + d.sway) * 0.12f * k;
                float x = Mathf.Lerp(d.seg[i].x, d.seg[0].x + wave, Mathf.Clamp01(dt * (18f - 8f * k)));
                d.seg[i] = new Vector3(x, d.seg[0].y - NeckGap - i * Spacing, d.seg[0].z);
            }
        }

        void Lay(Dragon d)
        {
            float z = TcWorld.Origin.z;
            d.head.transform.localScale = Vector3.one * Size * HeadSize;
            d.tuft.transform.localScale = Vector3.one * Size * BodySize * 0.7f;
            for (int i = 0; i < Segments; i++)
            {
                float neck = i < NeckPlates ? Mathf.Lerp(0.55f, 1f, i / (float)NeckPlates) : 1f;
                d.body[i].transform.localScale = new Vector3(BodyWide * neck, 1f, 1f) * d.bodyScale[i];
            }
            Place(d.head.transform, d.seg[0], HeadDir(d), z - 0.02f);   // (straight ahead, pivoting a little towards its target)
            for (int i = 0; i < Segments; i++)
                Place(d.body[i].transform, (d.seg[i] + d.seg[i + 1]) * 0.5f, d.seg[i] - d.seg[i + 1], z + 0.0004f * i);
            // Arms out of the body's sides at the front and back (their shoulders tucked under its edge): the upper arm
            // straight out, the forearm bent 45 degrees - forward on the front arms, back on the back ones (the art
            // bends towards its +x: mirrored where that's the wrong way).
            int[] at = { 4, 4, 12, 12 };
            for (int k = 0; k < 4; k++)
            {
                int i = at[k];
                var fwd = (d.seg[i] - d.seg[i + 1]).normalized;
                var side = new Vector3(-fwd.y, fwd.x, 0f) * (k % 2 == 0 ? 1f : -1f);
                float s = d.body[i].transform.localScale.y;
                var arm = d.claws[k].transform;
                Place(arm, d.seg[i] + side * 0.12f * s, side, z + 0.001f);
                var right = new Vector3(side.y, -side.x, 0f);   // (the art's +x, laid along `side`)
                bool front = k < 2;
                float mirror = (Vector3.Dot(right, fwd) > 0f) == front ? 1f : -1f;
                arm.localScale = new Vector3(mirror, 1f, 1f) * s * 1.05f;
            }
            var tail = (d.seg[Segments] - d.seg[Segments - 1]).normalized;
            Place(d.tuft.transform, d.seg[Segments], tail, z + 0.02f);
        }

        // The head's way: straight up the picture, pivoted towards its fire's aim - HeadTurn at the most.
        static Vector3 HeadDir(Dragon d)
        {
            float ang = Mathf.Clamp(Vector3.SignedAngle(Vector3.up, d.aim, Vector3.forward), -HeadTurn, HeadTurn) * Mathf.Deg2Rad;
            return new Vector3(-Mathf.Sin(ang), Mathf.Cos(ang), 0f);
        }

        static void Place(Transform t, Vector3 at, Vector3 forward, float z)
        {
            t.position = new Vector3(at.x, at.y, z);
            if (forward.sqrMagnitude > 1e-8f) t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - 90f);
        }

        // ---- Fire ----
        void Breathe(Dragon d, float dt)
        {
            var mouth = d.mouth;   // (the snout's tip: the head pivots a little; the fire aims fully)
            var across = new Vector3(-d.aim.y, d.aim.x, 0f);
            // The column: a thick rush of flames up the picture from the mouth, swelling as they go - orange and
            // yellow round a red heart.
            for (int k = 0; k < 3; k++)
                Spawn(mouth + across * Random.Range(-0.1f, 0.1f), d.aim * Random.Range(14f, 19f) + across * Random.Range(-0.4f, 0.4f), 1.5f * FireSize, 3.2f * FireSize, k == 2 ? 2 : Random.value < 0.5f ? 0 : 1, from: d);
            // The spray: a fan of small flame ahead.
            for (int k = 0; k < 2; k++)
            {
                float a = (Mathf.Atan2(d.aim.y, d.aim.x) * Mathf.Rad2Deg + Random.Range(-SprayAngle, SprayAngle)) * Mathf.Deg2Rad;
                float speed = Random.Range(5f, 7f);
                TcFx.Trail(mouth, TcFx.WithAlpha(FireColors[_look][Random.value < 0.5f ? 0 : 1], FireAlpha), Random.Range(0.06f, 0.11f) * FireSize, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, Mathf.Min(0.35f, (d.stop + 0.15f) / speed));
            }
            // Fireballs: one every so often at the nearest enemy.
            d.nextFireball -= dt;
            if (d.nextFireball <= 0f)
            {
                d.nextFireball = 0.28f;
                var target = Nearest(mouth);
                var aim = target ? (target.position - mouth).normalized : d.aim;
                aim.z = 0f;
                Spawn(mouth, aim * 12f, 2.2f * FireSize, 2.6f, 2, fireball: true);
            }
            // Burning in the spray's fan (only while it's breathing - the column's flames burn for themselves:
            // MoveFlames), and enemy bullets there gone - no further than where the fire stops.
            float reach = Mathf.Min(SprayReach, d.stop + 0.3f);
            foreach (var t in Targets())
            {
                if (!Alive(t)) continue;
                var p = t.position - mouth; p.z = 0f;
                if (p.magnitude < reach && Vector3.Angle(d.aim, p) < SprayAngle) _burn?.Invoke(t, SprayDps * dt);
            }
            // The enemy it's stopped on burns hardest (the column pours into it).
            if (d.hit) _burn?.Invoke(d.hit, FlameDps * 2f * dt);
            var sprayAim = (Vector2)d.aim; var m = (Vector2)mouth;
            TcDanmaku.ClearWhere(b => { var q = b - m; return q.magnitude < reach && Vector2.Angle(sprayAim, q) < SprayAngle; });
        }

        // The first enemy in its fire's way: along its aim from the mouth, within the column's reach of the line.
        // Only while it's breathing.
        void Block(Dragon d)
        {
            d.hit = null; d.stop = float.MaxValue;
            if (!Breathing) return;
            var f = TcWorld.Field;
            foreach (var t in Targets())
            {
                if (!Alive(t) || !f.Contains((Vector2)t.position)) continue;
                var p = t.position - d.mouth; p.z = 0f;
                float along = Vector3.Dot(p, d.aim);
                if (along < 0f) continue;
                float radius = Radius(t);
                if ((p - d.aim * along).magnitude > radius + ColumnHalfWidth) continue;
                float stop = Mathf.Max(0f, along - radius * 0.5f);
                if (stop < d.stop) { d.stop = stop; d.hit = t; }
            }
        }

        static float Radius(Transform t)
        {
            var c = t.GetComponent<CircleCollider2D>();
            if (c) return c.radius * Mathf.Abs(t.lossyScale.x);
            var b = t.GetComponent<Collider2D>();
            return b ? b.bounds.extents.x : 0.3f;
        }

        // The laser's impact, in the fire's colour, where the fire stops - until that enemy's dead.
        void ShowImpact(Dragon d, float dt)
        {
            bool on = d.hit;
            if (on && !d.impact)
            {
                d.impact = TcLaser.MakeImpact(transform, FireColors[_look][0]);
                d.impact.SetActive(false);
            }
            if (!d.impact) return;
            if (on != d.impact.activeSelf)
            {
                d.impact.SetActive(on);
                if (on) foreach (var ps in d.impact.GetComponentsInChildren<ParticleSystem>()) ps.Play();
            }
            if (!on) return;
            var at = d.mouth + d.aim * d.stop;
            d.impact.transform.position = new Vector3(at.x, at.y, TcWorld.Origin.z - 0.03f);
            d.impact.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.aim.y, d.aim.x) * Mathf.Rad2Deg - 90f);
            d.impact.transform.localScale = Vector3.one * (0.6f + 0.14f * Mathf.Sin(_t * 47f) + Random.Range(-0.08f, 0.08f));
            // Flame spitting back off it.
            if (Random.value < dt * 30f)
                TcFx.Burst(at, TcFx.WithAlpha(FireColors[_look][Random.value < 0.5f ? 0 : 1], 0.9f), 2, 3f, 0.05f, 0.25f, -(Vector2)d.aim * 1.5f);
        }

        void Spawn(Vector3 at, Vector3 vel, float size, float grow, int look, bool fireball = false, Dragon from = null)
        {
            var go = new GameObject(fireball ? "DRAGON FIREBALL" : "DRAGON FLAME");
            go.transform.SetParent(transform, false);
            var r = TcArt.Renderer(go, fireball ? 992 : 945);
            r.sprite = Fire(_look)[Mathf.Clamp(look, 0, 2)];
            r.color = FireTint[_look];
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg);   // (the art points right)
            go.transform.localScale = Vector3.one * size;
            _flames.Add(new Flame { r = r, pos = at, vel = vel, life = 1.2f, grow = grow, size = size, fireball = fireball, from = from });
        }

        /// Everything a bomb of theirs needs made ahead (the art drawn, the fire's frames looked up), so pressing
        /// bomb doesn't hitch: TcBombFx.Prewarm.
        public static void Prewarm()
        {
            int was = TcDragonArt.Look;
            for (int look = 0; look < TcDragonArt.Looks; look++)
            {
                TcDragonArt.Look = look;
                _ = TcDragonArt.Head; _ = TcDragonArt.Claw; _ = TcDragonArt.Tuft; _ = TcDragonArt.Plate(0f);
                var f = s_fire[look];
                if (f == null || f.Length == 0 || !f[0]) s_fire[look] = new[] { First(FireArt[look][0]), First(FireArt[look][1]), First(FireArt[look][2]) };
            }
            TcDragonArt.Look = was;
        }

        static Sprite First(string key) { var f = TcArt.Frames(key); return f != null && f.Length > 0 ? f[0] : TcArt.Pixel; }

        // Each flame does its own burning, where it is and as big as it's drawn: an enemy it's over burns, an
        // enemy bullet it touches is gone (like the laser). Nothing burns where no flame is - and once the
        // dragons have gone, only what's still flying (and visible) can.
        readonly List<Vector3> _hot = new List<Vector3>();   // (x, y: a flame's middle; z: its reach)

        void MoveFlames(float dt)
        {
            var f = TcWorld.Field;
            _hot.Clear();
            for (int i = _flames.Count - 1; i >= 0; i--)
            {
                var fl = _flames[i];
                bool fireball = fl.fireball;
                fl.age += dt;
                float age = fl.age;
                fl.pos += fl.vel * dt;
                bool gone = age > fl.life || !new Rect(f.xMin - 1f, f.yMin - 1f, f.width + 2f, f.height + 2f).Contains((Vector2)fl.pos);
                // The column's flames stop where its fire hits an enemy (like the laser): none goes past it.
                if (!gone && fl.from != null && fl.from.hit && Vector3.Dot(fl.pos - fl.from.mouth, fl.from.aim) > fl.from.stop) gone = true;
                if (!gone && !fireball && fl.age > 0.02f)
                    foreach (var t in Targets())
                        if (Alive(t) && ((Vector2)(t.position - fl.pos)).magnitude < Radius(t) * 0.8f) { _burn?.Invoke(t, FlameTouch); gone = true; break; }
                if (fireball && !gone)
                {
                    // A fireball bursts on the first enemy it reaches.
                    foreach (var t in Targets())
                        if (Alive(t) && ((Vector2)(t.position - fl.pos)).sqrMagnitude < 0.5f * 0.5f)
                        {
                            _burn?.Invoke(t, FireballDamage);
                            TcFx.Burst(fl.pos, FireColors[_look][0], 10, 4f, 0.07f, 0.35f);
                            TcFx.Burst(fl.pos, FireColors[_look][1], 6, 3f, 0.05f, 0.3f);
                            gone = true;
                            break;
                        }
                }
                if (gone) { if (fl.r) Destroy(fl.r.gameObject); _flames.RemoveAt(i); continue; }
                if (fl.r)
                {
                    float reach = (fireball ? 0.18f : 0.15f) * fl.r.transform.localScale.y + 0.06f;
                    _hot.Add(new Vector3(fl.pos.x, fl.pos.y, reach));
                    fl.r.transform.position = fl.pos;
                    if (!fireball)
                    {
                        // The column's flames swell as they climb, and fade at the end of their life.
                        float k = age / fl.life;
                        fl.r.transform.localScale = new Vector3(1f + fl.grow * k * 1.4f, 1f + fl.grow * k, 1f) * fl.size;
                        var c = FireTint[_look]; c.a = (k < 0.7f ? 1f : (1f - k) / 0.3f) * FireAlpha;
                        fl.r.color = c;
                    }
                }
            }
            if (_hot.Count == 0) return;
            foreach (var t in Targets())
            {
                if (!Alive(t)) continue;
                int over = 0;
                foreach (var h in _hot) if (((Vector2)t.position - (Vector2)h).sqrMagnitude < h.z * h.z) over++;
                if (over > 0) _burn?.Invoke(t, FlameDps * Mathf.Min(over, 4) * dt);
            }
            var hot = _hot;
            TcDanmaku.ClearWhere(b => { foreach (var h in hot) if ((b - (Vector2)h).sqrMagnitude < h.z * h.z) return true; return false; });
        }

        // ---- Targets ----
        IEnumerable<Transform> Targets()
        {
            if (PreviewTargets != null) { foreach (var t in PreviewTargets) yield return t; yield break; }
            foreach (var e in TcEnemy.All) if (e) yield return e.transform;
        }

        static bool Alive(Transform t)
        {
            if (!t || !t.gameObject.activeInHierarchy) return false;
            var e = t.GetComponent<TcEnemy>();
            return !e || e.Health > 0;
        }

        Transform Nearest(Vector3 from)
        {
            var f = TcWorld.Field;
            Transform best = null; float d = float.MaxValue;
            foreach (var t in Targets())
            {
                if (!Alive(t) || !f.Contains((Vector2)t.position)) continue;
                float dist = ((Vector2)(t.position - from)).sqrMagnitude;
                if (dist < d) { d = dist; best = t; }
            }
            return best;
        }
    }
}
