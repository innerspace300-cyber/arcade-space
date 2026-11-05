using UnityEngine;

namespace TimeCrystal
{
    public class EnemyScreenCleanup : MonoBehaviour
    {
        public float bottomLimit = -10f;

        void Update()
        {
            if (transform.position.y < TcWorld.Y(bottomLimit))
                Destroy(gameObject);
        }
    }
}
