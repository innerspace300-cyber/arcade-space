// AppSettings.cs — the player's settings (Settings screen), kept in
// PlayerPrefs.

using System;
using UnityEngine;

namespace SpatialEmulator
{
    public enum TouchControlsMode
    {
        Auto,    // hidden while a game controller is connected
        Always,
        Hidden,
    }

    public static class AppSettings
    {
        const string TouchControlsKey = "SpatialEmulator.touchControls";
        const string ResumeKey = "SpatialEmulator.resumeLatestSave";
        const string LayerSpacingKey = "SpatialEmulator.layerSpacingScale";
        const string PickerInARKey = "SpatialEmulator.pickerInAR";

        /// Layer spacing slider range: 0 (all layers flat on one plane) to
        /// three times the cabinet's default spacing; 1 = default.
        public const float MaxLayerSpacingScale = 3f;

        public static event Action Changed;

        public static TouchControlsMode TouchControls
        {
            get => (TouchControlsMode)PlayerPrefs.GetInt(TouchControlsKey, (int)TouchControlsMode.Auto);
            set { PlayerPrefs.SetInt(TouchControlsKey, (int)value); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        /// Multiplies the cabinet's layer spacing (Settings > Layer spacing).
        public static float LayerSpacingScale
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(LayerSpacingKey, 1f), 0f, MaxLayerSpacingScale);
            set { PlayerPrefs.SetFloat(LayerSpacingKey, Mathf.Clamp(value, 0f, MaxLayerSpacingScale)); Changed?.Invoke(); }
        }

        /// Show the game picker inside the cabinet's frame, in AR, instead of
        /// on the screen (when there's a cabinet).
        public static bool PickerInAR
        {
            get => PlayerPrefs.GetInt(PickerInARKey, 0) != 0;
            set { PlayerPrefs.SetInt(PickerInARKey, value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        /// Load a game's newest save state as soon as the game starts.
        public static bool ResumeLatestSave
        {
            get => PlayerPrefs.GetInt(ResumeKey, 0) != 0;
            set { PlayerPrefs.SetInt(ResumeKey, value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
        }
    }
}
