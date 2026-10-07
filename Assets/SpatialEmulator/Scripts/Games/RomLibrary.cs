// RomLibrary.cs — the romsets the player has added, kept unmodified as
// Documents/roms/<name>.zip (MAME finds files by name and CRC, and a Neo Geo
// game looks for neogeo.zip in the same folder).
//
// Import() copies in a zip picked from the Files app. A zip whose name isn't
// a MAME short name ("Street Fighter II (World).zip") is identified by the
// CRCs of its files and saved under the right name. Scan() audits every zip
// against GameCatalog, so the picker can say "incomplete" or "needs the Neo
// Geo BIOS" instead of letting the core fail to load it.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SpatialEmulator.Games
{
    public enum RomStatus
    {
        Ready,
        Incomplete,     // files missing that no parent/BIOS zip would supply
        NeedsParent,    // a clone whose shared files live in its parent's zip, which isn't there
        NeedsBios,      // a Neo Geo or PGM game without its BIOS zip (neogeo.zip, pgm.zip)
        NeedsQSound,    // a QSound game whose only missing file is the QSound DSP ROM (qsound.zip)
    }

    public sealed class LibraryGame
    {
        public CatalogGame game;
        public string path;
        public RomStatus status;
        public int missingFiles;
    }

    public static class RomLibrary
    {
        public const string RomsFolder = "roms";

        // Support zips that aren't games: the Neo Geo and PGM BIOSes and the QSound DSP
        // ROM (MAME looks for the latter as qsound_hle.zip, the device's name;
        // romsets often call it qsound.zip).
        static readonly string[] SupportSets = { GameCatalog.NeoGeoBios, GameCatalog.PgmBios, "qsound_hle", "qsound" };

        // Files that only appear in a Neo Geo BIOS zip.
        static readonly string[] NeoGeoBiosFiles = { "sfix.sfix", "sm1.sm1", "000-lo.lo" };

        // And in a PGM BIOS zip: its text tiles, sound samples and 68000 code.
        static readonly string[] PgmBiosFiles = { "pgm_t01s.rom", "pgm_m01s.rom", "pgm_p02s.u20", "pgm_p01s.u20" };

        // The QSound chip's DSP ROM (dl-1425.bin), shared by every CPS2 game.
        const uint QSoundDspCrc = 0xd6cf5ef5;

        public static bool HasQSound => File.Exists(PathFor("qsound_hle"));

        // (Kept once it's known: imports run off the main thread, where Unity's
        // persistentDataPath can't be read - GamePicker reads this first.)
        static string s_romsDir;
        public static string RomsDir => s_romsDir ??= Path.Combine(Application.persistentDataPath, RomsFolder);

        public static string PathFor(string name) => Path.Combine(RomsDir, name + ".zip");

        public static bool HasNeoGeoBios => File.Exists(PathFor(GameCatalog.NeoGeoBios));

        /// Whether the game's board BIOS zip is in the library (true if it needs none).
        public static bool HasBios(CatalogGame game) => game.Bios == null || File.Exists(PathFor(game.Bios));

        public struct ImportResult
        {
            public bool ok;
            public string name;       // the romset or support set it was saved as
            public string message;    // shown to the player
            public bool unsupported;  // not a game (or support set) ARcade runs
            public bool already;      // the same file was in the library already (skipExisting)
        }

        /// Copies a zip into the library. deleteSource removes the original
        /// afterwards (for files the app owns: the picker's temporary copy, or
        /// a zip dropped into Documents with the Files app).
        /// skipExisting leaves a zip be if the library has one the same size
        /// under its name (SCAN FOLDER, run again over the same folder).
        public static ImportResult Import(string sourcePath, bool deleteSource, bool skipExisting = false)
        {
            string fileName = Path.GetFileName(sourcePath);
            var entries = ZipDirectory.Read(sourcePath);
            if (entries == null)
                return Unsupported($"{fileName} is not a zip file");

            string target = Identify(Path.GetFileNameWithoutExtension(sourcePath), entries, out string label);
            if (target == null)
                return Unsupported($"{fileName} is not a supported arcade game");

            if (skipExisting && File.Exists(PathFor(target)) && new FileInfo(PathFor(target)).Length == new FileInfo(sourcePath).Length)
                return new ImportResult { ok = true, already = true, name = target, message = label + " is already added" };

            try
            {
                Directory.CreateDirectory(RomsDir);
                string destination = PathFor(target);
                if (Path.GetFullPath(destination) != Path.GetFullPath(sourcePath))
                {
                    File.Copy(sourcePath, destination, true);
                    if (target == "qsound")
                        File.Copy(sourcePath, PathFor("qsound_hle"), true);
                    if (deleteSource) File.Delete(sourcePath);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[RomLibrary] import of {sourcePath} failed: {e}");
                return Fail($"Couldn't copy {fileName}");
            }

            var game = GameCatalog.Find(target);
            string message = "Added " + label;
            if (game != null && !HasBios(game))
                message += $". {game.BoardLabel} games also need {game.Bios}.zip (the {game.BiosLabel})";
            else if (game != null && !game.isBios && Array.IndexOf(game.crcs, QSoundDspCrc) >= 0)
            {
                var entry = new LibraryGame { game = game, path = PathFor(target) };
                Audit(entry, new Dictionary<string, HashSet<uint>>());
                if (entry.status == RomStatus.NeedsQSound)
                    message += ". CPS2 games also need qsound.zip (the QSound sound chip), added once";
            }
            return new ImportResult { ok = true, name = target, message = message };

            ImportResult Fail(string why) => new ImportResult { ok = false, message = why };
            ImportResult Unsupported(string why) => new ImportResult { ok = false, unsupported = true, message = why };
        }

        /// Removes a game's zip from the library. If it's the game being
        /// played, the selection is cleared, which leaves the cabinet blank.
        public static bool Delete(string name)
        {
            try
            {
                File.Delete(PathFor(name));
            }
            catch (Exception e)
            {
                Debug.LogError($"[RomLibrary] delete of {name} failed: {e}");
                return false;
            }
            if (GameSelection.Current == name) GameSelection.Select(null);
            return true;
        }

        /// Imports every zip dropped straight into Documents (Files app), so
        /// they don't need to go into the roms folder by hand.
        public static List<ImportResult> ImportLooseZips()
        {
            var results = new List<ImportResult>();
            string documents = Application.persistentDataPath;
            if (!Directory.Exists(documents)) return results;
            foreach (string zip in Directory.GetFiles(documents, "*.zip"))
                results.Add(Import(zip, deleteSource: true));
            return results;
        }

        /// The romset name to store a zip as, or null if it isn't one we can run.
        static string Identify(string fileStem, List<ZipDirectory.Entry> entries, out string label)
        {
            string stem = fileStem.ToLowerInvariant();

            if (stem == GameCatalog.NeoGeoBios)
            {
                label = "the Neo Geo BIOS";
                return GameCatalog.NeoGeoBios;
            }
            if (stem == GameCatalog.PgmBios)
            {
                label = "the PGM BIOS";
                return GameCatalog.PgmBios;
            }
            if (stem == "qsound" || stem == "qsound_hle")
            {
                label = "the QSound ROM";
                return stem;
            }

            var named = GameCatalog.Find(stem);
            if (named != null && !named.isBios)
            {
                label = named.Title;
                return named.name;
            }

            // Renamed zip: the game it covers most of, counting a game only if
            // the zip holds nearly all of it (merged sets add their clones'
            // files, some add hacks) or nearly all the zip's files belong to
            // it (a split clone zip holds just the files that differ from its
            // parent). Ties - a merged set covers its clones too - go to the
            // parent, which is what the zip is named after.
            var crcs = new HashSet<uint>();
            foreach (var entry in entries) crcs.Add(entry.crc);
            CatalogGame best = null;
            float bestCoverage = 0;
            foreach (var game in GameCatalog.All)
            {
                if (game.isBios || game.crcs.Length == 0) continue;
                int matched = 0;
                foreach (uint crc in game.crcs)
                    if (crcs.Contains(crc)) matched++;
                float coverage = matched / (float)game.crcs.Length;
                bool mostlyThisGame = matched >= 0.8f * crcs.Count;
                if (matched == 0 || (coverage < 0.9f && !mostlyThisGame)) continue;
                bool isParent = GameCatalog.Find(game.parent) is not { isBios: false };
                bool bestIsParent = best != null && GameCatalog.Find(best.parent) is not { isBios: false };
                if (coverage > bestCoverage + 0.001f || (coverage > bestCoverage - 0.001f && isParent && !bestIsParent))
                {
                    best = game;
                    bestCoverage = coverage;
                }
            }
            if (best != null)
            {
                label = best.Title;
                return best.name;
            }

            // A BIOS zip under another name (looked for after the games: some
            // games' zips hold copies of BIOS files).
            if (LooksLike(entries, NeoGeoBiosFiles))
            {
                label = "the Neo Geo BIOS";
                return GameCatalog.NeoGeoBios;
            }
            if (LooksLike(entries, PgmBiosFiles))
            {
                label = "the PGM BIOS";
                return GameCatalog.PgmBios;
            }
            label = null;
            return null;
        }

        // Two or more of a BIOS's own files.
        static bool LooksLike(List<ZipDirectory.Entry> entries, string[] biosFiles)
        {
            int hits = 0;
            foreach (var entry in entries)
                if (Array.IndexOf(biosFiles, Path.GetFileName(entry.name).ToLowerInvariant()) >= 0)
                    hits++;
            return hits >= 2;
        }

        /// Every game in the library, audited, sorted by title.
        public static List<LibraryGame> Scan()
        {
            // The built-in demo is always installed, first in the list.
            var games = new List<LibraryGame>();
            var demo = new LibraryGame { game = Demo.PitDemoGame.Info, status = RomStatus.Ready };
            if (!Directory.Exists(RomsDir)) { games.Add(demo); return games; }

            var crcCache = new Dictionary<string, HashSet<uint>>();
            foreach (string zip in Directory.GetFiles(RomsDir, "*.zip"))
            {
                string name = Path.GetFileNameWithoutExtension(zip).ToLowerInvariant();
                if (Array.IndexOf(SupportSets, name) >= 0) continue;
                var game = GameCatalog.Find(name);
                if (game == null || game.isBios) continue;

                var entry = new LibraryGame { game = game, path = zip };
                Audit(entry, crcCache);
                games.Add(entry);
            }
            games.Sort((a, b) => string.Compare(a.game.description, b.game.description, StringComparison.OrdinalIgnoreCase));
            games.Insert(0, demo);
            return games;
        }

        static void Audit(LibraryGame entry, Dictionary<string, HashSet<uint>> cache)
        {
            var game = entry.game;
            // What MAME would search: the game's zip, its parents' zips, and
            // the QSound device zip.
            var pool = new HashSet<uint>(CrcsOf(game.name, cache));
            bool parentMissing = false;
            for (var parent = GameCatalog.Find(game.parent); parent != null; parent = GameCatalog.Find(parent.parent))
            {
                if (parent.isBios)
                {
                    // Some Neo Geo games list BIOS-side files (sm1.sm1) too.
                    pool.UnionWith(CrcsOf(parent.name, cache));
                    break;
                }
                if (File.Exists(PathFor(parent.name))) pool.UnionWith(CrcsOf(parent.name, cache));
                else parentMissing = true;
            }
            pool.UnionWith(CrcsOf("qsound_hle", cache));

            int missing = 0;
            bool qsoundMissing = false;
            foreach (uint crc in game.crcs)
                if (!pool.Contains(crc))
                {
                    missing++;
                    if (crc == QSoundDspCrc) qsoundMissing = true;
                }
            entry.missingFiles = missing;

            if (!HasBios(game)) entry.status = RomStatus.NeedsBios;
            else if (missing == 0) entry.status = RomStatus.Ready;
            else if (qsoundMissing && missing == 1) entry.status = RomStatus.NeedsQSound;
            else if (parentMissing) entry.status = RomStatus.NeedsParent;
            else entry.status = RomStatus.Incomplete;
        }

        static HashSet<uint> CrcsOf(string name, Dictionary<string, HashSet<uint>> cache)
        {
            if (cache.TryGetValue(name, out var crcs)) return crcs;
            crcs = new HashSet<uint>();
            string path = PathFor(name);
            if (File.Exists(path))
            {
                var entries = ZipDirectory.Read(path);
                if (entries != null)
                    foreach (var entry in entries) crcs.Add(entry.crc);
            }
            cache[name] = crcs;
            return crcs;
        }
    }
}
