// CabinetPrewarm.cs — the first cabinet's drop without a hitch: soon after
// the app starts, what the drop uses the first time is loaded and drawn
// once, out of sight - the slam's pixel splash (SparkExplosionYellow), its
// boom (MagicBoom), both 3D cabinets (CrtCabinet, CrtCabinetVertical) - so
// their textures are on the GPU and their shaders built before a plane's
// tapped, not in the middle of the fall.

using System.Collections;
using UnityEngine;

namespace SpatialEmulator
{
    public class CabinetPrewarm : MonoBehaviour
    {
        static readonly string[] Prefabs = { "SparkExplosionYellow", "CrtCabinet", "CrtCabinetVertical" };
        static readonly Object[] s_kept = new Object[Prefabs.Length + 1];   // (held: never unloaded)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("Cabinet Prewarm");
            DontDestroyOnLoad(go);
            go.AddComponent<CabinetPrewarm>();
        }

        IEnumerator Start()
        {
            // Loaded in the background.
            var loads = new ResourceRequest[Prefabs.Length];
            for (int i = 0; i < Prefabs.Length; i++) loads[i] = Resources.LoadAsync<GameObject>(Prefabs[i]);
            var boom = Resources.LoadAsync<AudioClip>("MagicBoom");
            foreach (var load in loads) yield return load;
            yield return boom;
            for (int i = 0; i < Prefabs.Length; i++) s_kept[i] = loads[i].asset;
            if (boom.asset is AudioClip clip) { s_kept[Prefabs.Length] = clip; clip.LoadAudioData(); }

            // Drawn once each, a speck (a tenth of a millimetre across) in front
            // of the camera - too small to see - for a few frames.
            Camera cam = null;
            while (!(cam = Camera.main)) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            var copies = new GameObject[Prefabs.Length];
            for (int i = 0; i < Prefabs.Length; i++)
            {
                if (!(s_kept[i] is GameObject prefab)) continue;
                var copy = Instantiate(prefab, cam.transform.position + cam.transform.forward * 0.3f, cam.transform.rotation);
                copy.name = prefab.name + " (Prewarm)";
                copy.transform.localScale = Vector3.one * 0.0001f;
                foreach (var system in copy.GetComponentsInChildren<ParticleSystem>(true)) { var main = system.main; main.playOnAwake = true; }
                foreach (var source in copy.GetComponentsInChildren<AudioSource>(true)) source.enabled = false;
                copies[i] = copy;
            }
            for (int f = 0; f < 4; f++) yield return null;
            foreach (var copy in copies) if (copy) Destroy(copy);
            Destroy(gameObject);
        }
    }
}
