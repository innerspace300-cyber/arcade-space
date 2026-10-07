// CoreThread.cs — runs the emulator on a thread of its own instead of in
// Unity's Update (every emulated game; first made for the ones too heavy to
// share the main thread, CV1000). On the main thread a slow game frame holds up
// everything else - drawing, the touch controls and their animations, the
// sound feed - so when the game falls behind, the whole app stutters with
// it. Here the game runs at its own rate (behind, it runs flat out; more than
// a few frames behind, it lets the backlog go, as RunCoreFrames does) and the
// app keeps drawing the latest frame it has.
//
// Each frame's depth layers are copied off the core straight after it runs
// into a Frame (the core's buffers are rewritten by the next frame), handed
// to the main thread through three of them: the thread fills one, the main
// thread draws from another, the third holds the newest finished one -
// neither side ever waits for the other.
//
// Everything else that calls into the core from the main thread (save
// states, reset, memory reads: LibretroCore) takes Lock, so it happens
// between two of the thread's frames. Start after the game's loaded, Stop
// before it's unloaded.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using SpatialEmulator.Games;

namespace SpatialEmulator.Mobile
{
    public static class CoreThread
    {
        /// Held while the core runs a frame; take it to call into the core.
        public static readonly object Lock = new object();

        public const int MaxLayers = 8;

        public sealed class Frame
        {
            public uint id;
            public int count;
            public readonly MobileRdLayer[] layers = new MobileRdLayer[MaxLayers];
            public readonly byte[][] pixels = new byte[MaxLayers][];
        }

        static Frame s_back = new Frame(), s_ready = new Frame(), s_front = new Frame();
        static bool s_fresh;
        static readonly object s_swap = new object();

        static Thread s_thread;
        static volatile bool s_run;
        static int s_frames;

        /// Holds the game (the game picker or Settings is open).
        public static volatile bool Paused;

        public static bool Running => s_thread != null;

        /// Game frames run since Start.
        public static int Frames => Volatile.Read(ref s_frames);

        /// Whether a game runs on the thread: every emulated game (the
        /// built-in demo isn't the core's).
        public static bool WantedFor(string game) => !string.IsNullOrEmpty(game);

        public static void Start()
        {
            if (s_thread != null) return;
            s_frames = 0;
            s_fresh = false;
            s_run = true;
            s_thread = new Thread(Loop) { Name = "ARcade emulator", IsBackground = true, Priority = ThreadPriority.AboveNormal };
            s_thread.Start();
        }

        public static void Stop()
        {
            if (s_thread == null) return;
            s_run = false;
            s_thread.Join();
            s_thread = null;
        }

        /// The newest frame the thread has finished since the last call, or
        /// null if there's none new. It stays the main thread's until the next
        /// call.
        public static Frame TakeFrame()
        {
            lock (s_swap)
            {
                if (!s_fresh) return null;
                (s_front, s_ready) = (s_ready, s_front);
                s_fresh = false;
                return s_front;
            }
        }

        static void Loop()
        {
            var clock = Stopwatch.StartNew();
            double next = 0;
            while (s_run)
            {
                double now = clock.Elapsed.TotalSeconds;
                if (Paused || !LibretroCore.IsRunning)
                {
                    Thread.Sleep(4);
                    next = clock.Elapsed.TotalSeconds;
                    continue;
                }
                double frameTime = LibretroCore.Fps > 0 ? 1.0 / LibretroCore.Fps : 1.0 / 60.0;
                // The sound well ahead (a catch-up burst): hold the game until
                // it plays down, or it would play ever later.
                if (MobileRetroAudio.Buffered > 2 * MobileRetroAudio.TargetFill)
                {
                    Thread.Sleep(2);
                    next = clock.Elapsed.TotalSeconds;
                    continue;
                }
                if (now < next)
                {
                    double wait = next - now;
                    if (wait > 0.002) Thread.Sleep((int)((wait - 0.001) * 1000));
                    else Thread.Yield();
                    continue;
                }

                long started = UI.PerfReadout.Now;
                lock (Lock)
                {
                    LibretroCore.RunFrame();
                    Copy(s_back);
                }
                UI.PerfReadout.Core(started, 1);
                Interlocked.Increment(ref s_frames);
                lock (s_swap)
                {
                    (s_back, s_ready) = (s_ready, s_back);
                    s_fresh = true;
                }

                next += frameTime;
                if (now - next > 4 * frameTime) next = now;   // too far behind: let it go
            }
        }

        static void Copy(Frame frame)
        {
            frame.id = MobileRetroDepth.FrameId;
            int count = System.Math.Min(MobileRetroDepth.LayerCount, MaxLayers);
            frame.count = count;
            for (int i = 0; i < count; i++)
            {
                if (!MobileRetroDepth.TryGetLayer(i, out var layer)) { frame.count = i; return; }
                frame.layers[i] = layer;
                int bytes = layer.Width * layer.Height * 4;
                if (frame.pixels[i] == null || frame.pixels[i].Length != bytes) frame.pixels[i] = new byte[bytes];
                var pin = GCHandle.Alloc(frame.pixels[i], GCHandleType.Pinned);
                try { MobileRetroDepth.TryGetLayerPixels(i, pin.AddrOfPinnedObject(), bytes); }
                finally { pin.Free(); }
            }
        }
    }
}
