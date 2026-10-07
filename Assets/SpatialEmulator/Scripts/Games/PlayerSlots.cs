// PlayerSlots.cs — games where the coin slot you use picks your character
// (4-player cabinets like The Simpsons and Sunset Riders: each player has
// their own coin slot, start button and controls). For these, holding COIN
// opens a chooser (PlayerSelect), or holding Create/View on a controller
// steps through the players, and the one set of controls (on-screen or
// controller) plays as that player: ArcadeInput.PlayerPort routes it to
// that player's port. The choice is remembered per game.
//
// Listed by main romset name; clones follow their parent, except the
// "2 Players" versions, where you pick your character on screen instead.
//
// Changing player mid-game: for games whose "players in the game" flags are
// known (s_inGameMask: a byte with one bit per player, cleared when that
// player drops to the continue countdown), the app checks the player you're
// leaving. Out (dead, on the countdown): you just join as the new player.
// Still playing: it restarts the game as the new player (RestartAs), so the
// old player can't stay in, idle, holding everyone back. For other games,
// once a coin has gone in, the chooser asks JOIN or RESTART.

using System;
using System.Collections.Generic;
using SpatialEmulator.Controls;
using SpatialEmulator.Mobile;
using UnityEngine;

namespace SpatialEmulator.Games
{
    public static class PlayerSlots
    {
        static readonly Dictionary<string, string[]> s_players = new Dictionary<string, string[]>
        {
            { "simpsons", new[] { "Marge", "Homer", "Bart", "Lisa" } },
            { "ssriders", new[] { "Steve", "Billy", "Bob", "Cormano" } },
            { "tmnt2", new[] { "Leonardo", "Michelangelo", "Donatello", "Raphael" } },
            { "tmnt", new[] { "Leonardo", "Michelangelo", "Donatello", "Raphael" } },
        };

        // Byte holding one bit per player in the game (bit 0 = player 1),
        // found by watching RAM as players join and run out of lives.
        static readonly Dictionary<string, (string cpu, uint address)> s_inGameMask = new Dictionary<string, (string, uint)>
        {
            { "ssriders", ("maincpu", 0x104170) },
            { "simpsons", ("maincpu", 0x4850) },
            { "tmnt2", ("maincpu", 0x1040b1) },
        };

        const string SlotKey = "SpatialEmulator.playerSlot.";

        /// Fired when the player for the selected game changes.
        public static event Action Changed;

        /// A coin went in (or a save was loaded) since the game last started.
        public static bool GameInProgress { get; private set; }

        /// Called when a coin reaches the game.
        public static void NoteCoin() => GameInProgress = true;

        /// Called when a save state is loaded.
        public static void NoteSaveLoaded() => GameInProgress = true;

        /// Called when the game restarts (RESTART button).
        public static void NoteRestart() => GameInProgress = false;

        /// Whether the player on slot is in the game right now: true (playing),
        /// false (not in, or dead on the continue countdown), or null if this
        /// game's flags aren't known.
        public static bool? IsInGame(int slot)
        {
            string game = GameSelection.Current;
            if (For(game) == null || !s_inGameMask.TryGetValue(Root(game), out var mask)) return null;
            int value = LibretroCore.ReadByte(mask.cpu, mask.address);
            if (value < 0) return null;
            return (value & (1 << slot)) != 0;
        }

        /// Switches to slot and restarts the game as that player.
        public static void RestartAs(int slot)
        {
            SetCurrentSlot(slot);
            LibretroCore.Reset();
            GameInProgress = false;
        }

        /// The characters on each coin slot, or null if the game doesn't
        /// pick players by coin slot.
        public static string[] For(string game)
        {
            if (string.IsNullOrEmpty(game)) return null;
            var info = GameCatalog.Find(game);
            if (info != null && info.description.Contains("2 Players")) return null;
            if (s_players.TryGetValue(game, out var names)) return names;
            if (info != null && info.parent.Length > 0 && s_players.TryGetValue(info.parent, out names)) return names;
            return null;
        }

        /// The selected game's characters, or null.
        public static string[] Current => For(GameSelection.Current);

        /// The player (0-based) the controls play as in game.
        public static int SlotFor(string game)
        {
            var names = For(game);
            if (names == null) return 0;
            return Mathf.Clamp(PlayerPrefs.GetInt(SlotKey + Root(game), 0), 0, names.Length - 1);
        }

        public static int CurrentSlot => SlotFor(GameSelection.Current);

        public static void SetCurrentSlot(int slot)
        {
            string game = GameSelection.Current;
            var names = For(game);
            if (names == null) return;
            PlayerPrefs.SetInt(SlotKey + Root(game), Mathf.Clamp(slot, 0, names.Length - 1));
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }

        /// "Player 2 - Homer"
        public static string Describe(int slot)
        {
            var names = Current;
            return names == null ? $"Player {slot + 1}" : $"Player {slot + 1} - {names[slot]}";
        }

        // Versions of a game share the choice.
        static string Root(string game)
        {
            var info = GameCatalog.Find(game);
            return info != null && info.parent.Length > 0 ? info.parent : game;
        }

        static void Apply() => ArcadeInput.PlayerPort = CurrentSlot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            GameSelection.Changed += Apply;
            MobileRetroDepthLayerStack.GameStarted += _ => GameInProgress = false;
            Apply();
        }
    }
}
