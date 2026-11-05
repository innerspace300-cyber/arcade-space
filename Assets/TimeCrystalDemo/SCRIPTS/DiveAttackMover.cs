using UnityEngine;

namespace TimeCrystal
{
    public class DiveAttackMover : MonoBehaviour
    {
        public float moveSpeed = 2f;
        public float diveSpeed = 6f;
        public float diveInterval = 3f;

        private Transform player;
        private float diveTimer;

        void Start()
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        void Update()
        {
            diveTimer += Time.deltaTime;

            if (player != null && diveTimer >= diveInterval)
            {
                Vector3 target = new Vector3(player.position.x, transform.position.y, transform.position.z);
                transform.position = Vector3.MoveTowards(transform.position, target, diveSpeed * Time.deltaTime);
            
                if (Vector3.Distance(transform.position, target) < 0.2f)
                    diveTimer = 0;
            }
            else
            {
                transform.Translate(Vector3.down * moveSpeed * Time.deltaTime);
            }
        }
    }
}
