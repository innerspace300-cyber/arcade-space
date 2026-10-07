// StartupSplash.cs — after Unity's splash: a black screen with the ARcade
// cabinet logo (the CAP watermark's art, Resources/Capture/Watermark, scaled
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
        RawImage _logo;
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

            var logo = Resources.Load<Texture2D>("Capture/Watermark");
            if (logo)
            {
                logo.filterMode = FilterMode.Point;
                var image = new GameObject("Logo", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(transform, false);
                image.texture = logo;
                image.raycastTarget = false;
                image.color = new Color(1f, 1f, 1f, 0f);
                _logo = image;
                // Whole-pixel steps: twice the old two-thirds-of-the-width size,
                // or as near as fits the screen.
                int scale = 2 * Mathf.FloorToInt(Screen.width * 0.66f / logo.width);
                scale = Mathf.Max(1, Mathf.Min(scale, Mathf.FloorToInt(Screen.width * 0.94f / logo.width)));
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
                yield return null;
            }
            if (_logo) _logo.color = Color.white;

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
    }
}
