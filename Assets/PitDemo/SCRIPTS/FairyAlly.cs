using System.Collections.Generic;
using UnityEngine;

// ENDLESS KNIGHT's fairy: a helper from every 3rd treasure chest
// (TreasureChest). She rises out of the kicked-open chest and waits there,
// bobbing, for the player to come and collect her (she blinks out if left
// too long); collected, she floats to the player's shoulder, then for
// lifetime seconds shoots ice spikes at the
// Slime Boss and the bomb guys while they're on screen - flying up a
// little and beating her wings faster as she fires. A spike chips the boss
// (a little damage and a push back, EnemyStateMachineSlimeBoss.Chip) and
// sets a bomb guy off where he stands (harmlessly, BombGuy.Detonate). Then
// she blinks out, until the next fairy chest. She's gone the moment the
// player dies.
//
// Each fairy chest brings her in the next colour (her spikes, shot sound and
// hit bursts to match), and tapping her in AR (PitDemoGame.TryTap) changes
// it too.
//
// She hangs from the player's root, not their sprite, which the attack
// animations move about (EndlessKnightBuilder moves her there), and she's
// animated here, not by an Animator, so her look can change. She keeps her
// place (behind the player's right shoulder) and faces the same way when
// the player turns around, which flips the player's root: she's placed
// after the player moves (LateUpdate, late in the frame), unmirrored.
[DefaultExecutionOrder(100)]
public class FairyAlly : MonoBehaviour
{
    [System.Serializable]
    public class Look
    {
        public Sprite[] frames;
        public Sprite[] spikeFrames;
        [Tooltip("Played on each shot.")]
        public AudioClip shotSound;
        [Tooltip("The burst where a spike hits (Blood FX's ClotBurst in the spike's colour).")]
        public Sprite[] impactFrames;
    }

    public Look[] looks;
    public float fps = 10f;
    [Tooltip("Wing beats while shooting.")]
    public float shootingFps = 22f;

    [Header("Visits")]
    [Tooltip("Seconds she stays, from being collected.")]
    public float lifetime = 30f;
    [Tooltip("Seconds she waits over the chest to be collected before she leaves.")]
    public float waitTime = 12f;
    public AudioClip collectSound;
    [Tooltip("She blinks for her last this many seconds, fading out over the last one.")]
    public float blinkTime = 3f;
    [Tooltip("Rising out of the chest: how high (her pixels) and how long; then the flight to the player.")]
    public float risePixels = 28f;
    public float riseTime = 0.9f, flyTime = 1f;
    [Tooltip("As she rises she drifts this far (her pixels) away from the player, who has to walk over to collect her.")]
    public float driftPixels = 45f;
    public AudioClip arriveSound;

    [Header("Ice spikes")]
    public float spikeFps = 15f;
    [Tooltip("Seconds between spikes while a target is on screen: a machine gun.")]
    public float fireInterval = 0.1f;
    [Tooltip("Flight speed, in her pixels per second.")]
    public float spikeSpeed = 420f;
    public int spikeDamage = 1;
    [Tooltip("How far each spike pushes the boss back, in her pixels (it can't walk forward under fire).")]
    public float knockback = 3f;
    [Tooltip("Scatter: each spike's aim is off by up to this many degrees, and it leaves from up to this many of her pixels above or below.")]
    public float spreadDegrees = 4f;
    public float spreadPixels = 3f;
    [Tooltip("How far she flies up (her pixels) while shooting.")]
    public float shootingLiftPixels = 14f;
    [Tooltip("Sorting layer and order: over the Slime Boss (on Default), under the Holy Slash.")]
    public string spikeSortingLayer = "Default";
    public int spikeSortingOrder = 150;
    public float impactFps = 24f;
    [Tooltip("Played when a spike hits the boss (one at random).")]
    public AudioClip[] hitSounds;
    [Range(0f, 1f)] public float shotVolume = 0.35f;
    [Range(0f, 1f)] public float hitVolume = 0.5f;
    public AudioClip tapSound;

    // A machine gun's sounds each start by cutting off the oldest still
    // ringing (they run a second or two), rather than piling up.
    const int ShotVoices = 3, HitVoices = 3;
    AudioSource[] _shotVoices, _hitVoices;
    int _nextShotVoice, _nextHitVoice;

    static int s_look, s_visits;

    SpriteRenderer _renderer;
    PlayerScriptARIANAClips _player;
    Vector3 _home;
    enum State { Away, Rising, Waiting, Flying, Helping }
    State _state;
    bool _here => _state != State.Away;
    float _since, _nextFire, _nextLook, _shootingUntil, _lift, _roadFrames;
    Vector3 _riseFrom, _waitAt, _flightFrom, _flightPosition;
    bool _inFlight => _state != State.Helping;
    SpriteRenderer _playerBody;

    /// Over the chest, not yet collected (EndlessKnightDirector holds new
    /// spawns meanwhile, as for gems).
    public bool Waiting => _state == State.Rising || _state == State.Waiting;
    EnemyStateMachineSlimeBoss _boss;
    readonly List<BombGuy> _bombs = new List<BombGuy>();
    readonly HashSet<BombGuy> _shotAt = new HashSet<BombGuy>();

    public static FairyAlly Current { get; private set; }

    /// Out and about (she can be tapped).
    public bool Visible => _here && _renderer.enabled;

    void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        var animator = GetComponent<Animator>();
        if (animator) animator.enabled = false;
        Current = this;
        _home = transform.localPosition;
        _shotVoices = Voices(ShotVoices);
        _hitVoices = Voices(HitVoices);
        Leave();
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void Start()
    {
        _player = GetComponentInParent<PlayerScriptARIANAClips>();
        var body = _player ? _player.transform.Find("ARIANA") : null;
        _playerBody = body ? body.GetComponent<SpriteRenderer>() : null;
        // ARcade: her shadow on the road below her, fainter the higher she flies.
        var cast = SpriteShadow.Cast(_renderer);
        if (cast) cast.fadeHeight = 2f;
    }

    // Blinking out: on half the time, then less and less over the last second.
    void Blink(float left)
    {
        _renderer.enabled = left >= blinkTime || Mathf.Repeat(Time.time * 10f, 1f) < 0.5f * Mathf.Clamp01(left);
    }

    // Where the road has moved her since last frame (while she's over it).
    void RideRoad(ref Vector3 p)
    {
        if (!_player) return;
        float now = _player.AccumulatedFrames;
        p.x -= (now - _roadFrames) * EndlessKnightDirector.RoadUnitsPerFrame;
        _roadFrames = now;
    }

    bool Reached()
    {
        if (!_playerBody) return false;
        var b = _playerBody.bounds;
        var p = _renderer.bounds.center;
        return Mathf.Abs(p.x - b.center.x) < b.extents.x * 0.7f + 0.08f && p.y < b.max.y + 0.3f;
    }

    /// From a fairy chest, opened at `from`: she rises out of it in the next colour.
    public void Arrive(Vector3 from)
    {
        if (looks != null && looks.Length > 0) s_look = s_visits++ % looks.Length;
        _state = State.Rising;
        _since = Time.time;
        _riseFrom = from;
        _lift = 0f;
        _renderer.enabled = true;
        _flightPosition = from;
        transform.position = from;
        if (_player) _roadFrames = _player.AccumulatedFrames;
        PitAudio.PlayClip(arriveSound, 0.8f);
    }

    void Leave()
    {
        _state = State.Away;
        _renderer.enabled = false;
        transform.localPosition = _home;
        _shotAt.Clear();
    }

    public Bounds Bounds => _renderer ? _renderer.bounds : new Bounds(transform.position, Vector3.zero);

    /// The next colour, in order.
    public void NextLook()
    {
        if (looks == null || looks.Length == 0) return;
        s_look = (s_look + 1) % looks.Length;
        PitAudio.PlayClip(tapSound, 0.8f);
    }

    Look CurrentLook => looks != null && looks.Length > 0 ? looks[s_look % looks.Length] : null;

    // One of her pixels, in the world.
    float Pixel => _renderer && _renderer.sprite ? Mathf.Abs(transform.lossyScale.y) / _renderer.sprite.pixelsPerUnit : 0.01f;

    void Update()
    {
        if (!_here) return;
        if (!_player || _player.IsDead) { Leave(); return; }

        float age = Time.time - _since;
        bool shooting = Time.time < _shootingUntil;
        var look = CurrentLook;
        if (look != null && look.frames != null && look.frames.Length > 0)
            _renderer.sprite = look.frames[(int)(Time.time * (shooting ? shootingFps : fps)) % look.frames.Length];

        switch (_state)
        {
            case State.Rising:   // up out of the chest
            {
                RideRoad(ref _riseFrom);
                float away = _playerBody && _playerBody.bounds.center.x > _riseFrom.x + 0.05f ? -1f : 1f;
                var top = _riseFrom + Vector3.up * (risePixels * Pixel) + Vector3.right * (away * driftPixels * Pixel);
                float k = Mathf.Clamp01(age / riseTime);
                float up = 1f - (1f - k) * (1f - k);
                _flightPosition = new Vector3(Mathf.Lerp(_riseFrom.x, top.x, k), Mathf.Lerp(_riseFrom.y, top.y, up), _riseFrom.z);
                if (k >= 1f) { _state = State.Waiting; _since = Time.time; _waitAt = top; }
                return;
            }
            case State.Waiting:  // bobbing over it, to be collected
            {
                RideRoad(ref _waitAt);
                _flightPosition = _waitAt + Vector3.up * (Mathf.Sin(age * 3f) * 3f * Pixel);
                Blink(waitTime - age);
                if (age > waitTime) { Leave(); return; }
                if (!Reached()) return;
                _renderer.enabled = true;
                PitAudio.PlayClip(collectSound, 0.9f);
                _state = State.Flying;
                _since = Time.time;
                _flightFrom = _flightPosition;
                return;
            }
            case State.Flying:   // over to the player's shoulder
            {
                float k = Mathf.Clamp01(age / flyTime);
                k = k * k * (3f - 2f * k);
                _flightPosition = Vector3.Lerp(_flightFrom, HomeWorld(), k) + Vector3.up * (Mathf.Sin(Mathf.PI * k) * 10f * Pixel);
                if (age >= flyTime) { _state = State.Helping; _since = Time.time; _nextFire = Time.time; }
                return;
            }
        }

        // Helping: lifetime seconds from being collected, blinking out at the end.
        float left = lifetime - flyTime - (Time.time - _since);
        if (left <= 0f) { Leave(); return; }
        Blink(left);

        // Up a little while shooting.
        _lift = Mathf.MoveTowards(_lift, shooting ? shootingLiftPixels : 0f, 60f * Time.deltaTime);

        if (Time.time < _nextFire) return;
        if (Time.time >= _nextLook)
        {
            _nextLook = Time.time + 0.25f;
            _boss = FindAnyObjectByType<EnemyStateMachineSlimeBoss>();
            _bombs.Clear();
            _bombs.AddRange(FindObjectsByType<BombGuy>(FindObjectsSortMode.None));
            _shotAt.RemoveWhere(b => !b);
        }

        // The nearest thing on screen to shoot: the boss, or a bomb guy
        // (one spike each is enough).
        var from = _renderer.bounds.center;
        float best = float.MaxValue;
        EnemyStateMachineSlimeBoss boss = null;
        BombGuy bomb = null;
        if (_boss && !_boss.IsDead && _boss.Body && OnScreen(_boss.Body.bounds))
        {
            best = Mathf.Abs(_boss.Body.bounds.center.x - from.x);
            boss = _boss;
        }
        foreach (var b in _bombs)
        {
            if (!b || _shotAt.Contains(b) || !b.TryGetComponent<SpriteRenderer>(out var sr) || !OnScreen(sr.bounds)) continue;
            float d = Mathf.Abs(sr.bounds.center.x - from.x);
            if (d < best) { best = d; bomb = b; boss = null; }
        }
        if (!boss && !bomb) return;
        if (bomb) _shotAt.Add(bomb);
        _nextFire = Time.time + fireInterval;
        _shootingUntil = Time.time + 0.4f;
        Shoot(look, boss, bomb);
    }

    // Her spot by the player as if they faced right, whichever way they face.
    Vector3 HomeWorld()
    {
        var parent = transform.parent;
        if (!parent) return _home;
        var scale = parent.lossyScale;
        return parent.position + parent.rotation * Vector3.Scale(_home, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
    }

    void LateUpdate()
    {
        if (!_here) return;
        // Unmirrored: her own x scale cancels the player's flip.
        var parent = transform.parent;
        if (parent)
        {
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * Mathf.Sign(parent.lossyScale.x);
            transform.localScale = s;
        }
        transform.position = _inFlight ? _flightPosition : HomeWorld() + Vector3.up * (_lift * Pixel);
    }

    static bool OnScreen(Bounds b)
    {
        var cam = SpatialEmulator.Demo.PitDemoGame.ActorsCamera;
        if (!cam) cam = Camera.main;
        if (!cam) return true;
        float halfWidth = cam.orthographic ? cam.orthographicSize * cam.aspect : float.MaxValue;
        return b.max.x > cam.transform.position.x - halfWidth && b.min.x < cam.transform.position.x + halfWidth;
    }

    void Shoot(Look look, EnemyStateMachineSlimeBoss boss, BombGuy bomb)
    {
        if (look == null || look.spikeFrames == null || look.spikeFrames.Length == 0) return;
        var go = new GameObject("ICE SPIKE");
        go.layer = gameObject.layer;
        go.transform.position = _renderer.bounds.center + Vector3.back * 0.01f
            + Vector3.up * (Random.Range(-spreadPixels, spreadPixels) * Pixel);
        go.transform.localScale = Vector3.one * (Pixel * look.spikeFrames[0].pixelsPerUnit);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = spikeSortingLayer;
        sr.sortingOrder = spikeSortingOrder;
        sr.sprite = look.spikeFrames[0];
        var spike = go.AddComponent<IceSpike>();
        spike.frames = look.spikeFrames;
        spike.fps = spikeFps;
        spike.speed = spikeSpeed * Pixel;
        spike.damage = spikeDamage;
        spike.push = knockback * Pixel;
        spike.boss = boss;
        spike.bomb = bomb;
        spike.fairy = this;
        spike.look = look;
        spike.jitter = Random.Range(-spreadDegrees, spreadDegrees);
        PlayOn(_shotVoices, ref _nextShotVoice, look.shotSound, shotVolume);
    }

    /// A spike broke on its target at `at` (IceSpike): the burst, and on
    /// the boss the ice punch (a bomb has its own bang).
    public void Hit(Look look, Vector3 at, bool sound)
    {
        if (sound && hitSounds != null && hitSounds.Length > 0)
            PlayOn(_hitVoices, ref _nextHitVoice, hitSounds[Random.Range(0, hitSounds.Length)], hitVolume);
        if (look == null || look.impactFrames == null || look.impactFrames.Length == 0) return;
        var go = new GameObject("ICE BURST");
        go.layer = gameObject.layer;
        go.transform.position = at + Vector3.back * 0.02f;
        go.transform.localScale = Vector3.one * (Pixel * look.impactFrames[0].pixelsPerUnit);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f));
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = spikeSortingLayer;
        sr.sortingOrder = spikeSortingOrder + 1;
        sr.flipX = Random.value < 0.5f;
        var fx = go.AddComponent<PitSpriteFx>();
        fx.frames = look.impactFrames;
        fx.fps = impactFps;
        fx.lockToRoad = true;   // stays where it hit, on the road's scroll
        sr.sprite = look.impactFrames[0];
    }

    AudioSource[] Voices(int count)
    {
        var voices = new AudioSource[count];
        for (int i = 0; i < count; i++)
        {
            voices[i] = gameObject.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
            voices[i].spatialBlend = 0f;
        }
        return voices;
    }

    static void PlayOn(AudioSource[] voices, ref int next, AudioClip clip, float volume)
    {
        if (!clip || voices == null) return;
        var voice = voices[next];
        next = (next + 1) % voices.Length;
        voice.Stop();
        voice.clip = clip;
        voice.volume = volume;
        voice.pitch = Random.Range(0.94f, 1.06f);
        voice.Play();
    }
}

// One of the fairy's ice spikes: it flies into the middle of its target
// (the Slime Boss or a bomb guy), turning to follow it, and breaks there.
public class IceSpike : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 15f;
    public float speed = 2f;
    public int damage = 1;
    public float push;
    public EnemyStateMachineSlimeBoss boss;
    public BombGuy bomb;
    [Tooltip("Degrees its aim is off by (the fairy's scatter).")]
    public float jitter;
    public FairyAlly fairy;
    public FairyAlly.Look look;

    SpriteRenderer _renderer;
    Vector3 _direction = Vector3.right;
    float _start;

    void Start()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _start = Time.time;
        Steer();
    }

    bool TargetAlive => bomb ? true : boss && !boss.IsDead && boss.Body && boss.Body.sprite;

    // The middle of what's drawn of it.
    Vector3 Middle()
    {
        if (bomb) return bomb.TryGetComponent<SpriteRenderer>(out var sr) ? sr.bounds.center : bomb.transform.position;
        return EnemyStateMachineSlimeBoss.DrawnBounds(boss.Body).center;
    }

    void Steer()
    {
        if (!TargetAlive) return;
        var to = Middle() - transform.position;
        to.z = 0f;
        if (to.sqrMagnitude > 1e-6f) _direction = Quaternion.Euler(0f, 0f, jitter) * to.normalized;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg);
    }

    void Update()
    {
        if (Time.time - _start > 3f) { Destroy(gameObject); return; }
        if (frames != null && frames.Length > 0)
            _renderer.sprite = frames[(int)((Time.time - _start) * fps) % frames.Length];
        Steer();
        transform.position += _direction * speed * Time.deltaTime;

        if (!TargetAlive) return;
        // Reaching the middle, or about to pass it.
        var middle = Middle();
        var to = middle - transform.position;
        to.z = 0f;
        float step = speed * Time.deltaTime;
        if (to.sqrMagnitude > step * step && Vector3.Dot(to, _direction) > 0f) return;
        var at = new Vector3(middle.x, middle.y, transform.position.z);
        if (bomb) bomb.Detonate();
        else boss.Chip(damage, Mathf.Sign(_direction.x) * push);
        if (fairy) fairy.Hit(look, at, sound: !bomb);
        Destroy(gameObject);
    }
}
