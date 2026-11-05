// RomKeysUnderPicture.cs — full screen on the phone, a heavy or vertical arcade game's
// picture sits up under the three bars and the gear (FullScreenTest.RomAtTop); if A, B
// and C still reach up onto it, they drop till C's top is just under it - never so far
// that B's letter runs into COIN, nor off the bottom. Back where they were otherwise.
// (The stick floats under the thumb: nothing to move.) THE TIME CRYSTAL moves its own
// (TcButtonHints).

using UnityEngine;

namespace SpatialEmulator.Controls
{
    public class RomKeysUnderPicture : MonoBehaviour
    {
        RectTransform[] _abc;
        Vector2[] _home;
        RectTransform _bLetter, _coin;
        bool _moved;
        static readonly Vector3[] s_corners = new Vector3[4];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("ROM Keys Under Picture");
            DontDestroyOnLoad(go);
            go.AddComponent<RomKeysUnderPicture>();
        }

        void LateUpdate()
        {
            if (_abc == null && !FindKeys()) return;
            bool want = UI.FullScreenTest.Enabled && UI.FullScreenTest.RomAtTop && !UI.TvOutput.Showing
                && Mobile.LibretroCore.IsRunning && !Demo.LayeredDemo.AnyRunning && UI.FullScreenTest.PictureRect.height > 0f;
            if (!want)
            {
                if (_moved) { for (int i = 0; i < 3; i++) if (_abc[i]) _abc[i].anchoredPosition = _home[i]; _moved = false; }
                return;
            }
            var canvas = _abc[2].GetComponentInParent<Canvas>();
            float k = canvas ? canvas.rootCanvas.scaleFactor : 1f;   // (screen pixels per canvas unit)
            if (k <= 0f) return;
            // From home: how far down C's top has to go to sit a few pixels under the picture (none if it's clear).
            float lift = _moved ? (_abc[2].anchoredPosition.y - _home[2].y) * k : 0f;   // (where it is now, from home, on screen)
            float cTop = Top(_abc[2]) - lift;
            float target = UI.FullScreenTest.PictureRect.yMin - Screen.width * 0.012f;
            float drop = Mathf.Min(0f, target - cTop);
            // No further than B's letter clear of COIN, and A's letter clear of the bottom.
            if (_bLetter && _coin) drop = Mathf.Max(drop, Top(_coin) + Screen.width * 0.008f - (Bottom(_bLetter) - lift));
            var aLetter = _abc[0].Find("Glyph") as RectTransform;
            if (aLetter) drop = Mathf.Max(drop, Screen.safeArea.yMin + Screen.width * 0.01f - (Bottom(aLetter) - lift));
            drop = Mathf.Min(0f, drop);
            for (int i = 0; i < 3; i++) _abc[i].anchoredPosition = _home[i] + new Vector2(0f, drop / k);
            _moved = true;
        }

        // Its top and bottom on the screen (an overlay canvas: world is screen).
        static float Top(RectTransform t) { t.GetWorldCorners(s_corners); return s_corners[1].y; }
        static float Bottom(RectTransform t) { t.GetWorldCorners(s_corners); return s_corners[0].y; }

        bool FindKeys()
        {
            var abc = new RectTransform[3];
            foreach (var b in FindObjectsByType<ArcadeButton>(FindObjectsInactive.Include))
            {
                if (b.name == "Button A") abc[0] = (RectTransform)b.transform;
                else if (b.name == "Button B") abc[1] = (RectTransform)b.transform;
                else if (b.name == "Button C") abc[2] = (RectTransform)b.transform;
                else if (b.button == RetroPadButton.Select) _coin = (RectTransform)b.transform;
            }
            if (!abc[0] || !abc[1] || !abc[2]) return false;
            _abc = abc;
            _home = new Vector2[3];
            for (int i = 0; i < 3; i++) _home[i] = abc[i].anchoredPosition;
            _bLetter = abc[1].Find("Glyph") as RectTransform;
            return true;
        }
    }
}
