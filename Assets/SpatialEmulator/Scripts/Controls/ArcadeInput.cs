// ArcadeInput.cs — current state of the on-screen arcade controls, in
// libretro RetroPad terms, read by LibretroCore's input callback.
//
// libretro-mame's default mapping (src/osd/modules/input/input_retro.cpp):
// RetroPad B/A/Y/X/L/R = MAME Button 1-6, Select = Coin, Start = Start.
//
// Every press stays visible for at least a few emulated frames, so a quick
// tap can't begin and end between two of the core's input polls and be lost.
// While the game's paused (a menu's open) no press reaches it or is saved for
// later, and a button held across the pause only counts once it's let go.
//
// Coins are rationed: one a second at most, each a clean tap of CoinPulseFrames
// (held or tapped). Some games count coins faster than they can take them and
// stop with a COIN ERROR (Muchi Muchi Pork) - taps in between are dropped.

using UnityEngine;

namespace SpatialEmulator.Controls
{
    // Values are libretro's RETRO_DEVICE_ID_JOYPAD_* ids.
    public enum RetroPadButton
    {
        B = 0, Y = 1, Select = 2, Start = 3,
        Up = 4, Down = 5, Left = 6, Right = 7,
        A = 8, X = 9, L = 10, R = 11,
    }

    /// Who is pressing: the on-screen controls and a game controller are
    /// tracked separately, so releasing one can't cancel the other.
    public enum InputSource { Touch = 0, Gamepad = 1 }

    public static class ArcadeInput
    {
        const int IdCount = 16;
        const int SourceCount = 2;
        static readonly bool[,] s_held = new bool[SourceCount, IdCount];
        static readonly int[] s_minFramesLeft = new int[IdCount];
        static readonly float[] s_push = new float[SourceCount];

        /// How far the joystick is pushed, 0 (centre) to 1 (all the way): the
        /// touch pad's knob or a controller's stick (its D-pad counts as 1).
        /// The built-in demo walks, runs or sprints by it; emulated games
        /// only see the directions.
        public static void SetPush(float push, InputSource source = InputSource.Touch) => s_push[(int)source] = Mathf.Clamp01(push);
        public static float Push => Mathf.Max(s_push[0], s_push[1]);

        static readonly Vector2[] s_stick = new Vector2[SourceCount];

        /// The joystick as a direction and how far (length 0..1, y up): the
        /// touch pad's knob or a controller's stick. THE TIME CRYSTAL steers
        /// by it (any angle, slow when nudged); everything else reads the
        /// eight directions.
        public static void SetStick(Vector2 stick, InputSource source = InputSource.Touch) => s_stick[(int)source] = Vector2.ClampMagnitude(stick, 1f);
        public static Vector2 Stick => s_stick[0].sqrMagnitude >= s_stick[1].sqrMagnitude ? s_stick[0] : s_stick[1];

        /// The libretro port (player - 1) the controls play as: 0 unless the
        /// game picks players by coin slot (PlayerSlots).
        public static int PlayerPort;

        public static void SetPressed(RetroPadButton button, bool pressed, int minFrames = 3, InputSource source = InputSource.Touch)
        {
            int id = (int)button;
            int src = (int)source;
            if (Mobile.MobileRetroDepthLayerStack.Paused)
            {
                // (Paused: noted as held, never stretched into a press for later.)
                if (pressed) s_mustRelease[id] = true;
                s_held[src, id] = pressed;
                return;
            }
            if (pressed && !s_held[src, id] && s_minFramesLeft[id] < minFrames)
                s_minFramesLeft[id] = minFrames;
            s_held[src, id] = pressed;
        }

        const int CoinPulseFrames = 6, CoinGapFrames = 60;   // (emulated frames: a coin's tap, and a second between coins)
        static int s_coinPulse, s_coinWait;
        static bool s_coinWas;

        public static bool IsPressed(uint id)
        {
            if (id == (uint)RetroPadButton.Select) return !Mobile.MobileRetroDepthLayerStack.Paused && s_coinPulse > 0;
            return Read(id);
        }

        // As pressed, paused or let go across a pause taken into account (coins not rationed yet).
        static bool Read(uint id)
        {
            if (id >= IdCount) return false;
            Guard();
            // While the game's paused nothing reaches it; a button held from then on
            // counts only once it's been let go (no shot or bomb the moment it goes on).
            if (Mobile.MobileRetroDepthLayerStack.Paused) return false;
            if (s_mustRelease[id])
            {
                if (HeldNow(id)) return false;
                s_mustRelease[id] = false;
            }
            return IsPressedRaw(id);
        }

        /// The buttons as they are, paused or not: for prompts that answer over a
        /// paused game (DemoDialogue's RESTART warning).
        public static bool IsPressedRaw(uint id)
        {
            if (id >= IdCount) return false;
            if (s_minFramesLeft[id] > 0) return true;
            return HeldNow(id);
        }

        static bool HeldNow(uint id)
        {
            for (int src = 0; src < SourceCount; src++)
                if (s_held[src, id]) return true;
            return false;
        }

        static readonly bool[] s_mustRelease = new bool[IdCount];

        // On pausing: any quick tap still being stretched is dropped, and from then
        // until it's released, any button pressed (or still held) won't count.
        static void Guard()
        {
            bool paused = Mobile.MobileRetroDepthLayerStack.Paused;
            if (paused)
                for (int i = 0; i < IdCount; i++)
                {
                    if (HeldNow((uint)i) || s_minFramesLeft[i] > 0) s_mustRelease[i] = true;
                    s_minFramesLeft[i] = 0;
                }
        }

        /// Call once per emulated frame, from the core's input-poll callback.
        public static void OnPoll()
        {
            // A new coin only when the button's newly down and a second's gone since the last.
            bool coin = Read((uint)RetroPadButton.Select);
            if (s_coinPulse > 0) s_coinPulse--;
            if (s_coinWait > 0) s_coinWait--;
            if (coin && !s_coinWas && s_coinWait == 0) { s_coinPulse = CoinPulseFrames; s_coinWait = CoinGapFrames; }
            s_coinWas = coin;
            for (int i = 0; i < IdCount; i++)
                if (s_minFramesLeft[i] > 0) s_minFramesLeft[i]--;
        }
    }
}
