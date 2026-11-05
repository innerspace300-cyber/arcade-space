using System.Collections.Generic;
using UnityEngine;

namespace TimeCrystal
{
    // ARcade: THE TIME CRYSTAL's per-enemy sounds (Resources/TcSfx, from
    // Toon Projectiles, Retro Arsenal and Pixel Arsenal, levelled): each
    // kind's shot, being hit and going up ("popcorn_shot", "heavy_death"...).
    // One plain source under the game's root; the same clip doesn't start
    // again within `gap` (a ring of bullets is one shot, a hail of hits one hit).
    public static class TcSfx
    {
        static readonly Dictionary<string, AudioClip> s_clips = new Dictionary<string, AudioClip>();
        static AudioSource s_source;

        public static AudioClip Clip(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!s_clips.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>("TcSfx/" + name);
                s_clips[name] = clip;
            }
            return clip;
        }

        public static void Play(string name, float volume = 1f, float gap = 0.06f)
        {
            var clip = Clip(name);
            if (!clip || !TcWorld.Root || !TcWorld.SoundAllowed(clip, gap)) return;
            if (!s_source || s_source.transform.parent != TcWorld.Root)
            {
                var go = new GameObject("TC SFX");
                go.transform.SetParent(TcWorld.Root, false);
                s_source = go.AddComponent<AudioSource>();
                s_source.playOnAwake = false;
                s_source.spatialBlend = 0f;
                SpatialEmulator.GameAudio.Apply();   // (the SFX volume)
            }
            s_source.PlayOneShot(clip, volume);
        }
    }
}
