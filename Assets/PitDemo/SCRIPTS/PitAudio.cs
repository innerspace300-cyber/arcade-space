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

    /// Plays the source's clip on top of anything it's already playing.
    public static void Play(AudioSource source)
    {
        if (!source || !source.clip) return;
        source.spatialBlend = 0f;
        SpatialEmulator.GameAudio.Fit(source);   // (the SFX volume)
        source.PlayOneShot(source.clip);
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
}
