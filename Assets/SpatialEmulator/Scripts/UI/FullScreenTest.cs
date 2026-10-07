// FullScreenTest.cs — Settings > SOUND + CAP > FULL SCREEN: plays the game
// flat on the phone's screen with AR off, for games too heavy to keep up in
// AR (CV1000 runs at full speed this way, not in AR). Switched on, it stops
// ARKit (camera, tracking and plane finding), stops drawing the 3D scene (the
// cabinets), and shows the running game filling the screen under the
// on-screen controls; off puts everything back. The picture is
// MAME's own composited frame (upright, LibretroCore.WholeFrame), so it works
// for every game; the layer stack stops uploading its layers meanwhile. The
// game list stays on the screen too (its AR setting is switched off). Not
// kept between launches, so the app always starts in AR (a cabinet has to be
// placed to run a game).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using SpatialEmulator.Games;
using SpatialEmulator.Mobile;

namespace SpatialEmulator.UI
{
    public class FullScreenTest : MonoBehaviour
    {
        static FullScreenTest s_instance;

        public static bool Enabled
        {
            get => s_instance && s_instance.enabled;
            set
            {
                if (value == Enabled) return;
                if (value)
                {
                    if (!s_instance)
                    {
                        var go = new GameObject("Full Screen Test");
                        DontDestroyOnLoad(go);
                        s_instance = go.AddComponent<FullScreenTest>();
                    }
                    s_instance.enabled = true;
                }
                else
                {
                    s_instance.enabled = false;
                    // Back in AR the old cabinet's place can't be trusted (ARKit
                    // was off): it goes, plane scanning starts over and the
                    // player places a fresh one, where the game starts again.
                    var gate = FindAnyObjectByType<SingleCabinetGate>();
                    if (gate && gate.cabinet) Destroy(gate.cabinet);
                }
            }
        }

        Canvas _canvas;
        RawImage _image;
        ARSession _session;
        ARPlaneManager _planes;
        bool _planesWere;
        ARCameraBackground _background;
        Camera _camera;
        Texture2D _texture;
        // The demo: its layers (render textures) flat, back to front, in a
        // 4:3 picture, the health bar and score above it.
        RectTransform _demoPicture;
        RawImage _demoBar;
        readonly List<RawImage> _demoLayers = new List<RawImage>();
        Demo.PitDemoGame _demo;
        GameObject _arKey;
        int _shown = -1;
        int _cullingMask;
        CameraClearFlags _clear;
        Color _color;

        void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = -10;   // under the controls and menus
            var back = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            back.transform.SetParent(transform, false);
            back.color = Color.black;
            back.raycastTarget = false;
            Stretch(back.rectTransform);
            _image = new GameObject("Game", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _image.transform.SetParent(transform, false);
            _image.raycastTarget = false;
            _image.uvRect = new Rect(0, 1, 1, -1);   // (rows come top first)
            var rect = _image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

            _demoPicture = new GameObject("Demo", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            _demoPicture.SetParent(transform, false);
            _demoBar = new GameObject("Demo Bar", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _demoBar.transform.SetParent(transform, false);
            _demoBar.raycastTarget = false;
        }

        /// Into full screen for a game picked with no cabinet placed yet (a
        /// full-screen-only one): a cabinet's put in front of the camera to
        /// run it - unseen in full screen, and gone again back in AR.
        public static void EnterWithCabinet()
        {
            var gate = FindAnyObjectByType<SingleCabinetGate>();
            var cam = Camera.main;
            if (gate && !gate.cabinet && gate.spawner && cam)
            {
                bool inView = gate.spawner.onlySpawnInView;
                gate.spawner.onlySpawnInView = false;
                gate.spawner.TrySpawnObject(cam.transform.position + cam.transform.forward * 1.5f, Vector3.up);
                gate.spawner.onlySpawnInView = inView;
            }
            Enabled = true;
        }

        /// A thumbnail of the game's latest frame (for a save state: the layers
        /// aren't kept up in full screen), rows bottom-up, or null.
        public static Texture2D CaptureFrame(int downscale = 2)
        {
            var source = s_instance && s_instance.enabled ? s_instance._texture : null;
            if (!source) return null;
            int w = source.width, h = source.height, tw = w / downscale, th = h / downscale;
            var full = source.GetPixels32();
            var small = new Color32[tw * th];
            // (Its rows come top first: flipped.)
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

        /// Back to AR, to place a cabinet (see Enabled).
        public static void ReturnToAR() => Enabled = false;

        // The AR key beside GAMES: back to the cabinet - or,
        // with a full-screen-only game on, a prompt to pick another for AR.
        void MakeArKey()
        {
            if (_arKey) return;
            var picker = FindAnyObjectByType<GamePicker>(FindObjectsInactive.Include);
            if (!picker || !picker.openButton) return;
            var games = (RectTransform)picker.openButton.transform;
            var key = (RectTransform)Instantiate(games.gameObject, games.parent, false).transform;
            key.name = "AR Button";
            key.SetSiblingIndex(games.GetSiblingIndex() + 1);
            float left = games.anchoredPosition.x + games.sizeDelta.x * 0.5f;
            key.sizeDelta = new Vector2(24, games.sizeDelta.y);
            key.anchoredPosition = new Vector2(left + 4 + 12, games.anchoredPosition.y);
            var label = key.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label) label.text = "AR";
            var button = key.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                Haptics.Play(Haptics.Kind.Light);
                if (GameCatalog.Find(GameSelection.Current) is { FullScreenOnly: true })
                    HeavyGamePrompt.NotInAR(picker);
                else ReturnToAR();
            });
            _arKey = key.gameObject;
        }

        void OnEnable()
        {
            MakeArKey();
            _canvas.enabled = true;
            LibretroCore.WholeFrame = true;
            // The game list in AR can't be seen with AR off: back on the screen.
            if (AppSettings.PickerInAR) AppSettings.PickerInAR = false;
            _shown = -1;
            _session = FindAnyObjectByType<ARSession>();
            if (_session) _session.enabled = false;
            _planes = FindAnyObjectByType<ARPlaneManager>();
            if (_planes) { _planesWere = _planes.enabled; _planes.enabled = false; }
            _camera = Camera.main;
            if (_camera)
            {
                _background = _camera.GetComponent<ARCameraBackground>();
                if (_background) _background.enabled = false;
                _cullingMask = _camera.cullingMask;
                _clear = _camera.clearFlags;
                _color = _camera.backgroundColor;
                _camera.cullingMask = 0;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = Color.black;
            }
        }

        void OnDisable()
        {
            _canvas.enabled = false;
            if (_arKey) _arKey.SetActive(false);
            LibretroCore.WholeFrame = false;
            _image.texture = null;
            if (_camera)
            {
                _camera.cullingMask = _cullingMask;
                _camera.clearFlags = _clear;
                _camera.backgroundColor = _color;
            }
            if (_background) _background.enabled = true;
            if (_planes) _planes.enabled = _planesWere;
            if (_session) _session.enabled = true;
            // Tracking and the plane search start over (CabinetHints).
            CabinetHints.RestartScanning();
        }

        void LateUpdate()
        {
            if (_arKey) _arKey.SetActive(!GamePicker.IsOpen && !SettingsScreen.IsOpen && !MenuDropdown.Exists);
            bool demo = Demo.PitDemoGame.IsRunning && Demo.PitDemoGame.Running;
            _demoPicture.gameObject.SetActive(demo);
            _demoBar.gameObject.SetActive(demo && _demoBar.texture);
            if (demo)
            {
                _image.enabled = false;
                ShowDemo(Demo.PitDemoGame.Running);
                return;
            }
            var pixels = LibretroCore.FlatPixels;
            bool running = pixels != null && LibretroCore.IsRunning;
            _image.enabled = running;
            if (!running) return;
            int w = LibretroCore.FlatWidth, h = LibretroCore.FlatHeight;
            if (pixels.Length != w * h * 4) return;   // (mid-change of size, on the core's thread)
            if (LibretroCore.FlatFrame != _shown)
            {
                _shown = LibretroCore.FlatFrame;
                if (!_texture || _texture.width != w || _texture.height != h)
                {
                    if (_texture) Destroy(_texture);
                    _texture = new Texture2D(w, h, TextureFormat.BGRA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                }
                _texture.LoadRawTextureData(pixels);
                _texture.Apply(false, false);
                _image.texture = _texture;
            }
            // Fit inside the screen at the game's own shape (its pixels
            // aren't always square: LibretroCore.AspectRatio), a vertical
            // game turned upright if its frame comes out sideways (the
            // driver draws it unturned, as its layers).
            float aspect = LibretroCore.AspectRatio > 0 ? (float)LibretroCore.AspectRatio : w / (float)h;
            if ((w < h) != (aspect < 1f)) aspect = 1f / aspect;
            int turn = GameCatalog.Find(GameSelection.Current)?.rotation ?? 0;
            bool sideways = turn % 180 == 90 && w > h;
            if (!sideways) turn = 0;
            float shown = sideways ? 1f / aspect : aspect;
            var screen = ((RectTransform)transform).rect.size;
            var size = screen.x / screen.y > shown ? new Vector2(screen.y * shown, screen.y) : new Vector2(screen.x, screen.x / shown);
            var rect = _image.rectTransform;
            rect.sizeDelta = sideways ? new Vector2(size.y, size.x) : size;
            rect.localEulerAngles = new Vector3(0, 0, -turn);
        }

        void ShowDemo(Demo.PitDemoGame game)
        {
            if (game != _demo || _demoLayers.Count + (_demoBar.texture ? 1 : 0) != game.Layers.Count)
            {
                _demo = game;
                foreach (var old in _demoLayers) Destroy(old.gameObject);
                _demoLayers.Clear();
                _demoBar.texture = null;
                var order = new List<Demo.PitDemoGame.Layer>(game.Layers);
                order.Sort((a, b) => a.depth.CompareTo(b.depth));
                foreach (var layer in order)
                {
                    if (layer.aboveFrame) { _demoBar.texture = layer.texture; continue; }
                    var image = new GameObject(layer.name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                    image.transform.SetParent(_demoPicture, false);
                    image.raycastTarget = false;
                    image.texture = layer.texture;
                    _demoLayers.Add(image);
                }
            }

            // The picture as wide as fits, the bar above it, both centred.
            var stack = MobileRetroDepthLayerStack.Active;
            var screen = ((RectTransform)transform).rect.size;
            const float aspect = 4f / 3f, gap = 0.02f;
            float barAspect = _demoBar.texture ? _demoBar.texture.width / (float)_demoBar.texture.height : 0f;
            float barWidth = stack ? stack.aboveFrameWidth : 1f;
            // height per unit of picture width: the picture, the gap, the bar
            float tall = 1f / aspect + (barAspect > 0 ? gap + barWidth / barAspect : 0f);
            float width = Mathf.Min(screen.x, screen.y / tall);
            float pictureHeight = width / aspect;
            float total = width * tall;
            _demoPicture.sizeDelta = new Vector2(width, pictureHeight);
            _demoPicture.anchoredPosition = new Vector2(0, -total * 0.5f + pictureHeight * 0.5f);
            if (barAspect > 0)
            {
                var bar = _demoBar.rectTransform;
                bar.sizeDelta = new Vector2(width * barWidth, width * barWidth / barAspect);
                bar.anchoredPosition = new Vector2(0, total * 0.5f - bar.sizeDelta.y * 0.5f);
            }
            // Each layer fills the picture at its own shape (the far
            // background's wider: cropped by the picture's edges).
            foreach (var image in _demoLayers)
            {
                if (!image.texture) continue;
                float layerAspect = image.texture.width / (float)image.texture.height;
                image.rectTransform.sizeDelta = layerAspect > aspect
                    ? new Vector2(pictureHeight * layerAspect, pictureHeight)
                    : new Vector2(width, width / layerAspect);
            }
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
