// StartupSplash.cs — after Unity's splash: a pixel-art space (the icon's navy and
// purples, nebula and twinkling stars, made here: SpaceBackground) under a pink-purple wash that
// slowly turns (its ends trading places over WashSeconds), with the ARcade
// logo (Resources/Capture/SplashLogo: the app icon over ARcade, centred; scaled
// up in whole pixels) over everything while the app starts up. The AR
// session is held off meanwhile - no camera, no plane search, no prompts -
// while Game Center signs in (its banner, or iOS's own sign-in sheet over
// this, would otherwise hitch the camera). Once sign-in's settled (or after
// MaxSeconds), it fades out and AR starts - or, for a full-screen-only game
// remembered from last time, full screen's already up and AR stays off.

using System.Collections;
using SpatialEmulator.Games;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace SpatialEmulator.UI
{
    public class StartupSplash : MonoBehaviour
    {
        // The logo fades in, holds, then the whole splash fades out to AR.
        const float FadeInSeconds = 2f;
        const float HoldSeconds = 2f;
        const float FadeOutSeconds = 2f;
        const float MaxSeconds = 8f;   // longest wait for Game Center
        // iOS slides its "Welcome back" banner in after sign-in's done, and it
        // stays up a few seconds - the splash holds through it.
        const float BannerSeconds = 3.5f;

        /// Up (AR held off until it's gone).
        public static bool Showing { get; private set; }

        CanvasGroup _group;
        RawImage _logo, _space, _wash;
        Texture2D _spaceTexture, _washTexture;
        // The wash's ends (their alpha its strength): pink at the top, purple at the bottom.
        static readonly Color32 WashTop = new Color32(255, 70, 190, 45), WashBottom = new Color32(110, 40, 230, 45);   // (light: the dark of space mostly shows)
        // A few stars that twinkle: their pixel, and their own rate and phase.
        Vector2Int[] _twinkle;
        float[] _twinkleRate, _twinklePhase;
        float _nextTwinkle;
        ARSession _session;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("Startup Splash");
            DontDestroyOnLoad(go);
            go.AddComponent<StartupSplash>();
        }

        void Awake()
        {
            Showing = true;
            _session = FindAnyObjectByType<ARSession>();
            if (_session) _session.enabled = false;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;   // over everything
            gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.AddComponent<CanvasGroup>();

            var back = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            back.transform.SetParent(transform, false);
            back.color = Color.black;
            var rect = back.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            // The space, over the black, fading in with the logo.
            _space = new GameObject("Space", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _space.transform.SetParent(transform, false);
            _space.raycastTarget = false;
            _space.texture = _spaceTexture = SpaceBackground(Screen.height / (float)Mathf.Max(1, Screen.width));
            _space.color = new Color(1f, 1f, 1f, 0f);
            var spaceRect = _space.rectTransform;
            spaceRect.anchorMin = Vector2.zero; spaceRect.anchorMax = Vector2.one;
            spaceRect.offsetMin = spaceRect.offsetMax = Vector2.zero;

            // Over the space (under the logo): a soft pink-to-purple wash, top to bottom - a 1 x 2
            // texture, smoothly filtered, so no edges.
            _wash = new GameObject("Wash", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _wash.transform.SetParent(transform, false);
            _wash.raycastTarget = false;
            _washTexture = new Texture2D(1, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            _washTexture.SetPixels32(new[] { WashBottom, WashTop });
            _washTexture.Apply();
            _wash.texture = _washTexture;
            _wash.uvRect = new Rect(0f, 0.25f, 1f, 0.5f);   // (texel middles at the screen's top and bottom: a full-height blend)
            _wash.color = new Color(1f, 1f, 1f, 0f);
            var washRect = _wash.rectTransform;
            washRect.anchorMin = Vector2.zero; washRect.anchorMax = Vector2.one;
            washRect.offsetMin = washRect.offsetMax = Vector2.zero;

            var logo = Resources.Load<Texture2D>("Capture/SplashLogo");   // (the icon over ARcade, both centred)
            if (!logo) logo = Resources.Load<Texture2D>("Capture/Watermark");
            if (logo)
            {
                logo.filterMode = FilterMode.Point;
                var image = new GameObject("Logo", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(transform, false);
                image.texture = logo;
                image.raycastTarget = false;
                image.color = new Color(1f, 1f, 1f, 0f);
                _logo = image;
                // Whole-pixel steps, about 70% of the screen's width (the stacked logo's narrower than the old one, so less wide).
                int scale = Mathf.Max(1, Mathf.FloorToInt(Screen.width * 0.7f / logo.width));
                image.rectTransform.sizeDelta = new Vector2(logo.width * scale, logo.height * scale);
            }
        }

        IEnumerator Start()
        {
            float started = Time.unscaledTime;
            float settledAt = -1f;
            for (float t = 0f; t < FadeInSeconds; t += Time.unscaledDeltaTime)
            {
                if (settledAt < 0f && GameCenter.Settled) settledAt = Time.unscaledTime;
                if (_logo) _logo.color = new Color(1f, 1f, 1f, t / FadeInSeconds);
                if (_space) _space.color = new Color(1f, 1f, 1f, t / FadeInSeconds);
                if (_wash) _wash.color = new Color(1f, 1f, 1f, t / FadeInSeconds);
                yield return null;
            }
            if (_logo) _logo.color = Color.white;
            if (_space) _space.color = Color.white;
            if (_wash) _wash.color = Color.white;

            // Hold: at least HoldSeconds, and until sign-in's settled (and its
            // banner's been and gone).
            float holdUntil = Time.unscaledTime + HoldSeconds;
            while (!GameCenter.Settled && Time.unscaledTime - started < MaxSeconds) yield return null;
            if (settledAt < 0f) settledAt = Time.unscaledTime;
            if (GameCenter.SignedIn) holdUntil = Mathf.Max(holdUntil, settledAt + BannerSeconds);
            while (Time.unscaledTime < holdUntil) yield return null;

            // AR on (full screen keeps it off itself), then fade.
            if (_session && !FullScreenTest.Enabled) _session.enabled = true;
            for (float t = 0f; t < FadeOutSeconds; t += Time.unscaledDeltaTime)
            {
                _group.alpha = 1f - t / FadeOutSeconds;
                yield return null;
            }
            Showing = false;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_spaceTexture) Destroy(_spaceTexture);
            if (_washTexture) Destroy(_washTexture);
        }

        void Update()
        {
            FitSpace();
            Twinkle();
            TurnWash();
        }

        // The wash's two ends lerping into each other's colour and back, WashSeconds each way (eased),
        // so the gradient slowly turns: pink over purple, then purple over pink.
        const float WashSeconds = 4f;

        void TurnWash()
        {
            if (!_washTexture) return;
            float k = Mathf.PingPong(Time.unscaledTime / WashSeconds, 1f);
            k = k * k * (3f - 2f * k);
            _washTexture.SetPixels32(new[] { Color32.Lerp(WashBottom, WashTop, k), Color32.Lerp(WashTop, WashBottom, k) });
            _washTexture.Apply(false);
        }

        // ---- The space: low-res pixel art, point-filtered up to the screen ----
        // The icon's colours: navy at the top into deep violet below, purple nebula
        // clouds in bands (no smooth gradients: pixel art), scattered stars, a few
        // bright cross-shaped ones, and some that twinkle.
        static readonly Color32 Navy = new Color32(8, 30, 60, 255), Violet = new Color32(20, 10, 40, 255),
            Plum = new Color32(53, 14, 71, 255),
            Purple = new Color32(58, 34, 120, 255), Lilac = new Color32(90, 54, 170, 255);

        const float Darkness = 0.45f;

        Texture2D SpaceBackground(float tall)
        {
            // 120 pixels across the screen's short side, however it's turned (tall: its height over its width).
            const int Across = 120;
            int w = tall >= 1f ? Across : Mathf.Max(1, Mathf.RoundToInt(Across / tall));
            int h = tall >= 1f ? Mathf.Max(1, Mathf.RoundToInt(Across * tall)) : Across;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            var rng = new System.Random(7);
            float ox = (float)rng.NextDouble() * 100f, oy = (float)rng.NextDouble() * 100f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Base: navy up top into violet below, smoothly (no bands).
                    float v = 1f - y / (float)(h - 1);
                    Color c = Color.Lerp(Navy, Violet, v);
                    // Nebula: two noise octaves, blended in softly from the clouds' edges to their hearts.
                    float n = Mathf.PerlinNoise(ox + x * 0.035f, oy + y * 0.035f) * 0.65f + Mathf.PerlinNoise(ox + x * 0.09f, oy + y * 0.09f) * 0.35f;
                    float cloud = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.82f, n));
                    c = Color.Lerp(c, Color.Lerp(Plum, Purple, cloud), cloud * 0.85f);
                    c = Color.Lerp(c, Lilac, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.76f, 0.9f, n)) * 0.6f);
                    c *= Darkness;   // (deep space; the stars go on at full brightness after)
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            // Stars: mostly faint single pixels, a few white, some cyan and lilac like the icon's.
            int stars = w * h / 110;
            for (int i = 0; i < stars; i++)
            {
                int x = rng.Next(w), y = rng.Next(h);
                double r = rng.NextDouble();
                px[y * w + x] = r < 0.55 ? new Color32(150, 150, 200, 255) : r < 0.8 ? new Color32(255, 255, 255, 255) : r < 0.92 ? new Color32(0, 214, 255, 255) : new Color32(170, 96, 255, 255);
            }
            // Bright ones: a plus of four dimmer pixels round a white middle.
            for (int i = 0; i < 6; i++)
            {
                int x = 1 + rng.Next(w - 2), y = 1 + rng.Next(h - 2);
                px[y * w + x] = new Color32(255, 255, 255, 255);
                var arm = new Color32(140, 200, 255, 255);
                px[y * w + x - 1] = px[y * w + x + 1] = px[(y - 1) * w + x] = px[(y + 1) * w + x] = arm;
            }
            // Twinkling: these come and go (Twinkle).
            int twinkles = 18;
            _twinkle = new Vector2Int[twinkles];
            _twinkleRate = new float[twinkles];
            _twinklePhase = new float[twinkles];
            for (int i = 0; i < twinkles; i++)
            {
                _twinkle[i] = new Vector2Int(rng.Next(w), rng.Next(h));
                _twinkleRate[i] = 0.6f + (float)rng.NextDouble() * 1.4f;
                _twinklePhase[i] = (float)rng.NextDouble();
            }
            tex.SetPixels32(px);
            tex.Apply();
            _spaceBase = px;
            return tex;
        }

        Color32[] _spaceBase;

        // The phone's turned on its side since this was made (in a Kishi or a Backbone: TvOutput): made again to
        // the new shape, or its pixels stretch and the stars are lines.
        void FitSpace()
        {
            if (!_spaceTexture || !_space) return;
            bool wide = Screen.width > Screen.height;
            if (wide == _spaceTexture.width > _spaceTexture.height) return;
            Destroy(_spaceTexture);
            _space.texture = _spaceTexture = SpaceBackground(Screen.height / (float)Mathf.Max(1, Screen.width));
        }

        void Twinkle()
        {
            if (!_spaceTexture || _twinkle == null || Time.unscaledTime < _nextTwinkle) return;
            _nextTwinkle = Time.unscaledTime + 0.12f;   // (stepped, like an old game's)
            int w = _spaceTexture.width;
            for (int i = 0; i < _twinkle.Length; i++)
            {
                var p = _twinkle[i];
                float k = Mathf.Repeat(Time.unscaledTime * _twinkleRate[i] + _twinklePhase[i], 1f);
                var c = k < 0.15f ? new Color32(255, 255, 255, 255) : k < 0.35f ? new Color32(120, 140, 210, 255) : _spaceBase[p.y * w + p.x];
                _spaceTexture.SetPixel(p.x, p.y, c);
            }
            _spaceTexture.Apply(false);
        }
    }
}
