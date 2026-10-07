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
//   L1 / R1        -> Buttons 5 / 6 (Street Fighter II's kicks)
//   Options / Menu -> Start, Create / View -> Coin (held: next player, in
//   games where the coin slot picks the player - PlayerSlots; mid-game the
//   hold asks: Options joins as that player, a second hold restarts as them)
//   D-pad or left stick -> joystick

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

        bool _coinPending;
        float _coinDownAt;
        float _restartArmedUntil;
        int _armedNext;

        /// Fired when a controller connects (its name) or disconnects (null).
        public static event Action<string> ControllerChanged;

        /// The connected controller's name, or null.
        public static string ControllerName => Gamepad.current?.displayName;

        /// True while a full-screen menu (picker, settings) is open.
        public static bool MenuOpen => GamePicker.IsOpen || SettingsScreen.IsOpen || PlayerSelect.IsOpen;

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
                ControllerChanged?.Invoke(device.displayName);
            else if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
                ControllerChanged?.Invoke(null);
        }

        void Update()
        {
            ApplyTouchControlsVisibility();

            var pad = Gamepad.current;
            if (pad == null || MenuOpen)
            {
                ReleaseAll();
                return;
            }

            Set(RetroPadButton.B, pad.buttonSouth);
            Set(RetroPadButton.A, pad.buttonEast);
            Set(RetroPadButton.Y, pad.buttonWest);
            Set(RetroPadButton.X, pad.buttonNorth);
            Set(RetroPadButton.L, pad.leftShoulder);
            Set(RetroPadButton.R, pad.rightShoulder);
            // After a mid-game hold of Create/View: Options joins as the next
            // player without restarting (the old player is out).
            if (Time.unscaledTime < _restartArmedUntil && pad.startButton.wasPressedThisFrame && PlayerSlots.IsInGame(PlayerSlots.CurrentSlot) != true)
            {
                _restartArmedUntil = 0f;
                PlayerSlots.SetCurrentSlot(_armedNext);
                var picker = FindAnyObjectByType<GamePicker>();
                if (picker) picker.ShowMessage($"You're {PlayerSlots.Describe(_armedNext)}. Insert a coin to join");
                ArcadeInput.SetPressed(RetroPadButton.Start, false, 0, InputSource.Gamepad);
                return;
            }
            Set(RetroPadButton.Start, pad.startButton);
            UpdateCoin(pad.selectButton);

            Vector2 stick = pad.leftStick.ReadValue();
            ArcadeInput.SetPressed(RetroPadButton.Up, pad.dpad.up.isPressed || stick.y > stickDeadZone, 3, InputSource.Gamepad);
            ArcadeInput.SetPressed(RetroPadButton.Down, pad.dpad.down.isPressed || stick.y < -stickDeadZone, 3, InputSource.Gamepad);
            ArcadeInput.SetPressed(RetroPadButton.Left, pad.dpad.left.isPressed || stick.x < -stickDeadZone, 3, InputSource.Gamepad);
            ArcadeInput.SetPressed(RetroPadButton.Right, pad.dpad.right.isPressed || stick.x > stickDeadZone, 3, InputSource.Gamepad);
            bool dpadHeld = pad.dpad.up.isPressed || pad.dpad.down.isPressed || pad.dpad.left.isPressed || pad.dpad.right.isPressed;
            ArcadeInput.SetPush(dpadHeld ? 1f : stick.magnitude > stickDeadZone ? stick.magnitude : 0f, InputSource.Gamepad);
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
                    if (picker) picker.ShowMessage($"You're {PlayerSlots.Describe(next)}. Hold Create/View to switch");
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
                        ? $"You're still playing. Hold Create/View again to restart as {PlayerSlots.Describe(next)}"
                        : $"{PlayerSlots.Describe(next)}: press Options to join (if you're out), or hold Create/View again to restart");
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
            => ArcadeInput.SetPressed(button, control.isPressed, 3, InputSource.Gamepad);

        static void ReleaseAll()
        {
            foreach (var button in s_all)
                ArcadeInput.SetPressed(button, false, 0, InputSource.Gamepad);
            ArcadeInput.SetPush(0f, InputSource.Gamepad);
        }

        void ApplyTouchControlsVisibility()
        {
            var mode = AppSettings.TouchControls;
            bool visible = mode == TouchControlsMode.Always || (mode == TouchControlsMode.Auto && Gamepad.current == null);
            if (touchControls && touchControls.enabled != visible) touchControls.enabled = visible;
            bool takesTouches = visible && !MenuOpen;
            if (router && router.enabled != takesTouches) router.enabled = takesTouches;
        }
    }
}
