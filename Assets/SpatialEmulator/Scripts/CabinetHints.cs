// CabinetHints.cs — the line of guidance near the top of the screen, which
// replaces the AR template's welcome card and tutorial. It follows what's
// going on:
//  - nothing yet until a game (the demo or a ROM) has been picked and the
//    picker closed - the room isn't scanned (plane detection off) before;
//  - no cabinet and no surfaces found yet: scan the room;
//  - no cabinet: tap a floor or wall to place it;
//  - just placed: tap the cabinet to move, rotate or resize it (a few seconds);
//  - selected: how the arrows, ring and pinch work, fading with the gizmo.
// The banner never takes touches, so taps pass through it to the scene.

using SpatialEmulator.Games;
using SpatialEmulator.UI;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace SpatialEmulator
{
    public class CabinetHints : MonoBehaviour
    {
        const string ScanText = "Move your phone slowly to find the floor, walls and ceiling";
        const string PlaceText = "Tap a floor, wall or ceiling to place the\nSPATIAL EMULATOR cabinet";
        const string EditText = "Tap the cabinet to move, rotate or resize it";
        const string EditingText = "Drag an arrow to move it\nDrag the ring to rotate • Pinch to resize\nTap LOCK to lock it in place";

        public SingleCabinetGate gate;
        public CabinetManipulator manipulator;
        public ARPlaneManager planeManager;
        public TMP_Text label;
        public CanvasGroup group;
        [Tooltip("Seconds the edit hint stays up after the cabinet is placed.")]
        public float placedHintSeconds = 6f;
        [Tooltip("Seconds the banner takes to fade in or out.")]
        public float fadeSeconds = 0.4f;
        [Tooltip("Colour of a message's last line (after its last line break); clear = same as the rest.")]
        public Color secondLineColor = Color.clear;

        GameObject _cabinet;
        static bool s_scanning;   // a game picked: the room is being scanned

        void Awake()
        {
            // No scanning until there's a game to put in the cabinet.
            if (planeManager && !s_scanning) planeManager.enabled = false;
        }
        float _placedAt = float.NegativeInfinity;

        void Update()
        {
            if (!label || !group) return;
            var cabinet = gate ? gate.cabinet : null;
            if (cabinet != _cabinet)
            {
                _cabinet = cabinet;
                if (cabinet) _placedAt = Time.unscaledTime;
            }

            // A game picked (the picker closed with one chosen): start scanning.
            if (!s_scanning && !GamePicker.IsOpen && GameSelection.Current != null)
            {
                s_scanning = true;
                if (planeManager) planeManager.enabled = true;
            }

            string text = null;
            float target = 1f;
            if (!cabinet)
                text = !s_scanning ? null : planeManager && planeManager.trackables.count > 0 ? PlaceText : ScanText;
            else if (manipulator && manipulator.isSelected)
            {
                text = EditingText;
                target = manipulator.editAlpha;
            }
            else if (!(manipulator && manipulator.locked) && Time.unscaledTime - _placedAt < placedHintSeconds)
                text = EditText;

            if (UI.FullScreenTest.Enabled) text = null;   // (no AR to talk about)
            else if (s_scanning && !cabinet) KeepScanning();

            // With nothing to say, fade out on the last message.
            if (text == null) target = 0f;
            else label.text = Styled(text);
            float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
            group.alpha = Mathf.MoveTowards(group.alpha, Mathf.Min(target, 1f), step);
        }

        // Asking to scan: make sure the scan's actually running - the AR
        // session and the plane search both on (each can be left off, by
        // full screen or the startup splash).
        ARSession _session;
        void KeepScanning()
        {
            if (StartupSplash.Showing) return;
            if (!_session) _session = FindAnyObjectByType<ARSession>();
            if (_session && !_session.enabled) _session.enabled = true;
            if (planeManager && !planeManager.enabled) planeManager.enabled = true;
        }

        /// Back to AR after it was off a while (full screen): tracking and
        /// the plane search start over from scratch - the old planes, and
        /// ARKit's idea of the room, can't be trusted.
        public static void RestartScanning()
        {
            var session = FindAnyObjectByType<ARSession>();
            if (session)
            {
                session.enabled = true;
                session.Reset();
            }
            var planes = FindAnyObjectByType<ARPlaneManager>(FindObjectsInactive.Include);
            if (planes && (s_scanning || GameSelection.Current != null))
            {
                s_scanning = true;
                planes.enabled = true;
            }
        }

        string Styled(string text)
        {
            int newline = text.LastIndexOf('\n');
            if (newline < 0 || secondLineColor.a <= 0f) return text;
            return text.Substring(0, newline + 1) + $"<color=#{ColorUtility.ToHtmlStringRGBA(secondLineColor)}>{text.Substring(newline + 1)}</color>";
        }
    }
}
