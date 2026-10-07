// CrtEffect.cs — ARcade's CRT look for the cabinet's screen, switched by the
// CRT keycap (on: its pressed cap) - remembered, so a cabinet's placed in
// whichever the player used last. On, each layer's quad shows the game through the
// layer shader's CRT filter (scanlines, an aperture grille, dimmed corners:
// LayerUnlit _Crt) and bulges toward you like a tube's glass - every layer
// the same curve, so the whole spaced-out picture curves together. The HUD
// shown above the frame (the demo's) stays flat and plain. On the layer stack
// (MobileRetroDepthLayerStack adds it); it follows the layers as games start.

using SpatialEmulator.Mobile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpatialEmulator
{
    [RequireComponent(typeof(MobileRetroDepthLayerStack))]
    public class CrtEffect : MonoBehaviour
    {
        // Kept between launches: a cabinet's placed the way the last one was
        // left - in its picture frame, or in the 3D CRT cabinet.
        const string Key = "ARcade.CRT";
        static bool s_on;
        public static bool On
        {
            get => s_on;
            set
            {
                s_on = value;
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }
        public static event System.Action Changed;

        [Tooltip("How far the middle of the screen bulges out, as a share of its width.")]
        public const float Bulge = 0.1f;
        public float bulge = Bulge;

        static Mesh s_curved;
        static readonly int CrtId = Shader.PropertyToID("_Crt");
        MobileRetroDepthLayerStack _stack;
        Mesh _flat;
        bool _applied;
        readonly Renderer[] _done = new Renderer[32];

        void Awake() => _stack = GetComponent<MobileRetroDepthLayerStack>();
        void OnEnable() => Changed += Refresh;
        void OnDisable() => Changed -= Refresh;

        System.Action _showKeys;   // (the CAB and FRAME keys' lit state, kept up by Changed)
        void OnDestroy() { if (_showKeys != null) Changed -= _showKeys; }
        void Refresh() => System.Array.Clear(_done, 0, _done.Length);

        void Start()
        {
            On = PlayerPrefs.GetInt(Key, 0) == 1;   // (as it was last left)
            AddButton();
            // The 3D cabinet round the screen while it's on (CrtCabinet).
            var cabinet = transform.parent ? transform.parent : transform;
            if (!cabinet.GetComponent<CrtCabinet>()) cabinet.gameObject.AddComponent<CrtCabinet>();
        }

        void LateUpdate()
        {
            bool on = On;
            for (int i = 0; i < _stack.LayerCount && i < _done.Length; i++)
            {
                var quad = _stack.LayerQuad(i);
                if (!quad) continue;
                bool hud = quad.transform == _stack.AboveFrameQuad;
                // (New quads, or the switch flipped: set them up once.)
                if (_done[i] != quad)
                {
                    _done[i] = quad;
                    var filter = quad.GetComponent<MeshFilter>();
                    if (!_flat && filter) _flat = filter.sharedMesh;
                    if (filter) filter.sharedMesh = on && !hud ? Curved() : _flat;
                    if (quad.sharedMaterial) quad.sharedMaterial.SetFloat(CrtId, on && !hud ? 1f : 0f);
                }
                // The curve's depth goes with the quad's width (its z scale, unused flat).
                if (on && !hud)
                {
                    var s = quad.transform.localScale;
                    if (!Mathf.Approximately(s.z, s.x)) quad.transform.localScale = new Vector3(s.x, s.y, s.x);
                }
            }
        }

        // A screen-sized grid bulging toward the viewer (-z), most in the
        // middle, flat at the edges (where the frame holds it).
        Mesh Curved()
        {
            if (s_curved) return s_curved;
            const int nx = 32, ny = 24;
            var vertices = new Vector3[(nx + 1) * (ny + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[nx * ny * 6];
            for (int y = 0, v = 0; y <= ny; y++)
                for (int x = 0; x <= nx; x++, v++)
                {
                    float u = x / (float)nx, w = y / (float)ny;
                    float dx = u * 2f - 1f, dy = w * 2f - 1f;
                    float depth = (1f - dx * dx) * (1f - dy * dy);
                    vertices[v] = new Vector3(u - 0.5f, w - 0.5f, -bulge * depth);
                    uvs[v] = new Vector2(u, w);
                }
            for (int y = 0, t = 0; y < ny; y++)
                for (int x = 0; x < nx; x++, t += 6)
                {
                    int a = y * (nx + 1) + x, b = a + 1, c = a + nx + 1, d = c + 1;
                    triangles[t] = a; triangles[t + 1] = c; triangles[t + 2] = b;
                    triangles[t + 3] = b; triangles[t + 4] = c; triangles[t + 5] = d;
                }
            s_curved = new Mesh { name = "CRT Screen", vertices = vertices, uv = uvs, triangles = triangles };
            s_curved.RecalculateNormals();
            s_curved.RecalculateBounds();
            return s_curved;
        }

        // The CRT keycap: a copy of LOCK's. It, CAP (ScreenCap) and LOCK go in
        // the saves panel (DEMO in the demo) as rows of their own under
        // RESTART - CRT MODE, CAP, LOCK - laid out with the panel's other keys.
        void AddButton()
        {
            var cabinet = transform.parent ? transform.parent : transform;
            var lockButton = cabinet.GetComponentInChildren<CabinetLockButton>(true);
            if (!lockButton) return;
            var lockRect = (RectTransform)lockButton.transform;
            // The keys go under GAMES, on the panel left of the screen; the
            // right one gets SCORES (the demo's leaderboard).
            var save = lockButton.GetComponentInParent<UI.SaveStatePanel>(true);
            if (save) UI.LeaderboardWindow.AddKey(save.transform.Find("Content"));
            var content = UI.GamesPanel.Create(save);
            if (!content || content.Find("CRT Line")) return;
            var copy = Instantiate(lockButton.gameObject, lockRect.parent);
            copy.name = "CRT Button";
            Destroy(copy.GetComponent<CabinetLockButton>());
            var frameKey = FrameButton(lockButton);
            InLine(frameKey, content, "Frame Line");
            InLine(FullScreenButton(lockButton), content, "Full Screen Line");
            InLine((RectTransform)copy.transform, content, "CRT Line");
            InLine(UI.ScreenCap.MakeButton(lockButton), content, "Cap Line");
            // LOCK stays a key of its own under the panel (its StackBelow).
            lockRect.SetParent(content.parent, false);
            if (lockRect.GetComponent<UI.StackBelow>() is { } below) below.above = (RectTransform)content;
            var button = copy.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            var image = copy.GetComponent<Image>();
            var label = copy.GetComponentInChildren<TMP_Text>(true);
            void Show()
            {
                if (label) label.text = "CAB";
                if (image && lockButton.unlockedSprite && lockButton.lockedSprite) image.sprite = On ? lockButton.lockedSprite : lockButton.unlockedSprite;
            }
            Show();
            button.onClick.AddListener(() => { On = !On; Haptics.Play(Haptics.Kind.Light); });
            // Lit as the mode is, whichever key (or Settings) changed it.
            var frameImage = frameKey.GetComponent<Image>();
            void ShowBoth()
            {
                Show();
                if (frameImage && lockButton.unlockedSprite && lockButton.lockedSprite) frameImage.sprite = On ? lockButton.unlockedSprite : lockButton.lockedSprite;
            }
            ShowBoth();
            _showKeys = ShowBoth;
            Changed += _showKeys;
        }

        // FRAME: back to the picture frame from the CRT cabinet (CAB toggles).
        static RectTransform FrameButton(CabinetLockButton lockButton)
        {
            var copy = Instantiate(lockButton.gameObject, lockButton.transform.parent);
            copy.name = "Frame Button";
            Destroy(copy.GetComponent<CabinetLockButton>());
            var label = copy.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = "FRAME";
            var button = copy.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { On = false; Haptics.Play(Haptics.Kind.Light); });
            return (RectTransform)copy.transform;
        }

        // FULL SCREEN: the game on the phone's screen with AR off
        // (UI.FullScreenTest). The panel's in AR, so it only switches it on;
        // Settings switches it back.
        static RectTransform FullScreenButton(CabinetLockButton lockButton)
        {
            var copy = Instantiate(lockButton.gameObject, lockButton.transform.parent);
            copy.name = "Full Screen Button";
            Destroy(copy.GetComponent<CabinetLockButton>());
            var image = copy.GetComponent<Image>();
            if (image && lockButton.unlockedSprite) image.sprite = lockButton.unlockedSprite;
            var label = copy.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = "FULL";
            var button = copy.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { UI.FullScreenTest.Enabled = true; Haptics.Play(Haptics.Kind.Light); });
            return (RectTransform)copy.transform;
        }

        // A key on a row of its own at the bottom of the panel (like RESTART's).
        static void InLine(RectTransform key, Transform content, string name)
        {
            var below = key.GetComponent<UI.StackBelow>();   // (it sat under the panel)
            if (below) Destroy(below);
            var line = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            line.transform.SetParent(content, false);
            line.transform.SetAsLastSibling();
            var size = line.GetComponent<LayoutElement>();
            size.minHeight = size.preferredHeight = key.sizeDelta.y;
            key.SetParent(line.transform, false);
            key.anchorMin = key.anchorMax = key.pivot = new Vector2(0.5f, 0.5f);
            key.anchoredPosition = Vector2.zero;
        }
    }
}
