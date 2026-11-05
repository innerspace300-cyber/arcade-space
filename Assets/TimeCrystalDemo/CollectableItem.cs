using UnityEngine;

namespace TimeCrystal
{
    public class CollectableItem : MonoBehaviour
    {
        [Header("Score Settings")]
        public int scoreValue = 10;

        [Header("Popup Settings (Assign Different Popup Prefabs Here)")]
        public GameObject popupPrefab;

        /// Where its points pop up, from where it's taken (TcGemPull: in front of the ship).
        [System.NonSerialized] public Vector2 popupOffset;

        [Header("Visual Effects")]
        public GameObject vfxPrefab;
        public float vfxDestroyDelay = 1f;

        private SpriteRenderer spriteRenderer;
        private Collider2D col2D;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            col2D = GetComponent<Collider2D>();

            if (col2D != null)
                col2D.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Only trigger if the PLAYER touches this collectable
            if (!other.CompareTag("PLAYER"))
                return;
            Collect();
        }

        bool _taken;
        public bool Taken => _taken;

        /// ARcade: taken (scored, its popup and pickup effect) - by the player
        /// touching it, or pulled into the ship (TcGemPull).
        public void Collect()
        {
            if (_taken) return;
            _taken = true;

            // Add score
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.AddScore(scoreValue);

            // Spawn SCORE POPUP (POOLED, supports different prefabs)
            if (popupPrefab != null)
            {
                FloatingScorePopup popup = FloatingScorePopupPool.Instance.GetPopup(popupPrefab);
                popup.transform.position = transform.position + (Vector3)popupOffset;
                popup.Initialize(scoreValue, popupPrefab);
            }

            // Optional VFX on pickup
            if (vfxPrefab != null)
            {
                TcWorld.SpawnEffect(vfxPrefab, transform.position, Quaternion.identity, vfxDestroyDelay);
            }

            // Disable visuals and collision immediately
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            if (col2D != null) col2D.enabled = false;

            // Destroy object
            Destroy(gameObject, 0.05f);
        }
    }
}
