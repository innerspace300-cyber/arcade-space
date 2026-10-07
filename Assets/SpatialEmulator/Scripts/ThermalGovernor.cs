// ThermalGovernor.cs — keeps the phone from overheating by easing off the
// drawing (never the emulator: the game keeps its full speed) as iOS reports
// it warming up (NSProcessInfo's thermal state, Plugins/iOS/SEDevice.mm):
//
//   NOMINAL   full resolution, the camera's frame rate (ARSession matches it)
//   FAIR      85% resolution, 30 frames a second, the camera at 30 too
//   SERIOUS   70% resolution, the same (iOS slows the phone here)
//   CRITICAL  60% resolution, the same
//
// The game always runs at its own speed (MobileRetroDepthLayerStack keeps the
// emulator's clock: at 30 drawn frames a second it runs two game frames a
// drawn one). The camera's slower mode (ARKit's, at the same picture size if
// there's one, else the nearest smaller) halves its capture and tracking
// work; it's switched only as the step changes - each switch is a moment's
// hitch in the camera.
//
// Resolution is URP's render scale (the picture drawn smaller, then scaled
// up to the screen). It steps down as soon as the phone's hotter, and back
// up only once it's stayed cooler for a while, so it doesn't see-saw.
// Nothing in the Editor (it would change the URP asset on disk). Made at
// run time.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Collections;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace SpatialEmulator
{
    public class ThermalGovernor : MonoBehaviour
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int SE_ThermalState();
        /// iOS's thermal state: 0 nominal, 1 fair, 2 serious, 3 critical.
        public static int Heat => SE_ThermalState();
#else
        public static int Heat => -1;
#endif

        static readonly float[] Scale = { 1f, 0.85f, 0.7f, 0.6f };
        static readonly int[] Fps = { 0, 30, 30, 30 };   // (0: the camera's own)
        const int CoolCameraFps = 30;
        const float CoolSeconds = 30f;   // cooler this long before stepping back up
        const float CheckSeconds = 1f;

        /// The step it's on (0-3, as the thermal states).
        public static int Level { get; private set; }

        /// The render scale in use.
        public static float RenderScale => Scale[Level];

        /// The camera's mode, e.g. "1920x1440 60" (blank until known).
        public static string CameraMode { get; private set; } = "";

        float _nextCheck, _coolerSince = -1f;
        float _homeScale = 1f;
        ARSession _session;
        ARCameraManager _camera;
        XRCameraConfiguration? _homeCamera;

#if UNITY_IOS && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("Thermal Governor");
            DontDestroyOnLoad(go);
            go.AddComponent<ThermalGovernor>();
        }
#endif

        void Start()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp) _homeScale = urp.renderScale;
            Apply(Mathf.Max(0, Heat));
        }

        void Update()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + CheckSeconds;
            Camera();   // (the camera's modes are known a little after start)
            int heat = Heat;
            if (heat < 0) return;
            if (heat > Level)
            {
                _coolerSince = -1f;
                Apply(heat);
            }
            else if (heat < Level)
            {
                if (_coolerSince < 0f) _coolerSince = Time.unscaledTime;
                else if (Time.unscaledTime - _coolerSince >= CoolSeconds)
                {
                    _coolerSince = -1f;
                    Apply(Level - 1);   // (a step at a time)
                }
            }
            else _coolerSince = -1f;
        }

        void Apply(int level)
        {
            Level = Mathf.Clamp(level, 0, Scale.Length - 1);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.renderScale = _homeScale * Scale[Level];
            if (!_session) _session = FindAnyObjectByType<ARSession>();
            int fps = Fps[Level];
            if (_session) _session.matchFrameRateRequested = fps == 0;
            if (fps > 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = fps;
            }
            Camera();
        }

        // The camera's mode for the step: its own at NOMINAL, a 30 a second
        // one above.
        void Camera()
        {
            if (!_camera) _camera = FindAnyObjectByType<ARCameraManager>();
            if (!_camera) return;
            var current = _camera.currentConfiguration;
            if (current == null) return;
            if (_homeCamera == null) _homeCamera = current;
            var want = Level == 0 ? _homeCamera : Cool(_homeCamera.Value);
            if (want != null && !Same(want.Value, current.Value))
            {
                try { _camera.currentConfiguration = want; current = want; }
                catch (System.Exception e) { Debug.LogWarning($"[ThermalGovernor] camera mode: {e.Message}"); }
            }
            var c = current.Value;
            CameraMode = $"{c.width}x{c.height} {(c.framerate.HasValue ? c.framerate.Value.ToString() : "?")}";
        }

        // A mode at no more than CoolCameraFps: the home size if it has one,
        // else the biggest smaller one; none if there isn't.
        XRCameraConfiguration? Cool(XRCameraConfiguration home)
        {
            XRCameraConfiguration? best = null;
            using (var modes = _camera.GetConfigurations(Allocator.Temp))
                foreach (var mode in modes)
                {
                    if (!mode.framerate.HasValue || mode.framerate.Value > CoolCameraFps) continue;
                    if (mode.width > home.width || mode.height > home.height) continue;
                    if (best == null || mode.width * mode.height > best.Value.width * best.Value.height) best = mode;
                }
            return best ?? home;
        }

        static bool Same(XRCameraConfiguration a, XRCameraConfiguration b) =>
            a.resolution == b.resolution && a.framerate == b.framerate;
    }
}
