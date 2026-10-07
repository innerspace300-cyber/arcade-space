// PortalBuilder.cs — sets up ENDLESS KNIGHT's way to the next level
// (LevelPortal): imports the portal's frames (PortalPack, Palette1: Activate,
// Idle, Close - 64 x 128, pixel art, pivoted at the art's bottom middle) into
// SPRITES/PORTAL, and scrimsy's whole fruit pack (16 px icons, every kind and
// colour) into SPRITES/PORTAL/FRUIT for the rings round it, puts LevelPortal
// on the PitDemo prefab, and makes
// LEVEL 2 - a copy of it, for now - as the level its portal leads to.
// The frames in SPRITES/PORTAL are recoloured neon pink and purple after
// import (SPRITES/PORTAL/neon_recolor.py.txt maps Palette1's 7 colours); the
// builder doesn't copy over frames already there, so that stays.
// Safe to run again: it doesn't copy over an existing LEVEL 2 (that's the
// level to build on), only links it.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PortalBuilder
{
    const string Palette = "Palette1";
    const string Source = "/Users/waynelamb/Downloads/PortalPack/Portals/" + Palette;
    const string FruitPack = "/Users/waynelamb/Downloads/scrimsy fruit icons/kind";
    const string Folder = "Assets/PitDemo/SPRITES/PORTAL";
    const string DemoPrefab = "Assets/PitDemo/PIT DEMO EXPORT/PitDemo.prefab";
    const string Level2Prefab = "Assets/PitDemo/PIT DEMO EXPORT/LEVEL 2.prefab";
    const string OpenSound = "Assets/PitDemo/AUDIO/Door IN Fantasy Word1.wav";
    const string EnterSound = "Assets/PitDemo/AUDIO/Retro Fireball Magic Hyperspace Teleport Arrives.wav";
    // The art's bottom row, 3 px up from the frame's.
    const float PivotY = 3f / 128f;

    [MenuItem("ARcade/Set Up Level Portal")]
    public static void Build()
    {
        // Another palette's frames from before go.
        foreach (var folder in new[] { "Activate", "Idle", "Close" })
            foreach (var old in Directory.Exists($"{Folder}/{folder}") ? Directory.GetFiles($"{Folder}/{folder}", "*.png") : new string[0])
                if (!Path.GetFileName(old).StartsWith($"Portal{Palette}_")) AssetDatabase.DeleteAsset(old);
        var activate = Frames("Activate", "activate");
        var idle = Frames("Idle", "idle");
        var close = Frames("Close", "close");
        var fruit = Fruit();

        // The first level's portal (and the copy to come takes it with it).
        var root = PrefabUtility.LoadPrefabContents(DemoPrefab);
        Fill(root, 1, null, activate, idle, close, fruit);
        PrefabUtility.SaveAsPrefabAsset(root, DemoPrefab);
        PrefabUtility.UnloadPrefabContents(root);

        if (!File.Exists(Level2Prefab)) AssetDatabase.CopyAsset(DemoPrefab, Level2Prefab);
        var level2 = AssetDatabase.LoadAssetAtPath<GameObject>(Level2Prefab);

        // Level 2: no portal on from it yet.
        root = PrefabUtility.LoadPrefabContents(Level2Prefab);
        Fill(root, 2, null, activate, idle, close, fruit);
        PrefabUtility.SaveAsPrefabAsset(root, Level2Prefab);
        PrefabUtility.UnloadPrefabContents(root);

        // Level 1's portal leads to it.
        root = PrefabUtility.LoadPrefabContents(DemoPrefab);
        root.GetComponent<LevelPortal>().nextLevel = level2;
        PrefabUtility.SaveAsPrefabAsset(root, DemoPrefab);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PortalBuilder] portal: {activate.Length}/{idle.Length}/{close.Length} frames; LEVEL 2 linked");
    }

    static void Fill(GameObject root, int level, GameObject next, Sprite[] activate, Sprite[] idle, Sprite[] close, Sprite[] fruit)
    {
        var portal = root.GetComponent<LevelPortal>();
        if (!portal) portal = root.AddComponent<LevelPortal>();
        portal.level = level;
        portal.nextLevel = next;
        portal.activateFrames = activate;
        portal.idleFrames = idle;
        portal.closeFrames = close;
        portal.ringFruit = fruit;
        portal.openSound = AssetDatabase.LoadAssetAtPath<AudioClip>(OpenSound);
        portal.enterSound = AssetDatabase.LoadAssetAtPath<AudioClip>(EnterSound);
    }

    // One animation's numbered frames, copied in and imported as pixel-art sprites.
    static Sprite[] Frames(string folder, string name)
    {
        var from = Path.Combine(Source, folder);
        var to = $"{Folder}/{folder}";
        Directory.CreateDirectory(to);
        var files = Directory.GetFiles(from, $"Portal{Palette}_{name}*.png")
            .Where(f => char.IsDigit(Path.GetFileNameWithoutExtension(f).Last()))
            .OrderBy(f => f).ToArray();
        var sprites = new Sprite[files.Length];
        for (int i = 0; i < files.Length; i++)
        {
            var path = $"{to}/{Path.GetFileName(files[i])}";
            sprites[i] = Import(files[i], path, new Vector2(0.5f, PivotY), SpriteMeshType.FullRect);
        }
        return sprites;
    }

    // The game's own fruit (the kinds in the chests, and oranges) from the
    // pack - bananas as bunches - in every colour but black and white, centred - in order: kind by
    // kind (the pack's folders), each kind's shapes in turn (banana, then
    // bananas), each in the rainbow's colours.
    public static readonly string[] Kinds = { "apple", "banana", "cherry", "orange", "peach", "strawberry" };
    static bool Wanted(string file) =>
        System.Array.IndexOf(Kinds, Path.GetFileName(Path.GetDirectoryName(file))) >= 0
        && !Path.GetFileName(file).StartsWith("black ") && !Path.GetFileName(file).StartsWith("white ")
        && Shape(file) != "banana";   // (the bunches only: a single banana is longer than the rest, and widens the ring)
    static Sprite[] Fruit()
    {
        var to = $"{Folder}/FRUIT";
        Directory.CreateDirectory(to);
        var files = Ordered(Directory.GetFiles(FruitPack, "*.png", SearchOption.AllDirectories).Where(Wanted));
        var sprites = new Sprite[files.Length];
        for (int i = 0; i < files.Length; i++)
            sprites[i] = Import(files[i], $"{to}/{Path.GetFileName(files[i])}", new Vector2(0.5f, 0.5f), SpriteMeshType.Tight);
        return sprites;
    }

    static readonly string[] Rainbow = { "red", "orange", "yellow", "green", "aqua", "blue", "purple", "pink", "brown" };

    // The pack's files in that order (a file's kind is its folder; its
    // shape, the rest of its name after the colour).
    static string[] Ordered(IEnumerable<string> files) => files
        .OrderBy(f => Path.GetFileName(Path.GetDirectoryName(f)))
        .ThenBy(f => Shape(f))
        .ThenBy(f => { int c = System.Array.IndexOf(Rainbow, Colour(f)); return c < 0 ? 99 : c; })
        .ToArray();

    static string Colour(string file) => Path.GetFileNameWithoutExtension(file).Split(' ')[0];
    // (An orange orange is just "orange 1": its colour is its name, its shape "orange 1" like the rest.)
    static string Shape(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        int space = name.IndexOf(' ');
        var rest = space < 0 ? name : name.Substring(space + 1);
        return rest.Any(char.IsLetter) ? rest : name;
    }

    /// The portal's fruit (already imported): just the wanted ones, in that order - by the pack's files.
    public static Sprite[] InOrder(Sprite[] fruit)
    {
        var byName = fruit.Where(f => f).ToDictionary(f => f.name);
        return Ordered(Directory.GetFiles(FruitPack, "*.png", SearchOption.AllDirectories)
                .Where(f => Wanted(f) && byName.ContainsKey(Path.GetFileNameWithoutExtension(f))))
            .Select(f => byName[Path.GetFileNameWithoutExtension(f)]).ToArray();
    }

    static Sprite Import(string source, string path, Vector2 pivot, SpriteMeshType mesh)
    {
        {
            if (!File.Exists(path)) File.Copy(source, path);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = mesh;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
