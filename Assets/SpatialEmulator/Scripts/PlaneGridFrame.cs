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

        void Awake() => Send();

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
