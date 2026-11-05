using UnityEngine;
using System.Collections.Generic;
using TMPro;

namespace TimeCrystal
{
    public class EnemyShooter2D : MonoBehaviour
    {
        [Header("Firing Settings")]
        public GameObject bulletPrefab;
        public Transform firePoint;
        public float fireRate = 1f;
        public float bulletSpeed = 5f;
        public float bulletLifetimeDistance = 20f;
        public int bulletPoolSize = 20;
        public AudioSource shootAudio;

        [Header("Health Settings")]
        public int maxHealth = 25;
        public GameObject deathVFX;

        [Header("Hit Effect")]
        public GameObject hitVFX;
        public Transform hitVFXSpawnPoint;

        [Header("Score Settings")]
        [Tooltip("How many points this enemy gives when killed")]
        public int scoreValue = 10;

        [Header("Score Popup Settings (Assign a popup prefab here)")]
        public GameObject popupPrefab;

        private int currentHealth;
        private float fireTimer;
        private Queue<GameObject> bulletPool;
        private Transform player;
        private SpriteRenderer spriteRenderer;

        void Start()
        {
            currentHealth = maxHealth;
            fireTimer = 0f;
            spriteRenderer = GetComponent<SpriteRenderer>();

            GameObject playerObj = GameObject.FindGameObjectWithTag("PLAYER");
            if (playerObj != null)
                player = playerObj.transform;

            // ARcade: the pool fills as the enemy fires (the jam build made
            // bulletPoolSize bullets per enemy up front, and kept them all).
            bulletPool = new Queue<GameObject>();
        }

        /// ARcade: off when TcStageDirector's patterns do the firing (TcEnemyGun).
        public bool AutoFire = true;

        void Update()
        {
            if (!AutoFire) return;
            if (player == null || firePoint == null)
                return;

            fireTimer += Time.deltaTime;

            if (fireTimer >= 1f / fireRate)
            {
                FireBullet();
                fireTimer = 0f;
            }
        }

        void FireBullet()
        {
            GameObject bullet = null;
            while (!bullet && bulletPool.Count > 0) bullet = bulletPool.Dequeue();   // (one that left the picture is gone)
            if (!bullet)
            {
                bullet = TcWorld.Spawn(bulletPrefab);
                bullet.AddComponent<TcOffField>();
            }
            bullet.transform.position = firePoint.position;
            bullet.transform.rotation = firePoint.rotation;
            bullet.SetActive(true);

            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            rb.linearVelocity = firePoint.up * -bulletSpeed;

            StartCoroutine(DisableBulletAfterDistance(bullet, bulletLifetimeDistance));

            TcWorld.PlayShot(shootAudio);   // (ARcade: one sound for every enemy firing at once)
        }

        System.Collections.IEnumerator DisableBulletAfterDistance(GameObject bullet, float maxDistance)
        {
            Vector3 startPosition = bullet.transform.position;

            while (bullet && bullet.activeSelf && Vector3.Distance(startPosition, bullet.transform.position) < maxDistance)
            {
                yield return null;
            }

            if (!bullet) yield break;
            bullet.SetActive(false);
            bulletPool.Enqueue(bullet);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            // Enemy ONLY takes damage from player bullets
            if (other.CompareTag("BULLET"))
            {
                if (currentHealth <= 0) return;   // ARcade: already going (scored once)
                currentHealth--;
                if (TcStageDirector.Instance) TcStageDirector.Instance.PlayHit();   // ARcade

                // Hit VFX
                if (hitVFX != null && hitVFXSpawnPoint != null)
                    TcWorld.SpawnEffect(hitVFX, hitVFXSpawnPoint.position, Quaternion.identity);

                // Check for death
                if (currentHealth <= 0)
                    Die();

                // The player's shot is spent (ARcade: gone - the jam build only hid it).
                if (other.GetComponent<TcShot>()) other.gameObject.SetActive(false);   // (pooled)
                else Destroy(other.gameObject);
            }
        }

        /// ARcade: destroyed by a bomb (TcBombs) - scored, its gem dropped, like being shot down.
        public void Kill()
        {
            if (currentHealth > 0) { currentHealth = 0; Die(); }
        }

        void Die()
        {
            if (TcStageDirector.Instance) TcStageDirector.Instance.PlayDestroyed();   // ARcade
            // Death VFX
            if (deathVFX != null)
            {
                // ARcade: its "death effect" is a gem dropped where it was (a
                // pickup, not a timed effect): it drifts down like the others.
                if (deathVFX.GetComponent<CollectableItem>())
                {
                    var gem = TcWorld.Spawn(deathVFX, transform.position, Quaternion.identity);
                    gem.AddComponent<CollectibleMover>();
                    gem.AddComponent<TcOffField>();
                }
                else TcWorld.SpawnEffect(deathVFX, transform.position, Quaternion.identity);
            }

            // Score Update
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.AddScore(scoreValue);

            // Spawn Score Popup (from pooled system)
            if (popupPrefab != null)
            {
                FloatingScorePopup popup = FloatingScorePopupPool.Instance.GetPopup(popupPrefab);
                popup.transform.position = transform.position;
                popup.Initialize(scoreValue, popupPrefab);
            }

            // Now and then a bomb pickup, too.
            if (TcBombs.Instance) TcBombs.Instance.MaybeDrop(transform.position);

            // ARcade: gone (the jam build only hid it, so they piled up).
            Destroy(gameObject);
        }

        // Its spare bullets go with it (any still flying go as they leave the picture).
        void OnDestroy()
        {
            if (bulletPool == null) return;
            foreach (var bullet in bulletPool) if (bullet) Destroy(bullet);
        }
    }
}
