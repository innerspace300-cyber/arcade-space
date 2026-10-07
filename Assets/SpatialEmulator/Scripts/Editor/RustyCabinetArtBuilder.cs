// RustyCabinetArtBuilder.cs — the vertical cabinet's (Resources/
// CrtCabinetVertical, the rusty Japanese one) side art: a picture over each
// outside face - left and right sides, the back, the front under the control
// panel, the top - from Models/RustyCabinet/Art/Source. Each is a quad a hair out
// from its face, its texture the picture cropped to fill the face and cut to
// the face's outline (alpha: the body mesh's triangles on that face, so the
// side's notch and the front's coin door stay the cabinet's own). The
// marquee gets its name, 弾幕天国, over a strip of the picture, lit; the
// body's made clean black metal (CleanBody). Rebuilt by
// Tools > ARcade > Build Rusty Cabinet Art (replaces the old quads).

using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpatialEmulator.EditorTools
{
    public static class RustyCabinetArtBuilder
    {
        const string PrefabPath = "Assets/SpatialEmulator/Resources/CrtCabinetVertical.prefab";
        const string ArtFolder = "Assets/SpatialEmulator/Models/RustyCabinet/Art";
        const float PixelsPerMetre = 640f;
        const float Lift = 0.0006f;   // out from the face, against z-fighting

        struct Face
        {
            public string name, source;
            public Vector3 normal, up;   // up: the picture's up, along the face
            public float pixelsPerMetre;   // (0: PixelsPerMetre)
            public bool glow;   // lit, like a marquee
            public float anchorX, anchorY;   // which part of the picture shows where it's cropped (0 left/bottom, 1 right/top)
        }

        // All one picture (the tree, the petals' stream and the black hole),
        // a different part of it on each face.
        static readonly Face[] Faces =
        {
            new Face { name = "Right", source = "CabinetArt_Tree.jpg", normal = Vector3.right, up = Vector3.up, anchorX = 0.85f, anchorY = 0.5f },   // (the black hole)
            new Face { name = "Left", source = "CabinetArt_Tree.jpg", normal = Vector3.left, up = Vector3.up, anchorX = 0.6f, anchorY = 0.5f },   // (the petals)
            new Face { name = "Back", source = "CabinetArt_Tree.jpg", normal = Vector3.back, up = Vector3.up, anchorX = 0.3f, anchorY = 0.5f },   // (the tree)
            new Face { name = "Front", source = "CabinetArt_Tree.jpg", normal = Vector3.forward, up = Vector3.up, anchorX = 0.3f, anchorY = 0.5f },
            new Face { name = "Top", source = "CabinetArt_Tree.jpg", normal = Vector3.up, up = Vector3.back, anchorX = 0.5f, anchorY = 0.5f },   // (its top: toward the back, seen from the front)
            // The marquee: its name, 弾幕天国, over a strip of the picture
            // (CabinetArt_Marquee.png, made from it), lit.
            new Face { name = "Marquee", source = "CabinetArt_Marquee.png", normal = new Vector3(0f, 0.2f, 0.98f).normalized, up = Vector3.up, pixelsPerMetre = 2400f, glow = true, anchorX = 0.5f, anchorY = 0.5f },
        };

        const string ModelFolder = "Assets/SpatialEmulator/Models/RustyCabinet";

        // The body clean: black metal, no rust or grime (CleanAlbedo: the old
        // paint all one black, but for the coloured plastic, its colours
        // evened out; CleanMetallicSmoothness: metal, a soft shine; no bumps).
        static void CleanBody()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{ModelFolder}/RustyCabinet_Body.mat");
            Texture2D Import(string name, string like)
            {
                string path = $"{ModelFolder}/{name}.png";
                AssetDatabase.ImportAsset(path);
                var from = (TextureImporter)AssetImporter.GetAtPath($"{ModelFolder}/{like}.png");
                var to = (TextureImporter)AssetImporter.GetAtPath(path);
                var settings = new TextureImporterSettings();
                from.ReadTextureSettings(settings);
                to.SetTextureSettings(settings);
                to.SetPlatformTextureSettings(from.GetPlatformTextureSettings("iPhone"));
                to.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            var albedo = Import("RustyCabinet_CleanAlbedo", "RustyCabinet_Albedo");
            var metal = Import("RustyCabinet_CleanMetallicSmoothness", "RustyCabinet_MetallicSmoothness");
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_MainTex", albedo);
            material.SetTexture("_MetallicGlossMap", metal);
            material.SetTexture("_BumpMap", null);
            material.DisableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
        }

        [MenuItem("Tools/ARcade/Build Rusty Cabinet Art")]
        public static string Build()
        {
            CleanBody();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var log = new System.Text.StringBuilder();
            try
            {
                var old = root.transform.Find("Cabinet Art");
                if (old) Object.DestroyImmediate(old.gameObject);
                var holder = new GameObject("Cabinet Art").transform;
                holder.SetParent(root.transform, false);

                MeshFilter body = null;
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.name == "RustyCabinet_Body") body = mf;
                if (!body) return "no body";
                var bodyMaterial = body.GetComponent<MeshRenderer>().sharedMaterial;
                var mesh = body.sharedMesh;
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                var toRoot = root.transform.worldToLocalMatrix * body.transform.localToWorldMatrix;
                var points = new Vector3[vertices.Length];
                for (int i = 0; i < vertices.Length; i++) points[i] = toRoot.MultiplyPoint3x4(vertices[i]);
                var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

                foreach (var face in Faces)
                {
                    // The face: the triangles facing out this way on its main
                    // plane - the one with the most of them, of those near the
                    // outside (the back and the front panel sit a little in from
                    // the sides' edges; the far wall's inner face faces this way
                    // too). Planes are told apart by their tilt as well as their
                    // depth (the top slopes back a little).
                    var outward = face.normal;
                    float outermost = float.MinValue;
                    for (int i = 0; i < triangles.Length; i += 3)
                        if (Facing(points, triangles, i, outward)) outermost = Mathf.Max(outermost, Vector3.Dot(Centroid(points, triangles, i), outward));
                    var areaAt = new System.Collections.Generic.Dictionary<(Vector3Int, int), float>();
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        if (!Facing(points, triangles, i, outward) || Vector3.Dot(Centroid(points, triangles, i), outward) < outermost - 0.04f) continue;
                        var key = PlaneKey(points, triangles, i, out float area);
                        areaAt.TryGetValue(key, out float sum);
                        areaAt[key] = sum + area;
                    }
                    var best = default((Vector3Int, int));
                    float bestArea = -1f;
                    foreach (var pair in areaAt)
                        if (pair.Value > bestArea) { bestArea = pair.Value; best = pair.Key; }
                    var on = new System.Collections.Generic.List<int>();
                    var n = Vector3.zero;
                    for (int i = 0; i < triangles.Length; i += 3)
                        if (Facing(points, triangles, i, outward) && PlaneKey(points, triangles, i, out float area) == best)
                        {
                            on.Add(i);
                            n += TriangleNormal(points, triangles, i) * area;
                        }
                    n.Normalize();
                    float plane = float.MinValue;   // (the quad goes just in front of the furthest out of them)
                    foreach (int i in on)
                        for (int k = 0; k < 3; k++) plane = Mathf.Max(plane, Vector3.Dot(points[triangles[i + k]], n));

                    // Its axes as seen from outside: right, and up (the
                    // picture's up, laid along the face).
                    var up = Vector3.ProjectOnPlane(face.up, n).normalized;
                    var right = Vector3.Cross(up, -n);
                    float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                    foreach (int i in on)
                        for (int k = 0; k < 3; k++)
                        {
                            var p = points[triangles[i + k]];
                            float x = Vector3.Dot(p, right), y = Vector3.Dot(p, up);
                            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                        }
                    float width = maxX - minX, height = maxY - minY;
                    float ppm = face.pixelsPerMetre > 0f ? face.pixelsPerMetre : PixelsPerMetre;
                    int w = Mathf.CeilToInt(width * ppm), h = Mathf.CeilToInt(height * ppm);

                    // The outline, drawn in.
                    var mask = new bool[w * h];
                    foreach (int i in on)
                    {
                        var a = Pixel(points[triangles[i]], right, up, minX, minY, w, h, width, height);
                        var b = Pixel(points[triangles[i + 1]], right, up, minX, minY, w, h, width, height);
                        var c = Pixel(points[triangles[i + 2]], right, up, minX, minY, w, h, width, height);
                        Fill(mask, w, h, a, b, c);
                    }
                    // (Grown a pixel, so it meets the face's edges without a seam.)
                    var grown = (bool[])mask.Clone();
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            if (!mask[y * w + x])
                                for (int dy = -1; dy <= 1 && !grown[y * w + x]; dy++)
                                    for (int dx = -1; dx <= 1; dx++)
                                    {
                                        int sx = x + dx, sy = y + dy;
                                        if (sx >= 0 && sy >= 0 && sx < w && sy < h && mask[sy * w + sx]) { grown[y * w + x] = true; break; }
                                    }

                    // The picture, cropped to fill the face.
                    var source = new Texture2D(2, 2);
                    source.LoadImage(File.ReadAllBytes(Path.Combine(ArtFolder, "Source", face.source)));
                    float faceAspect = width / height, imageAspect = source.width / (float)source.height;
                    float uSpan = 1f, vSpan = 1f;
                    if (imageAspect > faceAspect) uSpan = faceAspect / imageAspect;
                    else vSpan = imageAspect / faceAspect;
                    float u0 = (1f - uSpan) * face.anchorX, v0 = (1f - vSpan) * face.anchorY;
                    var pixels = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            Color color = source.GetPixelBilinear(u0 + (x + 0.5f) / w * uSpan, v0 + (y + 0.5f) / h * vSpan);
                            color.a = grown[y * w + x] ? 1f : 0f;
                            pixels[y * w + x] = color;
                        }
                    Object.DestroyImmediate(source);
                    var art = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    art.SetPixels32(pixels);
                    art.Apply();
                    string texturePath = $"{ArtFolder}/CabinetArt_{face.name}.png";
                    File.WriteAllBytes(texturePath, art.EncodeToPNG());
                    Object.DestroyImmediate(art);
                    AssetDatabase.ImportAsset(texturePath);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.mipmapEnabled = true;
                    importer.maxTextureSize = 2048;
                    var ios = importer.GetPlatformTextureSettings("iPhone");
                    ios.overridden = true;
                    ios.maxTextureSize = face.pixelsPerMetre > PixelsPerMetre ? 2048 : 1024;   // (the marquee's text, sharp)
                    ios.format = TextureImporterFormat.ASTC_6x6;
                    importer.SetPlatformTextureSettings(ios);
                    importer.SaveAndReimport();
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

                    // Its material: the body's, with the picture and cut out.
                    string materialPath = $"{ArtFolder}/CabinetArt_{face.name}.mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (!material)
                    {
                        material = new Material(bodyMaterial);
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    else material.CopyPropertiesFromMaterial(bodyMaterial);
                    material.name = $"CabinetArt_{face.name}";
                    foreach (var map in new[] { "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap", "_DetailAlbedoMap", "_DetailNormalMap" })
                        if (material.HasProperty(map)) material.SetTexture(map, null);
                    material.DisableKeyword("_NORMALMAP");
                    material.DisableKeyword("_METALLICSPECGLOSSMAP");
                    material.DisableKeyword("_EMISSION");
                    if (face.glow)
                    {
                        material.SetTexture("_EmissionMap", texture);
                        material.SetColor("_EmissionColor", Color.white * 0.55f);
                        material.EnableKeyword("_EMISSION");
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    }
                    material.SetTexture("_BaseMap", texture);
                    material.SetTexture("_MainTex", texture);
                    material.SetTextureScale("_BaseMap", Vector2.one);
                    material.SetTextureOffset("_BaseMap", Vector2.zero);
                    material.SetColor("_BaseColor", Color.white);
                    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
                    material.SetFloat("_AlphaClip", 1f);
                    material.SetFloat("_Cutoff", 0.5f);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    EditorUtility.SetDirty(material);

                    // The quad, over the face.
                    var go = new GameObject($"Art {face.name}", typeof(MeshFilter), typeof(MeshRenderer));
                    go.layer = body.gameObject.layer;
                    go.transform.SetParent(holder, false);
                    var center = right * (minX + width * 0.5f) + up * (minY + height * 0.5f) + n * (plane + Lift);
                    go.transform.localPosition = center;
                    go.transform.localRotation = Quaternion.LookRotation(-n, up);
                    go.transform.localScale = new Vector3(width, height, 1f);
                    go.GetComponent<MeshFilter>().sharedMesh = quad;
                    var renderer = go.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    log.Append($"{face.name} {w}x{h} tris {on.Count} plane {plane:F3}\n");
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static Vector3 Centroid(Vector3[] p, int[] t, int i) => (p[t[i]] + p[t[i + 1]] + p[t[i + 2]]) / 3f;

        static Vector3 TriangleNormal(Vector3[] p, int[] t, int i) => Vector3.Cross(p[t[i + 1]] - p[t[i]], p[t[i + 2]] - p[t[i]]).normalized;

        // Which plane a triangle's on: its normal (to about a degree) and its
        // distance along it (to 3 mm).
        static (Vector3Int, int) PlaneKey(Vector3[] p, int[] t, int i, out float area)
        {
            var cross = Vector3.Cross(p[t[i + 1]] - p[t[i]], p[t[i + 2]] - p[t[i]]);
            area = cross.magnitude * 0.5f;
            var normal = cross.normalized;
            var rounded = new Vector3Int(Mathf.RoundToInt(normal.x * 60f), Mathf.RoundToInt(normal.y * 60f), Mathf.RoundToInt(normal.z * 60f));
            return (rounded, Mathf.RoundToInt(Vector3.Dot(Centroid(p, t, i), normal) / 0.003f));
        }

        static bool Facing(Vector3[] p, int[] t, int i, Vector3 n)
        {
            var cross = Vector3.Cross(p[t[i + 1]] - p[t[i]], p[t[i + 2]] - p[t[i]]);
            return cross.sqrMagnitude > 1e-12f && Vector3.Dot(cross.normalized, n) > 0.95f;
        }

        static Vector2 Pixel(Vector3 p, Vector3 right, Vector3 up, float minX, float minY, int w, int h, float width, float height)
            => new Vector2((Vector3.Dot(p, right) - minX) / width * w, (Vector3.Dot(p, up) - minY) / height * h);

        static void Fill(bool[] mask, int w, int h, Vector2 a, Vector2 b, Vector2 c)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            float area = Edge(a, b, c);
            if (Mathf.Abs(area) < 1e-6f) return;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float wa = Edge(b, c, p) / area, wb = Edge(c, a, p) / area, wc = Edge(a, b, p) / area;
                    if (wa >= -1e-4f && wb >= -1e-4f && wc >= -1e-4f) mask[y * w + x] = true;
                }
        }

        static float Edge(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    }
}
