// TcMusic.cs — THE TIME CRYSTAL's soundtrack, a track for each stage
// (Resources/Music/TcMusic1-3, from Pixabay): stage 1 Retro BGM Chan's
// "Boss Battle Part 2", stage 2 Tunetank's "Synthwave Retro 80s", stage 3
// Retro BGM Chan's "Boss Battle Part 1" - each looping through its stage,
// fading over to the next as the warp to it starts (back to the first on the
// next loop), and from the first again for a new game. On the MUSIC volume (GameAudio, MusicTrack), under the game's
// sounds; one player for the whole app. It pauses while the game is paused
// (except in Settings, so the MUSIC slider can be heard), and fades out when
// the game stops.

using SpatialEmulator.UI;
using UnityEngine;

namespace SpatialEmulator.Demo
{
    public class TcMusic : MonoBehaviour
    {
        const float Loudness = 0.5f;
        const float FadeSeconds = 0.6f;
        const float StartSeconds = 2.4f;   // (a new game's first track fading up slowly, with the story's fade from black)
        bool _starting;
        static readonly string[] Tracks = { "Music/TcMusic1", "Music/TcMusic2", "Music/TcMusic3" };

        static TcMusic s_player;
        AudioSource _source;
        int _track = -1;
        float _level;

        /// Starts it, the first time the game runs.
        public static void Ensure()
        {
            if (s_player || !Application.isPlaying) return;
            var go = new GameObject("Time Crystal Music");
            s_player = go.AddComponent<TcMusic>();
            var source = go.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            go.AddComponent<MusicTrack>();
            GameAudio.Fit(source);
            s_player._source = source;
        }

        // Track `index`'s, from its start.
        void Load(int index)
        {
            _track = Mathf.Clamp(index, 0, Tracks.Length - 1);
            var old = _source.clip;
            _source.clip = Resources.Load<AudioClip>(Tracks[_track]);
            if (old && old != _source.clip) Resources.UnloadAsset(old);   // (one track in memory at a time)
            _source.time = 0f;
            _paused = false;
        }

        void Update()
        {
            bool game = TimeCrystalGame.Running;
            // A new game (RESTART, or out of lives and in again): stage 1's track from its start.
            if (game && TimeCrystalGame.Running != _game)
            {
                _game = TimeCrystalGame.Running;
                if (_track >= 0) { _source.Stop(); _track = -1; _level = 0f; }
            }
            bool paused = TimeCrystalGame.IsPaused && !SettingsScreen.IsOpen;
            bool on = game && !paused;
            var director = TimeCrystal.TcStageDirector.Instance;
            int stage = director ? Mathf.Clamp(director.MusicStage, 0, Tracks.Length - 1) : 0;   // (the next stage's from its warp's start)
            bool right = _track == stage;
            // Up while it's playing this stage's; down to change track, pause, or stop.
            if (_track < 0 && on) _starting = true;
            if (_level >= 1f || !on) _starting = false;
            _level = Mathf.MoveTowards(_level, on && (right || _track < 0) ? 1f : 0f, Time.unscaledDeltaTime / (_starting ? StartSeconds : FadeSeconds));
            _source.volume = _level * Loudness;
            if (_level > 0f)
            {
                if (_track < 0) { Load(stage); _source.Play(); }
                else if (!_source.isPlaying && _paused) { _source.UnPause(); _paused = false; }   // (on from where it was)
            }
            else if (game)
            {
                if (!right) { Load(stage); _source.Play(); }                    // (the new stage's, from its start, fading in)
                else if (_source.isPlaying) { _source.Pause(); _paused = true; }   // (paused, it waits)
            }
            else if (_track >= 0)
            {
                _source.Stop();   // (the game gone: the first track next time)
                _track = -1;
                _paused = false;
            }
        }

        bool _paused;
        TimeCrystalGame _game;
    }
}
