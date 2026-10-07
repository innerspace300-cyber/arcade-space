// CrtCabinetModel.cs — on the CRT cabinet model's prefab (Resources/
// CrtCabinet, made by CrtCabinetBuilder): where its screen is, in the
// model's own space, for CrtCabinet to fit the game's picture onto it.

using UnityEngine;

namespace SpatialEmulator
{
    public class CrtCabinetModel : MonoBehaviour
    {
        [Tooltip("The middle of the tube, on the glass.")]
        public Vector3 screenCenter;
        [Tooltip("Out of the glass, toward the player (the screen tilts back).")]
        public Vector3 screenNormal = Vector3.forward;
        [Tooltip("The tube (and the bezel's opening), across and up (along the glass).")]
        public float screenWidth = 0.64f, screenHeight = 0.5f;
        [Tooltip("How far behind the glass the picture's back layer goes (its edges; its middle bulges up to the tube).")]
        public float recess = 0.075f;
        [Tooltip("Half the cabinet's width.")]
        public float halfWidth = 0.42f;
        [Tooltip("The front edge of its top (the marquee's), at its right side (x = halfWidth).")]
        public Vector3 topFront;
        [Tooltip("How far back the control panel's top board goes (z; the model faces its +z).")]
        public float panelBack = 0.3f;
        [Tooltip("...and how high its back edge (the lip under the screen) is.")]
        public float panelBackHeight = 0.2f;
    }
}
