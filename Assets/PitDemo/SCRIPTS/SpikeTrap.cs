using UnityEngine;

// ENDLESS KNIGHT's spike trap (free trap assets, Trap_Spike.png: 27 frames).
// It sits sunk in the road with only the tips showing (frames 0-6), and
// fires (frames 7-26) when the player comes within triggerDistance; once
// it's back down it re-arms when the player leaves that range, and fires
// again on the way back. The damage zone is the spikes themselves, up to
// the tips of the current frame (tipPixels): a player over the trap whose
// feet are below the tips is hurt - running into it or landing on it -
// at most once every hitCooldown seconds, and knocked up and back,
// blinking. Clear it by jumping high enough. Spawned and moved with the
// road by EndlessKnightDirector (through CollectableSpawnSystem).
public class SpikeTrap : MonoBehaviour
{
    public Sprite[] frames;
    [Tooltip("Height of each frame's tips above the road, in the sheet's pixels.")]
    public int[] tipPixels;
    [Tooltip("Size relative to the characters' pixels (the sheet's spikes are big next to them).")]
    public float scale = 0.55f;
    [Tooltip("Frames the idle loop plays (the tips peeking out).")]
    public int idleFrames = 7;
    public float idleFps = 8f;
    public float fireFps = 22f;
    [Tooltip("Fires when the player is this close (world units, ahead of the trap's centre).")]
    public float triggerDistance = 1f;
    [Tooltip("Half the width of the spikes (43 px in the sheet) plus a little for the player's feet.")]
    public float hitMargin = 0.05f;
    [Tooltip("ARcade: her feet this many of the characters' pixels under its tips still clear them (an easier jump).")]
    public float jumpClearancePixels = 3f;
    public const int SpikesWidthPixels = 43;
    [Tooltip("Health lost per hit (the player has 125).")]
    public int damage = 15;
    [Tooltip("Seconds before the spikes can hurt again.")]
    public float hitCooldown = 0.5f;
    [Tooltip("Knockback: how high and how far back the player is thrown.")]
    public float knockHeight = 0.6f, knockBack = 0.5f;
    [Tooltip("Blood spray played at the player's feet on a hit (Blood FX 2.0, BloodEruption).")]
    public GameObject bloodPrefab;

    /// The road's height and the player's lane depth (set by EndlessKnightDirector).
    public static float GroundY, LaneZ;
    /// ARcade: how far below the road line the knight walks (down on the
    /// sidewalk; PitDemoGame.PlayerDropPixels) - her feet are at GroundY - PlayerDrop.
    public static float PlayerDrop;
    public static float PlayerFeetY => GroundY - PlayerDrop;
    /// World size of one pixel of the characters' art (set by EndlessKnightDirector).
    public static float PixelSize = 0.01f;
    /// Hits dealt by all spike traps (for testing).
    public static int Hits;

    SpriteRenderer _renderer;
    PlayerScriptARIANAClips _player;
    SpriteRenderer _playerBody;
    StatsControl _stats;
    float _fireTime = -1f;
    float _pixel = 0.01f;
    float _lastHit = -99f;
    bool _armed = true;

    void Start()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _player = FindAnyObjectByType<PlayerScriptARIANAClips>();
        var body = _player ? _player.transform.Find("ARIANA") : null;
        _playerBody = body ? body.GetComponent<SpriteRenderer>() : null;
        _stats = FindAnyObjectByType<StatsControl>();
        SpriteShadow.Cast(_renderer);   // ARcade: its shadow on the road as it springs up
        // Sit on the road in the player's lane, at the characters' pixel size
        // (the sprites are 100 px per unit).
        // (ARcade: down on the sidewalk, on the knight's line.)
        transform.position = new Vector3(transform.position.x, PlayerFeetY, LaneZ);
        transform.localScale = Vector3.one * (PixelSize / 0.01f * scale);
        _pixel = PixelSize * scale;
    }

    void Update()
    {
        if (frames == null || frames.Length == 0 || !_player) return;
        float dx = transform.position.x - _player.transform.position.x;
        // Far behind (about 10 s of walking back): CollectableSpawnSystem only hides it.
        if (dx < -30f) { Destroy(gameObject); return; }

        int frame;
        bool inRange = Mathf.Abs(dx) < triggerDistance;
        if (_fireTime < 0f)
        {
            frame = (int)(Time.time * idleFps) % Mathf.Min(idleFrames, frames.Length);
            if (!inRange) _armed = true;
            else if (_armed) { _fireTime = Time.time; _armed = false; }
        }
        else
        {
            frame = idleFrames + (int)((Time.time - _fireTime) * fireFps);
            if (frame >= frames.Length)
            {
                // Back down: idle again; it fires again once the player has
                // left the trigger range and comes back.
                frame = 0;
                _fireTime = -1f;
            }
        }
        _renderer.sprite = frames[frame];

        // The damage zone: the spikes' width, from the road up to this frame's tips.
        float tip = PlayerFeetY + (tipPixels != null && frame < tipPixels.Length ? tipPixels[frame] : 0) * _pixel;
        float halfWidth = SpikesWidthPixels * 0.5f * _pixel + hitMargin;
        if (Time.time - _lastHit >= hitCooldown && Mathf.Abs(dx) < halfWidth && _playerBody
            && _playerBody.bounds.min.y < tip - jumpClearancePixels * _pixel && _stats && _stats.GetCurrentHP() > 0)
        {
            _lastHit = Time.time;
            Hits++;
            // IncreaseHP ignores a change that would go below 0, so the last hit takes what's left.
            _stats.IncreaseHP(-Mathf.Min(damage, _stats.GetCurrentHP()));
            if (bloodPrefab)
            {
                var blood = Instantiate(bloodPrefab, new Vector3(_player.transform.position.x, PlayerFeetY, LaneZ - 0.01f), Quaternion.identity);
                blood.transform.localScale = Vector3.one * (PixelSize / 0.01f);
            }
            _player.Knockback(knockHeight, knockBack);
        }
    }
}
