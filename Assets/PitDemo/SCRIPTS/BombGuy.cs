using UnityEngine;

// ENDLESS KNIGHT's bomb guy (bomb_character sprites): waddles along the road
// toward the player, and when the player comes within fuseDistance he leaps
// at them, homing in, flashing red, and explodes where he lands (explosion.zip + the
// arcade explosion sound). The explosion cloud is the damage zone
// (ExplosionDamage): a player touching it is hurt and knocked back. Spawned and moved with the road by EndlessKnightDirector (through
// CollectableSpawnSystem); his own walk is added on top.
// ARcade: he leaps in a slow, smooth arc. If he comes into her attack's
// reach while an attack (or a Holy Slash) is playing - walking or mid-leap -
// he carries on into it for a moment, then is knocked back in an arc of his
// own and explodes where he lands, away from her. If not, he lands on her.
// ARcade: and run into, he goes off - his body into hers touchDepthPixels
// deep (her art's pixels), walking or mid-leap, unless she's batting him
// away. His leap follows her closely (leapHoming), so outrunning the blast is
// hard: hitting him, or a double jump - over the blast's reach
// (ExplosionDamage.clearHeight) - is the way past.
public class BombGuy : MonoBehaviour
{
    public Sprite[] walkFrames;
    [Tooltip("The red, lit frame shown while the fuse burns.")]
    public Sprite fuseFrame;
    [Tooltip("Size relative to the characters' pixels.")]
    public float scale = 0.65f;
    public float walkFps = 10f;
    [Tooltip("His own walking speed toward the player, world units per second.")]
    public float walkSpeed = 0.3f;
    public float fuseDistance = 1.1f;
    [Tooltip("Seconds in the air: he lands and explodes after this.")]
    public float fuseTime = 0.9f;
    [Tooltip("How high the leap goes, world units.")]
    public float leapHeight = 0.45f;
    public int damage = 20;
    public float knockHeight = 0.6f, knockBack = 0.5f;
    public GameObject explosionPrefab;
    public float explosionScale = 0.8f;
    public AudioClip explosionSound;
    [Range(0f, 1f)] public float explosionVolume = 0.45f;
    [Header("Knocked back")]
    [Tooltip("Seconds he stays in the blow (overlapping her sword) before it sends him off.")]
    public float overlapHold = 0.12f;
    [Tooltip("How far it sends him, world units.")]
    public float knockDistance = 1.6f;
    [Tooltip("Seconds in the air before he lands and goes off.")]
    public float knockTime = 0.8f;
    public float knockLift = 0.55f;

    /// Hits dealt by all bomb guys (for testing).
    public static int Hits;

    SpriteRenderer _renderer;
    PlayerScriptARIANAClips _player;
    float _fuseStart = -1f;
    float _contactAt = -1f;
    float _knockStart = -1f;
    // The leap's and the knock-back's ends, on the road (world x, moved with
    // the road as it scrolls - CollectableSpawnSystem moves him with it
    // between frames, so the drift is how far he's been moved since he was
    // last placed); where she stood as he leapt.
    float _fromX, _toX, _playerAtLeap, _lastSetX;
    bool _placed;
    [Tooltip("How much a leap follows her if she moves while he's in the air (0: lands where she was on the road - run and she gets clear of the blast).")]
    [Range(0f, 1f)] public float leapHoming = 0f;
    [Tooltip("ARcade: he goes off when his body is this far into hers (her art's pixels, across and up).")]
    public float touchDepthPixels = 5f;
    SpriteRenderer _playerArt;

    /// Bomb guys overlapping `area` (an attack's hit box, as it lands) are in the blow.
    public static int HitAll(Bounds area)
    {
        int hit = 0;
        foreach (var bomb in FindObjectsByType<BombGuy>(FindObjectsSortMode.None))
        {
            if (!bomb._renderer || bomb._knockStart >= 0f || !bomb.Overlaps(area)) continue;
            bomb.InTheBlow();
            hit++;
        }
        return hit;
    }

    // His body against `area`, across and up the road (whatever its depth).
    bool Overlaps(Bounds area)
    {
        var body = Drawn(_renderer);
        return !(body.max.x < area.min.x || body.min.x > area.max.x || body.max.y < area.min.y || body.min.y > area.max.y);
    }

    // Where a sprite's pixels are drawn: his frames are 64 px with a lot of
    // clear canvas round him, so not the renderer's bounds but its outline's.
    static Bounds Drawn(SpriteRenderer r)
    {
        var sprite = r.sprite;
        if (!sprite) return r.bounds;
        Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
        foreach (var v in sprite.vertices)
        {
            var world = r.transform.TransformPoint(new Vector3(r.flipX ? -v.x : v.x, r.flipY ? -v.y : v.y, 0f));
            min = Vector3.Min(min, world);
            max = Vector3.Max(max, world);
        }
        var drawn = new Bounds();
        drawn.SetMinMax(min, max);
        return drawn;
    }

    // Into the blow: he carries on for overlapHold, then it knocks him back.
    void InTheBlow()
    {
        if (_contactAt < 0f) _contactAt = Time.time;
    }

    void KnockBack()
    {
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Medium);   // ARcade: batted away
        float dx = transform.position.x - PlayerX();
        float away = Mathf.Abs(dx) > 0.01f ? Mathf.Sign(dx) : (_player && _player.transform.localScale.x < 0f ? -1f : 1f);
        _knockStart = Time.time;
        _fromX = transform.position.x;
        _toX = _fromX + away * knockDistance;
        _knockLiftFrom = transform.position.y - SpikeTrap.PlayerFeetY;
    }

    void Place(float x, float y)
    {
        transform.position = new Vector3(x, y, transform.position.z);
        _lastSetX = x;
        _placed = true;
    }
    float _knockLiftFrom;

    // Where she stands (not her drawn frame, which her animations move about).
    float PlayerX() => _player ? _player.transform.position.x : transform.position.x;

    void Start()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _player = FindAnyObjectByType<PlayerScriptARIANAClips>();
        var art = _player ? _player.transform.Find("ARIANA") : null;
        _playerArt = art ? art.GetComponent<SpriteRenderer>() : null;
        // Down on the sidewalk in line with the knight (her feet), at the characters' pixel size,
        // facing left (the art faces right).
        transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, SpikeTrap.LaneZ - 0.005f);
        transform.localScale = Vector3.one * (SpikeTrap.PixelSize / 0.01f * scale);
        _renderer.flipX = true;
        var shadow = SpriteShadow.Cast(_renderer);   // ARcade: his shadow on the road,
        if (shadow) shadow.raisePixels = 4f;          // right up under his feet
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

    void Update()
    {
        if (!_player || walkFrames == null || walkFrames.Length == 0) return;
        float playerX = PlayerX();
        float dx = transform.position.x - playerX;
        if (dx < -30f) { Destroy(gameObject); return; }
        // The road's scroll since he was last placed carries his arc's ends along.
        if (_placed)
        {
            float drift = transform.position.x - _lastSetX;
            _fromX += drift; _toX += drift;
        }

        if (_knockStart >= 0f)
        {
            // Knocked back: a smooth arc off from her (kept relative to her,
            // as the road scrolls), the fuse flashing fast; boom on landing.
            float t = Mathf.Min(1f, (Time.time - _knockStart) / knockTime);
            _renderer.sprite = ((int)((Time.time - _knockStart) * 16f)) % 2 == 0 && fuseFrame ? fuseFrame : walkFrames[0];
            float x = Mathf.Lerp(_fromX, _toX, 1f - (1f - t) * (1f - t));   // fast off, settling
            float y = Mathf.Lerp(_knockLiftFrom, 0f, t) + Mathf.Sin(Mathf.PI * t) * knockLift;
            Place(x, SpikeTrap.PlayerFeetY + y);
            _renderer.flipX = _toX < _fromX;
            if (t >= 1f) Explode(harmful: false);   // knocked away by her: his blast can't hurt her
            return;
        }

        // Into her attack's reach while it's playing: in the blow.
        if (_player.ActiveAttackArea(out var blow) && Overlaps(blow)) InTheBlow();
        if (_contactAt >= 0f && Time.time - _contactAt >= overlapHold) { KnockBack(); return; }
        // Run into (not being batted away): he goes off on her.
        if (_contactAt < 0f && Touching()) { Explode(harmful: !PlayerBuffs.Shield); return; }

        if (_fuseStart < 0f)
        {
            _renderer.sprite = walkFrames[(int)(Time.time * walkFps) % walkFrames.Length];
            transform.position += Vector3.left * walkSpeed * Time.deltaTime;
            if (Mathf.Abs(dx) < fuseDistance)
            {
                _fuseStart = Time.time;
                _fromX = transform.position.x;
                _toX = playerX;
                _playerAtLeap = playerX;
            }
            return;
        }

        // In the air: a slow, smooth arc to where she stood as he leapt (on
        // the road: she can run clear of it), flashing lit and normal, faster
        // as the fuse burns down; boom on landing.
        float burnt = Mathf.Min(1f, (Time.time - _fuseStart) / fuseTime);
        bool lit = ((int)((Time.time - _fuseStart) * Mathf.Lerp(6f, 16f, burnt))) % 2 == 0;
        _renderer.sprite = lit && fuseFrame ? fuseFrame : walkFrames[0];
        float target = _toX + leapHoming * (playerX - _playerAtLeap);
        Place(Mathf.Lerp(_fromX, target, Smooth(burnt)), SpikeTrap.PlayerFeetY + Mathf.Sin(Mathf.PI * burnt) * leapHeight);
        if (burnt >= 1f) Explode(harmful: !PlayerBuffs.Shield);   // ARcade: shielded, he just goes off
    }

    // His drawn body touchDepthPixels into hers, across and up (over her
    // head in a jump, he's not).
    bool Touching()
    {
        if (!_playerArt || !_playerArt.sprite || _player.IsDead) return false;
        float pixel = _playerArt.transform.lossyScale.x / _playerArt.sprite.pixelsPerUnit;
        var her = Drawn(_playerArt);
        var him = Drawn(_renderer);
        float across = Mathf.Min(her.max.x, him.max.x) - Mathf.Max(her.min.x, him.min.x);
        float up = Mathf.Min(her.max.y, him.max.y) - Mathf.Max(her.min.y, him.min.y);
        float depth = touchDepthPixels * Mathf.Abs(pixel);
        return across >= depth && up >= depth;
    }

    /// Blows up where he stands without hurting anyone (the road is cleared
    /// when the Slime Boss dies).
    public void Detonate() => Explode(harmful: false);

    void Explode(bool harmful = true)
    {
        if (explosionPrefab)
        {
            var boom = Instantiate(explosionPrefab, new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, SpikeTrap.LaneZ - 0.02f), Quaternion.identity);
            boom.transform.localScale = Vector3.one * (SpikeTrap.PixelSize / 0.01f * explosionScale);
            // The cloud is the damage zone.
            if (harmful)
            {
                var blast = boom.AddComponent<ExplosionDamage>();
                blast.damage = damage;
                blast.knockHeight = knockHeight;
                blast.knockBack = knockBack;
            }
        }
        PitAudio.PlayClip(explosionSound, explosionVolume);
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Rigid, harmful ? 1f : 0.5f);   // ARcade: boom
        Destroy(gameObject);
    }
}
