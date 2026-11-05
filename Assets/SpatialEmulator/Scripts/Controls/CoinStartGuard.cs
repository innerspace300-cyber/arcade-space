// CoinStartGuard.cs — in an arcade game, once START's been pressed (on screen or a
// controller's R1), COIN and START fade down to a ghost and stop taking presses, so a
// stray thumb can't put a coin in or start a second player mid-game. Touching either
// ghost only wakes them (no coin, no start: ArcadeButton asks Asleep); awake they work
// as ever for AwakeSeconds after the last press, then fade down again - at a continue
// countdown it's tap to wake, COIN, START. A new game, or none, puts them back as they
// were. (Nothing tells the app when a game's over - an emulated game is just a picture -
// so they wake by touch, not by the game.) Not the built-in demos. Made at run time.

using UnityEngine;

namespace SpatialEmulator.Controls
{
    public class CoinStartGuard : MonoBehaviour
    {
        const float Ghost = 0.15f, FadeSpeed = 4f;   // (alpha asleep; alpha per second)
        const float AwakeSeconds = 4f;

        static CoinStartGuard s_instance;

        ArcadeButton _coin, _start;
        CanvasGroup _coinGroup, _startGroup;
        string _game;
        bool _started;         // START's been pressed in this game
        bool _startWas;
        float _awakeUntil;

        /// COIN or START, faded down: a touch only wakes them (Wake), it doesn't press.
        public static bool Asleep(RetroPadButton button) =>
            (button == RetroPadButton.Select || button == RetroPadButton.Start) && s_instance && s_instance.Sleeping;

        /// A touch on a ghost: both back up, live for AwakeSeconds.
        public static void Wake() { if (s_instance) s_instance._awakeUntil = Time.unscaledTime + AwakeSeconds; }

        bool Guarding => Mobile.LibretroCore.IsRunning && !Demo.LayeredDemo.AnyRunning;
        bool Sleeping => Guarding && _started && Time.unscaledTime >= _awakeUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (s_instance) return;
            var go = new GameObject("Coin Start Guard");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<CoinStartGuard>();
        }

        void Update()
        {
            if (!_coin || !_start) Find();
            // A new game, or none: back as they were.
            string game = Guarding ? Games.GameSelection.Current : null;
            if (game != _game) { _game = game; _started = false; _awakeUntil = 0f; }
            if (Guarding)
            {
                // START pressed (on screen or a controller): from now on they sleep between presses.
                bool start = ArcadeInput.IsPressedRaw((uint)RetroPadButton.Start);
                if (start && !_startWas) _started = true;
                _startWas = start;
                // Any press while awake keeps them awake a while longer.
                if (!Sleeping && ((_coin && _coin.isHeld) || (_start && _start.isHeld) || start || ArcadeInput.IsPressedRaw((uint)RetroPadButton.Select)))
                    _awakeUntil = Time.unscaledTime + AwakeSeconds;
            }
            float target = Sleeping ? Ghost : 1f;
            Fade(ref _coinGroup, _coin, target);
            Fade(ref _startGroup, _start, target);
        }

        static void Fade(ref CanvasGroup group, ArcadeButton key, float target)
        {
            if (!key) return;
            if (!group)
            {
                if (target >= 1f) return;   // (nothing to fade yet)
                group = key.GetComponent<CanvasGroup>();
                if (!group) group = key.gameObject.AddComponent<CanvasGroup>();
            }
            if (group.alpha != target) group.alpha = Mathf.MoveTowards(group.alpha, target, FadeSpeed * Time.unscaledDeltaTime);
        }

        void Find()
        {
            foreach (var b in FindObjectsByType<ArcadeButton>(FindObjectsInactive.Include))
            {
                if (b.button == RetroPadButton.Select && !_coin) _coin = b;
                else if (b.button == RetroPadButton.Start && !_start) _start = b;
            }
        }
    }
}
