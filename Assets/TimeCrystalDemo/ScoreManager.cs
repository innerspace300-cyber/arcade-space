using UnityEngine;
using TMPro;
using System.Collections;

namespace TimeCrystal
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance;

        [Header("UI")]
        public TextMeshProUGUI scoreText;

        [Header("Animation Settings")]
        public float animationDuration = 0.5f; // how long it takes to animate the numbers
        public float animationSpeed = 40f; // how quickly digits "scroll"

        private int currentScore = 0;

        /// ARcade: the score so far (for the hi score).
        public int Score => currentScore;
        private Coroutine animateCoroutine;

        private void Awake()
        {
            // Make this accessible globally
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        public void AddScore(int amount)
        {
            int oldScore = currentScore;
            currentScore += amount;

            if (animateCoroutine != null)
                StopCoroutine(animateCoroutine);

            animateCoroutine = StartCoroutine(AnimateScore(oldScore, currentScore));
        }

        private IEnumerator AnimateScore(int oldValue, int newValue)
        {
            float timer = 0f;
            while (timer < animationDuration)
            {
                timer += Time.deltaTime;
                float t = timer / animationDuration;

                // Slot machine style interpolation
                int displayedScore = Mathf.FloorToInt(Mathf.Lerp(oldValue, newValue, t));

                // Slight jitter scrolling effect
                displayedScore += Mathf.FloorToInt(Mathf.Sin(timer * animationSpeed));

                // Clamp to valid value
                displayedScore = Mathf.Clamp(displayedScore, oldValue, newValue);

                scoreText.text = displayedScore.ToString("D7");   // ARcade: seven digits, like a counter
                yield return null;
            }

            // Ensure final correct score is shown
            scoreText.text = newValue.ToString("D7");
        }
    }
}
