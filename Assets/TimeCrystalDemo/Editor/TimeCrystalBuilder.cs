// TimeCrystalBuilder.cs — Tools > ARcade > Build Time Crystal Demo: makes
// Resources/TimeCrystalDemo.prefab (played by SpatialEmulator.Demo.TimeCrystalGame)
// from THE TIME CRYSTAL's scene (Wayne's 2025 game jam game, its assets in
// Assets/TimeCrystalDemo, copied from the UNITY GAME JAM project):
//  - the scene's game objects under one root (not its camera, light, volume
//    or event system - ARcade has its own);
//  - the space background rebuilt as six depth layers (TcSpaceScroller);
//  - the enemy and crystal spawn points brought in to the vertical
//    cabinet's 3:4 picture (the jam build's window was 16:10).
// Run again after changing the scene; it overwrites the prefab.

using System.Collections.Generic;
using TimeCrystal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpatialEmulator.EditorTools
{
    public static class TimeCrystalBuilder
    {
        const string ScenePath = "Assets/TimeCrystalDemo/Scenes/THE TIME CRYSTAL.unity";
        const string PrefabPath = "Assets/TimeCrystalDemo/Resources/TimeCrystalDemo.prefab";
        const string SpaceArt = "Assets/TimeCrystalDemo/SPRITES/ScifiSpaceAssetsNAv1/PremadeParallax/PremadeParallax3/bg{0}.png";

        /// The picture: 3:4, this many world units tall (the jam build showed 10).
        public const float FieldHeight = 12f;
        public const float FieldWidth = FieldHeight * 3f / 4f;

        // The six space layers, back to front: their depth group and how fast they scroll (units a second).
        static readonly (string layer, float speed)[] Space =
        {
            ("TcSpace", 0.25f), ("TcNebula", 0.45f), ("TcPlanets", 0.7f),
            ("TcStars", 1.0f), ("TcRocks", 1.4f), ("TcBigRocks", 1.9f),
        };

        // The jam build's spawn row ran x -5.58..5.56 at y 6.1, just above its picture.
        const float SpawnSpread = 0.62f;   // of their x
        const float SpawnAbove = 1.0f;     // above the picture's top edge

        static readonly string[] Skip = { "EventSystem", "Main Camera", "Global Light 2D", "Global Volume" };

        [MenuItem("Tools/ARcade/Build Time Crystal Demo")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var root = new GameObject("TimeCrystalDemo");
                SceneManager.MoveGameObjectToScene(root, scene);
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (go == root) continue;
                    if (System.Array.IndexOf(Skip, go.name) >= 0) { Object.DestroyImmediate(go); continue; }
                    go.transform.SetParent(root.transform, true);
                }

                ReplaceBackground(root.transform);
                MoveSpawnPoints(root.transform);
                FitHud(root.transform);
                AddBombs(root.transform);
                AddStage(root.transform);

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                Debug.Log(ok ? "[TimeCrystalBuilder] saved " + PrefabPath : "[TimeCrystalBuilder] save FAILED");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void ReplaceBackground(Transform root)
        {
            // The original: one picture (the same art, flattened), scrolled by ParallaxScroller2D.
            var old = root.Find("PARALLAX BG");
            if (old) Object.DestroyImmediate(old.gameObject);

            var bg = new GameObject("SPACE LAYERS").transform;
            bg.SetParent(root, false);
            var scroller = bg.gameObject.AddComponent<TcSpaceScroller>();
            var layers = new List<TcSpaceScroller.Layer>();
            float tile = 0f;
            for (int i = 0; i < Space.Length; i++)
            {
                var sprite = ImportSpaceArt(string.Format(SpaceArt, i + 1));
                if (!sprite) continue;
                // On its side: the art's 640 px height across the picture's width.
                tile = sprite.rect.width / sprite.pixelsPerUnit;
                var holder = new GameObject("SPACE " + Space[i].layer).transform;
                holder.SetParent(bg, false);
                holder.gameObject.layer = LayerMask.NameToLayer(Space[i].layer);
                var entry = new TcSpaceScroller.Layer { speed = Space[i].speed };
                for (int copy = 0; copy < 2; copy++)
                {
                    var t = new GameObject(copy == 0 ? "A" : "B").transform;
                    t.SetParent(holder, false);
                    t.gameObject.layer = holder.gameObject.layer;
                    t.localPosition = new Vector3(0f, copy * tile, 0f);
                    t.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    var sr = t.gameObject.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.sortingOrder = -100 + i;
                    if (copy == 0) entry.a = t; else entry.b = t;
                }
                layers.Add(entry);
            }
            scroller.layers = layers.ToArray();
            scroller.tileHeight = tile;
        }

        static Sprite ImportSpaceArt(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) { Debug.LogError("[TimeCrystalBuilder] missing " + path); return null; }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            // 640 px (the art's height, turned across) = the picture's width.
            float ppu = 640f / FieldWidth;
            bool changed = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                || !Mathf.Approximately(importer.spritePixelsPerUnit, ppu) || importer.filterMode != FilterMode.Point
                || importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = ppu;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // The HUD (pilot portrait, health bar, lives, SCORE) sat left of the
        // jam build's playfield, in its 16:10 window; here it's in the
        // picture's top-left corner, and a HI SCORE (TimeCrystalGame keeps
        // it) mirrors SCORE at the top right. Canvas units: the canvas
        // scaler's 1920 across the picture's width.
        static readonly Vector2 HudShift = new Vector2(-26f, 1382f);
        static readonly string[] LetterboxBars = { "TOP BAR", "BOTTOM BAR", "LEFT BAR", "RIGHT BAR" };

        static void FitHud(Transform root)
        {
            var canvas = root.Find("Canvas");
            if (!canvas) return;
            foreach (string bar in LetterboxBars)
            {
                var t = canvas.Find(bar);
                if (t) Object.DestroyImmediate(t.gameObject);   // (collapsed flat, they drew as thin grey lines)
            }
            foreach (Transform child in canvas)
                if (child is RectTransform rect) rect.anchoredPosition += HudShift;

            // HI SCORE: SCORE's label and number, copied to the right edge.
            var label = canvas.Find("HI SCORE");
            var number = canvas.Find("PLAYER 1 SCORE");
            if (!label || !number) return;
            var hiLabel = Object.Instantiate(label.gameObject, canvas).GetComponent<RectTransform>();
            hiLabel.name = "HI SCORE LABEL";
            hiLabel.GetComponent<TMPro.TMP_Text>().text = "HI SCORE:";
            var hiNumber = Object.Instantiate(number.gameObject, canvas).GetComponent<RectTransform>();
            hiNumber.name = "HI SCORE NUMBER";
            foreach (var (copy, original) in new[] { (hiLabel, (RectTransform)label), (hiNumber, (RectTransform)number) })
            {
                var text = copy.GetComponent<TMPro.TMP_Text>();
                text.alignment = TMPro.TextAlignmentOptions.TopRight;
                var p = original.anchoredPosition;
                copy.anchoredPosition = new Vector2(-p.x, p.y);
            }
        }

        // ---- Bombs (TcBombs): placeholders from the ship pack and Pixel Arsenal ----
        const string Art = "Assets/TimeCrystalDemo/";
        // The ship pack's bomb icon with a B for its S (ARCADE/icon-bomb-b.png): the HUD's counter and the pickup.
        const string BombIcon = Art + "ARCADE/icon-bomb-b.png";
        // The pickup: the same bomb on its side, its B kept upright.
        const string BombPickup = Art + "ARCADE/icon-bomb-b-side.png";
        const string BombBlast = Art + "Pixel Arsenal/Prefabs/Combat/Explosions/Nuke/NukeExplosionBlue.prefab";
        const string BombSound = Art + "Pixel Arsenal/Sound/ExplosionNuke.wav";
        const string PickupBurst = Art + "Pixel Arsenal/Prefabs/Interactive/Pickups/Explosion/PickupExplosionBlue.prefab";
        const string PickupSound = Art + "AUDIO/Cha Ching Bonus 6.wav";
        // The HUD's bomb row: under the lives (canvas units, after HudShift).
        static readonly Vector2 BombRowStart = new Vector2(-579f, 819f);
        const float BombRowStep = 40f, BombIconHeight = 60f;

        static void AddBombs(Transform root)
        {
            var go = new GameObject("BOMBS");
            go.transform.SetParent(root, false);
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            var bombs = go.AddComponent<TcBombs>();
            bombs.BlastEffect = AssetDatabase.LoadAssetAtPath<GameObject>(BombBlast);
            bombs.BlastSound = AssetDatabase.LoadAssetAtPath<AudioClip>(BombSound);
            bombs.PickupEffect = AssetDatabase.LoadAssetAtPath<GameObject>(PickupBurst);
            bombs.PickupSound = AssetDatabase.LoadAssetAtPath<AudioClip>(PickupSound);
            var icon = ImportPixelSprite(BombIcon);
            var pickup = ImportPixelSprite(BombPickup);
            bombs.PickupFrames = pickup ? new[] { pickup } : new Sprite[0];
            var canvas = root.Find("Canvas");
            var icons = new List<UnityEngine.UI.Image>();
            if (canvas && icon)
            {
                float width = BombIconHeight * icon.rect.width / icon.rect.height;
                for (int i = 0; i < bombs.MaxBombs; i++)
                {
                    var rect = new GameObject("BOMB " + (i + 1), typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
                    rect.SetParent(canvas, false);
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(width, BombIconHeight);
                    rect.anchoredPosition = BombRowStart + Vector2.right * (BombRowStep * i);
                    var image = rect.GetComponent<UnityEngine.UI.Image>();
                    image.sprite = icon;
                    image.raycastTarget = false;
                    icons.Add(image);
                }
            }
            bombs.Icons = icons.ToArray();
        }

        // ---- The stage (TcStageDirector) in place of the jam build's random waves ----
        static readonly string[] OrbArt =
            { Art + "ARCADE/orb_purple.png", Art + "ARCADE/orb_blue.png", Art + "ARCADE/orb_red.png" };
        const string MediumEnemy = Art + "PREFABS/MEDIUM ENEMY 1.prefab";
        /// The ship's hit circle, world units: small, as in a bullet-hell game (the jam build's was 0.24).
        const float PlayerHitRadius = 0.12f;

        static void AddStage(Transform root)
        {
            var waves = root.GetComponentInChildren<EnemyWaveSpawner>(true);
            var directorObject = new GameObject("STAGE DIRECTOR");
            directorObject.transform.SetParent(root, false);
            directorObject.AddComponent<AudioSource>().playOnAwake = false;
            var director = directorObject.AddComponent<TcStageDirector>();
            director.BeepSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "Retro Arsenal/Sound/Misc/retro_target_spawn.wav");
            director.HitSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "Retro Arsenal/Sound/Explosion/retro_explosion_small.wav");
            director.DestroySound = AssetDatabase.LoadAssetAtPath<AudioClip>(Art + "Retro Arsenal/Sound/Explosion/retro_explosion_medium.wav");
            director.Small = waves ? waves.easyEnemies : new GameObject[0];
            director.Medium = AssetDatabase.LoadAssetAtPath<GameObject>(MediumEnemy);
            var orbs = new List<Sprite>();
            foreach (string path in OrbArt)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer && (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point || importer.spriteImportMode != SpriteImportMode.Single))
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
                orbs.Add(AssetDatabase.LoadAssetAtPath<Sprite>(path));
            }
            director.Orbs = orbs.ToArray();
            if (waves) waves.gameObject.SetActive(false);   // (kept in the prefab, off)
            // The falling crystals wait for the countdown's GO!
            director.Crystals = root.GetComponentInChildren<CollectibleWaveSpawner>(true);
            if (director.Crystals) director.Crystals.enabled = false;

            var ship = root.GetComponentInChildren<RetroShmupPlayerController>(true);
            var hit = ship ? ship.GetComponent<CircleCollider2D>() : null;
            if (hit) hit.radius = PlayerHitRadius / Mathf.Max(0.01f, ship.transform.lossyScale.x);
        }

        // A single-sprite pixel-art PNG: point filtered, uncompressed.
        static Sprite ImportPixelSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) { Debug.LogError("[TimeCrystalBuilder] missing " + path); return null; }
            if (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void MoveSpawnPoints(Transform root)
        {
            float top = FieldHeight * 0.5f + SpawnAbove;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("ENEMY SPAWN POINT")) continue;
                t.localPosition = new Vector3(t.localPosition.x * SpawnSpread, top, t.localPosition.z);
                // The markers' own sprites (placement guides in the editor) don't draw.
                if (t.TryGetComponent<SpriteRenderer>(out var sr)) sr.enabled = false;
            }
            var respawn = root.Find("RESPAWN POINT");
            if (respawn && respawn.TryGetComponent<SpriteRenderer>(out var r)) r.enabled = false;
        }
    }
}
