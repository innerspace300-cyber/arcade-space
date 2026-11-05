// MenuPause.cs — the game holds while a menu is open: the screen's
// three-bar menu (MenuDropdown) or the AR side menus behind the frame's
// three bars (GamesPanel). As the menu closes it goes on - after a 3, 2, 1
// (ScreenCap's countdown) only in a vertical game (a shoot-'em-up: THE TIME
// CRYSTAL, CAVE...), straight away otherwise. Its sound holds too (AudioListener.pause). CAP tapped from a menu closes
// it and its own countdown is the one: the game goes on as the recording begins.

using SpatialEmulator.Mobile;

namespace SpatialEmulator.UI
{
    public static class MenuPause
    {
        /// The menus that hold the game: the screen's three-bar menu and the AR side menus.
        public enum Menu { Screen, Side }
        static readonly bool[] s_open = new bool[2];
        static bool s_counting;
        // (Held while either's open: the 3, 2, 1 only once both are closed.)
        static bool s_held => s_open[0] || s_open[1];

        public static void Open(Menu menu)
        {
            s_open[(int)menu] = true;
            MobileRetroDepthLayerStack.Paused = true;
            // Quiet too: the campfire, music, sounds and the game's own audio hold with it.
            UnityEngine.AudioListener.pause = true;
        }

        public static void Close(Menu menu)
        {
            if (!s_open[(int)menu]) return;
            s_open[(int)menu] = false;
            if (s_held) return;   // (the other menu's still open: still paused, no countdown)
            if (ScreenCap.Counting) { ScreenCap.ResumeOnRecord = true; return; }   // (CAP's countdown carries it)
            if (s_counting) return;   // (one already counting: it'll let go)
            // Decided next frame: a window opened from it as it closed (GAMES, SCORES, SAVES...) keeps the game held,
            // sound and all, and the 3, 2, 1 waits till that's closed too and the game's showing again.
            s_game = Games.GameSelection.Current;
            Watcher.Wait();
        }

        static string s_game;

        // Nothing open over it now: going on (a 3, 2, 1 first in a vertical game - not for a new game picked meanwhile).
        static void Go()
        {
            if (s_held || s_counting) return;
            // (No 3, 2, 1 for a horizontal game, nor THE TIME CRYSTAL's Choose Your Fighter - only its gameplay.)
            bool choosing = Demo.TimeCrystalGame.IsRunning && TimeCrystal.TcFighterSelect.Active;
            if (!MobileRetroDepthLayerStack.Vertical || choosing || Games.GameSelection.Current != s_game) { ResumeNow(); return; }
            MobileRetroDepthLayerStack.Paused = true;
            UnityEngine.AudioListener.pause = true;
            s_counting = true;
            ScreenCap.RunCountdown(() => { s_counting = false; ResumeNow(); });
        }

        // Waits out the windows opened from a menu as it closed, then lets the game go (Go).
        class Watcher : UnityEngine.MonoBehaviour
        {
            static Watcher s_watcher;
            bool _waiting;

            public static void Wait()
            {
                if (!s_watcher)
                {
                    var go = new UnityEngine.GameObject("Menu Pause Watcher");
                    DontDestroyOnLoad(go);
                    s_watcher = go.AddComponent<Watcher>();
                }
                s_watcher._waiting = true;
            }

            void Update()
            {
                if (!_waiting) return;
                if (s_held || s_counting) { _waiting = false; return; }   // (a menu's back open: it'll close again itself)
                if (ScreenCap.Counting) { _waiting = false; ScreenCap.ResumeOnRecord = true; return; }
                if (OthersOpen) { UnityEngine.AudioListener.pause = true; return; }   // (still held, quiet, under the window)
                _waiting = false;
                Go();
            }
        }

        /// Goes on now - unless a menu or window's holding it again meanwhile.
        public static void ResumeNow()
        {
            if (s_held) return;
            UnityEngine.AudioListener.pause = false;
            if (OthersOpen) return;
            MobileRetroDepthLayerStack.Paused = false;
        }

        /// Every menu folded away and the game straight back on, no 3, 2, 1
        /// (RESTART answered YES: the new run starts playing).
        public static void CloseMenus()
        {
            if (SaveStatePanel.SideMenusOpen) SaveStatePanel.SideMenusOpen = false;
            MenuDropdown.CloseIfOpen();
            bool was = s_held;
            s_open[0] = s_open[1] = false;
            if (was) ResumeNow();
        }

        /// A menu's open (the game's held by it): windows opened over it leave the game paused when they close.
        public static bool Held => s_held;

        static bool OthersOpen => GamePicker.IsOpen || SettingsScreen.IsOpen || LeaderboardWindow.IsOpen || SavesWindow.IsOpen;
    }
}
