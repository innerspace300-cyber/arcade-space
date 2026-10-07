// CoinInsertAnimation.cs — the Coin button's press feedback: the coin turns
// edge-on and drops into the slot beneath it (a RectMask2D whose bottom edge
// is the slot line clips it away), then a fresh coin pops back in.

using UnityEngine;

namespace SpatialEmulator.Controls
{
    public class CoinInsertAnimation : MonoBehaviour
    {
        public ArcadeButton button;
        [Tooltip("The coin art, inside a RectMask2D that ends at the slot.")]
        public RectTransform coin;
        public CanvasGroup coinGroup;
        public float insertSeconds = 0.22f;
        public float pauseSeconds = 0.12f;
        public float returnSeconds = 0.18f;
        [Tooltip("How far the coin drops, in coin heights; enough to clear the slot line.")]
        public float dropHeights = 1.2f;
        [Range(0.05f, 1f)] public float edgeOnWidth = 0.3f;

        Vector2 _home;
        int _presses;
        float _time = -1f;

        void Awake()
        {
            if (coin) _home = coin.anchoredPosition;
        }

        void OnDisable()
        {
            _time = -1f;
            Pose(Vector2.zero, Vector3.one, 1f);
        }

        void Update()
        {
            if (!button || !coin) return;
            // Plays when a coin goes in (not on a hold that opens the player chooser).
            int presses = button.pressCount;
            if (presses != _presses) _time = 0f;
            _presses = presses;
            if (_time < 0f) return;

            _time += Time.unscaledDeltaTime;
            float height = coin.rect.height;
            if (_time < insertSeconds)
            {
                float k = _time / insertSeconds;
                float turn = Mathf.Clamp01(k * 2f); // edge-on in the first half, then falls
                Pose(new Vector2(0f, -dropHeights * height * k * k), new Vector3(Mathf.Lerp(1f, edgeOnWidth, turn), 1f, 1f), 1f);
            }
            else if (_time < insertSeconds + pauseSeconds)
            {
                Pose(Vector2.zero, Vector3.one, 0f);
            }
            else if (_time < insertSeconds + pauseSeconds + returnSeconds)
            {
                float k = (_time - insertSeconds - pauseSeconds) / returnSeconds;
                float pop = 1f + 0.12f * Mathf.Sin(k * Mathf.PI); // slight overshoot
                Pose(Vector2.zero, Vector3.one * Mathf.Lerp(0.6f, 1f, k) * pop, k);
            }
            else
            {
                _time = -1f;
                Pose(Vector2.zero, Vector3.one, 1f);
            }
        }

        void Pose(Vector2 offset, Vector3 scale, float alpha)
        {
            if (!coin) return;
            coin.anchoredPosition = _home + offset;
            coin.localScale = scale;
            if (coinGroup) coinGroup.alpha = alpha;
        }
    }
}
