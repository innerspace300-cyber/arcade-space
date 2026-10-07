// LibretroCore.cs — minimal C# libretro frontend, replacing RetroArch.
//
// On mobile the patched MAME core (spatial-emulator-core/libretro-mame,
// branch spatial-emulator) is linked directly into the app as a static
// library (see build notes at the bottom of this file), so this class
// drives it exactly the way a libretro frontend normally would: register
// the four mandatory callbacks, call retro_init/retro_load_game once, then
// call retro_run() every frame from Unity's own update loop.
//
// The video/audio/input callbacks below are close to no-ops: the actual
// frame data comes from the same RetroDepth layer-export API used on
// desktop (see MobileRetroDepth.cs), not from retro_video_refresh - MAME
// hands that one composited frame, which is exactly what we don't want.
// They still have to be registered and non-null, or the core crashes the
// first time it tries to call them (confirmed against the core's actual
// call sites, not assumed).
//
// BUILD NOTE: libretro cores build as a dylib (spatial_libretro_ios.dylib,
// see spatial-emulator-core/libretro-mame's Makefile.libretro) - that's the
// standard libretro core format, and this build system's iOS StaticLib path
// has an unresolved genie/premake output-naming bug (kind=StaticLib still
// forces targetextension ".dylib", producing a broken ar invocation), not
// worth fighting further. Unity's iOS plugin importer doesn't auto-link a
// bare dylib, but it DOES auto-embed a proper .framework bundle, so the
// dylib gets wrapped as SpatialEmulatorCore.framework (see
// tools/wrap_ios_framework.sh) before dropping it into
// Assets/Plugins/iOS/. Unity links that framework into UnityFramework, so
// on iOS DllImport uses "__Internal": IL2CPP emits direct calls that the
// Xcode linker resolves against the framework. Naming the framework binary
// instead makes IL2CPP dlopen() "<App>.app/SpatialEmulatorCore", which
// doesn't exist - the framework is embedded under Frameworks/ (confirmed
// on-device 2026-09-24: DllNotFoundException, "no such file").

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using SpatialEmulator.Controls;
using UnityEngine;

namespace SpatialEmulator.Mobile
{
    public static class LibretroCore
    {
#if UNITY_IOS && !UNITY_EDITOR
        const string Lib = "__Internal"; // resolved at link time against SpatialEmulatorCore.framework, see build note above
#else
        const string Lib = "spatial_libretro"; // editor/standalone testing against a loose dylib, if ever needed
#endif

        // ---- libretro.h structs (only the fields we actually read/write) ----

        [StructLayout(LayoutKind.Sequential)]
        struct RetroGameInfo
        {
            public IntPtr path; // UTF8 C string
            public IntPtr data;
            public UIntPtr size;
            public IntPtr meta;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RetroGameGeometry
        {
            public uint base_width, base_height, max_width, max_height;
            public float aspect_ratio;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RetroSystemTiming { public double fps, sample_rate; }

        [StructLayout(LayoutKind.Sequential)]
        struct RetroSystemAvInfo { public RetroGameGeometry geometry; public RetroSystemTiming timing; }

        // ---- environment command ids we actually need to distinguish ----
        const uint RETRO_ENVIRONMENT_GET_SYSTEM_DIRECTORY = 9;
        const uint RETRO_ENVIRONMENT_SET_PIXEL_FORMAT = 10;
        const uint RETRO_ENVIRONMENT_GET_VARIABLE = 15;
        const uint RETRO_DEVICE_JOYPAD = 1;
        const uint RETRO_DEVICE_MASK = 0xff; // low byte = base device type, above it a subclass
        const uint RETRO_ENVIRONMENT_GET_CONTENT_DIRECTORY = 30;
        const uint RETRO_ENVIRONMENT_GET_SAVE_DIRECTORY = 31;
        const int RETRO_PIXEL_FORMAT_XRGB8888 = 1;

        // Kept alive for the process lifetime: the core copies these into
        // its own std::string members on the *_DIRECTORY calls, but the
        // pointer must stay valid for at least that copy, and there's no
        // benefit to freeing/reallocating per call.
        static IntPtr s_systemDirPtr;

        // Core option values returned for GET_VARIABLE: the defaults declared
        // in libretro-mame's libretro_core_options.h, i.e. what RetroArch hands
        // the core when no option has been changed - the setup the core was
        // verified with. Refusing GET_VARIABLE instead leaves the C globals at
        // their zero values, notably a blank mame_media_type, which makes MAME
        // boot the romset's parent folder name as a system ("Unknown system
        // 'roms'" - confirmed on-device 2026-09-24).
        static readonly Dictionary<string, string> s_coreOptions = new Dictionary<string, string>
        {
            { "mame_thread_mode", "enabled" },
            { "mame_cheats_enable", "disabled" },
            { "mame_throttle", "disabled" },
            { "mame_boot_to_bios", "disabled" },
            { "mame_boot_to_osd", "disabled" },
            { "mame_read_config", "disabled" },
            { "mame_write_config", "disabled" },
            { "mame_mame_paths_enable", "disabled" },
            { "mame_saves", "game" },
            { "mame_auto_save", "disabled" },
            { "mame_softlists_enable", "enabled" },
            { "mame_softlists_auto_media", "enabled" },
            { "mame_media_type", "rom" },
            { "mame_joystick_deadzone", "0.15" },
            { "mame_joystick_saturation", "0.85" },
            { "mame_joystick_threshold", "0.30" },
            { "mame_mame_4way_enable", "disabled" },
            { "mame_buttons_profiles", "disabled" },
            { "mame_mouse_enable", "enabled" },
            { "mame_lightgun_mode", "none" },
            { "mame_lightgun_offscreen_mode", "free" },
            { "mame_rotation_mode", "libretro" },
            { "mame_alternate_renderer", "disabled" },
            { "mame_altres", "640x480" },
            { "mame_cpu_overclock", "default" },
            { "mame_cpu_sound_overclock", "default" },
            { "mame_autoloadfastforward", "disabled" },
            { "mame_coin_limit", "0" },
        };

        // Same lifetime rule as s_systemDirPtr: the core may keep the value
        // pointer, so each option's C string is allocated once and never freed.
        static readonly Dictionary<string, IntPtr> s_coreOptionPtrs = new Dictionary<string, IntPtr>();

        // ---- delegate types matching libretro.h's callback signatures ----
        delegate bool EnvironmentCb(uint cmd, IntPtr data);
        delegate void VideoRefreshCb(IntPtr data, uint width, uint height, UIntPtr pitch);
        delegate void AudioSampleCb(short left, short right);
        delegate UIntPtr AudioSampleBatchCb(IntPtr data, UIntPtr frames);
        delegate void InputPollCb();
        delegate short InputStateCb(uint port, uint device, uint index, uint id);

        [DllImport(Lib)] static extern void retro_set_environment(EnvironmentCb cb);
        [DllImport(Lib)] static extern void retro_set_video_refresh(VideoRefreshCb cb);
        [DllImport(Lib)] static extern void retro_set_audio_sample(AudioSampleCb cb);
        [DllImport(Lib)] static extern void retro_set_audio_sample_batch(AudioSampleBatchCb cb);
        [DllImport(Lib)] static extern void retro_set_input_poll(InputPollCb cb);
        [DllImport(Lib)] static extern void retro_set_input_state(InputStateCb cb);
        [DllImport(Lib)] static extern void retro_init();
        [DllImport(Lib)] static extern void retro_deinit();
        [DllImport(Lib)] static extern void retro_get_system_av_info(out RetroSystemAvInfo info);
        [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] static extern bool retro_load_game(ref RetroGameInfo game);
        [DllImport(Lib)] static extern void retro_unload_game();
        [DllImport(Lib)] static extern void retro_run();
        [DllImport(Lib)] static extern void retro_reset();
        [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] static extern bool spatial_mem_read(string tag, uint address, byte[] data, uint length);
        [DllImport(Lib)] static extern UIntPtr retro_serialize_size();
        [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] static extern bool retro_serialize(IntPtr data, UIntPtr size);
        [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] static extern bool retro_unserialize(IntPtr data, UIntPtr size);

        public static bool IsRunning { get; private set; }
        public static double Fps { get; private set; }
        public static double SampleRate { get; private set; }
        /// Display aspect ratio (width / height) of the game's screen as the
        /// core reports it, e.g. 4:3 for the arcade monitors; 0 if unknown.
        public static double AspectRatio { get; private set; }

        // Kept alive for the process lifetime - libretro cores hold onto the
        // function pointers past the retro_set_*() call, so the delegates
        // (and the GC handles pinning them) must not be collected.
        static EnvironmentCb s_environmentCb;
        static VideoRefreshCb s_videoRefreshCb;
        static AudioSampleCb s_audioSampleCb;
        static AudioSampleBatchCb s_audioSampleBatchCb;
        static InputPollCb s_inputPollCb;
        static InputStateCb s_inputStateCb;

        static bool s_initialized;

        /// One-time setup: registers callbacks and calls retro_init(). Safe
        /// to call once per process; call StartGame() per ROM after this.
        public static void Initialize()
        {
            if (s_initialized) return;
            Debug.Log("[RD-DIAG] LibretroCore.Initialize() start");

            // MAME writes its cfg/nvram/state files under whatever this
            // resolves to; Application.persistentDataPath is the one
            // location guaranteed both writable and to survive app
            // relaunches on iOS (the app bundle itself is read-only).
            s_systemDirPtr = Marshal.StringToHGlobalAnsi(Application.persistentDataPath);
            Debug.Log("[RD-DIAG] persistentDataPath=" + Application.persistentDataPath);

            s_environmentCb = EnvironmentCallback;
            s_videoRefreshCb = VideoRefreshCallback;
            s_audioSampleCb = AudioSampleCallback;
            s_audioSampleBatchCb = AudioSampleBatchCallback;
            s_inputPollCb = InputPollCallback;
            s_inputStateCb = InputStateCallback;

            retro_set_environment(s_environmentCb);
            retro_set_video_refresh(s_videoRefreshCb);
            retro_set_audio_sample(s_audioSampleCb);
            retro_set_audio_sample_batch(s_audioSampleBatchCb);
            retro_set_input_poll(s_inputPollCb);
            retro_set_input_state(s_inputStateCb);
            Debug.Log("[RD-DIAG] all retro_set_* callbacks registered, calling retro_init()");

            retro_init();
            Debug.Log("[RD-DIAG] retro_init() returned");
            s_initialized = true;
        }

        /// Loads a romset by full path (need_fullpath=true - MAME reads the
        /// zip itself, same "point at the file" convention as -rompath on
        /// desktop; parent directory should hold any shared BIOS zip too,
        /// e.g. neogeo.zip next to a Neo Geo romset).
        public static bool StartGame(string romPath)
        {
            Debug.Log("[RD-DIAG] StartGame(" + romPath + ") start, fileExists=" + System.IO.File.Exists(romPath));
            if (!s_initialized) Initialize();
            if (IsRunning) StopGame();

            IntPtr pathPtr = Marshal.StringToHGlobalAnsi(romPath);
            try
            {
                var info = new RetroGameInfo { path = pathPtr, data = IntPtr.Zero, size = UIntPtr.Zero, meta = IntPtr.Zero };
                FlatPixels = null;   // (no last game's picture)
                FlatFrame = 0;
                Debug.Log("[RD-DIAG] calling retro_load_game()");
                bool ok = retro_load_game(ref info);
                Debug.Log("[RD-DIAG] retro_load_game() returned " + ok);
                if (ok)
                {
                    retro_get_system_av_info(out var av);
                    Fps = av.timing.fps;
                    SampleRate = av.timing.sample_rate;
                    AspectRatio = av.geometry.aspect_ratio;
                    IsRunning = true;
                    Debug.Log("[RD-DIAG] fps=" + Fps + " sampleRate=" + SampleRate + " aspect=" + AspectRatio);
                }
                else
                {
                    Debug.LogError($"[LibretroCore] retro_load_game failed for: {romPath}");
                }
                return ok;
            }
            finally
            {
                Marshal.FreeHGlobal(pathPtr);
            }
        }

        /// Restarts the running game (MAME's soft reset: the board's reset
        /// switch), from the next frame.
        public static void Reset()
        {
            lock (CoreThread.Lock) if (IsRunning) retro_reset();
        }

        static readonly byte[] s_byte = new byte[1];

        /// Reads one byte of a CPU's memory (e.g. "maincpu"), or -1 if no
        /// game is running.
        public static int ReadByte(string cpu, uint address)
        {
            lock (CoreThread.Lock)
            {
                if (!IsRunning || !spatial_mem_read(cpu, address, s_byte, 1)) return -1;
                return s_byte[0];
            }
        }

        public static void StopGame()
        {
            CoreThread.Stop();   // (not mid-frame)
            if (!IsRunning) return;
            retro_unload_game();
            IsRunning = false;
        }

        /// Call once per Unity Update() while a game is running. Runs one
        /// emulated frame; RetroDepth layer data is readable immediately
        /// after this returns (see MobileRetroDepth.cs).
        public static void RunFrame()
        {
            if (!IsRunning) return;
            retro_run();
        }

        /// The running game's full state (MAME save state), or null if it
        /// can't be saved right now. Call between frames (not inside RunFrame).
        /// Round trips replay frame-exactly, including the depth layers
        /// (spatial-emulator-core/tools/savestate_test.c).
        public static byte[] SaveState()
        {
            lock (CoreThread.Lock) return Serialize();
        }

        static byte[] Serialize()
        {
            if (!IsRunning) return null;
            int size = (int)retro_serialize_size();
            if (size <= 0) return null;
            var data = new byte[size];
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                return retro_serialize(handle.AddrOfPinnedObject(), (UIntPtr)size) ? data : null;
            }
            finally
            {
                handle.Free();
            }
        }

        /// Restores a state from SaveState() for the same game.
        public static bool LoadState(byte[] data)
        {
            if (data == null || data.Length == 0) return false;
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                lock (CoreThread.Lock) return IsRunning && retro_unserialize(handle.AddrOfPinnedObject(), (UIntPtr)data.Length);
            }
            finally
            {
                handle.Free();
            }
        }

        public static void Shutdown()
        {
            CoreThread.Stop();
            if (!s_initialized) return;
            if (IsRunning) StopGame();
            retro_deinit();
            s_initialized = false;
            if (s_systemDirPtr != IntPtr.Zero) { Marshal.FreeHGlobal(s_systemDirPtr); s_systemDirPtr = IntPtr.Zero; }
        }

        // ---- callback implementations ----
        // AOT-safe (MonoPInvokeCallback) so these work under IL2CPP/AOT on
        // device, not just in the Mono-JIT editor.

        [MonoPInvokeCallback(typeof(EnvironmentCb))]
        static bool EnvironmentCallback(uint cmd, IntPtr data)
        {
            // The one command the core treats as fatal if refused: it calls
            // exit(0) on false (confirmed in libretro.cpp's retro_init).
            // Every other command in the core's actual call sites is used
            // via `if (environ_cb(...))`, i.e. tolerant of false/no-op.
            if (cmd == RETRO_ENVIRONMENT_SET_PIXEL_FORMAT)
            {
                if (data == IntPtr.Zero) return false;
                int fmt = Marshal.ReadInt32(data);
                return fmt == RETRO_PIXEL_FORMAT_XRGB8888;
            }
            if (cmd == RETRO_ENVIRONMENT_GET_SYSTEM_DIRECTORY
                || cmd == RETRO_ENVIRONMENT_GET_SAVE_DIRECTORY
                || cmd == RETRO_ENVIRONMENT_GET_CONTENT_DIRECTORY)
            {
                if (data == IntPtr.Zero || s_systemDirPtr == IntPtr.Zero) return false;
                Marshal.WriteIntPtr(data, s_systemDirPtr); // data is `const char **`
                return true;
            }
            if (cmd == RETRO_ENVIRONMENT_GET_VARIABLE)
            {
                if (data == IntPtr.Zero) return false;
                // data is `struct retro_variable { const char *key; const char *value; } *`
                string key = Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(data));
                if (key == null || !s_coreOptions.TryGetValue(key, out var value)) return false;
                if (!s_coreOptionPtrs.TryGetValue(key, out var valuePtr))
                {
                    valuePtr = Marshal.StringToHGlobalAnsi(value);
                    s_coreOptionPtrs[key] = valuePtr;
                }
                Marshal.WriteIntPtr(data, IntPtr.Size, valuePtr);
                return true;
            }
            return false;
        }

        /// The composited frame (BGRA, rows top-down, opaque, upright), kept
        /// for a game whose driver doesn't export depth layers yet - it plays
        /// flat, as one layer (MobileRetroDepthLayerStack) - and for every
        /// game while WholeFrame is set (FULL SCREEN). FlatFrame counts them.
        public static byte[] FlatPixels { get; private set; }
        public static int FlatWidth { get; private set; }
        public static int FlatHeight { get; private set; }
        public static int FlatFrame { get; private set; }
        static readonly byte[][] s_flatBuffers = new byte[3][];
        static int s_flatTurn;

        /// Keep the composited frame for every game, layers or not.
        public static bool WholeFrame;

        [MonoPInvokeCallback(typeof(VideoRefreshCb))]
        static void VideoRefreshCallback(IntPtr data, uint width, uint height, UIntPtr pitch)
        {
            // Layer data comes from MobileRetroDepth, not this composited
            // frame - except for a driver with no layers (still registered
            // regardless, see the header comment: the core's video_cb(...)
            // call sites are unconditional). XRGB8888: B, G, R, X in memory.
            if (data == IntPtr.Zero || width == 0 || height == 0 || (MobileRetroDepth.LayerCount > 0 && !WholeFrame)) return;
            int w = (int)width, h = (int)height, row = w * 4, stride = (int)(ulong)pitch;
            // Filled in turn, three of them, and FlatPixels pointed at the
            // newest once it's whole: on CoreThread this runs on the core's
            // thread while the main thread may still be reading the last one.
            s_flatTurn = (s_flatTurn + 1) % s_flatBuffers.Length;
            var pixels = s_flatBuffers[s_flatTurn];
            if (pixels == null || pixels.Length != row * h) pixels = s_flatBuffers[s_flatTurn] = new byte[row * h];
            for (int y = 0; y < h; y++)
                Marshal.Copy(data + y * stride, pixels, y * row, row);
            for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            FlatWidth = w;
            FlatHeight = h;
            FlatPixels = pixels;
            FlatFrame++;
        }

        [MonoPInvokeCallback(typeof(AudioSampleCb))]
        static void AudioSampleCallback(short left, short right) { }

        [MonoPInvokeCallback(typeof(AudioSampleBatchCb))]
        static UIntPtr AudioSampleBatchCallback(IntPtr data, UIntPtr frames)
        {
            // Called once at the end of each retro_run(), on the calling
            // (main) thread, with that frame's interleaved stereo int16
            // samples. The core only ever uses this batch callback.
            MobileRetroAudio.Write(data, (int)frames);
            return frames;
        }

        // Called once per emulated frame, before the core reads input.
        [MonoPInvokeCallback(typeof(InputPollCb))]
        static void InputPollCallback() => ArcadeInput.OnPoll();

        // Player 1's RetroPad comes from the on-screen controls (ArcadeInput);
        // every other port/device reports released.
        [MonoPInvokeCallback(typeof(InputStateCb))]
        static short InputStateCallback(uint port, uint device, uint index, uint id)
        {
            if (port != (uint)ArcadeInput.PlayerPort || (device & RETRO_DEVICE_MASK) != RETRO_DEVICE_JOYPAD) return 0;
            return ArcadeInput.IsPressed(id) ? (short)1 : (short)0;
        }
    }
}
