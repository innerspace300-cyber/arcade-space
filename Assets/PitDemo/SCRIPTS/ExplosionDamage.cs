using UnityEngine;

// The bomb guy's blast (added to his EXPLOSION by BombGuy): the explosion
// cloud itself is the damage zone. While the cloud stands on the ground
// (explosion.zip frames 2-15, about 90 x 100 of its 112 px cells), a player
// whose body touches it is hurt once and knocked back. It moves with the
// explosion, which stays put on the road. It reaches clearHeight up the
// cloud: above a single jump, below a double jump - only a double jump clears it.
public class ExplosionDamage : MonoBehaviour
{
    public int damage = 20;
    public float knockHeight = 0.6f, knockBack = 0.5f;
    [Tooltip("Half the cloud's width, in the explosion sheet's pixels.")]
    public float halfWidthPixels = 42f;
    [Tooltip("How high above the road it hurts, world units: a single jump's feet peak at 0.5, a double jump's at 0.85-1.")]
    public float clearHeight = 0.62f;
    [Tooltip("When the cloud is on the ground, seconds after the explosion starts (frames 2-15 at 24 fps).")]
    public float from = 0.08f, until = 0.66f;

    PlayerScriptARIANAClips _player;
    SpriteRenderer _playerBody;
    StatsControl _stats;
    float _start;
    bool _hit;

    void Start()
    {
        _player = EndlessKnightDirector.Player ? EndlessKnightDirector.Player : FindAnyObjectByType<PlayerScriptARIANAClips>();
        var body = _player ? _player.transform.Find("ARIANA") : null;
        _playerBody = body ? body.GetComponent<SpriteRenderer>() : null;
        _stats = FindAnyObjectByType<StatsControl>();
        _start = Time.time;
    }

    void Update()
    {
        float age = Time.time - _start;
        if (_hit || age < from || age > until || !_playerBody || !_stats || _stats.GetCurrentHP() <= 0) return;
        // One sheet pixel in the world (the sheet is 100 px per unit).
        float pixel = transform.lossyScale.y * 0.01f;
        var cloud = new Bounds(
            new Vector3(transform.position.x, transform.position.y + clearHeight * 0.5f, _playerBody.bounds.center.z),
            new Vector3(halfWidthPixels * 2f * pixel, clearHeight, 10f));
        if (!cloud.Intersects(_playerBody.bounds)) return;
        _hit = true;
        BombGuy.Hits++;
        // IncreaseHP ignores a change that would go below 0, so the last hit takes what's left.
        _stats.IncreaseHP(-Mathf.Min(damage, _stats.GetCurrentHP()));
        _player.Knockback(knockHeight, knockBack);
    }
}
