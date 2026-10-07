// IosBuildPostprocessor.cs — patches the Xcode project and Info.plist Unity
// generates for iOS, so a "Replace" export builds without hand-edits.
//
// SpatialEmulatorCore.framework needs nothing here: its .meta has
// AddToEmbeddedBinaries, so Unity links, embeds and signs it itself, and the
// system frameworks it uses are load commands in its own binary.
//
// ENABLE_MODULE_VERIFIER: Unity 6000.6's iOS template turns on Xcode's
// Module Verifier for the UnityFramework target, and its VerifyModule step
// rejects Unity's own generated framework headers - "double-quoted include
// in framework header", "umbrella header for module 'UnityFramework' does
// not include header 'AppDelegateListener.h'", "could not build module
// 'Test'". Hit on a fresh AR Mobile template export (2026-09-23), so it's a
// Unity/Xcode incompatibility, not something this project adds.
//
// iOS 27 SDK AR rotation workaround: an app linked against the iOS 27 SDK
// (Xcode 27) and run on an iOS 27 device gets its AR camera pose rotated 90°
// - planes land on the wrong surfaces and content slides with the phone.
// Known Unity/AR Foundation bug, no fix yet:
// https://discussions.unity.com/t/ar-content-is-rotated-90-degrees-in-ios-27-beta/1735923
// iOS applies the new behavior based on the SDK version recorded in the
// binary, so a script phase rewrites it to 26.0 (vtool) before signing.
// Confirmed on-device 2026-09-23 (iPhone 13 Pro Max, iOS 27.2): camera roll
// went from a constant ~270° to ~0° held upright, and the floor was detected
// as HorizontalUp instead of Vertical. Remove once Unity ships a fix;
// release builds are made with Xcode 26, where the script skips itself.
//
// NOTE: do not port the old SPATIAL EMULATOR project's scene-delegate /
// UIApplicationSceneManifest code into this file. Unity 6000.6 already
// registers its own UnityScene delegate in the generated Info.plist, and
// overwriting that manifest would replace it with an empty delegate.

#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace SpatialEmulator.Editor
{
    public static class IosBuildPostprocessor
    {
        const string SdkVersionPhaseName = "Record iOS 26 SDK (iOS 27 AR rotation workaround)";

        // Only an iOS 27+ SDK triggers the bug, so release builds made with
        // Xcode 26 (App Store / TestFlight) skip the patch entirely.
        const string SdkVersionScript =
            "if [ \"${SDK_VERSION%%.*}\" -lt 27 ]; then exit 0; fi\n" +
            "BIN=\"${TARGET_BUILD_DIR}/${EXECUTABLE_PATH}\"\n" +
            "xcrun vtool -set-build-version ios \"${IPHONEOS_DEPLOYMENT_TARGET}\" 26.0 -replace " +
            "-output \"${BIN}.sdkpatch\" \"${BIN}\" && mv \"${BIN}.sdkpatch\" \"${BIN}\"\n";

        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            string pbxPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var pbx = new PBXProject();
            pbx.ReadFromFile(pbxPath);

            // TargetGuidByName throws for "Unity-iPhone"/"UnityFramework" on
            // this Unity version - use the dedicated accessors for those.
            // "GameAssembly" has no accessor, so TargetGuidByName is correct there.
            string mainTargetGuid = pbx.GetUnityMainTargetGuid();
            string frameworkTargetGuid = pbx.GetUnityFrameworkTargetGuid();
            foreach (var guid in new[] { mainTargetGuid, frameworkTargetGuid, pbx.TargetGuidByName("GameAssembly") })
            {
                if (string.IsNullOrEmpty(guid)) continue;
                pbx.SetBuildProperty(guid, "ENABLE_MODULE_VERIFIER", "NO");
            }

            // Each binary is patched in its own target, after linking and
            // before that target signs it - patching UnityFramework after it's
            // embedded in the app would break its signature.
            foreach (var guid in new[] { mainTargetGuid, frameworkTargetGuid })
            {
                // The script edits its own target's binary in place, which
                // Xcode's script sandboxing would block.
                pbx.SetBuildProperty(guid, "ENABLE_USER_SCRIPT_SANDBOXING", "NO");
                AddSdkVersionPhase(pbx, guid);
            }

            // Plugins/iOS/SEFilePicker.mm (the Files picker for ROM import)
            // is compiled into UnityFramework and uses UTTypeZIP.
            pbx.AddFrameworkToProject(frameworkTargetGuid, "UniformTypeIdentifiers.framework", false);
            // Plugins/iOS/SERecorder.mm (the CAP key's video) writes with
            // AVAssetWriter.
            // Plugins/iOS/SEShare.mm saves a video to Photos for YouTube.
            foreach (var framework in new[] { "AVFoundation.framework", "CoreMedia.framework", "CoreVideo.framework", "Photos.framework" })
                pbx.AddFrameworkToProject(frameworkTargetGuid, framework, false);
            // Plugins/iOS/SEGameCenter.mm (ENDLESS KNIGHT's leaderboard).
            pbx.AddFrameworkToProject(frameworkTargetGuid, "GameKit.framework", false);

            pbx.WriteToFile(pbxPath);

            // The Game Center capability (its entitlement) for the app.
            var capabilities = new ProjectCapabilityManager(pbxPath, "Unity-iPhone/ARcade.entitlements", null, mainTargetGuid);
            capabilities.AddGameCenter();
            capabilities.WriteToFile();

            // Shows the app's Documents folder (Application.persistentDataPath)
            // in the Files app, so romsets can also be dropped in there;
            // RomLibrary.ImportLooseZips moves them into Documents/roms.
            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("UIFileSharingEnabled", true);
            plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);
            // No encryption beyond what iOS provides, so App Store Connect
            // doesn't ask about export compliance on every upload.
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            // The share sheet's Save Image (ScreenCap, Plugins/iOS/SEShare.mm)
            // adds to Photos, which iOS only allows with a reason given.
            plist.root.SetString("NSPhotoLibraryAddUsageDescription", "ARcade saves your screen caps to Photos.");
            plist.WriteToFile(plistPath);
        }

        static void AddSdkVersionPhase(PBXProject pbx, string targetGuid)
        {
            // An "Append" build keeps the existing project, so don't add it twice.
            foreach (var phaseGuid in pbx.GetAllBuildPhasesForTarget(targetGuid))
                if (pbx.GetBuildPhaseName(phaseGuid) == SdkVersionPhaseName)
                    return;

            pbx.AddShellScriptBuildPhase(targetGuid, SdkVersionPhaseName, "/bin/sh", SdkVersionScript);
        }
    }
}
#endif
