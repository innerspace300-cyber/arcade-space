// GameCatalog.cs — every game the spatial core can run, read from
// Data/GameCatalog.txt. That file is generated from the core's own driver
// sources (spatial-emulator-core/tools/gen_game_catalog.py), so rerun the
// generator whenever the core's driver list changes.
//
// One line per MAME machine: name|parent|year|company|description|board|flags|crc,crc,...
// The CRCs are the ROM files the game's romset must contain (plus the QSound
// DSP ROM for QSound games), which is what RomLibrary audits a zip against.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpatialEmulator.Games
{
    public sealed class CatalogGame
    {
        public string name;         // MAME short name, e.g. "sf2" - the zip must be named <name>.zip
        public string parent;       // "" for a parent set; "neogeo" for Neo Geo parents
        public string year;
        public string company;
        public string description;  // e.g. "Street Fighter II: The World Warrior (World 910522)"
        public string board;        // cps1 / cps2 / neogeo / m92
        public bool isBios;
        public bool notWorking;
        public bool imperfect;
        /// How far its monitor's turned, clockwise (90, 180, 270; 0 for a
        /// horizontal game) - vertical games' layers come out of the driver unturned.
        public int rotation;
        public uint[] crcs;

        /// The description without its trailing "(region/version)" part.
        public string Title
        {
            get
            {
                int paren = description.IndexOf(" (", StringComparison.Ordinal);
                return paren > 0 ? description.Substring(0, paren) : description;
            }
        }

        /// The "(region/version)" part of the description, without brackets, or "".
        public string Version
        {
            get
            {
                int paren = description.IndexOf(" (", StringComparison.Ordinal);
                if (paren < 0) return "";
                return description.Substring(paren + 2).TrimEnd(')').Replace(") (", ", ");
            }
        }

        /// Too heavy to keep full speed in AR (with the camera running): it
        /// plays in FULL SCREEN only (UI.FullScreenTest). CV1000's boards.
        public bool FullScreenOnly => board == "cv1000";
        /// A vertical (tate) game: its monitor's on its side - it gets the vertical cabinet.
        public bool Vertical => rotation == 90 || rotation == 270;

        /// The BIOS zip the game needs besides its own (null if none): the BIOS
        /// its parents lead up to - neogeo, pgm. (Not every PGM game: CAVE's
        /// carry the BIOS parts they use in their own zips.)
        public string Bios
        {
            get
            {
                if (isBios) return null;
                for (var parent = GameCatalog.Find(this.parent); parent != null; parent = GameCatalog.Find(parent.parent))
                    if (parent.isBios) return parent.name;
                return null;
            }
        }

        /// What the BIOS is called to the player: "Neo Geo BIOS", "PGM BIOS".
        public string BiosLabel => board == "neogeo" ? "Neo Geo BIOS" : BoardLabel + " BIOS";

        public string BoardLabel => board switch
        {
            "cps1" => "CPS1",
            "cps2" => "CPS2",
            "neogeo" => "NEO GEO",
            "m92" => "IREM M92",
            "pgm" => "PGM",
            "demo" => "DEMO",
            _ => board.ToUpperInvariant(),
        };
    }

    public static class GameCatalog
    {
        public const string NeoGeoBios = "neogeo";
        public const string PgmBios = "pgm";

        static Dictionary<string, CatalogGame> s_games;
        static List<CatalogGame> s_list;

        public static bool IsLoaded => s_games != null;

        public static void Load(TextAsset asset)
        {
            if (s_games != null || asset == null) return;
            s_games = new Dictionary<string, CatalogGame>(StringComparer.OrdinalIgnoreCase);
            s_list = new List<CatalogGame>();
            foreach (string line in asset.text.Split('\n'))
            {
                string[] f = line.TrimEnd('\r').Split('|');
                if (f.Length < 8) continue;
                string[] flags = f[6].Split(',');
                string[] hex = f[7].Length > 0 ? f[7].Split(',') : Array.Empty<string>();
                var crcs = new uint[hex.Length];
                for (int i = 0; i < hex.Length; i++)
                    crcs[i] = Convert.ToUInt32(hex[i], 16);
                var game = new CatalogGame
                {
                    name = f[0], parent = f[1], year = f[2], company = f[3], description = f[4], board = f[5],
                    isBios = Array.IndexOf(flags, "bios") >= 0,
                    notWorking = Array.IndexOf(flags, "notworking") >= 0,
                    imperfect = Array.IndexOf(flags, "imperfect") >= 0,
                    rotation = Array.IndexOf(flags, "rot90") >= 0 ? 90 : Array.IndexOf(flags, "rot180") >= 0 ? 180 : Array.IndexOf(flags, "rot270") >= 0 ? 270 : 0,
                    crcs = crcs,
                };
                s_games[game.name] = game;
                s_list.Add(game);
            }
        }

        public static CatalogGame Find(string name)
            => s_games != null && name != null && s_games.TryGetValue(name, out var game) ? game : null;

        public static IReadOnlyList<CatalogGame> All => s_list ?? (IReadOnlyList<CatalogGame>)Array.Empty<CatalogGame>();
    }
}
