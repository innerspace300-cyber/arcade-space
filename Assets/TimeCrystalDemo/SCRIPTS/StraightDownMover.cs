using UnityEngine;

namespace TimeCrystal
{
    public class StraightDownMover : MonoBehaviour
    {
        public float speed = 2f;

        void Update()
        {
            transform.Translate(Vector3.down * speed * Time.deltaTime);
        }
    }
}
