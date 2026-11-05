using System.Collections.Generic;
using UnityEngine;

namespace TimeCrystal
{
    // ARcade: THE TIME CRYSTAL's coins. When an enemy goes down, the bullets
    // it still has in the air turn into coins (a boss's death: every bullet
    // on screen, in its colour) - "cute coins" (ARCADE/coin_*.png, a turning
    // strip). Each pops a moment, then is pulled into the player's ship,
    // faster and faster, and is worth Value with a blip as it's taken.
    public class TcCoin : MonoBehaviour
    {
        public const int Value = 25;
        const int Most = 450;
        const float Size = 1.5f;
        const float PopTime = 0.35f;   // (flung out, slowing, before it's pulled in)   // (the art's 8 px coins at 12 picture pixels)

        static readonly List<TcCoin> s_pool = new List<TcCoin>();
        static Transform s_root;

        SpriteRenderer _r;
        TcAnim _anim;
        Vector2 _v;
        float _age;

        /// A coin where a bullet was (gold; the sheet's other colours were too confusing).
        public static void Spawn(Vector3 at, string strip = "coin_gold")
        {
            var coin = Get();
            if (coin) coin.Launch(at, strip, Random.insideUnitCircle * 1.6f + Vector2.up * 0.6f);
        }

        /// `count` coins bursting out of a ship going down, `size` its scale.
        public static void Burst(Vector3 at, int count, float size)
        {
            float s = Mathf.Sqrt(Mathf.Max(1f, size));
            for (int i = 0; i < count; i++)
            {
                var coin = Get();
                if (!coin) return;
                Vector2 v = Random.insideUnitCircle.normalized * Random.Range(2f, 6.5f) * s + Vector2.up * 1f;
                coin.Launch(at + (Vector3)(Random.insideUnitCircle * 0.2f * s), "coin_gold", v);
            }
        }

        static TcCoin Get()
        {
            if (s_root != TcWorld.Root) { s_pool.Clear(); s_root = TcWorld.Root; }
            if (!TcWorld.Root) return null;
            foreach (var c in s_pool) if (c && !c.gameObject.activeSelf) return c;
            if (s_pool.Count >= Most) return null;
            var go = new GameObject("COIN");
            go.SetActive(false);
            go.transform.SetParent(TcWorld.Root, false);
            var coin = go.AddComponent<TcCoin>();
            coin._r = TcArt.Renderer(go, 45);
            coin._anim = go.AddComponent<TcAnim>();
            s_pool.Add(coin);
            return coin;
        }

        void Launch(Vector3 at, string strip, Vector2 velocity)
        {
            transform.position = new Vector3(at.x, at.y, TcWorld.Origin.z);
            transform.localScale = Vector3.one * Size;
            _r.color = Color.white;
            _anim.Play(TcArt.Frames(strip), 10f);
            _anim.Scatter();
            // A pop out first (a burst from a ship: flung further).
            _v = velocity;
            _age = 0f;
            gameObject.SetActive(true);
        }

        void Update()
        {
            _age += Time.deltaTime;
            var ship = TcPlayerShip.Instance;
            var player = ship ? ship.GetComponent<RetroShmupPlayerController>() : null;
            bool pulled = _age > PopTime && player && player.CanAct;
            if (pulled)
            {
                // Sucked in: steering at the ship, faster the longer it's been going.
                Vector2 to = (Vector2)(ship.transform.position - transform.position);
                float speed = Mathf.Min(24f, 4f + 26f * (_age - PopTime));
                _v = Vector2.Lerp(_v, to.normalized * speed, Time.deltaTime * 12f);
                if (to.magnitude < 0.3f + speed * Time.deltaTime) { Take(); return; }
            }
            else
            {
                _v *= 1f - Time.deltaTime * 3f;
                // Nobody to take it (the ship's down): it fades after a while.
                if (_age > 3f) { _r.color = TcFx.WithAlpha(Color.white, Mathf.Clamp01(4f - _age)); if (_age > 4f) gameObject.SetActive(false); }
            }
            transform.position += (Vector3)(_v * Time.deltaTime);
        }

        /// Every coin still out, taken now (before a warp).
        public static void CollectAll()
        {
            foreach (var c in s_pool) if (c && c.gameObject.activeSelf) c.Take();
        }

        void Take()
        {
            if (ScoreManager.Instance) ScoreManager.Instance.AddScore(Value);
            TcSfx.Play("coin", 0.45f, 0.035f);
            TcFx.Burst(transform.position, TcFx.Yellow, 3, 1.5f, 0.04f, 0.2f);
            gameObject.SetActive(false);
        }
    }

    /// ARcade: a boss's gem, pulled into the ship like a gold coin (TcCoin): popped out
    /// first, then sucked in, faster the longer it's been going, and taken on arrival
    /// (scored by its CollectableItem). With the ship down it drifts and fades.
    public class TcGemPull : MonoBehaviour
    {
        const float PopTime = 0.45f;
        Vector2 _v;
        float _age, _pop = PopTime, _flung;

        /// Flung harder (`speed`: out across the picture) and pulled in only after `delay` (the crystal's diamonds).
        public void Scatter(float speed, float delay) { _flung = speed; _pop = delay; }
        CollectableItem _item;
        SpriteRenderer _r;

        void Start()
        {
            _item = GetComponent<CollectableItem>();
            _r = GetComponent<SpriteRenderer>();
            _v = _flung > 0f ? Random.insideUnitCircle.normalized * _flung : Random.insideUnitCircle * Random.Range(1.5f, 3.5f);
        }

        void Update()
        {
            _age += Time.deltaTime;
            var ship = TcPlayerShip.Instance;
            var player = ship ? ship.GetComponent<RetroShmupPlayerController>() : null;
            if (_age > _pop && player && player.CanAct)
            {
                Vector2 to = (Vector2)(ship.transform.position - transform.position);
                float speed = Mathf.Min(22f, 3f + 22f * (_age - _pop));
                _v = Vector2.Lerp(_v, to.normalized * speed, Time.deltaTime * 10f);
                if (to.magnitude < 0.35f + speed * Time.deltaTime)
                {
                    // Its points pop up somewhere in front of the ship (not all on top of each other).
                    float a = Random.Range(15f, 165f) * Mathf.Deg2Rad;
                    if (_item) { _item.popupOffset = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(0.9f, 2.4f); _item.Collect(); }
                    else Destroy(gameObject);
                    TcSfx.Play("coin", 0.55f, 0.02f);
                    enabled = false;
                    return;
                }
            }
            else
            {
                _v *= 1f - Time.deltaTime * 3f;
                _v += Vector2.down * Time.deltaTime * 0.8f;
                // (Never taken - the ship gone: fading out a while after it should have been.)
                float fade = Mathf.Max(4f, _pop + 2f);
                if (_age > fade && _r) { _r.color = TcFx.WithAlpha(Color.white, Mathf.Clamp01(1f - (_age - fade))); if (_age > fade + 1f) Destroy(gameObject); }
            }
            transform.position += (Vector3)(_v * Time.deltaTime);
        }
    }
}
