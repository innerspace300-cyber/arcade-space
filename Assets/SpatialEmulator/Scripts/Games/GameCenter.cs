// GameCenter.cs — ENDLESS KNIGHT's Game Center leaderboard (native side:
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

        static long s_submitted;

        /// Sends a run's score (Game Center keeps each player's best).
        public static void Submit(long score)
        {
            if (score <= 0 || score == s_submitted) return;
            s_submitted = score;
#if UNITY_IOS && !UNITY_EDITOR
            SE_GCSubmit(LeaderboardId, score);
#endif
        }

        /// Loads the top entries; the reply comes to receiver's `method` as
        /// text for Parse.
        public static void LoadTop(GameObject receiver, string method)
        {
#if UNITY_IOS && !UNITY_EDITOR
            SE_GCLoadTop(LeaderboardId, TopCount, receiver.name, method);
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
