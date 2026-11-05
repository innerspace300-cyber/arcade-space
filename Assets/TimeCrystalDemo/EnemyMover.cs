using UnityEngine;

namespace TimeCrystal
{
    public class EnemyMover : MonoBehaviour
    {
        public float moveSpeed = 2f;

        void Update()
        {
            transform.Translate(Vector3.down * moveSpeed * Time.deltaTime);
        }
    }
}
