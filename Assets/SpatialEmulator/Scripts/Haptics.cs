// Haptics.cs — ARcade's taps on the phone (iOS's own feedback generators, in
// Plugins/iOS/SEHaptics.mm): light ticks for buttons and pickups, firmer
// impacts for hits and kicks, heavy for being hurt, success for wins. On by
// default; Settings switches them off (Enabled). Each kind is held off for a
// moment after it plays, so a fountain of gems is a patter, not a buzz.
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

        public static void Play(Kind kind, float intensity = 1f)
        {
            if (!Enabled) return;
            int k = (int)kind;
            float now = Time.unscaledTime;
            if (now - Last[k] < Gap[k] && Last[k] > 0f) return;
            Last[k] = now;
#if UNITY_IOS && !UNITY_EDITOR
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
#endif
    }
}
