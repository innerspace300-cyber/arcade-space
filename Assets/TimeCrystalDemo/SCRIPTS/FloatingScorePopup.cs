using UnityEngine;
using TMPro;

namespace TimeCrystal
{
    public class FloatingScorePopup : MonoBehaviour
    {
        public float moveUpSpeed = 1f;
        public float fadeSpeed = 2f;
        public float bounceScale = 1.6f;
        public float bounceSpeed = 8f;

        private TextMeshPro textMesh;
        private Color startColor;
        private Vector3 initialScale;
        private float alpha = 1f;

        // Track which prefab this instance came from
        private GameObject sourcePrefab;

        private void Awake()
        {
            textMesh = GetComponent<TextMeshPro>();
            initialScale = transform.localScale;
        }

        public void Initialize(int score, GameObject prefabRef)
        {
            textMesh.text = "+" + score;
            alpha = 1f;
            startColor = textMesh.color;
            transform.localScale = initialScale * bounceScale;

            sourcePrefab = prefabRef;
        }

        private void Update()
        {
            transform.rotation = Quaternion.identity;   // ARcade: square on to the picture
            transform.localScale = Vector3.Lerp(transform.localScale, initialScale, Time.deltaTime * bounceSpeed);
            transform.position += Vector3.up * moveUpSpeed * Time.deltaTime;

            alpha -= fadeSpeed * Time.deltaTime;
            startColor.a = alpha;
            textMesh.color = startColor;

            if (alpha <= 0f)
            {
                FloatingScorePopupPool.Instance.ReturnPopup(sourcePrefab, this);
            }
        }
    }
}
