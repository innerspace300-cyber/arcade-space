// EndlessKnightBuilder.cs — sets up ENDLESS KNIGHT's gameplay assets:
// slices the free spike trap sheet (SPRITES/TRAPS/Trap_Spike.png, 27 frames
// of 128 px), makes the SPIKE TRAP prefab, and adds EndlessKnightDirector to
// the PitDemo prefab. The spikes sit sunk into the road: each frame's bottom
// SinkPixels rows are cut off (they'd be underground) and the cut edge is
// the pivot, on the road, so the idle frames show only the tips.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class EndlessKnightBuilder
{
    const string SpikeSheet = "Assets/PitDemo/SPRITES/TRAPS/Trap_Spike.png";
    const string SpikePrefab = "Assets/PitDemo/SPRITES/TRAPS/SPIKE TRAP.prefab";
    const string DemoPrefab = "Assets/PitDemo/PIT DEMO EXPORT/PitDemo.prefab";
    const string BloodSheet = "Assets/PitDemo/SPRITES/FX/BloodEruption.png";
    const string BloodPrefab = "Assets/PitDemo/SPRITES/FX/BLOOD ERUPTION.prefab";
    const string ExplosionSheet = "Assets/PitDemo/SPRITES/FX/Explosion.png";
    const string ExplosionPrefab = "Assets/PitDemo/SPRITES/FX/EXPLOSION.prefab";
    const string BombWalk = "Assets/PitDemo/SPRITES/BOMB/bomb_character_o_walk.png";
    const string BombExplode = "Assets/PitDemo/SPRITES/BOMB/bomb_character_o_explode.png";
    const string BombPrefab = "Assets/PitDemo/SPRITES/BOMB/BOMB GUY.prefab";
    const string BombSound = "Assets/PitDemo/AUDIO/Arcade Game Explosion 03.wav";
    const string SplashSheet = "Assets/PitDemo/SPRITES/FX/SlimeSplash.png";
    const string ScatterSheet = "Assets/PitDemo/SPRITES/FX/SlimeScatter.png";
    const string SplatPrefab = "Assets/PitDemo/SPRITES/FX/SLIME SPLAT.prefab";
    // Sheet (under SPRITES/FX), frames used, and the row (from the top of its
    // 64 px cells) where the blood lands.
    static readonly (string sheet, int frames, int ground)[] SlimeBursts =
    {
        ("SlimeBoil", 10, 39),
        ("SlimeEssence", 7, 52),
        ("SlimeBurst11", 18, 64),
        ("SlimeBurst15", 18, 64),
        ("SlimeBurst16", 17, 64),
    };
    const string BloodRowsFolder = "Assets/PitDemo/SPRITES/FX/SLIME BLOOD";
    static readonly string[] SplatSounds =
    {
        "Assets/PitDemo/Retro Arsenal/Sound/Gore/retro_bloodbig.wav",
        "Assets/PitDemo/Retro Arsenal/Sound/Gore/retro_bloodbig_bone.wav",
        "Assets/PitDemo/AUDIO/Blood Squirting Weapon Impact.mp3",
    };
    const string ChestSheet = "Assets/PitDemo/SPRITES/CHEST/Chests.png";
    const string ChestPrefab = "Assets/PitDemo/SPRITES/CHEST/TREASURE CHEST.prefab";
    const string ChestBurst = "Assets/PitDemo/Retro Arsenal/Prefabs/Combat/Explosions/Sparkle/SparkleExplosion ORANGE.prefab";
    const string GemSheet = "Assets/PitDemo/SPRITES/GEMS/Gems.png";
    const string GemFolder = "Assets/PitDemo/SPRITES/GEMS";
    const string GemSound = "Assets/PitDemo/AUDIO/Diamond Collect.mp3";
    const string ChestCoinSound = "Assets/PitDemo/AUDIO/Treasure Gold Coins.mp3";
    const string SlimeBossPrefab = "Assets/PitDemo/PREFABS/SLIME BOSS.prefab";
    const string ChestLookFolder = "Assets/PitDemo/SPRITES/CHEST/DESIRE FANTASY";
    static readonly int[] ChestOrder = { 5, 3, 4, 6, 8, 10, 11 };   // not 1, 2, 7, 9 or 12
    // Each sheet's bottom row of art (measured), Chest01-12.
    static readonly int[] ChestBottoms = { 63, 59, 61, 61, 59, 60, 61, 61, 61, 61, 65, 62 };
    const string HueShader = "SpatialEmulator/SpriteHueShift";
    const string HueMaterialPath = "Assets/PitDemo/SPRITES/FX/SLIME HUE.mat";

    // What the chests hold, one kind each, in turn by boss: Diamonds & Gems'
    // gems (28 px spin frames, SPRITES/LOOT/Gem*.png) and scrimsy's fruit (16 px
    // icons, SPRITES/LOOT/Fruit*.png, one colour per chest), which
    // heal like the orange slices.
    const string LootFolder = "Assets/PitDemo/SPRITES/LOOT";
    const string FruitSound = "Assets/PitDemo/AUDIO/8bit Up 19.mp3";                // picking one up: the orange slices' sound
    const string FruitEmitSound = "Assets/PitDemo/AUDIO/Magic Coin Collect.wav";   // each fruit flying out
    static readonly string[] BossHurtSounds =
    {
        "Assets/PitDemo/AUDIO/CREATURE_MONSTER_Hit_Gurgle_Low_01.wav",
        "Assets/PitDemo/AUDIO/Dinosaur Groan.mp3",
    };
    static readonly string[] BossDeathSounds =
    {
        "Assets/PitDemo/AUDIO/Zombie Splat Hit 04.wav",
        "Assets/PitDemo/AUDIO/Zombie Death Shout With Bodyfall.wav",
    };

    const string BossArt = "Assets/PitDemo/SPRITES/Fantasy #7/Fantasy #7/";

    // A sliced sheet's sprites by their number (name_N), in order.
    static IEnumerable<Sprite> SheetFrames(string path, params int[] indices)
    {
        var byIndex = new Dictionary<int, Sprite>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Sprite sp && int.TryParse(sp.name.Substring(sp.name.LastIndexOf('_') + 1), out int n)) byIndex[n] = sp;
        foreach (int i in indices)
            if (byIndex.TryGetValue(i, out var sp)) yield return sp;
    }

    static AudioClip[] Clips(string[] paths) => System.Array.ConvertAll(paths, AssetDatabase.LoadAssetAtPath<AudioClip>);
    // The things a chest can hold: gems (spinning) and fruit (still icons in
    // the colours listed, by their place in the sheet's alphabetical colour
    // order: aqua, blue, green, orange, pink, purple, red, white, yellow).
    static readonly (string name, string sheet, bool fruit, int[] icons)[] LootItems =
    {
        ("EMERALD", "GemGreenDiamond", false, null),
        ("SAPPHIRE", "GemBlueDiamond", false, null),
        ("RUBY", "GemredDiamond", false, null),
        ("PINK GEM", "GemPinkGem", false, null),
        ("AMETHYST", "GemPurpleGem1", false, null),
        ("BANANA", "Fruitbanana", true, new[] { 6, 7, 16, 17 }),   // orange and yellow, single and bunch
        ("APPLE", "Fruitapple", true, new[] { 6 }),                 // red
        ("CHERRY", "Fruitcherry", true, new[] { 6 }),               // red
        ("PEACH", "Fruitpeach", true, new[] { 4 }),                 // pink
        ("STRAWBERRY", "Fruitstrawberry", true, new[] { 4, 6 }),    // pink and red
    };

    // Each chest's contents, in turn by boss: two kinds of gem, or one fruit
    // (half as many) - then round again.
    static readonly (string name, string[] items)[] LootSets =
    {
        ("EMERALDS + SAPPHIRES", new[] { "EMERALD", "SAPPHIRE" }),
        ("BANANAS", new[] { "BANANA" }),
        ("RUBIES + PINK GEMS", new[] { "RUBY", "PINK GEM" }),
        ("APPLES", new[] { "APPLE" }),
        ("AMETHYSTS + EMERALDS", new[] { "AMETHYST", "EMERALD" }),
        ("CHERRIES", new[] { "CHERRY" }),
        ("SAPPHIRES + RUBIES", new[] { "SAPPHIRE", "RUBY" }),
        ("PEACHES", new[] { "PEACH" }),
        ("PINK GEMS + AMETHYSTS", new[] { "PINK GEM", "AMETHYST" }),
        ("STRAWBERRIES", new[] { "STRAWBERRY" }),
    };

    static TreasureChest.Loot[] MakeLoot(int layer)
    {
        var prefabs = new Dictionary<string, GameObject>();
        var fruit = new HashSet<string>();
        foreach (var kind in LootItems)
        {
            string sheet = $"{LootFolder}/{kind.sheet}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(sheet);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            int cell = kind.fruit ? 16 : 28;
            var frames = SliceGrid(sheet, cell, cell, width / cell, 0, kind.fruit ? 0f : 0.1f);
            if (kind.icons != null) frames = System.Array.ConvertAll(kind.icons, i => frames[i]);
            var go = new GameObject(kind.name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            sr.sortingLayerName = "TopLayer";
            sr.sortingOrder = -44; // in front of the player
            go.layer = layer;
            var item = go.AddComponent<ChestGem>();
            item.frames = frames;
            if (kind.fruit)
            {
                fruit.Add(kind.name);
                item.fps = 0f;          // a still icon in one of its colours
                item.scale = 0.8f;      // 16 px icons about the gems' size
                item.healAmount = 5;    // like an orange slice
                item.collectSound = AssetDatabase.LoadAssetAtPath<AudioClip>(FruitSound);
            }
            else item.collectSound = AssetDatabase.LoadAssetAtPath<AudioClip>(GemSound);
            prefabs[kind.name] = PrefabUtility.SaveAsPrefabAsset(go, $"{LootFolder}/{kind.name}.prefab");
            Object.DestroyImmediate(go);
        }
        var loot = new List<TreasureChest.Loot>();
        foreach (var set in LootSets)
            loot.Add(new TreasureChest.Loot
            {
                name = set.name,
                items = System.Array.ConvertAll(set.items, n => prefabs[n]),
                rate = fruit.Contains(set.items[0]) ? 0.5f : 1f,
                openSound = null,   // (a fruit chest has just the lid's sound now: TreasureChest.lidSounds)
                emitSound = fruit.Contains(set.items[0]) ? AssetDatabase.LoadAssetAtPath<AudioClip>(FruitEmitSound) : null,
            });
        return loot.ToArray();
    }

    static Material MakeHueMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(HueMaterialPath);
        if (!material)
        {
            material = new Material(Shader.Find(HueShader));
            AssetDatabase.CreateAsset(material, HueMaterialPath);
        }
        return material;
    }
    const int SinkPixels = 28;
    // Top row of each frame's art in the 128 px cell (measured from the sheet).
    static readonly int[] TopRow = { 92, 93, 92, 93, 93, 93, 93, 82, 54, 54, 54, 54, 54, 54, 54, 58, 62, 66, 69, 74, 78, 87, 93, 92, 92, 92, 92 };

    [MenuItem("Tools/Spatial Emulator/Build Endless Knight Gameplay")]
    public static string Build()
    {
        // Blood FX 2.0's BloodEruption: 10 frames of 64 px, its ground line 12 px up.
        var blood = MakeFx(BloodSheet, BloodPrefab, 64, 10, 12f / 64f, 20f);
        // explosion.zip: 21 frames of 112 px, on the ground (for the bomb guys).
        var explosion = MakeFx(ExplosionSheet, ExplosionPrefab, 112, 21, 0f, 24f);

        // The Slime Boss's death: Blood FX 1.0 sheets 5 and 16 (35 frames of
        // 64 px) recoloured purple like the boss - a splash to each side and a scatter,
        // standing on the road, drawn over everything on the characters' layer
        // (THE PIT's particle burst drew behind the sprites).
        var splash = SliceGrid(SplashSheet, 64, 64, 35, 0, 0f);
        var scatter = SliceGrid(ScatterSheet, 64, 64, 35, 0, 0f);
        int actorsLayer = LayerMask.NameToLayer("PitActors");
        var splatGo = new GameObject("SLIME SPLAT");
        var splatFx = AddFx(splatGo, scatter, 30f);
        splatFx.characterScale = 1.6f;
        splatFx.snapToRoad = true;
        foreach (bool flip in new[] { false, true })
        {
            var side = new GameObject(flip ? "Splash Left" : "Splash Right");
            side.transform.SetParent(splatGo.transform, false);
            AddFx(side, splash, 30f).lockToRoad = false;   // moves with the root
            side.GetComponent<SpriteRenderer>().flipX = flip;
        }
        foreach (var t in splatGo.GetComponentsInChildren<Transform>()) t.gameObject.layer = actorsLayer;
        var splat = PrefabUtility.SaveAsPrefabAsset(splatGo, SplatPrefab);
        Object.DestroyImmediate(splatGo);
        // Its death bursts: Blood FX 2.0's BloodBoil, BloodEssence, 11, 15 and
        // 16 recoloured purple (SPRITES/FX/Slime*.png), 64 px cells, each
        // pivoted on the row where its blood lands, so they all stand on the
        // road (SlimeBurst* fall from the cell's top onto its bottom row).
        if (!AssetDatabase.IsValidFolder(BloodRowsFolder)) AssetDatabase.CreateFolder("Assets/PitDemo/SPRITES/FX", "SLIME BLOOD");
        foreach (string old in AssetDatabase.FindAssets("t:Prefab", new[] { BloodRowsFolder }))
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(old));
        var sprays = new GameObject[SlimeBursts.Length];
        for (int r = 0; r < sprays.Length; r++)
        {
            var burst = SlimeBursts[r];
            var burstFrames = SliceGrid($"Assets/PitDemo/SPRITES/FX/{burst.sheet}.png", 64, 64, burst.frames, 0, (64f - burst.ground) / 64f);
            var sprayGo = new GameObject("SLIME " + burst.sheet.Substring(5).ToUpperInvariant());
            var fx = AddFx(sprayGo, burstFrames, 18f);
            fx.snapToRoad = true;
            sprayGo.layer = actorsLayer;
            sprays[r] = PrefabUtility.SaveAsPrefabAsset(sprayGo, $"{BloodRowsFolder}/{sprayGo.name}.prefab");
            Object.DestroyImmediate(sprayGo);
        }
        var sounds = new List<AudioClip>();
        foreach (string path in SplatSounds)
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) is AudioClip clip) sounds.Add(clip);
        // The treasure chest: THE PIT's Animated Chests sheet, 48 x 32 px
        // cells, 5 per row; the gold chest's wiggle is row 4 and its opening
        // row 5. The chest sits in the left 32 px of its cell, on the bottom.
        var chestFrames = SliceGrid(ChestSheet, 48, 32, 40, 0, 0f, 16f / 48f);
        // Its gems: Diamonds & Gems' red and green diamonds and red and blue
        // gems, shrunk to their real 28 px, 9 spin frames each, one per row.
        var gemFrames = SliceGrid(GemSheet, 28, 28, 36, 0, 0.1f);
        string[] gemNames = { "RED DIAMOND", "GREEN DIAMOND", "RED GEM", "BLUE GEM" };
        var gems = new GameObject[gemNames.Length];
        for (int g = 0; g < gems.Length; g++)
        {
            var gemGo = new GameObject(gemNames[g]);
            var gemSr = gemGo.AddComponent<SpriteRenderer>();
            gemSr.sprite = gemFrames[g * 9];
            gemSr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            gemSr.sortingLayerName = "TopLayer";
            gemSr.sortingOrder = -44; // in front of the player
            gemGo.layer = actorsLayer;
            var gem = gemGo.AddComponent<ChestGem>();
            gem.frames = gemFrames[(g * 9)..((g + 1) * 9)];
            gem.collectSound = AssetDatabase.LoadAssetAtPath<AudioClip>(GemSound);
            gems[g] = PrefabUtility.SaveAsPrefabAsset(gemGo, $"{GemFolder}/{gemNames[g]}.prefab");
            Object.DestroyImmediate(gemGo);
        }

        var chestGo = new GameObject("TREASURE CHEST");
        var chestSr = chestGo.AddComponent<SpriteRenderer>();
        chestSr.sprite = chestFrames[20];
        chestSr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        chestSr.sortingLayerName = "TopLayer"; // the player's layer (Default draws over it)
        chestSr.sortingOrder = -47;            // just behind the player
        chestGo.layer = actorsLayer;
        var chest = chestGo.AddComponent<TreasureChest>();
        chest.idleFrames = chestFrames[20..25];
        chest.openFrames = chestFrames[25..30];
        chest.burstPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestBurst);
        chest.coinSound = AssetDatabase.LoadAssetAtPath<AudioClip>(ChestCoinSound);
        chest.fairyKickSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/PitDemo/AUDIO/Door IN Fantasy Word1.wav");
        chest.lidSounds = new[] { 1, 2, 3 }.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/PitDemo/AUDIO/Loot Chest Open {n}.mp3")).Where(c => c).ToArray();
        chest.gemPrefabs = gems;
        // DesireFantasy's 12 chests: 6 frames of 50 x 66 each (shut, the lid's
        // flash, open); each stands on its own bottom row. Chest05 first.
        var looks = new List<TreasureChest.Look>();
        foreach (int n in ChestOrder)
        {
            string sheet = $"{ChestLookFolder}/Chest{n:00}.png";
            looks.Add(new TreasureChest.Look { frames = SliceGrid(sheet, 50, 66, 6, 0, (66f - ChestBottoms[n - 1]) / 66f) });
        }
        chest.looks = looks.ToArray();
        chest.loot = MakeLoot(actorsLayer);
        chest.scale = 1.2f * 1.15f;   // their 26 px chests about the old 32 px one's size, and 15% more
        chest.openFps = 10f;
        var chestPrefab = PrefabUtility.SaveAsPrefabAsset(chestGo, ChestPrefab);
        Object.DestroyImmediate(chestGo);

        using (var scope = new PrefabUtility.EditPrefabContentsScope(SlimeBossPrefab))
        {
            scope.prefabContentsRoot.GetComponent<EnemyStateMachineSlimeBoss>().chestPrefab = chestPrefab;
            var boss = scope.prefabContentsRoot.GetComponent<EnemyStateMachineSlimeBoss>();
            boss.deathParticlePrefab = splat;
            boss.deathSplats = sprays;
            boss.deathSplatSounds = sounds.ToArray();
            boss.deathSplatCount = 7;
            boss.deathSplatTime = 0.8f;
            boss.attack2ParticlePrefab = null;   // the purple poison cloud on its hits
            boss.hurtSounds = Clips(BossHurtSounds);
            // The frames it strikes on, by swing: Attack 1's slash (frames 3,
            // 4) and Attack 2's swing (5, 6 - one swing over two frames).
            // THE PIT's Attack 1 clip runs Attack 2's frames after its own,
            // then Attack 2 plays: three swings, three hits.
            var strikes = new List<Sprite>();
            strikes.AddRange(SheetFrames(BossArt + "attack1.png", 3, 4));
            strikes.AddRange(SheetFrames(BossArt + "attack2.png", 5, 6));
            boss.strikeFrames = strikes.ToArray();
            boss.strikeSwings = new[] { 0, 0, 1, 1 };
            boss.deathSounds = Clips(BossDeathSounds);
        }

        // The bomb guy: 64 px frames, feet 13 px up; explode frame 0 is the lit bomb.
        var walk = SliceGrid(BombWalk, 64, 64, 6, 0, 13f / 64f);
        var lit = SliceGrid(BombExplode, 64, 64, 4, 0, 13f / 64f);
        var bombGo = new GameObject("BOMB GUY");
        var bombSr = bombGo.AddComponent<SpriteRenderer>();
        bombSr.sprite = walk[0];
        bombSr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        bombSr.sortingLayerName = "TopLayer"; // the player's (Default draws over it)
        bombSr.sortingOrder = -46;            // behind the player
        var bomb = bombGo.AddComponent<BombGuy>();
        bomb.walkFrames = walk;
        bomb.fuseFrame = lit[0];
        bomb.explosionPrefab = explosion;
        bomb.explosionSound = AssetDatabase.LoadAssetAtPath<AudioClip>(BombSound);
        var bombPrefab = PrefabUtility.SaveAsPrefabAsset(bombGo, BombPrefab);
        Object.DestroyImmediate(bombGo);

        // THE PIT's particle effects (the Slime Boss's hits and death burst)
        // draw in front of the sprites.
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/PitDemo" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset.GetComponentInChildren<ParticleSystemRenderer>(true)) continue;
            using var scope = new PrefabUtility.EditPrefabContentsScope(path);
            foreach (var r in scope.prefabContentsRoot.GetComponentsInChildren<ParticleSystemRenderer>(true))
                if (r.sortingOrder < 100) r.sortingOrder = 100;
        }

        var frames = SliceGrid(SpikeSheet, 128, 128, 27, SinkPixels);
        var tips = new int[TopRow.Length];
        for (int i = 0; i < tips.Length; i++) tips[i] = Mathf.Max(0, 128 - TopRow[i] - SinkPixels);

        var go = new GameObject("SPIKE TRAP");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        sr.sortingLayerName = "TopLayer"; // the player's (Default draws over it)
        sr.sortingOrder = -47;            // behind the player (THE PIT sorts characters by -z * 10)
        var trap = go.AddComponent<SpikeTrap>();
        trap.frames = frames;
        trap.tipPixels = tips;
        trap.bloodPrefab = blood;
        PrefabUtility.SaveAsPrefabAsset(go, SpikePrefab);
        Object.DestroyImmediate(go);

        using (var scope = new PrefabUtility.EditPrefabContentsScope(DemoPrefab))
        {
            var root = scope.prefabContentsRoot;
            var director = root.GetComponent<EndlessKnightDirector>();
            if (!director) director = root.AddComponent<EndlessKnightDirector>();
            director.spikePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpikePrefab);
            director.bombPrefab = bombPrefab;
            director.bossScore = 1000;
            director.bossScoreStep = 1000;
            director.bossHueMaterial = MakeHueMaterial();
            director.quietAfterBoss = 1f;
            SetUpRespawn(root);
            SetUpHudFrame(root);
            SetUpHudLayout(root);
            SetUpHudAccents(root);
            SetUpHudJuice(root);
            SetUpPlayerGlow(root);
            SetUpFruitTracker(root);
            LiftCampfire(root);
            SetUpFairy(root);
            SetUpGaits(root);
        }
        AssetDatabase.SaveAssets();
        return frames.Length + " spike frames";
    }

    // The respawn (DeathRespawn): the witch is THE PIT's AITHNE (SPRITES/
    // AITHNE); her idle frames come from her idle clip's sprite keys, and her
    // size and draw order from her sprite in the demo (hidden under Player).
    const string WitchFolder = "Assets/PitDemo/SPRITES/AITHNE";

    static void SetUpRespawn(GameObject demo)
    {
        var respawn = demo.GetComponent<DeathRespawn>();
        if (!respawn) respawn = demo.AddComponent<DeathRespawn>();

        AnimationClip idle = null;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { WitchFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview") && clip.name.ToLowerInvariant().Contains("idle")
                    && (idle == null || clip.name.Length < idle.name.Length))
                    idle = clip;
        }
        var frames = new List<Sprite>();
        if (idle)
        {
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(idle))
            {
                if (binding.type != typeof(SpriteRenderer)) continue;
                foreach (var key in AnimationUtility.GetObjectReferenceCurve(idle, binding))
                    if (key.value is Sprite s) frames.Add(s);
                respawn.witchFps = frames.Count > 0 && idle.length > 0f ? frames.Count / idle.length : 10f;
                break;
            }
        }
        respawn.witchFrames = frames.ToArray();

        // Salamander Witch (the same witch): her UpCast, on her idle frames'
        // 64 px canvas and centre pivot, and the Thunder Strikes she calls
        // down - 26 frames of 150 x 128, the bolt landing 3.5 px up at x 73.5.
        respawn.castFrames = SliceGrid(WitchCastSheet, 64, 64, 15, 0, 0.5f);
        respawn.arriveStrike = MakeStrike(YellowStrikeSheet, YellowStrikePrefab);
        respawn.strike = MakeStrike(BlueStrikeSheet, BlueStrikePrefab);
        respawn.strikeSound = AssetDatabase.LoadAssetAtPath<AudioClip>(StrikeSound);
        Debug.Log($"[EndlessKnightBuilder] witch idle: {(idle ? idle.name : "none")}, {frames.Count} frames at {respawn.witchFps:F1} fps");

        foreach (var r in demo.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (r.name != "AITHNE") continue;
            respawn.witchScale = r.transform.lossyScale;
            break;
        }
        // Over everything on the road - the Slime Boss, traps and bomb guys
        // (Default draws over TopLayer); the strikes go over her.
        respawn.witchSortingLayer = "Default";
        respawn.witchSortingOrder = 200;
    }

    const string WitchFx = "Assets/PitDemo/SPRITES/SALAMANDER WITCH";
    const string WitchCastSheet = WitchFx + "/WitchUpCast.png";
    const string BlueStrikeSheet = WitchFx + "/ThunderStrikeBlue.png";
    const string BlueStrikePrefab = WitchFx + "/THUNDER STRIKE BLUE.prefab";
    const string YellowStrikeSheet = WitchFx + "/ThunderStrikeYellow.png";
    const string YellowStrikePrefab = WitchFx + "/THUNDER STRIKE YELLOW.prefab";
    const string StrikeSound = "Assets/PitDemo/AUDIO/Fighting_Game_Designed_Defense_Spell_Lightning_Cast_Magic_Crackling_Zap_Strike_Whoosh_02.wav";

    static PitSpriteFx MakeStrike(string sheet, string prefabPath)
    {
        // Its pivot 10 px up from the glow's bottom, at the bolt's bright
        // impact: that lands on the road, where her feet are.
        var frames = SliceGrid(sheet, 150, 128, 26, 0, 13.5f / 128f, 73.5f / 150f);
        var go = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
        AddFx(go, frames, 20f);
        go.GetComponent<SpriteRenderer>().sortingOrder = 210;   // over the witch and everything on the road
        go.layer = LayerMask.NameToLayer("PitActors");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<PitSpriteFx>();
    }

    // The fairy (FairyAlly): three looks from Fairy.zip (8 frames of 32 px)
    // - THE PIT's purple one first, then blue, then yellow - each with the
    // Super Pixel Projectiles Pack 4 small ice spike in its colour (8 frames
    // of 32 x 16, pointing right). She moves from the player's sprite, which
    // the attack animations move about, to the player's root, where she
    // stays put (her place on screen unchanged).
    const string FairyFolder = "Assets/PitDemo/SPRITES/Fairy";
    // Each has its own shot sound, and its hits burst in a colour to go with
    // its spikes: Blood FX 2.0's ClotBurst (9 frames of 64 px; from its 4th,
    // where the clot pops) recoloured pink, white (ice) and orange.
    static readonly (string fairy, string spike, string shot, string burst)[] FairyLooks =
    {
        ("Fairy 3", "IceSpike_violet", "ToyLaser_1zap.wav", "IceBurst_pink"),
        ("Fairy 1", "IceSpike_blue", "Laser Shot 80.wav", "IceBurst_white"),
        ("Fairy 2", "IceSpike_yellow", "Laser Beam.wav", "IceBurst_orange"),
    };
    static readonly string[] FairyHitSounds =
        { "Ice Punch 1.wav", "Ice Punch 2.wav", "Ice Punch 3.wav", "Ice Punch 4.wav", "Ice Punch 5.wav", "Ice Punch 6.wav" };
    const string FairyTapSound = "Assets/PitDemo/AUDIO/Magic Coin Collect.wav";
    const string FairyArriveSound = "Assets/PitDemo/AUDIO/Retro Fireball Magic Hyperspace Teleport Arrives.wav";

    static void SetUpFairy(GameObject demo)
    {
        var player = demo.GetComponentInChildren<PlayerScriptARIANAClips>(true);
        Transform fairy = null;
        foreach (var t in player.GetComponentsInChildren<Transform>(true))
            if (t.name == "FAIRY") { fairy = t; break; }
        if (!fairy) { Debug.LogError("[EndlessKnightBuilder] no FAIRY"); return; }
        if (fairy.parent != player.transform)
        {
            fairy.SetParent(player.transform, true);
            fairy.SetAsLastSibling();
        }

        var ally = fairy.GetComponent<FairyAlly>();
        if (!ally) ally = fairy.gameObject.AddComponent<FairyAlly>();
        var looks = new List<FairyAlly.Look>();
        foreach (var (name, spike, shot, burst) in FairyLooks)
        {
            string sheet = $"{FairyFolder}/{name}.png";
            // THE PIT's own sheet keeps its slices (its Animator's clip uses them).
            var frames = name == "Fairy 3"
                ? new List<Sprite>(SheetFrames(sheet, 0, 1, 2, 3, 4, 5, 6, 7)).ToArray()
                : SliceGrid(sheet, 32, 32, 8, 0, 0.5f, 0.5f);
            looks.Add(new FairyAlly.Look
            {
                frames = frames,
                spikeFrames = SliceGrid($"{FairyFolder}/ICE SPIKES/{spike}.png", 32, 16, 8, 0, 0.5f, 0.5f),
                shotSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/PitDemo/AUDIO/" + shot),
                // The clot sits 41 px down its 64 px cell.
                impactFrames = SliceGrid($"{FairyFolder}/ICE SPIKES/{burst}.png", 64, 64, 9, 0, 23f / 64f)[3..],
            });
        }
        ally.looks = looks.ToArray();
        ally.tapSound = AssetDatabase.LoadAssetAtPath<AudioClip>(FairyTapSound);
        ally.arriveSound = AssetDatabase.LoadAssetAtPath<AudioClip>(FairyArriveSound);
        ally.collectSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/PitDemo/AUDIO/Fairy Princess Appearing.wav");
        // A helper, not the whole show: about a first boss's health in a
        // 30 s visit, pushing it back as it fires.
        ally.lifetime = 30f;
        ally.fireInterval = 0.1f;
        ally.spikeDamage = 1;
        ally.knockback = 3f;
        ally.hitSounds = System.Array.ConvertAll(FairyHitSounds, n => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/PitDemo/AUDIO/" + n));
        Debug.Log($"[EndlessKnightBuilder] fairy: {looks.Count} looks, {looks[0].frames.Length}/{looks[1].frames.Length}/{looks[2].frames.Length} frames");
    }

    // The player's run (Joanna D'Arc III's Running loop, from her Aseprite
    // import), for a part push of the joystick; a
    // full push is THE PIT's sprint (Spriting).
    const string JoannaSheet = "Assets/PitDemo/SPRITES/ARIANA/MAIN ANIMATIONS/JoannaD'ArcIII.aseprite";
    const string JoannaCasts = "Assets/PitDemo/SPRITES/ARIANA/MAIN ANIMATIONS/JoannaD'ArcIIICasts.aseprite";

    // A cast's sprite frames, with the sprite held where HolySlash.anim puts
    // it for the casts (its local position), played once.
    const string CastClipFolder = "Assets/PitDemo/SPRITES/ARIANA/MAIN ANIMATIONS";
    static readonly Vector3 CastSpritePosition = new Vector3(0.215f, -0.285f, 0f);
    const string JoannaMoveset2 = "Assets/PitDemo/SPRITES/ARIANA/MAIN ANIMATIONS/JoannaD'ArcIIIMoveset#2.aseprite";

    // The first `count` frames of a clip, played once (its last frame held to its end).
    static AnimationClip FirstFrames(AnimationClip source, string name, int count)
    {
        string path = $"{CastClipFolder}/{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (!clip) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.ClearCurves();
        clip.frameRate = source.frameRate;
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(source, binding);
            int n = Mathf.Min(count, keys.Length);
            var part = new ObjectReferenceKeyframe[n + 1];
            System.Array.Copy(keys, part, n);
            // Held until the next frame would have come.
            part[n] = new ObjectReferenceKeyframe { time = n < keys.Length ? keys[n].time : keys[n - 1].time, value = keys[n - 1].value };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, part);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static AnimationClip InPlaceCast(AnimationClip source, string name)
    {
        string path = $"{CastClipFolder}/{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (!clip) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.ClearCurves();
        clip.frameRate = source.frameRate;
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
            AnimationUtility.SetObjectReferenceCurve(clip, binding, AnimationUtility.GetObjectReferenceCurve(source, binding));
        float end = source.length;
        clip.SetCurve("", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0f, end, CastSpritePosition.x));
        clip.SetCurve("", typeof(Transform), "m_LocalPosition.y", AnimationCurve.Constant(0f, end, CastSpritePosition.y));
        clip.SetCurve("", typeof(Transform), "m_LocalPosition.z", AnimationCurve.Constant(0f, end, CastSpritePosition.z));
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void SetUpGaits(GameObject demo)
    {
        var player = demo.GetComponentInChildren<PlayerScriptARIANAClips>(true);
        player.walkClip = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(JoannaSheet))
        {
            if (!(asset is AnimationClip clip)) continue;
            // (Her Walking loop was too slow for the game.)
            if (clip.name == "Running") player.jogClip = clip;
        }
        // The C button's buff spells (SpellWheel): her CastBuff and
        // CastShieldBuff casts, as clips of their own that also move the
        // sprite, like HolySlash.anim - the casts' frames have her 0.86
        // units further left on their canvas than the moves' do, so they
        // played stepping back.
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(JoannaCasts))
        {
            if (!(asset is AnimationClip clip)) continue;
            if (clip.name == "CastBuff") player.swordBuffClip = InPlaceCast(clip, "SwordBuff");
            if (clip.name == "CastShieldBuff") player.shieldBuffClip = InPlaceCast(clip, "ShieldBuff");
        }
        // Resting when she's stood still a while: Moveset #2's Rest - her
        // sitting down, as a clip of its own up to where its Resting loop
        // starts - then that loop.
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(JoannaMoveset2))
        {
            if (!(asset is AnimationClip clip)) continue;
            if (clip.name == "Rest") player.restClip = FirstFrames(clip, "RestDown", 8);
            if (clip.name == "Resting") player.restingClip = clip;
            if (clip.name == "Sleeping") player.sleepingClip = clip;
        }
        player.swordBuffSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/PitDemo/AUDIO/Magic_Sword_Unfold.wav");
        player.shieldBuffSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/PitDemo/AUDIO/Choir Major.mp3");
        Debug.Log($"[EndlessKnightBuilder] buffs: sword {(player.swordBuffClip ? player.swordBuffClip.length.ToString("F2") : "none")}s, shield {(player.shieldBuffClip ? player.shieldBuffClip.length.ToString("F2") : "none")}s");
        Debug.Log($"[EndlessKnightBuilder] rest: {(player.restClip ? player.restClip.length.ToString("F2") : "none")}s then {(player.restingClip ? player.restingClip.name : "none")}");
        Debug.Log($"[EndlessKnightBuilder] gaits: walk {(player.walkClip ? player.walkClip.name : "none")}, run {(player.jogClip ? player.jogClip.name : "none")}, sprint {(player.runClip ? player.runClip.name : "none")}");
    }

    // THE PIT's campfire stood on the sidewalk's front edge: it goes up so
    // the lowest point of any of its animation frames is on the line the
    // player stands on (the bottom of their sprite).
    static void LiftCampfire(GameObject demo)
    {
        var fire = demo.transform.Find("BONFIRE");
        var body = demo.GetComponentInChildren<PlayerScriptARIANAClips>(true);
        var bodySprite = body && body.transform.Find("ARIANA") ? body.transform.Find("ARIANA").GetComponent<SpriteRenderer>() : null;
        var animator = fire ? fire.GetComponent<Animator>() : null;
        if (!fire || !bodySprite || !bodySprite.sprite || !animator || !animator.runtimeAnimatorController) return;
        float lowest = float.MaxValue;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                    if (key.value is Sprite frame)
                        lowest = Mathf.Min(lowest, fire.TransformPoint(new Vector3(0f, frame.bounds.min.y, 0f)).y);
        if (lowest == float.MaxValue) return;
        float ground = bodySprite.bounds.min.y;
        if (Mathf.Abs(ground - lowest) > 0.001f) fire.position += Vector3.up * (ground - lowest);
    }

    // The HUD's layout, fixed: THE PIT laid it out for a portrait phone; here
    // it sits at the left of the purple band under the road, filling its
    // height, the bars lengthened to reach almost across, the score filling
    // the room under them. Measured once from the fitting PitDemoGame used to
    // do at run time; it's drawn into the actors' picture, so it scales with
    // the cabinet like the rest.
    const float HudScale = 0.968556166f;
    static readonly Vector2 HudPosition = new Vector2(71.04285f, -196.7673f);
    // The score: a little smaller than it was (0.7218), to fit a SCORE
    // title beside it in its frame; its digits start at ScoreDigitsAt
    // (picture pixels). Its text box starts this far left of its first
    // digit at this size (the font's side bearing; measured).
    const float ScoreScale = 0.6f;
    const float ScoreY = -53.8605652f - 4.5f / (1.32541f * HudScale);   // centred in the panel, 4.5 px lower
    const float ScoreBearingPixels = 2.4f;
    static Vector3 ScorePosition => new Vector3(GuiLocalX(ScoreDigitsAt - ScoreBearingPixels
        + 100f * ScoreScale * HudScale * PixelsPerCanvasUnit.x), ScoreY, 0f);
    // Picture x to the GUI's own x (it hangs from the canvas's top-left).
    static float GuiLocalX(float pictureX) => ((pictureX - CanvasOriginPixels.x) / PixelsPerCanvasUnit.x - HudPosition.x) / HudScale;
    // The scores' panel: the band frame's own art at the same one art pixel
    // per picture pixel, just its top and sides (FrameDigitalLarge's top 18
    // rows, ScorePanelFrame.png - its own base doubled the band's lines),
    // standing on the band's base with its right side on the band's, and
    // split down the middle by a divider in the frame's colours. Each half
    // (picture pixels: x, y, width, height - inside the panel's edges) has a
    // title on its side at its left edge and the digits after it.
    const string ScorePanelSprite = "Assets/PitDemo/SPRITES/HUD/ScorePanelFrame.png";
    // Right of the portrait; drawn with the band's own 1 px border, its
    // bottom and right edges lying exactly on the band's (one set of lines).
    static readonly RectInt ScorePanel = new RectInt(95, 0, 332, 47);
    const int ScoreDivider = 250;
    static readonly RectInt ScoreFrame = new RectInt(101, 4, 148, 39), HiScoreFrame = new RectInt(255, 4, 166, 39);
    // Titles hug each half's left edge (SCORE is one line, HI SCORE two),
    // a line in the frame's colours just after each, and the digits close
    // after that.
    const int ScoreTitleCentre = 5, HiScoreTitleCentre = 9;
    const int ScoreTitleLine = 112, HiScoreTitleLine = 274;
    const float ScoreDigitsAt = 120f, HiScoreDigitsAt = 282f;
    // Its ring was 59 px tall (x 17-80, y 28-87); 86 px fills the band.
    // It scales about its top-left (42.4, 97.4 px) and moves to centre the
    // ring at (51, 47); the bars start 17 px further right (0.778 GUI units
    // a pixel).
    const float PortraitScale = 86f / 59f;
    static readonly Vector2 PortraitPosition = new Vector2(-0.21f, -0.5f + 6.05f);
    static readonly (string bar, Vector2 position)[] BarPositions =
    {
        ("HealthBar", new Vector2(8.9f + 13.23f, -10.8f)),
        ("ManaBar", new Vector2(-100f + 13.23f, 19f)),
    };
    static readonly (string bar, float divider)[] HudBars =
    {
        ("ManaBar", 0.29698f),   // shorter than the health bar: its value sits past its end
        ("HealthBar", 0.31089f), // both start right of the full-height portrait
    };

    static void SetUpHudLayout(GameObject demo)
    {
        var gui = demo.transform.Find("Responsive Health Bar ARK/Canvas/GUI") as RectTransform;
        if (!gui) return;
        gui.localScale = Vector3.one * HudScale;
        gui.anchoredPosition = HudPosition;
        var score = gui.Find("SCORE COUNTER");
        if (score)
        {
            score.localScale = Vector3.one * ScoreScale;
            score.localPosition = ScorePosition;
        }
        // The portrait as tall as the band inside its border (y 4-90), the
        // bars moved right to start under its edge as before.
        var portrait = gui.Find("Portrait") as RectTransform;
        if (portrait)
        {
            portrait.localScale = Vector3.one * PortraitScale;
            portrait.anchoredPosition = PortraitPosition;
        }
        foreach (var (name, position) in BarPositions)
            if (gui.Find(name) is RectTransform barRect) barRect.anchoredPosition = position;
        foreach (var (name, divider) in HudBars)
        {
            var bar = gui.Find(name) ? gui.Find(name).GetComponent<DynamicAttrBar>() : null;
            if (!bar) continue;
            var so = new SerializedObject(bar);
            so.FindProperty("divider").floatValue = divider;
            so.FindProperty("maxSize").floatValue = 100000f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // Pixel UI & HUD accents on the HUD's purple band, drawn on its canvas
    // behind the bars and score, each at one art pixel to one of the
    // picture's (the canvas is 400 x 300 over the 427 x 320 picture):
    // a faint lavender checker filling the band (the Arcade background
    // grid's 16 px squares), the purple PanelDigital round the score, a
    // bracketed box of line work (the White dividers tinted lavender, like
    // the frame) with a purple diamond in the free space right of it, and
    // corner pieces in the band's corners. Positions are the picture's
    // pixels from its bottom-left. The HUD canvas doesn't sit on the picture
    // one to one: it's larger, its bottom-left at (-51.79, -38.80) px and
    // 1.3264 x 1.3254 px to a canvas unit (measured; its reference pixels
    // per unit is 16) - the same space the GUI's baked layout is in.
    const string PixelUi = "Assets/PIXEL UI/Pixel UI & HUD 4/Sprites/";
    const string HudChecker = "Assets/PitDemo/SPRITES/HUD/HudChecker.png";
    static readonly Vector2 CanvasOriginPixels = new Vector2(-51.794f, -38.8037f);
    static readonly Vector2 PixelsPerCanvasUnit = new Vector2(1.32645f, 1.32541f);
    static readonly Color HudLavender = new Color32(184, 160, 240, 255);

    static Sprite HudSprite(string path, Vector4 border, float pixelsPerUnit = 100f)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
            || importer.spriteBorder != border || importer.filterMode != FilterMode.Point || importer.wrapMode != TextureWrapMode.Repeat
            || !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = border;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void SetUpHudAccents(GameObject demo)
    {
        var canvas = demo.transform.Find("Responsive Health Bar ARK/Canvas") as RectTransform;
        if (!canvas) return;
        var old = canvas.Find("HUD ACCENTS");
        if (old) Object.DestroyImmediate(old.gameObject);
        var group = new GameObject("HUD ACCENTS", typeof(RectTransform)).GetComponent<RectTransform>();
        group.SetParent(canvas, false);
        group.SetAsFirstSibling();   // behind the GUI
        group.anchorMin = Vector2.zero;
        group.anchorMax = Vector2.one;
        group.offsetMin = group.offsetMax = Vector2.zero;
        group.gameObject.layer = canvas.gameObject.layer;

        // x, y: its bottom-left, in picture pixels; w, h likewise.
        UnityEngine.UI.Image Piece(string name, Sprite sprite, int x, int y, int w, int h, Color color, bool flipY = false,
            UnityEngine.UI.Image.Type type = UnityEngine.UI.Image.Type.Simple)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            go.layer = group.gameObject.layer;
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(group, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w / PixelsPerCanvasUnit.x, h / PixelsPerCanvasUnit.y);
            rt.anchoredPosition = new Vector2((x + w * 0.5f - CanvasOriginPixels.x) / PixelsPerCanvasUnit.x,
                                              (y + h * 0.5f - CanvasOriginPixels.y) / PixelsPerCanvasUnit.y);
            if (flipY) rt.localScale = new Vector3(1, -1, 1);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.type = type;
            image.color = color;
            image.raycastTarget = false;
            // Sliced edges and tiles at one art pixel per picture pixel: the
            // image's pixels per unit is the sprite's 100 over the canvas's
            // 16, times this.
            image.pixelsPerUnitMultiplier = (PixelsPerCanvasUnit.x + PixelsPerCanvasUnit.y) * 0.5f * 16f / 100f;
            return image;
        }

        var sliced = UnityEngine.UI.Image.Type.Sliced;
        Piece("Checker", HudSprite(HudChecker, Vector4.zero), 6, 5, 415, 86, new Color(HudLavender.r, HudLavender.g, HudLavender.b, 7f / 255f),
            type: UnityEngine.UI.Image.Type.Tiled);
        // The band's border, drawn here one pixel to a picture pixel: two
        // 1 px lavender lines, as along its top (the frame art scaled to
        // half size merged its lines at the bottom and left).
        Piece("Band Border", HudSprite(HudBandBorderSprite, new Vector4(4, 4, 4, 4)), 0, 0, 427, 95, Color.white,
            type: UnityEngine.UI.Image.Type.Sliced);
        Piece("Score Panel", HudSprite(HudBandBorderSprite, new Vector4(4, 4, 4, 4)), ScorePanel.x, ScorePanel.y, ScorePanel.width, ScorePanel.height,
            Color.white, type: sliced);
        var frameLight = new Color32(182, 151, 239, 255);
        var frameDark = new Color32(40, 22, 60, 255);
        int dividerBottom = ScoreFrame.y, dividerHeight = ScoreFrame.height;
        Piece("Score Divider Left", null, ScoreDivider, dividerBottom, 1, dividerHeight, frameLight);
        Piece("Score Divider Middle", null, ScoreDivider + 1, dividerBottom, 1, dividerHeight, frameDark);
        Piece("Score Divider Right", null, ScoreDivider + 2, dividerBottom, 1, dividerHeight, frameLight);
        // Between each title and its digits: a light line with a dark one beside it, inset top and bottom.
        foreach (var (name, x) in new[] { ("Score Title Line", ScoreTitleLine), ("Hi Score Title Line", HiScoreTitleLine) })
        {
            Piece(name + " Light", null, x, dividerBottom + 4, 1, dividerHeight - 8, frameLight);
            Piece(name + " Dark", null, x + 1, dividerBottom + 4, 1, dividerHeight - 8, frameDark);
        }
        var corner = new Color(HudLavender.r, HudLavender.g, HudLavender.b, 0.9f);
        foreach (var (name, x, y) in new[] { ("TopLeft", 5, 78), ("TopRight", 411, 78), ("BottomLeft", 5, 5), ("BottomRight", 411, 5) })
            Piece("Corner " + name, HudSprite(PixelUi + $"Decorators/White/BorderB{name}.png", Vector4.zero), x, y, 11, 11, corner);
    }

    // HudJuice: the health and mana values over the health bar (HP left,
    // MP right), HI SCORE in the line-work box right of the score, and the
    // hit, heal and mana flashes - a white silhouette and a green outline of
    // the bar's frame (cut from its art, HealthBar.png's HealthBar_32, sliced
    // like it) and a purple strip over the mana bar.
    const string HudFlashSprite = "Assets/PitDemo/SPRITES/HUD/HealthBarFlash.png";
    const string HudOutlineSprite = "Assets/PitDemo/SPRITES/HUD/HealthBarOutline.png";
    const string HudManaOutlineSprite = "Assets/PitDemo/SPRITES/HUD/ManaBarOutline.png";
    const string HiScoreMaterialPath = "Assets/PitDemo/SPRITES/HUD/DigitalNumbers Blue Glow.mat";
    const string CompactFont = "Assets/SpatialEmulator/Fonts/DeadRevolverGameCompact Pixel.asset";
    const string ArcadeFont = "Assets/SpatialEmulator/Fonts/DeadRevolverArcade Pixel.asset";
    const string DigitalFont = "Assets/PitDemo/DigitalNumbers-Regular SDF 1.asset";

    static RectTransform PixelRect(string name, Transform parent, int x, int y, int w, int h)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.gameObject.layer = parent.gameObject.layer;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w / PixelsPerCanvasUnit.x, h / PixelsPerCanvasUnit.y);
        rt.anchoredPosition = new Vector2((x + w * 0.5f - CanvasOriginPixels.x) / PixelsPerCanvasUnit.x,
                                          (y + h * 0.5f - CanvasOriginPixels.y) / PixelsPerCanvasUnit.y);
        return rt;
    }

    // A pixel font's glyph pixels at one picture pixel each (Game Compact is
    // 8 font pixels to the em, Arcade 10.67).
    static TMPro.TextMeshProUGUI HudText(string name, Transform parent, int x, int y, int w, int h, string font, float fontPixels,
        Color color, TMPro.TextAlignmentOptions align, string text)
    {
        var rt = PixelRect(name, parent, x, y, w, h);
        var tmp = rt.gameObject.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(font);
        tmp.fontSize = fontPixels / ((PixelsPerCanvasUnit.x + PixelsPerCanvasUnit.y) * 0.5f);
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        tmp.overflowMode = TMPro.TextOverflowModes.Overflow;
        tmp.richText = true;
        tmp.raycastTarget = false;
        tmp.text = text;
        return tmp;
    }

    static void SetUpHudJuice(GameObject demo)
    {
        var canvas = demo.transform.Find("Responsive Health Bar ARK/Canvas") as RectTransform;
        var gui = canvas ? canvas.Find("GUI") : null;
        if (!gui) return;
        var juice = gui.GetComponent<HudJuice>();
        if (!juice) juice = gui.gameObject.AddComponent<HudJuice>();
        juice.stats = demo.GetComponentInChildren<PlayerStats>(true);

        // Overlays inside the bars, so they shake and size with them.
        var health = gui.Find("HealthBar") as RectTransform;
        var border = health.Find("HealthBarBorder").GetComponent<UnityEngine.UI.Image>();
        float barPpu = ((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(border.sprite))).spritePixelsPerUnit;
        UnityEngine.UI.Image Overlay(string name, Transform parent, int index, Sprite sprite, UnityEngine.UI.Image.Type type)
        {
            var old = parent.Find(name);
            if (old) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(index);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.type = type;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }
        var sliced = UnityEngine.UI.Image.Type.Sliced;
        juice.healthBar = health;
        juice.healthBorder = border;
        juice.healthFlash = Overlay("HealthBarHitFlash", health, border.transform.GetSiblingIndex() + 1,
            HudSprite(HudFlashSprite, border.sprite.border, barPpu), sliced);
        juice.healthOutline = Overlay("HealthBarHealOutline", health, border.transform.GetSiblingIndex() + 1,
            HudSprite(HudOutlineSprite, border.sprite.border + Vector4.one, barPpu), sliced);
        var mana = gui.Find("ManaBar");
        juice.manaFill = mana.Find("ManaBarFill").GetComponent<UnityEngine.UI.Image>();
        juice.manaFlash = Overlay("ManaBarGainFlash", mana, mana.Find("ManaBarBorder").GetSiblingIndex(), null, UnityEngine.UI.Image.Type.Simple);
        var manaBorder = mana.Find("ManaBarBorder").GetComponent<UnityEngine.UI.Image>();
        float manaPpu = ((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(manaBorder.sprite))).spritePixelsPerUnit;
        juice.manaBorder = manaBorder;
        juice.manaOutline = Overlay("ManaBarFullOutline", mana, manaBorder.transform.GetSiblingIndex() + 1,
            HudSprite(HudManaOutlineSprite, manaBorder.sprite.border + Vector4.one, manaPpu), sliced);

        // Text, over everything.
        var oldText = canvas.Find("HUD TEXT");
        if (oldText) Object.DestroyImmediate(oldText.gameObject);
        var group = new GameObject("HUD TEXT", typeof(RectTransform)).GetComponent<RectTransform>();
        group.SetParent(canvas, false);
        group.SetAsLastSibling();
        group.gameObject.layer = canvas.gameObject.layer;
        group.anchorMin = Vector2.zero; group.anchorMax = Vector2.one;
        group.offsetMin = group.offsetMax = Vector2.zero;
        var white = new Color32(240, 236, 255, 255);
        // The values one above the other at the bars' right: health over
        // the health bar's end, mana past the mana bar's.
        juice.healthText = HudText("Health Value", group, 366, 75, 120, 9, CompactFont, 8f, white, TMPro.TextAlignmentOptions.BottomLeft, "125 / 125");
        juice.manaText = HudText("Mana Value", group, 366, 48, 50, 9, CompactFont, 8f, white, TMPro.TextAlignmentOptions.MidlineLeft, "50 / 50");

        // HI SCORE: the score counter's twin (its size and glow), in the
        // line-work box; its title small and on its side, at the box's left.
        var score = gui.Find("SCORE COUNTER");
        var oldHi = gui.Find("HI SCORE COUNTER");
        if (oldHi) Object.DestroyImmediate(oldHi.gameObject);
        var hi = Object.Instantiate(score.gameObject, gui).transform;
        hi.name = "HI SCORE COUNTER";
        hi.SetSiblingIndex(score.GetSiblingIndex() + 1);
        hi.localScale = score.localScale;
        hi.localPosition = score.localPosition + Vector3.right * ((HiScoreDigitsAt - ScoreDigitsAt) / (PixelsPerCanvasUnit.x * gui.localScale.x));
        juice.hiScoreText = hi.GetComponent<TMPro.TMP_Text>();
        juice.hiScoreText.text = "0000000";
        // Neon blue, where the score glows purple: its own copy of the
        // digits' material, the glow turned blue.
        var pink = AssetDatabase.LoadAssetAtPath<Material>(HiScoreMaterialPath);
        if (!pink)
        {
            pink = new Material(juice.hiScoreText.fontSharedMaterial) { name = "DigitalNumbers Blue Glow" };
            AssetDatabase.CreateAsset(pink, HiScoreMaterialPath);
        }
        pink.CopyPropertiesFromMaterial(score.GetComponent<TMPro.TMP_Text>().fontSharedMaterial);
        pink.SetColor("_GlowColor", new Color(0.05f, 0.45f, 1f, 1f));
        pink.SetColor("_FaceColor", new Color(0.84f, 0.92f, 1f, 1f));
        EditorUtility.SetDirty(pink);
        juice.hiScoreText.fontSharedMaterial = pink;
        // Their titles, on their sides, in Game Compact (one font pixel to a
        // picture pixel, to fit inside the panel).
        foreach (var (name, frame, text, centre) in new[] { ("Score Title", ScoreFrame, "SCORE", ScoreTitleCentre), ("Hi Score Title", HiScoreFrame, "HI\nSCORE", HiScoreTitleCentre) })
        {
            int cx = frame.x + centre, cy = frame.y + frame.height / 2;
            var title = HudText(name, group, cx - 20, cy - 12, 40, 24, CompactFont, 8f, HudLavender, TMPro.TextAlignmentOptions.Center, text);
            title.lineSpacing = -4f;
            title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
    }

    // The fruit collection (FruitTracker): the orange slices in all six
    // colours (the fruit that spawns all the time), then every fruit the
    // chests hold in the order the chests bring them, at their 16 px over the
    // health bar.
    static readonly string[] TrackedOranges = { "ORANGE", "RED ORANGE", "PINK ORANGE", "PURPLE ORANGE", "BLUE ORANGE", "GREEN ORANGE" };
    static readonly (string sheet, int[] icons)[] TrackedFruit =
    {
        ("Fruitbanana", new[] { 6, 7, 16, 17 }), ("Fruitapple", new[] { 6 }), ("Fruitcherry", new[] { 6 }),
        ("Fruitpeach", new[] { 4 }), ("Fruitstrawberry", new[] { 4, 6 }),
    };
    const int FruitRowX = 99, FruitRowY = 73, FruitStep = 17;

    static void SetUpFruitTracker(GameObject demo)
    {
        var canvas = demo.transform.Find("Responsive Health Bar ARK/Canvas") as RectTransform;
        if (!canvas) return;
        var old = canvas.Find("FRUIT TRACKER");
        if (old) Object.DestroyImmediate(old.gameObject);
        var group = new GameObject("FRUIT TRACKER", typeof(RectTransform)).GetComponent<RectTransform>();
        group.SetParent(canvas, false);
        group.SetAsLastSibling();
        group.gameObject.layer = canvas.gameObject.layer;
        group.anchorMin = Vector2.zero; group.anchorMax = Vector2.one;
        group.offsetMin = group.offsetMax = Vector2.zero;
        var tracker = group.gameObject.AddComponent<FruitTracker>();
        var sprites = new List<Sprite>();
        foreach (string orange in TrackedOranges)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/PitDemo/PREFABS/FRUITS/{orange}.prefab");
            if (prefab && prefab.TryGetComponent<SpriteRenderer>(out var look) && look.sprite) sprites.Add(look.sprite);
        }
        foreach (var (sheet, icons) in TrackedFruit) sprites.AddRange(SheetFrames($"{LootFolder}/{sheet}.png", icons));
        var slots = new UnityEngine.UI.Image[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            var rt = PixelRect("Fruit " + sprites[i].name, group, FruitRowX + i * FruitStep, FruitRowY, 16, 16);
            rt.gameObject.AddComponent<CanvasRenderer>();
            var image = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprites[i];
            image.raycastTarget = false;
            slots[i] = image;
        }
        tracker.slots = slots;
        tracker.winSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/PitDemo/AUDIO/Futuristic Arcade Win.wav");
        tracker.fruit = sprites.ToArray();
        Debug.Log($"[EndlessKnightBuilder] fruit tracker: {sprites.Count} fruit");
    }

    // The player's hit flash and heal / mana outlines (PlayerGlow).
    const string SolidShader = "SpatialEmulator/SpriteSolid";
    const string SolidMaterialPath = "Assets/PitDemo/SPRITES/FX/SPRITE SOLID.mat";

    static void SetUpPlayerGlow(GameObject demo)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(SolidMaterialPath);
        if (!material)
        {
            material = new Material(Shader.Find(SolidShader));
            AssetDatabase.CreateAsset(material, SolidMaterialPath);
        }
        var player = demo.GetComponentInChildren<PlayerScriptARIANAClips>(true);
        var body = player.transform.Find("ARIANA");
        var glow = body.GetComponent<PlayerGlow>();
        if (!glow) glow = body.gameObject.AddComponent<PlayerGlow>();
        glow.solidMaterial = material;

        glow.stats = player.playerStats;
    }


    // The frame round the HUD's purple band (PitDemoGame sizes it): Pixel UI
    // & HUD's purple FrameDigitalLarge, 9-sliced - 6 px edges, the thick
    // bottom strip 13 px, and 26 px on the right so its notch stays whole.
    const string HudFrameSprite = "Assets/PIXEL UI/Pixel UI & HUD 4/Sprites/Panels/Purple/FrameDigitalLarge.png";
    const string HudBandFrameSprite = "Assets/PitDemo/SPRITES/HUD/HudBandFrame.png";
    const string HudBandBorderSprite = "Assets/PitDemo/SPRITES/HUD/HudBandBorder.png";

    static void SetUpHudFrame(GameObject demo)
    {
        // Its bottom edge is its top edge turned over (HudBandFrame.png:
        // FrameDigitalLarge's top and sides, mirrored below), as thin as
        // the top - the art's 13-row base was too heavy.
        var importer = (TextureImporter)AssetImporter.GetAtPath(HudBandFrameSprite);
        var border = new Vector4(6, 6, 8, 6);   // left, bottom, right (with its shadow), top
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != border || importer.filterMode != FilterMode.Point)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = border;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }
        var frame = demo.transform.Find("HUD FRAME");
        if (!frame)
        {
            frame = new GameObject("HUD FRAME").transform;
            frame.SetParent(demo.transform, false);
        }
        if (!frame.TryGetComponent<SpriteRenderer>(out var sr)) sr = frame.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HudBandFrameSprite);
        sr.enabled = false;   // the HUD canvas draws the border now (SetUpHudAccents)
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
    }

    static GameObject MakeFx(string sheet, string prefabPath, int cell, int count, float pivotY, float fps)
    {
        var frames = SliceGrid(sheet, cell, cell, count, 0, pivotY);
        var go = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
        AddFx(go, frames, fps);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static PitSpriteFx AddFx(GameObject go, Sprite[] frames, float fps)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        sr.sortingOrder = 120; // in front of the characters and THE PIT's particles
        var fx = go.AddComponent<PitSpriteFx>();
        fx.frames = frames;
        fx.fps = fps;
        return fx;
    }

    static Sprite[] SliceGrid(string path, int cellW, int cellH, int count, int cropBottom, float pivotY = 0f, float pivotX = 0.5f)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();   // so the size read below is the real one

        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        int cols = width / cellW;
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var rects = new List<SpriteRect>();
        string stem = System.IO.Path.GetFileNameWithoutExtension(path);
        for (int i = 0; i < count; i++)
        {
            int cx = i % cols, cy = i / cols;
            rects.Add(new SpriteRect
            {
                name = $"{stem}_{i}",
                rect = new Rect(cx * cellW, height - (cy + 1) * cellH + cropBottom, cellW, cellH - cropBottom),
                alignment = SpriteAlignment.Custom,
                pivot = new Vector2(pivotX, pivotY),
                spriteID = GUID.Generate(),
            });
        }
        provider.SetSpriteRects(rects.ToArray());
        var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        var pairs = new List<SpriteNameFileIdPair>();
        foreach (var r in rects) pairs.Add(new SpriteNameFileIdPair(r.name, r.spriteID));
        names.SetNameFileIdPairs(pairs);
        provider.Apply();
        importer.SaveAndReimport();

        var byName = new Dictionary<string, Sprite>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Sprite s) byName[s.name] = s;
        var result = new Sprite[count];
        for (int i = 0; i < count; i++) result[i] = byName[rects[i].name];
        return result;
    }
}
