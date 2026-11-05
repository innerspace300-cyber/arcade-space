using UnityEngine;

namespace TimeCrystal
{
    // Warp speed, retro style: seven copies of the player's ship under it (solid silhouettes,
    // Resources/TcFlash.shader, in the pilot's hit-diamond colour), each a little further behind
    // and a little later on its path - so they stream after it as it moves - flickering on and off
    // out of step, fainter down the line. In and out with the warp (TcSpaceScroller.Warp01).
    public class TcWarpTrail : MonoBehaviour
    {
        const int Copies = 7;
        const float Spacing = 0.3f;       // (world units behind - down the picture - per copy, at full warp)
        const float Lag = 0.035f;         // (seconds later on the ship's path, per copy)
        const float FlickerRate = 18f;    // (flashes a second)
        const int History = 64;

        SpriteRenderer _body;
        readonly SpriteRenderer[] _copies = new SpriteRenderer[Copies];
        readonly Vector3[] _path = new Vector3[History];
        readonly float[] _times = new float[History];
        int _head;

        void Start()
        {
            _body = GetComponent<SpriteRenderer>();
            for (int i = 0; i < History; i++) { _path[i] = transform.position; _times[i] = Time.time; }
            var material = TcDamageFlash.WhiteMaterial;
            for (int i = 0; i < Copies; i++)
            {
                var go = new GameObject("WARP TRAIL " + (i + 1)) { layer = gameObject.layer };
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                if (material) r.sharedMaterial = material;
                r.sortingLayerID = _body ? _body.sortingLayerID : 0;
                r.sortingOrder = (_body ? _body.sortingOrder : 20) - 1 - i;   // (under the ship, each under the one before)
                r.enabled = false;
                _copies[i] = r;
            }
        }

        void LateUpdate()
        {
            // Where it's been (its own position, wobble and all), newest first.
            _head = (_head + 1) % History;
            _path[_head] = transform.position;
            _times[_head] = Time.time;

            float warp = TcSpaceScroller.Warp01;
            bool on = warp > 0.02f && _body && _body.enabled && _body.sprite;
            var color = TcPilots.Current.Color;
            for (int i = 0; i < Copies; i++)
            {
                var r = _copies[i];
                if (!r) continue;
                if (!on) { r.enabled = false; continue; }
                int n = i + 1;
                // Flickering, out of step with its neighbours; fainter the further back.
                bool lit = Mathf.Repeat(Time.time * FlickerRate + n * 0.37f, 1f) < 0.6f;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(warp * 2.5f));
                float alpha = Mathf.Lerp(0.85f, 0.2f, i / (Copies - 1f)) * k;
                r.enabled = lit && alpha > 0.01f;
                if (!r.enabled) continue;
                r.sprite = _body.sprite;
                r.color = new Color(color.r, color.g, color.b, alpha);
                r.transform.localScale = new Vector3(_body.flipX ? -1f : 1f, _body.flipY ? -1f : 1f, 1f);
                r.transform.rotation = transform.rotation;
                r.transform.position = PathAt(Time.time - n * Lag) + Vector3.down * (n * Spacing * k) + new Vector3(0f, 0f, 0.001f * n);
            }
        }

        // The ship's position `time` ago (the nearest frame at or before it).
        Vector3 PathAt(float time)
        {
            for (int s = 0; s < History; s++)
            {
                int j = (_head - s + History) % History;
                if (_times[j] <= time) return _path[j];
            }
            return _path[(_head + 1) % History];
        }

        void OnDisable()
        {
            foreach (var r in _copies) if (r) r.enabled = false;
        }
    }
}
