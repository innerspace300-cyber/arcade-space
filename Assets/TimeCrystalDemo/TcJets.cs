using System.Collections.Generic;
using UnityEngine;

namespace TimeCrystal
{
    // ARcade: THE TIME CRYSTAL's engine flames - each pilot's own (TcArt.jets:
    // Hovl Studio's Jet engine effects) out of their jet's nozzles, under its
    // own pixel thrust, following the ship, with pixel sparks spitting out
    // the back. Off altogether while the ship's down (dying, respawning, game
    // over). Choose Your Fighter's big jet gets them too (Attach). The pack's
    // smoke, heat-shimmer and lights are left off (in the picture's layers
    // they drew as grey boxes).
    public class TcJets : MonoBehaviour
    {
        const float Scale = 0.45f;   // (up from 0.3: bigger flames)

        // Each jet's nozzles, art pixels from its middle (x right, y down), in TcPilots order.
        static readonly Vector2[][] Nozzles =
        {
            new[] { new Vector2(-0.5f, 32.5f) },                         // NOVA, ship-037 (1.25x): one, in the middle
            new[] { new Vector2(-3f, 26f), new Vector2(4f, 26f) },       // BLAZE, Plane 08A
            new[] { new Vector2(-0.5f, 29f) },                           // VIPER, Plane 07B: one big one
            new[] { new Vector2(-11f, 26.5f), new Vector2(9.5f, 26.5f) }, // RIO, ship-041 (1.2x): out of its two gun pods
        };

        // Their flames' colours: the prefab's own (NOVA's aqua) or tinted over it - BLAZE red, VIPER green, RIO orange.
        static readonly Color?[] FlameColors =
        {
            null, new Color(1f, 0.12f, 0.06f), new Color(0.2f, 1f, 0.12f), new Color(1f, 0.48f, 0.04f),
        };

        // Their sparks' colours (the flames' own).
        static readonly Color[] SparkColors =
        {
            new Color(0.35f, 0.95f, 1f), new Color(1f, 0.35f, 0.25f), new Color(0.45f, 1f, 0.35f), new Color(1f, 0.65f, 0.2f),
        };

        readonly List<GameObject> _flames = new List<GameObject>();
        int _pilot = -1;
        RetroShmupPlayerController _ship;
        SpriteRenderer _body;
        float _spark;

        void Awake()
        {
            _ship = GetComponent<RetroShmupPlayerController>();
            _body = GetComponent<SpriteRenderer>();
            Rebuild();
        }

        /// The chosen pilot's flames (TcPlayerShip.ApplyPilot).
        public void Rebuild()
        {
            int pilot = TcPilots.Index;
            if (pilot == _pilot && _flames.Count > 0) return;
            Clear(_flames);
            _pilot = pilot;
            Attach(transform, pilot, _flames);
        }

        /// `pilot`'s flames on `ship` (its nozzles, at Scale in its own space), into `into`.
        public static void Attach(Transform ship, int pilot, List<GameObject> into)
        {
            var prefabs = TcArt.Get ? TcArt.Get.jets : null;
            var prefab = prefabs != null && pilot >= 0 && pilot < prefabs.Length ? prefabs[pilot] : null;
            if (!prefab) return;
            foreach (var nozzle in Nozzles[Mathf.Clamp(pilot, 0, Nozzles.Length - 1)])
            {
                var go = Instantiate(prefab, ship);
                go.name = "JET FLAME";
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = TcArt.ActorsLayer;
                    if (t.name.Contains("Distortion") || t.name.Contains("Smoke") || t.name.Contains("Light")) t.gameObject.SetActive(false);
                }
                var tint = FlameColors[Mathf.Clamp(pilot, 0, FlameColors.Length - 1)];
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // (at Scale, times the ship's)
                    if (tint is Color c) { c.a = main.startColor.color.a; main.startColor = c; }
                }
                go.transform.localPosition = new Vector3(nozzle.x, -nozzle.y, 0f) / TcArt.PixelsPerUnit + Vector3.back * 0.01f;
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // (the flame out of its back: down)
                go.transform.localScale = Vector3.one * Scale;
                into.Add(go);
            }
        }

        public static void Clear(List<GameObject> flames)
        {
            foreach (var f in flames) if (f) Destroy(f);
            flames.Clear();
        }

        /// Pixel sparks spitting out of `flames`' nozzles, `rate` a second in all (call every frame).
        public static void Sparks(List<GameObject> flames, int pilot, float rate, float size, ref float clock)
        {
            clock += Time.deltaTime * rate;
            var color = SparkColors[Mathf.Clamp(pilot, 0, SparkColors.Length - 1)];
            while (clock >= 1f)
            {
                clock -= 1f;
                var f = flames.Count > 0 ? flames[Random.Range(0, flames.Count)] : null;
                if (!f || !f.activeInHierarchy) continue;
                var at = f.transform.position + new Vector3(Random.Range(-0.05f, 0.05f), -Random.Range(0.05f, 0.2f) * size, 0f);
                var c = Random.value < 0.3f ? Color.white : color;
                TcFx.Trail(at, c, Random.Range(0.035f, 0.07f) * size, new Vector2(Random.Range(-1.2f, 1.2f), -Random.Range(3f, 7f)) * size, Random.Range(0.15f, 0.35f));
            }
        }

        void Update()
        {
            if (_flames.Count == 0 && TcArt.Get) Rebuild();
            // Out with the ship: off altogether while it's down (dying, respawning, game over).
            bool on = _ship && _body && _body.enabled && _ship.CanAct;
            foreach (var f in _flames) if (f && f.activeSelf != on) f.SetActive(on);
            if (!on) return;
            // At warp speed they roar: up to 2.2x as the warp builds, back as it eases off.
            float warp = TcSpaceScroller.Warp01;
            float size = Scale * (1f + 1.2f * warp);
            foreach (var f in _flames) if (f) f.transform.localScale = Vector3.one * size;
            Sparks(_flames, _pilot, 22f * (1f + 2f * warp), 1f + warp, ref _spark);
        }
    }
}
