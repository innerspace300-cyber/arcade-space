// CabinetPlaneFader.cs — hides the detected-plane visuals once the cabinet
// is placed, so the game sits in the room instead of on a dotted overlay,
// and brings them back while the cabinet is being positioned. Planes show
// while there's no cabinet (to place one) and for CabinetManipulator's
// idleTimeout after the last touch that placed, selected or moved it (the
// gizmo fades on the same clock); planes detected while hidden stay hidden.
// Only the visual alpha fades, through the template's
// ARPlaneMeshVisualizerFader, so real walls and floors still occlude the
// cabinet.
//
// Plane detection follows the same rule: on while there's no cabinet or
// it's selected or being positioned, off the rest of the time - ARKit
// searching the room every frame warms the phone up during play. The planes
// already found stay (and keep occluding); none are added or refined until
// it's touched again.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Templates.AR;

namespace SpatialEmulator
{
    public class CabinetPlaneFader : MonoBehaviour
    {
        public ARPlaneManager planeManager;
        public SingleCabinetGate gate;
        public CabinetManipulator manipulator;

        /// Plane detection is on (none is running while a placed cabinet's
        /// left alone).
        public static bool Detecting { get; private set; } = true;

        UnityEngine.XR.ARSubsystems.PlaneDetectionMode _detectionMode;
        bool _detectionKnown;

        void Awake() => Detecting = true;

        // What each plane was last told, so a fade only starts on a change.
        readonly Dictionary<ARPlaneMeshVisualizerFader, bool> _shown = new Dictionary<ARPlaneMeshVisualizerFader, bool>();
        readonly List<ARPlaneMeshVisualizerFader> _dead = new List<ARPlaneMeshVisualizerFader>();

        public bool planesVisible =>
            !(gate && gate.cabinet) ||
            (manipulator && Time.unscaledTime - manipulator.lastInteractionTime < manipulator.idleTimeout);

        // Late, so it overrides the template's handler for new planes, which
        // always fades them in.
        void LateUpdate()
        {
            if (!planeManager) return;
            bool visible = planesVisible;
            Detect(visible || (manipulator && manipulator.isSelected));
            foreach (var plane in planeManager.trackables)
            {
                if (!plane.TryGetComponent<ARPlaneMeshVisualizerFader>(out var fader)) continue;
                if (_shown.TryGetValue(fader, out bool shown))
                {
                    if (shown != visible) fader.visualizeSurfaces = visible;
                }
                else if (!visible)
                {
                    fader.SetVisualsImmediate(0f); // new while hidden: never flash in
                }
                _shown[fader] = visible;
            }

            if (_shown.Count > planeManager.trackables.count)
            {
                _dead.Clear();
                foreach (var fader in _shown.Keys)
                    if (!fader) _dead.Add(fader);
                foreach (var fader in _dead)
                    _shown.Remove(fader);
            }
        }

        // ARKit's plane search on or off (only on a change: each one
        // reconfigures the session).
        void Detect(bool on)
        {
            if (!_detectionKnown)
            {
                _detectionMode = planeManager.requestedDetectionMode;
                if (_detectionMode == UnityEngine.XR.ARSubsystems.PlaneDetectionMode.None)
                    _detectionMode = UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Horizontal | UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Vertical;
                _detectionKnown = true;
            }
            // (Also when it should be on but the search was left off - by a
            // trip to full screen, say.)
            bool off = planeManager.requestedDetectionMode == UnityEngine.XR.ARSubsystems.PlaneDetectionMode.None;
            if (on == Detecting && on != off) return;
            Detecting = on;
            planeManager.requestedDetectionMode = on ? _detectionMode : UnityEngine.XR.ARSubsystems.PlaneDetectionMode.None;
        }
    }
}
