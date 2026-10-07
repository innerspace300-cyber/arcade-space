// PitDemoGame.cs — the built-in demo game ("ENDLESS KNIGHT", from Wayne's
// THE PIT project, Assets/PitDemo): a real Unity scene played in the
// cabinet in place of an emulated romset, so the app shows its depth effect
// with no ROMs.
//
// The demo's world is spawned far below the room (Origin) and drawn by one
// orthographic camera per depth group, each into its own 4:3 texture with a
// clear background, back to front: sky, sun, mountains, far palms, near
// palms, then the road with everything on it (player, enemies, items, the
// bonfire and witch - one layer, so feet stay on the ground at any
// spacing), and in front of all of them the HUD - the health bar and score
// on their purple band at the bottom of the picture, and the dialogue boxes
// - on a layer of its own, so it spaces out from the characters like the
// rest. MobileRetroDepthLayerStack shows those textures on its layer
// quads like the emulator's layers, so the frame, spacing and controls all
// work unchanged. The layers grow
// with the frame so they keep filling its opening as the layers spread,
// each staying in scale with the rest (Layer.fillFrame).
//
// Controls come from ArcadeInput (touch controls or a game controller):
// D-pad walks (up/down = away/toward), A attacks, B jumps, C is Holy Slash,
// START is Great Heal.

using System.Collections.Generic;
using SpatialEmulator.Controls;
using SpatialEmulator.Games;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SpatialEmulator.Demo
{
    public class PitDemoGame : MonoBehaviour
    {
        public const string GameName = "thepit";

        /// The demo's entry in the game list (always installed).
        public static readonly CatalogGame Info = new CatalogGame
        {
            name = GameName,
            parent = "",
            year = "2026",
            company = "Wayne Lamb",
            description = "ENDLESS KNIGHT",
            board = "demo",
            crcs = new uint[0],
        };

        public struct Layer
        {
            public string name;
            public uint zOrder;
            /// How deep it sits, in steps of the layer spacing (the emulator's
            /// layers are their z-order apart, one step each; the demo's, Depths).
            public float depth;
            public RenderTexture texture;
            /// Shown above the cabinet frame (the health bar and score), on the
            /// frame's own layer, not on the screen.
            public bool aboveFrame;
            /// Grown with the cabinet frame as the layers spread apart, so it
            /// keeps filling the frame's opening (the far background).
            public bool fillFrame;
        }

        /// Where the demo's world lives: well away from the room and the AR camera.
        static readonly Vector3 Origin = new Vector3(0f, -500f, 0f);

        const int TextureHeight = 320;          // 2x the background's 160 px
        const float Aspect = 4f / 3f;           // an arcade monitor, like the other games

        // Back to front. The characters' camera also sees THE PIT's physics
        // layers and Default (spawned enemies, items and effects keep theirs).
        static readonly string[] GroupLayers =
            { "PitSky", "PitSun", "PitMountains", "PitFarPalms", "PitNearPalms", "PitActors", HudLayer };
        /// Each depth group's depth, in steps of the layer spacing: the far
        /// ones close together, the gaps growing toward the front - widest
        /// between the road (the knight) and the HUD. As deep overall as six
        /// even steps.
        static readonly float[] Depths = { 0f, 0.48f, 1.08f, 1.8f, 2.76f, 4.2f, 6f };

        /// The knight and the witch, drawn this much bigger than THE PIT's art
        /// (from their feet; the sidewalk and everything else as they were).
        public const float CharacterScale = 1.25f;

        void ScaleUpCharacters()
        {
            if (_player) _player.ScaleSprite(CharacterScale);
            var witch = transform.Find("WITCH");
            var witchArt = witch ? witch.GetComponent<SpriteRenderer>() : null;
            if (witchArt)
            {
                float feet = witchArt.bounds.min.y;
                witch.localScale = Vector3.Scale(witch.localScale, new Vector3(CharacterScale, CharacterScale, 1f));
                witch.position += Vector3.up * (feet - witchArt.bounds.min.y);
            }
            // The witch who comes for the knight after a death, too.
            var respawn = GetComponentInChildren<DeathRespawn>(true);
            if (respawn) respawn.witchScale *= CharacterScale;
        }

        /// How far below the road line the knight walks, in her pixels (the
        /// art's, before CharacterScale): down on the sidewalk, her shadow,
        /// spells and lightning with her (SpikeTrap.PlayerDrop).
        public const float PlayerDropPixels = 10f;

        void DropPlayer()
        {
            SpikeTrap.PlayerDrop = 0f;
            var body = _player ? _player.transform.Find("ARIANA") : null;
            var art = body ? body.GetComponent<SpriteRenderer>() : null;
            if (!art || !art.sprite) return;
            float pixel = art.bounds.size.x / art.sprite.rect.width / CharacterScale;
            float drop = PlayerDropPixels * pixel;
            _player.transform.position += Vector3.down * drop;
            _player.OffsetLimits(Vector3.down * drop);   // (her walk limits come along: before her Start, which reads her place)
            SpikeTrap.PlayerDrop = drop;
        }

        /// The front layer: the HUD, the dialogue boxes, the fruit rain.
        public const string HudLayer = "PitHUD";
        static readonly string[] ActorExtraLayers = { "Default", "TransparentFX", "PLAYER", "ENEMY", "FOOD" };

        // synthwave.ase's layers -> depth group.
        static readonly (string layer, string group)[] BackgroundLayers =
        {
            ("back", "PitSky"), ("sun", "PitSun"), ("mountains", "PitMountains"),
            ("palms back", "PitFarPalms"), ("palms", "PitNearPalms"), ("road", "PitActors"),
        };

        public static PitDemoGame Running { get; private set; }
        public static bool IsRunning => Running != null;

        public readonly List<Layer> Layers = new List<Layer>();

        PlayerScriptARIANAClips _player;
        /// The knight.
        public PlayerScriptARIANAClips Player => _player;
        ParallaxSpawnSystem _enemies;
        CollectableSpawnSystem _collectables;
        readonly List<Camera> _cameras = new List<Camera>();
        bool _attackHeld, _jumpHeld, _slashHeld, _healHeld;
        float _nextAudioCheck;
        bool _paused;

        // ---- Levels (LevelPortal) ----

        /// The level being played (1 = the demo prefab), and its prefab past
        /// the first: a death or restart plays it again; a new game, or
        /// RESTART's wipe, goes back to the first.
        public static int Level { get; private set; } = 1;
        public static GameObject LevelPrefab { get; private set; }
        static int s_carryScore;

        /// Through the portal: the next run is `prefab`, level `level`,
        /// starting with this run's score.
        public static void GoToLevel(GameObject prefab, int level)
        {
            LevelPrefab = prefab;
            Level = level;
            s_carryScore = PitScoreManager.Instance ? PitScoreManager.Instance.GetScore() : 0;
            DeathRespawn.RestartRequested = true;
        }

        /// Back to the first level (a new game, or progress wiped).
        public static void ResetLevels()
        {
            LevelPrefab = null;
            Level = 1;
            s_carryScore = 0;
        }

        /// While true nothing reaches the knight (she's stepping into the portal).
        public static bool ControlsLocked;

        public static PitDemoGame Launch(GameObject prefab)
        {
            Stop();
            PlayerBuffs.Reset();
            ControlsLocked = false;
            var root = Instantiate(LevelPrefab ? LevelPrefab : prefab, Origin, Quaternion.identity);
            root.name = "PitDemo (running)";
            Running = root.AddComponent<PitDemoGame>();
            Running.Setup();
            DemoMusic.Ensure();   // (the soundtrack, carried on through restarts and levels)
            // The player's walk limits are world positions from THE PIT's scene.
            if (Running._player) Running._player.OffsetLimits(Origin);
            // Come through the portal: the score comes too (counted as a
            // bonus, so the bosses come at their usual points in the new level).
            if (s_carryScore > 0 && PitScoreManager.Instance)
            {
                PitScoreManager.Instance.AddScore(s_carryScore);
                TreasureChest.BonusPoints += s_carryScore;
            }
            s_carryScore = 0;
            return Running;
        }

        public static void Stop()
        {
            if (!Running) return;
            var game = Running;
            Running = null;
            Time.timeScale = 1f;
            PitInput.Move = Vector2.zero;
            // Spawned enemies and items live at the scene root, not under the demo.
            if (game._enemies) game._enemies.ClearActiveObjects();
            if (game._collectables) game._collectables.ClearActiveCollectables();
            foreach (var layer in game.Layers) if (layer.texture) { layer.texture.Release(); Destroy(layer.texture); }
            // Immediately, not at the end of the frame: a restart launches the
            // new copy right away, and THE PIT's singletons (PitScoreManager)
            // would otherwise find the old one still there and remove themselves.
            DestroyImmediate(game.gameObject);
            // Everything else it spawned at the scene's root down in its
            // world (chests, their gems and fruit, effects, strikes): a
            // restart brings back only what the new copy makes.
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (Mathf.Abs(root.transform.position.y - Origin.y) < 250f) DestroyImmediate(root);
        }

        /// Freezes the game (Time.timeScale; ARcade's own UI runs on unscaled time).
        public static bool Paused
        {
            set
            {
                if (!Running || Running._paused == value) return;
                Running._paused = value;
                // (The RESTART warning keeps it frozen until it's answered.)
                Time.timeScale = value || DemoDialogue.Freezing ? 0f : 1f;
            }
        }

        public static bool IsPaused => Running && Running._paused;

        void Setup()
        {
            _player = GetComponentInChildren<PlayerScriptARIANAClips>(true);
            _enemies = GetComponentInChildren<ParallaxSpawnSystem>(true);
            _collectables = GetComponentInChildren<CollectableSpawnSystem>(true);

            AssignGroups();
            BuildCameras();

            // The room's AR camera must not see the demo's depth groups.
            int pitMask = 0;
            foreach (string name in GroupLayers) pitMask |= 1 << LayerMask.NameToLayer(name);
            foreach (var cam in Camera.allCameras)
                if (!_cameras.Contains(cam)) cam.cullingMask &= ~pitMask;

            FlattenAudio();
            SpellWheel.Find();   // on the controls' C button
            gameObject.AddComponent<HudStyle>();   // the HUD's line work in the cabinet's style
            // Cast shadows on the road: the knight and the witch (bomb guys
            // and bosses get theirs as they come).
            ScaleUpCharacters();
            DropPlayer();
            var knightShadow = _player && _player.transform.Find("ARIANA") ? SpriteShadow.Cast(_player.transform.Find("ARIANA").GetComponent<SpriteRenderer>()) : null;
            if (knightShadow)
            {
                knightShadow.raisePixels = 3f;      // right up under her boots,
                knightShadow.fromRoadLine = true;   // even when a spell's rings are drawn below them
                knightShadow.belowRoad = SpikeTrap.PlayerDrop;   // (her line, down on the sidewalk)
                var knight = _player;
                knightShadow.fromDrawnBottom = () => knight && knight.IsHealing;   // (right under Great Heal's plate)
            }
            // REST over her head when she could do with it (C sits her down).
            var hud = GetComponentInChildren<HudJuice>(true);
            RestPrompt.Attach(_player, hud && hud.healthText ? hud.healthText.font : null);
            var witch = transform.Find("WITCH");
            if (witch) SpriteShadow.Cast(witch.GetComponentInChildren<SpriteRenderer>());
            // The campfire's too, from the bottom of its flames.
            var fire = transform.Find("BONFIRE");
            var fireShadow = fire ? SpriteShadow.Cast(fire.GetComponent<SpriteRenderer>()) : null;
            if (fireShadow) fireShadow.measurePixels = true;
            // The HUD's bars take their (fixed) lengths in their first Update;
            // it's shown from the end of that frame (LateUpdate), never short.
            SetHudShown(false);
            // The witch's welcome and HOW TO PLAY, the first time.
            DemoDialogue.ShowIntro();
        }

        void AssignGroups()
        {
            int actors = LayerMask.NameToLayer("PitActors");

            // Everything drawn by default goes with the characters, keeping
            // THE PIT's own physics layers (PLAYER / ENEMY / FOOD).
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.gameObject.layer == 0) t.gameObject.layer = actors;

            // The background's Aseprite layers.
            if (_player && _player.backgroundAnimator)
            {
                var bg = _player.backgroundAnimator.transform;
                foreach (var r in bg.GetComponentsInChildren<Renderer>(true))
                    r.gameObject.layer = LayerMask.NameToLayer(GroupFor(r.gameObject.name));
            }

            // Scenery props sit on the road they drift with.
            foreach (string prop in new[] { "BONFIRE", "WITCH" })
            {
                var t = transform.Find(prop);
                if (t) SetLayer(t, actors);
            }

            // The ground only holds the physics floor.
            var ground = transform.Find("GROUND PLANE");
            if (ground && ground.TryGetComponent<Renderer>(out var groundRenderer)) groundRenderer.enabled = false;

            // Health bar and score: the front layer, their own.
            var hudRoot = transform.Find("Responsive Health Bar ARK");
            if (hudRoot)
            {
                SetLayer(hudRoot, LayerMask.NameToLayer(HudLayer));
                // THE PIT's hidden debug stat buttons: their panel's backing
                // image still draws. (The panel's StatsControl stays - damage
                // goes through it.)
                var panel = hudRoot.Find("Canvas/GUI/ControlPanel");
                if (panel && panel.TryGetComponent<UnityEngine.UI.Image>(out var panelImage)) panelImage.enabled = false;
            }
        }

        static string GroupFor(string objectName)
        {
            string n = objectName.ToLowerInvariant();
            // Longest names first: "palms back" before "palms".
            foreach (var (layer, group) in BackgroundLayers)
                if (layer == "palms back" && n.Contains(layer)) return group;
            foreach (var (layer, group) in BackgroundLayers)
                if (n.Contains(layer)) return group;
            return "PitSky"; // a single flattened background
        }

        static void SetLayer(Transform root, int layer)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        void BuildCameras()
        {
            // THE PIT is viewed from above in AR, where perspective puts the
            // characters' feet on the road. Straight on, like an arcade game,
            // they'd float above it, so the background moves down until the
            // road is under the player's feet. The picture is then the whole
            // background (its 240 px canvas wide, down to the road's front
            // face), widened to fill 4:3 - THE PIT already stretches it
            // (1.37 x 1.48).
            Bounds view = new Bounds(transform.position, Vector3.one);
            if (_player && _player.backgroundAnimator)
            {
                var bg = _player.backgroundAnimator.transform;
                var merged = bg.GetComponent<SpriteRenderer>();
                var road = bg.Find("BG road") ? bg.Find("BG road").GetComponent<SpriteRenderer>() : null;
                var body = _player.transform.Find("ARIANA") ? _player.transform.Find("ARIANA").GetComponent<SpriteRenderer>() : null;
                if (merged && road && body)
                {
                    float pixel = merged.bounds.size.y / merged.sprite.rect.height;   // one background pixel
                    float feet = body.bounds.min.y;
                    bg.position += Vector3.up * (feet - (road.bounds.max.y - 6f * pixel));
                    StretchSidewalk(road);
                }
                view = merged ? merged.bounds : new Bounds(bg.position, Vector3.one);
                // A little more below the road, for a taller HUD band.
                view.SetMinMax(view.min - Vector3.up * view.size.y * HudBandExtra, view.max);
                // The deeper sidewalk: the picture reaches that much further
                // down - the scene and all the sky still in it, a little
                // smaller - keeping the HUD band (fixed on the screen) the
                // same share of it, its top on the sidewalk's bottom edge.
                if (_sidewalkExtra > 0f && road)
                {
                    float surfaceBottom = road.bounds.max.y - RoadSurfaceRows * road.bounds.size.y / road.sprite.rect.height;
                    float band = (surfaceBottom + _sidewalkExtra - view.min.y) / view.size.y;   // the HUD's share, as it was
                    float tall = (view.max.y - surfaceBottom) / (1f - band);
                    view.SetMinMax(new Vector3(view.min.x, view.max.y - tall, view.min.z), view.max);
                }
                float canvasWidth = merged ? view.size.x * 240f / merged.sprite.rect.width : view.size.x;
                float widen = view.size.y * Aspect / canvasWidth;
                var s = bg.localScale;
                bg.localScale = new Vector3(s.x * widen, s.y, s.z);
                // The canvas is centred on the sprite's pivot.
                view.center = new Vector3(bg.position.x, view.center.y, view.center.z);
            }
            int height = TextureHeight;
            int width = Mathf.RoundToInt(height * Aspect);

            var rig = new GameObject("Demo Cameras").transform;
            rig.SetParent(transform, false);
            rig.position = new Vector3(view.center.x, view.center.y, view.min.z - 5f);

            for (int i = 0; i < GroupLayers.Length; i++)
            {
                var rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
                {
                    name = "Pit " + GroupLayers[i],
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                };
                rt.Create();

                var go = new GameObject("Camera " + GroupLayers[i]);
                go.transform.SetParent(rig, false);
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = view.extents.y;
                cam.aspect = Aspect;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 50f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.useOcclusionCulling = false;
                int mask = 1 << LayerMask.NameToLayer(GroupLayers[i]);
                if (GroupLayers[i] == "PitActors")
                    foreach (string extra in ActorExtraLayers) mask |= 1 << LayerMask.NameToLayer(extra);
                cam.cullingMask = mask;
                cam.targetTexture = rt;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.renderShadows = false;
                data.requiresDepthTexture = false;
                data.requiresColorTexture = false;
                _cameras.Add(cam);

                if (GroupLayers[i] == "PitActors") BuildHudPanel(view);
                if (GroupLayers[i] == HudLayer) PointHudAt(cam);

                Layers.Add(new Layer { name = GroupLayers[i], zOrder = (uint)i, depth = Depths[i], texture = rt, fillFrame = true });
            }
            MoveSpawnPoints(view.center.x, view.extents.y * Aspect);
        }

        // ARcade: the sidewalk (the road art's 16-row surface, under its clear
        // top row) two and a half times as deep, for the characters'
        // full-length shadows (SpriteShadow; they may run off it, under the
        // HUD): the road art stretched down - its tiles' seams just steeper -
        // its pink top edge kept where it was (the characters' feet stay on it).
        public const float SidewalkStretch = 2.5f;
        const int RoadSurfaceArtRows = 16;
        float _sidewalkExtra;

        void StretchSidewalk(SpriteRenderer road)
        {
            if (SidewalkStretch <= 1f || !road.sprite) return;
            float rowHeight = road.bounds.size.y / road.sprite.rect.height;
            float edge = road.bounds.max.y - rowHeight;   // under its one clear row
            var scale = road.transform.localScale;
            road.transform.localScale = new Vector3(scale.x, scale.y * SidewalkStretch, scale.z);
            road.transform.position += Vector3.up * (edge - (road.bounds.max.y - rowHeight * SidewalkStretch));
            _sidewalkExtra = RoadSurfaceArtRows * rowHeight * (SidewalkStretch - 1f);
        }

        // The road's front face (its art's purple band under the road
        // surface) becomes the HUD's panel: a solid band in its colour from
        // the surface's bottom edge to the bottom of the picture, edge to edge
        // (the art leaves a few clear pixels at its left and a gap under the
        // surface), drawn behind the road on the characters' layer.
        static readonly Color RoadFrontColor = new Color32(65, 2, 116, 255);
        const int RoadSurfaceRows = 17;
        const float HudBandExtra = 0.17f; // of the picture's height, added below the road art   // the road art's clear top row + its 16 px surface
        float _hudBandTop, _hudBandBottom;

        void BuildHudPanel(Bounds view)
        {
            _hudBandBottom = view.min.y;
            _hudBandTop = view.min.y + view.size.y * 0.18f;
            var road = _player && _player.backgroundAnimator ? _player.backgroundAnimator.transform.Find("BG road") : null;
            var roadRenderer = road ? road.GetComponent<SpriteRenderer>() : null;
            if (roadRenderer && roadRenderer.sprite)
            {
                float pixel = roadRenderer.bounds.size.y / roadRenderer.sprite.rect.height;
                _hudBandTop = roadRenderer.bounds.max.y - RoadSurfaceRows * pixel;
            }

            var panel = new GameObject("HUD Panel");
            panel.transform.SetParent(transform, false);
            panel.layer = LayerMask.NameToLayer(HudLayer);
            var sr = panel.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            sr.color = RoadFrontColor;
            // In front of the road's front face (the same purple, but the art
            // has a stray pink dash on it), up to the surface's bottom edge.
            if (roadRenderer)
            {
                sr.sortingLayerID = roadRenderer.sortingLayerID;
                sr.sortingOrder = roadRenderer.sortingOrder + 1;
            }
            float z = roadRenderer ? roadRenderer.transform.position.z - 0.01f : view.center.z;
            float bottom = _hudBandBottom - 0.2f;
            float width = view.size.y * Aspect;
            panel.transform.position = new Vector3(view.center.x, (bottom + _hudBandTop) * 0.5f, z);
            panel.transform.localScale = new Vector3(width * 1.2f, _hudBandTop - bottom, 1f);

            // Pixel UI's purple digital frame round the band (9-sliced: its notched bottom-right corner
            // stays whole), with the HUD (its layout fixed in the prefab by the builder) inside it.
            var frame = transform.Find("HUD FRAME");
            var frameRenderer = frame ? frame.GetComponent<SpriteRenderer>() : null;
            if (frameRenderer && frameRenderer.sprite)
            {
                // Half the road art's pixels: one picture pixel each, a slimmer border.
                float pixel = 0.5f * (roadRenderer && roadRenderer.sprite ? roadRenderer.bounds.size.y / roadRenderer.sprite.rect.height : 0.02f);
                float scale = pixel * frameRenderer.sprite.pixelsPerUnit;
                frame.gameObject.layer = panel.layer;
                frameRenderer.drawMode = SpriteDrawMode.Sliced;
                frameRenderer.sortingLayerID = sr.sortingLayerID;
                frameRenderer.sortingOrder = sr.sortingOrder + 1;
                frame.localScale = Vector3.one;
                frame.localScale = new Vector3(scale / frame.lossyScale.x, scale / frame.lossyScale.y, 1f);
                frameRenderer.size = new Vector2(width / scale, (_hudBandTop - _hudBandBottom) / scale);
                frame.position = new Vector3(view.center.x, (_hudBandTop + _hudBandBottom) * 0.5f, z - 0.01f);
            }
        }


        // THE PIT's spawn points and the oranges' end point sat inside the
        // picture once it was widened to 4:3, so things popped in and out on
        // screen. They go just outside its left and right edges.
        void MoveSpawnPoints(float centerX, float halfWidth)
        {
            const float Margin = 0.35f;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToUpperInvariant();
                float x;
                if (n.Contains("SPAWN POINT")) x = centerX + halfWidth + Margin;
                else if (n.Contains("END POINT")) x = centerX - halfWidth - Margin;
                else continue;
                t.position = new Vector3(x, t.position.y, t.position.z);
            }
        }

        // The health bar and score draw into the HUD layer's texture.
        bool _hudShown;

        void LateUpdate()
        {
            if (_hudShown) return;
            _hudShown = true;
            SetHudShown(true);
        }

        void SetHudShown(bool shown)
        {
            var hudRoot = transform.Find("Responsive Health Bar ARK");
            if (hudRoot)
                foreach (var canvas in hudRoot.GetComponentsInChildren<Canvas>(true))
                    if (canvas.isRootCanvas) canvas.enabled = shown;
        }

        void PointHudAt(Camera cam)
        {
            var hudRoot = transform.Find("Responsive Health Bar ARK");
            if (!hudRoot) return;
            foreach (var canvas in hudRoot.GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
                // Over the characters and the road.
                canvas.sortingLayerName = "TopLayer";
                canvas.sortingOrder = 300;
            }
        }

        /// The characters' camera (the fairy shoots only at what's on screen).
        public static Camera ActorsCamera => Running ? Running._cameras.Find(c => c.name == "Camera PitActors") : null;
        /// The HUD's camera (the same view, in front of everything).
        public static Camera HudCamera => Running ? Running._cameras.Find(c => c.name == "Camera " + HudLayer) : null;

        /// A tap in the room (CabinetManipulator): true if it was on the
        /// fairy, who changes colour. It's found on the characters' layer
        /// quad, then in the demo's world through that layer's camera; she's
        /// small, so her box is taken a bit bigger.
        public static bool TryTap(Ray ray)
        {
            var fairy = FairyAlly.Current;
            var stack = Mobile.MobileRetroDepthLayerStack.Active;
            var cam = ActorsCamera;
            if (!Running || Running._paused || !fairy || !fairy.Visible || !stack || !cam) return false;
            if (!stack.LayerPoint(ray, "PitActors", out var uv)) return false;
            var point = cam.ViewportToWorldPoint(new Vector3(uv.x, uv.y, cam.nearClipPlane));
            var b = fairy.Bounds;
            float grow = Mathf.Max(b.extents.x, b.extents.y) * 0.6f;
            if (Mathf.Abs(point.x - b.center.x) > b.extents.x + grow || Mathf.Abs(point.y - b.center.y) > b.extents.y + grow) return false;
            fairy.NextLook();
            return true;
        }

        void Update()
        {
            if (_paused) return;
            FadeCampfire();
            ReadControls();
            if (Time.unscaledTime >= _nextAudioCheck)
            {
                _nextAudioCheck = Time.unscaledTime + 0.5f;
                FlattenAudio();
            }
        }

        void ReadControls()
        {
            if (DemoDialogue.Showing || ControlsLocked)
            {
                // The buttons move the dialogue on; nothing reaches the knight,
                // and a button still held as it closes doesn't count as a press.
                PitInput.Move = Vector2.zero;
                _attackHeld = Held(RetroPadButton.B);
                _jumpHeld = Held(RetroPadButton.A);
                _slashHeld = Held(RetroPadButton.Y);
                _healHeld = Held(RetroPadButton.Start);
                ArcadeInput.OnPoll();
                return;
            }
            float x = (Held(RetroPadButton.Right) ? 1f : 0f) - (Held(RetroPadButton.Left) ? 1f : 0f);
            float y = (Held(RetroPadButton.Up) ? 1f : 0f) - (Held(RetroPadButton.Down) ? 1f : 0f);
            PitInput.Move = new Vector2(x, y) * GaitSpeed(ArcadeInput.Push);

            if (_player)
            {
                // The Neo Geo style panel: A (red) = RetroPad B, B (yellow) = RetroPad A, C (green) = RetroPad Y.
                if (Pressed(RetroPadButton.B, ref _attackHeld)) _player.TriggerAttack();
                if (Pressed(RetroPadButton.A, ref _jumpHeld)) _player.TriggerJump();
                SpellButton();
                if (Pressed(RetroPadButton.Start, ref _healHeld)) _player.TriggerGreatHeal();
            }
            // ArcadeInput holds quick taps for a few "frames"; the demo's frame is Update.
            ArcadeInput.OnPoll();
        }

        static bool Held(RetroPadButton button) => ArcadeInput.IsPressed((uint)button);

        // C casts its spell (SpellWheel.Current) when a tap lets go; held
        // for SpellWheel.HoldSeconds it opens the spell wheel instead, and
        // letting go on a spell makes it C's and casts it. The player stands still while
        // the wheel is open (a controller's stick points at the spells).
        // While REST is up over her head, C sits her down to rest instead -
        // no spell, no wheel, for as long as that press is held.
        float _slashDownAt;
        bool _cRests;
        void SpellButton()
        {
            bool held = Held(RetroPadButton.Y);
            if (held && !_slashHeld && RestPrompt.Showing && _player.CanRest)
            {
                _player.StartRest();
                _cRests = true;
            }
            if (_cRests)
            {
                if (!held) _cRests = false;
                _slashHeld = held;
                return;
            }
            var wheel = SpellWheel.Instance;
            if (held && !_slashHeld) _slashDownAt = Time.unscaledTime;
            if (held && wheel && !wheel.IsOpen && Time.unscaledTime - _slashDownAt >= SpellWheel.HoldSeconds) wheel.Open();
            bool open = wheel && wheel.IsOpen;
            if (open)
            {
                PitInput.Move = Vector2.zero;
                if (held) wheel.Track();
            }
            if (!held && _slashHeld)
            {
                // Letting go on a spell makes it C's - and casts it there and then.
                if (open) { if (wheel.Choose()) Cast(SpellWheel.Current); }
                else Cast(SpellWheel.Current);
            }
            _slashHeld = held;
        }

        void Cast(SpellWheel.Spell spell)
        {
            switch (spell)
            {
                case SpellWheel.Spell.HolySlash: _player.TriggerHolySlash(); break;
                case SpellWheel.Spell.GreatHeal: _player.TriggerGreatHeal(); break;
                case SpellWheel.Spell.SwordBuff: _player.TriggerSwordBuff(); break;
                case SpellWheel.Spell.ShieldBuff: _player.TriggerShieldBuff(); break;
            }
        }

        // How far the joystick is pushed picks her gait - a run, or THE
        // PIT's sprint all the way out - each at its own speed (her feet match
        // it; the background scrolls with it), which PlayerScriptARIANAClips
        // reads back from the size of PitInput.Move to pick the animation. A
        // push must clear the boundary by a little to change gait, so a thumb
        // resting on it doesn't flicker between the two. No push reported (a
        // direction held some other way) is a sprint, as before. The run's
        // speed is the art's: a planted foot slides back ~5.5 px a frame
        // running and ~9 sprinting (both at 0.07 s a frame). (A walk, ~2 px,
        // was too slow for the game.)
        public const float RunSpeed = 0.63f;
        const float SprintFrom = 0.55f, GaitMargin = 0.04f;   // past about half a push sprints
        bool _sprinting = true;
        float GaitSpeed(float push)
        {
            bool want = push <= 0f || push >= SprintFrom;
            if (want != _sprinting && (push <= 0f || Mathf.Abs(push - SprintFrom) >= GaitMargin)) _sprinting = want;
            return _sprinting ? 1f : RunSpeed;
        }

        static bool Pressed(RetroPadButton button, ref bool wasHeld)
        {
            bool held = Held(button);
            bool pressed = held && !wasHeld;
            wasHeld = held;
            return pressed;
        }

        // The demo's world is far from the AR camera's listener, so its
        // sounds (and those of spawned enemies) play non-positional.
        // The campfire's crackle (THE PIT's looping "fire" on MUSIC) is heard
        // everywhere once FlattenAudio makes it non-positional: fade it with
        // the fire's distance from the picture - full on screen, gone a
        // world unit past the edge.
        AudioSource _campfireSound;
        Transform _campfire;
        float _campfireVolume = -1f;
        const float CampfireFade = 1f;
        const float CampfireLoudness = 0.4f;   // of THE PIT's volume, which drowned the game out

        void FadeCampfire()
        {
            if (_campfireVolume < 0f)
            {
                _campfire = transform.Find("BONFIRE");
                foreach (var source in GetComponentsInChildren<AudioSource>(true))
                    if (source.loop && source.clip && source.clip.name == "fire") _campfireSound = source;
                _campfireVolume = _campfireSound ? _campfireSound.volume * CampfireLoudness : 0f;
            }
            var cam = _cameras.Find(c => c.name == "Camera PitActors");
            if (!_campfireSound || !_campfire || !cam) return;
            float halfWidth = cam.orthographicSize * cam.aspect;
            float outside = Mathf.Abs(_campfire.position.x - cam.transform.position.x) - halfWidth;
            _campfireSound.volume = _campfireVolume * Mathf.Clamp01(1f - outside / CampfireFade);
        }

        static void FlattenAudio()
        {
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                source.spatialBlend = 0f;
            GameAudio.Apply();   // the MUSIC and SFX volumes on any new sources
        }

        void OnDestroy()
        {
            if (Running == this) Running = null;
        }
    }
}
