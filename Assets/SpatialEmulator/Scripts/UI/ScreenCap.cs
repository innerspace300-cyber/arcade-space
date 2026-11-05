// ScreenCap.cs — the CAP key (a copy of LOCK's with a red dot before its
// label, in the saves/DEMO panel between CRT MODE and LOCK; CrtEffect lays
// the row out). Tapped: a 3, 2, 1 countdown, then it records a video of the
// screen - the room, the cabinet and the on-screen controls, as played -
// with the game's sound. The key says STOP (its dot blinking) while it
// records. It stops by itself after CAP LENGTH (10, 20, 30 or 60 seconds), or
// when STOP's tapped (always, with UNTIL STOP), and iOS's share sheet comes
// up with the video (Plugins/iOS/SEShare.mm: Save Video, AirDrop, Messages,
// Mail, YouTube, and apps like Instagram, Snapchat and Gmail when they're
// installed; with a controller connected ShareWindow's SAVE TO PHOTOS / SHARE ON
// PHONE / CLOSE instead, which it can steer) - after SCREEN CAP DONE a moment first, the
// game held, so a press still meant for the game can't pick one. Holding the key opens its options (Settings' SOUND + CAP
// window, SettingsAudioRows): CAP LENGTH, FADE IN and FADE OUT.
//
// The video's 9:16 (720 x 1280), the whole screen fitted in, dark bars down
// its sides, so nothing's cut off on Instagram, Snapchat, TikTok and the
// like. With FADE IN it starts from black, with FADE OUT it ends in black
// (the sound too) - fade in's off to start with: Snapchat shows a video's
// first frame. After any fade in, the ARcade watermark (Resources/Capture/
// Watermark.png) pops up on its left, just above the on-screen joystick
// and clear of Instagram's names and comments - growing from
// nothing, a little jiggle - and five seconds on shrinks away. The screen's copied at the end of each frame, fitted into
// the video's frame, read back from the GPU and written, with the sound
// (tapped off the AudioListener), by Plugins/iOS/SERecorder.mm. In the
// Editor nothing's written: a frame from just after the watermark's popped
// up is saved as a picture, its path logged.
//
// On a TV (TvOutput.Showing when the countdown ends) it's the TV's picture instead,
// at the TV's own shape (16:9, up to 1920 x 1080): its camera drawn again into the
// video's frame, the game and any menus on it as seen there - not the phone's
// controller screen. The watermark sits low in the black at the left.

using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using SpatialEmulator.Mobile;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class ScreenCap : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern bool SE_RecStart(string path, int width, int height, int fps, int sampleRate, int channels);
        [DllImport("__Internal")] static extern void SE_RecVideoFrame(byte[] bgra, int width, int height);
        [DllImport("__Internal")] static extern void SE_RecAudio(float[] samples, int count, int channels);
        [DllImport("__Internal")] static extern void SE_RecStop(string gameObject, string method);
        [DllImport("__Internal")] static extern void SE_ShareFile(string path, string gameObject, string method);
#endif

        // ---- the options (Settings' SOUND + CAP window) ----

        static readonly int[] Lengths = { 10, 20, 30, 60, 0 };   // seconds; 0: until STOP
        const string LengthKey = "ARcade.CapLength", FadeInKey = "ARcade.CapFadeIn", FadeOutKey = "ARcade.CapFadeOut";

        /// How long it records, in seconds (0: until STOP's tapped).
        public static int Length
        {
            get => PlayerPrefs.GetInt(LengthKey, 30);
            set { PlayerPrefs.SetInt(LengthKey, value); PlayerPrefs.Save(); }
        }

        public static string LengthLabel => Length > 0 ? $"{Length} SEC" : "UNTIL STOP";

        /// The next length along (CAP LENGTH's key).
        public static void NextLength()
        {
            int i = Array.IndexOf(Lengths, Length);
            Length = Lengths[(i + 1) % Lengths.Length];
        }

        /// Starting from black (off to start with: Snapchat shows a video's first frame).
        public static bool FadeIn
        {
            get => PlayerPrefs.GetInt(FadeInKey, 0) == 1;
            set { PlayerPrefs.SetInt(FadeInKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// Ending in black.
        public static bool FadeOut
        {
            get => PlayerPrefs.GetInt(FadeOutKey, 1) == 1;
            set { PlayerPrefs.SetInt(FadeOutKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // ---- the recording ----

        const float StepSeconds = 0.8f;          // each count
        const float HoldSeconds = 0.5f;          // holding the key this long opens the options
        const float MaxSeconds = 600f;           // UNTIL STOP stops itself after this
        const int Fps = 30;
        const int VideoWidth = 720, VideoHeight = 1280;   // 9:16
        const float FadeSeconds = 0.5f;
        const float WatermarkShare = 0.28f;      // of the video's width, about (as whole pixels)... (the icon over ARcade: narrower than the old side-by-side one)
        const float WatermarkSize = 0.8f;        // ...then this much of that
        const float WatermarkBottom = 0.71f;     // its bottom, down the video: just above the joystick (its top's about 0.73)
        const float WatermarkSeconds = 5f, WatermarkPop = 0.45f;

        enum State { Idle, Counting, Recording, Finishing }
        static State s_state;
        static ScreenCap s_active;
        /// A 3, 2, 1 counting down to a recording.
        public static bool Counting => s_state == State.Counting;
        /// The game's held by a menu CAP was tapped from: let it go when recording starts (MenuPause).
        public static bool ResumeOnRecord;
        static float s_stoppedAt = -1f;

        /// The countdown on its own (a menu closing: MenuPause), then `done`.
        public static void RunCountdown(System.Action done) => Host.Run(CountdownThen(done));

        static IEnumerator CountdownThen(System.Action done)
        {
            yield return Countdown();
            done?.Invoke();
        }
        /// The share sheet's up (the game's paused).
        public static bool Sharing { get; private set; }

        /// Recording, for the sound tap (read on the audio thread).
        internal static volatile bool Capturing;
        /// The sound's level in the video (its fade), for the sound tap.
        internal static volatile float AudioGain = 1f;

        TMP_Text _label;
        Image _dot, _key;
        Sprite _idleSprite, _recordingSprite;
        RenderTexture _screen, _frame;
        byte[] _pixels;
        Texture2D _watermark;
        int _inFlight;
        float _started, _lastFrame;
        float _endAt;          // when it stops (-1: not yet known - UNTIL STOP)
        bool _fadeIn, _fadeOut;
        string _path;
        bool _editorFrameSaved;
        bool _tv;              // recording the TV's picture (TvOutput), not the phone's screen
        float _downAt = -1f;
        bool _swallowClick;

        // ---- the key ----

        /// The CAP key: LOCK's keycap, copied, with a red dot before CAP.
        public static RectTransform MakeButton(CabinetLockButton lockButton)
        {
            var copy = Instantiate(lockButton.gameObject, lockButton.transform.parent);
            Destroy(copy.GetComponent<CabinetLockButton>());
            return MakeButton(copy, lockButton.unlockedSprite, lockButton.lockedSprite);
        }

        /// The CAP key made of `copy` (a copied keycap: the screen's menu's,
        /// MenuDropdown), its cap `idle`, and `recording` while it records.
        public static RectTransform MakeButton(GameObject copy, Sprite idle, Sprite recording)
        {
            copy.name = "Cap Button";
            var cap = copy.AddComponent<ScreenCap>();
            cap._key = copy.GetComponent<Image>();
            cap._idleSprite = idle ? idle : cap._key ? cap._key.sprite : null;
            cap._recordingSprite = recording ? recording : cap._idleSprite;
            cap._label = copy.GetComponentInChildren<TMP_Text>(true);
            cap.MakeDot();
            cap.Show();
            var button = copy.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(cap.Tap);
            return (RectTransform)copy.transform;
        }

        // A small red pixel circle, beside the label (moving with it as the key's pressed).
        void MakeDot()
        {
            if (!_label) return;
            const int size = 7;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var red = new Color32(232, 32, 58, 255);
            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - 3f, dy = y - 3f;
                    pixels[y * size + x] = dx * dx + dy * dy <= 10.5f ? red : clear;
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dot.layer = gameObject.layer;
            _dot = dot.GetComponent<Image>();
            _dot.sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            _dot.raycastTarget = false;
            var rect = (RectTransform)dot.transform;
            rect.SetParent(_label.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
        }

        void Show()
        {
            bool recording = (s_state == State.Recording || s_state == State.Finishing) && s_active == this;
            if (_label)
            {
                _label.text = recording ? "STOP" : "CAP";
                // (The text moves right to make room for the dot before it.)
                _label.margin = new Vector4(9f, 0f, 0f, 0f);
                _label.ForceMeshUpdate();
                if (_dot)
                {
                    // (Halfway between the key's left edge - 2 px outside the
                    // label's - and the text.)
                    float text = _label.textBounds.min.x, edge = _label.rectTransform.rect.xMin - 2f;
                    ((RectTransform)_dot.transform).anchoredPosition = new Vector2(Mathf.Round((text + edge) * 0.5f), 0.5f);
                }
            }
            if (_key && _idleSprite) _key.sprite = recording ? _recordingSprite : _idleSprite;
        }

        public void OnPointerDown(PointerEventData eventData) => _downAt = s_state == State.Idle ? Time.unscaledTime : -1f;
        public void OnPointerUp(PointerEventData eventData) => _downAt = -1f;
        public void OnPointerExit(PointerEventData eventData) => _downAt = -1f;

        void Tap()
        {
            if (_swallowClick) { _swallowClick = false; return; }   // (it was held: the options opened)
            switch (s_state)
            {
                case State.Idle:
                    // Not of the game picker (MY GAMES): only of a game.
                    if (GamePicker.IsOpen) return;
                    Haptics.Play(Haptics.Kind.Light);
                    s_active = this;
                    Host.Run(Begin());
                    // From the AR side menus: they fold away, the game held through the countdown (MenuPause).
                    if (SaveStatePanel.SideMenusOpen) { SaveStatePanel.SideMenusOpen = false; MenuPause.Close(MenuPause.Menu.Side); }
                    MenuDropdown.CloseIfOpen();   // (and the screen's menu: CAP's countdown carries the game on once both are shut)
                    break;
                case State.Recording:
                    if (s_active == this) Finish();
                    break;
            }
        }

        CanvasGroup _dim;

        void Update()
        {
            // Dimmed over the game picker (it can't be tapped there).
            float alpha = GamePicker.IsOpen && s_state == State.Idle ? 0.35f : 1f;
            if (!_dim && alpha < 1f) _dim = gameObject.AddComponent<CanvasGroup>();
            if (_dim && _dim.alpha != alpha) _dim.alpha = alpha;
            // Held: its options.
            if (_downAt >= 0f && Time.unscaledTime - _downAt >= HoldSeconds)
            {
                _downAt = -1f;
                _swallowClick = true;
                Haptics.Play(Haptics.Kind.Medium);
                SettingsAudioRows.OpenCapOptions();
            }
        }

        // ---- the recording, run by Host ----
        // Not by the key itself: the screen menu's CAP key is put away (its
        // list closed) the moment it's tapped, and a switched-off key's
        // coroutines and Update stop - the countdown froze on 3 and the
        // length never ran out. Host is always on.
        sealed class Host : MonoBehaviour
        {
            static Host s_host;

            static Host Get()
            {
                if (s_host) return s_host;
                var go = new GameObject("ARcade Cap Host");
                DontDestroyOnLoad(go);
                return s_host = go.AddComponent<Host>();
            }

            public static string Name => Get().gameObject.name;
            public static void Run(IEnumerator routine) => Get().StartCoroutine(routine);

            void Update()
            {
                if (s_active) s_active.Tick();
                // The finished file's message never came (it should within a second or two): CAP's let go anyway.
                if (s_state == State.Finishing && !Capturing && s_stoppedAt > 0f && Time.unscaledTime - s_stoppedAt > 15f)
                {
                    s_stoppedAt = -1f;
                    if (s_active) s_active.OnRecorded("");
                    else s_state = State.Idle;
                }
            }

            // From SERecorder.mm (by this object's name): the file's path, or "".
            public void OnRecorded(string path)
            {
                if (s_active) s_active.OnRecorded(path);
                else s_state = State.Idle;
            }

            // From SEShare.mm: the share sheet's gone - a 3, 2, 1, then the game goes on.
            public void OnShareClosed(string unused) => ShareClosed();

            public static void ShareClosed()
            {
                if (!Sharing) return;
                Sharing = false;
                // (Under a menu that's holding the game anyway: that lets it go itself.)
                bool held = GamePicker.IsOpen || SettingsScreen.IsOpen || LeaderboardWindow.IsOpen || SavesWindow.IsOpen || MenuPause.Held;
                MobileRetroDepthLayerStack.Paused = true;
                if (!held) RunCountdown(MenuPause.ResumeNow);
            }
        }

        void Tick()
        {
            if (s_active != this || (s_state != State.Recording && s_state != State.Finishing)) return;
            // The game picker opened: the video ends there (it's only of games).
            if (s_state == State.Recording && GamePicker.IsOpen) { Finish(); return; }
            float t = Time.unscaledTime - _started;
            if (_dot) _dot.enabled = Mathf.Repeat(t, 1f) < 0.6f;   // blinking
            AudioGain = Level(t);
            if (s_state == State.Recording && _endAt < 0f && t >= MaxSeconds) Finish();
            if (_endAt >= 0f && t >= _endAt) Stop();
        }

        void OnDestroy()
        {
            if (s_active != this || s_state == State.Idle) return;
            // Its key gone mid-recording: the file's finished off (not left open), then idle.
            if (Capturing) Stop();
            Release();
            Capturing = false;
            s_state = State.Idle;
            s_active = null;
        }

        IEnumerator Begin()
        {
            s_state = State.Counting;
            yield return Countdown();
            if (s_active != this || s_state != State.Counting) yield break;   // (its key went meanwhile)
            if (GamePicker.IsOpen) { s_state = State.Idle; s_active = null; ResumeOnRecord = false; yield break; }   // (the picker opened meanwhile)

            int w = VideoWidth, h = VideoHeight;
            _tv = TvOutput.Showing && TvOutput.Camera;
            var source = new Vector2Int(Screen.width, Screen.height);
            if (_tv)
            {
                // The TV's size, its shape kept, no more than 1080 lines (and even, for H.264).
                source = Vector2Int.RoundToInt(TvOutput.Size);
                float k = Mathf.Min(1f, 1080f / source.y, 1920f / source.x);
                w = Mathf.RoundToInt(source.x * k / 2f) * 2;
                h = Mathf.RoundToInt(source.y * k / 2f) * 2;
            }
            _screen = new RenderTexture(source.x, source.y, 24, RenderTextureFormat.ARGB32) { name = "ARcade Rec Screen" };
            _screen.Create();
            _frame = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "ARcade Rec" };
            _frame.Create();
            _pixels = new byte[w * h * 4];
            _watermark = Resources.Load<Texture2D>("Capture/Watermark");
            _path = Path.Combine(Application.temporaryCachePath, $"ARcade {DateTime.Now:yyyy-MM-dd HH.mm.ss}.mp4");
            _fadeIn = FadeIn;
            _fadeOut = FadeOut;
            _endAt = Length > 0 ? Length : -1f;
            AudioTap.Ensure();
            AudioGain = _fadeIn ? 0f : 1f;

            var audio = AudioSettings.GetConfiguration();
            int channels = audio.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
#if UNITY_IOS && !UNITY_EDITOR
            // (Once more a moment later if the writer won't start - the first try can miss.)
            bool started = SE_RecStart(_path, w, h, Fps, audio.sampleRate, channels);
            if (!started)
            {
                yield return null;
                _path = Path.Combine(Application.temporaryCachePath, $"ARcade {DateTime.Now:yyyy-MM-dd HH.mm.ss} b.mp4");
                started = SE_RecStart(_path, w, h, Fps, audio.sampleRate, channels);
            }
            if (!started)
            {
                Debug.LogError("[ScreenCap] couldn't start recording");
                Release();
                s_state = State.Idle;
                if (ResumeOnRecord) { ResumeOnRecord = false; MenuPause.ResumeNow(); }
                var picker = FindAnyObjectByType<GamePicker>(); if (picker) picker.ShowMessage("Couldn't start recording - try CAP again", true);
                yield break;
            }
#endif
            _started = Time.unscaledTime;
            _lastFrame = -1f;
            _inFlight = 0;
            _editorFrameSaved = false;
            Capturing = true;
            // Opened from a menu: the game was held through the countdown - it goes on as recording begins.
            if (ResumeOnRecord) { ResumeOnRecord = false; MenuPause.ResumeNow(); }
            s_state = State.Recording;
            Show();
            Haptics.Play(Haptics.Kind.Medium);
            Host.Run(Frames());
        }

        // STOP: with FADE OUT, half a second more fading out to black, then it stops.
        void Finish()
        {
            if (s_state != State.Recording) return;
            float t = Time.unscaledTime - _started;
            s_state = State.Finishing;
            Haptics.Play(Haptics.Kind.Medium);
            if (_fadeOut) _endAt = t + FadeSeconds;
            else Stop();
        }

        void Stop()
        {
            if (s_state == State.Idle || !Capturing) return;
            s_state = State.Finishing;
            Capturing = false;
            s_stoppedAt = Time.unscaledTime;
            AudioGain = 1f;
            if (_dot) _dot.enabled = true;
            _endAt = -1f;
#if UNITY_IOS && !UNITY_EDITOR
            SE_RecStop(Host.Name, nameof(OnRecorded));
#else
            OnRecorded("");
#endif
        }

        // From SERecorder.mm once the file's written: its path, or "" if it failed.
        public void OnRecorded(string path)
        {
            Release();
            s_state = State.Idle;
            Show();
            if (string.IsNullOrEmpty(path)) return;
#if UNITY_IOS && !UNITY_EDITOR
            // The game holds while the share sheet's up (Host.OnShareClosed lets it go) - first SCREEN CAP DONE a moment,
            // so a press still meant for the game doesn't land on the sheet's keys.
            Sharing = true;
            MobileRetroDepthLayerStack.Paused = true;
            Host.Run(DoneThenShare(path));
#endif
        }

        const float DoneSeconds = 1.5f;

        static IEnumerator DoneThenShare(string path)
        {
            yield return Done();
#if UNITY_IOS && !UNITY_EDITOR
            // With a controller: ARcade's own window, which it can steer (iOS's sheet it can't) - on the TV with the menus.
            if (UnityEngine.InputSystem.Gamepad.current != null
                && ShareWindow.Open(path, share => { if (share) SE_ShareFile(path, Host.Name, "OnShareClosed"); else Host.ShareClosed(); }))
                yield break;
            SE_ShareFile(path, Host.Name, "OnShareClosed");
#endif
        }

        // SCREEN CAP DONE, in white on three lines, popping up over the held game a moment.
        static IEnumerator Done()
        {
            var overlay = Overlay("Cap Done", out var root, 32010);
            var label = new GameObject("Done", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(root, false);
            label.rectTransform.sizeDelta = new Vector2(360f, 160f);
            var font = ArcadeFont();
            if (font) label.font = font;
            label.fontSize = 14f;   // (the canvas fits 390 wide, so on a landscape screen this reads far bigger)
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.text = "SCREEN\nCAP\nDONE";
            Haptics.Play(Haptics.Kind.Medium);
            for (float t = 0f; t < DoneSeconds; t += Time.unscaledDeltaTime)
            {
                label.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, Mathf.Min(1f, t * 6f));
                label.alpha = t < DoneSeconds - 0.25f ? 1f : (DoneSeconds - t) / 0.25f;
                yield return null;
            }
            Destroy(overlay);
            yield return null;
        }

        void Release()
        {
            foreach (var texture in new[] { _screen, _frame })
                if (texture) { texture.Release(); Destroy(texture); }
            _screen = _frame = null;
        }

        // The picture's (and sound's) level at t seconds in: 0 black, 1 full.
        float Level(float t)
        {
            float level = _fadeIn ? Mathf.Clamp01(t / FadeSeconds) : 1f;
            if (_fadeOut && _endAt >= 0f) level = Mathf.Min(level, Mathf.Clamp01((_endAt - t) / FadeSeconds));
            return level;
        }

        // Each frame (no more than Fps a second): the finished screen,
        // fitted into the video's frame with the fade and the watermark, read
        // back and handed to the writer.
        IEnumerator Frames()
        {
            var end = new WaitForEndOfFrame();
            while ((s_state == State.Recording || s_state == State.Finishing) && s_active == this && Capturing)
            {
                yield return end;
                if (!Capturing || !_frame) break;
                float now = Time.unscaledTime;
                if (_lastFrame >= 0f && now - _lastFrame < 1f / Fps - 0.004f) continue;
                if (_inFlight >= 3) continue;   // (the GPU's behind: skip one)
                _lastFrame = now;
                if (_tv)
                {
                    // (The TV gone meanwhile: the video ends there.)
                    var tv = TvOutput.Camera;
                    if (!tv) { Finish(); continue; }
                    var target = tv.targetTexture;
                    tv.targetTexture = _screen;
                    tv.Render();
                    tv.targetTexture = target;
                }
                else ScreenCapture.CaptureScreenshotIntoRenderTexture(_screen);
                Compose(now - _started);
                _inFlight++;
                AsyncGPUReadback.Request(_frame, 0, TextureFormat.BGRA32, OnReadback);
            }
        }

        void Compose(float t)
        {
            int w = _frame.width, h = _frame.height;
            var active = RenderTexture.active;
            RenderTexture.active = _frame;
            GL.Clear(false, true, new Color(0.03f, 0.02f, 0.06f, 1f));   // (the bars down the sides)
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, w, h, 0);
            // The whole screen, as big as fits.
            float fit = Mathf.Min(w / (float)_screen.width, h / (float)_screen.height);
            float sw = _screen.width * fit, sh = _screen.height * fit;
            var picture = new Rect((w - sw) * 0.5f, (h - sh) * 0.5f, sw, sh);
            if (FlipScreen && !_tv) Graphics.DrawTexture(picture, _screen, new Rect(0, 1, 1, -1), 0, 0, 0, 0);
            else Graphics.DrawTexture(picture, _screen);
            Watermark(t, w, h);
            float level = Level(t);
            if (level < 1f)
                Graphics.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture, new Rect(0, 0, 1, 1), 0, 0, 0, 0, new Color(0f, 0f, 0f, 0.5f * (1f - level)));
            GL.PopMatrix();
            RenderTexture.active = active;
        }

        // The screen's copy comes upside down where textures start at the top
        // (Metal, the iPhone's). (Not the TV camera's: drawn into a texture, it's the right way up.)
        static bool FlipScreen => SystemInfo.graphicsUVStartsAtTop;

        // The watermark on the left, just above the joystick: from the end of any fade in, it
        // grows from nothing (a little past its size, then back), jiggles,
        // and WatermarkSeconds on shrinks away.
        void Watermark(float t, int w, int h)
        {
            if (!_watermark) return;
            float t0 = _fadeIn ? FadeSeconds : 0f, local = t - t0;
            if (local < 0f || local > WatermarkSeconds) return;
            float grow = Mathf.Clamp01(local / WatermarkPop), shrink = Mathf.Clamp01((WatermarkSeconds - local) / WatermarkPop);
            float scale = Mathf.Min(BackOut(grow), Smooth(shrink));
            float alpha = Mathf.Min(grow, shrink);
            if (scale <= 0.001f) return;
            // The jiggle: a quick wobble that settles, while it's up.
            float wobble = Mathf.Sin(local * 18f) * 4f * Mathf.Exp(-Mathf.Max(0f, local - WatermarkPop) * 1.6f) * grow;
            // (A whole-pixel scale, about WatermarkShare x WatermarkSize across: never a fraction of
            // its pixels, which would drop its one-pixel grid lines and glow.)
            // (On the TV's wide video: sized as if it were 3:4, low in the black at the left.)
            float across = _tv ? h * 0.75f : w;
            float k = Mathf.Max(1, Mathf.RoundToInt(across * WatermarkShare * WatermarkSize / _watermark.width));
            float mw = Mathf.Round(_watermark.width * k), mh = Mathf.Round(_watermark.height * k), margin = Mathf.Round(across * 0.05f);
            // (Its bottom a little above the on-screen joystick's ring, where it rests.)
            var centre = new Vector3(margin + mw * 0.5f, Mathf.Round(h * (_tv ? 0.92f : WatermarkBottom) - mh * 0.5f), 0f);
            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.TRS(centre, Quaternion.Euler(0f, 0f, wobble), Vector3.one * scale));
            // (DrawTexture's colour: 0.5 is the texture as it is.)
            Graphics.DrawTexture(new Rect(-mw * 0.5f, -mh * 0.5f, mw, mh), _watermark, new Rect(0, 0, 1, 1), 0, 0, 0, 0, new Color(0.5f, 0.5f, 0.5f, 0.5f * alpha));
            GL.PopMatrix();
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);

        // 0 to 1, overshooting a little before settling (a pop).
        static float BackOut(float x)
        {
            const float s = 1.9f;
            x -= 1f;
            return x * x * ((s + 1f) * x + s) + 1f;
        }

        void OnReadback(AsyncGPUReadbackRequest request)
        {
            _inFlight--;
            if (request.hasError || _pixels == null || !Capturing) return;
            var data = request.GetData<byte>();
            if (data.Length != _pixels.Length) return;
            data.CopyTo(_pixels);
#if UNITY_IOS && !UNITY_EDITOR
            SE_RecVideoFrame(_pixels, request.width, request.height);
#else
            // (A frame with the watermark up, to look at.)
            float t = Time.unscaledTime - _started;
            if (!_editorFrameSaved && t >= (_fadeIn ? FadeSeconds : 0f) + 1.2f)
            {
                _editorFrameSaved = true;
                var picture = new Texture2D(request.width, request.height, TextureFormat.BGRA32, false);
                picture.LoadRawTextureData(_pixels);
                picture.Apply();
                string file = Path.ChangeExtension(_path, ".png");
                File.WriteAllBytes(file, picture.EncodeToPNG());
                Destroy(picture);
                Debug.Log($"[ScreenCap] Editor: a frame saved {file}");
            }
#endif
        }

        // ---- on screen ----

        static IEnumerator Countdown()
        {
            var overlay = Overlay("Cap Countdown", out var root, 32010);
            var number = new GameObject("Number", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            number.rectTransform.SetParent(root, false);
            number.rectTransform.sizeDelta = new Vector2(300f, 200f);
            var font = ArcadeFont();
            if (font) number.font = font;
            number.fontSize = 96f;
            number.alignment = TextAlignmentOptions.Center;
            number.color = Color.white;
            for (int n = 3; n >= 1; n--)
            {
                number.text = n.ToString();
                Haptics.Play(Haptics.Kind.Tick);
                for (float t = 0f; t < StepSeconds; t += Time.unscaledDeltaTime)
                {
                    float k = t / StepSeconds;
                    number.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, Mathf.Min(1f, k * 4f));
                    number.alpha = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                    yield return null;
                }
            }
            Destroy(overlay);
            yield return null;   // (gone from the screen before the first frame's taken)
        }

        // A full-screen canvas on top of everything.
        static GameObject Overlay(string name, out RectTransform root, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.matchWidthOrHeight = 0f;
            root = (RectTransform)go.transform;
            return go;
        }

        // The picker's big pixel font (GAMES, SELECT GAME), for the countdown.
        static TMP_FontAsset ArcadeFont()
        {
            foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (text.font && text.font.name.Contains("Arcade")) return text.font;
            return null;
        }

        // ---- the sound ----

        // On the AudioListener: Unity's whole mix, passed through untouched,
        // a copy (at the video's fade level) to the recorder while recording.
        class AudioTap : MonoBehaviour
        {
            float[] _copy = new float[4096];

            public static void Ensure()
            {
                var listener = FindAnyObjectByType<AudioListener>();
                if (listener && !listener.GetComponent<AudioTap>()) listener.gameObject.AddComponent<AudioTap>();
            }

            void OnAudioFilterRead(float[] data, int channels)
            {
#if UNITY_IOS && !UNITY_EDITOR
                if (!Capturing) return;
                float gain = AudioGain;
                if (gain >= 0.999f) { SE_RecAudio(data, data.Length, channels); return; }
                if (_copy.Length < data.Length) _copy = new float[data.Length];
                for (int i = 0; i < data.Length; i++) _copy[i] = data[i] * gain;
                SE_RecAudio(_copy, data.Length, channels);
#endif
            }
        }
    }
}
