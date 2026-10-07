// CrtCabinetControls.cs — the CRT cabinet's own player 1 controls move with
// the player's: its joystick leans the way the stick (touch or controller)
// is pushed, and its buttons go down while theirs are held - A, B and C
// (left, top, right of its three), and 1P START. ArcadeInput is both the touch
// controls and a controller. Both players' joystick balls and buttons take
// the colours picked for the on-screen ones (Settings > COLORS,
// ControlColors): CLASSIC's red knob and red / yellow / green buttons, or
// one colour in A's deep, B's own and C's light shades. On the cabinet
// model (CrtCabinet adds it).

using SpatialEmulator.Controls;
using UnityEngine;

namespace SpatialEmulator
{
    public class CrtCabinetControls : MonoBehaviour
    {
        [Tooltip("How far the joystick leans, pushed all the way, in degrees.")]
        public float leanDegrees = 18f;
        [Tooltip("How far a button goes down while held, in the model's metres.")]
        public float pressDepth = 0.006f;
        [Tooltip("How quickly they follow (higher is snappier).")]
        public float follow = 30f;

        struct Part
        {
            public Transform t;
            public Vector3 position;
            public Quaternion rotation;
            public uint button;
        }

        Part _stick;
        Part[] _buttons;
        Vector2 _lean;

        static readonly Color Yellow = new Color32(255, 214, 30, 255), Green = new Color32(57, 200, 60, 255);
        static readonly Color ClassicRed = new Color32(178, 18, 34, 255);   // (the on-screen knob's deep red)
        Material _ball, _a, _b, _c;

        void OnEnable()
        {
            ControlColors.Changed += Paint;
            Paint();
        }

        void OnDisable() => ControlColors.Changed -= Paint;

        void Paint()
        {
            int stick = ControlColors.Joystick, buttons = ControlColors.Buttons;
            Color one = ControlColors.Swatches[buttons];
            bool classic = buttons == 0;
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name.EndsWith("Joystick") && r.sharedMaterials.Length > 1)
                {
                    // (The ball: its material's the controls' own - CrtControl, RustyCabinet_Control.)
                    var mats = r.sharedMaterials;
                    int ball = System.Array.FindIndex(mats, m => m && (m == _ball || m.name.Contains("Control")));
                    if (ball < 0) ball = 1;
                    if (!_ball) _ball = new Material(mats[ball]);
                    mats[ball] = _ball;
                    r.sharedMaterials = mats;
                }
                else if (r.name.EndsWith("Button02")) r.sharedMaterial = _a ? _a : _a = new Material(r.sharedMaterial);
                else if (r.name.EndsWith("Button01")) r.sharedMaterial = _b ? _b : _b = new Material(r.sharedMaterial);
                else if (r.name.EndsWith("Button03")) r.sharedMaterial = _c ? _c : _c = new Material(r.sharedMaterial);
            }
            Tint(_ball, stick == 0 ? ClassicRed : ControlColors.Swatches[stick]);
            Tint(_a, classic ? ClassicRed : one * 0.72f);                                 // A: deep
            Tint(_b, classic ? Yellow : one);                                             // B: the colour
            Tint(_c, classic ? Green : Color.Lerp(one, Color.white, 0.35f));              // C: light
        }

        // A colour with a little glow of its own, neon-ish.
        static void Tint(Material m, Color color)
        {
            if (!m) return;
            color.a = 1f;
            m.SetColor("_BaseColor", color);
            m.SetColor("_EmissionColor", color * 0.35f);
        }

        void OnDestroy()
        {
            foreach (var m in new[] { _ball, _a, _b, _c }) if (m) Destroy(m);
        }

        void Awake()
        {
            _stick = Find("Player01Joystick", 0);
            _buttons = new[]
            {
                Find("Player01Button02", (uint)RetroPadButton.B),   // A (red): the left one
                Find("Player01Button01", (uint)RetroPadButton.A),   // B (yellow): the top one
                Find("Player01Button03", (uint)RetroPadButton.Y),   // C (green): the right one
                Find("Player01", (uint)RetroPadButton.Start),       // 1P START
            };
        }

        Part Find(string name, uint button)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == name) return new Part { t = t, position = t.localPosition, rotation = t.localRotation, button = button };
            return default;
        }

        void LateUpdate()
        {
            float k = 1f - Mathf.Exp(-follow * Time.unscaledDeltaTime);
            if (_stick.t)
            {
                float x = (Held(RetroPadButton.Right) ? 1f : 0f) - (Held(RetroPadButton.Left) ? 1f : 0f);
                float y = (Held(RetroPadButton.Up) ? 1f : 0f) - (Held(RetroPadButton.Down) ? 1f : 0f);
                var target = new Vector2(x, y);
                if (target.sqrMagnitude > 1f) target.Normalize();
                target *= Mathf.Max(ArcadeInput.Push, target.sqrMagnitude > 0f ? 0.6f : 0f);
                _lean = Vector2.Lerp(_lean, target, k);
                // (The model faces away from its own +z, turned round to face the player:
                // the player's right is its -x, away from them its -z. Leaned in
                // the cabinet's own frame - upright - whatever way the part's turned.)
                _stick.t.localRotation = Quaternion.Euler(-_lean.y * leanDegrees, 0f, _lean.x * leanDegrees) * _stick.rotation;
            }
            foreach (var b in _buttons)
            {
                if (!b.t) continue;
                Vector3 down = Vector3.down * pressDepth;   // (straight down into the panel, in the cabinet's frame)
                Vector3 target = b.position + (ArcadeInput.IsPressed(b.button) ? down : Vector3.zero);
                b.t.localPosition = Vector3.Lerp(b.t.localPosition, target, k);
            }
        }

        static bool Held(RetroPadButton button) => ArcadeInput.IsPressed((uint)button);
    }
}
