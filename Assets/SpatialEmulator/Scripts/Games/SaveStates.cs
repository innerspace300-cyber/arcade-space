// SaveStates.cs — save states on disk: Documents/states/<romset>/<ticks>.state
// plus a <ticks>.png thumbnail of the screen at the moment of saving.
// Shown and used by the cabinet's side panel (SaveStatePanel).

using System;
using System.Collections.Generic;
using System.IO;
using SpatialEmulator.Mobile;
using UnityEngine;

namespace SpatialEmulator.Games
{
    public sealed class SaveState
    {
        public string game;
        public string statePath;
        public string thumbnailPath;
        public DateTime savedAt;
    }

    public static class SaveStates
    {
        public const string Folder = "states";

        /// Fired after a save is added or deleted, with the romset name.
        public static event Action<string> Changed;

        static string DirFor(string game) => Path.Combine(Application.persistentDataPath, Folder, game);

        /// The game's saves, newest first.
        public static List<SaveState> List(string game)
        {
            var saves = new List<SaveState>();
            if (string.IsNullOrEmpty(game)) return saves;
            string dir = DirFor(game);
            if (!Directory.Exists(dir)) return saves;
            foreach (string path in Directory.GetFiles(dir, "*.state"))
            {
                if (!long.TryParse(Path.GetFileNameWithoutExtension(path), out long ticks)) continue;
                string thumb = Path.ChangeExtension(path, ".png");
                saves.Add(new SaveState
                {
                    game = game,
                    statePath = path,
                    thumbnailPath = File.Exists(thumb) ? thumb : null,
                    savedAt = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime(),
                });
            }
            saves.Sort((a, b) => b.savedAt.CompareTo(a.savedAt));
            return saves;
        }

        /// Saves the running game. thumbnail may be null.
        public static SaveState Save(string game, Texture2D thumbnail)
        {
            byte[] data = LibretroCore.SaveState();
            if (data == null) return null;
            try
            {
                string dir = DirFor(game);
                Directory.CreateDirectory(dir);
                long ticks = DateTime.UtcNow.Ticks;
                string path = Path.Combine(dir, ticks + ".state");
                File.WriteAllBytes(path, data);
                string thumbPath = null;
                if (thumbnail)
                {
                    thumbPath = Path.ChangeExtension(path, ".png");
                    File.WriteAllBytes(thumbPath, thumbnail.EncodeToPNG());
                }
                Changed?.Invoke(game);
                return new SaveState { game = game, statePath = path, thumbnailPath = thumbPath, savedAt = DateTime.Now };
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveStates] save failed: {e}");
                return null;
            }
        }

        public static bool Load(SaveState save)
        {
            try
            {
                bool loaded = LibretroCore.LoadState(File.ReadAllBytes(save.statePath));
                if (loaded) PlayerSlots.NoteSaveLoaded();
                return loaded;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveStates] load of {save.statePath} failed: {e}");
                return false;
            }
        }

        public static void Delete(SaveState save)
        {
            try
            {
                File.Delete(save.statePath);
                if (save.thumbnailPath != null) File.Delete(save.thumbnailPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveStates] delete failed: {e}");
            }
            Changed?.Invoke(save.game);
        }
    }
}
