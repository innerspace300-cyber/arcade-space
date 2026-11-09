// SingleCabinetGate.cs — keeps the AR scene to one arcade cabinet, since
// there's one emulator to drive. Once a cabinet is placed, the template's
// tap-to-place trigger is switched off until that cabinet is deleted, so a
// stray tap can't drop a second one. Placement is held off while a finger
// is on a control button or the game picker is open. (The joystick zone is off until a cabinet exists,
// so a tap there places it: CabinetManipulator sets that.) Lives on the
// template's Object Spawner (added by Tools > Spatial Emulator > Build Arcade
// Controls).
//
// Away from the app even for a second, AR tracking starts over and the room's
// no longer where it was, so a cabinet would come back somewhere else: on
// return it goes, scanning restarts and the player places a new one (the
// game starts again). Full screen has no cabinet in the room, so it stays.

using SpatialEmulator.Controls;
using SpatialEmulator.UI;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.ARStarterAssets;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace SpatialEmulator
{
    public class SingleCabinetGate : MonoBehaviour
    {
        public ObjectSpawner spawner;
        public ARInteractorSpawnTrigger spawnTrigger;
        public ArcadeTouchRouter router;

        GameObject _cabinet;

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int SE_TakeBackgrounded();   // (SEDevice.mm)

        // Real leaving only: a sheet or Control Center over the app doesn't count.
        static bool LeftApp => SE_TakeBackgrounded() != 0;
#elif UNITY_ANDROID && !UNITY_EDITOR
        static bool s_left;
        void OnApplicationPause(bool paused) => s_left |= paused;
        static bool LeftApp { get { bool left = s_left; s_left = false; return left; } }
#else
        static bool LeftApp => false;
#endif

        /// The placed cabinet, or null.
        public GameObject cabinet => _cabinet ? _cabinet : null;

        void Awake()
        {
            if (!spawner) spawner = GetComponent<ObjectSpawner>();
            if (!spawnTrigger) spawnTrigger = GetComponent<ARInteractorSpawnTrigger>();
        }

        void OnEnable()
        {
            if (spawner) spawner.objectSpawned += OnObjectSpawned;
            Apply();
        }

        void OnDisable()
        {
            if (spawner) spawner.objectSpawned -= OnObjectSpawned;
        }

        void OnObjectSpawned(GameObject spawned)
        {
            if (_cabinet && _cabinet != spawned)
            {
                Destroy(spawned); // a spawn that slipped past the disabled trigger
                return;
            }
            _cabinet = spawned;
            spawned.AddComponent<CabinetDrop>();
            Apply();
        }

        // Deletion happens elsewhere (CabinetManipulator's Delete button), so
        // poll for it; a destroyed cabinet compares equal to null.
        void Update()
        {
            if (LeftApp && !FullScreenTest.Enabled)
            {
                if (_cabinet) Destroy(_cabinet);
                CabinetHints.RestartScanning();
            }
            Apply();
        }

        void Apply()
        {
            bool allowed = !_cabinet && !(router && router.busy) && !GamePicker.IsOpen;
            if (spawnTrigger && spawnTrigger.enabled != allowed) spawnTrigger.enabled = allowed;
        }
    }
}
