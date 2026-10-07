// MobileRetroDepthLayerStack.cs — mobile counterpart to
// RetroDepthLayerStack.cs (desktop). Same visual result (one quad per
// exported layer, stacked along Z), driven by LibretroCore/MobileRetroDepth
// instead of the desktop shared-memory transport.
//
// Intentionally a separate component rather than sharing RetroDepthLayerStack
// via #if branches - the two transports differ enough (process lifecycle,
// pixel-copy path, frame pacing) that merging them would mean threading
// preprocessor branches through nearly every method. A shared interface for
// "a source of layer frames" is the natural next refactor once both paths
// are proven out, not before.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using SpatialEmulator.Demo;
using SpatialEmulator.Games;
using UnityEngine;

namespace SpatialEmulator.Mobile
{
    public class MobileRetroDepthLayerStack : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("World-space gap between adjacent layers when no depth config overrides a layer, in meters. " +
                 "Settings > Layer spacing multiplies it (AppSettings.LayerSpacingScale).")]
        public float layerSpacing = 0.02f;

        /// The spacing in use: layerSpacing times the player's setting, or 0
        /// for a game that plays flat (FlatGames) or one in the 3D CRT cabinet
        /// (CrtCabinet.PlaysFlat).
        public float EffectiveSpacing => FlatGames.Current || CrtCabinet.PlaysFlat || UI.FullScreenTest.Enabled ? 0f : layerSpacing * SpacingScale;
        /// The player's spacing setting, held to MaxSpacingScale if that's set.
        public float SpacingScale => MaxSpacingScale > 0f ? Mathf.Min(AppSettings.LayerSpacingScale, MaxSpacingScale) : AppSettings.LayerSpacingScale;
        /// The most the spacing setting counts for here (0: all of it) - the
        /// CRT cabinet's is less, so the layers don't come out over its controls.
        public float MaxSpacingScale;

        bool _layersVisible = true;

        /// Hides the game's layers (while the game picker shows in the
        /// cabinet's frame).
        public bool LayersVisible
        {
            get => _layersVisible;
            set
            {
                _layersVisible = value;
                for (int i = 0; i < _builtLayers; i++) if (_quads[i]) _quads[i].enabled = value && _revealIn == 0;
            }
        }

        /// How far in front of the stack's origin the top layer sits.
        public float FrontOffset => DepthSteps * EffectiveSpacing;

        /// A game's layer this far apart at the least (0.5 mm), so with no
        /// spacing (flat in the CRT cabinet) they still draw back to front:
        /// transparent quads at one depth draw in no set order.
        const float MinLayerGap = 0.0005f;

        float LayerZ(uint zOrder) => zOrder * Mathf.Max(EffectiveSpacing, MinLayerGap);

        /// The front layer's depth, in steps of the spacing.
        public float DepthSteps
        {
            get
            {
                float top = 0f;
                for (int i = 0; i < _builtLayers; i++) if (_depths[i] > top) top = _depths[i];
                return top;
            }
        }
        float _appliedSpacing = -1f;
        [Tooltip("World-space height of the screen, in meters.")]
        public float screenHeight = 0.4f;
        [Tooltip("Extra horizontal stretch on top of the game's display aspect as reported by the core " +
                 "(4:3 for the arcade monitors); 1 = as on the original monitor.")]
        public float pixelAspect = 1.0f;

        [Header("Rendering")]
        [Tooltip("SpatialEmulator/LayerUnlit. Referenced here rather than only looked up by name so it's included " +
                 "in player builds - Shader.Find returns null on device for a shader nothing in the build references.")]
        public Shader layerShader;

        [Tooltip("The built-in demo game (THE PIT), played in place of a romset (PitDemoGame).")]
        public GameObject demoPrefab;

        // LibretroCore is a process-wide singleton, so only one stack drives
        // it: the first one enabled starts the game and calls RunFrame(), and
        // any extra cabinets mirror the same layers. Otherwise every extra
        // spawn restarted the game and each stack ran a frame, speeding it up.
        static MobileRetroDepthLayerStack s_owner;

        /// While true the core stops running frames (the game picker is open).
        public static bool Paused;

        /// Fired when a game has started (its romset name).
        public static event Action<string> GameStarted;

        /// Emulated frames run since the current game started.
        public static int FramesSinceStart { get; private set; }

        /// The stack driving the core, or null.
        public static MobileRetroDepthLayerStack Active => s_owner;

        /// The picture of a game shown flat (no depth layers), else null.
        public Texture FlatTexture => _flat ? _textures[0] : null;

        // TEMPORARY dev hook: a Documents/stack-settings.txt
        // with lines such as "layerSpacing=0.03" overrides the Layout fields
        // above, re-read whenever the file changes (checked once a second), so
        // the layout can be tuned by eye on the phone by pushing that file
        // over USB - no rebuild or relaunch. Deleting the file keeps the last
        // values until the next launch.
        const string DevSettingsFile = "stack-settings.txt";
        static readonly Dictionary<string, float> s_devSettings = new Dictionary<string, float>();
        static float s_nextDevSettingsCheck;
        static DateTime s_devSettingsWriteTime;
        static int s_devSettingsVersion;
        int _appliedDevSettingsVersion;

        const int MaxLayers = 8;
        const int MaxPixels = 512 * 256; // matches RD_MAX_WIDTH*RD_MAX_HEIGHT
        const int MaxCoreFramesPerUpdate = 3;

        double _frameAccumulator;
        AudioSource _audioSource;
        uint _lastFrame;
        bool _coreUnavailable;
        int _builtLayers;
        /// The layers' quads as built (CrtEffect), and the one shown above the frame (the demo's HUD).
        public int LayerCount => _builtLayers;
        public Renderer LayerQuad(int i) => i >= 0 && i < _builtLayers ? _quads[i] : null;
        public Transform AboveFrameQuad => _aboveFrameQuad;
        Texture2D[] _textures = new Texture2D[MaxLayers];
        uint[] _zOrders = new uint[MaxLayers];
        // How deep each sits, in steps of the spacing (its z-order, or the demo's own Depths).
        readonly float[] _depths = new float[MaxLayers];
        Renderer[] _quads = new Renderer[MaxLayers];
        IntPtr _pixelScratch;   // width*height*4 bytes, sized for the largest board we target
        IntPtr _ownerScratch;   // width*height*2 bytes

        void OnEnable()
        {
            if (!GetComponent<CrtEffect>()) gameObject.AddComponent<CrtEffect>();   // the CRT keycap and look
            _pixelScratch = Marshal.AllocHGlobal(MaxPixels * 4);
            _ownerScratch = Marshal.AllocHGlobal(MaxPixels * 2);

            if (s_owner != null) return;
            s_owner = this;
            GameSelection.Changed += OnGameChanged;
            StartSelectedGame();
        }

        // Loads GameSelection's game (the game picker sets it; it's remembered
        // across launches). With nothing selected the cabinet stays blank.
        void StartSelectedGame()
        {
            string game = GameSelection.Current;
            if (game == PitDemoGame.GameName)
            {
                StartDemo(game);
                return;
            }
            if (_coreUnavailable) return;
            string path = GameSelection.CurrentPath;
            if (path == null)
            {
                Debug.Log($"[MobileRetroDepthLayerStack] no game to load (selected: {game ?? "none"})");
                return;
            }

            Debug.Log("[RD-DIAG] MobileRetroDepthLayerStack starting " + path);
            try
            {
                LibretroCore.Initialize();
            }
            catch (DllNotFoundException e)
            {
                // Expected in the Editor: the core only ships as the iOS framework.
                Debug.LogWarning($"[MobileRetroDepthLayerStack] Emulator core not available on this platform ({e.Message}) - layer stack disabled.");
                _coreUnavailable = true;
                return;
            }

            if (!LibretroCore.StartGame(path))
            {
                Debug.LogError($"[MobileRetroDepthLayerStack] Failed to load: {path}");
                // A failed load leaves the core half set up; start clean next time.
                LibretroCore.Shutdown();
                GameSelection.ReportLoadFailed(game, "The emulator couldn't start this romset");
                return;
            }
            Debug.Log("[RD-DIAG] StartGame succeeded");
            _frameAccumulator = 0;
            FramesSinceStart = 0;
            StartAudio();
            if (CoreThread.WantedFor(game)) CoreThread.Start();
            GameStarted?.Invoke(game);
        }

        // The demo draws its 8 depth groups into render textures; they go on
        // the same quads the emulator's layers use.
        // announce: false for a respawn, which isn't a new game.
        // hidden: build the quads switched off (a respawn shows them once the new run has drawn).
        void StartDemo(string game, bool announce = true, bool hidden = false)
        {
            if (!demoPrefab)
            {
                Debug.LogError("[MobileRetroDepthLayerStack] no demo prefab assigned");
                return;
            }
            if (announce) PitDemoGame.ResetLevels();   // a new game starts at the first level
            var demo = PitDemoGame.Launch(demoPrefab);
            ClearLayers();
            var shader = layerShader != null ? layerShader : Shader.Find("SpatialEmulator/LayerUnlit");
            for (int i = 0; i < demo.Layers.Count && i < MaxLayers; i++)
            {
                var layer = demo.Layers[i];
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"layer{layer.zOrder}_{layer.name}";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0, 0, -layer.depth * EffectiveSpacing);
                if (layer.aboveFrame)
                {
                    _aboveFrameQuad = go.transform;
                    _aboveFrameAspect = layer.texture.width / (float)layer.texture.height;
                }
                _fillFrame[i] = layer.fillFrame;
                if (layer.fillFrame) _fillFrameCount++;
                else go.transform.localScale = QuadScale(layer.texture.width, layer.texture.height);
                var rend = go.GetComponent<Renderer>();
                rend.material = new Material(shader) { mainTexture = layer.texture };
                rend.material.SetFloat("_FlipV", 0f);
                rend.enabled = _layersVisible && !hidden;
                _zOrders[i] = layer.zOrder;
                _depths[i] = layer.depth;
                _quads[i] = rend;
                _builtLayers = i + 1;
            }
            _frame = transform.parent ? transform.parent.GetComponentInChildren<UI.ScreenFrame>(true) : null;
            PlaceAboveFrame();
            GrowFarLayers();   // not a frame at the quads' default size
            FramesSinceStart = 0;
            if (announce) GameStarted?.Invoke(game);
        }

        Transform _aboveFrameQuad;
        float _aboveFrameAspect = 3f;
        UI.ScreenFrame _frame;
        [Tooltip("Gap between the cabinet frame's top and the demo's health bar, in metres (cabinet scale 1).")]
        public float aboveFrameGap = 0.01f;

        // The demo's health bar and score sit above the frame's top edge on
        // the frame's own layer, centred, aboveFrameWidth times as wide as
        // the frame.
        [Tooltip("The demo's health bar width, relative to the cabinet frame's.")]
        public float aboveFrameWidth = 1f;

        void PlaceAboveFrame()
        {
            if (!_aboveFrameQuad) return;
            float z = _frame ? _frame.transform.localPosition.z - transform.localPosition.z - 0.001f : 0f;
            float top = _frame ? _frame.OuterHalfHeight : screenHeight * 0.5f;
            float halfWidth = _frame ? _frame.OuterHalfWidth : screenHeight * 2f / 3f;
            float grow = _frame ? _frame.Grow : 1f;
            float width = halfWidth * 2f * aboveFrameWidth;
            float height = width / _aboveFrameAspect;
            _aboveFrameQuad.localScale = new Vector3(width, height, 1);
            _aboveFrameQuad.localPosition = new Vector3(0, top + aboveFrameGap * grow + height * 0.5f, z);
        }

        // The demo's far layers grow like the frame does (ScreenFrame: seen
        // from its viewDistance, a layer further back looks smaller), so the
        // sky, sun and mountains keep filling the frame's opening.
        /// The widest a growing far layer may get, in the stack's metres (0: no
        /// limit) - CrtCabinet keeps them inside its 3D cabinet's walls.
        public float MaxFarLayerWidth;

        void GrowFarLayers()
        {
            if (_fillFrameCount == 0) return;
            float view = _frame ? _frame.viewDistance : 0.7f;
            float front = FrontOffset;
            float spacing = EffectiveSpacing;
            for (int i = 0; i < _builtLayers; i++)
            {
                if (!_fillFrame[i] || !_quads[i]) continue;
                float grow = (view + front - _depths[i] * spacing) / view;
                var baseScale = QuadScale(4, 3);
                if (MaxFarLayerWidth > 0f) grow = Mathf.Min(grow, Mathf.Max(1f, MaxFarLayerWidth / baseScale.x));
                _quads[i].transform.localScale = new Vector3(baseScale.x * grow, baseScale.y * grow, 1);
            }
        }

        /// Where a ray from the room meets a layer's quad (the demo's layers
        /// are named for their depth group), in its picture's 0-1
        /// coordinates; false if it misses the picture.
        public bool LayerPoint(Ray ray, string layerName, out Vector2 uv)
        {
            uv = default;
            for (int i = 0; i < _builtLayers; i++)
            {
                var quad = _quads[i];
                if (!quad || !quad.enabled || !quad.name.EndsWith("_" + layerName)) continue;
                var t = quad.transform;
                if (!new Plane(t.forward, t.position).Raycast(ray, out float distance)) return false;
                var local = t.InverseTransformPoint(ray.GetPoint(distance));
                uv = new Vector2(local.x + 0.5f, local.y + 0.5f);
                return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
            }
            return false;
        }

        readonly bool[] _fillFrame = new bool[MaxLayers];
        int _fillFrameCount;

        // Full core restart, not just unload + load: libretro-mame freezes if
        // a game is loaded again after retro_unload_game without a
        // retro_deinit/retro_init in between (harness RELOAD test).
        void OnGameChanged()
        {
            if (s_owner != this) return;
            StopCore();
            ClearLayers();
            StartSelectedGame();
        }

        void StopCore()
        {
            DropRetired();
            PitDemoGame.Stop();
            if (_audioSource) _audioSource.Stop();
            LibretroCore.Shutdown();
        }

        // Plain stereo for now (spatialBlend 0); 1 would position the sound
        // at the cabinet. The components are reused across disable/enable
        // rather than destroyed, since MobileRetroAudioOutput requires the
        // AudioSource.
        void StartAudio()
        {
            MobileRetroAudio.Configure(LibretroCore.SampleRate, AudioSettings.outputSampleRate);
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f;
                gameObject.AddComponent<MobileRetroAudioOutput>();
            }
            _audioSource.Play();
            Debug.Log($"[RD-DIAG] audio started: core {LibretroCore.SampleRate} Hz -> output {AudioSettings.outputSampleRate} Hz");
        }

        void OnDisable()
        {
            if (s_owner == this)
            {
                // Full shutdown (see OnGameChanged); the next cabinet's
                // OnEnable re-initializes.
                GameSelection.Changed -= OnGameChanged;
                StopCore();
                s_owner = null;
            }

            ClearLayers();

            if (_pixelScratch != IntPtr.Zero) { Marshal.FreeHGlobal(_pixelScratch); _pixelScratch = IntPtr.Zero; }
            if (_ownerScratch != IntPtr.Zero) { Marshal.FreeHGlobal(_ownerScratch); _ownerScratch = IntPtr.Zero; }
        }

        // Re-spaces the layers as soon as the setting changes - also while the
        // game is paused behind the Settings screen, where no new frames come.
        void ApplySpacing()
        {
            float spacing = EffectiveSpacing;
            if (Mathf.Approximately(spacing, _appliedSpacing)) return;
            _appliedSpacing = spacing;
            for (int i = 0; i < _builtLayers; i++)
                if (_quads[i])
                {
                    var p = _quads[i].transform.localPosition;
                    _quads[i].transform.localPosition = new Vector3(p.x, p.y, -_depths[i] * spacing);
                }
        }

        const int RevealFrames = 3;
        int _revealIn;
        readonly List<Renderer> _retiring = new List<Renderer>();

        // A respawn's old quads, kept showing until RevealNewRun on copies of
        // their last picture (the demo's own textures go with it).
        void RetireLayers()
        {
            for (int i = 0; i < _builtLayers; i++)
            {
                var quad = _quads[i];
                if (!quad) continue;
                if (quad.sharedMaterial && quad.sharedMaterial.mainTexture is RenderTexture source && source.IsCreated())
                {
                    var copy = new RenderTexture(source.descriptor) { filterMode = source.filterMode, wrapMode = source.wrapMode };
                    copy.Create();
                    Graphics.CopyTexture(source, copy);
                    quad.sharedMaterial.mainTexture = copy;
                }
                _retiring.Add(quad);
                _quads[i] = null;
            }
            ClearLayers();
        }

        void RevealNewRun()
        {
            for (int i = 0; i < _builtLayers; i++) if (_quads[i]) _quads[i].enabled = _layersVisible;
            DropRetired();
        }

        void DropRetired()
        {
            _revealIn = 0;
            foreach (var quad in _retiring)
            {
                if (!quad) continue;
                var material = quad.sharedMaterial;
                var texture = material ? material.mainTexture as RenderTexture : null;
                if (texture) { texture.Release(); Destroy(texture); }
                if (material) Destroy(material);
                Destroy(quad.gameObject);
            }
            _retiring.Clear();
        }

        void ClearLayers()
        {
            _flat = false;
            _flatShown = -1;
            _aboveFrameQuad = null;
            _fillFrameCount = 0;
            System.Array.Clear(_fillFrame, 0, _fillFrame.Length);
            for (int i = 0; i < _builtLayers; i++)
            {
                if (_quads[i]) Destroy(_quads[i].gameObject);
                if (_textures[i]) Destroy(_textures[i]);
                _quads[i] = null;
                _textures[i] = null;
            }
            _builtLayers = 0;
            _lastFrame = 0;
        }

        void Update()
        {
            ApplyDevSettings();
            ApplySpacing();

            if (PitDemoGame.IsRunning)
            {
                // The demo's run is over (DeathRespawn): start it again, fresh.
                // The old run's last picture stays up until the new one has
                // drawn and fitted its HUD (a fresh layer texture is blank, and
                // the HUD is fitted in the demo's first Update).
                if (s_owner == this && DeathRespawn.RestartRequested)
                {
                    DeathRespawn.RestartRequested = false;
                    RetireLayers();
                    PitDemoGame.Stop();
                    StartDemo(PitDemoGame.GameName, announce: false, hidden: true);
                    _revealIn = RevealFrames;
                    return;
                }
                if (_revealIn > 0 && --_revealIn == 0) RevealNewRun();
                if (s_owner == this) PitDemoGame.Paused = Paused;
                PlaceAboveFrame();
                GrowFarLayers();
                return;
            }
            if (!LibretroCore.IsRunning) return;

            // On its own thread (CoreThread), the game runs by itself: the
            // newest frame it's finished is what's drawn.
            CoreThread.Frame threaded = null;
            if (CoreThread.Running)
            {
                CoreThread.Paused = s_owner != this || Paused;
                FramesSinceStart = CoreThread.Frames;
                threaded = CoreThread.TakeFrame();
            }
            else if (s_owner == this && !Paused)
            {
                long started = UI.PerfReadout.Now;
                int before = FramesSinceStart;
                RunCoreFrames();
                UI.PerfReadout.Core(started, FramesSinceStart - before);
            }

            // Full screen shows the whole frame itself: no layers to draw.
            if (UI.FullScreenTest.Enabled) return;
            if (CoreThread.Running && threaded == null) return;   // (nothing new)

            // A driver without depth layers (yet): its whole picture, flat.
            int count = threaded != null ? threaded.count : MobileRetroDepth.LayerCount;
            if (count == 0) { ShowFlat(); return; }

            uint frame = threaded != null ? threaded.id : MobileRetroDepth.FrameId;
            if (frame == _lastFrame) return;
            _lastFrame = frame;

            if (count != _builtLayers || _flat) Rebuild(count, threaded);

            long uploading = UI.PerfReadout.Now;
            for (int i = 0; i < count; i++)
            {
                if (!GetLayer(i, threaded, out var layer)) continue;

                var tex = _textures[i];
                if (tex == null || tex.width != layer.Width || tex.height != layer.Height)
                {
                    Rebuild(count, threaded);
                    tex = _textures[i];
                }

                int pixelBytes = layer.Width * layer.Height * 4;
                if (threaded != null)
                {
                    if (threaded.pixels[i] == null || threaded.pixels[i].Length != pixelBytes) continue;
                    tex.LoadRawTextureData(threaded.pixels[i]);
                }
                else
                {
                    if (!MobileRetroDepth.TryGetLayerPixels(i, _pixelScratch, pixelBytes)) continue;
                    tex.LoadRawTextureData(_pixelScratch, pixelBytes);
                }
                tex.Apply(false, false);

                // Layers can reorder mid-game (same dynamic z-order behavior
                // as the desktop CPS1/CPS2 fix), so keep Z in sync every frame.
                _zOrders[i] = layer.ZOrder;
                _depths[i] = layer.ZOrder;
                var t = _quads[i].transform;
                float z = LayerZ(layer.ZOrder);
                if (!Mathf.Approximately(t.localPosition.z, -z))
                    t.localPosition = new Vector3(0, 0, -z);
            }
            UI.PerfReadout.Upload(uploading, count);
        }

        // Runs the core at its own rate (59.64 Hz for CPS1) rather than once
        // per rendered frame, so game speed and audio output keep up when
        // Unity's frame rate drops (e.g. to 30fps under thermal throttling) or
        // runs above the game's (120 Hz). A display up to 1% faster than the
        // game (60 Hz vs 59.64 Hz) still gets exactly one core frame per
        // rendered frame, keeping motion smooth; MobileRetroAudio's rate
        // control absorbs the resulting ~0.6% of extra audio.
        void RunCoreFrames()
        {
            double frameTime = LibretroCore.Fps > 0 ? 1.0 / LibretroCore.Fps : 1.0 / 60.0;
            _frameAccumulator += Time.unscaledDeltaTime;
            int runs = 0;
            while (_frameAccumulator >= frameTime * 0.99 && runs < MaxCoreFramesPerUpdate)
            {
                LibretroCore.RunFrame();
                FramesSinceStart++;
                _frameAccumulator -= frameTime;
                runs++;
            }
            // Negative means the display is slightly faster than the game: don't
            // bank that, or a frame would periodically be skipped. Still a frame
            // or more behind after the cap means a hitch: drop the backlog
            // rather than fast-forward through it.
            if (_frameAccumulator < 0 || _frameAccumulator >= frameTime)
                _frameAccumulator = 0;
        }

        /// The current frame as one picture (the layers composited back to
        /// front, transparent pixels skipped), shrunk by downscale; for save
        /// thumbnails. Null before the first frame.
        public Texture2D CaptureScreen(int downscale = 2)
        {
            if (_builtLayers == 0 || !_textures[0]) return null;
            int w = _textures[0].width, h = _textures[0].height;
            var order = new List<int>();
            for (int i = 0; i < _builtLayers; i++)
                if (_textures[i] && _textures[i].width == w && _textures[i].height == h) order.Add(i);
            order.Sort((a, b) => _zOrders[a].CompareTo(_zOrders[b]));

            var full = new Color32[w * h];
            foreach (int i in order)
            {
                var px = _textures[i].GetPixels32();
                for (int k = 0; k < px.Length; k++)
                    if (px[k].a > 0) full[k] = px[k];
            }
            int tw = w / downscale, th = h / downscale;
            var small = new Color32[tw * th];
            // The core's rows are top-down (LayerUnlit flips V to show them);
            // a texture's are bottom-up, so flip for the thumbnail.
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                {
                    var c = full[(y * downscale) * w + x * downscale];
                    c.a = 255;
                    small[(th - 1 - y) * tw + x] = c;
                }
            var thumb = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            thumb.SetPixels32(small);
            thumb.Apply();
            return thumb;
        }

        // ---- a game without depth layers: one quad with the core's picture ----

        bool _flat;
        int _flatShown = -1;

        void ShowFlat()
        {
            var pixels = LibretroCore.FlatPixels;
            if (pixels == null || LibretroCore.FlatFrame == _flatShown) return;
            _flatShown = LibretroCore.FlatFrame;
            int w = LibretroCore.FlatWidth, h = LibretroCore.FlatHeight;
            if (pixels.Length != w * h * 4) return;   // (mid-change of size, on CoreThread)
            if (!_flat || _builtLayers != 1 || !_textures[0] || _textures[0].width != w || _textures[0].height != h)
            {
                ClearLayers();
                var shader = layerShader != null ? layerShader : Shader.Find("SpatialEmulator/LayerUnlit");
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "layer0_Screen";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                go.transform.localScale = QuadScale(w, h);
                var tex = new Texture2D(w, h, TextureFormat.BGRA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var rend = go.GetComponent<Renderer>();
                rend.material = new Material(shader) { mainTexture = tex };
                rend.enabled = _layersVisible;
                _textures[0] = tex;
                _quads[0] = rend;
                _zOrders[0] = 0;
                _depths[0] = 0;
                _builtLayers = 1;
                _flat = true;
            }
            long uploading = UI.PerfReadout.Now;
            _textures[0].LoadRawTextureData(pixels);
            _textures[0].Apply(false, false);
            UI.PerfReadout.Upload(uploading, 1);
        }

        // A layer's description: from the thread's copy of the frame, or
        // straight from the core.
        static bool GetLayer(int index, CoreThread.Frame frame, out MobileRdLayer layer)
        {
            if (frame == null) return MobileRetroDepth.TryGetLayer(index, out layer);
            layer = frame.layers[index];
            return index < frame.count;
        }

        void Rebuild(int count, CoreThread.Frame threaded = null)
        {
            _flat = false;
            for (int i = 0; i < _builtLayers; i++)
            {
                if (_quads[i]) Destroy(_quads[i].gameObject);
                if (_textures[i]) Destroy(_textures[i]);
            }

            var shader = layerShader != null ? layerShader : Shader.Find("SpatialEmulator/LayerUnlit");
            for (int i = 0; i < count; i++)
            {
                if (!GetLayer(i, threaded, out var layer)) continue;

                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"layer{layer.ZOrder}_{layer.Name}";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0, 0, -LayerZ(layer.ZOrder));
                _zOrders[i] = layer.ZOrder;
                _depths[i] = layer.ZOrder;
                go.transform.localScale = QuadScale(layer.Width, layer.Height);
                Turn(go.transform);

                var tex = new Texture2D(layer.Width, layer.Height, TextureFormat.BGRA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                };

                var rend = go.GetComponent<Renderer>();
                rend.material = new Material(shader) { mainTexture = tex };

                _textures[i] = tex;
                rend.enabled = _layersVisible;
                _quads[i] = rend;
            }
            _builtLayers = count;
        }

        // A vertical game's layers come out of its driver as its video chips
        // draw them - on their side; MAME turns only its finished picture.
        // Each layer's quad is turned upright here (the monitor's rotation,
        // clockwise), sized so that, turned, it's the game's portrait shape
        // (QuadScale's width and height swapped). The flat picture (ShowFlat)
        // is MAME's own, already upright.
        static int Rotation
        {
            get
            {
                var game = GameCatalog.Find(GameSelection.Current);
                return game != null ? game.rotation : 0;
            }
        }

        /// The game's on a turned monitor: shown tall.
        public static bool Vertical => !PitDemoGame.IsRunning && Rotation % 180 == 90;

        /// The picture's shape as shown, width over height: 4:3 for most
        /// games, 3:4 for a vertical one (ScreenFrame, CrtCabinet and the
        /// saves panel size themselves to it).
        public float PictureAspect
        {
            get
            {
                float aspect = PitDemoGame.IsRunning ? 4f / 3f : LibretroCore.AspectRatio > 0 ? (float)LibretroCore.AspectRatio : 4f / 3f;
                if (Vertical && aspect > 1f) aspect = 1f / aspect;
                return aspect;
            }
        }

        void Turn(Transform quad)
        {
            int rotation = Rotation;
            if (rotation == 0 || PitDemoGame.IsRunning) return;
            quad.localRotation = Quaternion.Euler(0f, 0f, -rotation);   // (+z is anticlockwise, seen from the front)
            if (rotation == 180) return;
            var s = quad.localScale;
            quad.localScale = new Vector3(s.y, s.x, s.z);
        }

        // Sized to the game's display aspect (the core reports 4:3 for all
        // three boards), not the raw pixel grid - 384x224 as-is would be
        // 1.71:1, ~29% too wide. Falls back to square pixels if the core
        // reports no aspect.
        Vector3 QuadScale(int width, int height)
        {
            float aspect = PitDemoGame.IsRunning ? 4f / 3f
                : LibretroCore.AspectRatio > 0 ? (float)LibretroCore.AspectRatio : width / (float)height;
            // A vertical game's shown tall, whichever shape the core reports
            // (its layers' quads are turned by Turn, which swaps the two).
            if (!PitDemoGame.IsRunning && Rotation % 180 == 90 && aspect > 1f) aspect = 1f / aspect;
            return new Vector3(screenHeight * aspect * pixelAspect, screenHeight, 1);
        }

        void ApplyDevSettings()
        {
            PollDevSettings();
            if (_appliedDevSettingsVersion == s_devSettingsVersion) return;
            _appliedDevSettingsVersion = s_devSettingsVersion;

            if (s_devSettings.TryGetValue("layerSpacing", out float spacing)) layerSpacing = spacing;
            if (s_devSettings.TryGetValue("screenHeight", out float height)) screenHeight = height;
            if (s_devSettings.TryGetValue("pixelAspect", out float stretch)) pixelAspect = stretch;

            // Spacing is applied by Update's per-frame Z sync; size needs a rescale.
            for (int i = 0; i < _builtLayers; i++)
                if (_quads[i] && _textures[i])
                    _quads[i].transform.localScale = QuadScale(_textures[i].width, _textures[i].height);
        }

        static void PollDevSettings()
        {
            if (Time.unscaledTime < s_nextDevSettingsCheck) return;
            s_nextDevSettingsCheck = Time.unscaledTime + 1f;

            string file = Path.Combine(Application.persistentDataPath, DevSettingsFile);
            DateTime written = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : default;
            if (written == s_devSettingsWriteTime) return;
            s_devSettingsWriteTime = written;

            s_devSettings.Clear();
            if (written != default)
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    string[] parts = line.Split('=');
                    if (parts.Length == 2 && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                        s_devSettings[parts[0].Trim()] = value;
                }
            }
            s_devSettingsVersion++;

            var applied = new List<string>();
            foreach (var kv in s_devSettings) applied.Add(kv.Key + "=" + kv.Value.ToString(CultureInfo.InvariantCulture));
            Debug.Log("[RD-DIAG] stack-settings.txt applied: " + (applied.Count > 0 ? string.Join(", ", applied) : "(none)"));
        }
    }
}
