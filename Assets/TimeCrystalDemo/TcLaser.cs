using UnityEngine;

namespace TimeCrystal
{
    // ARcade: THE TIME CRYSTAL's laser, on C (TcWorld.Laser): a wide beam from the
    // ship's nose straight up - each pilot's own (TcArt.lasers: Hovl Studio's
    // 3D Lasers Pack, for its length and hit sparks; drawn as a chunky pixel-art
    // beam Wide across - PixelBeam), sparks spraying from its mouth, each
    // pilot's own flare round it (Flare: lightning, flames or sparks), and every
    // enemy bullet in its width cut away. It stops on the first enemy in its way, burning it
    // (Damage a second) until it's gone, then reaches on to the next or the
    // top of the picture. It runs on energy: a bar under the ship drains while
    // C's held and fills back up once it's let go (after a moment); empty, the
    // beam cuts out until there's enough to fire again.
    //
    // Hovl's script draws the beam (and its flash and hit sparks); the length
    // comes from here - a 2D cast against the enemies - since its own casts
    // are 3D physics.
    [DefaultExecutionOrder(200)]   // (after HS_Laser's LateUpdate: the hit sparks go where this says)
    public class TcLaser : MonoBehaviour
    {
        public const float Damage = 50f;         // a second, to what it's burning (doubled 2026-10-15)
        const float Drain = 1f / 4.5f;           // energy a second while firing (4.5 s from full)
        const float Charge = 0.3f;               // a second refilling
        const float ChargeDelay = 0.4f;          // after letting go, before it refills
        const float MinToFire = 0.15f;           // empty: it waits for this much
        const float Nose = 0.42f;                // the nose, above the ship's middle
        const float EffectScale = 0.4f;          // the flash and hit sparks (made for 3D scenes) at the picture's size
        // ARcade 2026-10-17: a wide CAVE-style beam - Hovl's beam this much wider, cutting through
        // every enemy bullet within CutHalfWidth of its middle, a spray of sparks and a glow at the nose.
        public const float Wide = 1.25f;          // the beam's width (world units: the field's 9 across)
        public const float CutHalfWidth = 0.55f;
        const float MuzzleRate = 220f;            // sparks a second
        public const float GlowSize = 2.2f, CoreSize = 0.9f;   // the glow at its mouth

        Hovl.HS_Laser _beam;
        GameObject _beamObject;
        ParticleSystem[] _hitSparks;
        Transform _hitObject;
        RetroShmupPlayerController _ship;
        SpriteRenderer _barBack, _barFill;
        float _energy = 1f, _idle, _burn;
        bool _firing, _empty;
        int _pilot = -1;
        Color _color = Color.white;

        public float Energy => _energy;
        /// The beam's on: the ship's guns hold (TcPlayerShip) - one weapon, one bar under it.
        public bool Firing => _firing;

        TcLaserVoice _voice;

        void Awake()
        {
            _ship = GetComponent<RetroShmupPlayerController>();
            // Its sound: Resources/TcSfx/laser_beam (TcLaserVoice).
            _voice = new TcLaserVoice(transform, TcSfx.Clip("laser_beam"));
            _barBack = Bar("LASER BAR", 21, new Color(0f, 0f, 0f, 0.6f));
            _barFill = Bar("LASER FILL", 22, Color.white);
            _barBack.transform.localScale = new Vector3(0.84f, 0.075f, 1f);
            Rebuild();
        }

        SpriteRenderer Bar(string name, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, -0.74f, 0f);
            var sr = TcArt.Renderer(go, order);
            sr.sprite = TcArt.Pixel;
            sr.color = color;
            return sr;
        }

        /// The chosen pilot's beam (Choose Your Fighter calls it through TcPlayerShip.ApplyPilot).
        public void Rebuild()
        {
            int pilot = TcPilots.Index;
            if (pilot == _pilot && _beam) return;
            _pilot = pilot;
            _color = TcPilots.Current.Color;
            if (_beamObject) Destroy(_beamObject);
            _beam = null;
            var prefabs = TcArt.Get ? TcArt.Get.lasers : null;
            var prefab = prefabs != null && pilot < prefabs.Length ? prefabs[pilot] : null;
            if (!prefab || !TcWorld.Root) return;
            // Under the game's root (not the ship: its banking and blinking stay its own),
            // switched off until made ready, on the ships' depth layer.
            _beamObject = Instantiate(prefab, TcWorld.Root);
            _beamObject.SetActive(false);
            _beamObject.name = "LASER";
            foreach (var t in _beamObject.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = TcArt.ActorsLayer;
            _beam = _beamObject.GetComponent<Hovl.HS_Laser>();
            if (!_beam) { Destroy(_beamObject); return; }
            _beam.collisionMask = 0;          // (its length is set here)
            _beam.damageOverTime = 0f;
            _beam.damageNearOrigin = false;
            _beam.chainBounces = false;
            _beam.playOnEnable = false;
            _beam.continuous = true;
            _beam.deactivateWhenFinished = false;
            _beam.growthDuration = 0.08f;
            _beam.fadeDuration = 0.12f;
            if (_beam.FlashEffect) _beam.FlashEffect.transform.localScale *= EffectScale;
            if (_beam.HitEffect)
            {
                _beam.HitEffect.transform.localScale *= EffectScale;
                _hitObject = _beam.HitEffect.transform;
                _hitSparks = _beam.HitEffect.GetComponentsInChildren<ParticleSystem>(true);
                _beam.HitEffect = null;   // (driven from here: its 3D casts would keep stopping it)
            }
            foreach (var ps in _beamObject.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // (shrinks with EffectScale)
                main.playOnAwake = false;   // (nothing until C)
            }
            _beamObject.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.back);   // (its forward: up the picture)
            Widen(_beam, 0.0001f);   // (Hovl's smooth line hidden: the pixel beam draws it - PixelBeam)
            if (_beam.FlashEffect) _beam.FlashEffect.SetActive(false);
            _beam.FlashEffect = null;
            BuildMuzzle();
            if (_pixelBeam) Destroy(_pixelBeam.gameObject);
            _pixelBeam = MakePixelBeam(_muzzleRoot.transform.parent, _color);
            _pixelBeam.gameObject.SetActive(false);
            // Left switched off until C's first pressed (its flash and sparks would otherwise run idle).
            _firing = false;
        }

        void OnDestroy()
        {
            if (_beamObject) Destroy(_beamObject);
            if (_muzzleRoot) Destroy(_muzzleRoot);
            if (_pixelBeam) Destroy(_pixelBeam.gameObject);
            if (_flare) Destroy(_flare.gameObject);
            if (_impactRoot) Destroy(_impactRoot);
        }

        /// Hovl's beam `width` across (every pilot's the same; its line, or its particle mesh), its length as it was.
        public static void Widen(Hovl.HS_Laser beam, float width)
        {
            var lines = beam.GetComponentsInChildren<LineRenderer>(true);
            float widest = 0f;
            foreach (var line in lines) widest = Mathf.Max(widest, line.widthMultiplier);
            foreach (var line in lines) line.widthMultiplier *= width / Mathf.Max(0.001f, widest);
            if (lines.Length == 0) beam.laserScale *= width / 0.3f;
            // HS_Laser notes its line's width once (Awake) and goes back to it on every Fire: it
            // has to note the new one, or the beam's as thin as ever in the game.
            var own = beam.GetComponent<LineRenderer>();
            var noted = typeof(Hovl.HS_Laser).GetField("originalWidth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (own && noted != null) noted.SetValue(beam, own.widthMultiplier);
        }

        // ---- the beam itself: chunky pixel art in the pilot's colours (CAVE-style) ----
        public const int BeamPixels = 20;
        public const float BeamOpacity = 0.55f;    // (45% see-through)
        const bool PixelImpact = true;             // (Hovl's own 3D hit sparks off: the pixel impact - MakeImpact - instead)          // across (each about 4 of the picture's pixels)
        const int BeamRows = 32, BeamFrames = 6;
        const float BeamFps = 24f;
        PixelBeam _pixelBeam;

        /// A tiled column of pixel-art beam frames: `MakePixelBeam` builds one (the preview render uses it too).
        public class PixelBeam : MonoBehaviour
        {
            public Sprite[] frames;
            public SpriteRenderer sr;
            float _t;
            public void Show(Vector3 bottom, float length, float alpha)
            {
                transform.position = bottom;
                sr.size = new Vector2(Wide, Mathf.Max(0.01f, length));
                _t += Time.deltaTime;
                sr.sprite = frames[(int)(_t * BeamFps) % frames.Length];
                sr.color = new Color(1f, 1f, 1f, alpha * BeamOpacity);
            }
        }

        public static PixelBeam MakePixelBeam(Transform parent, Color color)
        {
            var go = new GameObject("PIXEL BEAM");
            go.transform.SetParent(parent, false);
            go.layer = TcArt.ActorsLayer;
            var sr = TcArt.Renderer(go, 29);
            var beam = go.AddComponent<PixelBeam>();
            beam.sr = sr;
            beam.frames = new Sprite[BeamFrames];
            float ppu = BeamPixels / Wide;
            var rnd = new System.Random(color.GetHashCode());
            Color edge = Color.Lerp(color, Color.black, 0.45f), body = Color.Lerp(color, Color.black, 0.1f), light = Color.Lerp(color, Color.white, 0.35f);
            for (int f = 0; f < BeamFrames; f++)
            {
                var tex = new Texture2D(BeamPixels, BeamRows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                for (int y = 0; y < BeamRows; y++)
                {
                    // Each row: its own wobble of the edges and how wide the hot middle runs (energy surging up it).
                    float surge = Mathf.Sin((y + f * BeamRows / (float)BeamFrames) / BeamRows * Mathf.PI * 4f);
                    int edgeIn = rnd.Next(0, 2) + (surge > 0.6f ? 0 : 1);
                    int hot = 1 + (surge > 0.5f ? 1 : 0);
                    for (int x = 0; x < BeamPixels; x++)
                    {
                        float d = Mathf.Abs(x - (BeamPixels - 1) * 0.5f);   // from the middle
                        float half = BeamPixels * 0.5f;
                        Color c;
                        if (d >= half - edgeIn) c = Color.clear;
                        else if (d >= half - edgeIn - 2) c = new Color(edge.r, edge.g, edge.b, 0.85f);
                        else if (d >= hot + 2) c = body;
                        else if (d >= hot) c = light;
                        else c = Color.white;
                        // Sparkles running up it.
                        if (c.a > 0.5f && rnd.NextDouble() < 0.02) c = light;
                        tex.SetPixel(x, y, c);
                    }
                }
                tex.Apply();
                beam.frames[f] = Sprite.Create(tex, new Rect(0, 0, BeamPixels, BeamRows), new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect);
            }
            sr.sprite = beam.frames[0];
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(Wide, 1f);
            return beam;
        }

        // ---- each pilot's own flare round the beam ----
        // NOVA: yellow lightning crackling off its sides. BLAZE: flames licking out of its sides.
        // VIPER: purple lightning. RIO: green sparks streaming up the whole shaft.
        ParticleSystem _flare;
        static ParticleSystem.EmitParams s_fp;

        /// The flare's particles (big pixels, drawn over the beam): MakeFlare builds it, Flare fills it.
        public static ParticleSystem MakeFlare(Transform parent)
        {
            var go = new GameObject("LASER FLARE");
            go.transform.SetParent(parent, false);
            go.layer = TcArt.ActorsLayer;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;
            main.startSpeed = 0f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = TcFx.SparkMaterial;
            r.sortingOrder = 33;
            ps.Play();
            // Its halos: the same, soft and behind.
            var haloGo = Object.Instantiate(go, go.transform);
            haloGo.name = "LASER FLARE GLOW";
            foreach (Transform t in haloGo.transform) Object.Destroy(t.gameObject);
            var hr = haloGo.GetComponent<ParticleSystemRenderer>();
            hr.sharedMaterial = HaloMaterial;
            hr.sortingOrder = 28;
            var halos = haloGo.GetComponent<ParticleSystem>();
            halos.Play();
            s_halos = halos;
            return ps;
        }

        /// This frame's flare for `pilot`, on a beam from `bottom` up `length` (dt: the frame's time; the preview
        /// passes a longer one to fill it in at once).
        public static void Flare(ParticleSystem ps, int pilot, Vector3 bottom, float length, float dt)
        {
            if (!ps || length < 0.2f) return;
            float half = Wide * 0.5f, px = Wide / BeamPixels;
            switch (pilot)
            {
                case 0: Lightning(ps, bottom, length, half, px, new Color(1f, 0.92f, 0.25f), dt); break;   // NOVA: yellow
                case 2: Lightning(ps, bottom, length, half, px, new Color(0.78f, 0.35f, 1f), dt); break;   // VIPER: purple
                case 1:   // BLAZE: flames out of both sides
                    for (int i = 0, n = Mathf.CeilToInt(140f * length / 9f * dt * 10f); i < n; i++)
                    {
                        int side = Random.value < 0.5f ? -1 : 1;
                        float y = Random.Range(0.1f, length);
                        float life = Random.Range(0.18f, 0.38f);
                        var v = new Vector2(side * Random.Range(1.2f, 3.2f), Random.Range(0.6f, 2.2f));
                        float age = Random.Range(0f, life * 0.7f);
                        Color c = Random.value < 0.35f ? new Color(1f, 0.92f, 0.35f) : Random.value < 0.6f ? new Color(1f, 0.55f, 0.1f) : new Color(0.95f, 0.18f, 0.08f);
                        Emit(ps, bottom + new Vector3(side * (half - px) + v.x * age, y + v.y * age, -0.01f), v, c, px * Random.Range(1.2f, 2.4f), life - age);
                    }
                    break;
                case 3:   // RIO: green sparks streaming up its whole shaft
                    for (int i = 0, n = Mathf.CeilToInt(160f * length / 9f * dt * 10f); i < n; i++)
                    {
                        float x = Random.Range(-half * 0.95f, half * 0.95f);
                        float life = Random.Range(0.15f, 0.35f);
                        var v = new Vector2(x * 0.4f, Random.Range(6f, 12f));
                        float age = Random.Range(0f, life * 0.8f);
                        Color c = Random.value < 0.3f ? Color.white : Random.value < 0.6f ? new Color(0.4f, 1f, 0.35f) : new Color(0.1f, 0.85f, 0.3f);
                        Emit(ps, bottom + new Vector3(x + v.x * age, Random.Range(0f, length) , -0.01f), v, c, px * Random.Range(0.8f, 1.6f), life - age);
                    }
                    break;
            }
        }

        // Lightning crackling round the beam: jagged pixel bolts running up its sides, hugging and
        // jumping off its edge, now and then forking out; a white core pixel every few.
        static void Lightning(ParticleSystem ps, Vector3 bottom, float length, float half, float px, Color color, float dt)
        {
            int bolts = Mathf.Clamp(Mathf.RoundToInt(dt * 34f * Mathf.Max(1f, length / 4f)), 1, 60);
            for (int b = 0; b < bolts; b++)
            {
                int side = Random.value < 0.5f ? -1 : 1;
                float y = Random.Range(0.1f, length - 0.3f);
                float life = Random.Range(0.05f, 0.1f);
                int steps = Random.Range(10, 22), out1 = 1;
                for (int i = 0; i < steps; i++)
                {
                    // Up a pixel, in and out of the edge a few pixels (a zig-zag along it).
                    out1 = Mathf.Clamp(out1 + Random.Range(-2, 3), -1, 4);
                    y += px * Random.Range(0.7f, 1.4f);
                    float x = side * (half + out1 * px);
                    Emit(ps, bottom + new Vector3(x, y, -0.01f), Vector2.zero, i % 4 == 0 ? Color.white : color, px * 1.15f, life);
                    // A fork jumping out from it.
                    if (Random.value < 0.08f)
                    {
                        float fx = x, fy = y;
                        for (int k = 0, n = Random.Range(3, 7); k < n; k++)
                        {
                            fx += side * px * Random.Range(0.8f, 1.3f);
                            fy += px * Random.Range(-1.2f, 1.2f);
                            Emit(ps, bottom + new Vector3(fx, fy, -0.01f), Vector2.zero, color, px, life * 0.8f);
                        }
                    }
                }
            }
        }

        static void Emit(ParticleSystem ps, Vector3 at, Vector2 v, Color c, float size, float life)
        {
            s_fp.position = at;
            s_fp.velocity = v;
            s_fp.startColor = c;
            s_fp.startSize = size;
            s_fp.startLifetime = Mathf.Max(0.02f, life);
            ps.Emit(s_fp, 1);
            // Its glow: a soft halo behind it, the same colour, faint (lightning's packed close: fainter still).
            if (s_halos == null || Random.value > HaloShare) return;
            s_fp.position = at + new Vector3(0f, 0f, 0.005f);
            s_fp.startColor = TcFx.WithAlpha(c, HaloAlpha * (v == Vector2.zero ? 0.6f : 1f));
            s_fp.startSize = size * HaloSize;
            s_halos.Emit(s_fp, 1);
        }

        static ParticleSystem s_halos;
        const float HaloShare = 0.5f;   // (every other particle: plenty of glow, half the cost)

        // ---- the muzzle: a spray of pixel sparks fanning up and out of the nose, and a glow ----
        GameObject _muzzleRoot;
        ParticleSystem _muzzle;
        SpriteRenderer _glow, _core;
        static Sprite s_glowSprite;

        void BuildMuzzle()
        {
            if (_muzzleRoot) Destroy(_muzzleRoot);
            _muzzleRoot = new GameObject("LASER MUZZLE");
            _muzzleRoot.transform.SetParent(TcWorld.Root, false);
            _muzzleRoot.layer = TcArt.ActorsLayer;
            _muzzle = MakeMuzzle(_muzzleRoot.transform, _color);
            MakeMuzzle(_muzzleRoot.transform, _color, halo: true);
            if (_impactRoot) Destroy(_impactRoot);
            _impactRoot = MakeImpact(TcWorld.Root, _color);
            _impactRoot.SetActive(false);
            if (_flare) Destroy(_flare.gameObject);
            _flare = MakeFlare(TcWorld.Root);
            _glow = Glow(_muzzleRoot.transform, "GLOW", TcFx.WithAlpha(_color, 0.4f), GlowSize, 30);
            _core = Glow(_muzzleRoot.transform, "CORE", new Color(1f, 1f, 1f, 0.95f), CoreSize, 31);
            _muzzleRoot.SetActive(false);
            TcPlayerTop.Add(_muzzleRoot);        // (at the ship's nose: over the HUD with it)
            TcPlayerTop.Add(_flare.gameObject);
        }

        /// The spray (also used by the laser preview render): sparks in the pilot's colour and white.
        public static ParticleSystem MakeMuzzle(Transform parent, Color color, bool down = false, bool halo = false)
        {
            var go = new GameObject(halo ? "SPARK GLOW" : "SPARKS");
            go.transform.SetParent(parent, false);
            go.layer = TcArt.ActorsLayer;
            // Its cone opens up the picture (the beam's mouth), or down and out (where it hits).
            go.transform.localRotation = Quaternion.Euler(down ? 90f : -90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 12f);
            main.startSize = halo ? new ParticleSystem.MinMaxCurve(0.3f, 0.55f) : new ParticleSystem.MinMaxCurve(0.07f, 0.17f);
            main.startColor = halo ? new ParticleSystem.MinMaxGradient(TcFx.WithAlpha(color, HaloAlpha), TcFx.WithAlpha(Color.Lerp(color, Color.white, 0.4f), HaloAlpha))
                                   : new ParticleSystem.MinMaxGradient(Color.white, Color.Lerp(color, Color.white, 0.2f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission;
            emission.rateOverTime = halo ? MuzzleRate * 0.35f : MuzzleRate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = down ? 75f : 50f;
            shape.radius = 0.3f;
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true; drag.dampen = 0.07f; drag.limit = 3f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = halo ? HaloMaterial : TcFx.SparkMaterial;
            r.sortingOrder = halo ? 28 : 32;
            return ps;
        }

        // ---- glow round the pixel particles: a soft halo behind each (or, for the sprays, a cloud of them) ----
        const float HaloAlpha = 0.3f, HaloSize = 3.2f;
        static Material s_halo;

        public static Material HaloMaterial
        {
            get
            {
                if (s_halo) return s_halo;
                const int n = 32;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                        float a = Mathf.Clamp01(1f - d); a *= a * a;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                s_halo = new Material(TcFx.SparkMaterial) { name = "TC LASER HALO", mainTexture = tex };
                return s_halo;
            }
        }

        // ---- where it hits: a burst like its mouth's, spraying back down and out, and a glow over the cut end ----
        GameObject _impactRoot;

        /// The impact (also used by the preview render): sparks, their glow, and the pixel glow and core.
        public static GameObject MakeImpact(Transform parent, Color color)
        {
            var root = new GameObject("LASER IMPACT");
            root.transform.SetParent(parent, false);
            root.layer = TcArt.ActorsLayer;
            MakeMuzzle(root.transform, color, down: true);
            MakeMuzzle(root.transform, color, down: true, halo: true);
            Glow(root.transform, "GLOW", TcFx.WithAlpha(color, 0.45f), GlowSize * 0.9f, 30);
            Glow(root.transform, "CORE", new Color(1f, 1f, 1f, 0.9f), CoreSize * 0.9f, 31);
            return root;
        }

        void ShowImpact()
        {
            if (!_impactRoot) return;
            bool on = _firing && _target;
            if (on != _impactRoot.activeSelf)
            {
                _impactRoot.SetActive(on);
                if (on) foreach (var ps in _impactRoot.GetComponentsInChildren<ParticleSystem>()) ps.Play();
            }
            if (!on) return;
            _impactRoot.transform.position = new Vector3(_end.x, _end.y - 0.1f, _end.z);
            float pulse = 1f + 0.22f * Mathf.Sin(Time.time * 47f) + Random.Range(-0.1f, 0.1f);
            _impactRoot.transform.localScale = Vector3.one * pulse;
        }

        static SpriteRenderer Glow(Transform parent, string name, Color color, float size, int order)
        {
            if (!s_glowSprite)
            {
                const int n = 22;   // (big pixels: banded rings, not a smooth blur)
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f)) / (n * 0.5f);
                        float a = Mathf.Clamp01(1f - d); a = Mathf.Round(a * a * 4f) / 4f;   // (banded)
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                s_glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = TcArt.ActorsLayer;
            go.transform.localScale = Vector3.one * size;
            var sr = TcArt.Renderer(go, order);
            sr.sprite = s_glowSprite;
            sr.color = color;
            return sr;
        }

        float _beamAlpha;
        void ShowPixelBeam()
        {
            if (!_pixelBeam || !_beamObject) return;
            _beamAlpha = Mathf.MoveTowards(_beamAlpha, _firing ? 1f : 0f, Time.deltaTime / (_firing ? 0.06f : 0.12f));
            bool on = _beamAlpha > 0.01f;
            if (on != _pixelBeam.gameObject.activeSelf) _pixelBeam.gameObject.SetActive(on);
            if (on) _pixelBeam.Show(_beamObject.transform.position, _beam.MaxLength, _beamAlpha);
        }

        void ShowMuzzle()
        {
            if (!_muzzleRoot) return;
            bool on = _firing;
            if (on != _muzzleRoot.activeSelf) _muzzleRoot.SetActive(on);
            if (!on) return;
            _muzzleRoot.transform.position = _beamObject.transform.position;
            foreach (var ps in _muzzleRoot.GetComponentsInChildren<ParticleSystem>()) if (!ps.isPlaying) ps.Play();
            // A flickering, pulsing glow (the beam's mouth).
            float pulse = 1f + 0.18f * Mathf.Sin(Time.time * 40f) + Random.Range(-0.08f, 0.08f);
            _glow.transform.localScale = Vector3.one * GlowSize * pulse;
            _core.transform.localScale = Vector3.one * CoreSize * pulse;
        }

        void Update()
        {
            if (!_beam && TcWorld.Root) Rebuild();
            bool can = _ship && _ship.CanAct && _beam;
            if (_empty && _energy >= MinToFire) _empty = false;
            bool want = can && TcWorld.Laser && !_empty;

            if (want)
            {
                _energy -= Drain * Time.deltaTime;
                _idle = 0f;
                if (_energy <= 0f) { _energy = 0f; _empty = true; want = false; }
            }
            else
            {
                _idle += Time.deltaTime;
                if (_idle > ChargeDelay) _energy = Mathf.Min(1f, _energy + Charge * Time.deltaTime);
            }

            if (want && !_firing)
            {
                if (!_beamObject.activeSelf) _beamObject.SetActive(true);
                _beam.Fire(transform);
                _voice?.Start();
                // The laser instead of a power-up: whatever it had runs out now.
                if (TcPlayerShip.Instance) TcPlayerShip.Instance.EndPowerUp();
            }
            else if (!want && _firing) { _beam.DisablePrepare(); _voice?.Stop(); }
            _firing = want;
            if (_firing) SpatialEmulator.Haptics.Sustain();   // (a heavy rumble for as long as it's on)
            _voice?.Update(_firing);
            // Faded out: off altogether until C again.
            if (!_firing && _beamObject && _beamObject.activeSelf && !_beam.IsBusy) _beamObject.SetActive(false);

            if (_beamObject)
            {
                var nose = transform.position + Vector3.up * Nose;
                _beamObject.transform.position = new Vector3(nose.x, nose.y, TcWorld.Origin.z - 0.02f);
            }
            if (_firing) Burn();
            ShowMuzzle();
            ShowPixelBeam();
            ShowImpact();
            if (_firing && _flare && _beamObject) Flare(_flare, _pilot, _beamObject.transform.position, _beam.MaxLength, Time.deltaTime);
            ShowBar();
        }

        TcEnemy _target;
        Vector3 _end;

        // How far up it reaches (to the first enemy it can hit), and burning that one.
        void Burn()
        {
            var f = TcWorld.Field;
            Vector2 from = _beamObject.transform.position;
            float most = f.yMax - from.y + 0.5f;
            _target = null;
            float nearest = most;
            foreach (var hit in Physics2D.RaycastAll(from, Vector2.up, most, 1 << LayerMask.NameToLayer("TcEnemy")))
            {
                var enemy = hit.collider.GetComponentInParent<TcEnemy>();
                if (!enemy || enemy.Health <= 0 || !f.Contains(hit.point)) continue;
                if (hit.distance < nearest) { nearest = hit.distance; _target = enemy; }
            }
            // A little into it, so it reads as hitting it.
            _beam.MaxLength = _target ? nearest + 0.15f : most;
            _end = (Vector3)from + Vector3.up * _beam.MaxLength;
            // It cuts through every enemy bullet in its width, all the way up.
            float x0 = from.x, y0 = from.y - 0.2f, y1 = _end.y;
            TcDanmaku.ClearWhere(p => Mathf.Abs(p.x - x0) < CutHalfWidth && p.y > y0 && p.y < y1);
            // A pulse of its colour running up it.
            if (Random.value < 0.3f) TcFx.Trail((Vector3)from + Vector3.up * Random.Range(0.2f, _beam.MaxLength), TcFx.WithAlpha(_color, 0.8f), 0.05f, Vector2.up * 3f, 0.2f);

            if (!_target) return;
            _burn += Damage * Time.deltaTime;
            while (_burn >= 1f && _target && _target.Health > 0)
            {
                _burn -= 1f;
                _target.Hurt(1);
            }
            if (Random.value < 0.35f) TcFx.Burst(_end, _color, 2, 2.5f, 0.05f, 0.25f);
        }

        void LateUpdate()
        {
            // Its hit sparks: on the enemy it's burning, off otherwise (its own 3D casts never hit).
            if (_hitSparks == null || !_hitObject) return;
            bool hitting = _firing && _target;
            _hitObject.position = _end;
            foreach (var ps in _hitSparks)
            {
                if (!ps) continue;
                if (hitting && !ps.isPlaying && !PixelImpact) ps.Play(false);
                else if (!hitting && ps.isPlaying) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void ShowBar()
        {
            bool show = _ship && _ship.CanAct && (_firing || _energy < 0.999f);
            _barBack.enabled = _barFill.enabled = show;
            if (!show) return;
            float w = 0.8f * _energy;
            _barFill.transform.localScale = new Vector3(w, 0.04f, 1f);
            _barFill.transform.localPosition = new Vector3(-0.4f + w * 0.5f, -0.74f, 0f);
            // Its pilot's colour; red and blinking while it's run dry.
            _barFill.color = _empty ? (Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? TcFx.Red : Color.white) : _color;
        }
    }

    // The laser's sound, "High Energy Laser Beam" (12.4 s): its start and steady beam while
    // it's held - the steady part looped (cross-faded) past LoopEnd - and on letting go,
    // straight on to its last seconds, the beam shutting off. Each press starts it over.
    public class TcLaserVoice
    {
        const float LoopStart = 1.5f, LoopEnd = 9.4f, Tail = 9.65f;   // (seconds in the clip: the shut-off starts about 9.7)
        const float Cross = 0.15f, Cut = 0.04f;                          // (the loop's cross-fade; the quick fade into the shut-off)

        readonly AudioSource[] _body = new AudioSource[2];
        readonly AudioSource _tail;
        readonly float[] _fade = new float[2];   // (seconds to fade that body voice out over; 0 = at full)
        int _on;

        public TcLaserVoice(Transform parent, AudioClip clip)
        {
            _body[0] = Make(parent, clip, "LASER SOUND A");
            _body[1] = Make(parent, clip, "LASER SOUND B");
            _tail = Make(parent, clip, "LASER SOUND OFF");
        }

        static AudioSource Make(Transform parent, AudioClip clip, string name)
        {
            // Each on its own object: GameAudio's volume filter works on one source per object.
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.loop = false;
            a.playOnAwake = false;
            a.spatialBlend = 0f;
            a.volume = 1f;
            SpatialEmulator.GameAudio.Fit(a);   // (the SFX volume)
            return a;
        }

        public void Start()
        {
            if (!_tail || !_tail.clip) return;
            _tail.Stop();
            foreach (var a in _body) { a.Stop(); a.volume = 1f; }
            _fade[0] = _fade[1] = 0f;
            _on = 0;
            _body[0].time = 0f;
            _body[0].Play();
        }

        public void Stop()
        {
            if (!_tail || !_tail.clip) return;
            for (int i = 0; i < 2; i++) if (_body[i].isPlaying && _fade[i] <= 0f) _fade[i] = Cut;
            _tail.volume = 1f;
            _tail.time = Mathf.Min(Tail, _tail.clip.length - 0.05f);
            _tail.Play();
        }

        public void Update(bool held)
        {
            if (!_tail || !_tail.clip) return;
            // Held past the steady part's end: the other voice takes over from LoopStart, this one fading.
            var now = _body[_on];
            if (held && now.isPlaying && _fade[_on] <= 0f && now.time >= LoopEnd)
            {
                _fade[_on] = Cross;
                _on = 1 - _on;
                var next = _body[_on];
                next.Stop();
                _fade[_on] = 0f;
                next.volume = 1f;
                next.time = LoopStart;
                next.Play();
            }
            for (int i = 0; i < 2; i++)
            {
                if (_fade[i] <= 0f || !_body[i].isPlaying) continue;
                _body[i].volume -= Time.deltaTime / _fade[i];
                if (_body[i].volume <= 0f) { _body[i].Stop(); _body[i].volume = 1f; _fade[i] = 0f; }
            }
        }
    }
}
