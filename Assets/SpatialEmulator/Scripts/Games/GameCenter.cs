// GameCenter.cs — the demos' Game Center leaderboards, ENDLESS KNIGHT's and
// THE TIME CRYSTAL's (native side:
// Plugins/iOS/SEGameCenter.mm). Signs the player in at launch, submits a
// run's score (DeathRespawn as the knight falls, RESTART, and the leaderboard
// opening), and loads the global top scores for UI.LeaderboardWindow.
//
// The leaderboard itself is set up in App Store Connect (Game Center, a
// classic high-score leaderboard, best score kept, high to low) with the ID
// LeaderboardId; until it is, loading says it isn't available. In the Editor
// there's no Game Center: loading returns made-up entries.

using System;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace SpatialEmulator.Games
{
    public static class GameCenter
    {
        public const string LeaderboardId = "endlessknight.highscore";
        /// THE TIME CRYSTAL's (set up in App Store Connect the same way).
        public const string TimeCrystalLeaderboardId = "timecrystal.highscore";

        /// ENDLESS KNIGHT's level `level` best time (LevelTimer: hundredths of a second, lowest best).
        public static string LevelTimeBoard(int level) => "endlessknight.level" + level + ".time";
        public const int KnightLevels = 4;
        public static bool IsTimeBoard(string board) => board != null && board.EndsWith(".time");

        /// Hundredths of a second as mm:ss:hh (six digits, colons only).
        public static string FormatTime(long hundredths)
        {
            long minutes = hundredths / 6000, rest = hundredths % 6000;
            return minutes.ToString("00") + ":" + (rest / 100).ToString("00") + ":" + (rest % 100).ToString("00");
        }

        /// The board of the demo that's running (ENDLESS KNIGHT's otherwise).
        public static string CurrentBoard => Demo.TimeCrystalGame.IsRunning ? TimeCrystalLeaderboardId : LeaderboardId;

        /// The demo that's running's score now (0 if neither is).
        public static long CurrentScore =>
            Demo.TimeCrystalGame.IsRunning ? (TimeCrystal.ScoreManager.Instance ? TimeCrystal.ScoreManager.Instance.Score : 0) :
            Demo.PitDemoGame.IsRunning && PitScoreManager.Instance ? PitScoreManager.Instance.GetScore() : 0;
        public const int TopCount = 50;

        public struct Entry
        {
            public int rank;
            public string name;
            public long score;
            public bool me;
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SE_GCAuthenticate();
        [DllImport("__Internal")] static extern bool SE_GCSignedIn();
        [DllImport("__Internal")] static extern bool SE_GCSettled();
        [DllImport("__Internal")] static extern void SE_GCSubmit(string leaderboard, long score);
        [DllImport("__Internal")] static extern void SE_GCLoadTop(string leaderboard, int count, string gameObject, string method);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void SignIn() => SE_GCAuthenticate();

        public static bool SignedIn => SE_GCSignedIn();

        /// Sign-in's finished, one way or the other (UI.StartupSplash waits).
        public static bool Settled => SE_GCSettled();
#else
        public static bool SignedIn => false;
        public static bool Settled => true;
#endif

        static readonly System.Collections.Generic.Dictionary<string, long> s_submitted = new System.Collections.Generic.Dictionary<string, long>();

        /// Sends a run's score to `board` (ENDLESS KNIGHT's if not given;
        /// Game Center keeps each player's best).
        public static void Submit(long score, string board = null)
        {
            board ??= LeaderboardId;
            if (score <= 0 || (s_submitted.TryGetValue(board, out long last) && last == score)) return;
            s_submitted[board] = score;
#if UNITY_IOS && !UNITY_EDITOR
            SE_GCSubmit(board, score);
#endif
        }

        /// Loads the top entries; the reply comes to receiver's `method` as
        /// text for Parse.
        public static void LoadTop(GameObject receiver, string method, string board = null)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SE_GCLoadTop(board ?? LeaderboardId, TopCount, receiver.name, method);
#else
            var lines = new System.Text.StringBuilder();
            for (int i = 1; i <= 30; i++)
                lines.Append(i).Append("|PLAYER ").Append(i).Append('|').Append(2000000 - i * 47311).Append('|').Append(i == 7 ? 1 : 0).Append('\n');
            receiver.SendMessage(method, lines.ToString());
#endif
        }

        /// The entries in a LoadTop reply, or null with `error` set ("!..." replies).
        public static Entry[] Parse(string reply, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(reply)) return Array.Empty<Entry>();
            if (reply[0] == '!') { error = reply.Substring(1); return null; }
            var lines = reply.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var entries = new System.Collections.Generic.List<Entry>(lines.Length);
            foreach (var line in lines)
            {
                var f = line.Split('|');
                if (f.Length < 4 || !int.TryParse(f[0], out int rank) || !long.TryParse(f[2], out long score)) continue;
                entries.Add(new Entry { rank = rank, name = f[1], score = score, me = f[3] == "1" });
            }
            return entries.ToArray();
        }
    }
}
