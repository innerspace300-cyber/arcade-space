// CabinetDrop.cs — a placed cabinet flies in along the surface's normal and
// slams onto it: down onto a floor or table from high above the tap, in from
// the room against a wall, up from below into a ceiling. A little rebound on landing, an explosion of
// pixels at the contact and sparks out of its middle, both in the room grid's
// colour, and a heavy haptic, with Magic Boom from the moment of the tap. Put on the
// cabinet as it's placed (SingleCabinetGate); it moves the cabinet by its fall
// on top of whatever else moves it, then takes itself off.

using UnityEngine;

namespace SpatialEmulator
{
    // (After CrtCabinet, so it's in the air from the first frame it's seen.)
    [DefaultExecutionOrder(1002)]
    public class CabinetDrop : MonoBehaviour
    {
        const float DropHeight = 3f;        // metres: from out of the view, where the player stands
        const float FallSeconds = 0.3f;
        const float HopHeight = 0.035f;     // metres, at scale 1
        const float HopSeconds = 0.18f;
        const float SplashLift = 0.02f;     // metres off the surface
        const float SparksSeconds = 2.5f;   // long enough for the pixels to fall back down
        const float SparksScale = 0.45f;
        const float WallClearance = 0.5f;   // metres: how far from the player the front of a cabinet flying at a wall starts
        const float WallFlightLeast = 0.3f;

        const float MinPitch = 0.75f, MaxPitch = 1.3f, PitchStep = 0.12f;
        static float s_lastPitch;

        CabinetOrientation _orientation;
        float _time, _offset, _distance;
        Vector3 _from;   // the way it comes from: out of the surface
        bool _landed;

        void Awake() => _orientation = GetComponent<CabinetOrientation>();

        void LateUpdate()
        {
            if (!_orientation) { Destroy(this); return; }
            if (!_orientation.Fitted) return;

            if (_time <= 0f)
            {
                _from = _orientation.ceilingMounted ? Vector3.down : _orientation.wallMounted ? _orientation.WallOutward : Vector3.up;
                _distance = _orientation.wallMounted ? WallFlight() : DropHeight;
                Sound();
                UI.GamePicker.OpenOnCabinet();   // (it lands showing the game list, flat in its frame)
                // This frame's the heavy one (the cabinet and the list set up):
                // it waits up out of view, and falls from the next.
                transform.position += _from * (_distance - _offset);
                _offset = _distance;
                _time = 1e-5f;
                UI.GamePicker.FollowCabinet();
                return;
            }
            // (A slow frame doesn't skip the fall ahead: never more than a 30th of a second at a time.)
            _time += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            float height;
            if (_time < FallSeconds)
            {
                float t = _time / FallSeconds;
                height = _distance * (1f - t * t);   // (speeding up all the way down)
            }
            else
            {
                float after = (_time - FallSeconds) / HopSeconds;
                height = after < 1f ? HopHeight * transform.localScale.x * Mathf.Sin(after * Mathf.PI) : 0f;
            }
            transform.position += _from * (height - _offset);
            _offset = height;
            UI.GamePicker.FollowCabinet();

            if (!_landed && _time >= FallSeconds)
            {
                _landed = true;
                Slam();
            }
            if (_time >= FallSeconds + HopSeconds) Destroy(this);
        }

        // A wall's slam starts out in the room, the cabinet's front stopping short of the player.
        float WallFlight()
        {
            var cam = Camera.main;
            var box = GetComponent<BoxCollider>();
            if (!cam || !box) return WallFlightLeast;
            Vector3 size = box.bounds.size;
            float depth = Mathf.Abs(_from.x) * size.x + Mathf.Abs(_from.z) * size.z;
            float room = Vector3.Dot(cam.transform.position - _orientation.Contact, _from) - depth - WallClearance;
            return Mathf.Clamp(room, WallFlightLeast, DropHeight);
        }

        // Magic Boom, cut to start on its boom: heard the moment the plane is tapped, the cabinet landing inside it.
        static void Sound()
        {
            if (UI.FullScreenTest.Enabled) return;   // (put down for FULL, out of sight: silent)
            var clip = Resources.Load<AudioClip>("MagicBoom");
            if (!clip) return;
            var sound = new GameObject("Cabinet Drop Sound");
            var source = sound.AddComponent<AudioSource>();
            source.ignoreListenerPause = true;   // (heard over the game list, which holds the game's sound)
            source.clip = clip;
            source.spatialBlend = 0f;
            // A new pitch each slam, always clearly different from the last.
            float pitch;
            do pitch = Random.Range(MinPitch, MaxPitch);
            while (Mathf.Abs(pitch - s_lastPitch) < PitchStep);
            s_lastPitch = pitch;
            source.pitch = pitch;
            GameAudio.Fit(source);
            source.Play();
            Destroy(sound, clip.length / pitch + 0.1f);
        }

        void Slam()
        {
            if (!UI.FullScreenTest.Enabled) Haptics.Play(Haptics.Kind.Heavy);

            var box = GetComponent<BoxCollider>();
            var size = box ? box.bounds.size : Vector3.one * 0.9f;
            float footprint = Mathf.Max(size.x, size.z) / 0.9f;
            // The 3D cabinet pinched bigger or smaller: the splash sized from the cabinet at its
            // starting size, then by how far it's been pinched - the ring the pixels come
            // from, the pixels and their splash all in proportion (Burst).
            float k = 1f;
            var crt = GetComponent<CrtCabinet>();
            if (crt && crt.Shown && box && crt.StartScale > 0.001f)
            {
                k = Mathf.Clamp(transform.localScale.x / crt.StartScale, 0.2f, 5f);
                Vector3 local = Vector3.Scale(box.size, new Vector3(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z))) / k;
                footprint = Mathf.Max(local.x, local.z) / 0.9f;
            }

            // A splash of glowing square pixels shooting out from where the player tapped - the middle of the
            // cabinet's side on the surface (its base on a floor, top on a ceiling, back on a wall) - away from
            // that surface, then falling back under gravity.
            Vector3 at = _orientation.Contact + _from * SplashLift;
            Burst("SparkExplosionYellow", at, Quaternion.FromToRotation(Vector3.up, _from), SparksScale * footprint * k, k);
        }

        // A Retro Arsenal effect (made white), in the room grid's colour.
        static void Burst(string effect, Vector3 at, Quaternion rotation, float scale, float k)
        {
            var prefab = Resources.Load<GameObject>(effect);
            if (!prefab) return;
            var go = Instantiate(prefab, at, rotation);
            go.transform.localScale = Vector3.one * scale;
            // A cabinet pinched bigger or smaller (CrtCabinet.SizeRatio, kept for the next drop): its pixels,
            // and how high and far they splash, sized with it. (The particles go in metres - Shape scaling -
            // so the effect's scale only widens the ring they come from.) Speed and gravity both scaled:
            // the splash k times as high and wide, in the same time.
            if (!Mathf.Approximately(k, 1f))
                foreach (var system in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = system.main;
                    if (main.scalingMode != ParticleSystemScalingMode.Shape) continue;   // (Hierarchy ones scale with the effect already)
                    main.startSizeMultiplier *= k;
                    main.startSpeedMultiplier *= k;
                    main.gravityModifierMultiplier *= k;
                }
            Tint(go);
            Destroy(go, SparksSeconds);
        }

        static Gradient RainbowGradient(float alpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.yellow, 1f / 6f), new GradientColorKey(Color.green, 2f / 6f),
                        new GradientColorKey(Color.cyan, 0.5f), new GradientColorKey(Color.blue, 4f / 6f), new GradientColorKey(Color.magenta, 5f / 6f),
                        new GradientColorKey(Color.red, 1f) },
                new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) });
            return gradient;
        }

        static void Tint(GameObject effect)
        {
            Color color = PlaneGridFrame.Glows[AppSettings.GridColor];
            foreach (var part in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = part.main;
                color.a = main.startColor.color.a;
                // NEON RGB: each pixel its own colour of the rainbow (GlowPixel.shader then turns it on round).
                main.startColor = PlaneGridFrame.IsRgb ? new ParticleSystem.MinMaxGradient(RainbowGradient(color.a)) { mode = ParticleSystemGradientMode.RandomColor } : new ParticleSystem.MinMaxGradient(color);
            }
            color.a = 1f;
            foreach (var light in effect.GetComponentsInChildren<Light>(true)) light.color = color;
        }
    }
}
