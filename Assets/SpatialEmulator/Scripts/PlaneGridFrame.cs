// PlaneGridFrame.cs — one neon grid for the whole room (PlaneGrid.shader), so the lines on the floor run on up the
// walls instead of each plane drawing its own. The room's biggest wall squares the grid up and puts a line along its
// foot; the biggest wall square to that one does the same the other way; the biggest floor sets where height starts,
// so a line runs round the room where each wall meets it. Looked over a few times a second while planes are found,
// and only changed when the room's really moved on (a sudden shift of the whole grid is noticed; a few mm isn't worth it).

using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace SpatialEmulator
{
    public class PlaneGridFrame : MonoBehaviour
    {
        const float CheckEvery = 0.25f;
        const float SquareTo = 0.96f;     // (how nearly a wall has to line up with the first to count as square to it)
        const float MoveBy = 0.02f;       // (metres: less than this of a change, the grid stays where it is)
        const float TurnBy = 2f;          // (degrees, the same)

        static readonly int FrameId = Shader.PropertyToID("_GridFrame"), FloorId = Shader.PropertyToID("_GridFloorY");

        static readonly int GlowId = Shader.PropertyToID("_GridGlowColor"), LineId = Shader.PropertyToID("_GridLineColor");

        // Settings' neons (AppSettings.GridColor): the bloom round the lines, and the lines themselves.
        // NEON RGB, last, is a rainbow flowing across the room (PlaneGrid.shader, _GridRainbow); its colours here stand in for it
        // where one colour's needed (Settings' name for it).
        public static readonly string[] ColorNames = { "NEON BLUE", "NEON PINK", "NEON ORANGE", "NEON AQUA", "NEON RGB" };
        public static readonly Color[] Glows = { new Color(0f, 0.5f, 1f), new Color(1f, 0.08f, 0.58f), new Color(1f, 0.38f, 0f), new Color(0f, 0.9f, 0.75f), new Color(0.75f, 0.3f, 1f) };
        public static readonly Color[] Lines = { new Color(0.55f, 0.9f, 1f), new Color(1f, 0.7f, 0.9f), new Color(1f, 0.8f, 0.5f), new Color(0.6f, 1f, 0.95f), new Color(0.9f, 0.75f, 1f) };
        // Each single neon's five shades and neighbouring hues, looped through in order (PlaneGrid.shader _GridShades).
        static Color C(float r, float g, float b) => new Color(r, g, b);
        public static readonly Color[][] Shades =
        {
            new[] { C(0.25f, 0.1f, 1f), C(0f, 0.35f, 1f), C(0f, 0.65f, 1f), C(0.2f, 0.95f, 1f), C(0f, 0.5f, 1f) },          // NEON BLUE: violet-blue, electric, neon, ice cyan, blue
            new[] { C(0.6f, 0f, 1f), C(1f, 0f, 0.7f), C(1f, 0.08f, 0.45f), C(1f, 0.4f, 0.75f), C(1f, 0.15f, 0.9f) },       // NEON PINK: purple, magenta, hot pink, candy, fuchsia
            new[] { C(1f, 0.08f, 0f), C(1f, 0.3f, 0f), C(1f, 0.5f, 0f), C(1f, 0.75f, 0.05f), C(1f, 0.4f, 0.15f) },         // NEON ORANGE: red, orange, tangerine, gold, coral
            new[] { C(0f, 0.5f, 1f), C(0f, 0.8f, 0.95f), C(0f, 1f, 0.75f), C(0.3f, 1f, 0.5f), C(0f, 0.95f, 0.95f) },       // NEON AQUA: blue-teal, cyan, aqua, mint, turquoise
            new[] { C(1f, 0f, 0f), C(1f, 1f, 0f), C(0f, 1f, 0f), C(0f, 0.5f, 1f), C(1f, 0f, 1f) },                          // (NEON RGB uses the rainbow instead)
        };
        public const int Rgb = 4;
        public static bool IsRgb => AppSettings.GridColor == Rgb;

        // (Was earned: ENDLESS KNIGHT's ending, the dark crystal, unlocked it for good. See RgbForEveryone.)
        const string RgbUnlockKey = "SpatialEmulator.neonRgbUnlocked";
        public const string RgbUnlockHint = "BEAT ENDLESS KNIGHT TO UNLOCK";
        // NEON RGB is open to everyone (Wayne, 2026-11-09): no longer earned. The unlock is kept, unused, in case.
        const bool RgbForEveryone = true;
        public static bool RgbUnlocked => RgbForEveryone || PlayerPrefs.GetInt(RgbUnlockKey, 0) != 0;
        public static bool Locked(int color) => color == Rgb && !RgbUnlocked;
        public static void UnlockRgb()
        {
            if (RgbUnlocked) return;
            PlayerPrefs.SetInt(RgbUnlockKey, 1);
            PlayerPrefs.Save();
        }
        static readonly int RainbowId = Shader.PropertyToID("_GridRainbow");
        static readonly int ShadesId = Shader.PropertyToID("_GridShades");
        static readonly Vector4[] s_shades = new Vector4[5];

        static void ApplyColor()
        {
            int i = AppSettings.GridColor;
            Shader.SetGlobalColor(GlowId, Glows[i]);
            Shader.SetGlobalColor(LineId, Lines[i]);
            Shader.SetGlobalFloat(RainbowId, i == Rgb ? 1f : 0f);
            for (int k = 0; k < s_shades.Length; k++) s_shades[k] = Shades[i][k];
            Shader.SetGlobalVectorArray(ShadesId, s_shades);
        }

        static Vector2 s_axis = Vector2.right;

        /// The nearest of the room grid's four directions to `direction` (flat on the floor): what a cabinet squares up to.
        public static Vector3 Square(Vector3 direction)
        {
            float across = direction.x * s_axis.x + direction.z * s_axis.y;
            float along = direction.z * s_axis.x - direction.x * s_axis.y;
            var flat = Mathf.Abs(across) >= Mathf.Abs(along) ? s_axis * Mathf.Sign(across) : new Vector2(-s_axis.y, s_axis.x) * Mathf.Sign(along);
            return new Vector3(flat.x, 0f, flat.y);
        }

        ARPlaneManager _planes;
        float _nextCheck;
        Vector2 _across = Vector2.right;   // (the first wall's normal, flat on the floor)
        float _offsetU, _offsetV, _floorY;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("Plane Grid Frame");
            DontDestroyOnLoad(go);
            go.AddComponent<PlaneGridFrame>();
        }

        void Awake()
        {
            Send();
            ApplyColor();
            AppSettings.Changed += ApplyColor;
        }

        void OnDestroy() => AppSettings.Changed -= ApplyColor;

        void Update()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + CheckEvery;
            if (!_planes) _planes = FindAnyObjectByType<ARPlaneManager>();
            if (!_planes) return;

            ARPlane wall = null, side = null, floor = null;
            float wallSize = 0f, floorSize = 0f;
            foreach (var plane in _planes.trackables)
            {
                float size = plane.size.x * plane.size.y;
                if (plane.alignment == PlaneAlignment.HorizontalUp) { if (size > floorSize) { floor = plane; floorSize = size; } }
                else if (plane.alignment == PlaneAlignment.Vertical && size > wallSize) { wall = plane; wallSize = size; }
            }
            if (wall)
            {
                var facing = Flat(wall.normal);
                if (Vector2.Angle(facing, _across) > TurnBy && Vector2.Angle(-facing, _across) > TurnBy) _across = facing;
                float sideSize = 0f;
                foreach (var plane in _planes.trackables)
                {
                    if (plane.alignment != PlaneAlignment.Vertical || plane == wall) continue;
                    float size = plane.size.x * plane.size.y;
                    if (size > sideSize && Mathf.Abs(Vector2.Dot(Flat(plane.normal), Along)) >= SquareTo) { side = plane; sideSize = size; }
                }
                Settle(ref _offsetU, Vector2.Dot(Flat2(wall.center), _across));
                if (side) Settle(ref _offsetV, Vector2.Dot(Flat2(side.center), Along));
            }
            if (floor) Settle(ref _floorY, floor.center.y);
            Send();
        }

        Vector2 Along => new Vector2(-_across.y, _across.x);

        static Vector2 Flat(Vector3 direction) => new Vector2(direction.x, direction.z).normalized;
        static Vector2 Flat2(Vector3 position) => new Vector2(position.x, position.z);

        static void Settle(ref float value, float to)
        {
            if (Mathf.Abs(value - to) >= MoveBy) value = to;
        }

        void Send()
        {
            s_axis = _across;
            Shader.SetGlobalVector(FrameId, new Vector4(_across.x, _across.y, _offsetU, _offsetV));
            Shader.SetGlobalFloat(FloorId, _floorY);
        }
    }
}
