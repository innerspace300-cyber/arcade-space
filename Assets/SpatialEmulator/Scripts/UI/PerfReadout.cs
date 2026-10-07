// PerfReadout.cs — Settings > SOUND > PERF STATS: a small readout in the
// top left corner, for finding what's warming the phone up. Twice a second:
//   FPS     frames drawn a second, and the slowest frame's time
//   CPU/GPU each frame's work on the processor (main thread + render
//           thread) and on the graphics chip (Unity's FrameTimingManager;
//           Player Settings' frame timing stats)
//   EMU     the emulator's time for each game frame, and for each drawn
//           frame (two game frames a drawn one at 30), and game frames a second
//   LAYERS  the game's layers sent to the graphics chip, time a frame
//   PLANES  ARKit's plane search on or off (CabinetPlaneFader)
//   HEAT    iOS's thermal state (ThermalGovernor): NOMINAL, FAIR,
//           SERIOUS (iOS slows the phone down), CRITICAL - and RES, the
//           render resolution ThermalGovernor has eased off to
// MobileRetroDepthLayerStack reports the emulator's and the layers' times.
// Made at run time; off unless switched on (kept between launches).

using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator.UI
{
    public class PerfReadout : MonoBehaviour
    {
        const string Key = "ARcade.PerfStats";
        static PerfReadout s_instance;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(Key, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                PlayerPrefs.Save();
                Ensure();
            }
        }

        // ---- the emulator's and the layers' times (MobileRetroDepthLayerStack) ----

        static long s_coreTicks, s_uploadTicks;
        static int s_coreFrames, s_uploads, s_layers;

        public static long Now => Stopwatch.GetTimestamp();

        public static void Core(long since, int frames)
        {
            s_coreTicks += Stopwatch.GetTimestamp() - since;
            s_coreFrames += frames;
        }

        public static void Upload(long since, int layers)
        {
            s_uploadTicks += Stopwatch.GetTimestamp() - since;
            s_uploads++;
            s_layers = layers;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            if (Enabled && !s_instance)
            {
                var go = new GameObject("Perf Readout");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<PerfReadout>();
            }
            if (s_instance) s_instance.gameObject.SetActive(Enabled);
        }

        TMP_Text _text;
        float _since;
        int _frames;
        float _worst;
        readonly FrameTiming[] _timing = new FrameTiming[1];
        double _cpu, _render, _gpu;
        int _timed;

        void Start()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.matchWidthOrHeight = 0f;

            var area = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(transform, false);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
            area.offsetMin = area.offsetMax = Vector2.zero;
            area.gameObject.AddComponent<SafeAreaFitter>();

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            panel.SetParent(area, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(6f, -6f);
            var back = panel.GetComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0.6f);
            back.raycastTarget = false;
            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(5, 5, 4, 4);

            _text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            _text.transform.SetParent(panel, false);
            _text.fontSize = 9f;
            _text.raycastTarget = false;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            _text.text = "...";
            var picker = FindAnyObjectByType<GamePicker>(FindObjectsInactive.Include);
            if (picker && picker.toastLabel) { _text.font = picker.toastLabel.font; _text.fontSize = picker.toastLabel.fontSize; }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _since += dt;
            _frames++;
            _worst = Mathf.Max(_worst, dt);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
            {
                _cpu += _timing[0].cpuMainThreadFrameTime;
                _render += _timing[0].cpuRenderThreadFrameTime;
                _gpu += _timing[0].gpuFrameTime;
                _timed++;
            }
            if (_since < 0.5f) return;

            double tick = 1000.0 / Stopwatch.Frequency;
            float fps = _frames / _since;
            string emu = s_coreFrames > 0
                ? $"EMU {s_coreTicks * tick / s_coreFrames:0.0} ms ({s_coreTicks * tick / _frames:0.0}/draw)  {s_coreFrames / _since:0} fps"
                : "EMU -";
            string layers = s_uploads > 0 ? $"LAYERS {s_layers}  {s_uploadTicks * tick / _frames:0.0} ms" : "LAYERS -";
            string timing = _timed > 0 ? $"CPU {_cpu / _timed:0.0}+{_render / _timed:0.0}  GPU {_gpu / _timed:0.0} ms" : "CPU/GPU -";
            _text.text =
                $"FPS {fps:0}  worst {_worst * 1000f:0} ms\n" +
                timing + "\n" + emu + "\n" + layers + "\n" +
                $"PLANES {(CabinetPlaneFader.Detecting ? "<color=#ffd24a>ON</color>" : "OFF")}  CAM {ThermalGovernor.CameraMode}\n" +
                "HEAT " + Heat(ThermalGovernor.Heat) + $"  RES {ThermalGovernor.RenderScale * 100f:0}%";

            _since = 0f; _frames = 0; _worst = 0f;
            _cpu = _render = _gpu = 0; _timed = 0;
            s_coreTicks = s_uploadTicks = 0; s_coreFrames = s_uploads = 0;
        }

        static string Heat(int state) => state switch
        {
            0 => "<color=#7dff8a>NOMINAL</color>",
            1 => "<color=#ffd24a>FAIR</color>",
            2 => "<color=#ff8a3d>SERIOUS</color>",
            3 => "<color=#ff4d4d>CRITICAL</color>",
            _ => "-",
        };
    }
}
