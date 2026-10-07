// HudBuildCheck.cs — stops a build whose open scene has the GamePicker (the
// pixel HUD: GAMES, LOCK, prompts, the picker) switched off. Unticking it is
// handy while arranging the arcade controls, but a build made that way has
// no HUD and its PixelArtSizer controls fall back to their own scale.
// To hide the HUD while editing without affecting builds, use the Hierarchy's
// eye icon (Scene visibility) instead.

using SpatialEmulator.UI;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SpatialEmulator.Editor
{
    public class HudBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var hud = Object.FindAnyObjectByType<PixelCanvasScaler>(FindObjectsInactive.Include);
            if (hud && !hud.gameObject.activeSelf)
                throw new BuildFailedException(
                    "GamePicker (the pixel HUD) is switched off in the scene - tick it back on before building. " +
                    "To hide it while editing, use the Hierarchy's eye icon instead.");
        }
    }
}
