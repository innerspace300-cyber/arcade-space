using UnityEngine;

namespace TimeCrystal
{
    [System.Serializable]
    public class ParallaxLayer
    {
        public Transform layerTransform;
        public float scrollSpeed = 0.5f;
        public bool loop = true;

        [HideInInspector] public Vector3 startPosition;
        [HideInInspector] public float spriteWidth;
    }

    public class ParallaxScroller2D : MonoBehaviour
    {
        [Header("Parallax Layers (ordered back to front)")]
        public ParallaxLayer[] parallaxLayers;

        [Tooltip("Direction to scroll the layers (e.g., (0, 1) for up, (0, -1) for down)")]
        public Vector2 scrollDirection = Vector2.down;

        void Start()
        {
            foreach (var layer in parallaxLayers)
            {
                if (layer.layerTransform != null)
                {
                    layer.startPosition = layer.layerTransform.position;

                    // Assume full width based on attached sprite
                    SpriteRenderer sr = layer.layerTransform.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        layer.spriteWidth = sr.bounds.size.y; // Vertical scrolling (Y-axis)
                    }
                }
            }
        }

        void Update()
        {
            foreach (var layer in parallaxLayers)
            {
                if (layer.layerTransform == null) continue;

                Vector3 movement = (Vector3)(scrollDirection.normalized * layer.scrollSpeed * Time.deltaTime);
                layer.layerTransform.position += movement;

                if (layer.loop && layer.spriteWidth > 0)
                {
                    float offsetY = layer.layerTransform.position.y - layer.startPosition.y;
                    if (Mathf.Abs(offsetY) >= layer.spriteWidth)
                    {
                        layer.layerTransform.position = layer.startPosition;
                    }
                }
            }
        }
    }
}
