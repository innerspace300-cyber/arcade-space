// CrtCabinet.cs — with the CRT keycap on (CrtEffect.On), a 3D arcade
// cabinet (Resources/CrtCabinet: the neon "Retro Arcade Cabinet", low poly;
// for a vertical game Resources/CrtCabinetVertical, the rusty Japanese one,
// its near-square screen standing the tall picture)
// stands round the screen: sized so the game's picture fills its tube's
// opening, the picture set down in it just clear of the tube, and the picture tilted back to lie
// on that tube like an arcade monitor's - the layers spreading out of the
// tube along it (the demo's; a game's lie flat on the tube for now). The pixel frame is hidden while it's up (the cabinet's bezel
// takes its place), and the saves panel moves up by the front corner of its
// top, level with it.
//
// It grows to nearly life size as it comes on: on the floor (or a table) it
// stands on it, sized to bring its screen up to the viewer's eye (no bigger
// than life size), a soft fake shadow under it as if lit from overhead; on a wall it hangs with its back to it, and from a
// ceiling it hangs upright with its top just under it - both life size. The
// cabinet's tap box grows to take it in, so a tap anywhere on it picks the
// whole thing up to move, turn and scale (CabinetManipulator). Off,
// everything goes back as it was, at the size it had. On the AR cabinet's
// root (CrtEffect adds it).

using SpatialEmulator.Mobile;
using SpatialEmulator.UI;
using UnityEngine;

namespace SpatialEmulator
{
    // (After CabinetOrientation, so on the frame it's first fitted to the view
    // it's already in the 3D cabinet - the picture frame never shows first.)
    [DefaultExecutionOrder(1001)]
    public class CrtCabinet : MonoBehaviour
    {
        [Tooltip("The game picture's height over the cabinet's tube's (1: just filling the bezel's opening; its sides tuck in behind the bezel).")]
        public float overscan = 1f;
        [Tooltip("The saves panel's gap from the cabinet's side, as a share of the picture's width - room to press its keys without catching the cabinet.")]
        public float panelGap = 0.12f;
        [Tooltip("Largest the cabinet grows to as it comes on (1 = life size).")]
        public float lifeSize = 1f;
        [Tooltip("Gap between the cabinet and a wall or ceiling it hangs from, in metres.")]
        public float mountGap = 0.01f;

        enum Mount { Floor, Wall, Ceiling }

        MobileRetroDepthLayerStack _stack;
        ScreenFrame _frame;
        Canvas _frameCanvas;
        SaveStatePanel _panel;
        CabinetOrientation _orientation;
        BoxCollider _box;
        CrtCabinetModel _model;
        bool _modelVertical;   // _model's the vertical cabinet (CrtCabinetVertical)
        bool _shown;
        Mount _mount;
        Quaternion _stackRotation;
        Vector3 _stackPosition, _boxCenter, _boxSize;
        Vector3 _stackHome;   // where the picture sits in the 3D cabinet (tilted onto its tube)
        const float LipGap = 0.012f;   // the lifted HUD's bottom over the lip, in the model's metres
        const float HoodGap = 0.01f;   // behind the hood's front edge, in the model's metres
        float _scale;
        static readonly Quaternion Turn = Quaternion.Euler(0f, 180f, 0f);   // (the model faces its +z, the cabinet its -z)
        const float WallThickness = 0.035f;   // the cabinet's side panels, in the model's metres
        const float PanelGap = 0.07f;   // the front layer's gap from the control panel's back edge, in the model's metres: behind the lip under the screen
        const float StraightenSeconds = 0.35f;
        const float VerticalFill = 0.92f;   // a vertical game's picture: this much of the tube's height
        float _straight = -1f;   // 0 tilted onto the tube, 1 straight on (-1: not yet set)

        void Awake()
        {
            _stack = GetComponentInChildren<MobileRetroDepthLayerStack>(true);
            _frame = GetComponentInChildren<ScreenFrame>(true);
            _frameCanvas = _frame ? _frame.GetComponent<Canvas>() : null;
            _panel = GetComponentInChildren<SaveStatePanel>(true);
            _orientation = GetComponent<CabinetOrientation>();
            _box = GetComponent<BoxCollider>();
        }

        void OnDisable()
        {
            if (_shown) Show(false);
        }

        void OnDestroy()
        {
            if (_shadowMaterial) Destroy(_shadowMaterial);
        }

        void LateUpdate()
        {
            if (!_stack) return;
            // (Once it's been placed and fitted: it grows from there.)
            if (_orientation && !_orientation.Fitted) return;
            bool on = CrtEffect.On;
            // (A vertical game in its own cabinet, a horizontal one back in the
            // neon one: swapped as the game changes.)
            if (_shown && _model && _modelVertical != MobileRetroDepthLayerStack.Vertical) Show(false);
            if (on != _shown) Show(on);
            PlaysFlat = _shown && !SpatialEmulator.Demo.PitDemoGame.IsRunning;
            if (_shown) Fit();
        }

        void Show(bool on)
        {
            bool vertical = MobileRetroDepthLayerStack.Vertical;
            if (on && _model && _modelVertical != vertical)
            {
                Destroy(_model.gameObject);
                _model = null;
            }
            if (on && !_model)
            {
                var prefab = Resources.Load<CrtCabinetModel>(vertical ? "CrtCabinetVertical" : "CrtCabinet");
                if (!prefab) return;
                _modelVertical = vertical;
                _model = Instantiate(prefab, transform, false);
                _model.name = "CRT Cabinet";
                _model.gameObject.AddComponent<CrtCabinetControls>();   // (its stick and buttons follow the player's)
                // No real-time shadows (its fake one's on the floor): a
                // shadow pass and soft-shadow sampling every frame, saved.
                foreach (var r in _model.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                SetLayer(_model.transform, gameObject.layer);
                _model.gameObject.SetActive(false);
            }
            if (on) Shadow();
            _shown = on;
            if (on)
            {
                _mount = _orientation && _orientation.ceilingMounted ? Mount.Ceiling : _orientation && _orientation.wallMounted ? Mount.Wall : Mount.Floor;
                _stackPosition = _stack.transform.localPosition;
                _stackHome = _stackPosition;
                _stackRotation = _stack.transform.localRotation;
                if (_box) { _boxCenter = _box.center; _boxSize = _box.size; }
                _scale = transform.localScale.x;
                _model.gameObject.SetActive(true);
                PlaysFlat = !SpatialEmulator.Demo.PitDemoGame.IsRunning;
                if (_frameCanvas) _frameCanvas.enabled = false;

                // The picture moves within the cabinet so the 3D one stands
                // on the floor, backs onto the wall, or hangs from the ceiling.
                Fit();
                Bounds b = ModelBounds();
                var offset = _mount == Mount.Floor ? Vector3.up * -b.min.y
                    : _mount == Mount.Wall ? Vector3.back * (b.max.z - _stackPosition.z - mountGap)
                    : Vector3.up * (_boxCenter.y + _boxSize.y * 0.5f - b.max.y);
                _stackHome = _stackPosition + offset;
                _stack.transform.localPosition = _stackHome;
                Fit();

                // Then up to (nearly) life size, from where it meets the floor, wall or ceiling.
                float scale = lifeSize;
                var cam = Camera.main;
                if (_mount == Mount.Floor && cam)
                {
                    // (The screen's middle, at scale 1, is its height in metres over the floor.)
                    float eye = cam.transform.position.y - transform.position.y;
                    if (eye > 0f) scale = Mathf.Min(lifeSize, eye / _stackHome.y);
                }
                var manipulator = FindAnyObjectByType<CabinetManipulator>();
                if (manipulator) scale = Mathf.Clamp(scale, manipulator.minScale, manipulator.maxScale);
                ScaleAbout(Pivot(), scale);
                FitBox();
            }
            else
            {
                Vector3 pivot = Pivot();
                Vector3 anchor = transform.TransformPoint(pivot);
                _stack.transform.localPosition = _stackPosition;
                _stack.transform.localRotation = _stackRotation;
                _stack.MaxFarLayerWidth = 0f;
                _stack.MaxSpacingScale = 0f;
                LiftHud(0f);
                _straight = -1f;
                if (_box) { _box.center = _boxCenter; _box.size = _boxSize; }
                if (_model) _model.gameObject.SetActive(false);
                if (_frameCanvas) _frameCanvas.enabled = true;
                if (_panel) _panel.Corner = null;
                PlaysFlat = false;
                // Back to its size from before, its pivot where the 3D cabinet's was.
                transform.localScale = Vector3.one * _scale;
                transform.position += anchor - transform.TransformPoint(OffPivot());
            }
        }

        /// A game (not the demo) is in the 3D cabinet: its layers play on one
        /// plane, on the tube - only the demo's space out in it, for now.
        public static bool PlaysFlat { get; private set; }

        /// The 3D cabinet is up round the screen.
        public bool Shown => _shown && _model && _model.gameObject.activeSelf;

        /// Where the game picker shows in the cabinet (GamePicker, in AR): on
        /// the tube, just behind the glass, facing the player (its -z), and
        /// as wide as the tube, in world metres.
        public void TubePose(out Vector3 position, out Quaternion rotation, out float width)
        {
            float scale = _model.transform.localScale.x;
            Vector3 normal = Turn * _model.screenNormal;
            Vector3 glass = _model.transform.localPosition + Turn * (_model.screenCenter * scale);
            position = transform.TransformPoint(glass - normal * (0.005f * scale));
            rotation = transform.rotation * Quaternion.FromToRotation(Vector3.back, normal);   // (on the tube, however the picture's tilted)
            width = _model.screenWidth * scale * transform.lossyScale.x;
        }

        // A soft dark patch on the floor right under the cabinet, faked, as if
        // lit from straight overhead - standing on the floor only (none hung
        // on a wall or from a ceiling). On the model, so it moves and scales with it.
        const string ShadowName = "Shadow";
        static Texture2D s_shadowTexture;
        Material _shadowMaterial;

        void Shadow()
        {
            var shadow = _model.transform.Find(ShadowName);
            var mount = _orientation && _orientation.ceilingMounted ? Mount.Ceiling : _orientation && _orientation.wallMounted ? Mount.Wall : Mount.Floor;
            if (mount != Mount.Floor) { if (shadow) shadow.gameObject.SetActive(false); return; }
            if (!shadow)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = ShadowName;
                Destroy(go.GetComponent<Collider>());
                go.layer = gameObject.layer;
                shadow = go.transform;
                shadow.SetParent(_model.transform, false);
                if (!s_shadowTexture) s_shadowTexture = ShadowTexture(64);
                var shader = _stack.layerShader ? _stack.layerShader : Shader.Find("SpatialEmulator/LayerUnlit");
                _shadowMaterial = new Material(shader) { mainTexture = s_shadowTexture };
                _shadowMaterial.SetFloat("_FlipV", 0f);
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = _shadowMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            shadow.gameObject.SetActive(true);
            Bounds b = OwnBounds();
            // Flat on the floor, wider than its footprint - a centimetre up, clear
            // of the floor (and the AR planes) at any distance.
            shadow.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.localPosition = new Vector3(b.center.x, b.min.y + 0.01f, b.center.z);
            shadow.localScale = new Vector3(b.size.x * 1.7f, b.size.z * 1.7f, 1f);
        }

        // The model's extent in its own space (its shadow left out).
        Bounds OwnBounds()
        {
            var toModel = _model.transform.worldToLocalMatrix;
            bool any = false;
            var bounds = new Bounds();
            foreach (var filter in _model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh || filter.name == ShadowName) continue;
                var m = toModel * filter.transform.localToWorldMatrix;
                var mb = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        // Black, darkest in the middle and fading out to nothing at the edges
        // (a rounded square, so it follows the cabinet's box shape).
        static Texture2D ShadowTexture(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / size * 2f - 1f), v = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                    // Rounded-square distance from the middle: 0 there, 1 at the edge.
                    float d = Mathf.Pow(Mathf.Pow(u, 4f) + Mathf.Pow(v, 4f), 0.25f);
                    float fade = Mathf.InverseLerp(0.5f, 1f, d);
                    float a = 1f - fade * fade * (3f - 2f * fade);
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)(Mathf.Pow(a, 1.5f) * 0.8f * 255f));
                }
            t.SetPixels32(pixels);
            t.Apply(false, true);
            return t;
        }

        // Where it meets the floor (its base), the wall (behind the screen's
        // middle, where the picture hung before) or the ceiling (its top).
        Vector3 Pivot()
        {
            if (_mount == Mount.Floor) return Vector3.zero;
            if (_mount == Mount.Wall) return _stackPosition;
            return new Vector3(0f, ModelBounds().max.y, 0f);
        }

        // ...and the same with the 3D cabinet put away.
        Vector3 OffPivot() =>
            _mount == Mount.Floor ? Vector3.zero
            : _mount == Mount.Wall ? _stackPosition
            : new Vector3(0f, _boxCenter.y + _boxSize.y * 0.5f, 0f);

        void ScaleAbout(Vector3 pivot, float scale)
        {
            Vector3 anchor = transform.TransformPoint(pivot);
            transform.localScale = Vector3.one * scale;
            transform.position += anchor - transform.TransformPoint(pivot);
        }

        // The picture tilted onto the tube, the cabinet round it, the panel by its top.
        void Fit()
        {
            float width = _stack.screenHeight * _stack.PictureAspect * _stack.pixelAspect, height = _stack.screenHeight * _stack.pixelAspect;
            // The cabinet sized so the picture fills its tube's opening (top to
            // bottom; the tube's squarer than 4:3, so across it's a little wider).
            // A vertical game's tall picture stands in it a little inside its
            // top and bottom (VerticalFill: clear of the curved glass's edge),
            // the tube dark either side.
            float scale = MobileRetroDepthLayerStack.Vertical
                ? height / (_model.screenHeight * VerticalFill)
                : Mathf.Min(width / _model.screenWidth, height / _model.screenHeight) / overscan;
            Vector3 normal = Turn * _model.screenNormal;   // out of the glass, toward the viewer
            // Tilted onto the tube at the usual spacing; as it's spaced out it
            // straightens, in step with the layers spreading - straight on as
            // they're spread all the way, the layers coming out of the
            // cabinet toward the player.
            float most = _stack.MaxSpacingScale > 1f ? _stack.MaxSpacingScale : AppSettings.MaxLayerSpacingScale;
            float target = PlaysFlat ? 0f : Mathf.InverseLerp(1f, most, _stack.SpacingScale);
            if (_straight < 0f) _straight = target;   // (as it comes up: no swing)
            _straight = Mathf.MoveTowards(_straight, target, Time.unscaledDeltaTime / StraightenSeconds);
            float ease = _straight * _straight * (3f - 2f * _straight);
            _stack.transform.localRotation = Quaternion.Slerp(Quaternion.FromToRotation(Vector3.back, normal), Quaternion.identity, ease);
            var t = _model.transform;
            t.localRotation = Turn;
            t.localScale = Vector3.one * scale;
            Vector3 center = _stackHome;
            // The picture's back layer down in the cabinet, its curve just clear of the tube's.
            t.localPosition = center + normal * (_model.recess * scale) - Turn * (_model.screenCenter * scale);
            // Straight on, the whole picture slides back into the cabinet till
            // its back layer is under the hood (the marquee's front edge).
            float hood = (t.localPosition + Turn * (_model.topFront * scale)).z;
            float back = Mathf.Max(0f, hood - center.z) + HoodGap * scale;
            _stack.transform.localPosition = center + Vector3.forward * (back * ease);
            // Spaced out all the way, the front layer (the HUD) comes to the
            // back edge of the control panel's top board, the rest spread
            // between it and the back layer under the hood - no further, over
            // the joysticks and buttons. (The spacing setting counts for no
            // more than that here.)
            float panel = (t.localPosition + Turn * new Vector3(0f, 0f, _model.panelBack * scale)).z;
            float reach = center.z + back - panel - PanelGap * scale;
            // The HUD (the demo's front layer) lifted to show over the lip
            // under the screen as it straightens - its bottom at the lip's top.
            float lip = (t.localPosition + Turn * new Vector3(0f, _model.panelBackHeight, _model.panelBack) * scale).y + LipGap * scale;
            LiftHud(Mathf.Max(0f, lip - (_stack.transform.localPosition.y - _stack.screenHeight * 0.5f)) * ease);
            float steps = _stack.DepthSteps;
            if (steps > 0f && _stack.layerSpacing > 0f)
                _stack.MaxSpacingScale = Mathf.Max(1.05f, reach / (steps * _stack.layerSpacing));
            // The far layers, growing with the spacing, kept inside its walls.
            _stack.MaxFarLayerWidth = (_model.halfWidth - WallThickness) * 2f * scale;
            if (_panel)
            {
                // The front corner of its top on the viewer's right, then a gap.
                Vector3 corner = t.localPosition + Turn * (Vector3.Scale(_model.topFront, new Vector3(-1f, 1f, 1f)) * scale);
                if (corner.x < center.x) corner = t.localPosition + Turn * (_model.topFront * scale);
                _panel.Corner = corner + Vector3.right * (width * panelGap);
            }
        }

        // The demo's HUD layer up by `lift` (the cabinet's metres; it's square on, then).
        void LiftHud(float lift)
        {
            for (int i = 0; i < _stack.LayerCount; i++)
            {
                var quad = _stack.LayerQuad(i);
                if (!quad || !quad.name.EndsWith(SpatialEmulator.Demo.PitDemoGame.HudLayer)) continue;
                var p = quad.transform.localPosition;
                if (!Mathf.Approximately(p.y, lift)) quad.transform.localPosition = new Vector3(p.x, lift, p.z);
            }
        }

        // The cabinet's tap box takes in the whole 3D cabinet and the picture.
        void FitBox()
        {
            if (!_box) return;
            Bounds b = ModelBounds();
            b.Encapsulate(new Bounds(_boxCenter + (_stackHome - _stackPosition), _boxSize));
            _box.center = b.center;
            _box.size = b.size;
        }

        // The 3D cabinet's extent in the cabinet's own space.
        Bounds ModelBounds()
        {
            var bounds = new Bounds(_model.transform.localPosition, Vector3.zero);
            var toCabinet = transform.worldToLocalMatrix;
            foreach (var filter in _model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh || filter.name == ShadowName) continue;
                var m = toCabinet * filter.transform.localToWorldMatrix;
                var mb = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    bounds.Encapsulate(m.MultiplyPoint3x4(c));
                }
            }
            return bounds;
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayer(child, layer);
        }
    }
}
