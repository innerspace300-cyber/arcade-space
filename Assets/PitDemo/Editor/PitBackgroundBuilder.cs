// PitBackgroundBuilder.cs — splits THE PIT's synthwave background into its
// six Aseprite layers for ARcade's depth effect.
//
// SPRITES/PARALLAX/Layers holds one sprite sheet per layer (unique frames
// only, cropped to what the layer covers) plus synthwave_layers.json (each
// frame's rect, the pivot that keeps it aligned with the merged sprite, and
// which unique frame each of the 200 animation frames uses). They're
// generated from synthwave.ase outside Unity. This builder slices the sheets,
// adds one SpriteRenderer per layer under RETRO PARALLAX in the PitDemo
// prefab, and writes RETRO PARALLAX LAYERS.anim - the original 200-frame
// clip's timing with one sprite track per layer - which the player scrubs in
// place of the merged clip.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class PitBackgroundBuilder
{
    const string Dir = "Assets/PitDemo/SPRITES/PARALLAX";
    const string LayersDir = Dir + "/Layers";
    const string PrefabPath = "Assets/PitDemo/PIT DEMO EXPORT/PitDemo.prefab";
    const string ClipPath = Dir + "/RETRO PARALLAX LAYERS.anim";

    [System.Serializable] class Layer { public string name; public int index; public string file; public int[] size; public int[][] rects; public int[] frames; public float[] pivot; }
    [System.Serializable] class Meta { public int[] region; public Layer[] layers; }

    [MenuItem("Tools/Spatial Emulator/Build PIT Demo Background Layers")]
    public static string Build()
    {
        var meta = JsonUtilityEx(File.ReadAllText(LayersDir + "/synthwave_layers.json"));
        var sprites = new List<Sprite[]>();
        foreach (var layer in meta.layers) sprites.Add(SliceSheet(layer));

        // The merged clip's key times, reused for every layer.
        var original = AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir + "/RETRO PARALLAX.anim");
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(original)[0];
        var keys = AnimationUtility.GetObjectReferenceCurve(original, binding);

        var clip = new AnimationClip { frameRate = original.frameRate, name = "RETRO PARALLAX LAYERS" };
        for (int l = 0; l < meta.layers.Length; l++)
        {
            var layer = meta.layers[l];
            var frames = new ObjectReferenceKeyframe[keys.Length];
            for (int k = 0; k < keys.Length; k++)
                frames[k] = new ObjectReferenceKeyframe { time = keys[k].time, value = sprites[l][layer.frames[k % layer.frames.Length]] };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(ChildName(layer), typeof(SpriteRenderer), "m_Sprite"), frames);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(original);
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.DeleteAsset(ClipPath);
        AssetDatabase.CreateAsset(clip, ClipPath);

        using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
        {
            var root = scope.prefabContentsRoot;
            // THE PIT's touch-joystick example script wasn't ported.
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            var bg = root.transform.Find("RETRO PARALLAX");
            var merged = bg.GetComponent<SpriteRenderer>();
            for (int l = 0; l < meta.layers.Length; l++)
            {
                var layer = meta.layers[l];
                var old = bg.Find(ChildName(layer));
                if (old) Object.DestroyImmediate(old.gameObject);
                var go = new GameObject(ChildName(layer));
                go.transform.SetParent(bg, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprites[l][layer.frames[0]];
                sr.sharedMaterial = merged.sharedMaterial;
                sr.sortingLayerID = merged.sortingLayerID;
                sr.sortingOrder = merged.sortingOrder + l;
            }
            // The merged sprite stays for its Animator; it just isn't drawn.
            merged.enabled = false;
            var player = root.GetComponentInChildren<PlayerScriptARIANAClips>(true);
            player.backgroundClip = clip;
        }
        AssetDatabase.SaveAssets();
        return $"{meta.layers.Length} layers, {keys.Length} keys";
    }

    static string ChildName(Layer layer) => "BG " + layer.name;

    static Sprite[] SliceSheet(Layer layer)
    {
        string path = LayersDir + "/" + layer.file;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 4096;
        importer.npotScale = TextureImporterNPOTScale.None;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var rects = new List<SpriteRect>();
        for (int i = 0; i < layer.rects.Length; i++)
        {
            var r = layer.rects[i];
            rects.Add(new SpriteRect
            {
                name = $"{Path.GetFileNameWithoutExtension(layer.file)}_{i}",
                rect = new Rect(r[0], r[1], r[2], r[3]),
                alignment = SpriteAlignment.Custom,
                pivot = new Vector2(layer.pivot[0], layer.pivot[1]),
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
        var result = new Sprite[rects.Count];
        for (int i = 0; i < rects.Count; i++) result[i] = byName[rects[i].name];
        return result;
    }

    // JsonUtility can't read jagged arrays; the json is small and regular.
    static Meta JsonUtilityEx(string json)
    {
        var obj = (Dictionary<string, object>)MiniJson.Parse(json);
        var meta = new Meta();
        var region = (List<object>)obj["region"];
        meta.region = new[] { Num(region[0]), Num(region[1]) };
        var layers = (List<object>)obj["layers"];
        meta.layers = new Layer[layers.Count];
        for (int i = 0; i < layers.Count; i++)
        {
            var d = (Dictionary<string, object>)layers[i];
            var rects = (List<object>)d["rects"];
            var frames = (List<object>)d["frames"];
            var pivot = (List<object>)d["pivot"];
            meta.layers[i] = new Layer
            {
                name = (string)d["name"],
                index = Num(d["index"]),
                file = (string)d["file"],
                rects = rects.ConvertAll(r => ((List<object>)r).ConvertAll(Num).ToArray()).ToArray(),
                frames = frames.ConvertAll(Num).ToArray(),
                pivot = new[] { (float)System.Convert.ToDouble(pivot[0]), (float)System.Convert.ToDouble(pivot[1]) },
            };
        }
        return meta;
    }

    static int Num(object o) => System.Convert.ToInt32(o);

    // Minimal JSON reader (objects, arrays, strings, numbers).
    static class MiniJson
    {
        public static object Parse(string s) { int i = 0; return Value(s, ref i); }
        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(); i++;
                while (true)
                {
                    Ws(s, ref i); if (s[i] == '}') { i++; return d; }
                    string k = (string)Value(s, ref i); Ws(s, ref i); i++; // ':'
                    d[k] = Value(s, ref i); Ws(s, ref i);
                    if (s[i] == ',') i++;
                }
            }
            if (c == '[')
            {
                var l = new List<object>(); i++;
                while (true)
                {
                    Ws(s, ref i); if (s[i] == ']') { i++; return l; }
                    l.Add(Value(s, ref i)); Ws(s, ref i);
                    if (s[i] == ',') i++;
                }
            }
            if (c == '"')
            {
                int start = ++i; while (s[i] != '"') i++;
                return s.Substring(start, i++ - start);
            }
            int b = i; while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(b, i - b), System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
