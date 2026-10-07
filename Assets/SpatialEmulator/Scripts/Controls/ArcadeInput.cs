// ArcadeInput.cs — current state of the on-screen arcade controls, in
// libretro RetroPad terms, read by LibretroCore's input callback.
//
// libretro-mame's default mapping (src/osd/modules/input/input_retro.cpp):
// RetroPad B/A/Y/X/L/R = MAME Button 1-6, Select = Coin, Start = Start.
//
// Every press stays visible for at least a few emulated frames, so a quick
// tap can't begin and end between two of the core's input polls and be lost.

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

        /// The libretro port (player - 1) the controls play as: 0 unless the
        /// game picks players by coin slot (PlayerSlots).
        public static int PlayerPort;

        public static void SetPressed(RetroPadButton button, bool pressed, int minFrames = 3, InputSource source = InputSource.Touch)
        {
            int id = (int)button;
            int src = (int)source;
            if (pressed && !s_held[src, id] && s_minFramesLeft[id] < minFrames)
                s_minFramesLeft[id] = minFrames;
            s_held[src, id] = pressed;
        }

        public static bool IsPressed(uint id)
        {
            if (id >= IdCount) return false;
            if (s_minFramesLeft[id] > 0) return true;
            for (int src = 0; src < SourceCount; src++)
                if (s_held[src, id]) return true;
            return false;
        }

        /// Call once per emulated frame, from the core's input-poll callback.
        public static void OnPoll()
        {
            for (int i = 0; i < IdCount; i++)
                if (s_minFramesLeft[i] > 0) s_minFramesLeft[i]--;
        }
    }
}
