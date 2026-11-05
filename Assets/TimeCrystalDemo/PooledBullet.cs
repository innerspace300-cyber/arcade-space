using UnityEngine;
using System;

namespace TimeCrystal
{
    public class PooledBullet : MonoBehaviour
    {
        private Rigidbody2D rb;
        private Transform player;
        private float maxDistance;
        private Action<GameObject> returnToPool;
        private Vector3 startPoint;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        public void Init(Vector2 velocity, Transform target, float despawnDistance, Action<GameObject> returnCallback)
        {
            rb.linearVelocity = velocity;
            player = target;
            maxDistance = despawnDistance;
            returnToPool = returnCallback;
            startPoint = transform.position;
        }

        void Update()
        {
            if (player == null)
            {
                returnToPool?.Invoke(gameObject);
                return;
            }

            if (Vector2.Distance(transform.position, player.position) > maxDistance)
            {
                returnToPool?.Invoke(gameObject);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("PLAYER"))
            {
                returnToPool?.Invoke(gameObject);
            }
        }
    }
}
