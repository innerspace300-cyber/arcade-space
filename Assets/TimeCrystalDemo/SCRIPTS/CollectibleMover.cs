using UnityEngine;

namespace TimeCrystal
{
    public class CollectibleMover : MonoBehaviour
    {
        public float moveSpeed = 1.5f;

        void Update()
        {
            transform.Translate(Vector3.down * moveSpeed * Time.deltaTime);
        }
    }
}
