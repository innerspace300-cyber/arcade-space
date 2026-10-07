// CabinetGizmo.cs — the handles shown on a selected cabinet: three move
// arrows, X (red) along its left/right, Y (green) up, Z (blue) toward the
// viewer, and a white ring around its base for turning it. The arrows keep a
// constant size on screen; the ring hugs the cabinet's footprint. Everything
// draws over the scene (GizmoOverlay shader), fades with alpha, and is
// hit-tested in screen space with a generous reach so it's easy to grab with
// a thumb. CabinetManipulator does the dragging.

using System.Collections.Generic;
using UnityEngine;

namespace SpatialEmulator
{
    // After CabinetOrientation's LateUpdate clamp, so the arrows sit on the
    // cabinet's final pose for the frame.
    [DefaultExecutionOrder(1100)]
    public class CabinetGizmo : MonoBehaviour
    {
        public const int None = -1;
        public const int Ring = 3; // handles 0-2 are the X/Y/Z arrows
        static readonly string[] HandleNames = { "Arrow X", "Arrow Y", "Arrow Z", "Rotate Ring" };
        static readonly Color[] HandleColors =
        {
            new Color(0.95f, 0.27f, 0.27f),
            new Color(0.36f, 0.86f, 0.32f),
            new Color(0.30f, 0.56f, 1.00f),
            new Color(0.93f, 0.93f, 0.96f),
        };
        const int RingSamples = 48;
        static readonly Color HighlightColor = new Color(1f, 0.85f, 0.2f);

        [Tooltip("Arrow length as a fraction of the camera's distance, which keeps it the same size on screen.")]
        public float screenSize = 0.16f;
        [Tooltip("Ring radius as a multiple of half the cabinet's widest footprint side.")]
        public float ringMargin = 0.45f;

        Transform _target;
        Vector3 _localCenter;
        Vector2 _footprint; // local box size in X and Z
        readonly Transform[] _handles = new Transform[4];
        readonly Material[] _materials = new Material[4];
        int _highlight = None;
        float _alpha = 1f;

        public static CabinetGizmo Create(Shader shader)
        {
            var go = new GameObject("Cabinet Gizmo");
            var gizmo = go.AddComponent<CabinetGizmo>();
            var arrowMesh = ArrowMesh();
            var ringMesh = RingMesh();
            for (int i = 0; i < 4; i++)
            {
                var handle = new GameObject(HandleNames[i], typeof(MeshFilter), typeof(MeshRenderer));
                handle.transform.SetParent(go.transform, false);
                handle.GetComponent<MeshFilter>().sharedMesh = i == Ring ? ringMesh : arrowMesh;
                var renderer = handle.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                gizmo._materials[i] = new Material(shader) { color = HandleColors[i] };
                renderer.sharedMaterial = gizmo._materials[i];
                gizmo._handles[i] = handle.transform;
            }
            go.SetActive(false);
            return gizmo;
        }

        bool _showRing = true;

        public bool visible => gameObject.activeSelf && _target;

        /// Shows the gizmo on target: arrows from localCenter, and (if
        /// showRing) a ring around a footprint of localSize's X and Z (both in
        /// target's space).
        public void Show(Transform target, Vector3 localCenter, Vector3 localSize, bool showRing = true)
        {
            _target = target;
            _localCenter = localCenter;
            _footprint = new Vector2(localSize.x, localSize.z);
            _showRing = showRing;
            _handles[Ring].gameObject.SetActive(showRing);
            alpha = 1f;
            gameObject.SetActive(true);
            LateUpdate();
        }

        /// Opacity of every handle, for fading out.
        public float alpha
        {
            get => _alpha;
            set
            {
                _alpha = Mathf.Clamp01(value);
                ApplyColors();
            }
        }

        public void Hide()
        {
            SetHighlight(None);
            _target = null;
            gameObject.SetActive(false);
        }

        public Vector3 origin => _target.TransformPoint(_localCenter);

        /// Center of the ring: under the middle of the cabinet, level with its
        /// pivot (which sits on the surface it was placed on).
        public Vector3 ringCenter => _target.TransformPoint(new Vector3(_localCenter.x, 0f, _localCenter.z));

        float RingRadius => Mathf.Max(_footprint.x, _footprint.y) * 0.5f * _target.lossyScale.x * ringMargin;

        /// World direction of an axis: 0 = X (the cabinet's left/right),
        /// 1 = Y (up), 2 = Z (toward the viewer). X and Z follow the cabinet's yaw.
        public Vector3 Axis(int axis)
        {
            if (axis == 1) return Vector3.up;
            // The cabinet's forward points away from the camera (CabinetOrientation).
            Vector3 dir = axis == 0 ? _target.right : -_target.forward;
            dir.y = 0f;
            return dir.sqrMagnitude > 1e-6f ? dir.normalized : (axis == 0 ? Vector3.right : Vector3.back);
        }

        /// The handle (an arrow's axis, or Ring) nearest screenPosition
        /// within reachPixels, or None.
        public int HitTest(Vector2 screenPosition, float reachPixels)
        {
            var cam = Camera.main;
            if (!visible || !cam) return None;
            Vector3 o = origin;
            float length = Length(cam, o);
            int best = None;
            float bestDistance = reachPixels;
            for (int i = 0; i < 3; i++)
            {
                // Skip the crowded middle where the three arrows meet.
                Vector3 a = cam.WorldToScreenPoint(o + Axis(i) * length * 0.3f);
                Vector3 b = cam.WorldToScreenPoint(o + Axis(i) * length * 1.05f);
                if (a.z <= 0f || b.z <= 0f) continue;
                float distance = DistanceToSegment(screenPosition, a, b);
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }

            if (!_showRing) return best;
            Vector3 center = ringCenter;
            float radius = RingRadius;
            Vector3 previous = default;
            for (int k = 0; k <= RingSamples; k++)
            {
                float angle = k * Mathf.PI * 2f / RingSamples;
                Vector3 point = cam.WorldToScreenPoint(center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius);
                if (k > 0 && point.z > 0f && previous.z > 0f)
                {
                    float distance = DistanceToSegment(screenPosition, previous, point);
                    if (distance < bestDistance) { bestDistance = distance; best = Ring; }
                }
                previous = point;
            }
            return best;
        }

        public void SetHighlight(int handle)
        {
            if (handle == _highlight) return;
            _highlight = handle;
            ApplyColors();
        }

        void ApplyColors()
        {
            for (int i = 0; i < 4; i++)
            {
                if (!_materials[i]) continue;
                Color color = i == _highlight ? HighlightColor : HandleColors[i];
                color.a = _alpha;
                _materials[i].color = color;
            }
        }

        void LateUpdate()
        {
            if (!_target)
            {
                Hide(); // the cabinet was deleted
                return;
            }
            var cam = Camera.main;
            if (!cam) return;
            Vector3 o = origin;
            float length = Length(cam, o);
            transform.position = o;
            for (int i = 0; i < 3; i++)
            {
                _handles[i].SetPositionAndRotation(o, Quaternion.FromToRotation(Vector3.up, Axis(i)));
                _handles[i].localScale = Vector3.one * length;
            }
            // A hair above the surface; the overlay shader shows it regardless.
            _handles[Ring].SetPositionAndRotation(ringCenter + Vector3.up * 0.005f, Quaternion.identity);
            _handles[Ring].localScale = Vector3.one * RingRadius;
        }

        void OnDestroy()
        {
            foreach (var material in _materials)
                if (material) Destroy(material);
        }

        float Length(Camera cam, Vector3 o) => Vector3.Distance(cam.transform.position, o) * screenSize;

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        // Flat torus in the XZ plane: radius 1, tube radius 0.03.
        static Mesh RingMesh()
        {
            const int around = 64, tube = 8;
            const float tubeRadius = 0.03f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i <= around; i++)
            {
                float a = i * Mathf.PI * 2f / around;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j <= tube; j++)
                {
                    float b = j * Mathf.PI * 2f / tube;
                    Vector3 normal = radial * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    vertices.Add(radial + normal * tubeRadius);
                    normals.Add(normal);
                }
            }
            for (int i = 0; i < around; i++)
            {
                for (int j = 0; j < tube; j++)
                {
                    int k = i * (tube + 1) + j, next = k + tube + 1;
                    triangles.AddRange(new[] { k, k + 1, next, k + 1, next + 1, next });
                }
            }
            var mesh = new Mesh { name = "Gizmo Ring" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Unit arrow along +Y: a thin capped shaft from 0.12 to 0.8, then a cone to the tip.
        static Mesh ArrowMesh()
        {
            const int segments = 16;
            const float shaftRadius = 0.022f, shaftStart = 0.12f, shaftEnd = 0.8f, headRadius = 0.07f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            Vector3 Ring(int i) { float a = i * Mathf.PI * 2f / segments; return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }

            // Shaft side.
            int start = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                Vector3 d = Ring(i);
                vertices.Add(d * shaftRadius + Vector3.up * shaftStart); normals.Add(d);
                vertices.Add(d * shaftRadius + Vector3.up * shaftEnd); normals.Add(d);
            }
            for (int i = 0; i < segments; i++)
            {
                int k = start + i * 2;
                triangles.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
            }

            // Cone side; one apex vertex per segment so its normals stay smooth.
            float slope = headRadius / (1f - shaftEnd);
            start = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                Vector3 d = Ring(i);
                Vector3 normal = new Vector3(d.x, slope, d.z).normalized;
                vertices.Add(d * headRadius + Vector3.up * shaftEnd); normals.Add(normal);
                vertices.Add(Vector3.up); normals.Add(normal);
            }
            for (int i = 0; i < segments; i++)
            {
                int k = start + i * 2;
                triangles.AddRange(new[] { k, k + 1, k + 2 });
            }

            // Downward-facing caps: the shaft's end and the cone's base.
            foreach (var (y, radius) in new[] { (shaftStart, shaftRadius), (shaftEnd, headRadius) })
            {
                int center = vertices.Count;
                vertices.Add(Vector3.up * y); normals.Add(Vector3.down);
                for (int i = 0; i <= segments; i++) { vertices.Add(Ring(i) * radius + Vector3.up * y); normals.Add(Vector3.down); }
                for (int i = 0; i < segments; i++)
                    triangles.AddRange(new[] { center, center + 1 + i, center + 2 + i });
            }

            var mesh = new Mesh { name = "Gizmo Arrow" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
