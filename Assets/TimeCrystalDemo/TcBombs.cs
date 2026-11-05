using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TimeCrystal
{
    // ARcade: THE TIME CRYSTAL's bomb. B or C (TcWorld.BombPressed) sets one
    // off, if there's one left: each pilot's own (TcBombFx - missiles, lasers,
    // drones, a shockwave), every enemy on screen destroyed (scored, with
    // their gems) and every enemy bullet gone. The bombs left show as icons under the lives;
    // a game starts with StartBombs. Enemies sometimes drop a bomb pickup
    // (the same bomb on its side, pulsing), worth one more.
    // Built by TimeCrystalBuilder (art and sounds from the ship pack and
    // Pixel Arsenal - placeholders until Wayne picks his own).
    public class TcBombs : MonoBehaviour
    {
        public static TcBombs Instance { get; private set; }

        [Header("Count")]
        public int StartBombs = 3;
        public int MaxBombs = 3;
        /// The HUD's bomb icons, MaxBombs of them, left to right.
        public Image[] Icons;

        [Header("Blast")]
        public GameObject BlastEffect;
        public float BlastScale = 3f;
        public AudioClip BlastSound;
        public float FlashSeconds = 0.6f;
        [Range(0f, 1f)] public float FlashAlpha = 0.85f;

        [Header("Pickup")]
        [Range(0f, 1f)] public float DropChance = 0.08f;
        public Sprite[] PickupFrames;
        public float PickupFrameRate = 10f;
        public float PickupSize = 0.85f;   // world units across (the bomb on its side)
        public float PickupFallSpeed = 1.5f;
        public GameObject PickupEffect;
        public AudioClip PickupSound;

        int _bombs;
        AudioSource _audio;
        SpriteRenderer _flash;

        public int Bombs => _bombs;

        void Awake()
        {
            Instance = this;
            _audio = GetComponent<AudioSource>();
            _bombs = StartBombs;
            ShowIcons();
        }

        // Every bomb ready ahead (art drawn, sounds loaded) a moment after the game starts, so the first press doesn't hitch.
        System.Collections.IEnumerator Start()
        {
            yield return null;
            TcBombFx.Prewarm();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (!TcWorld.BombPressed) return;
            TcWorld.BombPressed = false;
            var player = TcWorld.Root ? TcWorld.Root.GetComponentInChildren<RetroShmupPlayerController>() : null;
            if (_bombs <= 0 || !player || !player.CanAct) return;
            _bombs--;
            ShowIcons();
            SpatialEmulator.Haptics.Rumble(1f, 0.25f, 0.7f);   // (a long rumble as it goes off)
            Blast();
        }

        /// A continue: back to a full start's bombs (or more if it had more).
        public void Refill()
        {
            _bombs = Mathf.Max(_bombs, StartBombs);
            ShowIcons();
        }

        public void Add(int count)
        {
            _bombs = Mathf.Min(MaxBombs, _bombs + count);
            ShowIcons();
        }

        void ShowIcons()
        {
            if (Icons == null) return;
            for (int i = 0; i < Icons.Length; i++)
                if (Icons[i]) Icons[i].enabled = i < _bombs;
        }

        // Each pilot's own bomb (TcBombFx): the same end - every enemy in the
        // picture hit, every bullet gone - each its own way.
        void Blast()
        {
            var player = TcPlayerShip.Instance ? TcPlayerShip.Instance.transform.position : (Vector3)TcWorld.Field.center;
            StartCoroutine(TcBombFx.Run(this, TcPilots.Index, player));
        }

        IEnumerator Flash()
        {
            if (!_flash)
            {
                var go = new GameObject("BOMB FLASH");
                go.transform.SetParent(transform, false);
                go.layer = LayerMask.NameToLayer("TcActors");
                _flash = go.AddComponent<SpriteRenderer>();
                _flash.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                _flash.sortingOrder = 1000;
            }
            var field = TcWorld.Field;
            _flash.transform.position = new Vector3(field.center.x, field.center.y, TcWorld.Origin.z - 1f);
            _flash.transform.localScale = new Vector3(field.width + 1f, field.height + 1f, 1f);
            _flash.enabled = true;
            for (float t = 0f; t < FlashSeconds; t += Time.deltaTime)
            {
                _flash.color = new Color(1f, 1f, 1f, FlashAlpha * (1f - t / FlashSeconds));
                yield return null;
            }
            _flash.enabled = false;
        }

        /// Called as an enemy goes: now and then it leaves a bomb pickup.
        public void MaybeDrop(Vector3 position)
        {
            if (Random.value > DropChance) return;
            Drop(position);
        }

        /// A bomb pickup, for certain (the boss fight hands them out).
        public void Drop(Vector3 position)
        {
            if (PickupFrames == null || PickupFrames.Length == 0) return;
            var go = new GameObject("BOMB PICKUP");
            go.transform.SetParent(TcWorld.Root, true);
            go.transform.position = position;
            go.layer = LayerMask.NameToLayer("TcActors");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PickupFrames[0];
            sr.sortingOrder = 5;
            float across = PickupFrames[0].bounds.size.x;
            if (across > 0f) go.transform.localScale = Vector3.one * (PickupSize / across);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            go.AddComponent<TcBombPickup>().Init(this, sr);
            go.AddComponent<CollectibleMover>().moveSpeed = PickupFallSpeed;
            go.AddComponent<TcOffField>();
        }

        public void PickedUp(Vector3 position)
        {
            Add(1);
            if (PickupEffect) TcWorld.SpawnEffect(PickupEffect, position, Quaternion.identity, 2f);
            TcSfx.Play("pickup_shotgun", 0.55f, 0.05f);   // (a shotgun's cock and shot, as a power-up's)
        }
    }

    /// A bomb pickup: its frames playing, worth a bomb to the player's ship.
    public class TcBombPickup : MonoBehaviour
    {
        TcBombs _bombs;
        SpriteRenderer _renderer;
        float _time;
        bool _taken;

        public void Init(TcBombs bombs, SpriteRenderer renderer)
        {
            _bombs = bombs;
            _renderer = renderer;
        }

        Vector3 _scale;

        void Start() => _scale = transform.localScale;

        void Update()
        {
            if (!_bombs || !_renderer) return;
            _time += Time.deltaTime;
            var frames = _bombs.PickupFrames;
            _renderer.sprite = frames[(int)(_time * _bombs.PickupFrameRate) % frames.Length];
            // A gentle pulse and flicker, so it reads as something to grab.
            transform.localScale = _scale * (1f + 0.12f * Mathf.Sin(_time * 7f));
            _renderer.color = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.6f), 0.5f + 0.5f * Mathf.Sin(_time * 11f));
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_taken || !other.CompareTag("PLAYER")) return;
            _taken = true;
            if (_bombs) _bombs.PickedUp(transform.position);
            Destroy(gameObject);
        }
    }
}
