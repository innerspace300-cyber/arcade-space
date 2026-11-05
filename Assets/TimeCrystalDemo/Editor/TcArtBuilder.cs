using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TimeCrystal.EditorTools
{
    // ARcade: makes Resources/TcArt (TcArt) from the packs: the projectile
    // strips (their spritesheet.txt gives the frame size), the player's ship
    // and its options, the enemy ships, the boss, the explosions and the
    // pickups' bubble. Every texture it uses is set to point filtering,
    // uncompressed, no mipmaps (pixel art, cut into frames at run time).
    public static class TcArtBuilder
    {
        const string Root = "Assets/TimeCrystalDemo/PACKS/";
        const string Ships = Root + "Animated_Pixel_Ships_v1.5.7/";
        const string Shmup = Root + "Pixel Spaceships for SHMUP 2 1.1.0/";
        const string Pilots = "Assets/TimeCrystalDemo/ARCADE/Pilots/";
        const string AssetPath = "Assets/TimeCrystalDemo/Resources/TcArt.asset";

        // Projectile types used (every colour of each).
        static readonly string[] StripTypes =
        {
            // The player: the base missile and the power-ups.
            "pj1_scifi_missile_small", "pj1_toon_missile_small", "pj1_toon_missile_large", "pj1_scifi_missile_large",
            "pj1_rock_small", "pj1_fireball_small", "pj2_meteor_small", "pj2_ice_spike_small",
            // The enemies' bullets.
            "pj1_blob_small", "pj1_blob_large", "pj1_energy_small", "pj2_lightning_orb_small",
            // The boss's own, full size.
            "pj2_fireball_small", "pj1_electricity_large", "pj1_magic_missile_large",
            // The other pilots' starting guns (TcPilots).
            "pj1_laser_small", "pj1_magic_missile_small", "pj2_scifi_missile_small",
        };

        // Enemy ships (Pixel Spaceships for SHMUP 2), by the name the stage uses.
        static readonly (string key, string file)[] EnemyShips =
        {
            ("enemy_popcorn", "ship-026"), ("enemy_dart", "ship-012"), ("enemy_fighter", "ship-008"),
            ("enemy_violet", "ship-023"), ("enemy_gunship", "ship-018"), ("enemy_heavy", "ship-032"),
            ("enemy_carrier", "ship-003"), ("enemy_wing", "ship-015"),
            ("enemy_boss", "ship-035"),
            // Stage 2 and 3's new ships, and their bosses.
            ("enemy_hornet", "ship-039"), ("enemy_lancer", "ship-034"), ("enemy_raptor", "ship-027"),
            ("enemy_frigate", "ship-030"), ("enemy_scorch", "ship-036"), ("enemy_specter", "ship-024"),
            ("enemy_cobra", "ship-006"),
            ("enemy_boss2", "ship-025"), ("enemy_boss3", "ship-043"),
        };

        [MenuItem("Tools/ARcade/Build Time Crystal Art")]
        public static string Build()
        {
            var art = AssetDatabase.LoadAssetAtPath<TcArt>(AssetPath);
            if (!art)
            {
                art = ScriptableObject.CreateInstance<TcArt>();
                AssetDatabase.CreateAsset(art, AssetPath);
            }

            var strips = new List<TcArt.Strip>();
            foreach (string pack in new[] { "Super Pixel Projectiles Pack 1", "Super Pixel Projectiles Pack 2" })
            {
                string dir = Root + pack + "/spritesheet";
                foreach (string sub in Directory.GetDirectories(dir))
                {
                    string name = Path.GetFileName(sub);
                    bool wanted = false;
                    foreach (string t in StripTypes) if (name.StartsWith(t + "_")) wanted = true;
                    if (!wanted) continue;
                    string txt = sub + "/spritesheet.txt", png = sub + "/spritesheet.png";
                    if (!File.Exists(txt) || !File.Exists(png)) continue;
                    // "name/frame0000.png = x y w h"
                    var parts = File.ReadAllLines(txt)[0].Split('=')[1].Trim().Split(' ');
                    strips.Add(new TcArt.Strip { key = name, texture = Texture(png), frameWidth = int.Parse(parts[2]), frameHeight = int.Parse(parts[3]) });
                }
            }

            // The bosses' gems (ENDLESS KNIGHT's loot strips, 9 frames of 28 px):
            // emeralds from the first, blue diamonds from the second, red from the third.
            foreach (var (key, file) in new[] { ("gem_emerald", "GemGreenDiamond"), ("gem_blue", "GemBlueDiamond"), ("gem_red", "GemredDiamond") })
                strips.Add(new TcArt.Strip { key = key, texture = Texture("Assets/PitDemo/SPRITES/LOOT/" + file + ".png"), frameWidth = 28, frameHeight = 28 });

            // Coins (the "cute coins" sheet, 4 turning frames of 10 px): enemies' bullets turn into them.
            foreach (string colour in new[] { "gold", "green", "blue", "pink" })
                strips.Add(new TcArt.Strip { key = "coin_" + colour, texture = Texture("Assets/TimeCrystalDemo/ARCADE/coin_" + colour + ".png"), frameWidth = 10, frameHeight = 10 });

            // THE TIME CRYSTAL itself (Creativekind's Magic Crystal, light blue: 12 frames of 64 px) and its
            // shattering (Magic Crystal Destruction: cracks, a white flash, gone to dust), and a blue copy of
            // ENDLESS KNIGHT's bomb blast (21 frames of 112 px in a row) for the crystal going up.
            strips.Add(new TcArt.Strip { key = "time_crystal", texture = Texture("Assets/TimeCrystalDemo/ARCADE/time_crystal.png"), frameWidth = 64, frameHeight = 64 });
            strips.Add(new TcArt.Strip { key = "time_crystal_break", texture = Texture("Assets/TimeCrystalDemo/ARCADE/time_crystal_break.png"), frameWidth = 64, frameHeight = 64 });
            strips.Add(new TcArt.Strip { key = "boom_blue", texture = Texture("Assets/TimeCrystalDemo/ARCADE/explosion_blue.png"), frameWidth = 112, frameHeight = 112 });
            // ...and the crystal of each later loop, its own colour (the pack's purple, green, red),
            // its blast that colour (the blue one, hue-shifted) and its diamonds (the purple: the blue gem's, shifted).
            foreach (string colour in new[] { "purple", "green", "red" })
            {
                strips.Add(new TcArt.Strip { key = "time_crystal_" + colour, texture = Texture("Assets/TimeCrystalDemo/ARCADE/time_crystal_" + colour + ".png"), frameWidth = 64, frameHeight = 64 });
                strips.Add(new TcArt.Strip { key = "time_crystal_break_" + colour, texture = Texture("Assets/TimeCrystalDemo/ARCADE/time_crystal_break_" + colour + ".png"), frameWidth = 64, frameHeight = 64 });
                strips.Add(new TcArt.Strip { key = "boom_" + colour, texture = Texture("Assets/TimeCrystalDemo/ARCADE/explosion_" + colour + ".png"), frameWidth = 112, frameHeight = 112 });
            }
            // The story's divider (Pixel UI & HUD 4's white DividerC: 48 x 1, tinted neon where it's used).
            strips.Add(new TcArt.Strip { key = "divider", texture = Texture("Assets/TimeCrystalDemo/ARCADE/divider.png"), frameWidth = 48, frameHeight = 1 });
            strips.Add(new TcArt.Strip { key = "gem_purple", texture = Texture("Assets/TimeCrystalDemo/ARCADE/gem_purple.png"), frameWidth = 28, frameHeight = 28 });

            var seqs = new List<TcArt.Sequence>();
            // The pilots' jets (TcPilots: ship NOVA, ship01 BLAZE, ship02 VIPER, ship03 RIO). BLAZE's Plane 08A and
            // VIPER's Plane 07B from the pack (1-4 level - 5-8 lean a little: the idle rocked side to side - 9-12
            // banking left, 17-20 right), their rolls made from their level frame (the pack has no spin for
            // them); NOVA's ship-037 and RIO's ship-041 (SHMUP 2 stills) given all of theirs:
            // ARCADE/Pilots, unity-helpers/pilot_frames.py.
            // (VIPER's frames copies with its drawn exhaust neon green, not red.)
            foreach (var (key, plane) in new[] { ("ship01", Ships + "Plane 08/planes_08A_"), ("ship02", Pilots + "plane07B-") })
            {
                seqs.Add(Seq(key + "_idle", Range(plane, 1, 4)));
                seqs.Add(Seq(key + "_bank_a", Range(plane, 9, 12)));
                seqs.Add(Seq(key + "_bank_b", Range(plane, 17, 20)));
            }
            seqs.Add(Seq("ship01_roll", Range(Pilots + "plane08A_roll-", 1, 8)));
            seqs.Add(Seq("ship02_roll", Range(Pilots + "plane07B_roll-", 1, 8)));
            foreach (var (key, made) in new[] { ("ship", "ship037"), ("ship03", "ship041") })
            {
                seqs.Add(Seq(key + "_idle", Range(Pilots + made + "_idle-", 1, 4)));
                seqs.Add(Seq(key + "_bank_a", Range(Pilots + made + "_bank_a-", 1, 4)));
                seqs.Add(Seq(key + "_bank_b", Range(Pilots + made + "_bank_b-", 1, 4)));
                seqs.Add(Seq(key + "_roll", Range(Pilots + made + "_roll-", 1, 8)));
            }
            // The pilots' faces (Choose Your Fighter, the HUD).
            foreach (var (key, file) in new[] { ("portrait_nova", "female-01"), ("portrait_blaze", "female-02"), ("portrait_viper", "male-01"), ("portrait_rio", "male-02") })
                seqs.Add(Seq(key, new[] { Ships + "Character Portrait/" + file + ".png" }));
            // Their mini jets (the HUD's lives).
            seqs.Add(Seq("ship_icon", new[] { Pilots + "ship037_icon.png" }));
            seqs.Add(Seq("ship01_icon", new[] { Ships + "Icons/icon-plane-08.png" }));
            seqs.Add(Seq("ship02_icon", new[] { Ships + "Icons/icon-plane-07.png" }));
            seqs.Add(Seq("ship03_icon", new[] { Pilots + "ship041_icon.png" }));
            // VIPER's bomb: a swarm of green drones.
            seqs.Add(Seq("drone_green", new[] { Ships + "Support Units/support_units-green.png", Ships + "Support Units/support_units-green-01.png", Ships + "Support Units/support_units-green-02.png" }));
            seqs.Add(Seq("option", new[] { Ships + "Support Units/support_units-blue.png", Ships + "Support Units/support_units-blue-01.png", Ships + "Support Units/support_units-blue-02.png" }));
            // The medium bomber: Plane 04B (blue, its thrusters flickering) - Plane 08 and 07 are BLAZE's and VIPER's now.
            seqs.Add(Seq("bomber", Range(Ships + "Plane 04/planes_04B-", 1, 4)));   // (level frames only: 5-8 lean, it rocked)
            // ...and the violet striker, Plane 05B.
            seqs.Add(Seq("striker", Range(Ships + "Plane 05/planes_05B-", 1, 4)));   // (level frames only)
            seqs.Add(Seq("explosion_small", Range(Ships + "Explosion/Small/explosion-", 1, 11)));
            seqs.Add(Seq("explosion_large", Range(Ships + "Explosion/Large/explosion_large-", 1, 10)));
            seqs.Add(Seq("white_blast", Range(Ships + "Explosion/White Blast/white_blast-", 1, 10)));
            seqs.Add(Seq("bubble", Range(Ships + "Shield/shield-", 1, 6)));
            foreach (var (key, file) in EnemyShips) seqs.Add(Seq(key, new[] { Shmup + file + ".png" }));

            art.strips = strips.ToArray();
            art.digitalFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/PitDemo/DigitalNumbers-Regular SDF 1.asset");
            // The pilots' lasers (Hovl's 3D Lasers Pack): NOVA crystal, BLAZE red, VIPER soul, RIO fire.
            art.lasers = new[] { "6 crystal", "4 red", "8 soul", "10 fire" }.Select(n =>
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/3D Lasers Pack/Prefabs/Laser beam " + n + ".prefab")).ToArray();
            // The pilots' engine flames (Hovl's Jet engine effects; BLAZE's, VIPER's and RIO's tinted red, green, orange: TcJets).
            art.jets = new[] { "Jet flames type 9", "Jet flames type 10", "Jet flames type 8", "Jet flames type 10" }.Select(n =>
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Jet engine effects/Prefabs/" + n + ".prefab")).ToArray();
            art.zone2 = Zone("zone2");
            art.zone3 = Zone("zone3");
            art.sequences = seqs.ToArray();
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            int missing = 0;
            foreach (var q in seqs) foreach (var f in q.frames) if (!f) missing++;
            return $"TcArt: {strips.Count} strips, {seqs.Count} sequences, {missing} missing frames";
        }

        // A zone's six space layers (composed from ScifiSpaceAssetsNAv1 by
        // unity-helpers/tc_zones_compose.py), imported as stage 1's are
        // (TimeCrystalBuilder.ImportSpaceArt: 640 px across the picture).
        static Sprite[] Zone(string name)
        {
            var sprites = new Sprite[6];
            for (int i = 0; i < 6; i++)
            {
                string path = $"Assets/TimeCrystalDemo/SPRITES/ScifiSpaceAssetsNAv1/Zones/{name}_bg{i + 1}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) { Debug.LogWarning("TcArtBuilder: missing " + path); continue; }
                float ppu = 640f / SpatialEmulator.EditorTools.TimeCrystalBuilder.FieldWidth;
                if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                    || !Mathf.Approximately(importer.spritePixelsPerUnit, ppu) || importer.filterMode != FilterMode.Point
                    || importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp)
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
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return sprites;
        }

        static string[] Range(string prefix, int from, int to)
        {
            var list = new List<string>();
            for (int i = from; i <= to; i++) list.Add(prefix + i + ".png");
            return list.ToArray();
        }

        static TcArt.Sequence Seq(string key, string[] files)
        {
            var frames = new Texture2D[files.Length];
            for (int i = 0; i < files.Length; i++) frames[i] = Texture(files[i]);
            return new TcArt.Sequence { key = key, frames = frames };
        }

        static Texture2D Texture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) { Debug.LogWarning("TcArtBuilder: missing " + path); return null; }
            bool changed = false;
            if (importer.isReadable) { importer.isReadable = false; changed = true; }
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.maxTextureSize < 4096) { importer.maxTextureSize = 4096; changed = true; }   // (a long strip - the blue blast's 2352 px - mustn't shrink)
            if (importer.npotScale != TextureImporterNPOTScale.None && importer.textureType != TextureImporterType.Sprite) { importer.npotScale = TextureImporterNPOTScale.None; changed = true; }
            var ios = importer.GetPlatformTextureSettings("iPhone");
            if (ios.overridden && ios.format != TextureImporterFormat.RGBA32) { ios.format = TextureImporterFormat.RGBA32; importer.SetPlatformTextureSettings(ios); changed = true; }
            if (changed) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
