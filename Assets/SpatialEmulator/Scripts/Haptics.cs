// Haptics.cs — ARcade's taps on the phone (iOS's own feedback generators, in
// Plugins/iOS/SEHaptics.mm): light ticks for buttons and pickups, firmer
// impacts for hits and kicks, heavy for being hurt, success for wins. On by
// default; Settings switches them off (Enabled). Each kind is held off for a
// moment after it plays, so a fountain of gems is a patter, not a buzz.
// A connected controller that can rumble (DualSense, Xbox...) feels each one
// too (Plugins/iOS/SEHaptics.mm, Core Haptics on the controller).
// Nothing in the Editor or off iOS.

using System.Runtime.InteropServices;
using UnityEngine;

namespace SpatialEmulator
{
    public static class Haptics
    {
        public enum Kind { Tick, Light, Medium, Heavy, Soft, Rigid, Success, Warning, Error }

        const string EnabledKey = "ARcade.Haptics";
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 1) == 1;
            set { PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        // The shortest gap between two of a kind (seconds).
        static readonly float[] Gap = { 0.12f, 0.05f, 0.08f, 0.15f, 0.08f, 0.08f, 0.3f, 0.3f, 0.3f };
        static readonly float[] Last = new float[9];

        // Each kind on the controller: how hard, how sharp, how long (0: one tap).
        static readonly Vector3[] Pad =
        {
            new Vector3(0.3f, 0.8f, 0f),      // Tick
            new Vector3(0.45f, 0.5f, 0.05f),  // Light
            new Vector3(0.7f, 0.45f, 0.1f),   // Medium
            new Vector3(1f, 0.3f, 0.22f),     // Heavy
            new Vector3(0.5f, 0.1f, 0.12f),   // Soft
            new Vector3(0.8f, 0.9f, 0.06f),   // Rigid
            new Vector3(0.6f, 0.5f, 0.3f),    // Success
            new Vector3(0.8f, 0.4f, 0.25f),   // Warning
            new Vector3(1f, 0.3f, 0.4f),      // Error
        };

        static bool HasPad => UnityEngine.InputSystem.Gamepad.current != null;

        // A controller that rumbles takes the haptics on its own: the phone's left still (its battery spared).
        static bool PadOnly
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                if (!HasPad) return false;
                if (Time.unscaledTime >= s_padCheckAt) { s_padCheckAt = Time.unscaledTime + 2f; s_padHaptics = _SEPadHasHaptics() != 0; }
                return s_padHaptics;
#else
                return false;
#endif
            }
        }
        static float s_padCheckAt;
        static bool s_padHaptics;

        /// A heavy rumble kept up while it's called every frame (THE TIME CRYSTAL's laser): the controller's held buzz,
        /// or - with no controller that rumbles - the phone's heaviest taps in quick succession.
        public static void Sustain(float intensity = 1f)
        {
            if (!Enabled) return;
            float now = Time.unscaledTime;
            if (now < s_sustainNext) return;
#if UNITY_IOS && !UNITY_EDITOR
            if (PadOnly) { s_sustainNext = now + 0.2f; _SEPadRumble(intensity, 0.3f, -0.25f); }
            else { s_sustainNext = now + 0.08f; _SEHapticImpact(2, intensity); }
#endif
        }
        static float s_sustainNext;

        /// A longer rumble on the controller (a bomb going off), with the phone's heaviest tap.
        public static void Rumble(float intensity, float sharpness, float seconds)
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            if (PadOnly) { _SEPadRumble(intensity, sharpness, seconds); return; }
            _SEHapticImpact(2, 1f);
#endif
        }

        public static void Play(Kind kind, float intensity = 1f)
        {
            if (!Enabled) return;
            int k = (int)kind;
            float now = Time.unscaledTime;
            if (now - Last[k] < Gap[k] && Last[k] > 0f) return;
            Last[k] = now;
#if UNITY_IOS && !UNITY_EDITOR
            if (PadOnly) { var p = Pad[k]; _SEPadRumble(p.x * (kind <= Kind.Rigid ? Mathf.Clamp01(intensity) : 1f), p.y, p.z); return; }
            switch (kind)
            {
                case Kind.Tick: _SEHapticSelection(); break;
                case Kind.Light: _SEHapticImpact(0, intensity); break;
                case Kind.Medium: _SEHapticImpact(1, intensity); break;
                case Kind.Heavy: _SEHapticImpact(2, intensity); break;
                case Kind.Soft: _SEHapticImpact(3, intensity); break;
                case Kind.Rigid: _SEHapticImpact(4, intensity); break;
                case Kind.Success: _SEHapticNotify(0); break;
                case Kind.Warning: _SEHapticNotify(1); break;
                case Kind.Error: _SEHapticNotify(2); break;
            }
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void _SEHapticImpact(int style, float intensity);
        [DllImport("__Internal")] static extern void _SEHapticNotify(int type);
        [DllImport("__Internal")] static extern void _SEHapticSelection();
        [DllImport("__Internal")] static extern void _SEPadRumble(float intensity, float sharpness, float seconds);
        [DllImport("__Internal")] static extern int _SEPadHasHaptics();
#endif
    }
}
