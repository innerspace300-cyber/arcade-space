using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TimeCrystal
{
    // ARcade: THE TIME CRYSTAL's story, before Choose Your Fighter: six slides, each a
    // line typed out in the HUD's pixel font over its picture - the crystal (Creativekind's
    // Magic Crystal, neon blue), BOOMER's ships, the three bosses, the four pilots. A
    // button finishes the line, or goes on to the next; it goes on by itself after a while.
    // Played the first time, then every few games (Every); its end flashes into the warp.
    public static class TcStory
    {
        public static bool Active { get; private set; }
        /// A button pressed this frame (TimeCrystalGame feeds it).
        public static bool Next;

        const string SeenKey = "TC_STORY_SEEN", RunsKey = "TC_STORY_RUNS";
        const int Every = 5;                 // (after the first time: every fifth game)
        const float CharsPerSecond = 38f, HoldAfter = 3.2f;
        const float ZoomAfter = 1f;
        // Slide 4: the pilots' faces dropping in one after another, then their ships flying up under
        // their names - its line after (PilotsIn).
        const float FaceGap = 0.3f, ShipsFrom = 1.5f, ShipGap = 0.22f, PilotsIn = ShipsFrom + 3 * ShipGap + 1.2f;   // (slide 1: the zoom in this long after its line's all out)

        struct Slide { public string text; public int look; public float top, smaller, cps, hold, wait; public bool keep, fit; public int same, rule; }   // (cps: its own typing speed; hold: its own time after; wait: before its line starts; keep: on with the last slide's pictures;
                 // fit: each of its lines (\n) sized to fill the width - its first `same` all one size, the widest's;
                 // rule: a neon divider on line `rule`, a short blank one (0: none))   // (top: where the line starts, up the picture, 0 = centred under it; smaller: its font, px under the rest)

        static readonly Slide[] Slides =
        {
            new Slide { look = 0, top = 0.328f, wait = FadeIn.Seconds, text = "DATA MINERS FOUND A CRYSTAL OLDER THAN THE STARS INSIDE A METEOR." },
            new Slide { look = 1, keep = true, top = 0.383f, cps = 15f, text = "IT COULD BEND TIME ITSELF - SPEED IT UP, SLOW IT DOWN, EVEN TURN IT BACK." },
            new Slide { look = 2, keep = true, hold = 6.5f, smaller = 4f, top = 0.383f, text = "BOOMER INDUSTRIES STOLE IT, AND NOW THEIR ARMADA GUARDS IT ACROSS THE GALAXY." },
            new Slide { look = 4, smaller = 4f, top = 0.384f, wait = PilotsIn, fit = true, same = 4, rule = 2, text = "BOOMERS WANT TIME\nSTUCK THE WAY IT WAS.\n \n4 REBEL ZOOMERS\nCAN TAKE IT BACK FOR GOOD!" },
        };

        /// This game should show it (the first ever, then every Every-th).
        static readonly List<Renderer> s_hidden = new List<Renderer>();

        // A text drawn over everything on the HUD (its own canvas, sorted last - the portrait's and
        // the scores' sprites and canvases included).
        static void OnTop(TMP_Text text)
        {
            if (!text) return;
            text.transform.SetAsLastSibling();
            if (!text.TryGetComponent(out Canvas canvas)) canvas = text.gameObject.AddComponent<Canvas>();   // (not ??: Unity's missing component isn't a real null)
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32000;
        }

        /// The ship as it was before the story (Choose Your Fighter calls it as it ends).
        public static void ShowShip()
        {
            foreach (var r in s_hidden) if (r) r.enabled = true;
            s_hidden.Clear();
        }

        /// The next game plays it whatever (RESTART from a menu: SaveStatePanel).
        public static bool PlayNext;

        public static bool Due()
        {
            if (PlayNext) { PlayNext = false; return true; }
            if (PlayerPrefs.GetInt(SeenKey, 0) == 0) return true;
            int runs = PlayerPrefs.GetInt(RunsKey, 0) + 1;
            PlayerPrefs.SetInt(RunsKey, runs);
            return runs % Every == 0;
        }

        public static IEnumerator Run(TcStageDirector director)
        {
            if (!TcWorld.Root) yield break;
            Active = true;
            Next = false;
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            var f = TcWorld.Field;
            Vector3 At(float x, float y) => new Vector3(f.xMin + x * f.width, f.yMin + y * f.height, TcWorld.Origin.z);

            var root = new GameObject("STORY").transform;
            root.SetParent(TcWorld.Root, false);
            // In from black (over everything in the picture; the music fades up with it: TcMusic).
            var black = Sprite(root, "FADE IN", TcArt.Pixel, 985, At(0.5f, 0.5f), 1f);
            black.transform.localScale = new Vector3(f.width + 1f, f.height + 1f, 1f);
            black.color = Color.black;
            black.gameObject.AddComponent<FadeIn>();
            var dim = Sprite(root, "DIM", TcArt.Pixel, 590, At(0.5f, 0.5f), 1f);
            dim.transform.localScale = new Vector3(f.width, f.height, 1f);
            dim.color = new Color(0f, 0f, 0.04f, 0.15f);   // (space showing through: its stars and planets)
            dim.gameObject.layer = LayerMask.NameToLayer("TcBigRocks");   // (under the HUD's lines, not over them)
            Backdrop(root, At);

            // The paragraph: centred in the space between the pictures (their bottoms about 0.57 up)
            // and ANY BUTTON at the bottom - as big as fits there (twice the last size at most).
            const float Bottom = 0.075f, Top = 0.51f;
            const float MaxFont = 1920f * 0.061f;   // (35% under its last size, twice; in HUD units)
            var line = director.MakeLine("STORY LINE", (Bottom + Top) * 0.5f, 1.05f);
            if (line)
            {
                line.textWrappingMode = TextWrappingModes.Normal;
                line.alignment = TextAlignmentOptions.Center;
                // In the HUD's canvas units (its 1920 x 2560, as MakeLine places lines) - the line's
                // scaled down (an announcement's), so its own size is that over its scale.
                float k = Mathf.Max(0.001f, line.transform.localScale.x);
                ((RectTransform)line.transform).sizeDelta = new Vector2(1920f * 0.92f / k, 2560f * (Top - Bottom) / k);
                line.enableAutoSizing = true;
                line.fontSizeMax = MaxFont / k;
                line.fontSizeMin = 1920f * 0.022f / k;
                line.lineSpacing = 8f;
                line.color = Color.white;
                OnTop(line);   // (over everything else in the HUD: its score, the pilot's portrait)
            }
            var skip = director.MakeLine("STORY SKIP", 0.04f, 0.8f);
            if (skip) { skip.text = "TAP TO SKIP  >"; skip.color = new Color(0.6f, 0.75f, 1f); OnTop(skip); }

            // The game's HUD away meanwhile (scores, the pilot's portrait, health, lives, bombs) - only
            // the story's own lines - till Choose Your Fighter.
            var hud = new List<GameObject>();
            if (line)
                foreach (Transform t in line.transform.parent)
                    if (t.gameObject.activeSelf && t != line.transform && (!skip || t != skip.transform)) { t.gameObject.SetActive(false); hud.Add(t.gameObject); }

            // The ship hidden meanwhile (Choose Your Fighter shows the pilots' jets after).
            s_hidden.Clear();
            if (TcPlayerShip.Instance)
                foreach (var r in TcPlayerShip.Instance.GetComponentsInChildren<SpriteRenderer>())
                    if (r.enabled) { r.enabled = false; s_hidden.Add(r); }

            var pictures = new List<GameObject>();
            for (int i = 0; i < Slides.Length; i++)
            {
                if (!Slides[i].keep)
                {
                    foreach (var p in pictures) if (p) Object.Destroy(p);
                    pictures.Clear();
                }
                Picture(director, root, Slides[i].look, At, pictures);
                if (line)
                {
                    line.text = Slides[i].text;
                    line.maxVisibleCharacters = 0;
                    // Its top edge just under the slide's mark (Wayne's red lines), else centred.
                    var rt = (RectTransform)line.transform;
                    float top = Slides[i].top > 0f ? Slides[i].top - 0.015f : Top;
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, (top - (Top - Bottom) * 0.5f - 0.5f) * 2560f);
                    line.alignment = Slides[i].top > 0f ? TextAlignmentOptions.Top : TextAlignmentOptions.Center;
                    // (A px of the 800-high picture: 2560 / 800 HUD units.)
                    line.fontSizeMax = (MaxFont - Slides[i].smaller * 3.2f) / Mathf.Max(0.001f, line.transform.localScale.x);
                    line.enableAutoSizing = !Slides[i].fit;
                    if (Slides[i].fit) line.text = FitLines(line, Slides[i].text, Slides[i].same);
                    if (Slides[i].rule > 0) pictures.Add(Divider(line, Slides[i].rule));
                }
                director.Beep(0.8f + 0.08f * i);
                // Typed out; a button finishes it, then goes on (or it goes on by itself).
                float t = -Slides[i].wait;   // (its pictures' entrance first)
                int total = Slides[i].text.Length;
                float cps = Slides[i].cps > 0f ? Slides[i].cps : CharsPerSecond;
                float zoomAt = -1f;
                Next = false;
                while (true)
                {
                    t += Time.deltaTime;
                    int shown = Mathf.Clamp((int)(t * cps), 0, total);
                    if (line) line.maxVisibleCharacters = shown;
                    foreach (var p in pictures) if (p && p.TryGetComponent(out TimeFlow flow)) flow.Shown(Slides[i].text, shown);
                    if (Next)
                    {
                        Next = false;
                        if (shown < total) t = total / cps;   // (finish the line)
                        else break;
                    }
                    // (The first: the slow zoom in to the meteor from the start of the fade in from black,
                    // reaching it ZoomAfter after its line's all out - the slide staying a moment more.)
                    float least = 0f;
                    if (Slides[i].look == 0)
                    {
                        float zoomEnd = total / cps + ZoomAfter;
                        if (zoomAt < 0f)
                        {
                            zoomAt = t;
                            foreach (var p in pictures) if (p && p.TryGetComponent(out Zoom z)) z.Begin(zoomEnd - t);
                        }
                        least = zoomEnd + 1f;
                    }
                    // (BOOMERS dropping in over them once the line's all out.)
                    if (shown >= total) foreach (var p in pictures) if (p && p.TryGetComponent(out TitleDrop drop)) drop.Begin();
                    float hold = Slides[i].hold > 0f ? Slides[i].hold : HoldAfter;
                    if (shown >= total && t > Mathf.Max(total / cps + hold, least)) break;
                    if (skip) skip.alpha = Mathf.Repeat(Time.time * 1.6f, 1f) < 0.6f ? 1f : 0.25f;
                    yield return null;
                }
                // The first slide out: the meteor blows apart, leaving the crystal (the next slide's).
                if (Slides[i].look == 0) yield return MeteorBlast(root, pictures);
            }
            // Out: the slides go, and space jumps into the warp (the full one, as between stages:
            // its whoosh, streaks and flashes) - Choose Your Fighter as it slows.
            foreach (var p in pictures) if (p) Object.Destroy(p);
            if (line) Object.Destroy(line.gameObject);
            if (skip) Object.Destroy(skip.gameObject);
            Object.Destroy(root.gameObject);
            // (The ship stays hidden through the warp and Choose Your Fighter: that shows it again - ShowShip.)
            if (TcSpaceScroller.Instance) TcSpaceScroller.Instance.Warp(0);   // (its own sound and flashes)
            else TcFx.ScreenFlash(new Color(0.4f, 0.9f, 1f), 0.9f, 0.6f);
            yield return new WaitForSeconds(2.2f);
            // The HUD back for Choose Your Fighter - but the player's own (portrait, health, lives, bombs) only once a pilot's chosen.
            foreach (var g in hud)
                if (!g) continue;
                else if (TcPilots.IsPlayerHud(g.name)) { g.SetActive(true); TcPilots.HidePlayerHud(g); }
                else g.SetActive(true);
            Active = false;
            Next = false;
        }

        // A neon divider on line `over` of the story's line (a blank one, kept short: FitLines) (DividerC from the pixel UI,
        // cyan, a couple of the picture's pixels thick, a soft glow round it), showing once the typing reaches it.
        static GameObject Divider(TMP_Text line, int over)
        {
            line.ForceMeshUpdate();
            var info = line.textInfo;
            var go = new GameObject("STORY DIVIDER", typeof(RectTransform));
            go.transform.SetParent(line.transform, false);
            if (info.lineCount <= over) return go;
            float k = Mathf.Max(0.001f, line.transform.localScale.x);
            // (Halfway between the sentence over it and the one under: an even gap each side.)
            float y = over + 1 < info.lineCount ? (info.lineInfo[over - 1].descender + info.lineInfo[over + 1].ascender) * 0.5f : (info.lineInfo[over].ascender + info.lineInfo[over].descender) * 0.5f;
            float width = ((RectTransform)line.transform).sizeDelta.x * 0.72f, px = 3.2f / k;   // (a picture pixel: 2560 / 800 HUD units)
            var glow = MakeImage(go.transform, "GLOW", TcArt.Pixel, new Color(0.3f, 0.9f, 1f, 0.3f), new Vector2(width, px * 8f), y);
            var bar = MakeImage(go.transform, "BAR", TcArt.Frame("divider"), new Color(0.55f, 1f, 1f, 1f), new Vector2(width, px * 3f), y);
            go.AddComponent<DividerReveal>().Set(line, info.lineInfo[over].firstCharacterIndex, glow, bar);
            return go;
        }

        static UnityEngine.UI.Image MakeImage(Transform parent, string name, Sprite sprite, Color color, Vector2 size, float y)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            var rt = (RectTransform)image.transform;
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(0f, y);
            return image;
        }

        // The divider out of sight till the typing gets to the line under it, then drawn out from the middle.
        class DividerReveal : MonoBehaviour
        {
            TMP_Text _line;
            int _at;
            UnityEngine.UI.Image[] _parts;
            Vector2[] _sizes;
            float _t = -1f;

            public void Set(TMP_Text line, int at, params UnityEngine.UI.Image[] parts)
            {
                _line = line; _at = at; _parts = parts;
                _sizes = new Vector2[parts.Length];
                for (int i = 0; i < parts.Length; i++) _sizes[i] = parts[i].rectTransform.sizeDelta;
                if (Application.isPlaying) Show(0f);   // (the editor's previews: out)
            }

            void Show(float k)
            {
                for (int i = 0; i < _parts.Length; i++)
                    if (_parts[i]) { _parts[i].rectTransform.sizeDelta = new Vector2(_sizes[i].x * k, _sizes[i].y); _parts[i].enabled = k > 0f; }
            }

            void Update()
            {
                if (_t < 0f && _line && _line.maxVisibleCharacters >= _at) _t = 0f;
                if (_t < 0f) return;
                _t += Time.deltaTime;
                float k = Mathf.Clamp01(_t / 0.25f);
                Show(1f - (1f - k) * (1f - k));
                if (k >= 1f) enabled = false;
            }
        }

        // `text`'s lines each as big as fills the line's width (a poster's look): its size tags on each.
        static string FitLines(TMP_Text line, string text, int same = 0)
        {
            float width = ((RectTransform)line.transform).sizeDelta.x * 0.97f;
            line.fontSize = line.fontSizeMax;
            var sb = new System.Text.StringBuilder();
            var parts = text.Split('\n');
            var sizes = new float[parts.Length];
            float shared = float.MaxValue;
            for (int i = 0; i < parts.Length; i++)
            {
                float natural = string.IsNullOrWhiteSpace(parts[i]) ? 0f : line.GetPreferredValues(parts[i], float.PositiveInfinity, float.PositiveInfinity).x;
                sizes[i] = natural > 0f ? Mathf.Clamp(width / natural * 100f, 50f, 190f) : 100f;   // (a blank line: the gap the divider sits in)
                if (i < same && natural > 0f) shared = Mathf.Min(shared, sizes[i]);   // (the first `same` as big as the widest of them can be)
            }
            for (int i = 0; i < parts.Length; i++)
            {
                float pct = i < same && !string.IsNullOrWhiteSpace(parts[i]) ? shared : sizes[i];
                sb.Append("<size=").Append(Mathf.FloorToInt(pct)).Append("%>").Append(parts[i]).Append("</size>");
                if (i < parts.Length - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        // Each slide's picture, in the upper part of the picture (the line's below it).
        static void Picture(TcStageDirector director, Transform root, int look, System.Func<float, float, Vector3> At, List<GameObject> into)
        {
            switch (look)
            {
                case 0:   // the crystal, found: far out in deep space, in a cracked-open meteor - zoomed in on
                    {
                        var zoom = new GameObject("ZOOM").transform;
                        zoom.SetParent(root, false);
                        zoom.position = At(0.5f, 0.64f);
                        into.Add(zoom.gameObject);
                        var rock = Sprite(zoom, "METEOR", Meteor(), 599, zoom.position + new Vector3(0f, -0.25f, 0f), 4f);
                        var c = Sprite(zoom, "time_crystal", TcArt.Frame("time_crystal"), 600, zoom.position, 4f);
                        var frames = TcArt.Frames("time_crystal");
                        if (frames != null && frames.Length > 1) c.gameObject.AddComponent<TcAnim>().Play(frames, 10f);
                        c.gameObject.AddComponent<CrystalFx>();
                        zoom.gameObject.AddComponent<Zoom>();
                        break;
                    }
                case 1:   // ...and its power: the crystal (slide 1's, as it was) pulsing, and time itself - space
                          // racing on, slowing near to a stop, then running backwards, as the line says so
                    {
                        var crystal = Find(into, "time_crystal");
                        if (crystal && !crystal.GetComponent<Pulse>()) crystal.AddComponent<Pulse>();
                        var flow = new GameObject("TIME FLOW");
                        flow.transform.SetParent(root, false);
                        flow.AddComponent<TimeFlow>();
                        into.Add(flow);
                        break;
                    }
                case 2:   // BOOMER's armada: out from the crystal (it shrinks back, a little higher), their ships
                          // swarming in round it from all sides, each shooting a neon chain into it - caught
                    {
                        foreach (var g in into) if (g && g.name == "TIME FLOW") Object.Destroy(g);   // (time back to normal)
                        var zoom = Find(into, "ZOOM");
                        var crystal = Find(into, "time_crystal");
                        Vector3 centre = At(0.5f, 0.665f);
                        if (zoom) zoom.AddComponent<ZoomOut>().Set(centre, 0.55f);
                        string[] ships = { "enemy_heavy", "enemy_fighter", "bomber", "enemy_gunship", "striker", "enemy_fighter" };
                        float[] angles = { 0f, 50f, 130f, 180f, 230f, 310f };
                        var f = TcWorld.Field;
                        for (int i = 0; i < ships.Length; i++)
                        {
                            float a = angles[i] * Mathf.Deg2Rad;
                            var home = centre + new Vector3(Mathf.Cos(a) * 3.3f, Mathf.Sin(a) * 1.55f, 0f);
                            var from = centre + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 9f;   // (in from off the picture, its way)
                            var s = Animated(root, ships[i], home, i == 0 ? 1.7f : 1.35f, 602, 12f, into);
                            s.flipY = true;   // (its nose down...)
                            s.transform.rotation = Quaternion.FromToRotation(Vector3.down, centre - home);   // (...turned on the crystal)
                            float delay = 0.25f + 0.16f * i;
                            s.gameObject.AddComponent<Hover>().Set(home, from, delay, i * 1.3f, 0.35f);
                            if (crystal) Chain.Make(root, s.transform, crystal.transform, delay + 1.15f + 0.12f * i, into);
                        }
                        // Their name over them in the arcade font, white, as wide as the paragraph under them.
                        var title = director ? director.MakeLine("STORY TITLE", 0.86f, 1f) : null;
                        if (title)
                        {
                            title.text = "BOOMERS";
                            float k = Mathf.Max(0.001f, title.transform.localScale.x);
                            ((RectTransform)title.transform).sizeDelta = new Vector2(1920f * 0.86f / k, 2560f * 0.09f / k);
                            title.textWrappingMode = TextWrappingModes.NoWrap;
                            title.enableAutoSizing = true;
                            title.fontSizeMin = 10f;
                            title.fontSizeMax = 2560f * 0.3f / k;
                            title.color = Color.white;
                            title.alignment = TextAlignmentOptions.Center;
                            OnTop(title);
                            title.gameObject.AddComponent<TitleDrop>();   // (not till the line's out)
                            into.Add(title.gameObject);
                        }
                        break;
                    }
                case 4:   // the four pilots: their faces dropping in from the top one after another, each name
                          // under it in their colour as it lands; then their ships flying up from the bottom, one
                          // after another, to hover under their names
                    for (int i = 0; i < TcPilots.All.Length; i++)
                    {
                        var pilot = TcPilots.All[i];
                        float x = 0.17f + 0.22f * i;
                        var face = Sprite(root, pilot.Name, TcArt.Frame(pilot.Portrait), 600, At(x, 0.66f), 0.85f);
                        face.gameObject.AddComponent<Hover>().Set(face.transform.position, At(x, 1.2f), FaceGap * i, 0f, 0f);
                        into.Add(face.gameObject);
                        var jet = Animated(root, pilot.Ship + "_idle", At(x, 0.455f), 1.05f, 601, 16f, into);
                        jet.gameObject.AddComponent<Hover>().Set(jet.transform.position, At(x, -0.12f), ShipsFrom + ShipGap * i, i * 1.7f, 0.3f);
                        TcJets.Attach(jet.transform, i, into);
                        TcHitMarker.OnPreview(jet.gameObject, 601, i);   // (its hit diamond, in its pilot's colour, as in the game)
                        var name = director.MakeLine("STORY NAME", 0.535f, 1.1f);   // (under the portraits)
                        if (!name) continue;
                        var rt = (RectTransform)name.transform;
                        rt.anchoredPosition = new Vector2((x - 0.5f) * 1920f, rt.anchoredPosition.y);   // (the HUD's 1920 across)
                        name.text = pilot.Name;
                        name.color = pilot.Color;
                        name.alignment = TextAlignmentOptions.Center;
                        OnTop(name);
                        name.gameObject.AddComponent<Reveal>().after = FaceGap * i + 0.9f;   // (as its face lands)
                        into.Add(name.gameObject);
                    }
                    break;
                default:  // the crystal again, and the title
                    {
                        var c = Animated(root, "time_crystal", At(0.5f, 0.7f), 4.2f, 600, 10f, into);
                        c.gameObject.AddComponent<Pulse>();
                        c.gameObject.AddComponent<CrystalFx>();
                        break;
                    }
            }
        }

        // The first of these (or their children) called `name`.
        static GameObject Find(List<GameObject> list, string name)
        {
            foreach (var g in list)
                if (g) foreach (var t in g.GetComponentsInChildren<Transform>()) if (t.name == name) return t.gameObject;
            return null;
        }

        static SpriteRenderer Animated(Transform root, string art, Vector3 at, float scale, int order, float fps, List<GameObject> into)
        {
            var sr = Sprite(root, art, TcArt.Frame(art), order, at, scale);
            var frames = TcArt.Frames(art);
            if (frames != null && frames.Length > 1) sr.gameObject.AddComponent<TcAnim>().Play(frames, fps);
            into.Add(sr.gameObject);
            return sr;
        }

        static SpriteRenderer Sprite(Transform root, string name, Sprite sprite, int order, Vector3 at, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = at;
            go.transform.localScale = Vector3.one * scale;
            var sr = TcArt.Renderer(go, order);
            sr.sprite = sprite;
            return sr;
        }

        // Behind the slides: deep space brighter than the stages' - pixel nebula clouds (blue, violet,
        // teal, pink) and twinkling stars, over the scrolling space (the same every time).
        static Sprite s_cloud;
        static void Backdrop(Transform root, System.Func<float, float, Vector3> At)
        {
            if (!s_cloud)
            {
                const int N = 128;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "TC Story Cloud" };
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.6f + Mathf.PerlinNoise(x * 0.13f + 7f, y * 0.13f + 3f) * 0.4f;
                        float a = Mathf.Clamp01(1f - r) * Mathf.Clamp01(n * 1.6f - 0.25f);
                        a = Mathf.Round(a * 5f) / 5f;   // (in steps: pixel art's banding)
                        px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                s_cloud = UnityEngine.Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N / 2f, 0, SpriteMeshType.FullRect);   // (2 units across: a pixel the art's size at its scale)
            }
            var rnd = new System.Random(1987);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            Color[] clouds = { new Color(0.2f, 0.35f, 1f, 0.091f), new Color(0.55f, 0.25f, 0.95f, 0.084f), new Color(0.15f, 0.8f, 0.85f, 0.056f), new Color(1f, 0.3f, 0.7f, 0.063f) };
            Vector2[] at = { new Vector2(0.2f, 0.8f), new Vector2(0.8f, 0.68f), new Vector2(0.35f, 0.45f), new Vector2(0.75f, 0.25f), new Vector2(0.15f, 0.2f), new Vector2(0.6f, 0.9f), new Vector2(0.5f, 0.6f) };
            for (int i = 0; i < at.Length; i++)
            {
                var c = Sprite(root, "CLOUD", s_cloud, 591, At(at[i].x, at[i].y), R(2.4f, 3.6f));
                c.transform.localRotation = Quaternion.Euler(0f, 0f, R(0f, 360f));
                c.color = clouds[i % clouds.Length];
            }
            Color[] stars = { Color.white, new Color(0.6f, 0.9f, 1f), new Color(1f, 0.7f, 0.95f), new Color(1f, 0.95f, 0.7f) };
            for (int i = 0; i < 90; i++)
            {
                var st = Sprite(root, "STAR", TcArt.Pixel, 592, At(R(0f, 1f), R(0f, 1f)), R(0.03f, 0.06f) * (i % 9 == 0 ? 1.8f : 1f));
                st.color = TcFx.WithAlpha(stars[i % stars.Length], 0.7f);
                st.gameObject.AddComponent<Twinkle>().phase = R(0f, 10f);
            }
        }

        class Twinkle : MonoBehaviour
        {
            public float phase;
            SpriteRenderer _sr;
            void Start() => _sr = GetComponent<SpriteRenderer>();
            void Update()
            {
                var c = _sr.color;
                c.a = 0.7f * (0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 1.3f + phase)));
                _sr.color = c;
            }
        }

        // The meteor bursting: its rock in chunks flying out and spinning away, a big explosion
        // round it, a flash and a bang - the crystal hanging there, whole, as they clear.
        static IEnumerator MeteorBlast(Transform root, List<GameObject> pictures)
        {
            SpriteRenderer rock = null;
            foreach (var p in pictures)
                if (p) foreach (var r in p.GetComponentsInChildren<SpriteRenderer>()) if (r.name == "METEOR") rock = r;
            if (!rock || !rock.enabled) yield break;
            var c = rock.transform.position;
            float scale = rock.transform.lossyScale.x;
            foreach (var (piece, at) in s_shards)
            {
                var off = (Vector3)(at * scale);
                var chunk = Sprite(root, "CHUNK", piece, 603, c + off, scale);
                var out1 = ((Vector2)off).normalized;
                if (out1 == Vector2.zero) out1 = Random.insideUnitCircle.normalized;
                chunk.gameObject.AddComponent<Chunk>().velocity = out1 * Random.Range(4f, 8f) + Random.insideUnitCircle;
                pictures.Add(chunk.gameObject);
            }
            rock.enabled = false;
            var boom = Sprite(root, "BOOM", TcArt.Frame("explosion_large"), 598, c, scale * 0.45f);   // (its frames 250 px: about the meteor across; behind the crystal, the chunks over it)
            var anim = boom.gameObject.AddComponent<TcAnim>();
            anim.Play(TcArt.Frames("explosion_large"), 18f, false);
            anim.DestroyAtEnd = true;
            pictures.Add(boom.gameObject);
            TcFx.ScreenFlash(new Color(1f, 0.95f, 0.85f), 0.7f, 0.4f);
            TcSfx.Play("boss_death", 1f, 0.2f);
            yield return new WaitForSeconds(1.2f);
        }

        // A piece of the meteor flying off: spinning, slowing, fading out.
        class Chunk : MonoBehaviour
        {
            public Vector2 velocity;
            float _spin, _t;
            SpriteRenderer _sr;
            void Start() { _sr = GetComponent<SpriteRenderer>(); _spin = Random.Range(-540f, 540f); }
            void Update()
            {
                _t += Time.deltaTime;
                transform.position += (Vector3)(velocity * Time.deltaTime);
                velocity *= 1f - Time.deltaTime * 1.2f;
                transform.Rotate(0f, 0f, _spin * Time.deltaTime);
                if (_sr) { var col = _sr.color; col.a = Mathf.Clamp01(1.4f - _t * 1.4f); _sr.color = col; }
                if (_t > 1.3f) Destroy(gameObject);
            }
        }

        // The first slide's way in: from far off (the meteor a speck in deep space) in to it, easing,
        // stars streaming out past as it comes.
        class Zoom : MonoBehaviour
        {
            const float From = 0.06f;
            float _seconds = 3.5f;
            float _t;
            bool _going;

            /// In it goes (till then it's a speck far off).
            public void Begin(float seconds) { _seconds = Mathf.Max(0.5f, seconds); _going = true; }
            ParticleSystem _streaks;
            static ParticleSystem.EmitParams s_ep;

            void Start()
            {
                transform.localScale = Vector3.one * From;
                _streaks = TcFx.Make(transform.parent, "ZOOM STARS", TcArt.Pixel, 600, 593);
            }

            void Update()
            {
                if (!_going) return;
                _t += Time.deltaTime;
                float k = Mathf.Clamp01(_t / _seconds);
                float e = k * k * (3f - 2f * k);   // (eases in and out)
                transform.localScale = Vector3.one * Mathf.Lerp(From, 1f, e * e);
                if (k >= 1f) { enabled = false; return; }   // (in: the later slides move it from here)
                if (!_streaks) return;
                // Stars rushing out from the middle, faster the further out (flying in).
                var f = TcWorld.Field;
                for (int i = 0, n = Mathf.CeilToInt(Time.deltaTime * 140f * (1f - k)); i < n; i++)
                {
                    var dir = Random.insideUnitCircle.normalized;
                    float r = Random.Range(0.3f, 2.5f);
                    s_ep.position = transform.position + (Vector3)(dir * r);
                    s_ep.velocity = dir * r * Random.Range(2.5f, 4f);
                    s_ep.startColor = Random.value < 0.3f ? new Color(0.6f, 0.9f, 1f) : Color.white;
                    s_ep.startSize = Random.Range(0.03f, 0.06f);
                    s_ep.startLifetime = Random.Range(0.4f, 0.8f);
                    _streaks.Emit(s_ep, 1);
                }
            }

            void OnDestroy() { if (_streaks) Destroy(_streaks.gameObject); }
        }

        // The meteor the crystal was found in: a lumpy rock, lit from the top left, cratered,
        // cracked open in front - a dark cave the crystal hangs in, its rim and a few cracks lit
        // the crystal's blue. Pixel art at the crystal's own pixel size (made once).
        static Sprite s_meteor;
        static readonly List<(Sprite sprite, Vector2 at)> s_shards = new List<(Sprite, Vector2)>();
        static Sprite Meteor()
        {
            if (s_meteor) return s_meteor;
            const int W = 104, H = 96;
            var px = new Color32[W * H];
            var isRock = new bool[W * H];
            Color32 C(float r, float g, float b, float a = 1f) => new Color32((byte)(r * 255), (byte)(g * 255), (byte)(b * 255), (byte)(a * 255));
            Color32[] rock = { C(0.16f, 0.13f, 0.13f), C(0.3f, 0.25f, 0.22f), C(0.46f, 0.39f, 0.31f), C(0.62f, 0.53f, 0.4f), C(0.78f, 0.68f, 0.52f) };
            var cave = C(0.03f, 0.04f, 0.09f);
            Color32 glow = C(0.25f, 0.75f, 1f), glowDim = C(0.12f, 0.35f, 0.55f);
            var craters = new[] { new Vector3(0.22f, 0.72f, 0.07f), new Vector3(0.8f, 0.3f, 0.09f), new Vector3(0.18f, 0.28f, 0.06f), new Vector3(0.78f, 0.78f, 0.05f), new Vector3(0.5f, 0.12f, 0.05f) };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H * 2f - 1f;
                    float a = Mathf.Atan2(v, u), d = Mathf.Sqrt(u * u + v * v);
                    float edge = 0.94f + 0.06f * Mathf.Sin(a * 3f + 0.7f) + 0.04f * Mathf.Sin(a * 7f + 2f);
                    if (d > edge) { px[y * W + x] = new Color32(0, 0, 0, 0); continue; }
                    // The cave: taller than wide, a little above the middle, its edge rough.
                    float cu = u / 0.56f, cv = (v - 0.12f) / 0.74f;
                    float cd = Mathf.Sqrt(cu * cu + cv * cv), crough = 1f + 0.07f * Mathf.Sin(Mathf.Atan2(cv, cu) * 9f) + 0.04f * Mathf.Sin(Mathf.Atan2(cv, cu) * 17f);
                    if (cd < crough)
                    {
                        // Inside it: dark, its walls lit blue near the crystal (the middle).
                        float lit = Mathf.Clamp01(1f - cd / crough);
                        px[y * W + x] = cd > crough - 0.04f ? C(0.08f, 0.2f, 0.32f) : (Color32)Color.Lerp(cave, C(0.06f, 0.12f, 0.22f), lit * 0.8f);
                        continue;
                    }
                    if (cd < crough + 0.03f) { px[y * W + x] = glowDim; continue; }   // (the rim, lit)
                    // The rock: lit from the top left, with lumps (noise) and its outline dark.
                    float shade = 0.5f + 0.6f * (-u * 0.6f + v * 0.7f) / 1.2f;
                    shade += (Mathf.PerlinNoise(x * 0.18f, y * 0.18f) - 0.5f) * 0.3f;
                    foreach (var cr in craters)
                    {
                        float dx = (x + 0.5f) / W - cr.x, dy = (y + 0.5f) / H - cr.y, cdd = Mathf.Sqrt(dx * dx + dy * dy);
                        if (cdd < cr.z) shade += (dx - dy) / cr.z * 0.35f - 0.25f;   // (a dip: dark top left, lit bottom right)
                    }
                    if (d > edge - 0.05f) shade = 0f;
                    int band = Mathf.Clamp(Mathf.FloorToInt(shade * rock.Length), 0, rock.Length - 1);
                    px[y * W + x] = rock[band];
                    isRock[y * W + x] = true;
                }
            // A few cracks out from the cave, glowing.
            var rnd = new System.Random(7);
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * Mathf.PI * 2f + 0.4f;
                float fx = W * 0.5f + Mathf.Cos(a) * W * 0.29f, fy = H * (0.5f + 0.06f) + Mathf.Sin(a) * H * 0.38f;
                for (int step = 0, n = 6 + rnd.Next(8); step < n; step++)
                {
                    a += (float)(rnd.NextDouble() - 0.5) * 0.9f;
                    fx += Mathf.Cos(a); fy += Mathf.Sin(a);
                    int ix = Mathf.RoundToInt(fx), iy = Mathf.RoundToInt(fy);
                    if (ix < 0 || iy < 0 || ix >= W || iy >= H || px[iy * W + ix].a == 0) break;
                    px[iy * W + ix] = step < n / 2 ? glow : glowDim;
                }
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "TC Story Meteor" };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_meteor = UnityEngine.Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), TcArt.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            // Its shards for MeteorBlast: the rock (not the cave) split round random points into
            // jagged pieces, each its own little sprite and where its middle sits on the meteor.
            const int Shards = 16;
            var seeds = new Vector2[Shards];
            for (int i = 0; i < Shards; i++)
            {
                float a = i / (float)Shards * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.3f;
                float r = (i % 2 == 0 ? 0.42f : 0.36f) + (float)rnd.NextDouble() * 0.06f;
                seeds[i] = new Vector2(W * (0.5f + Mathf.Cos(a) * r), H * (0.5f + Mathf.Sin(a) * r));
            }
            var owner = new int[W * H];
            var min = new Vector2Int[Shards]; var max = new Vector2Int[Shards];
            for (int i = 0; i < Shards; i++) { min[i] = new Vector2Int(W, H); max[i] = new Vector2Int(-1, -1); }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    owner[y * W + x] = -1;
                    if (!isRock[y * W + x]) continue;
                    int best = 0; float bd = float.MaxValue;
                    for (int i = 0; i < Shards; i++)
                    {
                        float d = (seeds[i] - new Vector2(x, y)).sqrMagnitude * (0.8f + 0.4f * Mathf.PerlinNoise(x * 0.3f + i, y * 0.3f));   // (ragged edges)
                        if (d < bd) { bd = d; best = i; }
                    }
                    owner[y * W + x] = best;
                    min[best] = Vector2Int.Min(min[best], new Vector2Int(x, y));
                    max[best] = Vector2Int.Max(max[best], new Vector2Int(x, y));
                }
            s_shards.Clear();
            for (int i = 0; i < Shards; i++)
            {
                if (max[i].x < 0) continue;
                int sw = max[i].x - min[i].x + 1, sh = max[i].y - min[i].y + 1;
                var spx = new Color32[sw * sh];
                for (int y = 0; y < sh; y++)
                    for (int x = 0; x < sw; x++)
                    {
                        int k = (y + min[i].y) * W + x + min[i].x;
                        spx[y * sw + x] = owner[k] == i ? px[k] : new Color32(0, 0, 0, 0);
                    }
                var st = new Texture2D(sw, sh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "TC Meteor Shard" };
                st.SetPixels32(spx);
                st.Apply(false, true);
                var centre = new Vector2(min[i].x + sw * 0.5f - W * 0.5f, min[i].y + sh * 0.5f - H * 0.5f) / TcArt.PixelsPerUnit;
                s_shards.Add((UnityEngine.Sprite.Create(st, new Rect(0, 0, sw, sh), new Vector2(0.5f, 0.5f), TcArt.PixelsPerUnit, 0, SpriteMeshType.FullRect), centre));
            }
            return s_meteor;
        }

        // Slide 3's way out from the crystal: it shrinks back and up a little, easing.
        class ZoomOut : MonoBehaviour
        {
            Vector3 _fromAt, _toAt;
            float _fromScale, _toScale, _t;
            const float Seconds = 1.3f;

            public void Set(Vector3 at, float scale)
            {
                _fromAt = transform.position; _toAt = at;
                _fromScale = transform.localScale.x; _toScale = scale;
                if (!Application.isPlaying) { transform.position = at; transform.localScale = Vector3.one * scale; }   // (the editor's previews)
            }

            void Update()
            {
                _t += Time.deltaTime;
                float k = Mathf.Clamp01(_t / Seconds);
                float e = k * k * (3f - 2f * k);
                transform.position = Vector3.Lerp(_fromAt, _toAt, e);
                transform.localScale = Vector3.one * Mathf.Lerp(_fromScale, _toScale, e);
                if (k >= 1f) enabled = false;
            }
        }

        // Slide 2's time: as the line gets to SPEED IT UP space races on (the stages' space and the
        // slide's stars, streaks of light), at SLOW IT DOWN it nearly stops, and at TURN IT BACK it
        // runs backwards - till the next slide puts it right (OnDestroy).
        class TimeFlow : MonoBehaviour
        {
            float _speed = 1f, _target = 1f;
            int _phase;
            readonly List<Transform> _stars = new List<Transform>();

            void Start()
            {
                foreach (Transform t in transform.parent) if (t.name == "STAR") _stars.Add(t);
            }

            /// The line, `shown` letters of it out.
            public void Shown(string text, int shown)
            {
                int phase = shown >= Index(text, "TURN") ? 3 : shown >= Index(text, "SLOW") ? 2 : shown >= Index(text, "SPEED") ? 1 : 0;
                if (phase == _phase) return;
                _phase = phase;
                _target = phase == 1 ? 10f : phase == 2 ? 0.1f : phase == 3 ? -8f : 1f;
                if (phase == 1 || phase == 3) TcSfx.Play("warp", 0.7f, 0.3f);
            }

            static int Index(string text, string word) { int i = text.IndexOf(word, System.StringComparison.Ordinal); return i < 0 ? int.MaxValue : i; }

            void Update()
            {
                _speed = Mathf.MoveTowards(_speed, _target, Time.deltaTime * (_target < _speed ? 16f : 12f));
                if (TcSpaceScroller.Instance) TcSpaceScroller.Instance.Boost = _speed;
                var f = TcWorld.Field;
                foreach (var t in _stars)
                {
                    if (!t) continue;
                    var p = t.position;
                    p.y -= _speed * 0.35f * Time.deltaTime;
                    if (p.y < f.yMin) p.y += f.height; else if (p.y > f.yMax) p.y -= f.height;
                    t.position = p;
                }
                float fast = Mathf.Abs(_speed);
                if (fast > 2.5f) TcFx.WarpStreaks(Mathf.CeilToInt(Time.deltaTime * fast * 22f), fast * 5f, _speed < 0f);
            }

            void OnDestroy() { if (TcSpaceScroller.Instance) TcSpaceScroller.Instance.Boost = 1f; }
        }

        // A BOOMER ship's neon chain: after `delay` it's shot from the ship's nose like a harpoon gun (a
        // muzzle flash, there in a blink), hits the crystal in a burst of pixels and a little explosion
        // (a clank, the crystal jolted), then stays hooked there - glowing, pulsing pink to violet, lightning
        // crackling along it.
        class Chain : MonoBehaviour
        {
            Transform _ship, _crystal;
            float _delay, _t;
            SpriteRenderer _links, _glow;
            bool _hooked;
            const float ShootTime = 0.07f, Nose = 0.45f, Edge = 0.16f;   // (Edge: the crystal's half width, its own units)
            const float Px = 0.03f;   // (its pixels: the chain's)
            ParticleSystem _fx;
            bool _fired;
            float _length;
            static ParticleSystem.EmitParams s_ep;
            static readonly Color Pink = new Color(1f, 0.35f, 0.85f), Violet = new Color(0.7f, 0.45f, 1f), Spark = new Color(0.75f, 0.95f, 1f);
            static Sprite s_link;

            public static void Make(Transform root, Transform ship, Transform crystal, float delay, List<GameObject> into)
            {
                if (!s_link)
                {
                    // One link and the next (side on): an oval ring, then a short bar to the next - 8 x 5.
                    string[] rows = { ".XXXX...", "X....X..", "X....XXX", "X....X..", ".XXXX..." };
                    var tex = new Texture2D(8, 5, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "TC Story Chain" };
                    for (int y = 0; y < 5; y++)
                        for (int x = 0; x < 8; x++)
                            tex.SetPixel(x, 4 - y, rows[y][x] == 'X' ? Color.white : new Color(1f, 1f, 1f, 0f));
                    tex.Apply(false, true);
                    // (Its pixels twice the picture's: about the ships' art.)
                    s_link = UnityEngine.Sprite.Create(tex, new Rect(0, 0, 8, 5), new Vector2(0f, 0.5f), TcArt.PixelsPerUnit / 2f, 0, SpriteMeshType.FullRect);
                }
                var go = new GameObject("CHAIN");
                go.transform.SetParent(root, false);
                var c = go.AddComponent<Chain>();
                c._ship = ship; c._crystal = crystal; c._delay = delay;
                c._links = TcArt.Renderer(go, 601);
                c._links.sprite = s_link;
                c._links.drawMode = SpriteDrawMode.Tiled;
                c._links.size = new Vector2(0f, s_link.bounds.size.y);
                var glow = new GameObject("GLOW");
                glow.transform.SetParent(go.transform, false);
                c._glow = TcArt.Renderer(glow, 600);
                c._glow.sprite = TcArt.Pixel;
                c._fx = TcFx.Make(go.transform, "CHAIN FX", TcArt.Pixel, 1500, 604);
                into.Add(go);
                c.Place(Application.isPlaying ? 0f : 1f);   // (the editor's previews: hooked, crackling)
                if (!Application.isPlaying) { c._hooked = true; c.Lightning(); c.Lightning(); c._fx.Simulate(0.005f, true, false, false); }
            }

            void Update()
            {
                _t += Time.deltaTime;
                float k = Mathf.Clamp01((_t - _delay) / ShootTime);
                if (k > 0f && !_fired)
                {
                    // Fired: a flash of pixels out of the nose.
                    _fired = true;
                    TcSfx.Play("dart_shot", 0.6f, 0.03f);
                    for (int i = 0; i < 12; i++)
                        Emit(transform.position, (Vector2)(transform.right * Random.Range(1f, 4f)) + Random.insideUnitCircle * 1.5f, Random.value < 0.4f ? Color.white : Pink, Px * Random.Range(1f, 1.6f), Random.Range(0.1f, 0.22f));
                }
                if (k >= 1f && !_hooked)
                {
                    _hooked = true;
                    TcSfx.Play("boss_chain", 0.7f, 0.05f);
                    Hit();
                    if (_crystal)
                    {
                        if (!_crystal.TryGetComponent(out Jolt jolt)) jolt = _crystal.gameObject.AddComponent<Jolt>();
                        jolt.Hit();
                    }
                }
                Place(k);
                if (_hooked && Random.value < Time.deltaTime * 14f) Lightning();
            }

            // Hooked: a burst of pixels flying off the crystal where it hit, and a little explosion.
            void Hit()
            {
                var at = transform.position + transform.right * _length;
                for (int i = 0; i < 34; i++)
                {
                    var c = i % 4 == 0 ? Color.white : i % 4 == 1 ? Spark : i % 2 == 0 ? Pink : Violet;
                    Emit(at, Random.insideUnitCircle.normalized * Random.Range(1.5f, 7f) - (Vector2)transform.right * 1.5f, c, Px * Random.Range(1f, 2f), Random.Range(0.25f, 0.6f));
                }
                var boom = new GameObject("CHAIN HIT");
                boom.transform.SetParent(transform, false);
                boom.transform.position = at;
                boom.transform.localScale = Vector3.one * 0.55f;
                var sr = TcArt.Renderer(boom, 603);
                var anim = boom.AddComponent<TcAnim>();
                anim.Play(TcArt.Frames("explosion_small"), 26f, false);
                anim.DestroyAtEnd = true;
            }

            // A bolt of lightning crackling along the chain: a jagged line of pixels in and out of it, a fork now and then.
            void Lightning()
            {
                if (_length <= 0.1f) return;
                float life = Random.Range(0.05f, 0.1f);
                var right = transform.right; var up = transform.up;
                float x = Random.Range(0f, _length * 0.4f), end = Mathf.Min(_length, x + Random.Range(0.6f, 1.6f));
                float off = 0f;
                int n = 0;
                var c = Random.value < 0.5f ? Spark : Color.Lerp(Pink, Color.white, 0.4f);
                while (x < end)
                {
                    off = Mathf.Clamp(off + Random.Range(-2, 3) * Px, -Px * 4f, Px * 4f);
                    var at = transform.position + right * x + up * off;
                    Emit(at, Vector2.zero, n % 4 == 0 ? Color.white : c, Px * 1.1f, life);
                    if (Random.value < 0.06f)
                        for (int f = 0, m = Random.Range(2, 5); f < m; f++)
                            Emit(at + up * (Mathf.Sign(off + 0.001f) * Px * (f + 1) * 1.2f) + right * Random.Range(-1, 2) * Px, Vector2.zero, c, Px, life * 0.8f);
                    x += Px * Random.Range(0.8f, 1.4f);
                    n++;
                }
            }

            void Emit(Vector3 at, Vector2 v, Color c, float size, float life)
            {
                if (!_fx) return;
                s_ep.position = at;
                s_ep.velocity = v;
                s_ep.startColor = c;
                s_ep.startSize = size;
                s_ep.startLifetime = life;
                _fx.Emit(s_ep, 1);
            }

            // Ship to crystal, `k` of the way out.
            void Place(float k)
            {
                if (!_ship || !_crystal) return;
                Vector3 to = _crystal.position, from = _ship.position;
                Vector3 dir = (to - from).normalized;
                from += dir * Nose;
                float length = Mathf.Max(0f, Vector3.Distance(from, to) - Edge * _crystal.lossyScale.x) * k;   // (to its edge)
                transform.position = from;
                transform.rotation = Quaternion.FromToRotation(Vector3.right, dir);
                _length = length;
                _links.size = new Vector2(length, s_link.bounds.size.y);
                _links.enabled = _glow.enabled = k > 0f;
                // Neon: pink to violet and back, the glow under it the same, faint and wider.
                var neon = Color.Lerp(new Color(1f, 0.3f, 0.85f), new Color(0.65f, 0.4f, 1f), 0.5f + 0.5f * Mathf.Sin(Time.time * 4f + transform.GetSiblingIndex()));
                _links.color = neon;
                _glow.transform.localPosition = new Vector3(length * 0.5f, 0f, 0.001f);
                _glow.transform.localScale = new Vector3(length, s_link.bounds.size.y * 1.5f, 1f);
                _glow.color = TcFx.WithAlpha(neon, 0.18f);
            }
        }

        // BOOMERS dropping in from above (once Begin: the line's out), a bounce as it lands, and a clank.
        class TitleDrop : MonoBehaviour
        {
            RectTransform _rt;
            TMP_Text _text;
            Vector2 _home;
            float _t = -1f;
            const float Fall = 0.45f, From = 700f;

            void Awake()
            {
                _rt = (RectTransform)transform;
                _text = GetComponent<TMP_Text>();
                _home = _rt.anchoredPosition;
                if (Application.isPlaying) _text.alpha = 0f;   // (the editor's previews: in place)
            }

            public void Begin() { if (_t < 0f) { _t = 0f; TcSfx.Play("boss_chain", 0.8f, 0.1f); } }

            void Update()
            {
                if (_t < 0f) return;
                _t += Time.deltaTime;
                float k = Mathf.Clamp01(_t / Fall);
                // Falling in, past its place and back up a little (a bounce), settling.
                float e = 1f - Mathf.Pow(1f - k, 3f);
                float bounce = k >= 1f ? Mathf.Sin(Mathf.Min(1f, (_t - Fall) / 0.3f) * Mathf.PI) * -40f * (1f - Mathf.Min(1f, (_t - Fall) / 0.3f)) : 0f;
                _rt.anchoredPosition = _home + Vector2.up * (From * (1f - e) + bounce);
                _text.alpha = Mathf.Clamp01(k * 3f);
                if (_t > Fall + 0.4f) enabled = false;
            }
        }

        // The story's start: black a moment, then fading away (easing).
        class FadeIn : MonoBehaviour
        {
            internal const float Hold = 0.35f, Seconds = 1.9f;   // (Seconds: all of it - slide 1's line starts then)
            SpriteRenderer _r;
            float _t;
            void Start() { _r = GetComponent<SpriteRenderer>(); if (!Application.isPlaying) Destroy(gameObject); }
            void Update()
            {
                _t += Time.deltaTime;
                float k = Mathf.Clamp01((_t - Hold) / (Seconds - Hold));
                _r.color = new Color(0f, 0f, 0f, 1f - k * k * (3f - 2f * k));
                if (k >= 1f) Destroy(gameObject);
            }
        }

        // A line out of sight till `after` seconds, then popping in.
        class Reveal : MonoBehaviour
        {
            public float after;
            TMP_Text _text;
            float _t;
            Vector3 _scale;
            void Start() { _text = GetComponent<TMP_Text>(); _scale = transform.localScale; _text.alpha = 0f; }
            void Update()
            {
                _t += Time.deltaTime;
                float k = Mathf.Clamp01((_t - after) / 0.15f);
                _text.alpha = k;
                transform.localScale = _scale * (1f + 0.35f * Mathf.Sin(k * Mathf.PI));
                if (_t > after + 0.15f) { transform.localScale = _scale; enabled = false; }
            }
        }

        // The crystal knocked as a chain hooks it: a shake, settling back where it hangs.
        class Jolt : MonoBehaviour
        {
            Vector3 _home;
            float _left;
            void Awake() => _home = transform.localPosition;
            public void Hit() => _left = 0.25f;
            void Update()
            {
                if (_left <= 0f) return;
                _left -= Time.deltaTime;
                transform.localPosition = _left > 0f ? _home + (Vector3)(Random.insideUnitCircle * 0.03f * (_left / 0.25f)) : _home;
            }
        }

        // Floating in space: a slow bob and drift about its place - after flying in to it, if from somewhere.
        class Hover : MonoBehaviour
        {
            Vector3 _home, _from;
            bool _flies;
            float _delay, _phase, _t, _bob = 1f;
            const float FlyTime = 1.1f;

            public void Set(Vector3 home, Vector3? from, float delay, float phase, float bob = 1f)
            {
                _bob = bob;
                _home = home;
                _flies = from.HasValue;
                _from = from ?? home;
                _delay = delay;
                _phase = phase;
                transform.position = _flies && Application.isPlaying ? _from : home;   // (the editor's previews: at home)
            }

            void Update()
            {
                _t += Time.deltaTime;
                float fly = _flies ? Mathf.Clamp01((_t - _delay) / FlyTime) : 1f;
                fly = 1f - Mathf.Pow(1f - fly, 3f);   // (eases into place)
                float b = _t + _phase;
                var bob = new Vector3(Mathf.Sin(b * 0.9f) * 0.12f, Mathf.Sin(b * 1.4f) * 0.16f, 0f) * fly * _bob;
                transform.position = Vector3.LerpUnclamped(_from, _home, fly) + bob;
            }
        }

        // The crystal's power: lightning swirling round it, wrapping it (two bolts on tilted rings,
        // drifting up and down it - the far side of a ring behind it, dimmer), and every few
        // seconds a spray of blue pixel sparks out of each of its flames.
        class CrystalFx : MonoBehaviour
        {
            ParticleSystem _front, _back;
            float _spin, _nextSpark;
            static ParticleSystem.EmitParams s_ep;
            // Its art's places, in its own units: the body's middle and half size; the two flames.
            static readonly Vector2 Body = new Vector2(0f, -0.09f), Ring = new Vector2(0.21f, 0.06f);
            static readonly Vector2[] Flames = { new Vector2(-0.36f, 0.17f), new Vector2(0.38f, 0.15f) };
            const float Px = 0.05f;

            void Awake()
            {
                _front = TcFx.Make(transform, "CRYSTAL FX", TcArt.Pixel, 3000, 605);
                _back = TcFx.Make(transform, "CRYSTAL FX BACK", TcArt.Pixel, 1500, 598);
                _nextSpark = 0.6f;
            }

            void Update() => Tick(Time.deltaTime);

            /// One step of it (the editor's previews call it themselves).
            public void Tick(float dt)
            {
                if (!_front) Awake();
                _spin += dt * 4.2f;
                for (int bolt = 0; bolt < 2; bolt++)
                {
                    // Its ring: up and down the crystal, tilted, the head going round it.
                    float head = _spin + bolt * Mathf.PI;
                    float height = Body.y + Mathf.Sin(_spin * 0.45f + bolt * 2.2f) * 0.2f;
                    float tilt = (bolt == 0 ? 0.35f : -0.3f);
                    float wobble = 0f;
                    for (int i = 0; i < 18; i++)
                    {
                        float a = head - i * 0.085f;
                        wobble = Mathf.Clamp(wobble + Random.Range(-0.012f, 0.012f), -0.025f, 0.025f);
                        float x = Mathf.Cos(a) * (Ring.x + wobble), y = Mathf.Sin(a) * (Ring.y + wobble);
                        var at = transform.TransformPoint(new Vector3(x, height + y + x * tilt, 0f));
                        bool far = Mathf.Sin(a) > 0f;   // (the ring's back half: behind the crystal)
                        Color c = i % 5 == 0 ? Color.white : Random.value < 0.5f ? new Color(0.3f, 0.95f, 1f) : new Color(0.45f, 0.65f, 1f);
                        if (far) c.a = 0.55f;
                        Emit(far ? _back : _front, at, Vector2.zero, c, Px * (i < 3 ? 1.3f : 1f), Random.Range(0.05f, 0.09f));
                        // Now and then a fork jumping off it.
                        if (!far && Random.value < 0.05f)
                        {
                            var fork = at;
                            var dir = ((Vector2)(at - transform.position)).normalized;
                            for (int k = 0, n = Random.Range(2, 5); k < n; k++)
                            {
                                fork += (Vector3)(dir * Px * 1.1f + Random.insideUnitCircle * Px * 0.8f);
                                Emit(_front, fork, Vector2.zero, new Color(0.5f, 0.9f, 1f), Px, 0.06f);
                            }
                        }
                    }
                }
                // The flames' sparks: a spray up and out of each, every couple of seconds; a stray one now and then.
                _nextSpark -= dt;
                foreach (var f in Flames)
                {
                    var at = transform.TransformPoint(f);
                    if (Random.value < dt * 3f) Spark(at, f.x, 1f);
                    if (_nextSpark <= 0f) for (int k = 0; k < 16; k++) Spark(at, f.x, Random.Range(0.6f, 1.4f));
                }
                if (_nextSpark <= 0f) _nextSpark = Random.Range(1.6f, 3f);
            }

            void Spark(Vector3 at, float side, float speed)
            {
                var v = new Vector2(Mathf.Sign(side) * Random.Range(0.2f, 1.6f), Random.Range(1.4f, 3.4f)) * speed;
                Color c = Random.value < 0.25f ? Color.white : Random.value < 0.6f ? new Color(0.3f, 0.9f, 1f) : new Color(0.25f, 0.55f, 1f);
                Emit(_front, at + (Vector3)(Random.insideUnitCircle * Px), v, c, Px * Random.Range(0.8f, 1.3f), Random.Range(0.4f, 0.8f));
            }

            static void Emit(ParticleSystem ps, Vector3 at, Vector2 v, Color c, float size, float life)
            {
                s_ep.position = at;
                s_ep.velocity = v;
                s_ep.startColor = c;
                s_ep.startSize = size;
                s_ep.startLifetime = life;
                ps.Emit(s_ep, 1);
            }
        }

        // The crystal's power: it swells and glows a little, on a beat.
        class Pulse : MonoBehaviour
        {
            Vector3 _base;
            void Start() => _base = transform.localScale;
            void Update()
            {
                float k = 1f + 0.06f * Mathf.Sin(Time.time * 5f);
                transform.localScale = _base * k;
                if (Random.value < Time.deltaTime * 20f)
                    TcFx.Trail(transform.position + (Vector3)(Random.insideUnitCircle * 0.8f), new Color(0.45f, 0.9f, 1f, 0.8f), 0.07f, Random.insideUnitCircle * 0.8f, 0.7f);
            }
        }
    }
}
