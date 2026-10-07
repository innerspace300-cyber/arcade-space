// GameSelection.cs — which romset the cabinet plays, remembered across
// launches. MobileRetroDepthLayerStack loads it when a cabinet is placed and
// restarts the core when it changes; the game picker sets it.

using System;
using UnityEngine;

namespace SpatialEmulator.Games
{
    public static class GameSelection
    {
        const string PrefsKey = "SpatialEmulator.currentGame";

        static string s_current;
        static bool s_loaded;

        /// Fired after Current changes.
        public static event Action Changed;

        /// Fired when the core can't start a game: (romset name, message).
        public static event Action<string, string> LoadFailed;

        /// Romset name (e.g. "sf2"), or null if nothing has been picked.
        public static string Current
        {
            get
            {
                if (!s_loaded)
                {
                    s_loaded = true;
                    // A fresh install starts on the built-in demo.
                    string saved = PlayerPrefs.GetString(PrefsKey, Demo.PitDemoGame.GameName);
                    s_current = saved.Length > 0 ? saved : null;
                }
                return s_current;
            }
        }

        /// Full path of the current romset's zip, or null if there's none on disk.
        public static string CurrentPath
        {
            get
            {
                string name = Current;
                if (name == null) return null;
                string path = RomLibrary.PathFor(name);
                return System.IO.File.Exists(path) ? path : null;
            }
        }

        /// Selects a game; restarts the cabinet's game even if it's already current.
        public static void Select(string name)
        {
            s_loaded = true;
            s_current = name;
            PlayerPrefs.SetString(PrefsKey, name ?? "");
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void ReportLoadFailed(string name, string message) => LoadFailed?.Invoke(name, message);
    }
}
