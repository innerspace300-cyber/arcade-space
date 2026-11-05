// TvOutput.cs — the game on a TV. With Screen Mirroring on to an Apple TV (Control
// Center; an app can't start it itself), iOS gives the app the TV as a second display
// (IosBuildPostprocessor's scene role, Plugins/iOS/SEExternalDisplay.mm) instead of
// mirroring the phone. Seen, it's switched on at 1080p and the game goes into full
// screen (FullScreenTest) with its picture drawn there, on the TV - the game alone,
// no buttons - while the phone is the controller: AR off, black, the on-screen controls
// (hidden with a controller connected, GamepadInput's Auto) and the menus. The TV
// gone, the phone's back as it was (AR again, unless it was in full screen before).
// With a controller connected the menus go to the TV too (MenusOnTv: the three bars,
// the gear, the dropdown, Settings, the game list, the windows and prompts, the
// controller's gold frame, the spell wheel over the knight), steered by the controller
// (ControllerMenuNav); without one they stay on the phone, to be tapped.
//
// A controller the phone sits in (a Razer Kishi, a Backbone: Handheld, SEDevice.mm's SE_ControllerAttached)
// makes the phone its own TV: it turns on its side (landscape, either way up), the game in full screen only,
// drawn and laid out as on a TV - the menus where they'd be there, steered by the controller, no on-screen
// keys (GamepadInput) - and back upright when the controller comes off, still in full screen, the game held
// till its keys are back and a 3, 2, 1 has run (HoldThroughTurn). A real TV comes first, and the phone stays on
// its side there while it's still in the controller (Sideways) - PLAYING ON TV, the share sheet, the game list
// (TvMenuMirror) all that way round - upright again once it's out.

using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpatialEmulator.UI
{
    public class TvOutput : MonoBehaviour
    {
        /// The game's picture is on the TV (display 1), not the phone.
        public static bool Showing { get; private set; }
        /// The phone is its own TV (a controller it sits in): the game drawn as on a TV, on the phone's screen, landscape.
        public static bool Handheld { get; private set; }
        /// The phone's on its side: in a controller it sits in, handheld or playing on a TV.
        public static bool Sideways { get; private set; }
        /// A controller the phone sits in is on it (PadLabels: a Kishi's own button names).
        public static bool GripOn => s_self && s_self._attached;
        /// The display the game's drawn on: the TV's (1), or the phone's own (0) when it's handheld.
        public static int Display => Handheld ? 0 : 1;
        /// The TV's camera, drawing FullScreenTest's picture (a canvas on Layer, in front of it - an overlay
        /// canvas on a second display comes out upside down on the phone's Metal).
        public static Camera Camera { get; private set; }
        public const int Layer = 31;   // (an unnamed layer nothing else is on)

        /// The menus are on the TV (a controller's connected to steer them), not the phone.
        public static bool MenusOnTv => Showing && Camera && Gamepad.current != null;
        /// The TV's size, in pixels.
        public static Vector2 Size => !Handheld && UnityEngine.Display.displays.Length > Display
            ? new Vector2(UnityEngine.Display.displays[Display].renderingWidth, UnityEngine.Display.displays[Display].renderingHeight)
            : new Vector2(Screen.width, Screen.height);
        /// This canvas is drawn on the TV.
        public static bool OnTv(Canvas canvas) => canvas && Camera && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.rootCanvas.worldCamera == Camera;
        /// The size of the screen this canvas is on, in pixels.
        public static Vector2 ScreenOf(Canvas canvas) => OnTv(canvas) ? Size : new Vector2(Screen.width, Screen.height);

        // The phone's own canvases moved onto the TV, with the layer each was on.
        readonly Dictionary<Canvas, int> _moved = new Dictionary<Canvas, int>();
        readonly List<Canvas> _canvases = new List<Canvas>();
        int _found = -100;

        // (A moment for the TV's scene to connect before its window's made: SEExternalDisplay.mm.)
        const float SettleTime = 0.5f;
        float _seenAt = -1f;
        bool _wasFull;
        Camera _camera;
        GameObject _label;
        bool _attached;
        float _checkAt;
        float _turnFreeAt = -1f;   // (handheld: free to turn either way up from then)

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int SE_ControllerAttached();
        static bool ControllerAttached() => SE_ControllerAttached() != 0;
#else
        /// Editor: as if a Kishi were on (probing the handheld layout in play mode).
        public static bool EditorAttached;
        static bool ControllerAttached() => EditorAttached;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("TV Output");
            DontDestroyOnLoad(go);
            go.transform.position = new Vector3(0f, -10000f, 0f);   // (its camera well away from the room and the games)
            s_self = go.AddComponent<TvOutput>();
        }

        static TvOutput s_self;

        /// A canvas just made or shown (the spell wheel over the knight) onto the TV now, with the menus there -
        /// not drawn on the phone at the phone's size first, till the next look round for new ones.
        public static void Adopt(Canvas c)
        {
            if (!s_self || !c || !MenusOnTv || s_self._moved.ContainsKey(c) || !c.isRootCanvas || !Moves(c)) return;
            s_self.ToTv(c);
        }

        void Update()
        {
            // (Asked twice a second: a controller clipped on or off.)
            if (Time.unscaledTime >= _checkAt) { _checkAt = Time.unscaledTime + 0.5f; _attached = ControllerAttached(); }
            bool tv = UnityEngine.Display.displays.Length > 1;
            if (!tv) _seenAt = -1f;
            // A real TV over the phone in hand; switching from one to the other starts over.
            if (Showing && (Handheld ? tv || !_attached : !tv)) Stop();
            // (Handheld: snapped onto its side first, then free to turn either way up - both asked at once, the second won.)
            if (Sideways && _turnFreeAt >= 0f && Time.unscaledTime >= _turnFreeAt) { _turnFreeAt = -1f; Screen.orientation = ScreenOrientation.AutoRotation; }
            if (Showing)
            {
                if (!Handheld) Turn(_attached);   // (on a TV: a controller clipped on or off)
                // (Full screen switched off from the phone's settings: the TV keeps the game.)
                if (!FullScreenTest.Enabled) FullScreenTest.EnterWithCabinet();
                return;
            }
            if (tv)
            {
                if (_seenAt < 0f) _seenAt = Time.unscaledTime;
                if (Time.unscaledTime - _seenAt < SettleTime) return;
                Begin(false);
            }
            else if (_attached) Begin(true);
        }

        void LateUpdate()
        {
            if (s_resortAt > 0 && Time.frameCount >= s_resortAt) { s_resortAt = -1; Resort(); }
            if (s_resortAgainAt > 0 && Time.frameCount >= s_resortAgainAt) { s_resortAgainAt = -1; Resort(); }
            bool menus = MenusOnTv;
            if (menus)
            {
                // (Looked for again now and then: windows and prompts are made as they open.)
                if (Time.frameCount - _found > 15)
                {
                    _found = Time.frameCount;
                    _canvases.Clear();
                    foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
                        if (c.isRootCanvas && !_moved.ContainsKey(c) && Moves(c)) _canvases.Add(c);
                    foreach (var c in _canvases) ToTv(c);
                }
                var gone = new List<Canvas>();
                foreach (var pair in _moved)
                {
                    if (!pair.Key) { gone.Add(pair.Key); continue; }
                    if (pair.Key.gameObject.activeInHierarchy) OnLayer(pair.Key.transform, Layer);   // (keys made since)
                }
                foreach (var c in gone) _moved.Remove(c);
            }
            else if (_moved.Count > 0)
            {
                foreach (var pair in _moved)
                {
                    if (!pair.Key) continue;
                    BackToOverlay(pair.Key);
                    OnLayer(pair.Key.transform, pair.Value);
                }
                _moved.Clear();
            }
        }

        // The phone's own screen canvases, except the touch controls and what's the phone's alone.
        static bool Moves(Canvas c)
        {
            if (c.renderMode != RenderMode.ScreenSpaceOverlay) return false;
            if (c.name == "ArcadeControls" || c.name == "TV Label" || c.name == "TV Mirror" || c.name == "UI") return false;   // ("UI": AR Foundation's sample screen)
            if (c.GetComponent<FullScreenTest>() || c.GetComponent<StartupSplash>() || c.GetComponent<PerfReadout>()) return false;   // (full screen moves its own picture)
            if (c.GetComponentInParent<Canvas>(true) != c) return false;
            return c.transform.root.name != "DebugMenu";
        }

        void ToTv(Canvas c)
        {
            _moved[c] = c.gameObject.layer;
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = Camera;
            c.planeDistance = 1f;
            OnLayer(c.transform, Layer);
        }

        /// A canvas back on the phone's screen (off the TV's camera) - and the overlays re-sorted a few frames on: one
        /// turned back into an overlay keeps a stale place among them (full screen's black came back over the on-screen
        /// keys, the three bars and the gear after the phone came out of a Kishi), and re-sorting it the same frame
        /// doesn't take.
        public static void BackToOverlay(Canvas c)
        {
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.worldCamera = null;
            ResortSoon();
        }

        static void ResortSoon()
        {
            s_resortAt = Time.frameCount + 2;
            s_resortAgainAt = Time.frameCount + 15;
        }

        static int s_resortAt = -1, s_resortAgainAt = -1;

        // Every overlay on the phone nudged out of its order and back, switched off and on: put back in their places.
        static void Resort()
        {
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay || !c.enabled) continue;
                int order = c.sortingOrder;
                c.sortingOrder = order + 1;
                c.sortingOrder = order;
                c.enabled = false;
                c.enabled = true;
            }
        }

        static void OnLayer(Transform t, int layer)
        {
            if (t.gameObject.layer != layer) t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) OnLayer(t.GetChild(i), layer);
        }

        void Begin(bool handheld)
        {
            Debug.Log($"[TvOutput] begin {(handheld ? "handheld" : "tv")} full={FullScreenTest.Enabled} core={Mobile.LibretroCore.IsRunning} frame={Time.frameCount}");
            Handheld = handheld;
            Turn(handheld || _attached);
            if (!handheld)
            {
                var tv = UnityEngine.Display.displays[Display];
                if (!tv.active) tv.Activate();
                // 1080 lines (a 4K TV's own size is four times the work for the same pixel art).
                if (tv.systemHeight > 1080)
                    tv.SetRenderingResolution(Mathf.RoundToInt(1080f * tv.systemWidth / tv.systemHeight), 1080);
            }
            _camera = new GameObject("TV Camera").AddComponent<Camera>();
            _camera.transform.SetParent(transform, false);
            _camera.targetDisplay = Display;
            _camera.depth = 100;   // (on the phone, over its own cameras: the game as on a TV)
            _camera.cullingMask = 1 << Layer;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 10f;
            Camera = _camera;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _wasFull = FullScreenTest.Enabled;
            Showing = true;
            if (!_wasFull) FullScreenTest.EnterWithCabinet();
            ShowLabel(!handheld);
        }

        void Stop()
        {
            Debug.Log($"[TvOutput] stop {(Handheld ? "handheld" : "tv")} full={FullScreenTest.Enabled} core={Mobile.LibretroCore.IsRunning} frame={Time.frameCount}");
            Showing = false;
            ResortSoon();   // (full screen's own canvas comes back off the TV's camera after this)
            if (_camera) Destroy(_camera.gameObject);
            Camera = null;
            // Back to AR as it was - but not out of the controller (it stays in full screen, only upright again: going
            // back to AR drops the cabinet, and a game still loading into it froze), nor in a full-screen-only game.
            bool fullOnly = Games.GameCatalog.Find(Games.GameSelection.Current) is { FullScreenOnly: true };
            // (Nor off a TV with the phone in a controller: it goes straight to handheld, full screen again.)
            if (!_wasFull && !Handheld && !fullOnly && !_attached) FullScreenTest.Enabled = false;
            ShowLabel(false);
            bool toTv = UnityEngine.Display.displays.Length > 1;
            if (Handheld)
            {
                Handheld = false;
                // Going over to a TV the phone stays on its side (Begin sorts it out), and the game just carries on there.
                if (!toTv)
                {
                    Turn(false);
                    // Mid-game: held, turned upright, the screen's keys back, then a 3, 2, 1 and on.
                    if (Mobile.LibretroCore.IsRunning || Demo.LayeredDemo.AnyRunning) StartCoroutine(HoldThroughTurn());
                }
            }
            else if (!_attached) Turn(false);
        }

        // On its side, either way up - the controller's grips either side of it - or upright again, as the app always
        // is otherwise. (Info.plist too, so it opens upright: Unity's view controller allows the rest.)
        void Turn(bool sideways)
        {
            if (sideways == Sideways) return;
            Sideways = sideways;
            Screen.autorotateToPortrait = !sideways;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = sideways;
            Screen.orientation = sideways ? ScreenOrientation.LandscapeLeft : ScreenOrientation.Portrait;
            _turnFreeAt = sideways ? Time.unscaledTime + 1f : -1f;
            Debug.Log($"[TvOutput] {(sideways ? "sideways" : "upright")} asked");
        }

        // The game held while the phone turns upright and its keys come back (the controller dropped:
        // GamepadInput), then ScreenCap's 3, 2, 1 - not if a menu's holding it anyway (that lets it go itself).
        System.Collections.IEnumerator HoldThroughTurn()
        {
            if (MenuPause.Held) yield break;
            Mobile.MobileRetroDepthLayerStack.Paused = true;
            AudioListener.pause = true;
            float until = Time.unscaledTime + 2f;   // (however long it takes, no more than this)
            while (Time.unscaledTime < until && (Screen.width > Screen.height || Gamepad.all.Count > 0)) yield return null;
            yield return new WaitForSecondsRealtime(0.4f);   // (the keys drawn again: Resort)
            if (MenuPause.Held) yield break;   // (a menu opened meanwhile)
            if (Showing) { MenuPause.ResumeNow(); yield break; }   // (back in a controller, or on a TV: on with the game)
            ScreenCap.RunCountdown(MenuPause.ResumeNow);
        }

        // "ON TV" on the phone's black, in the menus' own font, so it's not taken for a blank screen.
        void ShowLabel(bool on)
        {
            if (!on) { if (_label) _label.SetActive(false); return; }
            if (!_label)
            {
                var canvas = new GameObject("TV Label", typeof(Canvas)).GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = -9;   // (over full screen's black, under the controls)
                canvas.transform.SetParent(transform, false);
                var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
                text.transform.SetParent(canvas.transform, false);
                var like = FindAnyObjectByType<GamePicker>(FindObjectsInactive.Include)?.openButton?.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (like) text.font = like.font;
                text.text = "PLAYING ON TV";
                text.alignment = TMPro.TextAlignmentOptions.Center;
                text.color = new Color(1f, 1f, 1f, 0.35f);
                text.raycastTarget = false;
                var rect = text.rectTransform;
                // (Either way up: the phone can turn with this up.)
                rect.anchorMin = new Vector2(0f, 0.4f);
                rect.anchorMax = new Vector2(1f, 0.6f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                text.fontSize = Mathf.Min(Screen.width, Screen.height) * 0.05f;
                _label = canvas.gameObject;
            }
            _label.SetActive(true);
        }
    }
}
