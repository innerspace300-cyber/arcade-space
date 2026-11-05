using System.Collections.Generic;
using UnityEngine;

namespace TimeCrystal
{
    // VIPER's bomb: a swarm of energy snakes, his laser's colours. All at once they burst out of the ship
    // across a 180-degree fan in front of it (left, up, right - nothing behind), each head leading and its body
    // streaming out of the ship behind along the head's own path, wriggling as it flies. After that first
    // burst (SeekAfter) each one homes: on the nearest enemy (one no other snake's after, if there's one),
    // swerving onto it and killing it on contact, then the next. Pixel art made here:
    // round segments shrinking to the tail, a head with eyes and a forked tongue; neon green ones crackling
    // with purple lightning, purple ones with green. Driven by Step (TcBombFx.SnakeSwarm; the Editor's
    // previews call it too).
    public class TcSnakeSwarm : MonoBehaviour
    {
        public const float CoilSeconds = 0f;   // (no coiled pause: out at once)
        const int Segments = 16;
        const float Spacing = 0.13f;            // (world units between segments along the path)
        const float Speed = 13f, Wriggle = 0.9f, WriggleRate = 16f;
        const float Thick = 0.24f;              // (a segment's size at the head end)
        public const int PerWave = 22;
        public const float SeekAfter = 0.2f;    // (seconds of the burst before they hunt)
        const float SeekTurn = 9f;              // (how hard they swerve onto a target)
        public const float Bite = 0.5f;         // (a head this near an enemy kills it: TcBombFx.SnakeSwarm)
        /// The Editor's previews: stand-in targets (no enemies there).
        public static List<Transform> PreviewTargets;
        const float Fan = 180f;   // (degrees, centred straight up the picture: left to right, over the top)

        // Like VIPER's laser: neon green (white-hot stripes) with purple lightning, or purple with green.
        // [head, tail, stripe, lightning]
        static readonly Color[][] Looks =
        {
            new[] { new Color(0.35f, 1f, 0.35f), new Color(0.1f, 0.8f, 0.25f), new Color(0.85f, 1f, 0.8f), new Color(0.75f, 0.3f, 1f) },
            new[] { new Color(0.8f, 0.4f, 1f), new Color(0.55f, 0.2f, 1f), new Color(0.95f, 0.85f, 1f), new Color(0.35f, 1f, 0.35f) },
        };

        class Snake
        {
            public SpriteRenderer head;
            public SpriteRenderer[] body;
            public readonly List<Vector3> path = new List<Vector3>();   // (newest first: the head's way back to the ship)
            public Vector3 pos, dir, aim;
            public float born, phase;
            public Transform target;
            public int look;
            public bool launched, gone;
        }

        readonly List<Snake> _snakes = new List<Snake>();
        // Bitten once: never chased again (a boss only dents, so it'd draw them round it for good).
        readonly HashSet<Transform> _bitten = new HashSet<Transform>();
        System.Action<Vector2> _strike;
        Vector3 _from;
        float _t;
        static Sprite s_disc, s_head;

        public bool Done { get; private set; }

        /// The swarm out of `from`, all at once. `strike(at)` is called for each head as it flies.
        public static TcSnakeSwarm Start(Vector3 from, System.Action<Vector2> strike)
        {
            var go = new GameObject("VIPER SNAKES");
            go.transform.SetParent(TcWorld.Root, false);
            var s = go.AddComponent<TcSnakeSwarm>();
            s._from = from;
            s._strike = strike;
            s.Wave(0f, 0);
            return s;
        }

        void Wave(float delay, int wave)
        {
            for (int i = 0; i < PerWave; i++)
            {
                // Spread evenly over the fan in front (up the picture), a little jitter.
                float deg = 90f - Fan * 0.5f + Fan * i / (PerWave - 1f) + Random.Range(-3f, 3f);
                float a = deg * Mathf.Deg2Rad;
                var aim = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                _snakes.Add(Make(_from, aim, delay, i % 2));
            }
        }

        Snake Make(Vector3 from, Vector3 aim, float born, int look)
        {
            var s = new Snake { aim = aim, born = born, look = look, phase = Random.Range(0f, 6.3f) };
            s.body = new SpriteRenderer[Segments];
            for (int i = Segments - 1; i >= 0; i--)
            {
                var r = Part("SNAKE " + i, Disc, 930 + Segments - i);
                float k = i / (Segments - 1f);
                r.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.38f, k);
                s.body[i] = r;
            }
            s.head = Part("SNAKE HEAD", Head, 930 + Segments + 1);
            // All of it at the ship to start; the body streams out of it behind the head.
            s.path.Add(from);
            s.pos = from;
            s.dir = aim;
            Lay(s);
            Show(s, false);
            return s;
        }

        SpriteRenderer Part(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = TcArt.Renderer(go, order);
            r.sprite = sprite;
            return r;
        }

        static void Show(Snake s, bool on)
        {
            s.head.enabled = on;
            foreach (var b in s.body) b.enabled = on;
        }

        void Update() => Step(Time.deltaTime);

        /// On by `dt` seconds.
        public void Step(float dt)
        {
            if (Done) return;
            _t += dt;
            bool any = false;
            var f = TcWorld.Field;
            foreach (var s in _snakes)
            {
                if (s.gone) continue;
                any = true;
                float age = _t - s.born;
                if (age < 0f) continue;
                if (!s.head.enabled) Show(s, true);
                {
                    if (!s.launched) s.launched = true;
                    float fly = age - CoilSeconds;
                    // After the burst: after the nearest enemy (a fresh one when it's gone).
                    if (fly >= SeekAfter)
                    {
                        if (!Alive(s.target)) s.target = Nearest(s);
                        if (s.target)
                        {
                            var to = s.target.position - s.pos; to.z = 0f;
                            if (to.sqrMagnitude > 1e-6f) s.aim = to.normalized;
                        }
                    }
                    // On along its aim, wriggling side to side.
                    s.dir = Vector3.Slerp(s.dir, s.aim, Mathf.Clamp01(dt * (s.target ? SeekTurn : 14f))).normalized;
                    var side = new Vector3(-s.dir.y, s.dir.x, 0f);
                    float speed = Speed * Mathf.Clamp01(0.35f + fly * 4f);
                    var vel = s.dir * speed + side * Mathf.Cos(fly * WriggleRate + s.phase) * Wriggle * speed * 0.45f;
                    s.pos += vel * dt;
                    s.path.Insert(0, s.pos);
                    if (s.path.Count > 400) s.path.RemoveAt(s.path.Count - 1);
                    _strike?.Invoke(s.pos);
                    if (s.target && ((Vector2)(s.target.position - s.pos)).sqrMagnitude < Bite * Bite) { _bitten.Add(s.target); s.target = null; }
                    // Off the picture (its head past the edge): gone, all of it, there and then.
                    if (!f.Contains((Vector2)s.pos)) { s.gone = true; Remove(s); continue; }
                }
                Lay(s);
                Colour(s, age);
                if (s.launched && Random.value < 0.3f) Lightning(s);
            }
            if (!any) { Done = true; Destroy(gameObject); }
        }

        static bool Alive(Transform t)
        {
            if (!t || !t.gameObject.activeInHierarchy) return false;
            var e = t.GetComponent<TcEnemy>();
            return !e || e.Health > 0;
        }

        // The nearest live enemy in the picture - one no other snake's after if there is one, else any.
        Transform Nearest(Snake me)
        {
            var f = TcWorld.Field;
            Transform best = null, bestTaken = null;
            float d = float.MaxValue, dTaken = float.MaxValue;
            IEnumerable<Transform> all = PreviewTargets;
            if (all == null)
            {
                var list = new List<Transform>();
                foreach (var e in TcEnemy.All) if (e) list.Add(e.transform);
                all = list;
            }
            foreach (var t in all)
            {
                if (!Alive(t) || _bitten.Contains(t) || !f.Contains((Vector2)t.position)) continue;
                float dist = ((Vector2)(t.position - me.pos)).sqrMagnitude;
                bool taken = false;
                foreach (var o in _snakes) if (o != me && !o.gone && o.target == t) { taken = true; break; }
                if (!taken && dist < d) { d = dist; best = t; }
                if (dist < dTaken) { dTaken = dist; bestTaken = t; }
            }
            return best ? best : bestTaken;
        }

        // A crackle of the other colour: a jagged little bolt of pixels off a segment, gone in a blink.
        static void Lightning(Snake s)
        {
            var color = Looks[s.look][3];
            var p = s.body[Random.Range(0, Segments)].transform.position;
            var dir = (Vector3)Random.insideUnitCircle.normalized;
            int steps = Random.Range(4, 8);
            for (int k = 0; k < steps; k++)
            {
                var side = new Vector3(-dir.y, dir.x, 0f) * Random.Range(-0.07f, 0.07f);
                p += dir * 0.06f + side;
                TcFx.Trail(p, k == 0 ? Color.white : color, 0.045f, Vector2.zero, 0.09f);
            }
        }

        static void Remove(Snake s)
        {
            if (s.head) Destroy(s.head.gameObject);
            foreach (var b in s.body) if (b) Destroy(b.gameObject);
        }

        // Each segment Spacing further back along the path from the head; the head turned along its way.
        static void Lay(Snake s)
        {
            float want = 0f, walked = 0f;
            int j = 0;
            var z = new Vector3(0f, 0f, TcWorld.Origin.z);
            s.head.transform.position = new Vector3(s.path[0].x, s.path[0].y, z.z);
            var ahead = s.path.Count > 1 ? s.path[0] - s.path[1] : s.dir;
            if (ahead.sqrMagnitude < 1e-8f) ahead = s.dir;
            s.head.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(ahead.y, ahead.x) * Mathf.Rad2Deg - 90f);
            for (int i = 0; i < Segments; i++)
            {
                want += Spacing;
                while (j < s.path.Count - 1 && walked + Vector3.Distance(s.path[j], s.path[j + 1]) < want)
                {
                    walked += Vector3.Distance(s.path[j], s.path[j + 1]);
                    j++;
                }
                Vector3 p;
                if (j >= s.path.Count - 1) p = s.path[s.path.Count - 1];
                else
                {
                    float seg = Vector3.Distance(s.path[j], s.path[j + 1]);
                    p = Vector3.Lerp(s.path[j], s.path[j + 1], seg > 1e-6f ? (want - walked) / seg : 0f);
                }
                s.body[i].transform.position = new Vector3(p.x, p.y, z.z + 0.0005f * i);
            }
        }

        // Head colour into tail colour, every third segment its belly colour; white flickers running down it (the electricity).
        void Colour(Snake s, float age)
        {
            var look = Looks[s.look];
            int flash = (int)(age * 40f + s.phase * 3f) % (Segments + 6);
            for (int i = 0; i < Segments; i++)
            {
                float k = i / (Segments - 1f);
                var c = Color.Lerp(look[0], look[1], k);
                if (i % 3 == 1) c = Color.Lerp(c, look[2], 0.55f);
                if (Mathf.Abs(i - flash) <= 1) c = Color.Lerp(c, Color.white, 0.75f);
                s.body[i].color = c;
            }
            s.head.color = Color.Lerp(look[0], Color.white, Random.value < 0.15f ? 0.6f : 0.1f);
        }

        // ---- The pixel art (about the ships' 2x pixels) ----
        /// The art made ahead, so pressing bomb doesn't hitch: TcBombFx.Prewarm.
        public static void Prewarm() { _ = Disc; _ = Head; }

        static Sprite Disc
        {
            get
            {
                if (s_disc) return s_disc;
                // A round segment, 9 art pixels across, a darker rim and a light shine top left.
                const int n = 9;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x - 4f, dy = y - 4f, d = Mathf.Sqrt(dx * dx + dy * dy);
                        Color c = Color.clear;
                        if (d <= 4.4f) c = d > 3.4f ? new Color(0.72f, 0.72f, 0.72f, 1f) : Color.white;
                        if (d <= 2f && dx < 0 && dy > 0) c = Color.white;
                        tex.SetPixel(x, y, c);
                    }
                tex.Apply();
                s_disc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / Thick);
                return s_disc;
            }
        }

        static Sprite Head
        {
            get
            {
                if (s_head) return s_head;
                // Pointing up: a rounded wedge 11 wide, 13 tall with the tongue's fork above it; two eyes.
                const int w = 11, h = 15;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var clear = Color.clear;
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) tex.SetPixel(x, y, clear);
                for (int y = 0; y < 12; y++)
                {
                    // Widest a third of the way up, rounding to the snout.
                    float half = y < 4 ? 3.5f + y * 0.5f : Mathf.Lerp(5.5f, 2f, (y - 4) / 7f);
                    for (int x = 0; x < w; x++)
                        if (Mathf.Abs(x - 5f) <= half) tex.SetPixel(x, y, Mathf.Abs(x - 5f) > half - 1f ? new Color(0.72f, 0.72f, 0.72f, 1f) : Color.white);
                }
                // Eyes: dark, a white glint.
                var eye = new Color(0.05f, 0.02f, 0.12f, 1f);
                tex.SetPixel(2, 7, eye); tex.SetPixel(8, 7, eye); tex.SetPixel(2, 8, eye); tex.SetPixel(8, 8, eye);
                // Tongue: up from the snout, forked.
                var tongue = new Color(1f, 0.25f, 0.45f, 1f);
                tex.SetPixel(5, 12, tongue); tex.SetPixel(5, 13, tongue); tex.SetPixel(4, 14, tongue); tex.SetPixel(6, 14, tongue);
                tex.Apply();
                s_head = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.3f), w / (Thick * 1.2f));   // (a bit wider than the body)
                return s_head;
            }
        }
    }
}
