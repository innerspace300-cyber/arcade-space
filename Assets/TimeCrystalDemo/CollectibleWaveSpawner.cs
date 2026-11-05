using UnityEngine;
using System.Collections;

namespace TimeCrystal
{
    public class CollectibleWaveSpawner : MonoBehaviour
    {
        [Header("Spawn Points")]
        [Tooltip("All possible spawn positions for collectibles.")]
        public Transform[] spawnPoints;

        [Header("Collectible Types")]
        [Tooltip("Crystals that give score only.")]
        public GameObject[] crystalPrefabs;

        [Tooltip("PowerUps that provide weapon upgrades, health, etc.")]
        public GameObject[] powerUpPrefabs;

        [Header("Spawn Behavior")]
        [Tooltip("How many items spawn each wave.")]
        public int collectiblesPerWave = 3;

        [Tooltip("Seconds between each item in a wave.")]
        public float timeBetweenSpawns = 0.6f;

        [Tooltip("Seconds between waves of collectibles.")]
        public float timeBetweenWaves = 5f;

        [Tooltip("If true, spawns forever.")]
        public bool loopWaves = true;

        [Header("Spawn Ratios")]
        [Range(0f, 1f)]
        [Tooltip("Chance (0–1) that a spawned item will be a powerup instead of a crystal.")]
        public float powerUpChance = 0.15f; // 15% chance of power-up by default

        [Header("Movement")]
        [Tooltip("Speed that collectibles move downward.")]
        public float moveSpeed = 1.5f;

        private bool spawning = false;

        private void Start()
        {
            StartCoroutine(SpawnSequence());
        }

        private IEnumerator SpawnSequence()
        {
            spawning = true;

            while (spawning)
            {
                // Shuffle spawn points so no overlap
                Transform[] order = (Transform[])spawnPoints.Clone();
                Shuffle(order);

                for (int i = 0; i < collectiblesPerWave; i++)
                {
                    Transform point = order[i % order.Length];
                    SpawnCollectible(point);
                    yield return new WaitForSeconds(timeBetweenSpawns);
                }

                yield return new WaitForSeconds(timeBetweenWaves);

                if (!loopWaves)
                    spawning = false;
            }
        }

        void SpawnCollectible(Transform spawnPoint)
        {
            bool spawnPowerUp = Random.value <= powerUpChance;

            GameObject[] pool = spawnPowerUp ? powerUpPrefabs : crystalPrefabs;

            if (pool.Length == 0)
            {
                Debug.LogWarning("CollectibleWaveSpawner: No prefabs assigned for selected type.");
                return;
            }

            GameObject prefab = pool[Random.Range(0, pool.Length)];
            GameObject obj = TcWorld.Spawn(prefab, spawnPoint.position, Quaternion.identity);
            obj.AddComponent<TcOffField>();

            // Add downward motion
            var mover = obj.AddComponent<CollectibleMover>();
            mover.moveSpeed = moveSpeed;
        }

        void Shuffle(Transform[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                Transform temp = arr[i];
                int r = Random.Range(i, arr.Length);
                arr[i] = arr[r];
                arr[r] = temp;
            }
        }
    }
}
