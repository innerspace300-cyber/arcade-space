using UnityEngine;
using System.Collections;

namespace TimeCrystal
{
    public class EnemyWaveSpawner : MonoBehaviour
    {
        [Header("Spawn Points")]
        public Transform[] spawnPoints;

        [Header("Difficulty Enemy Groups")]
        public GameObject[] easyEnemies;
        public GameObject[] mediumEnemies;
        public GameObject[] hardEnemies;
        public GameObject[] bossEnemies;

        [Header("Wave Settings")]
        public bool loopWaves = true;
        public int enemiesPerWave = 5;
        public float timeBetweenEnemySpawns = 0.5f;
        public float timeBetweenWaves = 3f;

        [Header("Movement Speeds")]
        public float baseMoveSpeed = 2f;

        private bool spawning = false;
        private int currentWave = 0;

        private void Start()
        {
            StartCoroutine(WaveSequence());
        }

        private IEnumerator WaveSequence()
        {
            spawning = true;

            while (spawning)
            {
                currentWave++;

                // Difficulty tier
                GameObject[] enemySet = GetEnemySetForWave(currentWave);

                // Random movement type
                MovementStyle waveMoveStyle = (MovementStyle)Random.Range(0, 4);

                // Random formation pattern
                FormationPattern wavePattern = (FormationPattern)Random.Range(0, 4);

                if (Debug.isDebugBuild) Debug.Log($"Wave {currentWave} | Move: {waveMoveStyle} | Formation: {wavePattern}");

                // Build ordered spawn sequence based on formation pattern
                Transform[] waveSpawnOrder = GetFormationSpawnOrder(wavePattern);

                // Spawn using formation ordering
                for (int i = 0; i < enemiesPerWave; i++)
                {
                    Transform spawnPoint = waveSpawnOrder[i % waveSpawnOrder.Length];
                    SpawnEnemyAtPoint(enemySet, waveMoveStyle, spawnPoint);
                    yield return new WaitForSeconds(timeBetweenEnemySpawns);
                }

                yield return new WaitForSeconds(timeBetweenWaves);

                if (!loopWaves)
                    spawning = false;
            }
        }

        // Difficulty selection logic
        GameObject[] GetEnemySetForWave(int wave)
        {
            if (wave % 10 == 0 && bossEnemies.Length > 0) return bossEnemies;
            if (wave < 4 && easyEnemies.Length > 0) return easyEnemies;
            if (wave < 8 && mediumEnemies.Length > 0) return mediumEnemies;
            if (hardEnemies.Length > 0) return hardEnemies;
            return easyEnemies;
        }

        // Main spawn method
        void SpawnEnemyAtPoint(GameObject[] pool, MovementStyle style, Transform spawnPoint)
        {
            if (pool.Length == 0) return;

            GameObject prefab = pool[Random.Range(0, pool.Length)];
            GameObject enemy = TcWorld.Spawn(prefab, spawnPoint.position, Quaternion.identity);

            AddMovement(style, enemy);
            enemy.AddComponent<EnemyScreenCleanup>();
        }

        // Movement assignment
        void AddMovement(MovementStyle style, GameObject enemy)
        {
            switch (style)
            {
                case MovementStyle.StraightDown:
                    var straight = enemy.AddComponent<StraightDownMover>();
                    straight.speed = baseMoveSpeed;
                    break;

                case MovementStyle.SineWave:
                    var sine = enemy.AddComponent<SineWaveMover>();
                    sine.verticalSpeed = baseMoveSpeed;
                    break;

                case MovementStyle.FormationHover:
                    var hover = enemy.AddComponent<FormationHoverMover>();
                    hover.moveSpeed = baseMoveSpeed;
                    break;

                case MovementStyle.DiveAttack:
                    var dive = enemy.AddComponent<DiveAttackMover>();
                    dive.moveSpeed = baseMoveSpeed;
                    break;
            }
        }

        // NEW — Formation logic
        Transform[] GetFormationSpawnOrder(FormationPattern pattern)
        {
            Transform[] points = (Transform[])spawnPoints.Clone();

            switch (pattern)
            {
                case FormationPattern.LineAcross:
                    // Already naturally ordered in inspector order
                    return points;

                case FormationPattern.VFormation:
                    System.Array.Sort(points, (a, b) => a.position.x.CompareTo(b.position.x));
                    return points; // Left-to-right creates "V" when moving downward

                case FormationPattern.ReverseVFormation:
                    System.Array.Sort(points, (a, b) => b.position.x.CompareTo(a.position.x));
                    return points;

                case FormationPattern.WaveArc:
                    // Sine-curved ordering across X
                    System.Array.Sort(points, (a, b) => Mathf.Sin(a.position.x).CompareTo(Mathf.Sin(b.position.x)));
                    return points;
            }

            return points;
        }
    }
}
