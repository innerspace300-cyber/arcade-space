using System.Collections.Generic;
using UnityEngine;

namespace TimeCrystal
{
    // The player's ship and everything that goes with it (its engine flames, hit diamond, power-up
    // bits, its laser's muzzle and flare) kept on the ship's own layer, TcPlayer - the picture's
    // last group (TimeCrystalGame): at the ships' depth, drawn over the HUD, so the pilot's
    // portrait and health bar never hide it in the bottom left. (What has a collider keeps its layer.)
    public class TcPlayerTop : MonoBehaviour
    {
        static readonly List<GameObject> s_extra = new List<GameObject>();
        static int s_layer = -1;
        static int Layer => s_layer >= 0 ? s_layer : (s_layer = LayerMask.NameToLayer("TcPlayer"));

        /// One more thing of the ship's that isn't under it (the laser's muzzle at its nose).
        public static void Add(GameObject go)
        {
            if (go && !s_extra.Contains(go)) s_extra.Add(go);
        }

        readonly List<Transform> _scratch = new List<Transform>();
        float _next;

        void LateUpdate()
        {
            // (A few times a second: effects come and go as it flies.)
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.1f;
            Keep(gameObject);
            s_extra.RemoveAll(go => !go);
            foreach (var go in s_extra) Keep(go);
        }

        void Keep(GameObject root)
        {
            int layer = Layer;
            if (layer < 0) return;
            root.GetComponentsInChildren(true, _scratch);
            foreach (var t in _scratch)
                if (t.gameObject.layer != layer && !t.GetComponent<Collider2D>()) t.gameObject.layer = layer;
        }
    }
}
