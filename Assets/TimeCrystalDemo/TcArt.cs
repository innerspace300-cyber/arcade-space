using System.Collections.Generic;
using UnityEngine;

namespace TimeCrystal
{
    // ARcade: every piece of THE TIME CRYSTAL's new art in one place
    // (Resources/TcArt, made by TcArtBuilder): the projectile packs' strips,
    // the pilots' jets (Animated Pixel Ships and SHMUP 2: TcArtBuilder), the
    // enemy ships (Pixel Spaceships for SHMUP 2), the boss, the pickups'
    // icons and the explosions. Kept as plain textures and cut into frames
    // here, at one art pixel to one of the picture's (800 px over the
    // field's 12 units), whatever their import settings say.
    [CreateAssetMenu(menuName = "ARcade/Time Crystal Art")]
    public class TcArt : ScriptableObject
    {
        /// One art pixel to one of the picture's.
        public const float PixelsPerUnit = 800f / 12f;

        [System.Serializable]
        public class Strip
        {
            public string key;
            public Texture2D texture;
            [Tooltip("Frame size in pixels: a horizontal strip of them (0 = the whole texture is one frame).")]
            public int frameWidth, frameHeight;
        }

        [System.Serializable]
        public class Sequence
        {
            public string key;
            public Texture2D[] frames;
        }

        public Strip[] strips;
        public Sequence[] sequences;
        /// ENDLESS KNIGHT's digital-clock font, for SCORE and HI SCORE (TcStageDirector.LayoutHud).
        public TMPro.TMP_FontAsset digitalFont;
        /// Stage 2's and 3's space (TcSpaceScroller.Warp): six layers each,
        /// back to front, made like stage 1's (ScifiSpaceAssetsNAv1/Zones).
        public Sprite[] zone2, zone3;
        /// Each pilot's laser (C, TcLaser): Hovl Studio's 3D Lasers Pack beams, in TcPilots order.
        public GameObject[] lasers;
        /// Each pilot's engine flames (TcJets): Hovl Studio's Jet engine effects, in TcPilots order.
        public GameObject[] jets;

        static TcArt s_art;
        public static TcArt Get => s_art ? s_art : (s_art = Resources.Load<TcArt>("TcArt"));

        readonly Dictionary<string, Sprite[]> _cache = new Dictionary<string, Sprite[]>();

        /// A strip or sequence's frames ("pj1_scifi_missile_small_yellow", "ship_idle"...), pivot in the middle.
        public static Sprite[] Frames(string key)
        {
            var art = Get;
            if (!art || string.IsNullOrEmpty(key)) return null;
            if (art._cache.TryGetValue(key, out var frames) && (frames == null || frames.Length == 0 || frames[0])) return frames;   // (made again if Unity's unloaded them)
            frames = art.Make(key);
            art._cache[key] = frames;
            if (frames == null) Debug.LogWarning("TcArt: no art called " + key);
            return frames;
        }

        public static Sprite Frame(string key, int index = 0)
        {
            var f = Frames(key);
            return f != null && f.Length > 0 ? f[Mathf.Clamp(index, 0, f.Length - 1)] : null;
        }

        Sprite[] Make(string key)
        {
            if (strips != null)
                foreach (var s in strips)
                {
                    if (s.key != key || !s.texture) continue;
                    int w = s.frameWidth > 0 ? s.frameWidth : s.texture.width;
                    int h = s.frameHeight > 0 ? s.frameHeight : s.texture.height;
                    int n = Mathf.Max(1, s.texture.width / w);
                    var list = new Sprite[n];
                    for (int i = 0; i < n; i++)
                    {
                        list[i] = Sprite.Create(s.texture, new Rect(i * w, s.texture.height - h, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
                        list[i].name = key + "_" + i;
                    }
                    return list;
                }
            if (sequences != null)
                foreach (var q in sequences)
                {
                    if (q.key != key || q.frames == null) continue;
                    var list = new Sprite[q.frames.Length];
                    for (int i = 0; i < list.Length; i++)
                    {
                        var t = q.frames[i];
                        list[i] = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
                        list[i].name = key + "_" + i;
                    }
                    return list;
                }
            return null;
        }

        // ---- Shared drawing bits ----

        static Material s_material;
        /// The plain sprite material (a new renderer's default is the 2D
        /// renderer's lit one; this is the one the boss bars use on the phone).
        public static Material SpriteMaterial
        {
            get
            {
                if (!s_material) s_material = new Material(Shader.Find("Sprites/Default")) { name = "TC Sprite" };
                return s_material;
            }
        }

        /// A sprite renderer on the ships' layer, the plain material.
        public static SpriteRenderer Renderer(GameObject go, int order)
        {
            go.layer = ActorsLayer;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteMaterial;
            sr.sortingOrder = order;
            return sr;
        }

        static int s_actors = -1;
        public static int ActorsLayer => s_actors >= 0 ? s_actors : (s_actors = LayerMask.NameToLayer("TcActors"));

        static Sprite s_pixel;

        /// One white pixel, a unit across.
        public static Sprite Pixel
        {
            get
            {
                if (s_pixel) return s_pixel;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "TC Pixel" };
                tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
                tex.Apply(false, true);
                s_pixel = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f, 0, SpriteMeshType.FullRect);
                return s_pixel;
            }
        }
    }

    /// Plays a run of frames on its sprite renderer: looping, or once (and
    /// then optionally gone).
    public class TcAnim : MonoBehaviour
    {
        public Sprite[] Frames;
        public float Fps = 12f;
        public bool Loop = true;
        public bool DestroyAtEnd;
        SpriteRenderer _r;
        float _t;

        public bool Done => !Loop && Frames != null && _t * Fps >= Frames.Length;

        public void Play(Sprite[] frames, float fps, bool loop = true, bool restart = true)
        {
            if (frames == Frames && !restart) return;
            Frames = frames;
            Fps = fps;
            Loop = loop;
            if (restart || frames != Frames) _t = 0f;
            Show();
        }

        /// Starts at a random point (so a crowd of the same thing doesn't flicker in step).
        public void Scatter()
        {
            if (Frames != null && Fps > 0f) _t = Random.value * Frames.Length / Fps;
        }

        void Awake() => _r = GetComponent<SpriteRenderer>();

        void Update()
        {
            _t += Time.deltaTime;
            if (DestroyAtEnd && Done) { Destroy(gameObject); return; }
            Show();
        }

        void Show()
        {
            if (!_r) _r = GetComponent<SpriteRenderer>();
            if (!_r || Frames == null || Frames.Length == 0) return;
            int i = (int)(_t * Fps);
            i = Loop ? i % Frames.Length : Mathf.Min(i, Frames.Length - 1);
            _r.sprite = Frames[i];
        }
    }
}
