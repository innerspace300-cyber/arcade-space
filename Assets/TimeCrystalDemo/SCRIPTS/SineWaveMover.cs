using UnityEngine;

namespace TimeCrystal
{
    public class SineWaveMover : MonoBehaviour
    {
        public float verticalSpeed = 2f;
        public float horizontalAmplitude = 1f;
        public float horizontalFrequency = 2f;

        private float startX;

        void Start()
        {
            startX = transform.position.x;
        }

        void Update()
        {
            float x = startX + Mathf.Sin(Time.time * horizontalFrequency) * horizontalAmplitude;
            transform.position += Vector3.down * verticalSpeed * Time.deltaTime;
            transform.position = new Vector3(x, transform.position.y, transform.position.z);
        }
    }
}
