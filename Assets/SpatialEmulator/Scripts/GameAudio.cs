// GameAudio.cs — ARcade's two volumes (Settings > MUSIC and SFX), kept in
// PlayerPrefs. Every AudioSource gets a VolumeFilter that scales what it
// plays by one of them - MUSIC for the ones marked as music (MusicTrack: the
// demo's background music, when it has some), SFX for the rest (the demo's
// sounds, and an emulated game's own sound) - after its own volume, so the
// scripts that set a source's volume before each sound are left alone.
// Apply() fits the filters to any new sources (PitDemoGame and the core's
// audio output call it now and then).

using UnityEngine;

namespace SpatialEmulator
{
    public static class GameAudio
    {
        const string MusicKey = "ARcade.MusicVolume", SfxKey = "ARcade.SfxVolume";

        // Read on the audio thread (PlayerPrefs can't be): set on the main one.
        internal static volatile float MusicNow = -1f, SfxNow = -1f;

        public static float Music
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.8f);
            set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); MusicNow = Mathf.Clamp01(value); }
        }

        public static float Sfx
        {
            get => PlayerPrefs.GetFloat(SfxKey, 1f);
            set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); SfxNow = Mathf.Clamp01(value); }
        }

        /// Fits every source with its filter (once each).
        public static void Apply()
        {
            if (MusicNow < 0f) MusicNow = Music;
            if (SfxNow < 0f) SfxNow = Sfx;
            foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Fit(source);
        }

        public static void Fit(AudioSource source)
        {
            if (!source || source.GetComponent<VolumeFilter>()) return;
            if (MusicNow < 0f) MusicNow = Music;
            if (SfxNow < 0f) SfxNow = Sfx;
            source.gameObject.AddComponent<VolumeFilter>().music = source.GetComponent<MusicTrack>();
        }
    }

    /// Marks an AudioSource as music (GameAudio: the MUSIC slider, not SFX).
    public class MusicTrack : MonoBehaviour { }

    /// Scales what its object's AudioSource plays by the MUSIC or SFX volume.
    public class VolumeFilter : MonoBehaviour
    {
        public bool music;

        void OnAudioFilterRead(float[] data, int channels)
        {
            float v = music ? GameAudio.MusicNow : GameAudio.SfxNow;
            if (v < 0f || v >= 0.999f) return;
            for (int i = 0; i < data.Length; i++) data[i] *= v;
        }
    }
}
