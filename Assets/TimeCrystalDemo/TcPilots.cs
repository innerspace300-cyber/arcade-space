using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TimeCrystal
{
    // ARcade: THE TIME CRYSTAL's four pilots - each their own face
    // (Animated Pixel Ships' portraits), jet (its powered-up planes) and the
    // gun they start with and go back to after a power-up runs out.
    public static class TcPilots
    {
        public class Pilot
        {
            public string Name, Portrait, Ship;
            public TcWeapon Weapon;
            public Color Color;
        }

        public static readonly Pilot[] All =
        {
            new Pilot { Name = "NOVA", Portrait = "portrait_nova", Ship = "ship", Weapon = TcWeapon.Missile, Color = TcFx.Cyan },
            new Pilot { Name = "BLAZE", Portrait = "portrait_blaze", Ship = "ship01", Weapon = TcWeapon.Laser, Color = TcFx.Red },
            new Pilot { Name = "VIPER", Portrait = "portrait_viper", Ship = "ship02", Weapon = TcWeapon.Seeker, Color = TcFx.Green },
            new Pilot { Name = "RIO", Portrait = "portrait_rio", Ship = "ship03", Weapon = TcWeapon.Rockets, Color = TcFx.Orange },
        };

        const string Key = "TC_PILOT";
        static int s_index = -1;

        /// The one flying (the last one chosen, kept between games).
        public static int Index
        {
            get { if (s_index < 0) s_index = Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, All.Length - 1); return s_index; }
            set { s_index = (value % All.Length + All.Length) % All.Length; PlayerPrefs.SetInt(Key, s_index); }
        }

        public static Pilot Current => All[Index];

        // ---- The HUD's portrait ----
        static Sprite s_hudSprite;
        static Vector3 s_hudScale;
        static Transform s_hud;

        /// The HUD shows the pilot flying: their portrait, and their mini jet for each life.
        // ---- The player's own HUD (portrait, health bar, lives, bombs): away till a pilot's chosen ----
        static readonly List<GameObject> s_playerHud = new List<GameObject>();

        public static bool IsPlayerHud(string name) =>
            name == "PLAYER PORTRAIT" || name == "HEALTH BAR" || name.StartsWith("PLAYER LIFE") || name.StartsWith("BOMB ");

        /// These off (kept, to come back with ShowPlayerHud).
        public static void HidePlayerHud(GameObject piece)
        {
            if (!piece || s_playerHud.Contains(piece)) return;
            if (piece.activeSelf) { piece.SetActive(false); s_playerHud.Add(piece); }
        }

        public static void HidePlayerHud()
        {
            var canvas = TcWorld.Root ? TcWorld.Root.Find("Canvas") : null;
            if (canvas) foreach (Transform t in canvas) if (IsPlayerHud(t.name)) HidePlayerHud(t.gameObject);
        }

        public static void ShowPlayerHud()
        {
            foreach (var g in s_playerHud) if (g) g.SetActive(true);
            s_playerHud.Clear();
        }

        public static void ShowOnHud()
        {
            var canvas = TcWorld.Root ? TcWorld.Root.Find("Canvas") : null;
            var hud = canvas ? canvas.Find("PLAYER PORTRAIT") : null;
            var r = hud ? hud.GetComponent<SpriteRenderer>() : null;
            var face = TcArt.Frame(Current.Portrait);
            if (!r || !face) return;
            if (s_hud != hud) { s_hud = hud; s_hudSprite = r.sprite; s_hudScale = hud.localScale; }
            // The same size on screen as the one it had.
            float k = s_hudSprite ? s_hudSprite.bounds.size.y / face.bounds.size.y : 1f;
            r.sprite = face;
            hud.localScale = s_hudScale * k;
            var icon = TcArt.Frame(Current.Ship + "_icon");
            if (!icon) return;
            foreach (Transform child in canvas)
                if (child.name.StartsWith("PLAYER LIFE") && child.GetComponent<UnityEngine.UI.Image>() is { } life)
                {
                    life.sprite = icon;
                    life.preserveAspect = true;
                }
        }
    }

    // ARcade: CHOOSE YOUR FIGHTER, before the countdown: the four pilots'
    // faces in a row, the chosen one framed in their colour with their name,
    // their gun and their jet big under them. Left/right to choose, any
    // button to go (no time limit).
    // The ship can't move or fire meanwhile (TimeCrystalGame).
    // A tap on the picture this frame (a finger coming down, or the mouse in the Editor): where on it, 0..1 across
    // and up. Full screen: inside the picture's rect; otherwise the ray onto the frame's layers in the room.
    public static class TcTap
    {
        public static bool Picture(out Vector2 uv)
        {
            uv = default;
            Vector2 screen = default;
            bool down = false;
            foreach (var t in UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches)
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began) { screen = t.screenPosition; down = true; break; }
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (!down && mouse != null && mouse.leftButton.wasPressedThisFrame) { screen = mouse.position.ReadValue(); down = true; }
            if (!down) return false;
            if (SpatialEmulator.UI.FullScreenTest.Enabled)
            {
                var r = SpatialEmulator.UI.FullScreenTest.PictureRect;
                if (r.height <= 0f || !r.Contains(screen)) return false;
                uv = Rect.PointToNormalized(r, screen);
                return true;
            }
            var stack = SpatialEmulator.Mobile.MobileRetroDepthLayerStack.Active;
            var quad = stack ? stack.LayerQuad(0) : null;
            var cam = Camera.main;
            if (!quad || !cam) return false;
            var ray = cam.ScreenPointToRay(screen);
            var q = quad.transform;
            if (!new Plane(q.forward, q.position).Raycast(ray, out float d)) return false;
            var local = q.InverseTransformPoint(ray.GetPoint(d));
            uv = new Vector2(local.x + 0.5f, local.y + 0.5f);
            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }
    }

    public static class TcFighterSelect
    {
        public static bool Active { get; private set; }
        /// A button pressed this frame (TimeCrystalGame).
        public static bool Confirm;
        /// Left/right pushed since it last looked (TimeCrystalGame; the ship never sees them).
        public static bool Left, Right;

        const float FaceScale = 0.8f, Spacing = 2.1f;

        public static IEnumerator Run(TcStageDirector director)
        {
            if (!TcWorld.Root || TcArt.Frame(TcPilots.All[0].Portrait) == null) yield break;
            Active = true;
            Confirm = Left = Right = false;
            var f = TcWorld.Field;
            Vector3 At(float x, float y) => new Vector3(f.xMin + x * f.width, f.yMin + y * f.height, TcWorld.Origin.z);

            var root = new GameObject("CHOOSE YOUR FIGHTER").transform;
            root.SetParent(TcWorld.Root, false);

            // Space dimmed behind it.
            var dim = Sprite(root, "DIM", TcArt.Pixel, 590, At(0.5f, 0.5f));
            dim.transform.localScale = new Vector3(f.width, f.height, 1f);
            dim.color = new Color(0f, 0f, 0.05f, 0.6f);
            dim.gameObject.layer = LayerMask.NameToLayer("TcBigRocks");   // (under the HUD: the ships' layer draws over it now, and this mustn't dim the title and names)

            TcPilots.HidePlayerHud();   // (the player's HUD back once one's chosen)
            var faces = new List<SpriteRenderer>();
            float rowY = 0.59f;   // (all of it lower than it was: the game's title over it)
            for (int i = 0; i < TcPilots.All.Length; i++)
            {
                var pos = At(0.5f, rowY) + Vector3.right * (i - (TcPilots.All.Length - 1) * 0.5f) * Spacing;
                var face = Sprite(root, TcPilots.All[i].Name, TcArt.Frame(TcPilots.All[i].Portrait), 600, pos);
                faces.Add(face);
            }
            // The frame round the chosen one: four bars.
            var bars = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++) bars[i] = Sprite(root, "FRAME", TcArt.Pixel, 602, Vector3.zero);

            var jet = Sprite(root, "JET", null, 601, At(0.5f, 0.23f));
            jet.transform.localScale = Vector3.one * 1.6f;
            var jetAnim = jet.gameObject.AddComponent<TcAnim>();
            TcHitMarker.OnPreview(jet.gameObject, 601);   // (its hit diamond, as in the game)
            // Its engine flames and sparks too (TcJets).
            var flames = new List<GameObject>();
            float sparkClock = 0f;

            var title = director.MakeLine("CHOOSE TITLE", 0.75f, 2.2f);
            var name = director.MakeLine("CHOOSE NAME", 0.435f, 3f);
            var gun = director.MakeLine("CHOOSE GUN", 0.385f, 1.5f);
            var prompt = director.MakeLine("CHOOSE PROMPT", 0.338f, 1.3f);
            var game = director.MakeLine("GAME TITLE", 0.85f, 1f);
            var texts = new[] { title, name, gun, prompt, game };
            if (game) TitleSlide.Make(game);
            if (title) { title.text = "CHOOSE YOUR FIGHTER"; title.color = TcFx.Yellow; }

            // The ship itself hidden meanwhile: the big one shows the jet.
            var hidden = new List<Renderer>();
            if (TcPlayerShip.Instance)
                foreach (var r in TcPlayerShip.Instance.GetComponentsInChildren<SpriteRenderer>())
                    if (r.enabled) { r.enabled = false; hidden.Add(r); }

            int shown = -1;
            while (!Confirm)
            {
                if (Left) { TcPilots.Index--; director.Beep(1f); }
                if (Right) { TcPilots.Index++; director.Beep(1f); }
                // Or a face tapped: that pilot - tapped again, chosen (as FIRE).
                if (TcTap.Picture(out var tap))
                {
                    var at = new Vector3(f.xMin + tap.x * f.width, f.yMin + tap.y * f.height, 0f);
                    for (int i = 0; i < faces.Count; i++)
                    {
                        var b = faces[i].bounds;
                        if (!new Rect(b.min.x - 0.15f, b.min.y - 0.15f, b.size.x + 0.3f, b.size.y + 0.3f).Contains((Vector2)at)) continue;
                        if (i == TcPilots.Index) Confirm = true;
                        else { TcPilots.Index = i; director.Beep(1f); }
                        break;
                    }
                }
                Left = Right = false;
                var pilot = TcPilots.Current;
                if (shown != TcPilots.Index)
                {
                    shown = TcPilots.Index;
                    if (TcPlayerShip.Instance) TcPlayerShip.Instance.ApplyPilot();
                    TcPilots.ShowOnHud();
                    jetAnim.Play(TcArt.Frames(pilot.Ship + "_idle"), 16f);
                    TcJets.Clear(flames);
                    TcJets.Attach(jet.transform, shown, flames);
                    if (name) { name.text = pilot.Name; name.color = pilot.Color; }
                    if (gun) { gun.text = TcWeapons.Specs[pilot.Weapon].Name; gun.color = Color.white; }
                    TcFx.Burst(faces[shown].transform.position, pilot.Color, 18, 3f, 0.07f, 0.4f);
                }
                // The chosen one bigger and bright, the rest small and dark.
                for (int i = 0; i < faces.Count; i++)
                {
                    bool on = i == shown;
                    faces[i].color = on ? Color.white : new Color(0.35f, 0.35f, 0.45f, 1f);
                    float s = FaceScale * (on ? 1.12f + 0.03f * Mathf.Sin(Time.time * 6f) : 1f);
                    faces[i].transform.localScale = Vector3.one * s;
                }
                Frame(bars, faces[shown], pilot.Color, Mathf.Repeat(Time.time * 3f, 1f) < 0.7f);
                TcJets.Sparks(flames, shown, 30f, 1.6f, ref sparkClock);
                if (prompt) prompt.text = "< >  CHOOSE     FIRE  GO";
                yield return null;
            }

            // Gone: a flash on the jet, the pilot's name called.
            director.Beep(1.5f);
            TcFx.Burst(jet.transform.position, TcPilots.Current.Color, 40, 5f, 0.09f, 0.6f);
            TcPilots.ShowOnHud();
            if (TcPlayerShip.Instance) TcPlayerShip.Instance.ApplyPilot();
            foreach (var r in hidden) if (r) r.enabled = true;
            TcStory.ShowShip();   // (and what the story hid)
            TcPilots.ShowPlayerHud();
            if (TcPlayerShip.Instance) TcFx.Burst(TcPlayerShip.Instance.transform.position, TcPilots.Current.Color, 30, 4f, 0.08f, 0.5f);
            foreach (var t in texts) if (t) Object.Destroy(t.gameObject);
            Object.Destroy(root.gameObject);
            Active = false;
            Confirm = false;
            director.Announce(TcPilots.Current.Name, TcPilots.Current.Color, 1f, 1.5f, lower: 420f);   // (under the 3, 2, 1)
        }

        // THE TIME CRYSTAL over it all, in neon: sliding in from the left, a flicker as it lights
        // (a neon sign's), then glowing steady with a slow pulse.
        class TitleSlide : MonoBehaviour
        {
            TMPro.TMP_Text _text;
            RectTransform _rt;
            Vector2 _home;
            float _t;
            const float In = 0.75f;

            public static void Make(TMPro.TMP_Text text)
            {
                text.text = "THE TIME CRYSTAL";
                float k = Mathf.Max(0.001f, text.transform.localScale.x);
                ((RectTransform)text.transform).sizeDelta = new Vector2(1920f * 0.9f / k, 2560f * 0.07f / k);
                text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                text.enableAutoSizing = true;
                text.fontSizeMin = 10f;
                text.fontSizeMax = 2560f * 0.3f / k;
                text.alignment = TMPro.TextAlignmentOptions.Center;
                // Neon: hot cyan at the top into magenta, a deep violet edge.
                text.color = Color.white;   // (filled white; its outline the neon - Update cycles it)
                text.enableVertexGradient = false;
                // Its own material: the HUD's face is over-bright (for its bloom: white whatever its
                // colour) - here near plain, so the colours show, a hot pink tube round it and its glow.
                var m = text.fontMaterial;
                m.SetColor("_FaceColor", new Color(1.1f, 1.1f, 1.1f, 1f));
                m.SetFloat("_OutlineWidth", 0.24f);   // (a couple of the picture's pixels)
                m.SetColor("_OutlineColor", new Color(1.3f, 0.25f, 1.5f, 1f));
                m.EnableKeyword("GLOW_ON");
                m.SetColor("_GlowColor", new Color(0.75f, 0.2f, 1f, 0.8f));
                m.SetFloat("_GlowOuter", 0.45f);
                m.SetFloat("_GlowInner", 0.05f);
                m.SetFloat("_GlowPower", 0.6f);
                text.UpdateMeshPadding();
                // A little taller: stretched up, as wide as before.
                var sc = text.transform.localScale;
                text.transform.localScale = new Vector3(sc.x, sc.y * 1.25f, sc.z);
                var s = text.gameObject.AddComponent<TitleSlide>();
                s._text = text;
                s._rt = (RectTransform)text.transform;
                s._home = s._rt.anchoredPosition;
                if (Application.isPlaying) s._rt.anchoredPosition = s._home + Vector2.left * 1920f * 1.3f;
            }

            /// Straight to where it ends (the editor's previews).
            public void Finish() { _t = In + 1f; if (_rt) _rt.anchoredPosition = _home; Update(); }

            void Update()
            {
                _t += Time.deltaTime;
                float k = Mathf.Clamp01(_t / In);
                float e = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);   // (eases out, a little past and back)
                _rt.anchoredPosition = Vector2.LerpUnclamped(_home + Vector2.left * 1920f * 1.3f, _home, e);
                float a = 1f;
                float lit = _t - In;
                if (lit > 0f && lit < 0.45f) a = Mathf.Repeat(lit * 13f, 1f) < 0.55f ? 1f : 0.3f;   // (the flicker)
                else if (lit >= 0.45f) a = 0.88f + 0.12f * Mathf.Sin(lit * 3f);
                _text.alpha = a;
                // The neon round it going through the colours: outline and glow, over-bright for the bloom.
                var neon = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.22f, 1f), 0.85f, 1f);
                var m = _text.fontMaterial;
                m.SetColor("_OutlineColor", neon * 1.8f);
                m.SetColor("_GlowColor", new Color(neon.r, neon.g, neon.b, 0.8f));
            }
        }

        // ---- On CONTINUE?: a mini Choose Your Fighter ----
        // Over everything (its own canvas on the ships' layer, which draws over the HUD and the enemies): the
        // scene darkened, INSERT COIN / TO CONTINUE (the arcade's own words) at the top, CHOOSE ANOTHER PLAYER?
        // over the four faces, the one flying framed in its colour and its name under them and, full screen, the
        // COIN button itself under that (TcButtonHints: CoinY). Once the coin's in: 3, 2, 1, GO! on it, the dark
        // lifting, then the game goes on (ResumeCountdown). Left/right swaps pilot there and
        // then (the ship, its gem, jets and the HUD's portrait), so the coin carries on with whoever's picked.
        // No countdown: it waits. Drawn on real time (the game's clock is stopped).
        const float MiniFace = 250f, MiniGap = 90f;   // (canvas units: the HUD's 1920 across, 2560 down)
        const float InsertY = 0.74f, ToContinueY = 0.675f, HintY = 0.59f, FacesY = 0.495f, NameY = 0.39f;
        /// Where the COIN button goes while CONTINUE? is up (0 bottom, 1 top of the picture); -1 when it isn't.
        public static float CoinY => s_mini && !s_counting ? 0.262f : -1f;
        static bool s_counting;
        static UnityEngine.UI.Image s_miniDim;
        static readonly List<GameObject> s_miniParts = new List<GameObject>();
        static Canvas s_mini;
        static readonly List<UnityEngine.UI.Image> s_miniFaces = new List<UnityEngine.UI.Image>();
        static UnityEngine.UI.Image[] s_miniBars;
        static TMP_Text s_miniName, s_miniPress;

        /// Shown (made the first time; call every frame while CONTINUE? is up).
        public static void ShowMini(TcStageDirector director)
        {
            if (!TcWorld.Root || TcArt.Frame(TcPilots.All[0].Portrait) == null) return;
            if (!s_mini && !MakeMini(director)) return;
            int shown = TcPilots.Index;
            var pilot = TcPilots.Current;
            for (int i = 0; i < s_miniFaces.Count; i++)
            {
                bool on = i == shown;
                s_miniFaces[i].color = Color.white;   // (all of them lit: the frame shows the pick)
                s_miniFaces[i].rectTransform.localScale = Vector3.one * (on ? 1.12f + 0.03f * Mathf.Sin(Time.unscaledTime * 6f) : 1f);
            }
            // The frame round the chosen one.
            var face = s_miniFaces[shown].rectTransform;
            float size = MiniFace * face.localScale.x + 36f, t = 12f;
            bool blink = Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.7f;
            var c = face.anchoredPosition;
            Bar(s_miniBars[0], c + new Vector2(0f, size * 0.5f), new Vector2(size + t, t));
            Bar(s_miniBars[1], c - new Vector2(0f, size * 0.5f), new Vector2(size + t, t));
            Bar(s_miniBars[2], c - new Vector2(size * 0.5f, 0f), new Vector2(t, size + t));
            Bar(s_miniBars[3], c + new Vector2(size * 0.5f, 0f), new Vector2(t, size + t));
            foreach (var bar in s_miniBars) { bar.color = pilot.Color; bar.enabled = blink; }
            if (s_miniName) { s_miniName.text = "<  " + pilot.Name + "  >"; s_miniName.color = pilot.Color; }
            if (s_miniPress) s_miniPress.enabled = Mathf.Repeat(Time.unscaledTime * 2.5f, 1f) < 0.6f;
        }

        static void Bar(UnityEngine.UI.Image bar, Vector2 at, Vector2 size)
        {
            bar.rectTransform.anchoredPosition = at;
            bar.rectTransform.sizeDelta = size;
        }

        static bool MakeMini(TcStageDirector director)
        {
            var hud = TcWorld.Root.Find("Canvas") ? TcWorld.Root.Find("Canvas").GetComponent<Canvas>() : null;
            int layer = LayerMask.NameToLayer("TcPlayer");
            Camera cam = null;
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.name == "Camera TcPlayer") { cam = c; break; }   // (TimeCrystalGame's: the ships' layer, last over everything)
            if (!hud || !cam || !director) return false;

            var go = new GameObject("CONTINUE", typeof(RectTransform)) { layer = layer };
            go.transform.SetParent(TcWorld.Root, false);
            s_mini = go.AddComponent<Canvas>();
            s_mini.renderMode = RenderMode.ScreenSpaceCamera;
            s_mini.worldCamera = cam;
            s_mini.planeDistance = hud.planeDistance;
            var sorting = SortingLayer.layers;
            s_mini.sortingLayerID = sorting[sorting.Length - 1].id;   // (the last sorting layer: over every sprite on the ships' layer)
            s_mini.sortingOrder = 1000;
            if (hud.GetComponent<UnityEngine.UI.CanvasScaler>() is { } from)
            {
                var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = from.uiScaleMode;
                scaler.referenceResolution = from.referenceResolution;
                scaler.screenMatchMode = from.screenMatchMode;
                scaler.matchWidthOrHeight = from.matchWidthOrHeight;
                scaler.scaleFactor = from.scaleFactor;
            }
            var root = (RectTransform)go.transform;

            // The scene darkened behind it.
            var dim = s_miniDim = Image(root, "DIM", TcArt.Pixel, new Color(0f, 0f, 0.04f, 0.8f));
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.sizeDelta = Vector2.zero;

            Line(director, root, "CONTINUE HINT", HintY, 1.2f, "CHOOSE ANOTHER PLAYER?", Color.white);
            s_miniFaces.Clear();
            for (int i = 0; i < TcPilots.All.Length; i++)
            {
                var face = Image(root, TcPilots.All[i].Name, TcArt.Frame(TcPilots.All[i].Portrait), Color.white);
                face.preserveAspect = true;
                face.rectTransform.sizeDelta = Vector2.one * MiniFace;
                face.rectTransform.anchoredPosition = new Vector2((i - (TcPilots.All.Length - 1) * 0.5f) * (MiniFace + MiniGap), (FacesY - 0.5f) * 2560f);
                s_miniFaces.Add(face);
            }
            s_miniBars = new UnityEngine.UI.Image[4];
            for (int i = 0; i < 4; i++) s_miniBars[i] = Image(root, "FRAME", TcArt.Pixel, Color.white);
            s_miniName = Line(director, root, "CONTINUE PILOT", NameY, 1.6f, "", Color.white);
            s_miniPress = Line(director, root, "INSERT COIN", InsertY, 2.6f, "INSERT COIN", new Color(1f, 0.82f, 0.18f));
            Line(director, root, "TO CONTINUE", ToContinueY, 1.4f, "TO CONTINUE", Color.white);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            s_miniParts.Clear();
            foreach (Transform t in root) if (t != dim.transform) s_miniParts.Add(t.gameObject);
            s_counting = false;
            return true;
        }

        static UnityEngine.UI.Image Image(RectTransform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // One of the HUD's lines (its neon font), moved onto this canvas.
        static TMP_Text Line(TcStageDirector director, RectTransform parent, string name, float y, float size, string text, Color color)
        {
            var line = director.MakeLine(name, y, size);
            if (!line) return null;
            var at = ((RectTransform)line.transform).anchoredPosition;
            line.transform.SetParent(parent, false);
            ((RectTransform)line.transform).anchoredPosition = at;
            line.text = text;
            line.color = color;
            return line;
        }

        /// A face on CONTINUE? tapped: that pilot, flying now (call every frame while it's up).
        public static void TapMini(TcStageDirector director)
        {
            if (!s_mini || s_counting || !TcTap.Picture(out var tap)) return;
            var at = new Vector2((tap.x - 0.5f) * 1920f, (tap.y - 0.5f) * 2560f);   // (this canvas's units)
            for (int i = 0; i < s_miniFaces.Count; i++)
            {
                var c = s_miniFaces[i].rectTransform.anchoredPosition;
                if (Mathf.Abs(at.x - c.x) > (MiniFace + MiniGap) * 0.5f || Mathf.Abs(at.y - c.y) > MiniFace * 0.6f) continue;
                if (i != TcPilots.Index) StepMini(director, i - TcPilots.Index);
                break;
            }
        }

        /// Left (-1) or right (+1): the next pilot, flying now.
        public static void StepMini(TcStageDirector director, int dir)
        {
            TcPilots.Index += dir;
            if (director) director.Beep(1f);
            if (TcPlayerShip.Instance) TcPlayerShip.Instance.ApplyPilot();
            TcPilots.ShowOnHud();
        }

        /// After the coin: the picker gone, 3, 2, 1, GO! big over the frozen game (real time), the dark lifting
        /// as it counts; then it's all gone and the caller lets the game go.
        public static IEnumerator ResumeCountdown(TcStageDirector director)
        {
            if (!s_mini) yield break;
            s_counting = true;
            foreach (var part in s_miniParts) if (part) part.SetActive(false);
            var number = Line(director, (RectTransform)s_mini.transform, "CONTINUE COUNT", 0.5f, 6f, "", Color.white);
            if (number) number.gameObject.layer = s_mini.gameObject.layer;
            var size = number ? number.transform.localScale : Vector3.one;
            string[] words = { "3", "2", "1", "GO!" };
            for (int i = 0; i < words.Length; i++)
            {
                if (number) number.text = words[i];
                if (director) director.Beep(i == words.Length - 1 ? 1.5f : 1f);
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
                {
                    if (number) number.transform.localScale = size * (1f + 0.6f * Mathf.Clamp01(1f - t / 0.25f));
                    if (s_miniDim) s_miniDim.color = new Color(0f, 0f, 0.04f, 0.8f * (1f - (i + t) / words.Length));
                    if (i == words.Length - 1 && number) number.color = new Color(1f, 1f, 1f, t < 0.6f ? 1f : (1f - t) / 0.4f);
                    yield return null;
                }
            }
            HideMini();
        }

        public static void HideMini()
        {
            s_counting = false;
            s_miniParts.Clear();
            if (s_mini) Object.Destroy(s_mini.gameObject);
            s_mini = null; s_miniName = null; s_miniPress = null;
            s_miniFaces.Clear();
        }

        static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, int order, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var r = TcArt.Renderer(go, order);
            r.sprite = sprite;
            return r;
        }

        static void Frame(SpriteRenderer[] bars, SpriteRenderer face, Color color, bool on)
        {
            var b = face.bounds;
            const float t = 0.07f, pad = 0.08f;
            float w = b.size.x + pad * 2f, h = b.size.y + pad * 2f;
            Vector3 c = b.center;
            Set(bars[0], c + Vector3.up * (h * 0.5f), new Vector3(w + t, t, 1f));
            Set(bars[1], c + Vector3.down * (h * 0.5f), new Vector3(w + t, t, 1f));
            Set(bars[2], c + Vector3.left * (w * 0.5f), new Vector3(t, h + t, 1f));
            Set(bars[3], c + Vector3.right * (w * 0.5f), new Vector3(t, h + t, 1f));
            foreach (var bar in bars) { bar.color = color; bar.enabled = on; }
        }

        static void Set(SpriteRenderer r, Vector3 at, Vector3 scale)
        {
            r.transform.position = at;
            r.transform.localScale = scale;
        }
    }
}
