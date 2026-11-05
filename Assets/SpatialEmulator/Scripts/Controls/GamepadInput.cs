// GamepadInput.cs — plays the game with a Bluetooth controller (PS5
// DualSense, PS4, Xbox, MFi - anything the Input System sees as a Gamepad;
// pair it in the iOS Settings app). Feeds ArcadeInput as its own source, so
// it works alongside the on-screen controls.
//
// Also decides whether the on-screen controls show (AppSettings.TouchControls:
// Auto hides them while a controller is connected) and whether they take
// touches (not while the game picker or settings are open).
//
// libretro-mame maps RetroPad B/A/Y/X/L/R to MAME Buttons 1-6:
//   Cross / A      -> Button 1 (on-screen A)
//   Circle / B     -> Button 2 (on-screen B)
//   Square / X     -> Button 3 (on-screen C)
//   Triangle / Y   -> Button 4 (Neo Geo D)
//   L2 / R2        -> Buttons 5 / 6 (Street Fighter II's kicks)
//   L1 -> Coin, R1 -> Start (TvCoinHints says so on a TV); Create / View is a coin
//   too (held: next player, in games where the coin slot picks the player -
//   PlayerSlots; mid-game the hold asks: R1 joins as that player, a second hold
//   restarts as them)
//   D-pad or left stick -> joystick
//   ENDLESS KNIGHT: Cross jump, Square attack, Circle C (held: spell wheel), Triangle Great Heal
// Options opens the screen's menu (MenuDropdown), everywhere (ENDLESS KNIGHT's
// Great Heal is on Triangle). While a menu's up the controller steers it
// instead (ControllerMenuNav).

using System;
using SpatialEmulator.Games;
using SpatialEmulator.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SpatialEmulator.Controls
{
    public class GamepadInput : MonoBehaviour
    {
        [Tooltip("The on-screen controls' canvas (hidden when AppSettings says so).")]
        public Canvas touchControls;
        public ArcadeTouchRouter router;
        [Range(0.1f, 0.9f)] public float stickDeadZone = 0.45f;
        [Tooltip("Games where the coin slot picks the player: holding Create/View this long steps to the next player.")]
        public float playerHoldSeconds = 0.6f;

        [Tooltip("Seconds a first hold (mid-game) waits for the second hold that restarts as the next player.")]
        public float restartConfirmSeconds = 4f;
        bool _waitRelease;

        bool _coinPending;
        float _coinDownAt;
        float _restartArmedUntil;
        int _armedNext;

        /// Fired when a controller connects (its name) or disconnects (null).
        public static event Action<string> ControllerChanged;

        /// The connected controller's name, or null.
        public static string ControllerName => Gamepad.current?.displayName;

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int SE_ControllerBattery(out int charging);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int SE_ControllerLight(float r, float g, float b);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int SE_ControllerCount();
#endif
        // The light bar dimmed to a faint ARcade violet while it's connected (bright, it runs the battery down).
        static readonly Color LightBar = new Color(0.06f, 0.01f, 0.1f);
        float _nextLight;

        void DimLight()
        {
            if (Time.unscaledTime < _nextLight || Gamepad.current == null) return;
            _nextLight = Time.unscaledTime + 5f;   // (again now and then: a controller that reconnects comes back bright)
#if UNITY_IOS && !UNITY_EDITOR
            SE_ControllerLight(LightBar.r, LightBar.g, LightBar.b);
#endif
        }
        /// The connected controller's battery, 0-100 (-1: none, or it doesn't say), and whether it's charging.
        public static int Battery(out bool charging)
        {
            charging = false;
#if UNITY_IOS && !UNITY_EDITOR
            int level = SE_ControllerBattery(out int c);
            charging = c != 0;
            return level;
#else
            return -1;
#endif
        }

        /// True while a full-screen menu (picker, settings) is open.
        public static bool MenuOpen => GamePicker.IsOpen || SettingsScreen.IsOpen || PlayerSelect.IsOpen || ControllerMenuNav.Active;

        static bool InDemo => Demo.PitDemoGame.IsRunning || Demo.TimeCrystalGame.IsRunning;

        static readonly RetroPadButton[] s_all =
        {
            RetroPadButton.B, RetroPadButton.A, RetroPadButton.Y, RetroPadButton.X, RetroPadButton.L, RetroPadButton.R,
            RetroPadButton.Start, RetroPadButton.Select,
            RetroPadButton.Up, RetroPadButton.Down, RetroPadButton.Left, RetroPadButton.Right,
        };

        void OnEnable() => InputSystem.onDeviceChange += OnDeviceChange;

        void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            ReleaseAll();
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad)) return;
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
            {
                _nextLight = 0f;   // (dimmed straight away)
                ControllerChanged?.Invoke(device.displayName);
            }
            else if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
                ControllerChanged?.Invoke(null);
        }

        // A controller iOS no longer has (a Kishi or Backbone the phone's just been taken out of) that Unity's input
        // still lists - Gamepad.current left set, so the screen's keys, the portraits and the rest stayed hidden: gone
        // from Unity's list too, checked twice a second.
        float _nextGoneCheck;

        static int NativeCount()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SE_ControllerCount();
#else
            return -1;
#endif
        }

        void DropGonePads()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (Gamepad.all.Count == 0 || Time.unscaledTime < _nextGoneCheck) return;
            _nextGoneCheck = Time.unscaledTime + 0.5f;
            if (SE_ControllerCount() > 0) return;
            foreach (var pad in Gamepad.all.ToArray())
            {
                Debug.Log($"[GamepadInput] {pad.displayName} gone from iOS: dropped");
                InputSystem.RemoveDevice(pad);
            }
            ControllerChanged?.Invoke(null);
#endif
        }

        // The controller running down: a warning on screen at 10% and again at 5% (a DualSense that low drops off
        // and won't stay connected), checked every few seconds; again after it's charged.
        float _nextBatteryCheck;
        int _warnedAt = 101;

        void WatchBattery()
        {
            if (Time.unscaledTime < _nextBatteryCheck) return;
            _nextBatteryCheck = Time.unscaledTime + 10f;
            if (Gamepad.current == null) return;
            int level = Battery(out bool charging);
            if (level < 0) return;
            if (charging || level > 15) { _warnedAt = 101; return; }
            int band = level <= 5 ? 5 : level <= 10 ? 10 : 101;
            if (band >= _warnedAt || band > 10) return;
            _warnedAt = band;
            var picker = FindAnyObjectByType<GamePicker>();
            if (picker) picker.ShowMessage($"Controller battery low ({level}%). Charge it soon - it may disconnect", error: true);
        }

        void Update()
        {
            DropGonePads();
            ApplyTouchControlsVisibility();
            WatchBattery();
            DimLight();

            var pad = Gamepad.current;
            if (pad != null) MenuButton(pad.startButton);
            if (pad == null || MenuOpen)
            {
                _waitRelease = true;
                ReleaseAll();
                return;
            }
            // (Out of a menu: the button that pressed its key doesn't reach the game - not till it's let go.)
            if (_waitRelease)
            {
                if (pad.buttonSouth.isPressed || pad.buttonEast.isPressed || pad.buttonWest.isPressed || pad.buttonNorth.isPressed || pad.startButton.isPressed)
                {
                    ReleaseAll();
                    return;
                }
                _waitRelease = false;
            }

            // ENDLESS KNIGHT (Wayne's): Cross jumps (its B), Square attacks (A), Circle is C - the heavy blow, held for
            // the spell wheel (the stick or d-pad picks); Triangle the Great Heal (START, below).
            bool knight = Demo.PitDemoGame.IsRunning;
            Set(RetroPadButton.B, knight ? pad.buttonWest : pad.buttonSouth);
            Set(RetroPadButton.A, knight ? pad.buttonSouth : pad.buttonEast);
            Set(RetroPadButton.Y, knight ? pad.buttonEast : pad.buttonWest);
            Set(RetroPadButton.X, pad.buttonNorth);
            // A game: L1 is COIN, R1 is START, the triggers buttons 5 and 6 (Options is the menu); a demo keeps L1 and R1.
            bool rom = CoinOnShoulders;
            Set(RetroPadButton.L, rom ? pad.leftTrigger : pad.leftShoulder);
            Set(RetroPadButton.R, rom ? pad.rightTrigger : pad.rightShoulder);
            // After a mid-game hold of Create/View: R1 joins as the next
            // player without restarting (the old player is out).
            if (Time.unscaledTime < _restartArmedUntil && pad.rightShoulder.wasPressedThisFrame && PlayerSlots.IsInGame(PlayerSlots.CurrentSlot) != true)
            {
                _restartArmedUntil = 0f;
                PlayerSlots.SetCurrentSlot(_armedNext);
                var picker = FindAnyObjectByType<GamePicker>();
                if (picker) picker.ShowMessage($"You're {PlayerSlots.Describe(_armedNext)}. Insert a coin to join");
                ArcadeInput.SetPressed(RetroPadButton.Start, false, 0, InputSource.Gamepad);
                return;
            }
            // START: in ENDLESS KNIGHT Triangle (the Great Heal), none in THE TIME CRYSTAL; in a game R1.
            if (InDemo) ArcadeInput.SetPressed(RetroPadButton.Start, Demo.PitDemoGame.IsRunning && pad.buttonNorth.isPressed, 3, InputSource.Gamepad);
            else ArcadeInput.SetPressed(RetroPadButton.Start, pad.rightShoulder.isPressed, 3, InputSource.Gamepad);
            // COIN: L1, or Create (held for the next player where the coin slot picks one).
            if (!pad.selectButton.isPressed && !pad.selectButton.wasReleasedThisFrame && (pad.leftShoulder.isPressed || pad.leftShoulder.wasReleasedThisFrame))
                ShoulderCoin(pad.leftShoulder);
            else UpdateCoin(pad.selectButton);

            Vector2 stick = pad.leftStick.ReadValue();
            ArcadeInput.SetPressed(RetroPadButton.Up, pad.dpad.up.isPressed || stick.y > stickDeadZone, 3, InputSource.Gamepad);
            ArcadeInput.SetPressed(RetroPadButton.Down, pad.dpad.down.isPressed || stick.y < -stickDeadZone, 3, InputSource.Gamepad);
            ArcadeInput.SetPressed(RetroPadButton.Left, pad.dpad.left.isPressed || stick.x < -stickDeadZone, 3, InputSource.Gamepad);
            ArcadeInput.SetPressed(RetroPadButton.Right, pad.dpad.right.isPressed || stick.x > stickDeadZone, 3, InputSource.Gamepad);
            bool dpadHeld = pad.dpad.up.isPressed || pad.dpad.down.isPressed || pad.dpad.left.isPressed || pad.dpad.right.isPressed;
            ArcadeInput.SetPush(dpadHeld ? 1f : stick.magnitude > stickDeadZone ? stick.magnitude : 0f, InputSource.Gamepad);
            Vector2 dpad = new Vector2((pad.dpad.right.isPressed ? 1f : 0f) - (pad.dpad.left.isPressed ? 1f : 0f), (pad.dpad.up.isPressed ? 1f : 0f) - (pad.dpad.down.isPressed ? 1f : 0f));
            ArcadeInput.SetStick(dpadHeld ? dpad.normalized : stick.magnitude > stickDeadZone ? stick : Vector2.zero, InputSource.Gamepad);
        }

        // Options: the screen's three-bar menu (FULL, GAMES, CAB... in any mode), opened or put away. The cabinet's
        // side menus, if they're open, fold away instead.
        void MenuButton(ButtonControl options)
        {
            bool windowUp = GamePicker.IsOpen || SettingsScreen.IsOpen || PlayerSelect.IsOpen || LeaderboardWindow.IsOpen || SavesWindow.IsOpen;
            if (!options.wasPressedThisFrame || windowUp) return;
            if (SaveStatePanel.SideMenusOpen) { ToggleSideMenus(); return; }
            MenuDropdown.ToggleFromController();
        }

        // A ROM game's running (not the demos).
        static bool RomRunning => !InDemo && Mobile.LibretroCore.IsRunning;

        /// The game running takes its coin on L1 and its start on R1: every arcade game (not the built-in demos).
        public static bool CoinOnShoulders => RomRunning;

        static void ToggleSideMenus()
        {
            SaveStatePanel.SideMenusOpen = !SaveStatePanel.SideMenusOpen;
            if (SaveStatePanel.SideMenusOpen) MenuPause.Open(MenuPause.Menu.Side); else MenuPause.Close(MenuPause.Menu.Side);
        }

        // L1 is Coin. Where the coin slot picks the player (PlayerSlots) it's as the screen's COIN: held, the player
        // chooser (PlayerSelect) - a tap, a coin as it's let go.
        bool _shoulderPending;
        float _shoulderDownAt;

        void ShoulderCoin(ButtonControl l1)
        {
            if (PlayerSlots.Current == null)
            {
                ArcadeInput.SetPressed(RetroPadButton.Select, l1.isPressed, 6, InputSource.Gamepad);
                return;
            }
            if (l1.wasPressedThisFrame) { _shoulderDownAt = Time.unscaledTime; _shoulderPending = true; }
            if (_shoulderPending && l1.isPressed && Time.unscaledTime - _shoulderDownAt >= playerHoldSeconds)
            {
                _shoulderPending = false;
                PlayerSelect.Open();
            }
            else if (_shoulderPending && l1.wasReleasedThisFrame)
            {
                _shoulderPending = false;
                ArcadeInput.SetPressed(RetroPadButton.Select, true, 6, InputSource.Gamepad);
                ArcadeInput.SetPressed(RetroPadButton.Select, false, 6, InputSource.Gamepad);
                PlayerSlots.NoteCoin();
            }
        }

        // Create/View is Coin. In games where the coin slot picks the player
        // (PlayerSlots), holding it steps to the next player instead, and a
        // short press inserts a coin when released.
        void UpdateCoin(ButtonControl select)
        {
            var players = PlayerSlots.Current;
            if (players == null)
            {
                ArcadeInput.SetPressed(RetroPadButton.Select, select.isPressed, 6, InputSource.Gamepad);
                return;
            }
            if (select.wasPressedThisFrame) { _coinDownAt = Time.unscaledTime; _coinPending = true; }
            if (_coinPending && select.isPressed && Time.unscaledTime - _coinDownAt >= playerHoldSeconds)
            {
                _coinPending = false;
                int next = (PlayerSlots.CurrentSlot + 1) % players.Length;
                var picker = FindAnyObjectByType<GamePicker>();
                bool? stillIn = PlayerSlots.IsInGame(PlayerSlots.CurrentSlot);
                if (stillIn == false || (stillIn == null && !PlayerSlots.GameInProgress))
                {
                    PlayerSlots.SetCurrentSlot(next);
                    if (picker) picker.ShowMessage($"You're {PlayerSlots.Describe(next)}. Hold {PadLabels.Select} to switch");
                }
                else if (Time.unscaledTime < _restartArmedUntil)
                {
                    // Second hold: switch and restart.
                    _restartArmedUntil = 0f;
                    PlayerSlots.RestartAs(next);
                    if (picker) picker.ShowMessage($"Restarted as {PlayerSlots.Describe(next)}. Insert a coin to start");
                }
                else
                {
                    _restartArmedUntil = Time.unscaledTime + restartConfirmSeconds;
                    _armedNext = next;
                    if (picker) picker.ShowMessage(stillIn == true
                        ? $"You're still playing. Hold {PadLabels.Select} again to restart as {PlayerSlots.Describe(next)}"
                        : $"{PlayerSlots.Describe(next)}: press {PadLabels.Start} to join (if you're out), or hold {PadLabels.Select} again to restart");
                }
            }
            if (select.wasReleasedThisFrame && _coinPending)
            {
                _coinPending = false;
                ArcadeInput.SetPressed(RetroPadButton.Select, true, 6, InputSource.Gamepad);
                ArcadeInput.SetPressed(RetroPadButton.Select, false, 6, InputSource.Gamepad);
                PlayerSlots.NoteCoin();
            }
        }

        static void Set(RetroPadButton button, ButtonControl control)
            => ArcadeInput.SetPressed(button, control != null && control.isPressed, 3, InputSource.Gamepad);

        static void ReleaseAll()
        {
            foreach (var button in s_all)
                ArcadeInput.SetPressed(button, false, 0, InputSource.Gamepad);
            ArcadeInput.SetPush(0f, InputSource.Gamepad);
            ArcadeInput.SetStick(Vector2.zero, InputSource.Gamepad);
        }

        readonly System.Collections.Generic.List<CanvasGroup> _hidden = new System.Collections.Generic.List<CanvasGroup>();

        // Everything on the controls' canvas faded out but COIN (or all back).
        void CoinOnly(bool on)
        {
            if (!on)
            {
                foreach (var g in _hidden) if (g) { g.alpha = 1f; g.blocksRaycasts = true; }
                _hidden.Clear();
                return;
            }
            if (_hidden.Count > 0 || !touchControls) return;
            var coin = Demo.TimeCrystalGame.CoinButton();
            if (!coin) return;
            Hide(touchControls.transform, coin.transform);
        }

        void Hide(Transform under, Transform keep)
        {
            foreach (Transform child in under)
            {
                if (child == keep) continue;
                if (keep.IsChildOf(child)) { Hide(child, keep); continue; }
                var g = child.GetComponent<CanvasGroup>();
                if (!g) g = child.gameObject.AddComponent<CanvasGroup>();
                if (g.alpha <= 0f) continue;   // (already out of sight: not ours to bring back)
                g.alpha = 0f; g.blocksRaycasts = false;
                _hidden.Add(g);
            }
        }

        void ApplyTouchControlsVisibility()
        {
            var mode = AppSettings.TouchControls;
            bool visible = mode == TouchControlsMode.Always || (mode == TouchControlsMode.Auto && Gamepad.current == null);
            visible &= !TimeCrystal.TcStory.Active;   // (off through THE TIME CRYSTAL's story: a tap skips it - TimeCrystalGame)
            visible &= !UI.TvOutput.Sideways;   // (in a controller the phone sits in - a Kishi, a Backbone: its own buttons, never the screen's)
            // THE TIME CRYSTAL's CONTINUE? with a controller: just its COIN, in the picture (Cross or Create puts it in).
            bool coinOnly = !visible && Demo.TimeCrystalGame.WantsCoin && Gamepad.current != null;
            CoinOnly(coinOnly);
            if (touchControls && touchControls.enabled != (visible || coinOnly))
            {
                touchControls.enabled = visible || coinOnly;
                Debug.Log($"[GamepadInput] touch keys {(touchControls.enabled ? "on" : "off")}: mode={mode} pad={(Gamepad.current != null ? Gamepad.current.displayName : "none")} pads={Gamepad.all.Count} ios={NativeCount()} handheld={UI.TvOutput.Handheld} story={TimeCrystal.TcStory.Active}");
            }
            bool takesTouches = (visible || coinOnly) && !MenuOpen;
            if (router && router.enabled != takesTouches) router.enabled = takesTouches;
        }
    }
}
