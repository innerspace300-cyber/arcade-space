// FlatGames.cs — games whose art doesn't hold up with the layers spread
// apart (Settings > Layer spacing): they always play with every layer on one
// plane. Listed by main romset name; clones follow their parent.

using System.Collections.Generic;

namespace SpatialEmulator.Games
{
    public static class FlatGames
    {
        static readonly HashSet<string> s_flat = new HashSet<string>
        {
            "ssriders", // Sunset Riders
            "simpsons", // The Simpsons
        };

        /// True if the game (or the main version it's a clone of) plays flat.
        public static bool IsFlat(string game)
        {
            if (string.IsNullOrEmpty(game)) return false;
            if (s_flat.Contains(game)) return true;
            var info = GameCatalog.Find(game);
            return info != null && info.parent.Length > 0 && s_flat.Contains(info.parent);
        }

        /// True if the game that's selected plays flat.
        public static bool Current => IsFlat(GameSelection.Current);
    }
}
