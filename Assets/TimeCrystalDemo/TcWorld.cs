using UnityEngine;

namespace TimeCrystal
{
    // What THE TIME CRYSTAL's scripts get from ARcade (set by
    // SpatialEmulator.Demo.TimeCrystalGame) in place of the keyboard and a
    // scene of its own: its world lives far from the room, spawned things go
    // under its root, and the controls come from ArcadeInput.
    public static class TcWorld
    {
        /// The game's root: everything spawned goes under it, so it all goes when the game stops.
        public static Transform Root;
        /// Where the original scene's (0, 0, 0) is now.
        public static Vector3 Origin;
        /// What the picture shows, in world space.
        public static Rect Field;

        /// The joystick, -1..1 each way (was WASD / the arrow keys).
        public static Vector2 Move;
        /// Held this frame (were J, K and L).
        public static bool FireJ, FireK, FireL;
        /// A held: the ship's guns (TcPlayerShip; the jam build's J/K/L guns stay off).
        public static bool Fire;
        /// C held: the laser (TcLaser).
        public static bool Laser;
        /// Pressed this frame: set off a bomb (TcBombs).
        public static bool BombPressed;
        /// A direction pushed this frame (a quick double push dashes).
        public static bool LeftPressed, RightPressed, UpPressed, DownPressed;

        /// Instantiate, under the game's root.
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
            => Object.Instantiate(prefab, position, rotation, Root);

        public static GameObject Spawn(GameObject prefab)
            => Object.Instantiate(prefab, Root);

        /// A one-off effect (a hit, an explosion), gone after `seconds`. Its
        /// own sound (played on awake) is muted if the same effect's sound
        /// started a moment ago - a wave going up at once is one bang.
        public static GameObject SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, float seconds = 3f)
        {
            var fx = Spawn(prefab, position, rotation);
            Object.Destroy(fx, seconds);
            foreach (var source in fx.GetComponentsInChildren<AudioSource>(true))
                if (source.playOnAwake && source.clip && !SoundAllowed(source.clip, EffectGap)) source.Stop();
            return fx;
        }

        // ---- Sound ----
        // Firing all three guns at once and every enemy's shots, each a new
        // one-shot, ran iOS out of voices (Unity's 32), cutting sounds off.
        // The same clip doesn't start again within a few hundredths of a
        // second; one that's still ringing carries the sound.

        const float ShotGap = 0.09f, EffectGap = 0.06f;
        static readonly System.Collections.Generic.Dictionary<AudioClip, float> s_nextAllowed = new System.Collections.Generic.Dictionary<AudioClip, float>();

        public static bool SoundAllowed(AudioClip clip, float gap)
        {
            float now = Time.unscaledTime;
            if (s_nextAllowed.TryGetValue(clip, out float next) && now < next) return false;
            s_nextAllowed[clip] = now + gap;
            return true;
        }

        /// PlayOneShot, unless this clip started a moment ago.
        public static void PlayShot(AudioSource source, float gap = ShotGap)
        {
            if (source && source.clip && SoundAllowed(source.clip, gap)) source.PlayOneShot(source.clip);
        }

        /// The original scene's height `y`, where it is now.
        public static float Y(float y) => Origin.y + y;
    }

    /// Gone once it's well outside the picture (the player's shots and the
    /// falling crystals never were, in the original).
    public class TcOffField : MonoBehaviour
    {
        const float Margin = 2f;

        void Update()
        {
            var f = TcWorld.Field;
            var p = transform.position;
            if (p.y > f.yMax + Margin || p.y < f.yMin - Margin || p.x < f.xMin - Margin || p.x > f.xMax + Margin)
                Destroy(gameObject);
        }
    }
}
