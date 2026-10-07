// NativeFilePicker.cs — opens the system file picker for .zip files: the
// iOS Files picker (Plugins/iOS/SEFilePicker.mm) on device, a plain open
// panel in the Editor. The picked files come back as paths the app may read
// and delete - on iOS they are copies in the app's tmp folder.
//
// Or a folder (PickFolder): every .zip in it and its subfolders, read where
// they are (the player's own files - not to be deleted), until
// EndFolderAccess.
//
// The iOS side answers with UnitySendMessage, so this component's GameObject
// name must be unique in the scene.

using System;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace SpatialEmulator.Games
{
    public class NativeFilePicker : MonoBehaviour
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SE_PickZipFiles(string gameObjectName, string callbackMethod);
        [DllImport("__Internal")] static extern void SE_PickFolder(string gameObjectName, string callbackMethod);
        [DllImport("__Internal")] static extern void SE_EndFolderAccess();
#endif

        Action<string[]> _pending;

        public bool IsOpen => _pending != null;

        /// Calls done with the picked paths (empty if cancelled).
        public void PickZips(Action<string[]> done)
        {
            if (_pending != null) return;
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Add ROM", "", "zip");
            done(string.IsNullOrEmpty(path) ? Array.Empty<string>() : new[] { path });
#elif UNITY_IOS
            _pending = done;
            SE_PickZipFiles(gameObject.name, nameof(OnNativeFilesPicked));
#else
            Debug.LogWarning("[NativeFilePicker] no file picker on this platform");
            done(Array.Empty<string>());
#endif
        }

        /// Calls done with the zips in a picked folder and its subfolders
        /// (none if cancelled), and how many more are in iCloud, not yet
        /// downloaded to the phone. Call EndFolderAccess once they're read.
        public void PickFolder(Action<string[], int> done)
        {
            if (_pending != null) return;
#if UNITY_EDITOR
            string folder = UnityEditor.EditorUtility.OpenFolderPanel("Scan Folder", "", "");
            done(string.IsNullOrEmpty(folder) ? Array.Empty<string>()
                : System.IO.Directory.GetFiles(folder, "*.zip", System.IO.SearchOption.AllDirectories), 0);
#elif UNITY_IOS
            _pending = paths =>
            {
                // The first line: "?N", N the zips still in iCloud.
                int inCloud = 0;
                var lines = new System.Collections.Generic.List<string>(paths);
                if (lines.Count > 0 && lines[0].StartsWith("?"))
                {
                    int.TryParse(lines[0].Substring(1), out inCloud);
                    lines.RemoveAt(0);
                }
                done(lines.ToArray(), inCloud);
            };
            SE_PickFolder(gameObject.name, nameof(OnNativeFilesPicked));
#else
            Debug.LogWarning("[NativeFilePicker] no folder picker on this platform");
            done(Array.Empty<string>(), 0);
#endif
        }

        /// Lets go of the picked folder (iOS: its security-scoped access).
        public void EndFolderAccess()
        {
#if UNITY_IOS && !UNITY_EDITOR
            SE_EndFolderAccess();
#endif
        }

        // Called from SEFilePicker.mm: newline-separated paths, "" if cancelled.
        public void OnNativeFilesPicked(string joined)
        {
            var done = _pending;
            _pending = null;
            done?.Invoke(string.IsNullOrEmpty(joined)
                ? Array.Empty<string>()
                : joined.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
