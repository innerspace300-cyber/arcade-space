using System.Collections.Generic;
using UnityEngine;

namespace TimeCrystal
{
    // ARcade: THE TIME CRYSTAL's effects, all at once and as many as the
    // screen holds: two shared particle systems (one draw each, however
    // many bullets) under the game's root -
    //  - Trail: the pixel trails every shot and enemy bullet leaves;
    //  - Spark: bursts of pixels (hits, muzzles, explosions).
    // Emitted by position and colour (TcShot, TcBullet, TcEnemy, TcBoss...).
    public static class TcFx
    {
        static ParticleSystem s_trail, s_spark, s_nova, s_warp, s_blast;
        static Transform s_root;

        static bool Ready()
        {
            if (s_trail && s_root == TcWorld.Root) return true;
            if (!TcWorld.Root) return false;
            s_root = TcWorld.Root;
            var holder = new GameObject("TC FX");
            holder.transform.SetParent(s_root, false);
            holder.layer = TcArt.ActorsLayer;
            s_trail = Make(holder.transform, "TRAILS", TcArt.Pixel, 4000, 40);
            s_spark = Make(holder.transform, "SPARKS", TcArt.Pixel, 3000, 60);
            // Enemies' death blasts (StageBlast): no gravity - it's space - just drifting out and easing off.
            s_blast = Make(holder.transform, "BLAST", TcArt.Pixel, 5000, 76);
            var drift = s_blast.limitVelocityOverLifetime;
            drift.enabled = true;
            drift.limit = 0f;
            drift.dampen = 0.06f;
            var keepSize = s_blast.sizeOverLifetime;
            keepSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 0.85f), new Keyframe(1f, 0.3f)));
            // The warp's streaks: stretched along their fall, like stars at a jump to light speed.
            s_warp = Make(holder.transform, "WARP", TcArt.Pixel, 1500, 2);
            var warp = s_warp.GetComponent<ParticleSystemRenderer>();
            warp.renderMode = ParticleSystemRenderMode.Stretch;
            warp.velocityScale = 0.06f;
            warp.lengthScale = 1f;
            var keep = s_warp.sizeOverLifetime;
            keep.enabled = false;
            // A boss's last blast: its pixels fall (NeonNova).
            s_nova = Make(holder.transform, "NOVA", TcArt.Pixel, 5000, 75);
            var main = s_nova.main;
            main.gravityModifier = 0.8f;
            var shrink = s_nova.sizeOverLifetime;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0.3f)));
            return true;
        }

        /// The pixel sparks' material (TcLaser's muzzle spray draws with it).
        public static Material SparkMaterial { get { Ready(); return s_spark ? s_spark.GetComponent<ParticleSystemRenderer>().sharedMaterial : null; } }

        internal static ParticleSystem Make(Transform parent, string name, Sprite look, int max, int order)
        {
            var go = new GameObject(name);
            go.layer = TcArt.ActorsLayer;
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = 0.3f;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            // Shrinks and fades as it goes.
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            color.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(TcArt.SpriteMaterial) { name = "TC " + name, mainTexture = look.texture };
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sortingOrder = order;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 10f;
            ps.Play();
            return ps;
        }

        static ParticleSystem.EmitParams s_p;

        /// One trail pixel, left behind (drifting a little).
        public static void Trail(Vector3 at, Color color, float size, Vector2 drift, float life = 0.22f)
        {
            if (!Ready()) return;
            s_p.position = new Vector3(at.x, at.y, at.z + 0.01f);
            s_p.velocity = drift;
            s_p.startColor = color;
            s_p.startSize = size;
            s_p.startLifetime = life;
            s_trail.Emit(s_p, 1);
        }

        /// A burst of pixels flying out.
        public static void Burst(Vector3 at, Color color, int count, float speed, float size, float life = 0.35f, Vector2? bias = null)
        {
            if (!Ready()) return;
            Vector2 b = bias ?? Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                Vector2 v = Random.insideUnitCircle.normalized * speed * Random.Range(0.35f, 1f) + b;
                s_p.position = new Vector3(at.x, at.y, at.z - 0.01f);
                s_p.velocity = v;
                s_p.startColor = Color.Lerp(color, Color.white, Random.value * 0.45f);
                s_p.startSize = size * Random.Range(0.6f, 1.2f);
                s_p.startLifetime = life * Random.Range(0.6f, 1.1f);
                s_spark.Emit(s_p, 1);
            }
        }


        /// A one-off animated explosion (TcArt's frames), with its sparks and flash.
        public static void Explode(Vector3 at, string frames, float scale, Color color, int sparks = 14, float fps = 22f, Transform follow = null)
        {
            if (!TcWorld.Root) return;
            var go = new GameObject("BOOM");
            // (`follow`: it rides along on a moving ship - a boss going down.)
            go.transform.SetParent(follow ? follow : TcWorld.Root, false);
            go.transform.position = new Vector3(at.x, at.y, at.z - 0.03f);
            go.transform.localScale = Vector3.one * scale / (follow ? follow.lossyScale.x : 1f);
            // (Turned a random quarter for variety - not the blue one: a mushroom cloud, it has an up.)
            go.transform.rotation = frames == "boom_blue" ? Quaternion.identity : Quaternion.Euler(0f, 0f, Random.Range(0, 4) * 90f);
            var sr = TcArt.Renderer(go, 70);
            // The big fireball cloud and the white blast are see-through.
            if (frames == "explosion_large") sr.color = new Color(1f, 1f, 1f, 0.55f);
            else if (frames == "white_blast") sr.color = new Color(1f, 1f, 1f, 0.6f);
            var anim = go.AddComponent<TcAnim>();
            anim.DestroyAtEnd = true;
            anim.Play(TcArt.Frames(frames), fps, loop: false);
            Burst(at, color, sparks, 3.5f * Mathf.Sqrt(scale), 0.07f, 0.5f);
        }

        static readonly Color[] s_neon = { new Color(1f, 0.25f, 0.85f), new Color(0.3f, 0.95f, 1f), new Color(1f, 0.92f, 0.3f), new Color(0.4f, 1f, 0.4f), new Color(0.7f, 0.35f, 1f), new Color(1f, 0.55f, 0.15f) };

        /// An enemy going up: chunky pixels of every neon colour flung out
        /// in a ring and a scatter, `scale` times a small one's.
        public static void NeonBurst(Vector3 at, float scale, Color[] palette = null)
        {
            if (!Ready()) return;
            int count = Mathf.RoundToInt(28 * Mathf.Sqrt(scale));
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
                bool ring = i % 2 == 0;
                float speed = (ring ? 4.2f : Random.Range(1f, 3.6f)) * Mathf.Sqrt(scale);
                s_p.position = new Vector3(at.x, at.y, at.z - 0.02f);
                s_p.velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed;
                var colors = palette ?? s_neon;
                s_p.startColor = colors[Random.Range(0, colors.Length)];
                s_p.startSize = Random.Range(0.07f, 0.13f) * Mathf.Min(1.6f, Mathf.Sqrt(scale));
                s_p.startLifetime = Random.Range(0.35f, 0.7f);
                s_spark.Emit(s_p, 1);
            }
        }

        // ---- Debris: pieces of a ship flying off where it's hit ----
        // Little squares cut from the ship's own frame (from its middle, where
        // it's solid), pooled; each spins away, falls and fades.
        const int MostDebris = 160;
        static readonly Dictionary<Sprite, Sprite[]> s_bits = new Dictionary<Sprite, Sprite[]>();
        static readonly List<Piece> s_pieces = new List<Piece>();
        static Transform s_piecesRoot;

        public static void Debris(Sprite ship, Vector3 at, float scale, int count)
        {
            if (!ship || !TcWorld.Root) return;
            if (s_piecesRoot != TcWorld.Root) { s_pieces.Clear(); s_bits.Clear(); s_piecesRoot = TcWorld.Root; }
            if (!s_bits.TryGetValue(ship, out var bits)) s_bits[ship] = bits = Cut(ship);
            for (int i = 0; i < count; i++)
            {
                var piece = GetPiece();
                if (!piece) return;
                Vector2 v = new Vector2(Random.Range(-3.2f, 3.2f), Random.Range(0.5f, 3.5f)) * Mathf.Sqrt(scale);
                piece.Launch(bits[Random.Range(0, bits.Length)], at, scale, v);
            }
        }

        static Sprite[] Cut(Sprite ship)
        {
            var r = ship.textureRect;
            var bits = new Sprite[10];
            for (int i = 0; i < bits.Length; i++)
            {
                int size = Random.Range(3, 6);
                float x = Random.Range(r.x + r.width * 0.2f, r.xMax - r.width * 0.2f - size);
                float y = Random.Range(r.y + r.height * 0.2f, r.yMax - r.height * 0.2f - size);
                bits[i] = Sprite.Create(ship.texture, new Rect(Mathf.Round(x), Mathf.Round(y), size, size), new Vector2(0.5f, 0.5f), TcArt.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            return bits;
        }

        static Piece GetPiece()
        {
            foreach (var p in s_pieces) if (p && !p.gameObject.activeSelf) return p;
            if (s_pieces.Count >= MostDebris) return null;
            var go = new GameObject("DEBRIS");
            go.SetActive(false);
            go.transform.SetParent(TcWorld.Root, false);
            var piece = go.AddComponent<Piece>();
            piece.Renderer = TcArt.Renderer(go, 65);
            s_pieces.Add(piece);
            return piece;
        }

        class Piece : MonoBehaviour
        {
            public SpriteRenderer Renderer;
            Vector2 _v;
            float _spin, _life, _t, _scale;

            public void Launch(Sprite bit, Vector3 at, float scale, Vector2 velocity)
            {
                Renderer.sprite = bit;
                Renderer.color = Color.white;
                transform.position = new Vector3(at.x, at.y, at.z - 0.02f);
                transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                _scale = scale;
                transform.localScale = Vector3.one * scale;
                _v = velocity;
                _spin = Random.Range(360f, 900f) * (Random.value < 0.5f ? -1f : 1f);
                _life = Random.Range(0.45f, 0.8f);
                _t = 0f;
                gameObject.SetActive(true);
            }

            void Update()
            {
                _t += Time.deltaTime;
                if (_t >= _life) { gameObject.SetActive(false); return; }
                _v += Vector2.down * 7f * Time.deltaTime;   // (falling behind, as the picture scrolls)
                transform.position += (Vector3)(_v * Time.deltaTime);
                transform.Rotate(0f, 0f, _spin * Time.deltaTime);
                float k = _t / _life;
                Renderer.color = WithAlpha(Color.white, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
                transform.localScale = Vector3.one * _scale * Mathf.Lerp(1f, 0.6f, k);
            }
        }

        /// Warp streaks: `count` lines of light falling past at `speed`
        /// (stretched by it), from all over the picture.
        public static void WarpStreaks(int count, float speed, bool reverse = false)
        {
            if (!Ready()) return;
            var f = TcWorld.Field;
            for (int i = 0; i < count; i++)
            {
                // (Backwards: from below, rushing up.)
                s_p.position = new Vector3(Random.Range(f.xMin, f.xMax), reverse ? Random.Range(f.yMin - 2f, f.yMax) : Random.Range(f.yMin, f.yMax + 2f), TcWorld.Origin.z + 0.02f);
                s_p.velocity = (reverse ? Vector2.up : Vector2.down) * speed * Random.Range(0.6f, 1.2f);
                s_p.startColor = Random.value < 0.6f ? new Color(0.85f, 0.95f, 1f, 0.9f) : WithAlpha(Random.value < 0.5f ? Cyan : Violet, 0.85f);
                s_p.startSize = Random.Range(0.025f, 0.05f);
                s_p.startLifetime = Random.Range(0.25f, 0.45f);
                s_warp.Emit(s_p, 1);
            }
        }

        /// The whole picture flashes `color` (up to `alpha`), fading over
        /// `seconds` - over everything but the HUD.
        public static void ScreenFlash(Color color, float alpha, float seconds)
        {
            if (!TcWorld.Root) return;
            var go = new GameObject("SCREEN FLASH");
            go.transform.SetParent(TcWorld.Root, false);
            var f = TcWorld.Field;
            go.transform.position = new Vector3(f.center.x, f.center.y, TcWorld.Origin.z - 0.1f);
            go.transform.localScale = new Vector3(f.width, f.height, 1f);
            var r = TcArt.Renderer(go, 980);
            r.sprite = TcArt.Pixel;
            go.AddComponent<Fader>().Run(r, color, alpha, seconds);
        }

        class Fader : MonoBehaviour
        {
            SpriteRenderer _r;
            Color _color;
            float _alpha, _seconds, _t;

            public void Run(SpriteRenderer r, Color color, float alpha, float seconds)
            {
                _r = r; _color = color; _alpha = alpha; _seconds = Mathf.Max(0.01f, seconds);
                Update();
            }

            void Update()
            {
                float k = 1f - _t / _seconds;
                if (k <= 0f) { Destroy(gameObject); return; }
                _r.color = WithAlpha(_color, _alpha * k * k);
                _t += Time.deltaTime;
            }
        }

        // The enemies' death blasts, a set of three colours a stage; each
        // blast is one of them (in its own shades), picked at random.
        static readonly Color[][] StageColors =
        {
            new[] { new Color(0.3f, 1f, 0.3f), new Color(0.2f, 1f, 0.75f), new Color(1f, 0.2f, 0.75f) },   // neon green, aqua green, neon pink
            new[] { new Color(0.65f, 0.3f, 1f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.95f, 0.2f) },  // purple, orange, neon yellow
            new[] { new Color(0.2f, 0.55f, 1f), new Color(1f, 0.2f, 0.25f), new Color(1f, 0.8f, 0.2f) },   // electric blue, neon red, gold
        };

        /// One colour's shades: itself, a deeper one, a paler hotter one.
        static Color[] Shades(Color c) => new[] { c, c * 0.75f + new Color(0f, 0f, 0f, 0.25f), Color.Lerp(c, Color.white, 0.45f) };

        /// An enemy going up: a falling pixel blast in one of the stage's colours.
        public static void StageBlast(Vector3 at, float scale)
        {
            int stage = TcStageDirector.Instance ? Mathf.Clamp(TcStageDirector.Instance.Level, 0, StageColors.Length - 1) : 0;
            var set = StageColors[stage];
            if (!Ready()) return;
            // (In space: no gravity; glowing more - a bigger, brighter halo and more white-hot pixels.)
            Nova(s_blast, at, scale, Shades(set[Random.Range(0, set.Length)]), false, false, 1.45f, 0.55f, 0.3f, 1.8f);   // (bigger pixels, same spread; the glow tight round each)
        }

        // Neon palettes (a bright, a deep and a white-hot shade of each).
        public static readonly Color[] GreenNeon = { new Color(0.3f, 1f, 0.35f), new Color(0.1f, 0.85f, 0.3f), new Color(0.75f, 1f, 0.6f), new Color(0.55f, 1f, 0.1f) };
        public static readonly Color[] BlueNeon = { new Color(0.2f, 0.65f, 1f), new Color(0.3f, 0.95f, 1f), new Color(0.65f, 0.85f, 1f), new Color(0.35f, 0.4f, 1f) };
        public static readonly Color[] PurpleNeon = { new Color(0.7f, 0.35f, 1f), new Color(0.85f, 0.55f, 1f), new Color(0.95f, 0.8f, 1f), new Color(0.55f, 0.2f, 1f) };
        public static readonly Color[] RedNeon = { new Color(1f, 0.25f, 0.25f), new Color(1f, 0.5f, 0.3f), new Color(1f, 0.8f, 0.7f), new Color(1f, 0.15f, 0.45f) };
        public static readonly Color[] PinkNeon = { new Color(1f, 0.2f, 0.75f), new Color(1f, 0.45f, 0.9f), new Color(1f, 0.75f, 0.95f), new Color(0.85f, 0.2f, 1f) };

        /// A big ship's last moment, all pixels: a flash, then a blast of
        /// chunky neon squares in its palette - each with a faint square glow
        /// round it - flung out and up, arcing down under gravity, the
        /// biggest and hottest from the middle.
        public static void NeonNova(Vector3 at, float scale, Color[] palette, bool flash = true)
            => Nova(s_nova, at, scale, palette, flash, true, 1.6f, 0.3f, 0.15f, 1f);

        // A pixel blast into `into`: `lift` throws it up a little (it falls back
        // under s_nova's gravity); each pixel has a square glow `halo` times its
        // size at `haloAlpha`, `hot` of them white-hot.
        static void Nova(ParticleSystem into, Vector3 at, float scale, Color[] palette, bool flash, bool lift, float halo, float haloAlpha, float hot, float pixel)
        {
            if (!Ready()) return;
            float s = Mathf.Sqrt(scale);
            if (flash) ScreenFlash(Color.Lerp(palette[0], Color.white, 0.5f), 0.6f, 0.4f);
            int count = Mathf.RoundToInt(80 * s);
            for (int i = 0; i < count; i++)
            {
                // Out every way, faster near the start of the list, a lift upward (it falls back).
                Vector2 dir = Random.insideUnitCircle.normalized;
                float speed = Random.Range(0.8f, 4.2f) * s * (Random.value < 0.2f ? 1.4f : 1f);
                var v = dir * speed + (lift ? Vector2.up * Random.Range(1f, 3f) : Vector2.zero);
                var pos = new Vector3(at.x, at.y, at.z - 0.02f) + (Vector3)(Random.insideUnitCircle * 0.35f * s);
                Color c = palette[Random.Range(0, palette.Length)];
                if (Random.value < hot) c = Color.Lerp(c, Color.white, 0.7f);   // (white-hot bits)
                float size = Random.Range(0.018f, 0.042f) * s * pixel * (Random.value < 0.1f ? 1.8f : 1f);
                float life = Random.Range(1f, 1.8f);
                // The glow: a tight square halo, the same square a little bigger and faint, travelling with it.
                s_p.position = pos; s_p.velocity = v; s_p.startColor = WithAlpha(c, haloAlpha); s_p.startSize = size * halo; s_p.startLifetime = life;
                into.Emit(s_p, 1);
                s_p.startColor = c; s_p.startSize = size;
                into.Emit(s_p, 1);
            }
        }

        // The neon colours.
        public static readonly Color Pink = new Color(1f, 0.25f, 0.85f);
        public static readonly Color Violet = new Color(0.7f, 0.35f, 1f);
        public static readonly Color Cyan = new Color(0.3f, 0.95f, 1f);
        public static readonly Color Red = new Color(1f, 0.25f, 0.3f);
        public static readonly Color Orange = new Color(1f, 0.6f, 0.15f);
        public static readonly Color Yellow = new Color(1f, 0.92f, 0.3f);
        public static readonly Color Green = new Color(0.4f, 1f, 0.4f);
        public static readonly Color White = new Color(1f, 1f, 1f);

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
