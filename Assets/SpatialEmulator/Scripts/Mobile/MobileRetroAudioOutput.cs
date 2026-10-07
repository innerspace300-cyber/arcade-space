// MobileRetroAudioOutput.cs — plays the core's audio (MobileRetroAudio)
// through this GameObject's AudioSource. The AudioSource has no clip;
// OnAudioFilterRead supplies the samples on Unity's audio thread.

using UnityEngine;

namespace SpatialEmulator.Mobile
{
    [RequireComponent(typeof(AudioSource))]
    public class MobileRetroAudioOutput : MonoBehaviour
    {
        // (The game's sound at the SFX volume: its filter added after this one, so it scales what this makes.)
        void Start() => GameAudio.Fit(GetComponent<AudioSource>());

        void OnAudioFilterRead(float[] data, int channels) => MobileRetroAudio.Read(data, channels);
    }
}
