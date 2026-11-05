using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TimeCrystal
{
    // ARcade: THE TIME CRYSTAL's stage, in the manner of CAVE's shooters
    // (DoDonPachi): a 3-2-1-GO countdown, a breather, then waves of neon
    // ships (TcEnemy) - each kind its own colour, size and toughness -
    // filling the screen with bullets (TcDanmaku), building up to one big
    // boss (TcBoss) with a health bar; then it all loops, a little harder
    // each time round. Gold carriers drop power-ups (TcWeapons).
    // Positions are fractions of the picture (0,0 bottom left - 1,1 top right).
    public class TcStageDirector : MonoBehaviour
    {
        [Header("The jam build's enemies (only their gem and score popup are used now)")]
        public GameObject[] Small;
        public GameObject Medium;
        public Sprite[] Orbs;   // (old; the bullets are TcDanmaku's now)

        [Header("Sounds (Retro Arsenal)")]
        public AudioClip BeepSound;      // the countdown (GO! higher)
        public AudioClip HitSound;       // a shot landing on an enemy: a soft explosion
        public AudioClip DestroySound;   // an enemy going up: a bigger one
        [Range(0f, 1f)] public float HitVolume = 0.3f, DestroyVolume = 0.9f;

        public static TcStageDirector Instance { get; private set; }
        AudioSource _audio;
        GameObject _gem, _popup;
        TMP_FontAsset _font;

        void Awake()
        {
            Instance = this;
            _audio = GetComponent<AudioSource>();
            // The jam build's gem and score popup, from its small enemy.
            var old = Small != null && Small.Length > 0 && Small[0] ? Small[0].GetComponent<EnemyShooter2D>() : null;
            if (old)
            {
                _gem = old.deathVFX;
                _popup = old.popupPrefab;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) { Instance = null; HoldFire = false; }
        }

        /// A shot landing (TcWorld.SoundAllowed keeps a hail of them to one at a time).
        public void PlayHit() => Play(HitSound, HitVolume, 0.07f);
        public void PlayDestroyed(bool big = false) => Play(DestroySound, big ? 1f : DestroyVolume, 0.08f, big ? 0.75f : 1f);
        public void PlayPowerUp()
        {
            TcSfx.Play("pickup_shotgun", 0.55f, 0.05f);   // (a shotgun's cock and shot, as a bomb's: TcBombs.PickedUp)
        }

        void Play(AudioClip clip, float volume, float gap, float pitch = 1f)
        {
            if (!clip || !_audio || !TcWorld.SoundAllowed(clip, gap)) return;
            _audio.pitch = pitch;
            _audio.PlayOneShot(clip, volume);
        }

        /// The score it's worth, floating up where an enemy went.
        public void Popup(Vector3 at, int score)
        {
            if (!_popup || !FloatingScorePopupPool.Instance) return;
            var popup = FloatingScorePopupPool.Instance.GetPopup(_popup);
            popup.transform.position = at;
            popup.Initialize(score, _popup);
        }

        /// A gem (worth points) drifting down from where an enemy went - a
        /// boss's (`boss` 0, 1, 2) its own kind: emeralds, blue diamonds, red
        /// diamonds, each worth more.
        public GameObject DropGem(Vector3 at, int boss = -1, string strip = null)
        {
            if (!_gem || !_gem.GetComponent<CollectableItem>()) return null;
            var gem = TcWorld.Spawn(_gem, at, Quaternion.identity);
            gem.AddComponent<TcOffField>();
            // A boss's gems are pulled into the ship like the gold coins; the rest drift down to be flown over.
            if (boss < 0) { gem.AddComponent<CollectibleMover>(); return gem; }
            gem.AddComponent<TcGemPull>();
            string[] strips = { "gem_emerald", "gem_blue", "gem_red" };
            var frames = TcArt.Frames(strip ?? strips[Mathf.Clamp(boss, 0, strips.Length - 1)]);   // (`strip`: the crystal's diamonds, its colour)
            if (frames == null || frames.Length == 0) return gem;
            if (gem.GetComponent<Animator>() is { } animator) animator.enabled = false;
            // The same size on screen as the usual gem (and the same reach to pick up).
            var r = gem.GetComponent<SpriteRenderer>();
            float before = r && r.sprite ? r.sprite.bounds.size.x * gem.transform.localScale.x : 0.6f;
            float scale = before / frames[0].bounds.size.x * 1.15f;
            if (gem.GetComponent<CircleCollider2D>() is { } circle) circle.radius *= gem.transform.localScale.x / scale;
            gem.transform.localScale = Vector3.one * scale;
            gem.AddComponent<TcAnim>().Play(frames, 12f);
            gem.GetComponent<CollectableItem>().scoreValue = 2500 * (boss + 1);   // (there are lots of them now)
            return gem;
        }

        [Header("Start")]
        public float BeforeEnemies = 5f;   // after GO!
        public CollectibleWaveSpawner Crystals;   // the falling crystals start at GO!

        int _loop, _level;
        /// Which stage it is, 0..2 (the enemies' death colours follow it: TcFx.StageBlast).
        public int Level => _level;
        /// Which loop it is (0 the first): its TIME CRYSTAL a colour of its own (TcTimeCrystal.Looks).
        public int Loop => _loop;
        /// The stage whose music's playing (TcMusic): the next one's from the moment its warp starts.
        public int MusicStage { get; private set; }
        const int Levels = 3;
        TMP_Text _countdown, _announce, _press;

        // Each stage, and more each time round all three, up to Hardest:
        // bullets a little faster, more of them, tougher ships.
        const int Hardest = 4;
        int D => Mathf.Min(_loop * 2 + _level, Hardest);
        float BulletSpeed(float speed) => speed * (1f + 0.07f * D);
        int Tough(int health) => Mathf.RoundToInt(health * (1f + 0.25f * D));

        /// No shots or bombs yet: from Choose Your Fighter (its button press)
        /// until GO! (TimeCrystalGame holds them).
        public static bool HoldFire { get; private set; }

        IEnumerator Start()
        {
            HoldFire = true;
            TcDanmaku.Reset();
            MakeTexts();
            LayoutHud();
            TcPilots.ShowOnHud();
            TcTimeCrystal.Clear();
            if (TcStory.Due()) yield return TcStory.Run(this);   // (the story: the first time, then every few games)
            yield return TcFighterSelect.Run(this);
            yield return Countdown();
            HoldFire = false;
            if (Crystals) Crystals.enabled = true;
            yield return new WaitForSeconds(BeforeEnemies);
            // Three stages, each its own waves and ships and its own BIG boss
            // at the end (WARNING first); then round again, harder.
            while (true)
            {
                for (_level = 0; _level < Levels; _level++)
                {
                    if (_level > 0 || _loop > 0)
                    {
                        // (After the crystal: back to STAGE 1, time turned back - the loop counted under it.)
                        Announce("STAGE " + (_level + 1) + (_loop > 0 ? "\nLOOP " + (_loop + 1) : ""), TcFx.Cyan, 2.2f, 2f, important: true);
                        yield return new WaitForSeconds(2.5f);
                    }
                    yield return _level == 0 ? Stage1() : _level == 1 ? Stage2() : Stage3();
                    yield return WaitForClear(14f);
                    yield return Boss(_level);
                    yield return new WaitForSeconds(3f);
                    if (_level == Levels - 1)
                    {
                        // The last boss guarded THE TIME CRYSTAL: it's shot to pieces, then time snaps
                        // back - the warp backwards to stage 1, the score kept.
                        yield return CrystalFinale();
                        continue;
                    }
                    // On into the next zone's space (the third loops back to the first),
                    // nothing else in the picture: what's still floating is taken or goes.
                    ClearForWarp();
                    _warping = true;   // (kept clear - gems and all - till the next level: Update)
                    if (Crystals) Crystals.enabled = false;
                    // QUANTUM DRIVE ACTIVATED, flashing like WARNING (space flashing cyan, the
                    // warning sounds for now), then the jump.
                    yield return QuantumDrive();
                    MusicStage = (_level + 1) % Levels;   // (its music fading in with the warp)
                    if (TcSpaceScroller.Instance) TcSpaceScroller.Instance.Warp((_level + 1) % Levels);
                    // The jump: barrel rolls all through its run up and top speed (about 1.7 s).
                    if (TcPlayerShip.Instance) TcPlayerShip.Instance.BarrelRolls(3);
                    yield return new WaitForSeconds(3.2f);
                    _warping = false;
                    if (Crystals) Crystals.enabled = true;
                }
                _loop++;
            }
        }

        // ---- Countdown and announcements ----

        IEnumerator Countdown()
        {
            foreach (string word in new[] { "3", "2", "1", "GO!" })
            {
                if (_countdown)
                {
                    _countdown.text = word;
                    _countdown.gameObject.SetActive(true);
                }
                if (BeepSound && _audio)
                {
                    _audio.pitch = word == "GO!" ? 1.5f : 1f;
                    _audio.PlayOneShot(BeepSound);
                }
                for (float t = 0f; t < 1f; t += Time.deltaTime)
                {
                    if (_countdown)
                    {
                        float pop = 1f + 0.6f * Mathf.Clamp01(1f - t / 0.25f);
                        _countdown.transform.localScale = _countdownScale * pop;
                        var c = _countdown.color;
                        c.a = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
                        _countdown.color = c;
                    }
                    yield return null;
                }
            }
            if (_countdown) _countdown.gameObject.SetActive(false);
        }

        Vector3 _countdownScale = Vector3.one, _announceScale = Vector3.one;

        void MakeTexts()
        {
            // The score's number (its font and colour), big, in the middle.
            var canvas = TcWorld.Root ? TcWorld.Root.Find("Canvas") : null;
            var score = canvas ? canvas.Find("PLAYER 1 SCORE") : null;
            if (!score) return;
            _font = score.GetComponent<TMP_Text>().font;
            var copy = Instantiate(score.gameObject, canvas);
            copy.name = "COUNTDOWN";
            _countdown = copy.GetComponent<TMP_Text>();
            _countdown.alignment = TextAlignmentOptions.Center;
            var rect = (RectTransform)copy.transform;
            rect.anchoredPosition = new Vector2(0f, 150f);
            _countdownScale = rect.localScale * 3.5f;
            copy.SetActive(false);

            var line = Instantiate(score.gameObject, canvas);
            line.name = "ANNOUNCE";
            _announce = line.GetComponent<TMP_Text>();
            _announce.alignment = TextAlignmentOptions.Center;
            _announce.textWrappingMode = TextWrappingModes.NoWrap;
            _announce.overflowMode = TextOverflowModes.Overflow;
            var r = (RectTransform)line.transform;
            r.sizeDelta = new Vector2(600f, r.sizeDelta.y);
            r.anchoredPosition = new Vector2(0f, 60f);
            _announceScale = r.localScale * 1.8f;
            line.SetActive(false);

            // CONTINUE?'s last line, under the countdown.
            var press = Instantiate(line, canvas);
            press.name = "PRESS COIN";
            _press = press.GetComponent<TMP_Text>();
        }

        // The HUD: SCORE and HI SCORE along the top, the pilot's
        // portrait, health bar, lives and bombs in the bottom left.
        // Canvas units: 1920 across, 2560 top to bottom, (0, 0) the middle.
        // (The ship stays out of it: RetroShmupPlayerController.HudBlocks, HudCeiling.)
        void LayoutHud()
        {
            var canvas = TcWorld.Root ? TcWorld.Root.Find("Canvas") : null;
            if (!canvas) return;
            // SCORE top left and HI SCORE top right, each title over its
            // number, in ENDLESS KNIGHT's digital-clock font, seven digits each.
            TMP_Text T(string name) => canvas.Find(name) ? canvas.Find(name).GetComponent<TMP_Text>() : null;
            var scoreLabel = T("HI SCORE");   // (the jam build's SCORE: label)
            var score = T("PLAYER 1 SCORE");
            var hiLabel = T("HI SCORE LABEL");
            var hi = T("HI SCORE NUMBER");
            if (scoreLabel && score && hiLabel && hi)
            {
                var digital = TcArt.Get ? TcArt.Get.digitalFont : null;
                // (Its own material: full-weight white digits, an aqua glow round them.)
                Material plain = null;
                if (digital)
                {
                    plain = new Material(digital.material) { name = "TC Digital" };
                    plain.SetFloat("_FaceDilate", 0.1f);
                    plain.EnableKeyword("GLOW_ON");
                    plain.SetColor("_GlowColor", new Color(0.2f, 0.95f, 1f, 1f));
                    plain.SetFloat("_GlowOffset", 0.05f);
                    plain.SetFloat("_GlowInner", 0f);
                    plain.SetFloat("_GlowOuter", 0.32f);
                    plain.SetFloat("_GlowPower", 0.85f);
                }
                foreach (var t in new[] { scoreLabel, score, hiLabel, hi })
                {
                    if (digital) { t.font = digital; t.fontSharedMaterial = plain; }
                    t.color = Color.white;   // (white, glowing aqua)
                    t.alignment = TextAlignmentOptions.MidlineLeft;
                    t.textWrappingMode = TextWrappingModes.NoWrap;
                    t.overflowMode = TextOverflowModes.Overflow;
                }
                scoreLabel.text = "SCORE";
                hiLabel.text = "HI SCORE";
                // Each title over its number: SCORE's from the left edge, HI
                // SCORE's from the right edge; as big as fits (up to two and a half times
                // the jam build's) without the two meeting.
                const float edge = 930f, apart = 60f;
                float W(TMP_Text t, string s) => t.GetPreferredValues(s).x;
                float left = Mathf.Max(W(scoreLabel, "SCORE"), W(score, "0000000"));
                float middle = Mathf.Max(W(hiLabel, "HI SCORE"), W(hi, "0000000"));
                float unit = score.transform.localScale.x;
                float k = Mathf.Min(2.5f * unit, (2f * edge - apart) / (left + middle));
                float line = score.GetPreferredValues("0").y * k;
                float y = 1280f - 24f - line * 0.5f;   // (the titles' tops just under the picture's)
                void Place(TMP_Text t, float x, float at, TextAlignmentOptions align)
                {
                    t.alignment = align;
                    t.transform.localScale = Vector3.one * k;
                    var r = (RectTransform)t.transform;
                    float half = r.rect.width * k * 0.5f;
                    r.anchoredPosition = new Vector2(align == TextAlignmentOptions.MidlineLeft ? x + half : align == TextAlignmentOptions.MidlineRight ? x - half : x, at);
                }
                Place(scoreLabel, -edge, y, TextAlignmentOptions.MidlineLeft);
                Place(score, -edge, y - line, TextAlignmentOptions.MidlineLeft);
                Place(hiLabel, edge, y, TextAlignmentOptions.MidlineRight);
                Place(hi, edge, y - line, TextAlignmentOptions.MidlineRight);
                // (The boss's bar stays under them; the ship can fly over them.)
                RetroShmupPlayerController.HudCeiling = TcWorld.Field.center.y + (y - line * 1.5f) / 2560f * TcWorld.Field.height;
                _hudTop = y - line * 1.5f;
            }
            // The pilot, 1.65x, in the bottom left: portrait, health bar beside it,
            // then lives and bombs (PlaceBottomHud).
            foreach (var name in BottomHud)
                if (canvas.Find(name) is RectTransform t) t.localScale *= 1.65f;
            _hudFullScreen = null;
            PlaceBottomHud();
        }

        static readonly string[] BottomHud = { "PLAYER PORTRAIT", "HEALTH BAR", "PLAYER LIFE", "PLAYER LIFE (1)", "PLAYER LIFE (2)", "BOMB 1", "BOMB 2", "BOMB 3" };
        bool? _hudFullScreen;
        /// The Editor's previews: laid out as in full screen.
        public static bool PreviewFullScreen;

        // The bottom left's pieces (canvas units). In AR: lives then bombs in one row along the
        // bottom. Full screen, where the on-screen buttons cover the picture's bottom left: the
        // bombs over the lives, and all of it in from the left and up from the bottom.
        void PlaceBottomHud()
        {
            bool full = PreviewFullScreen || SpatialEmulator.UI.FullScreenTest.Enabled;
            var canvas = TcWorld.Root ? TcWorld.Root.Find("Canvas") : null;
            if (!canvas || _hudFullScreen == full) return;
            _hudFullScreen = full;
            // Top left, under SCORE (in AR and full screen alike - away from the stick and thumbs):
            // the portrait in the corner, its health bar right of it, tops level; under the portrait, from
            // its left edge, the lives, then the bombs under them. (Canvas units; the portrait and bar's
            // half sizes as laid out at 1.65x.)
            const float portraitHalfW = 136f, portraitHalfH = 227f, barHalfW = 57f, barHalfH = 246f, gap = 40f;
            float top = _hudTop - 24f;
            void Put(string name, float x, float y)
            {
                if (canvas.Find(name) is RectTransform t) t.anchoredPosition = new Vector2(x, y);
            }
            float W(string name) => canvas.Find(name) is RectTransform t ? t.rect.width * t.localScale.x : 0f;
            float H(string name) => canvas.Find(name) is RectTransform t ? t.rect.height * t.localScale.y : 0f;
            float portraitX = -960f + portraitHalfW;
            Put("PLAYER PORTRAIT", portraitX, top - portraitHalfH);
            float barX = portraitX + portraitHalfW + gap * 0.75f + barHalfW;
            Put("HEALTH BAR", barX, top - barHalfH);
            float left = -960f + 24f + W("PLAYER LIFE") * 0.5f;   // (the first life's middle)
            float lives = top - portraitHalfH * 2f - 60f - H("PLAYER LIFE") * 0.5f;   // (a gap under the portrait)
            float bombs = lives - H("PLAYER LIFE") * 0.5f - 40f - H("BOMB 1") * 0.5f;
            Put("PLAYER LIFE", left, lives);
            Put("PLAYER LIFE (1)", left + 120f, lives);
            Put("PLAYER LIFE (2)", left + 240f, lives);
            Put("BOMB 1", left, bombs);
            Put("BOMB 2", left + 120f, bombs);
            Put("BOMB 3", left + 240f, bombs);
            // Nothing along the bottom now for the ship to keep off (it flies over the HUD up top).
            RetroShmupPlayerController.HudBlocks = new Rect[0];
        }

        /// Under the scores (canvas units): where the HUD's top-right block starts (LayoutHud).
        float _hudTop = 1280f - 330f;


        /// A line of text in the score's font, centred at `y` (0 bottom, 1 top)
        /// and `size` times the announcements' (Choose Your Fighter).
        public TMP_Text MakeLine(string name, float y, float size)
        {
            if (!_announce) return null;
            var line = Instantiate(_announce.gameObject, _announce.transform.parent);
            line.name = name;
            line.SetActive(true);
            var text = line.GetComponent<TMP_Text>();
            // (The HUD's frame: 2560 canvas units top to bottom of the picture.)
            ((RectTransform)line.transform).anchoredPosition = new Vector2(0f, (y - 0.5f) * 2560f);
            line.transform.localScale = _announceScale * size;
            text.color = Color.white;
            return text;
        }

        /// The countdown's beep (Choose Your Fighter's cursor).
        public void Beep(float pitch)
        {
            if (!BeepSound || !_audio) return;
            _audio.pitch = pitch;
            _audio.PlayOneShot(BeepSound, 0.7f);
        }

        // ---- CONTINUE? ----
        // Out of lives: the game frozen (TimeCrystalGame), the countdown big
        // in the middle, CONTINUE? and PRESS COIN blinking over it - drawn
        // each frame on real time (the game's clock is stopped).
        int _shownCount = -1;

        public void ShowContinue(float secondsLeft)
        {
            if (!_countdown || !_announce || !_press) return;
            if (_announcing != null) { StopCoroutine(_announcing); _announcing = null; }
            int n = Mathf.Max(0, Mathf.CeilToInt(secondsLeft) - 1);
            float into = 1f - Mathf.Repeat(secondsLeft, 1f);   // 0..1 through this second
            if (n != _shownCount)
            {
                _shownCount = n;
                if (BeepSound && _audio) { _audio.pitch = 1f; _audio.PlayOneShot(BeepSound); }
            }
            // Three lines, each 3x what they were, the gaps between them equal:
            //   CONTINUE? / the number / PRESS COIN.
            Vector3 numberScale = _countdownScale * 3f, lineScale = _announceScale * 3f;
            _countdown.gameObject.SetActive(true);
            _countdown.text = n.ToString();
            _countdown.transform.localScale = numberScale * (1f + 0.25f * Mathf.Clamp01(1f - into / 0.2f));
            _countdown.color = new Color(1f, 1f, 1f, 1f);
            _announce.gameObject.SetActive(true);
            _announce.text = "CONTINUE?";
            _announce.transform.localScale = lineScale;
            _announce.color = Color.Lerp(TcFx.Yellow, Color.white, 0.3f);
            _press.gameObject.SetActive(Mathf.Repeat(Time.unscaledTime * 2.5f, 1f) < 0.6f);
            _press.text = "PRESS COIN";
            _press.transform.localScale = lineScale;
            _press.color = _announce.color;
            // Centre to centre: half of each, plus the same gap.
            _countdown.ForceMeshUpdate();
            _announce.ForceMeshUpdate();
            float number = _countdown.textBounds.size.y * numberScale.y;
            float word = _announce.textBounds.size.y * lineScale.y;
            float step = number * 0.5f + word * 0.5f + number * 0.35f;
            const float middle = 120f;
            ((RectTransform)_countdown.transform).anchoredPosition = new Vector2(0f, middle);
            ((RectTransform)_announce.transform).anchoredPosition = new Vector2(0f, middle + step);
            ((RectTransform)_press.transform).anchoredPosition = new Vector2(0f, middle - step);
        }

        public void HideContinue()
        {
            _shownCount = -1;
            if (_countdown) _countdown.gameObject.SetActive(false);
            if (_announce) _announce.gameObject.SetActive(false);
            if (_press) _press.gameObject.SetActive(false);
            // (Back where the countdown and the announcements go.)
            if (_countdown) ((RectTransform)_countdown.transform).anchoredPosition = new Vector2(0f, 150f);
            if (_announce) ((RectTransform)_announce.transform).anchoredPosition = new Vector2(0f, 60f);
        }

        Coroutine _announcing;
        float _importantUntil = -1f;

        /// A word flashed in the middle of the picture (a power-up's name, WARNING).
        /// `important` ones (WARNING, QUANTUM DRIVE, STAGE, DANGER...) can't be
        /// cut off by a lesser one (a power-up's name) while they're up.
        public void Announce(string text, Color color, float seconds = 1.3f, float size = 1f, bool important = false, float lower = 0f)
        {
            if (!_announce) return;
            if (!important && Time.time < _importantUntil) return;
            if (important) _importantUntil = Time.time + seconds;
            if (_announcing != null) StopCoroutine(_announcing);
            _announcing = StartCoroutine(Announcing(text, color, seconds, size, lower));
        }

        Vector2 _announceHome = new Vector2(float.NaN, 0f);

        // `lower`: this far under its usual place (the HUD's canvas units) - the pilot's name, clear of the countdown.
        IEnumerator Announcing(string text, Color color, float seconds, float size, float lower)
        {
            var place = (RectTransform)_announce.transform;
            if (float.IsNaN(_announceHome.x)) _announceHome = place.anchoredPosition;
            place.anchoredPosition = _announceHome + Vector2.down * lower;
            _announce.text = text;
            _announce.gameObject.SetActive(true);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float pop = 1f + 0.4f * Mathf.Clamp01(1f - t / 0.2f);
                _announce.transform.localScale = _announceScale * size * pop;
                var c = Color.Lerp(color, Color.white, Mathf.Repeat(t * 8f, 1f) < 0.5f ? 0.5f : 0f);
                c.a = t < seconds - 0.25f ? 1f : (seconds - t) / 0.25f;
                _announce.color = c;
                yield return null;
            }
            _announce.gameObject.SetActive(false);
        }

        Vector3 _bossAt;

        // ---- The end of a loop: THE TIME CRYSTAL (TcTimeCrystal) ----
        IEnumerator CrystalFinale()
        {
            var f = TcWorld.Field;
            var from = f.Contains((Vector2)_bossAt) ? _bossAt : new Vector3(f.center.x, f.yMax - 1f, TcWorld.Origin.z);
            TcTimeCrystal.Clear();
            Announce("THE TIME CRYSTAL", TcTimeCrystal.LookFor(_loop).color, 2.6f, 2f, important: true);
            var crystal = TcTimeCrystal.Drop(from, Tough(210 + 60 * _loop));
            while (crystal) yield return null;
            // Its diamonds all taken (or gone) and its blast played out.
            float give = Time.time + 8f;
            while ((!TcTimeCrystal.Done || FindObjectsByType<TcGemPull>(FindObjectsSortMode.None).Length > 0) && Time.time < give) yield return null;
            yield return new WaitForSeconds(0.6f);
            // The pilot, from the ship (every crystal): there's more of them out there.
            yield return TcShipSpeech.Say(this, "THERE MUST BE ANOTHER\nTIME CRYSTAL!");
            ClearForWarp();
            _warping = true;
            if (Crystals) Crystals.enabled = false;
            yield return WarpMalfunction();
            // Time running backwards: the warp in reverse, back to stage 1's space (and its music).
            MusicStage = 0;
            if (TcSpaceScroller.Instance) TcSpaceScroller.Instance.Warp(0, reverse: true);
            if (TcPlayerShip.Instance) TcPlayerShip.Instance.BarrelRolls(3);
            yield return new WaitForSeconds(3.2f);
            _warping = false;
            if (Crystals) Crystals.enabled = true;
        }

        IEnumerator WarpMalfunction()
        {
            bool braam = Random.value < 0.5f;
            float length = braam ? 4.6f : 4f;
            Announce("WARP DRIVE\nMALFUNCTION", TcFx.Red, length - 0.2f, 2.2f, important: true);
            if (TcSpaceScroller.Instance) TcSpaceScroller.Instance.Flash(new Color(1f, 0.3f, 0.35f), Mathf.RoundToInt(length / 0.48f), 0.48f);
            TcSfx.Play(braam ? "warning" : "warning2", 1f, 1f);
            yield return new WaitForSeconds(length - 0.3f);
        }

        IEnumerator QuantumDrive()
        {
            bool braam = Random.value < 0.5f;
            float length = braam ? 4.6f : 4f;
            Announce("QUANTUM DRIVE\nACTIVATED", TcFx.Cyan, length - 0.2f, 2.2f, important: true);
            if (TcSpaceScroller.Instance) TcSpaceScroller.Instance.Flash(new Color(0.3f, 0.9f, 1f), Mathf.RoundToInt(length / 0.48f), 0.48f);
            TcSfx.Play(braam ? "warning" : "warning2", 1f, 1f);
            yield return new WaitForSeconds(length - 0.3f);
        }

        // Before a warp: coins and gems still about are taken (scored), power-ups
        // and bomb pickups go - only space and the ship in the jump.
        bool _warping;

        void LateUpdate()
        {
            PlaceBottomHud();   // (FULL on or off mid-game)
            // Through QUANTUM DRIVE and the warp nothing floats about: whatever turns
            // up (a crystal wave still dropping, a late gem) is taken at once.
            if (_warping) ClearForWarp(quiet: true);
        }

        void ClearForWarp(bool quiet = false)
        {
            TcCoin.CollectAll();
            if (!TcWorld.Root) return;
            foreach (var item in TcWorld.Root.GetComponentsInChildren<CollectableItem>())
            {
                if (item.Taken) continue;   // (already scored, on its way out)
                if (ScoreManager.Instance) ScoreManager.Instance.AddScore(item.scoreValue);
                if (!quiet) TcFx.Burst(item.transform.position, TcFx.Yellow, 4, 2f, 0.05f, 0.25f);
                Destroy(item.gameObject);
            }
            foreach (var p in TcWorld.Root.GetComponentsInChildren<TcPowerUp>()) Destroy(p.gameObject);
            foreach (var b in TcWorld.Root.GetComponentsInChildren<TcBombPickup>()) Destroy(b.gameObject);
            if (!quiet) TcDanmaku.ClearAll(false);
        }

        // ---- The enemies ----

        enum Kind { Popcorn, Dart, Fighter, Violet, Gunship, Heavy, Carrier, Wing, Bomber,
            // Stage 2's and 3's.
            Hornet, Lancer, Raptor, Frigate, Scorch, Specter, Cobra, Striker }

        struct Look { public string art, voice; public int health; public float scale, powerUp; public Color color; public int score; public Vector2[] ports; }

        static Vector2[] Ports(params float[] xy)
        {
            var p = new Vector2[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
            return p;
        }

        // Each kind: its art, sounds (TcSfx), toughness, size, colour (its
        // sparks), score, and its chance of leaving a power-up (the big ones
        // mostly do; the gold carriers always).
        static Look Of(Kind k) => k switch
        {
            Kind.Popcorn => new Look { art = "enemy_popcorn", voice = "popcorn", health = 1, scale = 1f, color = TcFx.Green, score = 100, powerUp = 0.02f , ports = Ports(-3f, 26f, 5f, 26f) },
            Kind.Dart => new Look { art = "enemy_dart", voice = "dart", health = 2, scale = 1f, color = TcFx.Pink, score = 200, powerUp = 0.03f , ports = Ports(0f, 20f) },
            Kind.Fighter => new Look { art = "enemy_fighter", voice = "fighter", health = 4, scale = 1f, color = TcFx.Cyan, score = 400, powerUp = 0.05f , ports = Ports(-21f, 6f, 21f, 6f, 0f, 24f) },
            Kind.Violet => new Look { art = "enemy_violet", voice = "violet", health = 6, scale = 1f, color = TcFx.Violet, score = 500, powerUp = 0.08f , ports = Ports(0f, 26f) },
            Kind.Gunship => new Look { art = "enemy_gunship", voice = "gunship", health = 40, scale = 1.5f, color = TcFx.Red, score = 1500, powerUp = 0.6f , ports = Ports(-10f, 25f, 10f, 25f) },
            Kind.Heavy => new Look { art = "enemy_heavy", voice = "heavy", health = 90, scale = 2f, color = TcFx.Cyan, score = 4000, powerUp = 1f , ports = Ports(-8f, 29f, 9f, 29f, 0f, 23f) },
            Kind.Carrier => new Look { art = "enemy_carrier", voice = "carrier", health = 30, scale = 1.3f, color = TcFx.Yellow, score = 2000, powerUp = 1f , ports = Ports(-25f, 8f, 25f, 8f, 0f, 25f) },
            // The medium bomber (Plane 04B, animated): its wing tips, nose, the pods either side of it.
            Kind.Bomber => new Look { art = "bomber", voice = "bomber", health = 65, scale = 1.5f, color = TcFx.Cyan, score = 3000, powerUp = 0.75f, ports = Ports(-48f, -4f, 48f, -4f, 0f, 52f, -16f, 36f, 16f, 36f) },
            // The violet striker (Plane 05B, animated): a second medium bomber - its wing cannons, twin nose, inner pods.
            Kind.Striker => new Look { art = "striker", voice = "bomber", health = 95, scale = 1.45f, color = TcFx.Violet, score = 2800, powerUp = 0.7f, ports = Ports(-34f, 8f, 34f, 8f, 0f, 60f, -12f, 30f, 12f, 30f) },
            // Stage 2: light blue hornets (small, quick), teal lancers, green raptors, the big green frigate.
            Kind.Hornet => new Look { art = "enemy_hornet", voice = "dart", health = 2, scale = 1f, color = TcFx.Cyan, score = 200, powerUp = 0.03f, ports = Ports(-7f, 22f, 7f, 22f) },
            Kind.Lancer => new Look { art = "enemy_lancer", voice = "fighter", health = 5, scale = 1f, color = TcFx.Green, score = 450, powerUp = 0.06f, ports = Ports(0f, 28f, -14f, 4f, 14f, 4f) },
            Kind.Raptor => new Look { art = "enemy_raptor", voice = "violet", health = 7, scale = 1.1f, color = TcFx.Green, score = 600, powerUp = 0.08f, ports = Ports(-9f, 24f, 9f, 24f, 0f, 20f) },
            Kind.Frigate => new Look { art = "enemy_frigate", voice = "gunship", health = 55, scale = 1.7f, color = TcFx.Green, score = 2500, powerUp = 0.7f, ports = Ports(-8f, 22f, 8f, 22f, 0f, 18f, -26f, -6f, 26f, -6f) },
            // Stage 3: red scorchers (tough), purple specters, blue cobras.
            Kind.Scorch => new Look { art = "enemy_scorch", voice = "wing", health = 28, scale = 1.35f, color = TcFx.Red, score = 1400, powerUp = 0.4f, ports = Ports(0f, 24f, -18f, -4f, 18f, -4f) },
            Kind.Specter => new Look { art = "enemy_specter", voice = "violet", health = 6, scale = 1f, color = TcFx.Violet, score = 550, powerUp = 0.08f, ports = Ports(-6f, 20f, 6f, 20f) },
            Kind.Cobra => new Look { art = "enemy_cobra", voice = "dart", health = 3, scale = 1f, color = TcFx.Cyan, score = 300, powerUp = 0.04f, ports = Ports(0f, 22f, -16f, 6f, 16f, 6f) },
            _ => new Look { art = "enemy_wing", voice = "wing", health = 24, scale = 1.3f, color = TcFx.Pink, score = 1200, powerUp = 0.35f , ports = Ports(-10f, 2f, 10f, 2f, 0f, 28f) },
        };

        TcEnemy Spawn(Kind kind, Vector3[] path, float[] waits, float speed, Vector3? start = null, float sway = 0f)
        {
            var look = Of(kind);
            var enemy = TcEnemy.Make(look.art, start ?? path[0], Tough(look.health), look.scale, look.color, look.score);
            enemy.PowerUpChance = look.powerUp;
            enemy.Voice = look.voice;
            enemy.Ports = look.ports;
            if (kind == Kind.Popcorn) enemy.GemChance = 0.15f;
            var mover = enemy.gameObject.AddComponent<TcEnemyPath>();
            mover.Points = path;
            mover.Waits = waits;
            mover.Speed = speed;
            mover.Sway = sway;
            enemy.gameObject.AddComponent<TcEnemyGun>();
            enemy.gameObject.AddComponent<TcOffField>();
            return enemy;
        }

        void Shoot(TcEnemy enemy, IEnumerator pattern)
        {
            if (enemy) enemy.GetComponent<TcEnemyGun>().Run(pattern);
        }

        // ---- The stage ----

        // Stage 1: the first ships.
        IEnumerator Stage1()
        {
            StartCoroutine(Popcorn(true));
            yield return new WaitForSeconds(1.6f);
            yield return Popcorn(false);
            yield return new WaitForSeconds(1.5f);
            yield return DartDive();
            yield return new WaitForSeconds(2f);
            StartCoroutine(Popcorn(true));
            yield return Carrier();
            yield return new WaitForSeconds(2.5f);
            yield return VWave(0.5f);
            yield return new WaitForSeconds(1.8f);
            yield return VWave(0.3f);
            yield return new WaitForSeconds(2.5f);
            StartCoroutine(Rain(18 + 3 * D));
            yield return SideSweep();
            yield return new WaitForSeconds(3f);
            yield return Gunships();
            yield return new WaitForSeconds(2f);
            yield return Carrier();
            yield return new WaitForSeconds(2f);
            yield return Wings();
            yield return new WaitForSeconds(2f);
            yield return Bombers();
            yield return new WaitForSeconds(2.5f);
            StartCoroutine(Popcorn(false));
            yield return Carrier();
            yield return Heavies();
        }

        // Stage 2: hornets crossing in an X, a line of lancers, raptors
        // looping, a hornet storm, frigates, bombers.
        IEnumerator Stage2()
        {
            yield return XCross(Kind.Hornet);
            yield return new WaitForSeconds(1.5f);
            yield return LineHold(Kind.Lancer, 0.8f);
            yield return new WaitForSeconds(0.5f);
            StartCoroutine(Popcorn(false, Kind.Hornet));
            yield return Carrier();
            yield return new WaitForSeconds(2f);
            yield return Loops(Kind.Raptor, true);
            yield return new WaitForSeconds(1.2f);
            yield return Loops(Kind.Raptor, false);
            yield return new WaitForSeconds(2.5f);
            StartCoroutine(Rain(16 + 3 * D, Kind.Hornet));
            yield return SideSweep(Kind.Lancer);
            yield return new WaitForSeconds(3f);
            yield return Gunships(Kind.Frigate);
            yield return new WaitForSeconds(1.5f);
            yield return DartDive(Kind.Hornet);
            yield return new WaitForSeconds(1.5f);
            yield return Carrier();
            yield return new WaitForSeconds(1.5f);
            StartCoroutine(LineHold(Kind.Lancer, 0.58f));
            yield return Strikers();
            yield return new WaitForSeconds(2f);
            StartCoroutine(XCross(Kind.Hornet));
            yield return Heavies(Kind.Frigate);
        }

        // Stage 3: specters from both sides at once, cobras diving and
        // looping, a line of scorchers, then everything together.
        IEnumerator Stage3()
        {
            yield return Pincer(Kind.Specter);
            yield return new WaitForSeconds(1.5f);
            StartCoroutine(Popcorn(true, Kind.Cobra));
            yield return DartDive(Kind.Cobra);
            yield return new WaitForSeconds(2f);
            yield return Loops(Kind.Specter, false);
            yield return new WaitForSeconds(1.5f);
            yield return LineHold(Kind.Scorch, 0.8f);
            yield return new WaitForSeconds(1f);
            yield return Carrier();
            yield return new WaitForSeconds(1.5f);
            StartCoroutine(XCross(Kind.Cobra));
            yield return VWave(0.5f, Kind.Specter);
            yield return new WaitForSeconds(2.5f);
            StartCoroutine(Rain(18 + 3 * D, Kind.Cobra));
            yield return Gunships(Kind.Frigate);
            yield return new WaitForSeconds(1.5f);
            yield return Wings(Kind.Scorch);
            yield return new WaitForSeconds(1.5f);
            yield return Pincer(Kind.Cobra);
            yield return new WaitForSeconds(1.5f);
            StartCoroutine(Carrier());
            yield return Bombers();
            yield return new WaitForSeconds(2f);
            StartCoroutine(Gunships(Kind.Gunship));
            yield return Heavies();
            yield return new WaitForSeconds(1f);
            yield return Strikers();
        }

        IEnumerator WaitForClear(float most)
        {
            for (float t = 0f; t < most && TcEnemy.All.Count > 0; t += Time.deltaTime) yield return null;
        }

        // Small green fighters swooping in from a top corner, dipping and
        // out the far side; each fires once, aimed (three ways later on).
        IEnumerator Popcorn(bool fromLeft, Kind kind = Kind.Popcorn)
        {
            int count = 10 + 2 * D;
            float s = fromLeft ? 1f : -1f;
            for (int i = 0; i < count; i++)
            {
                var path = Curve(At(0.5f - 0.6f * s, 0.9f), At(0.5f, 0.05f), At(0.5f + 0.6f * s, 0.9f), 16);
                var e = Spawn(kind, path, null, 2.4f);
                Shoot(e, SingleShot(e.transform, 0.9f + 0.1f * (i % 3), D >= 2 ? 3 : 1, OrbColor.Green, BulletKind.Needle, 3f));
                yield return new WaitForSeconds(0.3f);
            }
        }

        // Pink darts plunging in columns, firing aimed needles on the way.
        IEnumerator DartDive(Kind kind = Kind.Dart)
        {
            float[] xs = { 0.2f, 0.8f, 0.35f, 0.65f, 0.5f, 0.12f, 0.88f, 0.5f };
            for (int i = 0; i < xs.Length; i++)
            {
                var path = new[] { At(xs[i], 0.62f), At(xs[i], -0.2f) };
                var e = Spawn(kind, path, new[] { 0.5f, 0f }, 4.2f, start: At(xs[i], 1.08f));
                Shoot(e, FanBursts(e.transform, 0.55f, 2, 3, 26f, 0.25f, OrbColor.Purple, BulletKind.Needle, 3.4f));
                yield return new WaitForSeconds(0.38f);
            }
        }

        // A gold carrier crossing slowly, rings of orbs as it goes, a
        // power-up inside it.
        IEnumerator Carrier()
        {
            bool left = Random.value < 0.5f;
            float a = left ? -0.15f : 1.15f, b = left ? 1.15f : -0.15f;
            var path = new[] { At(a, 0.74f), At(b, 0.62f) };
            var e = Spawn(Kind.Carrier, path, null, 1.1f);
            Shoot(e, CarrierPattern(e.transform));
            yield return new WaitForSeconds(2f);
        }

        // Five blue fighters drop in as a V, hold, fan, carry on down.
        IEnumerator VWave(float middle, Kind kind = Kind.Fighter)
        {
            float[] dx = { 0f, -0.13f, 0.13f, -0.26f, 0.26f };
            float[] lag = { 0f, 0.07f, 0.07f, 0.14f, 0.14f };
            for (int i = 0; i < dx.Length; i++)
            {
                float x = Mathf.Clamp(middle + dx[i] + (middle < 0.5f ? 0.2f : 0f), 0.1f, 0.9f);
                var path = new[] { At(x, 0.7f - lag[i]), At(x, -0.2f) };
                var e = Spawn(kind, path, new[] { 1.8f, 0f }, 2.2f, start: At(x, 1.06f + lag[i]));
                Shoot(e, i == 0
                    ? FanBursts(e.transform, 0.8f, 3, 5 + D / 2 * 2, 50f, 0.45f, OrbColor.Blue, BulletKind.Orb, 2.4f)
                    : FanBursts(e.transform, 1.1f, 2, 3, 24f, 0.5f, OrbColor.Blue, BulletKind.Needle, 3f));
            }
            yield return null;
        }

        // Violet ships crossing from alternate sides in a shallow arc,
        // dropping slow curving pairs.
        IEnumerator SideSweep(Kind kind = Kind.Violet)
        {
            int count = 8 + D;
            for (int i = 0; i < count; i++)
            {
                float s = i % 2 == 0 ? 1f : -1f;
                float y = i % 2 == 0 ? 0.84f : 0.68f;
                var path = Curve(At(0.5f - 0.62f * s, y), At(0.5f, y - 0.22f), At(0.5f + 0.62f * s, y), 14);
                var e = Spawn(kind, path, null, 2.2f);
                Shoot(e, CurlPair(e.transform, 0.8f, s));
                yield return new WaitForSeconds(0.5f);
            }
        }

        // Popcorn falling in random columns, each dropping a slow orb.
        IEnumerator Rain(int count, Kind kind = Kind.Popcorn)
        {
            for (int i = 0; i < count; i++)
            {
                float x = Random.Range(0.08f, 0.92f);
                var e = Spawn(kind, new[] { At(x, -0.2f) }, null, 2f, start: At(x, 1.06f));
                Shoot(e, DropShot(e.transform, Random.Range(0.6f, 1.6f)));
                yield return new WaitForSeconds(0.28f);
            }
        }

        // Two red gunships park either side: wide aimed fans and rings.
        IEnumerator Gunships(Kind kind = Kind.Gunship)
        {
            foreach (float x in new[] { 0.25f, 0.75f })
            {
                var path = new[] { At(x, 0.74f), At(x, 1.2f) };
                var e = Spawn(kind, path, new[] { 6f, 0f }, 1.8f, start: At(x, 1.1f), sway: 0.4f);
                Shoot(e, GunshipPattern(e.transform, x < 0.5f ? 1f : -1f));
                yield return new WaitForSeconds(0.5f);
            }
            yield return new WaitForSeconds(5f);
        }

        // Pink wings sweep in and lay down curving curtains.
        IEnumerator Wings(Kind kind = Kind.Wing)
        {
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 0.3f : 0.7f;
                var path = new[] { At(x, 0.78f), At(x, 1.2f) };
                var e = Spawn(kind, path, new[] { 4.5f, 0f }, 2f, start: At(x, 1.08f), sway: 0.6f);
                Shoot(e, CurtainPattern(e.transform, i == 0 ? 1f : -1f));
                yield return new WaitForSeconds(0.4f);
            }
            yield return new WaitForSeconds(4.5f);
        }

        // Ships streaming in from the top corners in turn, crossing in an X
        // through the middle and out the bottom sides, an aimed shot each.
        IEnumerator XCross(Kind kind)
        {
            int count = 12 + 2 * D;
            for (int i = 0; i < count; i++)
            {
                float s = i % 2 == 0 ? 1f : -1f;
                var path = new[] { At(0.5f - 0.6f * s, 0.95f), At(0.5f, 0.62f), At(0.5f + 0.62f * s, 0.25f) };
                var e = Spawn(kind, path, null, 3f);
                Shoot(e, FanBursts(e.transform, 0.6f, 1, D >= 2 ? 3 : 1, 20f, 0f, OrbColor.Blue, BulletKind.Needle, 3.2f));
                yield return new WaitForSeconds(0.32f);
            }
        }

        // A row of five drops in at `y`, holds firing aimed pairs, then
        // peels off to the sides.
        IEnumerator LineHold(Kind kind, float y)
        {
            for (int i = 0; i < 5; i++)
            {
                float x = 0.14f + i * 0.18f;
                float side = x < 0.5f ? -1f : 1f;
                var path = new[] { At(x, y), At(x + side * 0.15f, y - 0.1f), At(x + side * 0.85f, y - 0.25f) };
                var e = Spawn(kind, path, new[] { 3.5f, 0f, 0f }, 2.6f, start: At(x, 1.08f));
                Shoot(e, FanBursts(e.transform, 0.9f + 0.15f * i, 4, 2, 14f, 0.7f, OrbColor.Purple, BulletKind.Orb, 2.6f));
            }
            yield return new WaitForSeconds(4.5f);
        }

        // A stream that comes in from a top corner, goes once round a loop
        // in the upper middle and leaves by the other corner, curling pairs.
        IEnumerator Loops(Kind kind, bool fromLeft)
        {
            int count = 8 + D;
            float s = fromLeft ? 1f : -1f;
            var points = new List<Vector3> { At(0.5f - 0.65f * s, 0.92f) };
            float start = fromLeft ? Mathf.PI : 0f;
            for (int k = 0; k <= 16; k++)
            {
                float a = start + s * k / 16f * Mathf.PI * 2f;   // (down first, from the side it came in)
                points.Add(At(0.5f + Mathf.Cos(a) * 0.28f, 0.62f + Mathf.Sin(a) * 0.2f));
            }
            points.Add(At(0.5f + 0.65f * s, 0.95f));
            var path = points.ToArray();
            for (int i = 0; i < count; i++)
            {
                var e = Spawn(kind, path, null, 2.8f);
                Shoot(e, CurlPair(e.transform, 1.2f + 0.2f * (i % 3), s));
                yield return new WaitForSeconds(0.34f);
            }
        }

        // Pairs from both sides at once, sweeping down through the middle
        // past each other, red fans.
        IEnumerator Pincer(Kind kind)
        {
            int count = 6 + D / 2;
            for (int i = 0; i < count; i++)
            {
                float y = 0.86f - (i % 3) * 0.08f;
                foreach (float s in new[] { 1f, -1f })
                {
                    var path = Curve(At(0.5f - 0.62f * s, y), At(0.5f, y - 0.3f), At(0.5f + 0.62f * s, y - 0.1f), 14);
                    var e = Spawn(kind, path, null, 2.4f);
                    Shoot(e, FanBursts(e.transform, 0.7f, 2, 3, 30f, 0.5f, OrbColor.Red, BulletKind.Orb, 2.4f));
                }
                yield return new WaitForSeconds(0.6f);
            }
        }

        // Two bombers drop in, hold and pound: missiles from their barrels,
        // fireball fans from their inner ports; then on down.
        IEnumerator Bombers(Kind kind = Kind.Bomber)
        {
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 0.28f : 0.72f;
                var path = new[] { At(x, 0.74f), At(x, -0.3f) };
                var e = Spawn(kind, path, new[] { 6f, 0f }, 1.6f, start: At(x, 1.12f), sway: 0.35f);
                Shoot(e, BomberPattern(e));
                yield return new WaitForSeconds(0.7f);
            }
            yield return new WaitForSeconds(5f);
        }

        // Two green strikers that never sit still: they dart between spots in
        // the upper half (crossing each other), firing as they go, then dive out.
        IEnumerator Strikers()
        {
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? 1f : -1f;
                float L(float x) => 0.5f + (x - 0.5f) * s;   // (mirrored for the second)
                var path = new[]
                {
                    At(L(0.25f), 0.78f), At(L(0.7f), 0.62f), At(L(0.35f), 0.52f), At(L(0.8f), 0.8f),
                    At(L(0.2f), 0.66f), At(L(0.65f), 0.74f), At(L(0.5f), 0.55f), At(L(0.5f), -0.3f),
                };
                var waits = new[] { 0.5f, 0.35f, 0.35f, 0.35f, 0.35f, 0.35f, 0.35f, 0f };
                var e = Spawn(Kind.Striker, path, waits, 3.6f, start: At(L(0.25f), 1.12f), sway: 0.5f);
                Shoot(e, StrikerPattern(e));
                yield return new WaitForSeconds(0.5f);
            }
            yield return new WaitForSeconds(7f);
        }

        IEnumerator StrikerPattern(TcEnemy self)
        {
            var gun = self.GetComponent<TcEnemyGun>();
            yield return new WaitForSeconds(0.8f);
            for (int n = 0; self; n++)
            {
                if (gun.OnScreen)
                {
                    foreach (int port in new[] { 0, 1 })
                    {
                        var from = self.Port(port);
                        TcDanmaku.NWay(from, TcDanmaku.AimAt(from), 3, 18f, BulletSpeed(3.2f), OrbColor.Green, BulletKind.Needle);
                    }
                    if (n % 3 == 2)
                        foreach (int port in new[] { 3, 4 })
                            TcDanmaku.NWay(self.Port(port), port == 3 ? -15f : 15f, 5, 50f, BulletSpeed(2.4f), OrbColor.Green);
                }
                yield return new WaitForSeconds(0.6f);
            }
        }

        IEnumerator BomberPattern(TcEnemy self)
        {
            var path = self.GetComponent<TcEnemyPath>();
            while (!path.Holding) yield return null;
            while (path.Holding)
            {
                foreach (int gun in new[] { 0, 1 })
                {
                    var from = self.Port(gun);
                    TcDanmaku.NWay(from, TcDanmaku.AimAt(from), 3, 20f, BulletSpeed(3f), OrbColor.Purple, BulletKind.Needle);
                }
                yield return new WaitForSeconds(0.6f);
                foreach (int gun in new[] { 3, 4 })
                    TcDanmaku.NWay(self.Port(gun), gun == 3 ? -20f : 20f, 5, 50f, BulletSpeed(2.3f), OrbColor.Purple);
                yield return new WaitForSeconds(0.7f);
            }
        }

        // Two teal heavies hold the top: spirals and rings, the screen full.
        IEnumerator Heavies(Kind kind = Kind.Heavy)
        {
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 0.3f : 0.7f;
                var path = new[] { At(x, 0.72f), At(x, -0.3f) };
                var e = Spawn(kind, path, new[] { 9f, 0f }, 1.5f, start: At(x, 1.1f), sway: 0.3f);
                Shoot(e, HeavyPattern(e.transform, i == 0 ? 1f : -1f));
                yield return new WaitForSeconds(0.8f);
            }
            yield return new WaitForSeconds(6f);
        }

        // ---- The boss ----
        // (Its patterns were thinned out 2026-10-15 - spirals slower and with
        // fewer arms, rings and fans fewer - so there are gaps to slip through.)

        IEnumerator Boss(int level)
        {
            // WARNING, 3x, all of space flashing red under it, one of Wayne's
            // warning sounds (the braam, 4.7 s, or the countdown, 4 s, at
            // random) once; the boss comes in as it ends.
            bool braam = Random.value < 0.5f;
            float length = braam ? 4.6f : 4f;
            Announce("WARNING!!", TcFx.Red, length - 0.2f, 3f, important: true);
            if (TcSpaceScroller.Instance) TcSpaceScroller.Instance.Flash(new Color(1f, 0.15f, 0.15f), Mathf.RoundToInt(length / 0.48f), 0.48f);
            TcSfx.Play(braam ? "warning" : "warning2", 1f, 1f);
            yield return new WaitForSeconds(length - 1f);
            var boss = TcBoss.MakeBoss(At(0.5f, 1.25f), Mathf.RoundToInt((1100 + 300 * D) * (1f + 0.25f * level)), _font, level);
            var mover = boss.gameObject.AddComponent<TcEnemyPath>();
            mover.Points = new[] { At(0.5f, 0.7f) };
            mover.Waits = new[] { 99999f };
            mover.Speed = 1.2f;
            boss.gameObject.AddComponent<TcEnemyGun>().Run(BossPattern(boss, mover, level));
            // Its health bar is long: a power-up falls in from the top every
            // few seconds through the fight (and more fall from it as it's hurt, TcBoss).
            // Bombs too: one every BossBombEvery seconds (and one at each phase, BossPattern).
            float nextDrop = Time.time + 6f, nextBomb = Time.time + 9f;
            while (boss)
            {
                _bossAt = boss.transform.position;
                if (Time.time >= nextDrop && boss.Health > 0)
                {
                    nextDrop = Time.time + 8f;
                    TcPowerUp.Drop(At(Random.Range(0.15f, 0.85f), 1.03f), TcPowerUp.Any);
                }
                if (Time.time >= nextBomb && boss.Health > 0)
                {
                    nextBomb = Time.time + BossBombEvery;
                    if (TcBombs.Instance) TcBombs.Instance.Drop(At(Random.Range(0.15f, 0.85f), 1.03f));
                }
                yield return null;   // the stage waits for it
            }
        }

        const float BossBombEvery = 12f;

        IEnumerator BossPattern(TcBoss boss, TcEnemyPath mover, int level)
        {
            while (!mover.Holding) yield return null;
            yield return new WaitForSeconds(0.6f);
            int lastPhase = 0;
            while (Alive(boss))
            {
                int phase = boss.Phase;
                if (phase != lastPhase)
                {
                    lastPhase = phase;
                    // Each third gone: a burst, it sways wider and fights harder.
                    mover.Sway = phase == 1 ? 1.6f : 2.4f;
                    TcFx.Explode(boss.transform.position, "explosion_large", 1f, TcFx.Orange, 30);
                    PlayDestroyed(true);
                    if (TcBombs.Instance) TcBombs.Instance.Drop(boss.transform.position + Vector3.down * 1f);
                    Announce(phase == 1 ? "DANGER" : "FINAL FORM", TcFx.Red, 1.2f, important: true);
                    yield return new WaitForSeconds(1f);
                }
                if (level == 1) yield return phase == 0 ? Manta0(boss) : phase == 1 ? Manta1(boss) : Manta2(boss);
                else if (level == 2) yield return phase == 0 ? Titan0(boss) : phase == 1 ? Titan1(boss) : Titan2(boss);
                else if (phase == 0) yield return BossPhase0(boss);
                else if (phase == 1) yield return BossPhase1(boss);
                else yield return BossPhase2(boss);
            }
        }

        // Its guns (TcBoss.Ports): 0, 1 cannon barrels; 2 nose; 3, 4 inner ports; 5, 6 wingtips.
        const int BarrelL = 0, BarrelR = 1, Nose = 2, InnerL = 3, InnerR = 4, WingL = 5, WingR = 6;

        // Missiles from the barrels, half rings of big orbs from the nose,
        // fireball fans from the inner ports.
        IEnumerator BossPhase0(TcBoss boss)
        {
            for (int b = 0; b < 4 && Alive(boss); b++)
            {
                foreach (int gun in new[] { BarrelL, BarrelR })
                {
                    var from = boss.Port(gun);
                    TcDanmaku.NWay(from, TcDanmaku.AimAt(from), 3, 24f, BulletSpeed(3.2f), OrbColor.Red, BulletKind.Missile);
                }
                yield return new WaitForSeconds(0.45f);
            }
            for (int r = 0; r < 3 && Alive(boss); r++)
            {
                TcDanmaku.Ring(boss.Port(Nose), 16 + 2 * D, r * 8f, BulletSpeed(1.6f), OrbColor.Purple, BulletKind.BigOrb);
                yield return new WaitForSeconds(0.7f);
            }
            for (int b = 0; b < 3 && Alive(boss); b++)
            {
                foreach (int gun in new[] { InnerL, InnerR })
                    TcDanmaku.NWay(boss.Port(gun), gun == InnerL ? -18f : 18f, 5, 60f, BulletSpeed(2.6f), OrbColor.Orange, BulletKind.Fireball);
                yield return new WaitForSeconds(0.55f);
            }
            yield return new WaitForSeconds(0.4f);
        }

        // A two-way spiral of orbs from the nose, electric bolts aimed from
        // the wingtips, fireball curtains from the barrels.
        IEnumerator BossPhase1(TcBoss boss)
        {
            float a = 0f;
            for (float t = 0f; t < 4f && Alive(boss); t += 0.13f)
            {
                var from = boss.Port(Nose);
                for (int arm = 0; arm < 4; arm++)
                {
                    TcDanmaku.Fire(from, a + arm * 90f, BulletSpeed(2.2f), OrbColor.Purple);
                    TcDanmaku.Fire(from, -a + arm * 90f + 45f, BulletSpeed(2f), OrbColor.Red);
                }
                a += 9f;
                if (Mathf.Repeat(t, 0.6f) < 0.13f)
                    foreach (int gun in new[] { WingL, WingR })
                    {
                        var w = boss.Port(gun);
                        TcDanmaku.Fire(w, TcDanmaku.AimAt(w), BulletSpeed(2.8f), OrbColor.Blue, BulletKind.Bolt, anyWay: true);
                    }
                yield return new WaitForSeconds(0.13f);
            }
            for (int b = 0; b < 3 && Alive(boss); b++)
            {
                foreach (int gun in new[] { BarrelL, BarrelR })
                    TcDanmaku.NWay(boss.Port(gun), 0f, 7 + D / 2, 100f, BulletSpeed(2.4f), OrbColor.Orange, BulletKind.Fireball);
                yield return new WaitForSeconds(0.6f);
            }
            yield return new WaitForSeconds(0.4f);
        }

        // Everything: a six-arm curling spiral from the nose, lightning from
        // the barrels, missile fans from the wingtips, speeding kunai rings.
        IEnumerator BossPhase2(TcBoss boss)
        {
            float a = 0f;
            for (float t = 0f; t < 5f && Alive(boss); t += 0.12f)
            {
                var from = boss.Port(Nose);
                for (int arm = 0; arm < 4; arm++)
                    TcDanmaku.Fire(from, a + arm * 60f, BulletSpeed(1.8f), arm % 2 == 0 ? OrbColor.Purple : OrbColor.Red, BulletKind.Orb, arm % 2 == 0 ? 22f : -22f);
                a += 7f;
                if (Mathf.Repeat(t, 0.8f) < 0.12f)
                    foreach (int gun in new[] { BarrelL, BarrelR })
                    {
                        var g = boss.Port(gun);
                        TcDanmaku.NWay(g, TcDanmaku.AimAt(g), 3, 18f, BulletSpeed(2.8f), OrbColor.Blue, BulletKind.Lightning);
                    }
                if (Mathf.Repeat(t + 0.4f, 1.2f) < 0.12f)
                    foreach (int gun in new[] { WingL, WingR })
                        TcDanmaku.NWay(boss.Port(gun), gun == WingL ? -25f : 25f, 4, 40f, BulletSpeed(3f), OrbColor.Red, BulletKind.Missile);
                yield return new WaitForSeconds(0.12f);
            }
            for (int r = 0; r < 3 && Alive(boss); r++)
            {
                TcDanmaku.Ring(boss.Port(Nose), 20, r * 6f, BulletSpeed(0.9f), OrbColor.Yellow, BulletKind.Kunai, 0f, 1.8f);
                yield return new WaitForSeconds(0.45f);
            }
            yield return new WaitForSeconds(0.4f);
        }

        static bool Alive(TcBoss boss) => boss && boss.Health > 0;

        // ---- Boss 2: GOBLIN ELITE ----

        // Kunai fans from its prongs, lightning aimed from its pod, rings from its wingtips.
        IEnumerator Manta0(TcBoss boss)
        {
            for (int b = 0; b < 4 && Alive(boss); b++)
            {
                foreach (int gun in new[] { BarrelL, BarrelR })
                {
                    var from = boss.Port(gun);
                    TcDanmaku.NWay(from, TcDanmaku.AimAt(from), 4, 40f, BulletSpeed(2.8f), OrbColor.Yellow, BulletKind.Kunai);
                }
                yield return new WaitForSeconds(0.5f);
            }
            for (int b = 0; b < 3 && Alive(boss); b++)
            {
                var from = boss.Port(Nose);
                TcDanmaku.NWay(from, TcDanmaku.AimAt(from), 3, 22f, BulletSpeed(3f), OrbColor.Blue, BulletKind.Lightning);
                yield return new WaitForSeconds(0.6f);
            }
            for (int r = 0; r < 2 && Alive(boss); r++)
            {
                foreach (int gun in new[] { WingL, WingR })
                    TcDanmaku.Ring(boss.Port(gun), 10 + 2 * D, r * 12f, BulletSpeed(1.8f), OrbColor.Purple);
                yield return new WaitForSeconds(0.8f);
            }
            yield return new WaitForSeconds(0.4f);
        }

        // A five-petal rose of curling orbs from its pod, bolts from the
        // prongs, fireball fans from the wingtips.
        IEnumerator Manta1(TcBoss boss)
        {
            float a = 0f;
            for (float t = 0f; t < 4f && Alive(boss); t += 0.14f)
            {
                var from = boss.Port(Nose);
                for (int arm = 0; arm < 5; arm++)
                    TcDanmaku.Fire(from, a + arm * 72f, BulletSpeed(2f), arm % 2 == 0 ? OrbColor.Purple : OrbColor.Blue, BulletKind.Orb, 30f);
                a += 11f;
                if (Mathf.Repeat(t, 0.8f) < 0.14f)
                    foreach (int gun in new[] { BarrelL, BarrelR })
                    {
                        var g = boss.Port(gun);
                        TcDanmaku.Fire(g, TcDanmaku.AimAt(g), BulletSpeed(3f), OrbColor.Blue, BulletKind.Bolt, anyWay: true);
                    }
                yield return new WaitForSeconds(0.14f);
            }
            for (int b = 0; b < 3 && Alive(boss); b++)
            {
                foreach (int gun in new[] { WingL, WingR })
                    TcDanmaku.NWay(boss.Port(gun), gun == WingL ? -20f : 20f, 5, 70f, BulletSpeed(2.4f), OrbColor.Orange, BulletKind.Fireball);
                yield return new WaitForSeconds(0.6f);
            }
            yield return new WaitForSeconds(0.4f);
        }

        // Counter-turning spirals of big orbs from its strips, speeding kunai
        // rings from its pod, missiles from the wingtips, then lightning.
        IEnumerator Manta2(TcBoss boss)
        {
            float a = 0f;
            for (float t = 0f; t < 4.5f && Alive(boss); t += 0.16f)
            {
                for (int arm = 0; arm < 3; arm++)
                {
                    TcDanmaku.Fire(boss.Port(InnerL), a + arm * 120f, BulletSpeed(1.7f), OrbColor.Purple, BulletKind.BigOrb);
                    TcDanmaku.Fire(boss.Port(InnerR), -a + arm * 120f, BulletSpeed(1.7f), OrbColor.Blue, BulletKind.BigOrb);
                }
                a += 10f;
                if (Mathf.Repeat(t, 1f) < 0.16f)
                    TcDanmaku.Ring(boss.Port(Nose), 16, t * 20f, BulletSpeed(0.9f), OrbColor.Yellow, BulletKind.Kunai, 0f, 1.6f);
                if (Mathf.Repeat(t + 0.5f, 1.4f) < 0.16f)
                    foreach (int gun in new[] { WingL, WingR })
                    {
                        var g = boss.Port(gun);
                        TcDanmaku.NWay(g, TcDanmaku.AimAt(g), 3, 24f, BulletSpeed(3f), OrbColor.Red, BulletKind.Missile);
                    }
                yield return new WaitForSeconds(0.16f);
            }
            for (int b = 0; b < 3 && Alive(boss); b++)
            {
                var from = boss.Port(Nose);
                TcDanmaku.NWay(from, TcDanmaku.AimAt(from), 4, 50f, BulletSpeed(3f), OrbColor.Blue, BulletKind.Lightning);
                yield return new WaitForSeconds(0.5f);
            }
            yield return new WaitForSeconds(0.4f);
        }

        // ---- Boss 3: STAR PIRATES ----

        // Walls of fireballs from its wing guns, missiles from its nose, big
        // orbs aimed from its engines.
        IEnumerator Titan0(TcBoss boss)
        {
            for (int b = 0; b < 4 && Alive(boss); b++)
            {
                int gun = b % 2 == 0 ? WingL : WingR;
                TcDanmaku.NWay(boss.Port(gun), 0f, 6, 70f, BulletSpeed(2.4f), OrbColor.Orange, BulletKind.Fireball);
                var nose = boss.Port(Nose);
                TcDanmaku.NWay(nose, TcDanmaku.AimAt(nose), 3, 20f, BulletSpeed(3.2f), OrbColor.Red, BulletKind.Missile);
                yield return new WaitForSeconds(0.55f);
            }
            for (int b = 0; b < 3 && Alive(boss); b++)
            {
                foreach (int gun in new[] { InnerL, InnerR })
                {
                    var from = boss.Port(gun);
                    TcDanmaku.NWay(from, TcDanmaku.AimAt(from), 3, 16f, BulletSpeed(1.8f), OrbColor.Red, BulletKind.BigOrb);
                }
                yield return new WaitForSeconds(0.7f);
            }
            yield return new WaitForSeconds(0.3f);
        }

        // An eight-arm spiral from its nose, lightning from its side spikes.
        IEnumerator Titan1(TcBoss boss)
        {
            float a = 0f;
            for (float t = 0f; t < 4.5f && Alive(boss); t += 0.12f)
            {
                var from = boss.Port(Nose);
                for (int arm = 0; arm < 6; arm++)
                    TcDanmaku.Fire(from, a + arm * 45f, BulletSpeed(2.1f), arm % 2 == 0 ? OrbColor.Orange : OrbColor.Red);
                a += 6f;
                if (Mathf.Repeat(t, 0.6f) < 0.12f)
                    foreach (int gun in new[] { BarrelL, BarrelR })
                    {
                        var g = boss.Port(gun);
                        TcDanmaku.Fire(g, TcDanmaku.AimAt(g), BulletSpeed(3.2f), OrbColor.Blue, BulletKind.Lightning, anyWay: true);
                    }
                yield return new WaitForSeconds(0.12f);
            }
            for (int r = 0; r < 3 && Alive(boss); r++)
            {
                TcDanmaku.Ring(boss.Port(Nose), 18 + 2 * D, r * 7f, BulletSpeed(1.6f), OrbColor.Purple, BulletKind.BigOrb);
                yield return new WaitForSeconds(0.65f);
            }
            yield return new WaitForSeconds(0.3f);
        }

        // Everything: twin counter-spirals from its side spikes, speeding
        // kunai rings, missile fans from its wing guns, fireballs aimed from its engines.
        IEnumerator Titan2(TcBoss boss)
        {
            float a = 0f;
            for (float t = 0f; t < 5f && Alive(boss); t += 0.13f)
            {
                for (int arm = 0; arm < 4; arm++)
                {
                    TcDanmaku.Fire(boss.Port(BarrelL), a + arm * 90f, BulletSpeed(1.9f), OrbColor.Orange, BulletKind.Orb, 20f);
                    TcDanmaku.Fire(boss.Port(BarrelR), -a + arm * 90f, BulletSpeed(1.9f), OrbColor.Red, BulletKind.Orb, -20f);
                }
                a += 8f;
                if (Mathf.Repeat(t, 1.1f) < 0.13f)
                    TcDanmaku.Ring(boss.Port(Nose), 20, t * 15f, BulletSpeed(0.9f), OrbColor.Yellow, BulletKind.Kunai, 0f, 1.8f);
                if (Mathf.Repeat(t + 0.5f, 1.3f) < 0.13f)
                    foreach (int gun in new[] { WingL, WingR })
                        TcDanmaku.NWay(boss.Port(gun), gun == WingL ? -25f : 25f, 4, 40f, BulletSpeed(3f), OrbColor.Red, BulletKind.Missile);
                if (Mathf.Repeat(t + 0.2f, 0.9f) < 0.13f)
                    foreach (int gun in new[] { InnerL, InnerR })
                    {
                        var g = boss.Port(gun);
                        TcDanmaku.Fire(g, TcDanmaku.AimAt(g), BulletSpeed(2.8f), OrbColor.Orange, BulletKind.Fireball);
                    }
                yield return new WaitForSeconds(0.13f);
            }
            yield return new WaitForSeconds(0.4f);
        }

        // ---- Firing patterns ----

        IEnumerator SingleShot(Transform self, float delay, int ways, OrbColor color, BulletKind kind, float speed)
        {
            var gun = self.GetComponent<TcEnemyGun>();
            yield return new WaitForSeconds(delay);
            while (!gun.OnScreen) yield return null;
            var from = Muzzle(self);
            TcDanmaku.NWay(from, TcDanmaku.AimAt(from), ways, 24f, BulletSpeed(speed), color, kind);
        }

        IEnumerator DropShot(Transform self, float delay)
        {
            var gun = self.GetComponent<TcEnemyGun>();
            yield return new WaitForSeconds(delay);
            if (gun.OnScreen) TcDanmaku.Fire(Muzzle(self), 0f, BulletSpeed(1.6f), OrbColor.Green);
        }

        IEnumerator FanBursts(Transform self, float delay, int bursts, int ways, float spread, float gap, OrbColor color, BulletKind kind, float speed)
        {
            var gun = self.GetComponent<TcEnemyGun>();
            yield return new WaitForSeconds(delay);
            for (int b = 0; b < bursts; b++)
            {
                if (gun.OnScreen)
                {
                    var from = Muzzle(self);
                    TcDanmaku.NWay(from, TcDanmaku.AimAt(from), ways, spread, BulletSpeed(speed), color, kind);
                }
                yield return new WaitForSeconds(gap);
            }
        }

        IEnumerator CarrierPattern(Transform self)
        {
            var gun = self.GetComponent<TcEnemyGun>();
            float offset = 0f;
            while (true)
            {
                if (gun.OnScreen) TcDanmaku.Ring(Muzzle(self), 12 + 2 * D, offset, BulletSpeed(1.7f), OrbColor.Orange);
                offset += 11f;
                yield return new WaitForSeconds(0.9f);
            }
        }

        IEnumerator CurlPair(Transform self, float delay, float side)
        {
            var gun = self.GetComponent<TcEnemyGun>();
            yield return new WaitForSeconds(delay);
            for (int i = 0; i < 3; i++)
            {
                if (gun.OnScreen)
                {
                    var from = Muzzle(self);
                    TcDanmaku.Fire(from, -25f, BulletSpeed(2.2f), OrbColor.Purple, BulletKind.Orb, -30f * side);
                    TcDanmaku.Fire(from, 25f, BulletSpeed(2.2f), OrbColor.Purple, BulletKind.Orb, 30f * side);
                }
                yield return new WaitForSeconds(0.35f);
            }
        }

        IEnumerator GunshipPattern(Transform self, float side)
        {
            var path = self.GetComponent<TcEnemyPath>();
            while (!path.Holding) yield return null;
            while (path.Holding)
            {
                var from = Muzzle(self, 0.45f);
                TcDanmaku.NWay(from, TcDanmaku.AimAt(from), 7 + D, 70f, BulletSpeed(2.5f), OrbColor.Red);
                yield return new WaitForSeconds(0.5f);
                TcDanmaku.Ring(Muzzle(self, 0.45f), 16, Random.Range(0f, 22f), BulletSpeed(2.2f), OrbColor.Purple, BulletKind.Needle, 12f * side);
                yield return new WaitForSeconds(0.7f);
            }
        }

        IEnumerator CurtainPattern(Transform self, float side)
        {
            var path = self.GetComponent<TcEnemyPath>();
            while (!path.Holding) yield return null;
            float a = -60f;
            while (path.Holding)
            {
                var from = Muzzle(self, 0.35f);
                TcDanmaku.Fire(from, a * side, BulletSpeed(2.3f), OrbColor.Purple, BulletKind.Orb, 45f * side);
                TcDanmaku.Fire(from, (a + 120f) * side, BulletSpeed(2.3f), OrbColor.Blue, BulletKind.Orb, -45f * side);
                a += 14f;
                if (a > 60f) a -= 120f;
                yield return new WaitForSeconds(0.06f);
            }
        }

        IEnumerator HeavyPattern(Transform self, float side)
        {
            var path = self.GetComponent<TcEnemyPath>();
            while (!path.Holding) yield return null;
            float angle = 0f;
            for (float t = 0f; t < 3.5f && path.Holding; t += 0.1f)
            {
                var from = Muzzle(self, 0.6f);
                TcDanmaku.Fire(from, angle, BulletSpeed(2.1f), OrbColor.Blue);
                TcDanmaku.Fire(from, angle + 120f, BulletSpeed(2.1f), OrbColor.Purple);
                TcDanmaku.Fire(from, angle + 240f, BulletSpeed(2.1f), OrbColor.Blue);
                angle += 13f * side;
                yield return new WaitForSeconds(0.1f);
            }
            for (int r = 0; r < 4 && path.Holding; r++)
            {
                TcDanmaku.Ring(Muzzle(self, 0.6f), 20 + 2 * D, r * 6f, BulletSpeed(1.9f), OrbColor.Red);
                yield return new WaitForSeconds(0.65f);
            }
            while (path.Holding)
            {
                var from = Muzzle(self, 0.6f);
                TcDanmaku.NWay(from, TcDanmaku.AimAt(from), 3, 16f, BulletSpeed(1.5f), OrbColor.Red, BulletKind.BigOrb);
                yield return new WaitForSeconds(0.9f);
            }
        }

        // ---- Helpers ----

        static Vector3 At(float x, float y)
        {
            var f = TcWorld.Field;
            return new Vector3(f.xMin + x * f.width, f.yMin + y * f.height, TcWorld.Origin.z);
        }

        /// Where its next shot leaves: its gun ports in turn (TcEnemy.Ports).
        static Vector3 Muzzle(Transform self, float below = 0.25f)
        {
            var e = self ? self.GetComponent<TcEnemy>() : null;
            if (e && e.Ports != null && e.Ports.Length > 0) return e.NextPort();
            return self.position + Vector3.down * below;
        }

        /// A quadratic curve from a through b's pull to c, as `steps` points (the first is where it spawns).
        static Vector3[] Curve(Vector3 a, Vector3 b, Vector3 c, int steps)
        {
            var points = new Vector3[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                points[i] = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c;
            }
            return points;
        }
    }
}
