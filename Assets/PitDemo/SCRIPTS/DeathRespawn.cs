using System.Collections;
using UnityEngine;

// ENDLESS KNIGHT's respawn: when the player dies, the witch (AITHNE, the
// Salamander Witch) arrives to their right in a yellow lightning strike and
// idles for a moment, then casts: a blue lightning strike comes down on the
// player and takes them. Once it has played out the run starts over at the
// campfire with the score back at 0 (RestartRequested: the cabinet's layer
// stack relaunches the demo, which rebuilds everything fresh), where the
// player lands in another blue strike.
public class DeathRespawn : MonoBehaviour
{
    [Tooltip("AITHNE's idle animation frames and speed.")]
    public Sprite[] witchFrames;
    public float witchFps = 10f;
    [Tooltip("Her UpCast (same canvas as the idle frames), the frame she calls the strike on, and its speed.")]
    public Sprite[] castFrames;
    public float castFps = 12f;
    public int castStrikeFrame = 3;
    [Tooltip("AITHNE's size and draw order, copied from her sprite in THE PIT.")]
    public Vector3 witchScale = Vector3.one;
    public string witchSortingLayer = "TopLayer";
    public int witchSortingOrder = -44;
    [Tooltip("How far to the right of the player she appears (world units).")]
    public float witchOffset = 0.7f;
    [Tooltip("Thunder Strikes: blue on the player, yellow bringing the witch.")]
    public PitSpriteFx strike, arriveStrike;
    public AudioClip strikeSound;
    [Tooltip("The strike frame where the bolt reaches the ground.")]
    public int strikeImpactFrame = 7;
    [Tooltip("Back at the campfire: the strike's size (x the witch's pixels), and the frame after which the bolt has played and the player appears.")]
    public float arriveStrikeScale = 1.6f;
    public int arriveRevealFrame = 15;
    [Tooltip("Seconds: death to the witch appearing, her idling before the cast, the end of the strike to the restart.")]
    public float witchDelay = 1f, witchTime = 1.5f, vanishTime = 0.3f;

    /// Set when a run is over; the cabinet's layer stack restarts the demo and clears it.
    public static bool RestartRequested;
    // The next run starts with the player arriving in a strike (only after a death).
    static bool s_arriveByLightning;

    PlayerScriptARIANAClips _player;
    SpriteRenderer _playerSprite;
    bool _started;
    bool _hidden;
    float _bodyOffset;   // the body's centre from the player's position

    void Awake()
    {
        RestartRequested = false;
        _player = GetComponentInChildren<PlayerScriptARIANAClips>(true);
        var body = _player ? _player.transform.Find("ARIANA") : null;
        _playerSprite = body ? body.GetComponent<SpriteRenderer>() : null;
        if (s_arriveByLightning)
        {
            s_arriveByLightning = false;
            // Hidden in Awake, as the demo is made (by Start it has been drawn
            // once), and every frame (LateUpdate) until the strike has played.
            // Switched off, not made clear: her material ignores sprite colour.
            if (_playerSprite) _bodyOffset = _playerSprite.bounds.center.x - _player.transform.position.x;
            _hidden = true;
            HidePlayer();
            StartCoroutine(Arrive());
        }
    }

    void Update()
    {
        if (_started || !_player || !_player.IsDead) return;
        _started = true;
        StartCoroutine(Respawn());
    }

    void LateUpdate() { if (_hidden) HidePlayer(); }

    void HidePlayer() { if (_playerSprite) _playerSprite.enabled = false; }

    // Back at the campfire: a big blue strike comes down on the player's
    // spot, and they appear once its bolt has played.
    IEnumerator Arrive()
    {
        // Let the demo lay itself out first (the director finds the road).
        for (int i = 0; i < 3; i++) yield return null;
        if (!_playerSprite) yield break;
        var at = _player.transform.position;
        var bolt = Strike(strike, new Vector3(at.x + _bodyOffset, SpikeTrap.PlayerFeetY, at.z), arriveStrikeScale);
        yield return new WaitForSeconds(bolt ? arriveRevealFrame / bolt.fps : 0f);
        _hidden = false;
        _playerSprite.enabled = true;
    }

    IEnumerator Respawn()
    {
        yield return new WaitForSeconds(witchDelay);

        // The witch, standing on the road to the player's right, facing them.
        float x = _playerSprite ? _playerSprite.bounds.center.x : _player.transform.position.x;
        float z = _player.transform.position.z;
        var sr = MakeWitch(x + witchOffset);
        // She arrives in a yellow strike.
        var arrival = Strike(arriveStrike, new Vector3(sr.bounds.center.x, SpikeTrap.GroundY, z));
        yield return new WaitForSeconds(Impact(arrival));
        sr.enabled = true;
        for (float t = 0f; t < witchTime; t += Time.deltaTime)
        {
            Idle(sr, t);
            yield return null;
        }

        // She casts: a blue strike takes the player, and plays out.
        PitSpriteFx bolt = null;
        bool struck = false;
        float boltStart = 0f, boltEnd = float.MaxValue;
        float castLength = castFrames != null && castFrames.Length > 0 ? castFrames.Length / castFps : 0f;
        for (float t = 0f; t < castLength || Time.time < boltEnd; t += Time.deltaTime)
        {
            int f = (int)(t * castFps);
            if (t < castLength) sr.sprite = castFrames[Mathf.Min(f, castFrames.Length - 1)];
            else Idle(sr, t - castLength);
            if (!struck && (f >= castStrikeFrame || castLength == 0f))
            {
                float px = _playerSprite ? _playerSprite.bounds.center.x : _player.transform.position.x;
                bolt = Strike(strike, new Vector3(px, SpikeTrap.PlayerFeetY, z));
                struck = true;
                boltStart = Time.time;
                boltEnd = boltStart + (bolt ? bolt.frames.Length / bolt.fps : 0f);
            }
            if (struck && _playerSprite && _playerSprite.enabled && Time.time >= boltStart + Impact(strike))
                _playerSprite.enabled = false;
            yield return null;
        }
        if (_playerSprite) _playerSprite.enabled = false;
        yield return new WaitForSeconds(vanishTime);
        s_arriveByLightning = true;
        // The run's over: its score to the Game Center leaderboard.
        SpatialEmulator.Games.GameCenter.Submit(PitScoreManager.Instance ? PitScoreManager.Instance.GetScore() : 0);
        RestartRequested = true;
    }

    /// The witch (hidden, her idle's first frame) standing on the road at
    /// `x`, facing left - for her arrival in a strike.
    public SpriteRenderer MakeWitch(float x)
    {
        float z = _player.transform.position.z;
        var witch = new GameObject("WITCH (summoned)");
        witch.transform.SetParent(transform, true);
        witch.layer = _playerSprite ? _playerSprite.gameObject.layer : LayerMask.NameToLayer("PitActors");
        witch.transform.position = new Vector3(x, _player.transform.position.y, z);
        witch.transform.localScale = witchScale;
        var sr = witch.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = witchSortingLayer;
        sr.sortingOrder = witchSortingOrder;
        sr.flipX = true;
        sr.enabled = false;
        OnTop(sr);
        if (witchFrames != null && witchFrames.Length > 0) sr.sprite = witchFrames[0];
        // Her frames sit higher in their canvas than the player's: feet on the road.
        witch.transform.position += Vector3.up * (SpikeTrap.GroundY - sr.bounds.min.y);
        SpriteShadow.Cast(sr);   // ARcade: her shadow on the road (shown once she is)
        return sr;
    }

    public void Idle(SpriteRenderer sr, float t)
    {
        if (witchFrames != null && witchFrames.Length > 0)
            sr.sprite = witchFrames[(int)(t * witchFps) % witchFrames.Length];
    }

    /// A strike whose bolt lands at `ground`, drawn at the witch's pixel size.
    public PitSpriteFx Strike(PitSpriteFx prefab, Vector3 ground, float size = 1f)
    {
        PitAudio.PlayClip(strikeSound);
        if (!prefab) return null;
        var fx = Instantiate(prefab, ground + Vector3.back * 0.05f, Quaternion.identity);
        var art = fx.GetComponent<SpriteRenderer>();
        OnTop(art);
        // Over the knight too (she's on the front layer).
        if (art) { art.sortingLayerName = "Default"; art.sortingOrder = 500; }
        fx.transform.localScale = Vector3.one * (transform.lossyScale.x * witchScale.x * size);
        return fx;
    }

    /// Drawn after everything else on the road (a later render queue beats
    /// any sorting order): the Slime Boss, bomb guys, traps and fruit.
    public static void OnTop(SpriteRenderer sr)
    {
        if (!sr) return;
        var material = sr.material;   // its own copy
        material.renderQueue = 3100;
    }

    /// Seconds from a strike starting to its bolt reaching the ground.
    public float Impact(PitSpriteFx fx) => fx ? (strikeImpactFrame + 0.5f) / fx.fps : 0f;
}
