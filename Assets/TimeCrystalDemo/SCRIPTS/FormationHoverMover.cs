using UnityEngine;
using System.Collections;

namespace TimeCrystal
{
    public class FormationHoverMover : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 2f;
        public float hoverYPosition = 3f; // The Y height where enemy will hover
        public float hoverDuration = 1.5f; // How long enemy hovers before continuing

        private bool reachedHoverPosition = false;
        private float originalMoveSpeed;

        private void Start()
        {
            originalMoveSpeed = moveSpeed;
            StartCoroutine(HoverRoutine());
        }

        IEnumerator HoverRoutine()
        {
            // Phase 1: Move down until we reach hoverYPosition
            while (transform.position.y > TcWorld.Y(hoverYPosition))
            {
                transform.position += Vector3.down * moveSpeed * Time.deltaTime;
                yield return null;
            }

            // Freeze at hover point
            reachedHoverPosition = true;
            moveSpeed = 0f;

            // Hover pause
            yield return new WaitForSeconds(hoverDuration);

            // Resume movement
            moveSpeed = originalMoveSpeed;

            // Phase 2: Continue moving down off-screen
            while (true)
            {
                transform.position += Vector3.down * moveSpeed * Time.deltaTime;
                yield return null;
            }
        }
    }
}
