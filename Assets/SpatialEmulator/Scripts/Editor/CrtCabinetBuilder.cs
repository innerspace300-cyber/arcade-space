// CrtCabinetBuilder.cs — makes Resources/CrtCabinet.prefab, the 3D cabinet
// CrtCabinet stands round the screen in CRT mode: the "Retro Arcade Cabinet"
// (low poly, from its Unity package; Models/RetroCabinet) with URP Lit
// materials from its textures (paint, normals, shading, the marquee's and
// buttons' glow), its screen a dark glossy tube (the game shows in front of
// it) and no glass; the buttons and the joysticks' balls on a plain
// material of their own, for CrtCabinetControls to colour. It measures the screen (the glass's tilt and middle, the
// tube's width) for CrtCabinetModel. Safe to run again.

using SpatialEmulator;
using UnityEditor;
using UnityEngine;

public static class CrtCabinetBuilder
{
    const string Folder = "Assets/SpatialEmulator/Models/RetroCabinet";
    const string Model = Folder + "/RetroArcadeCabinetLowPoly.fbx";
    const string PrefabPath = "Assets/SpatialEmulator/Resources/CrtCabinet.prefab";
    // The picture's least gap in front of the tube.
    const float TubeGap = 0.004f;

    [MenuItem("ARcade/Build CRT Cabinet")]
    public static string Build()
    {
        var cabinet = Material("CrtCabinet", m =>
        {
            m.SetTexture("_BaseMap", Texture("ArcadeCabinet_AlbedoLit"));   // (the paint brighter and richer, as if lit)
            m.SetTexture("_BumpMap", Texture("ArcadeCabinet_Nmap"));
            m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_OcclusionMap", Texture("ArcadeCabinet_AO"));
            m.EnableKeyword("_OCCLUSIONMAP");
            m.SetTexture("_EmissionMap", Texture("ArcadeCabinet_EmisLit"));   // (its own glow, and a soft glow of the paint)
            m.SetColor("_EmissionColor", Color.white * 1.6f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetFloat("_Smoothness", 0.45f);
        });
        var tube = Material("CrtTube", m =>
        {
            m.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.03f));
            m.SetFloat("_Smoothness", 0.85f);
        });

        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = "CrtCabinet";
        var data = root.AddComponent<CrtCabinetModel>();
        Transform glass = null, screen = null, body = null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.EndsWith("_Glass")) glass = t;
            else if (t.name.EndsWith("_Screen")) screen = t;
            else if (t.name.EndsWith("_Base")) body = t;
        }

        // The screen: the glass's plane (its tilt and middle), the tube's width.
        var quad = glass.GetComponent<MeshFilter>().sharedMesh.vertices;
        Vector3 a = root.transform.InverseTransformPoint(glass.TransformPoint(quad[0]));
        Vector3 b = root.transform.InverseTransformPoint(glass.TransformPoint(quad[1]));
        Vector3 c = root.transform.InverseTransformPoint(glass.TransformPoint(quad[2]));
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
        if (normal.z < 0f) normal = -normal;   // (out of the front, toward the player)
        var glassBounds = glass.GetComponent<Renderer>().bounds;
        data.screenNormal = normal;
        // The tube, measured across and up along the tilted glass (its middle on the glass).
        Vector3 glassCenter = root.transform.InverseTransformPoint(glassBounds.center);
        Vector3 right = Vector3.right, up = Vector3.Cross(normal, right).normalized;
        if (up.y < 0f) up = -up;
        float uMin = float.MaxValue, uMax = float.MinValue, vMin = float.MaxValue, vMax = float.MinValue;
        foreach (var v in screen.GetComponent<MeshFilter>().sharedMesh.vertices)
        {
            var d = root.transform.InverseTransformPoint(screen.TransformPoint(v)) - glassCenter;
            float u = Vector3.Dot(d, right), w = Vector3.Dot(d, up);
            uMin = Mathf.Min(uMin, u); uMax = Mathf.Max(uMax, u); vMin = Mathf.Min(vMin, w); vMax = Mathf.Max(vMax, w);
        }
        data.screenCenter = glassCenter + right * ((uMin + uMax) * 0.5f) + up * ((vMin + vMax) * 0.5f);
        data.screenWidth = uMax - uMin;
        data.screenHeight = vMax - vMin;
        // How far the picture sits down in it: its back layer (as tall as the
        // tube, wider than it - the sides tucked behind the bezel - and
        // curved by the CRT filter's bulge) as deep as it can go without
        // the tube's glass-ward curve showing through it.
        float pictureHalfW = data.screenHeight * 4f / 3f * 0.5f, pictureHalfH = data.screenHeight * 0.5f;
        float bulge = CrtEffect.Bulge * pictureHalfW * 2f;
        Vector3 tubeMiddle = data.screenCenter;
        float edge = float.MinValue;
        foreach (var v in screen.GetComponent<MeshFilter>().sharedMesh.vertices)
        {
            var d = root.transform.InverseTransformPoint(screen.TransformPoint(v)) - tubeMiddle;
            float du = Mathf.Clamp(Vector3.Dot(d, right) / pictureHalfW, -1f, 1f), dv = Mathf.Clamp(Vector3.Dot(d, up) / pictureHalfH, -1f, 1f);
            edge = Mathf.Max(edge, Vector3.Dot(d, normal) - bulge * (1f - du * du) * (1f - dv * dv));
        }
        data.recess = -(edge + TubeGap);
        data.halfWidth = body.GetComponent<Renderer>().bounds.extents.x;
        // The front edge of its top (the saves panel sits level with it).
        float top = float.MinValue, front = float.MinValue;
        var bodyMesh = body.GetComponent<MeshFilter>().sharedMesh.vertices;
        foreach (var v in bodyMesh) top = Mathf.Max(top, root.transform.InverseTransformPoint(body.TransformPoint(v)).y);
        foreach (var v in bodyMesh)
        {
            var p = root.transform.InverseTransformPoint(body.TransformPoint(v));
            if (p.y > top - 0.03f) front = Mathf.Max(front, p.z);
        }
        data.topFront = new Vector3(data.halfWidth, top, front);
        // The back edge of the control panel's top board: the furthest back of
        // the body's faces that lie as the buttons do (the panel's slope).
        Vector3 panelUp = Vector3.up;
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == "Player01Button01") panelUp = root.transform.InverseTransformDirection(t.up);
        var bodyNormals = body.GetComponent<MeshFilter>().sharedMesh.normals;
        float panelBack = float.MaxValue, panelBackHeight = 0f;
        for (int i = 0; i < bodyMesh.Length; i++)
        {
            var p = root.transform.InverseTransformPoint(body.TransformPoint(bodyMesh[i]));
            var n = root.transform.InverseTransformDirection(body.TransformDirection(bodyNormals[i]));
            if (Vector3.Dot(n, panelUp) > 0.97f && Mathf.Abs(p.x) < data.halfWidth - 0.05f && p.y > 0f && p.y < 0.3f && p.z < panelBack)
            {
                panelBack = p.z;
                panelBackHeight = p.y;
            }
        }
        data.panelBack = panelBack;
        data.panelBackHeight = panelBackHeight;
        Object.DestroyImmediate(glass.gameObject);

        // The controls' coloured parts (CrtCabinetControls colours them as
        // Settings > COLORS has the on-screen ones): the button caps, and
        // the joysticks' balls - split off their shafts as a second part.
        var control = Material("CrtControl", m =>
        {
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.7f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        });
        // The rings round the buttons and the joysticks' bases, silver (they
        // were the paint's pink and purple): a part of the body of their own.
        // (The paint's discs under them on the panel are painted out of
        // ArcadeCabinet_AlbedoLit, the paint made brighter and richer.)
        var silver = Material("CrtSilver", m =>
        {
            m.SetColor("_BaseColor", new Color(0.82f, 0.83f, 0.88f));
            m.SetFloat("_Metallic", 0.55f);
            m.SetFloat("_Smoothness", 0.8f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.12f, 0.12f, 0.14f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        });
        var controls = new System.Collections.Generic.List<(Vector3 at, float reach)>();
        var buttons = new System.Collections.Generic.List<Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(t.name, @"^Player0\dButton0\d$")) { controls.Add((t.position, ButtonReach)); buttons.Add(t); }
            if (t.name.EndsWith("Joystick")) controls.Add((t.position, 0.06f));
        }
        var bodyFilter = body.GetComponent<MeshFilter>();
        var bodySource = bodyFilter.sharedMesh;
        bodyFilter.sharedMesh = TrimApart(bodySource, body, controls, ButtonReach);
        // Each button's bezel a piece of its own, and the buttons laid out as
        // the on-screen ones are (ButtonsInARow).
        foreach (var button in buttons) BezelFor(button, bodySource, body, silver);
        ButtonsInARow(root.transform);

        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.sharedMaterial = r.transform == screen ? tube : r.name.EndsWith(" Bezel") ? silver : cabinet;
            if (r.transform == body) r.sharedMaterials = new[] { cabinet, silver };
            if (System.Text.RegularExpressions.Regex.IsMatch(r.name, @"^Player0\dButton0\d$")) r.sharedMaterial = control;
            if (r.name.EndsWith("Joystick"))
            {
                var filter = r.GetComponent<MeshFilter>();
                filter.sharedMesh = BallApart(filter.sharedMesh, r.name);
                r.sharedMaterials = new[] { cabinet, control };
            }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);   // (its parts' meshes are made afresh)
        float tilt = Vector3.Angle(normal, Vector3.forward);
        return $"CrtCabinet: tilt {tilt:F1}°, screen {data.screenWidth:F3} x {data.screenHeight:F3}, picture {data.recess:F3} down, at {data.screenCenter}, half width {data.halfWidth:F3}, top front {data.topFront}, panel back {data.panelBack:F3} at {data.panelBackHeight:F3}";
    }

    // The body with the controls' trim - its triangles round them (within
    // `reach`) painted from the paint's flat pink / purple patches and
    // circles (u 0.2-0.32 and 0.8-1) - as a second part, saved beside the model.
    static Mesh TrimApart(Mesh source, Transform body, System.Collections.Generic.List<(Vector3 at, float reach)> controls, float buttonReach)
    {
        var mesh = Object.Instantiate(source);
        mesh.name = "Cabinet body (trim apart)";
        var v = mesh.vertices;
        var uv = mesh.uv;
        var all = mesh.triangles;
        var rest = new System.Collections.Generic.List<int>();
        var trim = new System.Collections.Generic.List<int>();
        for (int i = 0; i < all.Length; i += 3)
        {
            Vector2 u = (uv[all[i]] + uv[all[i + 1]] + uv[all[i + 2]]) / 3f;
            // (All three corners near one control: not the panel's big triangles that reach in.)
            bool near = false, nearButton = false;
            foreach (var control in controls)
            {
                bool inside = true;
                for (int k = 0; k < 3 && inside; k++) inside = (body.TransformPoint(v[all[i + k]]) - control.at).magnitude < control.reach;
                if (inside) { near = true; nearButton = control.reach == buttonReach; break; }
            }
            bool trimPaint = (u.x > 0.2f && u.x < 0.32f) || u.x > 0.8f;
            if (near && trimPaint && nearButton) continue;   // (a button's bezel: BezelFor)
            (near && trimPaint ? trim : rest).AddRange(new[] { all[i], all[i + 1], all[i + 2] });
        }
        mesh.subMeshCount = 2;
        mesh.SetTriangles(rest, 0);
        mesh.SetTriangles(trim, 1);
        string path = $"{Folder}/CabinetBodySplit.asset";
        // (Made afresh each time: copying over the old one left its old triangles in use.)
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    const float ButtonReach = 0.032f;

    // A button's silver bezel - the body's trim triangles round it - as a
    // piece of its own beside it (same parent, same place), to move with it.
    static void BezelFor(Transform button, Mesh bodyMesh, Transform body, Material silver)
    {
        var v = bodyMesh.vertices;
        var n = bodyMesh.normals;
        var uv = bodyMesh.uv;
        var all = bodyMesh.triangles;
        var verts = new System.Collections.Generic.List<Vector3>();
        var norms = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < all.Length; i += 3)
        {
            Vector2 u = (uv[all[i]] + uv[all[i + 1]] + uv[all[i + 2]]) / 3f;
            if (!((u.x > 0.2f && u.x < 0.32f) || u.x > 0.8f)) continue;
            bool inside = true;
            for (int k = 0; k < 3 && inside; k++) inside = (body.TransformPoint(v[all[i + k]]) - button.position).magnitude < ButtonReach;
            if (!inside) continue;
            for (int k = 0; k < 3; k++)
            {
                tris.Add(verts.Count);
                verts.Add(button.parent.InverseTransformPoint(body.TransformPoint(v[all[i + k]])) - button.localPosition);
                norms.Add(button.parent.InverseTransformDirection(body.TransformDirection(n[all[i + k]])));
            }
        }
        var mesh = new Mesh { name = button.name + " Bezel" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        string path = $"{Folder}/{button.name}Bezel.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        var bezel = new GameObject(button.name + " Bezel", typeof(MeshFilter), typeof(MeshRenderer));
        bezel.transform.SetParent(button.parent, false);
        bezel.transform.localPosition = button.localPosition;
        bezel.GetComponent<MeshFilter>().sharedMesh = mesh;
        bezel.GetComponent<MeshRenderer>().sharedMaterial = silver;
    }

    // Each player's three buttons on a rising diagonal, as the on-screen A,
    // B and C are (from the player: A front left, B, C back right - evenly
    // stepped across and up the panel), round where their cluster was.
    const float ButtonStepAcross = 0.045f, ButtonStepUp = 0.02f;
    static void ButtonsInARow(Transform root)
    {
        foreach (var player in new[] { "Player01", "Player02" })
        {
            Transform a = null, b = null, c = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == player + "Button02") a = t;
                if (t.name == player + "Button01") b = t;
                if (t.name == player + "Button03") c = t;
            }
            if (!a || !b || !c) continue;
            Vector3 middle = (a.position + b.position + c.position) / 3f;
            // The player's right is the model's -x (it faces its +z); up the
            // panel is away from them along its slope (from the front pair to the back one).
            Vector3 across = Vector3.left;
            Vector3 up = Vector3.ProjectOnPlane(b.position - (a.position + c.position) * 0.5f, Vector3.right).normalized;
            Move(a, middle - across * ButtonStepAcross - up * ButtonStepUp);
            Move(b, middle);
            Move(c, middle + across * ButtonStepAcross + up * ButtonStepUp);
        }
    }

    // A button and its bezel to `at`, the bezel's base kept on the panel (the
    // panel's a plane through the old cluster, so moving along it stays on it).
    static void Move(Transform button, Vector3 at)
    {
        var bezel = button.parent.Find(button.name + " Bezel");
        Vector3 delta = at - button.position;
        button.position = at;
        if (bezel) bezel.position += delta;
    }

    // A joystick with its ball (above the shaft, BallFrom up from its foot)
    // as a second part, saved beside the model.
    const float BallFrom = 0.03f;
    static Mesh BallApart(Mesh source, string name)
    {
        var mesh = Object.Instantiate(source);
        mesh.name = name + " (ball apart)";
        var v = mesh.vertices;
        var all = mesh.triangles;
        var shaft = new System.Collections.Generic.List<int>();
        var ball = new System.Collections.Generic.List<int>();
        for (int i = 0; i < all.Length; i += 3)
        {
            bool top = v[all[i]].y > BallFrom && v[all[i + 1]].y > BallFrom && v[all[i + 2]].y > BallFrom;
            (top ? ball : shaft).AddRange(new[] { all[i], all[i + 1], all[i + 2] });
        }
        mesh.subMeshCount = 2;
        mesh.SetTriangles(shaft, 0);
        mesh.SetTriangles(ball, 1);
        string path = $"{Folder}/{name}Split.asset";
        // (Made afresh each time: copying over the old one left its old triangles in use.)
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}.png");

    static Material Material(string name, System.Action<Material> setUp)
    {
        string path = $"{Folder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        setUp(m);
        EditorUtility.SetDirty(m);
        return m;
    }
}
