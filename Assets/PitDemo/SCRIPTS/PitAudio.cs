using UnityEngine;

// ARcade: one-shot sounds in the demo overlap instead of cutting each other
// off - even two of the same clip (orange pickups in a row, hits, jumps).
// PlayOneShot stacks voices on a source, where Play() restarts it. They
// also play non-positional: the demo's world is far from the AR camera's
// listener, so a positional sound would be silent. Looping sounds (the
// particle loops) still use Play().
public static class PitAudio
{
    static AudioSource s_shared;

    /// Plays the source's clip (at its volume and pitch) on top of anything it's already playing - on PitAudio's own
    /// voices, which the SFX volume reaches: the SFX filter on an object reaches only one of its sources, and the
    /// knight keeps all hers on one (her jump, slash, Holy Slash and hurt played on at SFX 0).
    public static void Play(AudioSource source)
    {
        if (!source || !source.clip || source.mute) return;
        float pitch = source.pitch;
        if (Mathf.Abs(pitch - 1f) < 0.01f) { PlayClip(source.clip, source.volume); return; }
        var go = new GameObject("PitAudio Pitched");
        var voice = go.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        SpatialEmulator.GameAudio.Fit(voice);   // (the SFX volume)
        voice.clip = source.clip;
        voice.volume = source.volume;
        voice.pitch = pitch;
        voice.Play();
        Object.Destroy(go, source.clip.length / Mathf.Max(0.05f, Mathf.Abs(pitch)) + 0.1f);
    }

    /// Plays a clip that outlives its object (in place of AudioSource.PlayClipAtPoint).
    public static void PlayClip(AudioClip clip, float volume = 1f)
    {
        if (!clip) return;
        if (!s_shared)
        {
            var go = new GameObject("PitAudio");
            s_shared = go.AddComponent<AudioSource>();
            s_shared.playOnAwake = false;
            s_shared.spatialBlend = 0f;
            SpatialEmulator.GameAudio.Fit(s_shared);   // (the SFX volume)
        }
        s_shared.PlayOneShot(clip, volume);
    }

    /// Plays a clip on a voice of its own and hands it back, so it can be faded or stopped early (a boss's death cry,
    /// over as its body goes). It goes away by itself once the clip's done.
    public static AudioSource Voice(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (!clip) return null;
        var go = new GameObject("PitAudio Voice");
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        SpatialEmulator.GameAudio.Fit(source);   // (the SFX volume)
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.05f, Mathf.Abs(pitch)) + 0.1f);   // (lower, it plays longer)
        return source;
    }

    /// Plays just the first `seconds` of a clip, faded out over its last `fade` - on a voice of its own, so it
    /// can be cut short (the chest's coins, now it throws half as many).
    public static void PlayClip(AudioClip clip, float volume, float seconds, float fade = 0.3f)
    {
        if (!clip) return;
        if (seconds >= clip.length) { PlayClip(clip, volume); return; }
        var go = new GameObject("PitAudio Cut");
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        SpatialEmulator.GameAudio.Fit(source);   // (the SFX volume)
        source.clip = clip;
        source.volume *= volume;
        source.Play();
        var cut = go.AddComponent<Cut>();
        cut.source = source; cut.end = Time.time + seconds; cut.fade = Mathf.Max(0.01f, fade); cut.volume = source.volume;
    }

    class Cut : MonoBehaviour
    {
        public AudioSource source;
        public float end, fade, volume;

        void Update()
        {
            float left = end - Time.time;
            if (left <= 0f || !source.isPlaying) { Destroy(gameObject); return; }
            source.volume = volume * Mathf.Clamp01(left / fade);
        }
    }
}
