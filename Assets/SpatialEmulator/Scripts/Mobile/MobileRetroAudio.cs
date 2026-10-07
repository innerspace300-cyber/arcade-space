// MobileRetroAudio.cs — carries the core's audio from retro_run() (Unity's
// main thread, via LibretroCore's audio batch callback) to Unity's audio
// thread (MobileRetroAudioOutput.OnAudioFilterRead).
//
// Single producer / single consumer and lock-free: only Write() advances
// s_write and only Read() advances s_read, so the audio thread never blocks
// on the main thread.
//
// Read() resamples from the core's rate (48 kHz for libretro-mame) to Unity's
// output rate, and applies dynamic rate control: it speeds up or slows down
// consumption by at most MaxRateAdjust to hold the buffer near its target
// fill. That absorbs the small, steady mismatch between how fast the core
// produces audio (it runs in step with the display, e.g. 60 Hz for a 59.64 Hz
// game) and how fast the device plays it, instead of the buffer slowly
// draining (dropouts) or filling up (drops and growing latency).

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SpatialEmulator.Mobile
{
    public static class MobileRetroAudio
    {
        const int Capacity = 16384;             // stereo frames (~340 ms at 48 kHz); must be a power of two
        const int Mask = Capacity - 1;
        const double TargetLatencySeconds = 0.06;
        const double RateControlStrength = 0.02;
        const double MaxRateAdjust = 0.015;
        // Under target the game's running slow (a heavy scene): rather than
        // run dry and break up, the sound's stretched without changing its
        // pitch - every Grain output frames, playback steps back over part of
        // what it's just played (at most half a grain), crossfading over Fade
        // frames from where it was to where it stepped back to.
        const int Grain = 1024;
        const int Fade = 256;
        static int s_grainLeft = Grain;
        static int s_fadeLeft;
        static int s_fadeRead;      // the head being faded out
        static double s_fadePhase;

        static readonly float[] s_left = new float[Capacity];
        static readonly float[] s_right = new float[Capacity];
        static short[] s_scratch = new short[4096];

        // Frame counters that only ever increase; wrapped with Mask on access.
        // Their difference stays correct across int overflow.
        static int s_write;
        static int s_read;

        static double s_ratio = 1.0;   // core frames consumed per output frame
        static double s_phase;         // fractional position between s_read and s_read + 1
        static int s_targetFill = 2880;
        static bool s_primed;

        /// Stereo frames waiting to play, and how many it aims to hold
        /// (CoreThread holds the game back while it has well over that).
        public static int Buffered => Volatile.Read(ref s_write) - Volatile.Read(ref s_read);
        public static int TargetFill => s_targetFill;

        /// Main thread, before output starts (MobileRetroAudioOutput not yet
        /// playing): sets the rates and empties the buffer.
        public static void Configure(double coreSampleRate, int outputSampleRate)
        {
            s_ratio = coreSampleRate / outputSampleRate;
            s_targetFill = (int)(coreSampleRate * TargetLatencySeconds);
            s_phase = 0;
            s_primed = false;
            Volatile.Write(ref s_read, Volatile.Read(ref s_write));
        }

        /// Main thread: appends `frames` interleaved stereo int16 frames.
        /// Anything that doesn't fit is dropped.
        public static void Write(IntPtr interleaved, int frames)
        {
            int count = frames * 2;
            if (s_scratch.Length < count) s_scratch = new short[count];
            Marshal.Copy(interleaved, s_scratch, 0, count);

            int w = s_write;
            int free = Capacity - (w - Volatile.Read(ref s_read));
            int n = Math.Min(frames, free);
            for (int i = 0; i < n; i++)
            {
                int idx = (w + i) & Mask;
                s_left[idx] = s_scratch[2 * i] / 32768f;
                s_right[idx] = s_scratch[2 * i + 1] / 32768f;
            }
            Volatile.Write(ref s_write, w + n);
        }

        /// Audio thread: fills Unity's interleaved float buffer. Outputs
        /// silence until TargetLatencySeconds of audio is buffered, and again
        /// after an underflow, so playback restarts cleanly.
        public static void Read(float[] data, int channels)
        {
            int frames = data.Length / channels;
            int r = s_read;
            int fill = Volatile.Read(ref s_write) - r;

            if (!s_primed)
            {
                if (fill < s_targetFill)
                {
                    Array.Clear(data, 0, data.Length);
                    return;
                }
                s_primed = true;
            }

            // Far over (a burst of frames run to catch up): skip ahead to the
            // target rather than play everything late - rate control alone
            // would take seconds to drain it.
            if (fill > 3 * s_targetFill)
            {
                r += fill - s_targetFill;
                fill = s_targetFill;
                s_phase = 0;
            }

            double adjust = RateControlStrength * (fill - s_targetFill) / s_targetFill;
            double step = s_ratio * (1.0 + Math.Max(-MaxRateAdjust, Math.Min(MaxRateAdjust, adjust)));

            for (int i = 0; i < frames; i++)
            {
                if (fill < 2) // underflow: need two frames to interpolate between
                {
                    Array.Clear(data, i * channels, (frames - i) * channels);
                    s_primed = false;
                    break;
                }

                int i0 = r & Mask;
                int i1 = (r + 1) & Mask;
                float t = (float)s_phase;
                float left = s_left[i0] + (s_left[i1] - s_left[i0]) * t;
                float right = s_right[i0] + (s_right[i1] - s_right[i0]) * t;
                if (s_fadeLeft > 0)
                {
                    int f0 = s_fadeRead & Mask, f1 = (s_fadeRead + 1) & Mask;
                    float ft = (float)s_fadePhase;
                    float fadeLeft = s_left[f0] + (s_left[f1] - s_left[f0]) * ft;
                    float fadeRight = s_right[f0] + (s_right[f1] - s_right[f0]) * ft;
                    float w = 1f - s_fadeLeft / (float)Fade;   // 0 -> 1: toward the new head
                    left = fadeLeft + (left - fadeLeft) * w;
                    right = fadeRight + (right - fadeRight) * w;
                    s_fadePhase += step;
                    int fadeAdvance = (int)s_fadePhase;
                    s_fadePhase -= fadeAdvance;
                    s_fadeRead += fadeAdvance;
                    s_fadeLeft--;
                }

                int o = i * channels;
                if (channels == 1)
                {
                    data[o] = (left + right) * 0.5f;
                }
                else
                {
                    data[o] = left;
                    data[o + 1] = right;
                    for (int c = 2; c < channels; c++) data[o + c] = 0f;
                }

                s_phase += step;
                int advance = (int)s_phase;
                s_phase -= advance;
                r += advance;
                fill -= advance;

                // Running low: step back (see Grain) to stretch the sound.
                if (--s_grainLeft <= 0)
                {
                    s_grainLeft = Grain;
                    int jump = Math.Min(Grain / 2, (s_targetFill - fill) / 2);
                    if (s_fadeLeft == 0 && jump > Fade)
                    {
                        s_fadeRead = r;
                        s_fadePhase = s_phase;
                        s_fadeLeft = Fade;
                        r -= jump;
                        fill += jump;
                    }
                }
            }
            Volatile.Write(ref s_read, r);
        }
    }
}
