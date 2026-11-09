// TimeCrystalGame.cs — the second built-in demo, THE TIME CRYSTAL (Wayne's
// 2025 game jam shooter, Assets/TimeCrystalDemo; its prefab is made by
// Tools > ARcade > Build Time Crystal Demo): a vertical game, so it plays in
// the vertical cabinet, its picture 3:4.
//
// Like ENDLESS KNIGHT (PitDemoGame), its world is spawned far from the
// room and drawn by one orthographic camera per depth group into its own
// texture, back to front: the six space layers (TcSpaceScroller), then the
// ships, shots and crystals, then the HUD. MobileRetroDepthLayerStack puts
// them on its layer quads, so the frame, spacing and controls all work
// unchanged.
//
// Controls come from ArcadeInput (touch controls or a game controller):
// the joystick flies (tight: full speed at once, no drift) and A fires
// (TcPlayerShip: the missile, or a power-up's gun for 10 s); B or C sets off a
// bomb (TcBombs: clears the screen; enemies sometimes drop more). No dash. Out of lives, a new game starts after a moment.

using SpatialEmulator.Controls;
using SpatialEmulator.Games;
using TimeCrystal;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SpatialEmulator.Demo
{
    [DefaultExecutionOrder(-100)]   // (the controls are read before the ship's Update)
    public class TimeCrystalGame : LayeredDemo
    {
        public const string GameName = "timecrystal";

        /// Its entry in the game list (always installed). Turned like a
        /// vertical arcade game, so it's tagged VERT and gets the vertical cabinet.
        public static readonly CatalogGame Info = new CatalogGame
        {
            name = GameName,
            parent = "",
            year = "2025",
            company = "Wayne Lamb",
            description = "THE TIME CRYSTAL",
            board = "demo",
            rotation = 90,
            crcs = new uint[0],
        };

        /// Resources/TimeCrystalDemo (TimeCrystalBuilder).
        public const string PrefabName = "TimeCrystalDemo";

        /// Where its world lives: well away from the room, the AR camera
        /// and ENDLESS KNIGHT's world.
        static readonly Vector3 Origin = new Vector3(0f, -1000f, 0f);

        // The picture: 3:4, as tall as TimeCrystalBuilder made the field;
        // 800 px tall, about the space art's own pixels.
        const float PictureAspect = 3f / 4f;
        const float FieldHeight = 12f;
        const int TextureHeight = 800;

        // Back to front. The ships' camera also sees the game's physics layers and Default.
        // Last, the player's ship and its effects (TcPlayer, TimeCrystal.TcPlayerTop): at the
        // ships' depth, but drawn over the HUD so the portrait and health bar never hide it.
        public const string HudLayer = "TcHUD";
        const string PlayerLayer = "TcPlayer";
        static readonly string[] GroupLayers =
            { "TcSpace", "TcNebula", "TcPlanets", "TcStars", "TcRocks", "TcBigRocks", "TcActors", HudLayer, PortraitLayer, PlayerLayer };
        // The pilot's portrait: at the HUD's depth, but on a camera with no bloom, so the HUD's strong glow
        // (for the neon score) doesn't blow out the face. (TcStatus: full screen's old status layer, unused now.)
        const string PortraitLayer = StatusLayer;
        static readonly string[] ActorExtraLayers = { "Default", "TransparentFX", "TcBullet" };
        // The enemy ships and their bullets go with the player's (over the HUD's portrait, health bar, lives and bombs).
        static readonly string[] PlayerExtraLayers = { "TcEnemy", "TcEnemyBullet" };
        /// Each depth group's depth, in steps of the layer spacing: close
        /// together at the back, the widest gap between the ships and the HUD.
        static readonly float[] Depths = { 0f, 0.45f, 1f, 1.6f, 2.3f, 3.1f, 4.4f, 6f, 6f, 4.4f };

        public static TimeCrystalGame Running { get; private set; }
        public static bool IsRunning => Running != null;

        /// Out of lives: MobileRetroDepthLayerStack starts a new game.
        public static bool RestartRequested;

        public override float Aspect => PictureAspect;
        public override string HudLayerName => HudLayer;

        // ---- Full screen: the picture at the top, the pilot's portrait and health bar under it ----
        // (TcStageDirector moves those two onto TcStatus in full screen; this camera, the HUD's
        // twin, draws only them, into a picture-sized texture FullScreenTest shows below the picture.)
        public const string StatusLayer = "TcStatus";
        /// How far down that's shown, in picture heights (their tops on the picture's bottom): TcStageDirector.
        public static float StatusDrop;
        public override bool FullScreenAtTop => true;
        public override float BelowPictureDrop => StatusDrop;
        Camera _statusCam;

        void BuildStatusCamera(Camera hud, int width, int height)
        {
            int layer = LayerMask.NameToLayer(StatusLayer);
            if (!hud || layer < 0) return;
            var rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
                { name = "TimeCrystal " + StatusLayer, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, useMipMap = false };
            rt.Create();
            var go = new GameObject("Camera " + StatusLayer);
            go.transform.SetParent(hud.transform.parent, false);
            go.transform.localPosition = hud.transform.localPosition;
            _statusCam = go.AddComponent<Camera>();
            _statusCam.CopyFrom(hud);
            _statusCam.cullingMask = 1 << layer;
            _statusCam.targetTexture = rt;
            _statusCam.enabled = false;
            var data = _statusCam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            _cameras.Add(_statusCam);
            Layers.Add(new Layer { name = StatusLayer, zOrder = (uint)Layers.Count, depth = Depths[Depths.Length - 2], texture = rt, premultiplied = true, belowPicture = true });
        }

        RetroShmupPlayerController _player;
        readonly System.Collections.Generic.List<Camera> _cameras = new System.Collections.Generic.List<Camera>();
        bool _paused;
        float _gameOverAt = -1f, _continueLeft;
        bool _coinHeld;
        int _pickHeld;
        bool _resuming;
        const float CoinInsert = 0.55f;   // (CoinInsertAnimation's drop, pause and pop)

        System.Collections.IEnumerator Resume(TcStageDirector director)
        {
            yield return new WaitForSecondsRealtime(CoinInsert);
            yield return TcFighterSelect.ResumeCountdown(director);
            Time.timeScale = 1f;
            _resuming = false;
        }   // (CONTINUE?'s pilot swap: -1/+1 while left/right is held, so one push is one step)
        const float ContinueAfter = 1.2f, ContinueSeconds = 1f;   // (ContinueSeconds: only marks CONTINUE? as up - it waits)
        const int ContinueLives = 3;

        /// CONTINUE? is up: COIN shows (SpellWheel hides it the rest of the time).
        public static bool WantsCoin => Running && (Running._continueLeft > 0f || Time.unscaledTime < Running._coinShownUntil);
        // (COIN kept up a moment after it's put in at CONTINUE?: its insert animation plays out - CoinInsertAnimation.)
        float _coinShownUntil = -1f;
        const string HiScoreKey = "TimeCrystal.HiScore";

        // A finger put down on the picture this frame (not on a menu or a button: the story's TAP TO SKIP).
        // The left mouse button for one in the Editor.
        static readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> s_uiHits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        static bool s_touches;
        static bool ScreenTapped()
        {
            // (Its own hold on EnhancedTouch: the controls' router, which has one too, is off meanwhile.)
            if (!s_touches) { UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable(); s_touches = true; }
            foreach (var touch in UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches)
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began && !OverUi(touch.screenPosition)) return true;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return Application.isEditor && mouse != null && mouse.leftButton.wasPressedThisFrame && !OverUi(mouse.position.ReadValue());
        }

        static bool OverUi(Vector2 at)
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (!events) return false;
            s_uiHits.Clear();
            events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events) { position = at }, s_uiHits);
            return s_uiHits.Count > 0;
        }

        public static TimeCrystalGame Launch(GameObject prefab)
        {
            Stop();
            RestartRequested = false;
            var root = Instantiate(prefab, Origin, Quaternion.identity);
            root.name = "TimeCrystal (running)";
            TcWorld.Root = root.transform;
            TcWorld.Origin = Origin;
            TcWorld.Field = new Rect(Origin.x - FieldHeight * PictureAspect * 0.5f, Origin.y - FieldHeight * 0.5f, FieldHeight * PictureAspect, FieldHeight);
            Running = root.AddComponent<TimeCrystalGame>();
            Active = Running;
            Running.Setup();
            TcMusic.Ensure();   // (its soundtrack, carried on through restarts)
            TcButtonHints.Ensure();   // (FIRE, BOMB, LASER over the buttons)
            return Running;
        }

        public static void Stop()
        {
            if (!Running) return;
            // The run so far counts on the leaderboard (leaving it, restarting, game over).
            if (ScoreManager.Instance) GameCenter.Submit(ScoreManager.Instance.Score, GameCenter.TimeCrystalLeaderboardId);
            var game = Running;
            Running = null;
            if (Active == game) Active = null;
            Time.timeScale = 1f;
            ClearControls();
            foreach (var layer in game.Layers) if (layer.texture) { layer.texture.Release(); Destroy(layer.texture); }
            // Immediately: a new game launches right away, and the jam
            // build's singletons (ScoreManager) would find the old one.
            DestroyImmediate(game.gameObject);
            TcWorld.Root = null;
        }

        /// Freezes the game (Time.timeScale; ARcade's own UI runs on unscaled time).
        public static bool Paused
        {
            set
            {
                if (!Running || Running._paused == value) return;
                Running._paused = value;
                Time.timeScale = value ? 0f : 1f;
            }
        }

        public static bool IsPaused => Running && Running._paused;

        void Setup()
        {
            _player = GetComponentInChildren<RetroShmupPlayerController>(true);
            // The new ship: the pilot's jet's look and animations, and its guns (TcWeapons).
            if (_player && !_player.GetComponent<TcPlayerShip>()) _player.gameObject.AddComponent<TcPlayerShip>();
            AssignGroups();
            BuildCameras();

            // The room's AR camera must not see the game's depth groups.
            int mask = 0;
            foreach (string name in GroupLayers) mask |= 1 << LayerMask.NameToLayer(name);
            if (LayerMask.NameToLayer(StatusLayer) >= 0) mask |= 1 << LayerMask.NameToLayer(StatusLayer);
            foreach (string name in ActorExtraLayers) if (name != "Default" && name != "TransparentFX") mask |= 1 << LayerMask.NameToLayer(name);
            foreach (string name in PlayerExtraLayers) mask |= 1 << LayerMask.NameToLayer(name);
            foreach (var cam in Camera.allCameras)
                if (!_cameras.Contains(cam)) cam.cullingMask &= ~mask;

            // Hundreds of bullets: they never touch each other, the player's
            // shots or the scenery - only the player's ship.
            int shots = LayerMask.NameToLayer("TcBullet"), bullets = LayerMask.NameToLayer("TcEnemyBullet"), scenery = LayerMask.NameToLayer("TcActors");
            Physics2D.IgnoreLayerCollision(shots, bullets);
            Physics2D.IgnoreLayerCollision(shots, shots);
            Physics2D.IgnoreLayerCollision(bullets, bullets);
            Physics2D.IgnoreLayerCollision(bullets, scenery);
            Physics2D.IgnoreLayerCollision(shots, scenery);
            // Enemies fly through each other and their own bullets.
            int enemies = LayerMask.NameToLayer("TcEnemy");
            Physics2D.IgnoreLayerCollision(enemies, enemies);
            Physics2D.IgnoreLayerCollision(enemies, bullets);

            SpellWheel.Find();   // (it hides START, and COIN except at CONTINUE?)

            FlattenAudio();
            ShowHiScore();
        }

        void AssignGroups()
        {
            int actors = LayerMask.NameToLayer("TcActors");
            int hud = LayerMask.NameToLayer(HudLayer);
            int ui = LayerMask.NameToLayer("UI");
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.layer == 0) t.gameObject.layer = actors;
                else if (t.gameObject.layer == ui) t.gameObject.layer = hud;
            }
            int portrait = LayerMask.NameToLayer(PortraitLayer);
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "PLAYER PORTRAIT" && portrait >= 0)
                    foreach (var part in t.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = portrait;
        }

        void BuildCameras()
        {
            Camera hudCam = null;
            int height = TextureHeight;
            int width = Mathf.RoundToInt(height * PictureAspect);
            var rig = new GameObject("Demo Cameras").transform;
            rig.SetParent(transform, false);
            rig.position = Origin + new Vector3(0f, 0f, -10f);

            for (int i = 0; i < GroupLayers.Length; i++)
            {
                var rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
                {
                    name = "TimeCrystal " + GroupLayers[i],
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                };
                rt.Create();

                var go = new GameObject("Camera " + GroupLayers[i]);
                go.transform.SetParent(rig, false);
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = FieldHeight * 0.5f;
                cam.aspect = PictureAspect;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 50f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                // The back layer is solid black (space), the rest clear.
                cam.backgroundColor = i == 0 ? Color.black : new Color(0, 0, 0, 0);
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.useOcclusionCulling = false;
                int mask = 1 << LayerMask.NameToLayer(GroupLayers[i]);
                if (GroupLayers[i] == "TcActors")
                    foreach (string extra in ActorExtraLayers) mask |= 1 << LayerMask.NameToLayer(extra);
                if (GroupLayers[i] == PlayerLayer)
                    foreach (string extra in PlayerExtraLayers) mask |= 1 << LayerMask.NameToLayer(extra);
                cam.cullingMask = mask;
                cam.targetTexture = rt;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.renderShadows = false;
                data.requiresDepthTexture = false;
                data.requiresColorTexture = false;
                _cameras.Add(cam);

                if (ActorsBloom && GroupLayers[i] == "TcActors") { _actorsBloomBase = 1.6f; _actorsBloom = AddBloom(cam, data, "TcActors", 0.85f, _actorsBloomBase, 0.4f); }
                if (ActorsBloom && GroupLayers[i] == PlayerLayer) _playerBloom = AddBloom(cam, data, PlayerLayer, 0.85f, 1.6f, 0.4f);   // (the ships' bloom: its flames, its laser's muzzle)
                if (GroupLayers[i] == HudLayer)
                {
                    PointHudAt(cam);
                    AddBloom(cam, data, HudLayer, 0.6f, 4.5f, 0.4f);   // (the neon blue score text, glowing wider)
                }

                Layers.Add(new Layer { name = GroupLayers[i], zOrder = (uint)i, depth = Depths[i], texture = rt, fillFrame = true, premultiplied = true, onTop = GroupLayers[i] == PlayerLayer || GroupLayers[i] == "TcActors" });   // (the ships, gems, power-ups and blasts over the HUD's pieces; the player's over them)
                if (GroupLayers[i] == HudLayer) hudCam = cam;
            }
            // (BuildStatusCamera: full screen's HUD under the picture - not used now the HUD's up top right.)
        }

        // True bloom on the HUD's layer (the neon SCORE and HI SCORE) and, high
        // threshold, the ships' layer (the jets' flames, lasers, the brightest
        // blasts): each of those cameras runs post-processing with a bloom
        // volume only it sees; the space layers stay clean. The layers keep
        // their transparency through it (URP-Performant: post-process alpha
        // output), and the glow adds over what's behind (they're premultiplied).
        const bool ActorsBloom = true;   // (Wayne's trying it, 2026-10-15)

        Bloom _actorsBloom, _playerBloom;
        float _actorsBloomBase;

        Bloom AddBloom(Camera cam, UniversalAdditionalCameraData data, string layerName, float threshold, float intensity, float scatter)
        {
            int layer = LayerMask.NameToLayer(layerName);
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            data.volumeLayerMask = 1 << layer;
            var go = new GameObject(layerName + " Bloom") { layer = layer };
            go.transform.SetParent(cam.transform, false);
            var volume = go.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(threshold);
            bloom.intensity.Override(intensity);
            bloom.scatter.Override(scatter);
            bloom.highQualityFiltering.Override(false);
            volume.sharedProfile = profile;
            return bloom;
        }

        void PointHudAt(Camera cam)
        {
            foreach (var canvas in GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
            }
        }

        void Update()
        {
            // The ships' bloom swells by 2 at warp speed, back to normal as the next level comes.
            if (_actorsBloom) _actorsBloom.intensity.value = _actorsBloomBase + 2f * TcSpaceScroller.Warp01;
            if (_playerBloom) _playerBloom.intensity.value = _actorsBloomBase + 2f * TcSpaceScroller.Warp01;
            if (_statusCam) _statusCam.enabled = UI.FullScreenTest.Enabled;
            if (_paused) return;
            ReadControls();
            KeepHiScore();

            // Out of lives: a moment, then CONTINUE? - the game frozen where it
            // is, the scene darkened, a pilot to pick; COIN goes on right there
            // (three more lives, full bombs), boss fights too. No countdown.
            if (_resuming) { Time.timeScale = 0f; return; }   // (frozen till the continue's GO!)
            if (_player && _player.IsDead)
            {
                if (_continueLeft <= 0f)
                {
                    if (_gameOverAt < 0f) _gameOverAt = Time.unscaledTime;
                    else if (Time.unscaledTime - _gameOverAt >= ContinueAfter)
                    {
                        _continueLeft = ContinueSeconds;
                        _coinHeld = true;
                        // Game over: the score goes up to the leaderboard now (a continue carries on).
                        if (ScoreManager.Instance) GameCenter.Submit(ScoreManager.Instance.Score, GameCenter.TimeCrystalLeaderboardId);
                    }
                }
                else
                {
                    // No countdown: it waits for the coin (TcFighterSelect's mini select over it all).
                    Time.timeScale = 0f;
                    var director = TcStageDirector.Instance;
                    // (with a controller, Cross puts it in too - on the TV there's no coin to tap)
                    var pad = UnityEngine.InputSystem.Gamepad.current;
                    bool padCoin = pad != null && (pad.buttonSouth.isPressed || pad.selectButton.isPressed);
                    bool coin = Held(RetroPadButton.Select) || padCoin;
                    if (coin && !_coinHeld)
                    {
                        // In: back in the game with whoever's picked, still frozen - the coin's insert plays out,
                        // the COIN button goes, then 3, 2, 1, GO! and it's live (Resume).
                        _continueLeft = 0f;
                        _gameOverAt = -1f;
                        _coinShownUntil = Time.unscaledTime + CoinInsert;
                        TcSfx.Play("coin", 0.9f, 0.2f);   // (the gems' cha-ching, as it goes in)
                        if (padCoin) CoinButton()?.NotePress();   // (its drop into the slot, as a tap plays it)
                        _player.Continue(ContinueLives);
                        if (TcBombs.Instance) TcBombs.Instance.Refill();
                        _resuming = true;
                        StartCoroutine(Resume(director));
                    }
                    else
                    {
                        // Left/right: another pilot for the continue.
                        float x = TcWorld.Move.x;
                        int pick = x < -0.5f ? -1 : x > 0.5f ? 1 : 0;
                        if (pick != 0 && pick != _pickHeld) TcFighterSelect.StepMini(director, pick);
                        _pickHeld = pick;
                        TcFighterSelect.TapMini(director);   // (or a face tapped)
                        TcFighterSelect.ShowMini(director);
                    }
                    _coinHeld = coin;
                }
            }
        }

        // ---- Controls ----

        bool _left, _right, _up, _down, _bomb, _fire, _laser;

        void ReadControls()
        {
            bool left = Held(RetroPadButton.Left), right = Held(RetroPadButton.Right);
            bool up = Held(RetroPadButton.Up), down = Held(RetroPadButton.Down);
            TcWorld.Move = new Vector2((right ? 1f : 0f) - (left ? 1f : 0f), (up ? 1f : 0f) - (down ? 1f : 0f)).normalized;
            // The stick, analog: any angle, and how far it's pushed sets the
            // speed - a nudge creeps (threading tight gaps), past three
            // quarters is full speed.
            var stick = ArcadeInput.Stick;
            if (stick.sqrMagnitude > 0.0001f)
            {
                float push = stick.magnitude;
                TcWorld.Move = stick / push * Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(0.15f, 0.75f, push));
            }
            TcWorld.LeftPressed = left && !_left;
            TcWorld.RightPressed = right && !_right;
            TcWorld.UpPressed = up && !_up;
            TcWorld.DownPressed = down && !_down;
            _left = left; _right = right; _up = up; _down = down;
            // A (red, RetroPad B) fires all three of the jam build's guns (its J, K and L) at once.
            bool fire = Held(RetroPadButton.B);
            TcWorld.Fire = fire;
            TcWorld.FireJ = TcWorld.FireK = TcWorld.FireL = false;   // (the jam build's guns: TcPlayerShip fires now)
            // B (yellow, RetroPad A): a bomb (TcBombs). C (green, RetroPad Y): the laser (TcLaser).
            bool bomb = Held(RetroPadButton.A);
            TcWorld.Laser = Held(RetroPadButton.Y);
            if (bomb && !_bomb) TcWorld.BombPressed = true;
            // The story: any button finishes its line or goes on; nothing else moves meanwhile.
            if (TimeCrystal.TcStory.Active)
            {
                if ((fire && !_fire) || (bomb && !_bomb) || (TcWorld.Laser && !_laser) || ScreenTapped()) TimeCrystal.TcStory.Next = true;
                TcWorld.Move = Vector2.zero;
                TcWorld.Fire = TcWorld.BombPressed = false;
                TcWorld.LeftPressed = TcWorld.RightPressed = TcWorld.UpPressed = TcWorld.DownPressed = false;
            }
            // Choose Your Fighter: any button goes; the ship stays put meanwhile.
            else if (TcFighterSelect.Active)
            {
                if ((fire && !_fire) || (bomb && !_bomb) || (TcWorld.Laser && !_laser)) TcFighterSelect.Confirm = true;
                TcWorld.Move = Vector2.zero;
                TcWorld.Fire = TcWorld.BombPressed = false;
                TcFighterSelect.Left |= TcWorld.LeftPressed;
                TcFighterSelect.Right |= TcWorld.RightPressed;
                TcWorld.LeftPressed = TcWorld.RightPressed = TcWorld.UpPressed = TcWorld.DownPressed = false;
            }
            // ...and no shots or bombs until GO! (the press that picked the pilot doesn't fire).
            _laser = TcWorld.Laser;
            if (TimeCrystal.TcStageDirector.HoldFire) TcWorld.Fire = TcWorld.BombPressed = TcWorld.Laser = false;
            _bomb = bomb;
            _fire = fire;
            // ArcadeInput holds quick taps for a few "frames"; the demo's frame is Update.
            ArcadeInput.OnPoll();
        }

        static bool Held(RetroPadButton button) => ArcadeInput.IsPressed((uint)button);

        /// The on-screen COIN (its insert animation).
        public static ArcadeButton CoinButton()
        {
            if (s_coin) return s_coin;
            foreach (var b in FindObjectsByType<ArcadeButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.button == RetroPadButton.Select) { s_coin = b; break; }
            return s_coin;
        }
        static ArcadeButton s_coin;

        static void ClearControls()
        {
            TcWorld.Move = Vector2.zero;
            TcWorld.FireJ = TcWorld.FireK = TcWorld.FireL = TcWorld.Fire = TcWorld.Laser = false;
            TcWorld.LeftPressed = TcWorld.RightPressed = TcWorld.UpPressed = TcWorld.DownPressed = false;
            TcWorld.BombPressed = false;
        }

        // ---- Hi score ----

        TMPro.TMP_Text _hiScoreText;
        int _hiScore;

        void ShowHiScore()
        {
            _hiScore = PlayerPrefs.GetInt(HiScoreKey, 0);
            foreach (var text in GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (text.gameObject.name == "HI SCORE NUMBER") _hiScoreText = text;   // (TimeCrystalBuilder's)
            if (_hiScoreText) _hiScoreText.text = _hiScore.ToString("D7");
        }

        void KeepHiScore()
        {
            int score = ScoreManager.Instance ? ScoreManager.Instance.Score : 0;
            if (score <= _hiScore) return;
            _hiScore = score;
            PlayerPrefs.SetInt(HiScoreKey, _hiScore);
            if (_hiScoreText) _hiScoreText.text = _hiScore.ToString("D7");
        }

        // The game's world is far from the AR camera's listener, so its
        // sounds play non-positional.
        float _nextAudioCheck;

        void LateUpdate()
        {
            if (Time.unscaledTime < _nextAudioCheck) return;
            _nextAudioCheck = Time.unscaledTime + 0.5f;
            FlattenAudio();
        }

        void FlattenAudio()
        {
            foreach (var source in GetComponentsInChildren<AudioSource>(true))
                source.spatialBlend = 0f;
            GameAudio.Apply();   // the MUSIC and SFX volumes on any new sources
        }

        void OnDisable()
        {
            if (Running == this) { PlayerPrefs.Save(); }
        }

        void OnDestroy()
        {
            if (Running == this) Running = null;
            if (Active == this) Active = null;
        }
    }
}
