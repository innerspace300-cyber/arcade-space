using System.Collections.Generic;
using UnityEngine;

namespace TimeCrystal
{
    public class FloatingScorePopupPool : MonoBehaviour
    {
        public static FloatingScorePopupPool Instance;

        // Pools are grouped by prefab
        private Dictionary<GameObject, Queue<FloatingScorePopup>> pools = new Dictionary<GameObject, Queue<FloatingScorePopup>>();

        private void Awake()
        {
            Instance = this;
        }

        public FloatingScorePopup GetPopup(GameObject popupPrefab)
        {
            if (!pools.ContainsKey(popupPrefab))
                pools[popupPrefab] = new Queue<FloatingScorePopup>();

            // If pool has none available, create one
            if (pools[popupPrefab].Count == 0)
            {
                GameObject obj = Instantiate(popupPrefab, transform);
                obj.SetActive(false);
                pools[popupPrefab].Enqueue(obj.GetComponent<FloatingScorePopup>());
            }

            // Activate one
            FloatingScorePopup popup = pools[popupPrefab].Dequeue();
            popup.gameObject.SetActive(true);
            return popup;
        }

        public void ReturnPopup(GameObject popupPrefab, FloatingScorePopup popup)
        {
            popup.gameObject.SetActive(false);
            pools[popupPrefab].Enqueue(popup);
        }
    }
}
