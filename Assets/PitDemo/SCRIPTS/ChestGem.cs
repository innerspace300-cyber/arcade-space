using UnityEngine;

// A gem from ENDLESS KNIGHT's treasure chest (Diamonds & Gems, 28 px, 9
// spin frames): shot out of the chest by TreasureChest, it arcs up, falls
// and bounces on the road, and moves with the road as it scrolls. The player
// picks it up by touching it (points + the diamond collect sound); it
// blinks out if left too long.
public class ChestGem : MonoBehaviour
{
    public Sprite[] frames;
    [Tooltip("Spin speed; 0 = a still icon, one of the frames at random (fruit in all its colours).")]
    public float fps = 12f;
    [Tooltip("Health restored on pickup (fruit, like the orange slices).")]
    public int healAmount;
    [Tooltip("Share of the mana bar restored on pickup (gems; set by TreasureChest so one chest's gems fill it).")]
    public float manaShare;
    [Tooltip("Size relative to the characters' pixels.")]
    public float scale = 0.45f;
    public float gravity = 6f;
    [Tooltip("Share of the fall speed kept on each bounce.")]
    public float bounce = 0.45f;
    public int scoreValue = 100;
    public AudioClip collectSound;
    public float lifetime = 8f;

    /// Launch velocity, set by TreasureChest before Start.
    [HideInInspector] public Vector2 velocity;

    SpriteRenderer _renderer;
    PlayerScriptARIANAClips _player;
    SpriteRenderer _playerBody;
    float _start, _lastFrames, _phase;
    bool _resting, _landed;

    void Start()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _player = EndlessKnightDirector.Player ? EndlessKnightDirector.Player : FindAnyObjectByType<PlayerScriptARIANAClips>();
        var body = _player ? _player.transform.Find("ARIANA") : null;
        _playerBody = body ? body.GetComponent<SpriteRenderer>() : null;
        transform.localScale = Vector3.one * (SpikeTrap.PixelSize / 0.01f * scale);
        _start = Time.time;
        _phase = Random.value * 10f;
        if (fps <= 0f && frames != null && frames.Length > 0) _renderer.sprite = frames[Random.Range(0, frames.Length)];
        if (_player) _lastFrames = _player.AccumulatedFrames;
    }

    void Update()
    {
        if (!_player) return;
        float age = Time.time - _start;
        if (age > lifetime) { Destroy(gameObject); return; }
        // Blinks for its last two seconds.
        _renderer.enabled = age < lifetime - 2f || ((int)(age * 10f)) % 2 == 0;
        if (fps > 0f && frames != null && frames.Length > 0)
            _renderer.sprite = frames[(int)((Time.time + _phase) * fps) % frames.Length];

        // Moves with the road, plus its own flight.
        float now = _player.AccumulatedFrames;
        var p = transform.position;
        p.x -= (now - _lastFrames) * EndlessKnightDirector.RoadUnitsPerFrame;
        _lastFrames = now;
        if (!_resting)
        {
            velocity.y -= gravity * Time.deltaTime;
            p += (Vector3)(velocity * Time.deltaTime);
            if (p.y <= SpikeTrap.PlayerFeetY && velocity.y < 0f)   // (down on the sidewalk, in line with the knight)
            {
                p.y = SpikeTrap.PlayerFeetY;
                _landed = true;
                velocity = new Vector2(velocity.x * 0.6f, -velocity.y * bounce);
                if (velocity.y < 0.3f) _resting = true;
            }
        }
        transform.position = p;

        // Picked up once it has come down (not on the way out of the chest).
        if (!_landed || !_playerBody) return;
        var b = _playerBody.bounds;
        if (Mathf.Abs(p.x - b.center.x) < b.extents.x * 0.7f + 0.05f && p.y < b.max.y && p.y > b.min.y - 0.1f)
        {
            PitAudio.PlayClip(collectSound, 0.8f);
            SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Tick);   // ARcade: picked up
            if (healAmount > 0)
            {
                FindAnyObjectByType<StatsControl>()?.HealHP(healAmount);
                FruitTracker.Collect(_renderer.sprite);   // the fruit row over the health bar
            }
            if (manaShare > 0f && FindAnyObjectByType<StatsControl>() is StatsControl control && control.playerStats)
                control.AddMP(Mathf.CeilToInt(manaShare * control.playerStats.MaxMP));
            if (scoreValue > 0) { PitScoreManager.Instance?.AddScore(scoreValue); TreasureChest.BonusPoints += scoreValue; }
            Destroy(gameObject);
        }
    }
}
