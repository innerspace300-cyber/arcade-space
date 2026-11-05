using UnityEngine;

namespace TimeCrystal
{
    // ARcade: the space background as six layers (ScifiSpaceAssets'
    // PremadeParallax3 bg1-6: deep space, nebula, planets, stars, small and
    // big asteroids), each on its own depth group, scrolling down at its own
    // speed - the far ones slowest. The art is turned on its side (its
    // edges are empty, so it tiles seamlessly either way) and each layer is
    // two copies stacked, the lower one jumping above the other as it
    // leaves the picture. Built by TimeCrystalBuilder; replaces the
    // original's single scrolling picture (ParallaxScroller2D).
    public class TcSpaceScroller : MonoBehaviour
    {
        [System.Serializable]
        public class Layer
        {
            public Transform a, b;
            public float speed;
        }

        public Layer[] layers;
        /// One copy's height, in world units.
        public float tileHeight;

        public static TcSpaceScroller Instance { get; private set; }

        void OnEnable() { Instance = this; }

        // ---- Zones: each stage its own space (TcArt.zone2, zone3) ----
        Sprite[] _zone1;
        float _boost = 1f;
        const float MostBoost = 16f;
        /// How far into a warp it is, 0 (cruising) to 1 (flat out): the
        /// ships' bloom and the jets' flames swell with it (TimeCrystalGame, TcJets).
        /// How fast space is going, times its usual (backwards below 0) - the story's time flow sets it.
        public float Boost { get => _boost; set => _boost = value; }
        public static float Warp01 => Instance ? Mathf.Clamp01((Mathf.Abs(Instance._boost) - 1f) / (MostBoost - 1f)) : 0f;
        public int Zone { get; private set; }

        Sprite[] ZoneArt(int zone)
        {
            if (_zone1 == null)
            {
                _zone1 = new Sprite[layers.Length];
                for (int i = 0; i < layers.Length; i++)
                    _zone1[i] = layers[i].a ? layers[i].a.GetComponent<SpriteRenderer>().sprite : null;
            }
            var art = TcArt.Get;
            return zone == 1 && art ? art.zone2 : zone == 2 && art ? art.zone3 : _zone1;
        }

        /// Into the next zone (after a boss): space speeds up into a warp, a
        /// white flash, and it's the new zone's layers as it slows back down.
        public void Warp(int zone, bool reverse = false)
        {
            StopAllCoroutines();
            Tint(Color.white);
            StartCoroutine(Warping(zone, reverse));
        }

        // `reverse`: time running backwards (THE TIME CRYSTAL broken) - space and its streaks rush the other way.
        System.Collections.IEnumerator Warping(int zone, bool reverse)
        {
            float dir = reverse ? -1f : 1f;
            // Timed to its sound (Wayne's futuristic whoosh, 2.6 s): building to
            // 1.2 s, flat out through its peak, the jump at 1.7 s as it breaks, easing out with its tail.
            const float up = 1.2f, hold = 0.5f, down = 0.9f, most = MostBoost;
            TcSfx.Play("warp", 1f, 0.5f);
            float streaks = 0f;
            for (float t = 0f; t < up; t += Time.deltaTime)
            {
                float k = t / up;
                _boost = dir * Mathf.Lerp(1f, most, k * k);
                // Streaks of light: a few at first, more and longer as it speeds up.
                streaks += Time.deltaTime * Mathf.Lerp(30f, 220f, k);
                int n = (int)streaks; streaks -= n;
                TcFx.WarpStreaks(n, Mathf.Lerp(8f, 60f, k * k), reverse);
                yield return null;
            }
            // Flat out: the jump - a flash, a torrent of streaks, another flash.
            TcFx.ScreenFlash(Color.white, 0.45f, 0.25f);
            bool flashed = false;
            for (float t = 0f; t < hold; t += Time.deltaTime)
            {
                _boost = dir * most;
                TcFx.WarpStreaks(4, 70f, reverse);
                if (!flashed && t > hold * 0.5f) { flashed = true; TcFx.ScreenFlash(Color.white, 0.35f, 0.2f); }
                yield return null;
            }
            TcFx.ScreenFlash(Color.white, 0.95f, 0.7f);
            Show(zone);
            for (float t = 0f; t < down; t += Time.deltaTime)
            {
                float k = 1f - t / down;
                _boost = dir * Mathf.Lerp(1f, most, k * k);
                if (Random.value < k) TcFx.WarpStreaks(Mathf.CeilToInt(3f * k), Mathf.Lerp(8f, 60f, k * k), reverse);
                yield return null;
            }
            _boost = 1f;
        }

        /// Straight to a zone's layers (0, 1, 2).
        public void Show(int zone)
        {
            var art = ZoneArt(zone);
            if (art == null) return;
            Zone = zone;
            for (int i = 0; i < layers.Length && i < art.Length; i++)
            {
                if (!art[i]) continue;
                foreach (var t in new[] { layers[i].a, layers[i].b })
                    if (t) t.GetComponent<SpriteRenderer>().sprite = art[i];
            }
        }
        void OnDisable() { if (Instance == this) Instance = null; StopAllCoroutines(); Tint(Color.white); _boost = 1f; }

        /// The whole of space flashes `color` (the boss's WARNING): `times`
        /// pulses, each `period` seconds, back to its own colours after.
        public void Flash(Color color, int times = 4, float period = 0.5f)
        {
            StartCoroutine(Flashing(color, times, period));
        }

        System.Collections.IEnumerator Flashing(Color color, int times, float period)
        {
            for (int i = 0; i < times; i++)
                for (float t = 0f; t < period; t += Time.deltaTime)
                {
                    // Snaps to the colour, eases back off.
                    float k = 1f - Mathf.Clamp01(t / (period * 0.8f));
                    Tint(Color.Lerp(Color.white, color, k * k));
                    yield return null;
                }
            Tint(Color.white);
        }

        void Tint(Color c)
        {
            if (layers == null) return;
            foreach (var layer in layers)
                foreach (var t in new[] { layer.a, layer.b })
                    if (t) foreach (var r in t.GetComponentsInChildren<SpriteRenderer>(true)) r.color = c;
        }

        void Update()
        {
            foreach (var layer in layers)
            {
                if (!layer.a || !layer.b) continue;
                Vector3 step = Vector3.down * layer.speed * _boost * Time.deltaTime;
                layer.a.localPosition += step;
                layer.b.localPosition += step;
                // The one that's gone down past a whole tile goes back on top.
                Wrap(layer.a, layer.b);
                Wrap(layer.b, layer.a);
            }
        }

        void Wrap(Transform low, Transform other)
        {
            if (low.localPosition.y <= -tileHeight)
                low.localPosition = other.localPosition + Vector3.up * tileHeight;
            // (Running backwards - the reverse warp: the one gone up past a tile goes back under.)
            else if (low.localPosition.y >= tileHeight)
                low.localPosition = other.localPosition + Vector3.down * tileHeight;
        }
    }
}
