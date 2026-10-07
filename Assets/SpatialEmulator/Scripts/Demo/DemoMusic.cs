// DemoMusic.cs — ENDLESS KNIGHT's soundtrack (Resources/Music/DemoTheme:
// Elevate Audio's "8bit Arcade Action Chiptune Game"), looping under the
// demo on the MUSIC volume (GameAudio, MusicTrack). One player for the whole
// app, outside the demo's world, so a restart or the portal to the next
// level doesn't start it over. It fades out when the demo stops (a ROM game
// is picked) and pauses while the demo is paused - except in Settings, so
// the MUSIC slider can be heard as it moves. While the knight rests (and on
// as she sleeps) the rest music (Resources/Music/RestTheme: "meditation")
// takes over, from its start each time she sits down; as she gets up it
// fades away and the soundtrack comes back, from its beginning.

using SpatialEmulator.UI;
using UnityEngine;

namespace SpatialEmulator.Demo
{
    public class DemoMusic : MonoBehaviour
    {
        const float Loudness = 0.55f;       // (under the game's sounds)
        const float RestLoudness = 0.7f;
        const float FadeSeconds = 0.6f;
        const float RestFadeSeconds = 1.5f, WakeFadeSeconds = 0.8f;

        static DemoMusic s_player;
        Track _theme, _rest;

        // One piece of music, faded in and out (paused at silence, so it picks up where it was).
        class Track
        {
            public AudioSource source;
            public float level, loudness;

            public Track(Transform parent, string name, AudioClip clip, float loudness)
            {
                // (Each on its own object: the MUSIC volume's filter works on its object's source.)
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                source = go.AddComponent<AudioSource>();
                source.clip = clip;
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.volume = 0f;
                go.AddComponent<MusicTrack>();
                GameAudio.Fit(source);
                this.loudness = loudness;
            }

            /// Toward `target` (0-1) over `seconds`; at silence it stops (from the top when it comes back) or pauses.
            public void Fade(float target, float seconds, bool stopAtSilence)
            {
                if (!source.clip) return;
                level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime / seconds);
                source.volume = level * loudness;
                if (level > 0f && !source.isPlaying)
                {
                    if (source.time > 0f) source.UnPause(); else source.Play();
                }
                else if (level <= 0f && source.isPlaying)
                {
                    if (stopAtSilence) source.Stop(); else source.Pause();
                }
            }
        }

        /// Starts it, the first time the demo runs.
        public static void Ensure()
        {
            if (s_player) return;
            var theme = Resources.Load<AudioClip>("Music/DemoTheme");
            if (!theme) return;
            var go = new GameObject("Demo Music");
            s_player = go.AddComponent<DemoMusic>();
            s_player._theme = new Track(go.transform, "Theme", theme, Loudness);
            s_player._rest = new Track(go.transform, "Rest", Resources.Load<AudioClip>("Music/RestTheme"), RestLoudness);
        }

        void Update()
        {
            bool demo = PitDemoGame.Running;
            bool paused = PitDemoGame.IsPaused && !SettingsScreen.IsOpen;
            var knight = demo ? PitDemoGame.Running.Player : null;
            bool resting = knight && knight.IsResting && !knight.IsDead && _rest.source.clip;
            bool on = demo && !paused;
            float fade = on ? (resting ? RestFadeSeconds : WakeFadeSeconds) : FadeSeconds;
            // The soundtrack: from the top again after a rest or a ROM game; paused, where it was.
            _theme.Fade(on && !resting ? 1f : 0f, fade, !demo || resting);
            // The rest music: from its start each time she sits down (paused, it waits).
            _rest.Fade(on && resting ? 1f : 0f, fade, !(paused && resting));
        }
    }
}
