using UnityEngine;

// A one-shot sprite animation (blood spray, explosion, slime splat): plays
// its frames once at fps, then removes itself. It stays where it started on
// the road, moving with it as the player walks (the world scrolls, not the
// player), and can size itself to the characters' pixels.
public class PitSpriteFx : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 18f;
    [Tooltip("Move with the road (off for effects parented to something that moves already).")]
    public bool lockToRoad = true;
    [Tooltip("If above 0, scale to this many times the characters' pixel size on start.")]
    public float characterScale;
    [Tooltip("Drop onto the road in the player's lane on start.")]
    public bool snapToRoad;

    SpriteRenderer _renderer;
    float _start;
    float _lastFrames;

    void Start()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _start = Time.time;
        if (frames != null && frames.Length > 0) _renderer.sprite = frames[0];
        if (snapToRoad) transform.position = new Vector3(transform.position.x, SpikeTrap.PlayerFeetY, SpikeTrap.LaneZ - 0.02f);
        if (characterScale > 0f) transform.localScale = Vector3.one * (SpikeTrap.PixelSize / 0.01f * characterScale);
        var player = EndlessKnightDirector.Player;
        if (player) _lastFrames = player.AccumulatedFrames;
    }

    void Update()
    {
        int frame = (int)((Time.time - _start) * fps);
        if (frames == null || frame >= frames.Length) { Destroy(gameObject); return; }
        _renderer.sprite = frames[frame];

        var player = EndlessKnightDirector.Player;
        if (lockToRoad && player)
        {
            float now = player.AccumulatedFrames;
            transform.position += Vector3.left * ((now - _lastFrames) * EndlessKnightDirector.RoadUnitsPerFrame);
            _lastFrames = now;
        }
    }
}
