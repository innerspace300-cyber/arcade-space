using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using UnityEngine.Animations;

// Drop-in replacement for PlayerScriptARIANA that plays AnimationClip assets
// directly via a PlayableGraph — no Animator Controller needed.
// Leave the Animator component's Controller field EMPTY on the player prefab.
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerScriptARIANAClips : MonoBehaviour
{
    // ---------- Animation Clips (drop in the Inspector) ----------
    [Header("Animation Clips")]
    public AnimationClip idleClip;
    public AnimationClip runClip;
    public AnimationClip jumpClip;
    public AnimationClip attackClip;
    public AnimationClip holySlashClip;
    public AnimationClip greatHealClip;
    public AnimationClip hurtClip;
    public AnimationClip deathClip;
    public AnimationClip kickChestClip;
    [Tooltip("ARcade: slower gaits, picked by how far the joystick is pushed (PitDemoGame): a walk, then a run; a full push is runClip (the sprint).")]
    public AnimationClip walkClip;
    public AnimationClip jogClip;
    [Tooltip("ARcade: the C button's buff spells (PlayerBuffs): Sword Buff (CastBuff) and Shield Buff (CastShieldBuff).")]
    public AnimationClip swordBuffClip;
    public AnimationClip shieldBuffClip;
    [Header("Rest (ARcade)")]
    [Tooltip("Standing idle this long with health or mana to get back, REST shows over her head (RestPrompt); C then sits her down (restClip, once) to rest (restingClip, looping) until she's moved.")]
    public float restPromptAfterSeconds = 3f;
    public AnimationClip restClip, restingClip;
    [Tooltip("Resting this long, she falls asleep (sleepingClip, looping).")]
    public float sleepAfterSeconds = 10f;
    public AnimationClip sleepingClip;
    [Tooltip("Health back a second while she rests, and while she sleeps (faster) - up to full; the health bar pulses green (HudJuice).")]
    public float restHealPerSecond = 2f, sleepHealPerSecond = 5f;
    [Tooltip("Mana back a second while she sleeps (the mana bar pulses purple).")]
    public float sleepManaPerSecond = 10f;
    public AudioClip swordBuffSound, shieldBuffSound;
    [Tooltip("ARcade: how long a buff lasts after its cast.")]
    [SerializeField] private float buffSeconds = 30f;
    [Tooltip("ARcade: input sizes up to these are a walk, then a run (PitDemoGame's gait speeds 0.25 / 0.63 / 1).")]
    [SerializeField] private float walkUpTo = 0.4f, jogUpTo = 0.8f;

    [Tooltip("Crossfade duration between clips, in seconds. ~0.08 feels smooth for skeletal sprites, 0 for crisp pixel art.")]
    [Range(0f, 0.5f)] public float clipBlendTime = 0.08f;

    // ---------- Health Bar UI Effects ----------
    [Header("Health Bar — On Hit")]
    [Tooltip("Animation clip that plays on the health bar every time the player takes damage (e.g. a flash/shake FX).")]
    public AnimationClip hitFXClip;
    [Tooltip("Animator on the UI object where the Hit FX clip should play. Leave its Controller field empty.")]
    public Animator hitFXAnimator;
    [Tooltip("Audio clip to play on every hit (optional).")]
    public AudioClip hitFXAudioClip;
    [Tooltip("AudioSource for the hit FX audio. If empty, one is created at runtime.")]
    public AudioSource hitFXAudioSource;

    [Header("Health Bar — On Death")]
    [Tooltip("The HealthBarBorder BROKEN GLASS GameObject (under Portrait > HealthBarBorder). Starts disabled — enabled and animated when health reaches zero.")]
    public GameObject borderBrokenGlassObject;
    [Tooltip("Animation clip to play on the HealthBarBorder BROKEN GLASS object when health reaches zero.")]
    public AnimationClip borderBrokenGlassClip;

    [Tooltip("The HealthBarBreakAnim GameObject. Starts disabled — enabled, then plays the break clip on it.")]
    public GameObject hpBarBreakObject;
    [Tooltip("HP BAR BREAK animation clip — plays once when health reaches zero.")]
    public AnimationClip hpBarBreakClip;

    [Tooltip("The HealthBarBrokenDrip GameObject (single-frame sprite). Starts disabled — enabled after HP BAR BREAK finishes.")]
    public GameObject hpBarBrokenGlassObject;

    [Tooltip("The HealthBarBloodDrip GameObject. Starts disabled — enabled, then plays the blood drip clip in a loop.")]
    public GameObject bloodDripObject;
    [Tooltip("HEALTH BAR BLOOD DRIP looping animation clip — starts after HP BAR BREAK finishes and loops until destroyed.")]
    public AnimationClip bloodDripClip;

    [Tooltip("GLASS SHATTER FX GameObject. Starts disabled — enabled and animated when health reaches zero.")]
    public GameObject glassShatterFXObject;
    [Tooltip("GLASS SHATTER FX animation clip — plays once on death.")]
    public AnimationClip glassShatterFXClip;

    [Tooltip("Audio clip to play when health reaches zero (e.g. glass shatter).")]
    public AudioClip healthBarDeathAudioClip;
    [Tooltip("AudioSource for the death audio. If empty, one is created at runtime.")]
    public AudioSource healthBarDeathAudioSource;

    // ---------- Parallax Background ----------
    [Header("Parallax Background")]
    [Tooltip("The background plane's Animator. Leave its Controller field empty — the clip is driven via PlayableGraph.")]
    public Animator backgroundAnimator;
    [Tooltip("The looping background animation clip (100 frames). The script scrubs through it based on joystick input.")]
    public AnimationClip backgroundClip;
    [Tooltip("Speed multiplier for the background scroll. Higher = background moves faster relative to player.")]
    [SerializeField] private float backgroundScrollSpeed = 1f;
    [Tooltip("Maximum playback speed (normalized time per second) for the background clip regardless of input.")]
    [SerializeField] private float backgroundMaxSpeed = 2f;

    // ---------- Movement Limits ----------
    [Header("Movement Limits")]
    [Tooltip("Enable to clamp the player position on the X axis.")]
    public bool limitX = false;
    [SerializeField] private float minX = -10f;
    [SerializeField] private float maxX = 10f;

    [Tooltip("Enable to clamp the player position on the Y axis.")]
    public bool limitY = false;
    [SerializeField] private float minY = -10f;
    [SerializeField] private float maxY = 10f;

    [Tooltip("Enable to clamp the player position on the Z axis.")]
    public bool limitZ = false;
    [SerializeField] private float minZ = -10f;
    [SerializeField] private float maxZ = 10f;

    /// ARcade: thrown up and back when a trap hurts the player, blinking.
    public void Knockback(float height, float back)
    {
        // Not during the Holy Slash, which plays out whole (the hit still hurts).
        // Not during a spell, which plays out whole (the hit still hurts), nor
        // under the Shield Buff.
        if (isDead || isCasting || PlayerBuffs.Shield || !TakeKnock()) return;
        isJumping = true;
        jumpTimer = 0f;
        jumpSpan = 1f; bounceArc = false;
        jumpMultiplier = height / Mathf.Max(0.01f, jumpHeight);
        startJumpPosition = logicalPosition;
        targetJumpPosition = startJumpPosition + new Vector3(isFacingRight ? -back : back, 0f, 0f);
        // The hurt animation: TriggerHurt skips it while airborne, which the
        // knockback is (the sound and health bar FX come with the damage).
        PlayClipForState(PlayerClipState.Hurt);
        knockedBackAt = Time.time;   // (then the jump's falling frames, JumpFrames)
        StartCoroutine(Blink());
    }

    /// ARcade: in the air on her way down (past the top of her jump) - landing on a foe's head stomps it.
    public bool IsComingDown => isJumping && !isDead && jumpTimer > 0.5f;
    /// ARcade: in the air at all (going up too) - a frog leaping up into her boots is stomped as well (RoadEnemy).
    public bool IsInAir => isJumping && !isDead;

    /// ARcade: bounced up off a foe's head, `height` world units - no hurt, no blink; still moving the way
    /// she's pushing, and a second jump still hers.
    /// ARcade: head bounces since she last stood on the road (RoadEnemy: no more than MaxBounces in a row).
    [System.NonSerialized] public int Bounces;
    public const int MaxBounces = 4;

    public void Bounce(float height)
    {
        if (isDead) return;
        Bounces++;
        isJumping = true;
        jumpTimer = 0f;
        jumpCount = 1;
        jumpSpan = BounceSpan; bounceArc = true;   // (quick and springy: up fast, gravity pulling her back down)
        jumpMultiplier = height / Mathf.Max(0.01f, jumpHeight);
        startJumpPosition = logicalPosition;
        targetJumpPosition = startJumpPosition + new Vector3(PitInput.Move.x * jumpDistance * 0.5f, 0f, 0f);
        int jump = stateToClipIndex != null ? stateToClipIndex[(int)PlayerClipState.Jump] : -1;
        if (jump >= 0 && !isAttacking && !isCasting)
        {
            PlayClipForState(PlayerClipState.Jump);
            clipPlayables[jump].SetTime(JumpLeapFrom);
        }
    }

    // ARcade: shoved back along the ground (a tentacle's slap): `back` world units over `seconds`, slowing as she
    // goes, her feet on the road - no flight, no falling frames (the hurt clip comes with the damage). Blinking.
    public void Pushback(float back, float seconds)
    {
        if (isDead || isCasting || PlayerBuffs.Shield || !TakeKnock()) return;
        _pushStart = Time.time;
        _pushTime = Mathf.Max(0.05f, seconds);
        _pushBack = isFacingRight ? -back : back;
        StartCoroutine(Blink());
    }
    private float _pushStart = -1f, _pushTime, _pushBack;

    // ARcade (Wayne's): one knockback, then none for KnockCooldown seconds - hits still hurt, but she isn't bounced
    // from foe to foe (or held in the hurt clip) with no chance to fight back.
    private const float KnockCooldown = 2f;
    private float _knockAt = -999f;
    private bool KnockImmune => Time.time - _knockAt < KnockCooldown;
    private bool TakeKnock()
    {
        if (KnockImmune) return false;
        _knockAt = Time.time;
        return true;
    }

    // ARcade: the jump clip ("Jump&Fall": crouch and launch, leaping up, in
    // the air falling, landing - then idle frames tacked on its end) held on
    // its falling frames for as long as she's in the air, the landing frame
    // as she comes down: a double jump or a long jump no longer shows the
    // idle frames mid-air. Knocked back, the hurt clip (which ends on an
    // idle frame) gives way to the falling frames too.
    private float knockedBackAt = -1f;
    private const float JumpLeapFrom = 0.5f, JumpFallFrom = 0.7f, JumpFallHold = 0.95f, JumpLandAt = 0.92f;
    private const float KnockHurtSeconds = 0.3f;

    private void JumpFrames()
    {
        if (!isJumping || isDead || stateToClipIndex == null || clipPlayables == null) { knockedBackAt = -1f; return; }
        int jump = stateToClipIndex[(int)PlayerClipState.Jump];
        if (jump < 0) return;
        if (knockedBackAt >= 0f && Time.time - knockedBackAt >= KnockHurtSeconds)
        {
            knockedBackAt = -1f;
            if (currentClipIndex != jump && !isCasting && !isAttacking)
            {
                PlayClipForState(PlayerClipState.Jump);
                clipPlayables[jump].SetTime(JumpFallFrom);
            }
        }
        if (currentClipIndex == jump && jumpTimer < JumpLandAt && clipPlayables[jump].GetTime() > JumpFallHold)
            clipPlayables[jump].SetTime(JumpFallHold);
    }

    private IEnumerator Blink()
    {
        var body = transform.Find("ARIANA");
        var sr = body ? body.GetComponent<SpriteRenderer>() : null;
        if (!sr) yield break;
        for (int i = 0; i < 10; i++)
        {
            sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(0.06f);
        }
        sr.enabled = true;
    }

    /// ARcade: the demo's world is moved away from the room, so its walk
    /// limits (world positions) move with it.
    public void OffsetLimits(Vector3 offset)
    {
        minX += offset.x; maxX += offset.x;
        minY += offset.y; maxY += offset.y;
        minZ += offset.z; maxZ += offset.z;
    }

    /// ARcade: her walk limits side to side (world x).
    public float WalkMinX => minX;
    public float WalkMaxX => maxX;

    /// ARcade: scrolls the road (and the background with it) by `frames` of
    /// it, as her walking does - negative back, as if she'd walked left -
    /// without her moving: a level's standing gate brought to her as she
    /// arrives in it (LevelPortal).
    public void ScrollRoad(float frames)
    {
        float scrollDelta = frames / 100f;   // (ScrubBackground's units: a loop is 100 frames)
        accumulatedFrames += frames;
        netFrames += frames;
        LastScrollDelta = scrollDelta;   // (read this frame by the levels' own backgrounds)
        backgroundNormalizedTime = Mathf.Repeat(backgroundNormalizedTime + scrollDelta, 1f);
        if (backgroundGraph.IsValid() && backgroundClip != null)
        {
            backgroundPlayable.SetTime(backgroundNormalizedTime * backgroundClip.length);
            backgroundGraph.Evaluate();
        }
    }

    // ---------- Player state flags ----------
    private bool isJumping = false;
    private bool isAttacking;
    private bool isUsingAbility;
    private bool isDead = false;
    private bool isHurt = false;
    private bool isKickingChest = false;

    // ---------- Tunables ----------
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Jump")]
    [SerializeField] private float jumpDuration = 1.0f;
    [SerializeField] private float jumpHeight = 5f;
    [SerializeField] private float jumpDistance = 5f;
    [SerializeField] private int maxJumps = 2;
    [SerializeField] private float jumpCooldownTime = 3f;

    [Header("Ability Timings")]
    [SerializeField] private float attackDelay = 0.3f;
    [SerializeField] private float holySlashDelay = 0.3f;
    [SerializeField] private float holySlashColliderEnableDelay = 0.2f;
    [SerializeField] private float greatHealDelay = 0.3f;
    [SerializeField] private float hurtDelay = 0.5f;

    [Header("Damage / Heal")]
    [SerializeField] private int healAmount = 20;
    [SerializeField] private int mpDecreaseAmount = 15;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private int holySlashDamage = 20;
    [Tooltip("ARcade: the Holy Slash's damage to a Slime Boss (they have 300, then 450, 600... health).")]
    [SerializeField] private int holySlashBossDamage = 700;
    [Tooltip("ARcade: mana each Holy Slash drains (the bar holds 50); gems from chests fill it back up.")]
    [SerializeField] private int holySlashManaCost = 15;

    // ---------- Physics / jump state ----------
    private Rigidbody rb;
    private Vector3 startJumpPosition;
    private Vector3 targetJumpPosition;
    private float jumpTimer = 0f;
    // ARcade: a bounce off a foe's head is quicker than a jump (this much of its time) and a true
    // falling arc (a parabola: fast off the head, hanging a moment, gravity bringing her down).
    const float BounceSpan = 0.6f;
    private float jumpSpan = 1f;
    private bool bounceArc;
    private int lastHealth;
    private int jumpCount = 0;
    private bool isJumpOnCooldown = false;
    private float jumpCooldownTimer = 0f;
    private float jumpMultiplier = 1f;
    private float initialYPosition;

    // ---------- External refs ----------
    [Header("Input / External")]
    // ARcade: the touch joystick is replaced by PitInput.Move (ARcade controls).
    public StatsControl statsControl;
    public PlayerStats playerStats;

    // ---------- Attack colliders ----------
    [Header("Attack Colliders")]
    [SerializeField] private BoxCollider attackCollider;
    [SerializeField] private Vector3 colliderOffset = new Vector3(1.0f, 0.0f, 0.0f);
    [SerializeField] private BoxCollider holySlashCollider;
    [SerializeField] private Vector3 holySlashColliderOffset = new Vector3(1.5f, 0.0f, 0.0f);

    // ---------- Audio ----------
    [Header("Audio")]
    [SerializeField] private AudioSource healSound1;
    [SerializeField] private AudioSource healSound2;
    [SerializeField] private AudioSource attackSound;
    [SerializeField] private AudioSource holySlashSound;
    [SerializeField] private AudioSource jumpSound;
    [SerializeField] private AudioSource hurtSound;
    [SerializeField] private AudioSource deathSound;

    // ---------- Rendering ----------
    [Header("Rendering")]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    // ---------- Animator ----------
    [Header("Animator")]
    [Tooltip("Required as PlayableGraph output target. Leave Controller field empty.")]
    public Animator animator;

    private bool isFacingRight = true;
    /// ARcade: the way she's facing (the cat demons face it, with nothing to shoot).
    public bool FacingRight => isFacingRight;
    private bool playFirstHealSound = true;

    // ---------- Playable graph plumbing ----------
    private enum PlayerClipState { Idle, Run, Jump, Attack, HolySlash, GreatHeal, Hurt, Death, KickChest, Walk, Jog, SwordBuff, ShieldBuff, Rest, Resting, Sleeping }

    private PlayableGraph playableGraph;
    private AnimationMixerPlayable mixer;
    private AnimationClipPlayable[] clipPlayables;
    private int currentClipIndex = -1;
    private Coroutine blendRoutine;
    private Coroutine kickChestRoutine;

    // Maps each PlayerClipState (by int index) to the actual clipPlayables[] slot.
    // -1 means the clip was null and has no slot in the mixer.
    // This ensures the mixer ONLY contains non-null clips — no unconnected ports.
    private int[] stateToClipIndex;

    // Separate PlayableGraphs for each health bar UI effect
    private PlayableGraph hitFXGraph;
    private PlayableGraph borderBrokenGlassGraph;
    private PlayableGraph hpBarBreakGraph;
    private PlayableGraph bloodDripGraph;
    private PlayableGraph glassShatterFXGraph;

    // Background parallax graph — manually scrubbed, not auto-playing
    private PlayableGraph backgroundGraph;
    private AnimationClipPlayable backgroundPlayable;
    private float backgroundNormalizedTime = 0f; // 0..1 wrapping

    // Parallax frame counters — read by ParallaxSpawnSystem
    // accumulatedFrames: directional net counter (forward adds, backward subtracts)
    // netFrames: unclamped directional — positive = rightward, negative = leftward
    private float accumulatedFrames = 0f;
    private float netFrames        = 0f;

    /// <summary>Net parallax frames moving forward (adds going right, subtracts going left, min 0).
    /// Used by ParallaxSpawnSystem for spawn thresholds.</summary>
    public float AccumulatedFrames => accumulatedFrames;
    /// <summary>Net parallax frames (positive = rightward, negative = leftward).</summary>
    public float NetFrames => netFrames;
    /// <summary>Raw horizontal joystick input this frame (-1..1).</summary>
    public float CurrentHorizontalInput => lastInputX;
    /// <summary>Last computed parallax scroll delta (normalized, before wrapping). Zero when idle.</summary>
    public float LastScrollDelta { get; private set; }
    /// <summary>Player move speed — used by ParallaxSpawnSystem to scale world drift.</summary>
    public float MoveSpeed => moveSpeed;
    /// <summary>Actual world-space X distance the player moved this frame. Always accurate regardless of background setup.</summary>
    public float LastMoveX { get; private set; }
    /// ARcade: in a boss's arena (BossArena) - the road stays put; she walks about in it.
    [System.NonSerialized] public bool ScrollLocked;
    /// ARcade: walked to this x and held there (EndlessKnightDirector: a boss arena's middle, till its boss
    /// leaps in); NaN: hers to move.
    [System.NonSerialized] public float HoldX = float.NaN;
    /// <summary>Current normalized background scroll position (0–1, wrapping). Used by reflection and parallax systems.</summary>
    public float BackgroundNormalizedTime => backgroundNormalizedTime;

    // Cached joystick input — read in Update, applied in LateUpdate
    private float lastInputX;
    private float lastInputZ;

    // We track the "real" position ourselves so animation curves can never push
    // the player past the clamp bounds, not even for a single frame.
    private Vector3 logicalPosition;

    // ============================================================
    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        if (playerSpriteRenderer == null)
        {
            playerSpriteRenderer = GetComponent<SpriteRenderer>();
            if (playerSpriteRenderer == null)
                playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        BuildPlayableGraph();
        PlayerBuffs.Ate += FoodFlash;
    }

    void Start()
    {
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

        // Prevent the PlayableGraph from overriding the player's position via root motion
        if (animator != null) animator.applyRootMotion = false;

        logicalPosition = transform.position;

        initialYPosition = transform.position.y;

        if (playerStats != null) lastHealth = playerStats.health;

        if (attackCollider != null) attackCollider.enabled = false;
        if (holySlashCollider != null) holySlashCollider.enabled = false;

        // All death UI objects start hidden
        if (borderBrokenGlassObject != null) borderBrokenGlassObject.SetActive(false);
        if (hpBarBreakObject != null) hpBarBreakObject.SetActive(false);
        if (hpBarBrokenGlassObject != null) hpBarBrokenGlassObject.SetActive(false);
        if (bloodDripObject != null) bloodDripObject.SetActive(false);
        if (glassShatterFXObject != null) glassShatterFXObject.SetActive(false);

        PlayClipForState(PlayerClipState.Idle);

        // Build the background parallax graph (manually scrubbed, not auto-playing)
        BuildBackgroundGraph();
    }

    void OnDestroy()
    {
        PlayerBuffs.Ate -= FoodFlash;
        if (playableGraph.IsValid()) playableGraph.Destroy();
        if (hitFXGraph.IsValid()) hitFXGraph.Destroy();
        if (borderBrokenGlassGraph.IsValid()) borderBrokenGlassGraph.Destroy();
        if (hpBarBreakGraph.IsValid()) hpBarBreakGraph.Destroy();
        if (bloodDripGraph.IsValid()) bloodDripGraph.Destroy();
        if (glassShatterFXGraph.IsValid()) glassShatterFXGraph.Destroy();
        if (backgroundGraph.IsValid()) backgroundGraph.Destroy();
    }

    // ============================================================
    // PlayableGraph — the "drag clips in" mechanism
    // ============================================================
    private void BuildPlayableGraph()
    {
        playableGraph = PlayableGraph.Create($"{name}_PlayerGraph");
        playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        var output = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);

        // One entry per PlayerClipState — null entries are simply skipped.
        AnimationClip[] allStateClips =
        {
            idleClip, runClip, jumpClip, attackClip,
            holySlashClip, greatHealClip, hurtClip, deathClip, kickChestClip, walkClip, jogClip, swordBuffClip, shieldBuffClip,
            restClip, restingClip, sleepingClip
        };

        // Build a compact list of only the non-null clips, and record the mapping.
        stateToClipIndex = new int[allStateClips.Length];
        var validClips = new System.Collections.Generic.List<AnimationClip>();
        for (int s = 0; s < allStateClips.Length; s++)
        {
            if (allStateClips[s] == null) { stateToClipIndex[s] = -1; continue; }
            stateToClipIndex[s] = validClips.Count;
            validClips.Add(allStateClips[s]);
        }

        // Mixer has EXACTLY one port per non-null clip — no unconnected ports.
        mixer = AnimationMixerPlayable.Create(playableGraph, validClips.Count);
        clipPlayables = new AnimationClipPlayable[validClips.Count];

        for (int i = 0; i < validClips.Count; i++)
        {
            clipPlayables[i] = AnimationClipPlayable.Create(playableGraph, validClips[i]);
            playableGraph.Connect(clipPlayables[i], 0, mixer, i);
            mixer.SetInputWeight(i, 0f);
        }

        output.SetSourcePlayable(mixer);
        playableGraph.Play();
    }

    private void PlayClipForState(PlayerClipState state)
    {
        if (stateToClipIndex == null || clipPlayables == null) return;
        int stateIdx = (int)state;
        if (stateIdx >= stateToClipIndex.Length) return;

        int idx = stateToClipIndex[stateIdx];
        if (idx < 0) return;                // clip was null in Inspector — skip gracefully
        if (idx == currentClipIndex) return;

        // Rewind so one-shot clips (attack, hurt, death...) always start at frame 0
        clipPlayables[idx].SetTime(0);

        if (blendRoutine != null) StopCoroutine(blendRoutine);
        blendRoutine = StartCoroutine(CrossfadeTo(idx, clipBlendTime));
        currentClipIndex = idx;
    }

    private IEnumerator CrossfadeTo(int targetIdx, float duration)
    {
        if (duration <= 0f)
        {
            for (int i = 0; i < clipPlayables.Length; i++)
                if (clipPlayables[i].IsValid())
                    mixer.SetInputWeight(i, i == targetIdx ? 1f : 0f);
            yield break;
        }

        float[] startWeights = new float[clipPlayables.Length];
        for (int i = 0; i < clipPlayables.Length; i++)
            startWeights[i] = mixer.GetInputWeight(i);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            // Smooth ease-in/out — feels better than linear for animation crossfade
            float s = k * k * (3f - 2f * k);
            for (int i = 0; i < clipPlayables.Length; i++)
            {
                if (!clipPlayables[i].IsValid()) continue;
                float target = i == targetIdx ? 1f : 0f;
                mixer.SetInputWeight(i, Mathf.Lerp(startWeights[i], target, s));
            }
            yield return null;
        }
    }

    // ARcade: standing still restPromptAfterSeconds with health or mana to
    // get back, REST shows over her head (RestPrompt), and C sits her down to
    // rest (the Rest clip once, then Resting looping); sleepAfterSeconds on
    // she falls asleep (Sleeping), healing slowly all the while, until
    // anything else happens. She never sits down by herself.
    private float standingSince = -1f, restingSince = -1f, restHealCarry, restManaCarry;
    private bool sleeping, restAsked;

    /// Sitting down, resting or asleep (healing; the health bar pulses green).
    public bool IsResting => restingSince >= 0f;
    /// Asleep (also getting mana back; her outline and the mana bar pulse purple).
    public bool IsSleeping => IsResting && sleeping;
    /// Health or mana to get back.
    public bool NeedsRest => playerStats != null && (playerStats.health < playerStats.MaxHP || playerStats.mana < playerStats.MaxMP);
    /// REST is up over her head: she's stood idle a while, and could do with it.
    public bool CanRest => !IsResting && !isDead && standingSince >= 0f && restClip && restingClip
        && Time.time - standingSince >= restPromptAfterSeconds && NeedsRest;

    /// Sits her down to rest (C, while REST is up).
    public void StartRest()
    {
        if (CanRest) restAsked = true;
    }

    private PlayerClipState IdleOrRest()
    {
        if (restingSince < 0f)
        {
            if (standingSince < 0f) standingSince = Time.time;
            if (!restAsked) return PlayerClipState.Idle;
            restAsked = false;
            restingSince = Time.time;
            restHealCarry = restManaCarry = 0f;
        }
        float rested = Time.time - restingSince;
        var state = rested < restClip.length ? PlayerClipState.Rest
            : sleepingClip && rested >= restClip.length + sleepAfterSeconds ? PlayerClipState.Sleeping
            : PlayerClipState.Resting;
        // Slowly back to health, faster asleep (straight onto it:
        // StatsControl's heal would flash the bar and her outline every point).
        if (playerStats != null && playerStats.health < playerStats.MaxHP)
        {
            restHealCarry += (state == PlayerClipState.Sleeping ? sleepHealPerSecond : restHealPerSecond) * Time.deltaTime;
            while (restHealCarry >= 1f && playerStats.health < playerStats.MaxHP) { playerStats.health++; restHealCarry -= 1f; }
        }
        sleeping = state == PlayerClipState.Sleeping;
        if (sleeping && playerStats != null && playerStats.mana < playerStats.MaxMP)
        {
            restManaCarry += sleepManaPerSecond * Time.deltaTime;
            while (restManaCarry >= 1f && playerStats.mana < playerStats.MaxMP) { playerStats.mana++; restManaCarry -= 1f; }
        }
        return state;
    }

    // ============================================================
    void Update()
    {
        // Hurt detection
        if (playerStats != null && playerStats.health < lastHealth)
        {
            // ARcade: every hit sounds and flashes the health bar (boss hits
            // came mid-attack, where TriggerHurt skips everything); only the
            // hurt animation waits for a free moment.
            if (hurtSound != null)
            {
                hurtSound.pitch = Random.Range(0.9f, 1.1f);
                PitAudio.Play(hurtSound);
            }
            PlayHealthBarHitFX();
            TriggerHurt();
        }
        // ARcade: follow heals too - it only followed drops, so after an
        // orange a hit that left more health than before the orange went unheard.
        if (playerStats != null) lastHealth = playerStats.health;

        // Death
        if (playerStats != null && playerStats.health <= 0 && !isDead)
        {
            TriggerDeath();
            return;
        }
        // ARcade: once dead, neither she nor the background moves.
        if (isDead) { ScrubBackground(0f); return; }

        // Locomotion input — only when nothing is blocking it
        float xAxis = PitInput.Move.x;
        float zAxis = PitInput.Move.y;
        // ARcade: the stick pushed mostly up or down - she only creeps along
        // the road (the way it leans, or the way she faces), never runs.
        if (Mathf.Abs(zAxis) > 0.1f && Mathf.Abs(zAxis) > Mathf.Abs(xAxis))
        {
            float way = Mathf.Abs(xAxis) > 0.05f ? Mathf.Sign(xAxis) : (isFacingRight ? 1f : -1f);
            xAxis = way * CreepSpeed;
            zAxis = 0f;
        }
        // ARcade: something solid in the way (the Slime Boss) - no walking into it.
        if (BlockedDirection != 0 && xAxis * BlockedDirection > 0f) xAxis = 0f;
        // (and up against its wall - it hopping at her, say - the road doesn't scroll on under her either)
        if ((xAxis > 0f && logicalPosition.x >= WallMax - WallSlack) || (xAxis < 0f && logicalPosition.x <= WallMin + WallSlack)) xAxis = 0f;
        // (held: she walks herself to the spot, slowing as she gets there, and waits there facing right - a
        // step past it is near enough, never a turn back)
        if (!float.IsNaN(HoldX))
        {
            float toGo = HoldX - logicalPosition.x;
            xAxis = toGo > -0.3f && toGo < 0.02f ? 0f : Mathf.Clamp(toGo * 6f, -1f, 1f);
            if (Mathf.Abs(xAxis) < 0.1f) { xAxis = 0f; FlipPlayer(1f); }
            zAxis = 0f;
        }
        // (shoved back: her own running doesn't carry her through it)
        if (_pushStart >= 0f) { xAxis = 0f; zAxis = 0f; }
        lastInputX = xAxis;
        lastInputZ = zAxis;

        bool standing = false;
        if (!isDead && !isAttacking && !isUsingAbility && !isHurt && !isKickingChest)
        {
            if (Mathf.Abs(xAxis) > 0.1f || Mathf.Abs(zAxis) > 0.1f)
            {
                if (!isJumping) PlayClipForState(Gait(Mathf.Abs(xAxis)));
                FlipPlayer(xAxis);
            }
            else if (!isJumping)
            {
                standing = true;
                PlayClipForState(IdleOrRest());
            }
        }
        // (Any touch of the stick, even into the Slime Boss, gets her up and takes REST away.)
        if (!standing || PitInput.Move.sqrMagnitude > 0f) { standingSince = -1f; restingSince = -1f; restAsked = false; }

        // Scrub the background — freeze it while an ability animation or chest kick is playing
        ScrubBackground((isUsingAbility || isKickingChest || ScrollLocked) ? 0f : xAxis);
        CatchUp();

        // Jump cooldown
        if (isJumpOnCooldown)
        {
            jumpCooldownTimer -= Time.deltaTime;
            if (jumpCooldownTimer <= 0f)
            {
                isJumpOnCooldown = false;
                jumpCooldownTimer = 0f;
                jumpCount = 0;
            }
        }

        JumpFrames();
        AdjustSpriteSortingOrder();
    }

    // LateUpdate runs AFTER the PlayableGraph writes animation data.
    // We force transform.position to our tracked logicalPosition every frame
    // so animation curves can never visually push the player past the clamps.
    // ARcade: her drawn sprite (ARIANA) bigger than the game was built for -
    // her body, colliders and the fairy unchanged. It grows from her feet,
    // and the position the spell clips give it (their frames sit elsewhere
    // on their canvas) grows with it.
    private Transform spriteBody;
    private float spriteScale = 1f;
    private Vector3 spriteHome, spriteHomeScaled, spriteLastSet;

    public void ScaleSprite(float scale)
    {
        spriteBody = transform.Find("ARIANA");
        var sr = spriteBody ? spriteBody.GetComponent<SpriteRenderer>() : null;
        if (!sr || scale <= 0f) return;
        float feet = sr.bounds.min.y;
        spriteHome = spriteBody.localPosition;
        spriteBody.localScale *= scale;
        spriteBody.position += Vector3.up * (feet - sr.bounds.min.y);
        spriteHomeScaled = spriteLastSet = spriteBody.localPosition;
        spriteScale = scale;
    }

    // The animation's position for the sprite, scaled about her feet (only
    // when it has written one this frame). Her resting position - which the
    // animation may write back as it was or as already scaled, depending on
    // when it took it - is her scaled home; only the spell clips' own
    // positions are scaled.
    private void ScaleSpritePosition()
    {
        if (!spriteBody || spriteScale == 1f) return;
        var written = spriteBody.localPosition;
        if ((written - spriteLastSet).sqrMagnitude < 1e-10f) return;
        if ((written - spriteHomeScaled).sqrMagnitude < 1e-8f || (written - spriteHome).sqrMagnitude < 1e-8f)
            spriteLastSet = spriteHomeScaled;
        else
            spriteLastSet = spriteHomeScaled + spriteScale * (written - spriteHome);
        spriteBody.localPosition = spriteLastSet;
    }

    void LateUpdate()
    {
        ScaleSpritePosition();
        // ARcade: dead, she stays where she fell (on the ground).
        // Killed mid-jump or mid-knockback, she drops to the ground as the
        // death animation plays instead of hanging in the air.
        if (isDead)
        {
            LastMoveX = 0f;
            logicalPosition.y = Mathf.MoveTowards(logicalPosition.y, initialYPosition, DeathFallSpeed * Time.deltaTime);
            transform.position = logicalPosition;
            ApplyBig();
            return;
        }

        // Apply joystick movement to our tracked position
        LastMoveX = 0f;
        if (!isAttacking && !isUsingAbility && !isHurt && !isJumping && !isKickingChest)
        {
            if (Mathf.Abs(lastInputX) > 0.1f || Mathf.Abs(lastInputZ) > 0.1f)
            {
                Vector3 move = new Vector3(lastInputX, 0f, lastInputZ) * moveSpeed * Time.deltaTime;
                move.x += LockedDrift();   // (in an arena: the road's share of her speed too)
                logicalPosition += move;
                LastMoveX = move.x;   // actual X units moved this frame
            }
        }

        // (shoved back: a share of it this frame, most at first)
        if (_pushStart >= 0f)
        {
            float a = Mathf.Clamp01((Time.time - Time.deltaTime - _pushStart) / _pushTime), b = Mathf.Clamp01((Time.time - _pushStart) / _pushTime);
            logicalPosition.x += _pushBack * ((1f - (1f - b) * (1f - b)) - (1f - (1f - a) * (1f - a)));
            if (b >= 1f) _pushStart = -1f;
        }

        // Apply jump arc to logical position
        if (isJumping)
        {
            // (in an arena, carried on as far as the road would have taken her)
            float drift = LockedDrift();
            startJumpPosition.x += drift; targetJumpPosition.x += drift;
            PerformArcJump();
        }

        // Clamp logical position to limits
        if (limitX && !ScrollLocked) logicalPosition.x = Mathf.Clamp(logicalPosition.x, minX, maxX);   // (in an arena: its whole width, BossArena's walls)
        if (limitY) logicalPosition.y = Mathf.Clamp(logicalPosition.y, minY, maxY);
        if (limitZ) logicalPosition.z = Mathf.Clamp(logicalPosition.z, minZ, maxZ);
        // ARcade: something solid (the Slime Boss) - she can't get past it,
        // walking, jumping or in a spell's moves (EndlessKnightDirector sets it).
        logicalPosition.x = Mathf.Clamp(logicalPosition.x, WallMin, WallMax);

        // Force the actual transform to match — overrides anything the animation graph wrote
        transform.position = logicalPosition;
        ApplyBig();
    }

    // ARcade: big on viking food (PlayerBuffs.Big) - all of her grows from her feet (her sword's
    // reach and her body with her; not the fairy), springing up, and back down as it wears off,
    // flickering for its last seconds. And a flash of colour over her as she eats.
    private Vector3 rootScale;
    private float feetBelow = -1f, bigNow = 1f, bigVel;
    private bool wasBig;
    private Transform fairyArt;
    private Vector3 fairyScale;
    private SpriteRenderer foodFlash;
    private MaterialPropertyBlock foodFlashBlock;
    private float foodFlashAt = -10f;
    private Color foodFlashColor = Color.green;
    private const float FoodFlashTime = 0.8f;

    private void ApplyBig()
    {
        var art = spriteBody ? spriteBody : transform.Find("ARIANA");
        if (bigNow == 1f && bigVel == 0f)
        {
            // Her size as she is (each frame, so nothing else's change to it is lost).
            var sc = transform.localScale;
            rootScale = new Vector3(Mathf.Abs(sc.x), sc.y, sc.z);
            var artSr = art ? art.GetComponent<SpriteRenderer>() : null;
            if (feetBelow < 0f && artSr && artSr.sprite && !isCasting && !isUsingAbility && !isJumping) feetBelow = transform.position.y - (art.position.y + SpriteShadow.OpaqueBottom(artSr.sprite) * art.lossyScale.y);   // (her boots' lowest solid pixels - not the frame's clear canvas under them: grown, they stay on the ground)
            if (!fairyArt) { fairyArt = transform.Find("FAIRY"); if (fairyArt) fairyScale = fairyArt.localScale; }
        }
        bool big = PlayerBuffs.Big && !isDead;
        if (wasBig && !big && !isDead && PlayerBuffs.ShrinkSound) PitAudio.PlayClip(PlayerBuffs.ShrinkSound, 0.9f);
        wasBig = big;
        float target = big ? PlayerBuffs.BigScale : 1f;
        if (big && PlayerBuffs.BigUntil - Time.time < 3f) target *= ((int)(Time.time * 8f)) % 2 == 0 ? 1f : 0.9f;   // (wearing off)
        if (Mathf.Abs(target - bigNow) > 0.001f || Mathf.Abs(bigVel) > 0.001f)
        {
            bigVel += ((target - bigNow) * 90f - bigVel * 11f) * Time.deltaTime;   // (a spring: a boing as she grows)
            bigNow += bigVel * Time.deltaTime;
            if (Mathf.Abs(target - bigNow) < 0.002f && Mathf.Abs(bigVel) < 0.02f) { bigNow = target; bigVel = 0f; }
        }
        PlayerBuffs.BigNow = bigNow;
        if (bigNow != 1f || bigVel != 0f)
        {
            float sign = transform.localScale.x < 0f ? -1f : 1f;
            transform.localScale = new Vector3(sign * rootScale.x * bigNow, rootScale.y * bigNow, rootScale.z * bigNow);
            if (feetBelow > 0f) transform.position += Vector3.up * feetBelow * (bigNow - 1f);
            if (fairyArt) fairyArt.localScale = fairyScale / bigNow;
        }
        else
        {
            float sign = transform.localScale.x < 0f ? -1f : 1f;
            transform.localScale = new Vector3(sign * rootScale.x, rootScale.y, rootScale.z);
            if (fairyArt) fairyArt.localScale = fairyScale;
        }

        // The flash as she eats: her shape in its colour over her, pulsing out.
        float age = Time.time - foodFlashAt;
        if (foodFlash || age < FoodFlashTime)
        {
            var artSr = art ? art.GetComponent<SpriteRenderer>() : null;
            if (!foodFlash && artSr)
            {
                var shader = Shader.Find("SpatialEmulator/SpriteSolid");
                if (!shader) return;
                var go = new GameObject("FOOD FLASH");
                go.layer = art.gameObject.layer;
                go.transform.SetParent(art, false);
                foodFlash = go.AddComponent<SpriteRenderer>();
                foodFlash.sharedMaterial = new Material(shader) { name = "Food Flash" };
                foodFlashBlock = new MaterialPropertyBlock();
            }
            if (!foodFlash || !artSr) return;
            bool on = age < FoodFlashTime && artSr.enabled;
            foodFlash.enabled = on;
            if (!on) return;
            foodFlash.sprite = artSr.sprite;
            foodFlash.flipX = artSr.flipX;
            foodFlash.sortingLayerID = artSr.sortingLayerID;
            foodFlash.sortingOrder = artSr.sortingOrder + 3;
            float pulse = 0.5f + 0.5f * Mathf.Cos(age * Mathf.PI * 2f * 4f);
            var c = foodFlashColor; c.a = 0.85f * pulse * (1f - age / FoodFlashTime);
            foodFlashBlock.SetColor("_Color", c);
            foodFlash.SetPropertyBlock(foodFlashBlock);
        }
    }

    private void FoodFlash(Color color) { foodFlashColor = color; foodFlashAt = Time.time; }

    // ============================================================
    // Parallax background
    // ============================================================
    private void BuildBackgroundGraph()
    {
        if (backgroundClip == null || backgroundAnimator == null) return;

        backgroundGraph = PlayableGraph.Create($"{name}_BackgroundParallax");
        // Manual mode — we set the time every frame, we don't want it auto-advancing
        backgroundGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

        var output = AnimationPlayableOutput.Create(backgroundGraph, "Background", backgroundAnimator);
        backgroundPlayable = AnimationClipPlayable.Create(backgroundGraph, backgroundClip);

        // Pause auto-play; we scrub manually
        backgroundPlayable.SetPlayState(PlayState.Paused);
        backgroundPlayable.SetDuration(double.MaxValue);

        output.SetSourcePlayable(backgroundPlayable);
        backgroundGraph.Play();

        // Set initial frame
        backgroundPlayable.SetTime(0f);
        backgroundGraph.Evaluate();
    }

    // ARcade: in an arena (BossArena) the road stands still, so she covers the ground the road's
    // scrolling would have this frame herself - as fast, and her jumps as long, as out on the road.
    private float LockedDrift()
    {
        if (!ScrollLocked || Mathf.Abs(lastInputX) < 0.1f) return 0f;
        float scroll = Mathf.Clamp(lastInputX * backgroundScrollSpeed * Time.deltaTime, -backgroundMaxSpeed * Time.deltaTime, backgroundMaxSpeed * Time.deltaTime);
        return scroll * 100f * EndlessKnightDirector.RoadUnitsPerFrame;
    }

    // ARcade: out of an arena (BossArena) somewhere past her usual stretch of the picture: the road
    // scrolls her back into it, a little at a time - the picture following her, not her sliding back.
    private void CatchUp()
    {
        if (ScrollLocked || !limitX || isDead || !backgroundGraph.IsValid() || backgroundClip == null) return;
        float x = logicalPosition.x, into = Mathf.Clamp(x, minX, maxX);
        if (Mathf.Abs(x - into) < 0.001f) return;
        float units = Mathf.MoveTowards(x, into, 1.6f * Time.deltaTime) - x;   // (she moves back this much...)
        logicalPosition.x += units;
        // ...and the road with her, the other way.
        float frames = -units / Mathf.Max(1e-5f, EndlessKnightDirector.RoadUnitsPerFrame);
        Scroll(frames / 100f);
    }

    private void ScrubBackground(float horizontalInput)
    {
        // Always reset so ParallaxSpawnSystem reads 0 when the player is idle
        LastScrollDelta = 0f;

        if (!backgroundGraph.IsValid() || backgroundClip == null) return;

        // No input → don't move the background
        if (Mathf.Abs(horizontalInput) < 0.1f) return;

        float clipLength = backgroundClip.length;

        // Player moves right → background scrolls right (positive direction in clip time)
        // Player moves left  → background scrolls left (negative direction in clip time)
        float scrollDelta = horizontalInput * backgroundScrollSpeed * Time.deltaTime;

        // Clamp to max speed
        float maxDelta = backgroundMaxSpeed * Time.deltaTime;
        scrollDelta = Mathf.Clamp(scrollDelta, -maxDelta, maxDelta);

        Scroll(scrollDelta);
    }

    private void Scroll(float scrollDelta)
    {
        float clipLength = backgroundClip.length;
        // Accumulate frame counters — 100 frames per full normalized cycle.
        // accumulatedFrames is directional: moving forward adds, moving backward subtracts,
        // ARcade: not clamped at 0 - walking back from the start scrolls the
        // road, so the counter has to follow it or everything on the road
        // drifts out of step with it (spawns still need net forward progress).
        float frameDelta   = scrollDelta * 100f;
        accumulatedFrames  = accumulatedFrames + frameDelta;
        netFrames         += frameDelta;
        LastScrollDelta   += scrollDelta;

        // Advance normalized time and wrap 0..1
        backgroundNormalizedTime += scrollDelta;

        // Wrap around seamlessly — going past 1.0 loops back to 0.0, going below 0.0 loops to 1.0
        backgroundNormalizedTime -= Mathf.Floor(backgroundNormalizedTime);

        // Scrub the playable to the exact frame
        backgroundPlayable.SetTime(backgroundNormalizedTime * clipLength);
        backgroundGraph.Evaluate();
    }

    // ============================================================
    // Movement / jump
    // ============================================================
    private void PerformArcJump()
    {
        jumpTimer += Time.deltaTime / (jumpDuration * jumpSpan);

        float totalHeight = jumpHeight * jumpMultiplier;
        float t01 = Mathf.Clamp01(jumpTimer);
        float curveY = (bounceArc ? 4f * t01 * (1f - t01) : Mathf.Sin(Mathf.PI * jumpTimer)) * totalHeight;

        Vector3 nextPosition = Vector3.Lerp(startJumpPosition, targetJumpPosition, jumpTimer);
        nextPosition.y = Mathf.Lerp(startJumpPosition.y, initialYPosition, jumpTimer) + curveY;

        // Write to logicalPosition — LateUpdate will apply it + clamp
        logicalPosition = nextPosition;

        if (jumpTimer >= 1f)
        {
            isJumping = false;
            jumpTimer = 0f;
            Bounces = 0;   // (down on the road: her head bounces counted afresh)

            logicalPosition.y = initialYPosition;

            // ARcade: landing gives the jumps back (THE PIT locked jumping for
            // jumpCooldownTime after two, which made rows of spikes unjumpable).
            jumpCount = 0;
        }
    }

    public void TriggerJump()
    {
        // ARcade: a second jump cuts short a swing in the air (its hit has already landed).
        if (isAttacking && isJumping && jumpCount < maxJumps && !isDead)
        {
            CancelInvoke(nameof(AttackComplete));
            isAttacking = false;
        }
        if (!isJumpOnCooldown && !isAttacking && !isUsingAbility && !isDead && !isKickingChest)
        {
            if (jumpCount < maxJumps)
            {
                isJumping = true;
                jumpCount++;
                jumpTimer = 0f;
                jumpSpan = 1f; bounceArc = false;

                jumpMultiplier = 1f + (jumpCount - 1) * 0.5f;

                float xAxis = PitInput.Move.x;
                float zAxis = PitInput.Move.y;

                startJumpPosition = logicalPosition;   // (not lifted for her size)
                targetJumpPosition = startJumpPosition + new Vector3(xAxis * jumpDistance, 0f, zAxis * jumpDistance);

                rb.constraints = RigidbodyConstraints.FreezeRotation;

                // (A second jump, already on the jump clip, leaps up again from its leaping frames.)
                int jumpIdx = stateToClipIndex != null ? stateToClipIndex[(int)PlayerClipState.Jump] : -1;
                bool again = jumpCount > 1;
                if (jumpIdx >= 0 && currentClipIndex == jumpIdx) clipPlayables[jumpIdx].SetTime(JumpLeapFrom);
                PlayClipForState(PlayerClipState.Jump);
                if (again && jumpIdx >= 0) clipPlayables[jumpIdx].SetTime(JumpLeapFrom);   // (from a swing too)
                if (jumpSound != null) PitAudio.Play(jumpSound);
            }
        }
    }

    private void StartJumpCooldown()
    {
        isJumpOnCooldown = true;
        jumpCooldownTimer = jumpCooldownTime;
    }

    // ARcade: the spells (Holy Slash, Great Heal, the buffs) are drawn over
    // everything on the road - the Slime Boss and the other enemies
    // included. They're on the Default sorting layer, which draws after the
    // player's TopLayer, so for a spell - or a blow of her sword, her kick -
    // she moves higher, over the foes and bosses (205-217).
    private bool slashOnTop;
    private const int SlashSortingOrder = 219;   // ARcade: over the foes (210) and bosses (205) and their shots and eyes - her blows seen landing
    private const int KnightSortingOrder = 150;   // ARcade: over everything on the road
    private static readonly PlayerClipState[] SpellStates =
        { PlayerClipState.HolySlash, PlayerClipState.GreatHeal, PlayerClipState.SwordBuff, PlayerClipState.ShieldBuff, PlayerClipState.Attack, PlayerClipState.KickChest };   // (and her sword, her kick: over the foes they hit)
    private void SlashOnTop()
    {
        bool slashing = false;
        if (stateToClipIndex != null && !isDead)
            foreach (var spell in SpellStates)
            {
                int index = stateToClipIndex[(int)spell];
                if (index >= 0 && currentClipIndex == index) { slashing = true; break; }
            }
        if (slashing == slashOnTop || playerSpriteRenderer == null) return;
        slashOnTop = slashing;
        // ARcade: she's always on the front sorting layer now (over the
        // campfire, the witch, the portal and its fruit); spells just higher.
        playerSpriteRenderer.sortingLayerName = "Default";
    }

    private void AdjustSpriteSortingOrder()
    {
        SlashOnTop();
        if (playerSpriteRenderer != null)
        {
            playerSpriteRenderer.sortingLayerName = "Default";
            playerSpriteRenderer.sortingOrder = slashOnTop ? SlashSortingOrder : KnightSortingOrder;
        }
    }

    // ARcade: the walk, run or sprint for how fast she's going (a gait
    // without its clip falls back to the sprint).
    private PlayerClipState Gait(float speed)
    {
        if (speed <= walkUpTo && walkClip) return PlayerClipState.Walk;
        if (speed <= jogUpTo && jogClip) return PlayerClipState.Jog;
        return PlayerClipState.Run;
    }

    private void FlipPlayer(float xAxis)
    {
        if (xAxis > 0f && !isFacingRight)
        {
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x);
            transform.localScale = s;
            isFacingRight = true;
        }
        else if (xAxis < 0f && isFacingRight)
        {
            var s = transform.localScale;
            s.x = -Mathf.Abs(s.x);
            transform.localScale = s;
            isFacingRight = false;
        }
    }

    // ============================================================
    // Death / hurt
    // ============================================================
    private void TriggerDeath()
    {
        isDead = true;
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Error);   // ARcade: down

        // Cancel any in-flight attack / ability Invokes so colliders
        // and damage calls queued just before death never fire.
        CancelInvoke(nameof(AttackComplete));
        CancelInvoke(nameof(CastComplete));
        CancelInvoke(nameof(EnableHolySlashCollider));
        CancelInvoke(nameof(DamageAllEnemiesInHolySlash));
        CancelInvoke(nameof(GreatHealComplete));

        if (kickChestRoutine != null) { StopCoroutine(kickChestRoutine); kickChestRoutine = null; }
        isKickingChest = false;

        // Immediately disable both attack colliders
        if (attackCollider    != null) attackCollider.enabled    = false;
        if (holySlashCollider != null) holySlashCollider.enabled = false;

        // Clear state flags so nothing lingers
        isAttacking    = false;
        isUsingAbility = false;
        isCasting = false;
        PlayerBuffs.Reset();
        // ARcade: the jump arc stops; LateUpdate brings her down to the ground.
        isJumping      = false;
        jumpTimer      = 0f;

        PlayClipForState(PlayerClipState.Death);
        rb.constraints = RigidbodyConstraints.FreezeAll;

        if (deathSound != null && !deathSound.isPlaying) PitAudio.Play(deathSound);

        if (playerSpriteRenderer != null)
        {
            playerSpriteRenderer.sortingLayerName = "Default";
            playerSpriteRenderer.sortingOrder = KnightSortingOrder;
        }

        // Health bar death audio (glass shatter, etc.)
        PlayOneShotAudio(healthBarDeathAudioClip, healthBarDeathAudioSource);

        // HP BAR BREAK → BROKEN GLASS → BLOOD DRIP sequence
        StartCoroutine(HealthBarDeathSequence());
    }

    private IEnumerator HealthBarDeathSequence()
    {
        // Step 1a: Enable and play BORDER BROKEN GLASS clip
        if (borderBrokenGlassClip != null && borderBrokenGlassObject != null)
        {
            borderBrokenGlassObject.SetActive(true);
            Animator borderAnim = borderBrokenGlassObject.GetComponent<Animator>();
            if (borderAnim == null) borderAnim = borderBrokenGlassObject.AddComponent<Animator>();
        }

        // Step 1b: Enable and play GLASS SHATTER FX clip
        if (glassShatterFXClip != null && glassShatterFXObject != null)
        {
            glassShatterFXObject.SetActive(true);
            Animator shatterAnim = glassShatterFXObject.GetComponent<Animator>();
            if (shatterAnim == null) shatterAnim = glassShatterFXObject.AddComponent<Animator>();
        }

        yield return null; // let all newly-enabled Animators initialize

        // Now fire the clips
        if (borderBrokenGlassClip != null && borderBrokenGlassObject != null)
        {
            Animator borderAnim = borderBrokenGlassObject.GetComponent<Animator>();
            PlayOneShotGraph(ref borderBrokenGlassGraph, borderBrokenGlassClip, borderAnim, $"{name}_BorderBrokenGlass");
        }

        if (glassShatterFXClip != null && glassShatterFXObject != null)
        {
            Animator shatterAnim = glassShatterFXObject.GetComponent<Animator>();
            PlayOneShotGraph(ref glassShatterFXGraph, glassShatterFXClip, shatterAnim, $"{name}_GlassShatterFX");
        }

        // Step 2: Enable the break object and play HP BAR BREAK clip on it
        float breakDuration = 0f;
        if (hpBarBreakClip != null && hpBarBreakObject != null)
        {
            hpBarBreakObject.SetActive(true);

            Animator breakAnim = hpBarBreakObject.GetComponent<Animator>();
            if (breakAnim == null) breakAnim = hpBarBreakObject.AddComponent<Animator>();

            yield return null; // let Animator initialize

            PlayOneShotGraph(ref hpBarBreakGraph, hpBarBreakClip, breakAnim, $"{name}_HPBarBreak");
            breakDuration = hpBarBreakClip.length;
        }

        // Wait for the break animation to finish
        if (breakDuration > 0f)
            yield return new WaitForSeconds(breakDuration);

        // Step 3: Enable the BROKEN DRIP single-frame sprite
        if (hpBarBrokenGlassObject != null)
            hpBarBrokenGlassObject.SetActive(true);

        // Step 4: Enable the blood drip object and start looping animation
        if (bloodDripClip != null && bloodDripObject != null)
        {
            bloodDripObject.SetActive(true);

            Animator dripAnim = bloodDripObject.GetComponent<Animator>();
            if (dripAnim == null) dripAnim = bloodDripObject.AddComponent<Animator>();

            yield return null; // let Animator initialize

            PlayLoopingGraph(ref bloodDripGraph, bloodDripClip, dripAnim, $"{name}_BloodDrip");
        }
    }

    private void TriggerHurt()
    {
        // (Not again while she's shaking off a knockback - KnockImmune - past the hit that knocked her.)
        bool shrugged = KnockImmune && Time.time - _knockAt > 0.1f;
        if (!isDead && !isHurt && !isJumping && !isAttacking && !isUsingAbility && !isKickingChest && !shrugged)
        {
            isHurt = true;
            PlayClipForState(PlayerClipState.Hurt);
            // (The sound and health bar hit FX play where the damage is noticed.)
            Invoke(nameof(HurtComplete), hurtDelay);
        }
    }

    private void HurtComplete() { isHurt = false; }

    // ============================================================
    // Health Bar UI effect helpers
    // ============================================================

    // Plays the HIT FX clip on the health bar every time the player takes damage.
    // Restarts from frame 0 each time so rapid hits re-trigger the effect.
    private void PlayHealthBarHitFX()
    {
        if (hitFXClip == null || hitFXAnimator == null) return;

        PlayOneShotGraph(ref hitFXGraph, hitFXClip, hitFXAnimator, $"{name}_HitFX");

        // Optional hit FX audio
        PlayOneShotAudio(hitFXAudioClip, hitFXAudioSource);
    }

    // Creates (or recreates) a PlayableGraph that plays a clip once from frame 0.
    private void PlayOneShotGraph(ref PlayableGraph graph, AnimationClip clip, Animator target, string graphName)
    {
        if (graph.IsValid()) graph.Destroy();

        graph = PlayableGraph.Create(graphName);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        var output = AnimationPlayableOutput.Create(graph, graphName, target);
        var playable = AnimationClipPlayable.Create(graph, clip);
        output.SetSourcePlayable(playable);

        graph.Play();
    }

    // Creates a PlayableGraph that plays a clip in an endless loop.
    private void PlayLoopingGraph(ref PlayableGraph graph, AnimationClip clip, Animator target, string graphName)
    {
        if (graph.IsValid()) graph.Destroy();

        graph = PlayableGraph.Create(graphName);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        var output = AnimationPlayableOutput.Create(graph, graphName, target);
        var playable = AnimationClipPlayable.Create(graph, clip);

        // Force the playable to loop regardless of the clip's Loop Time setting
        playable.SetDuration(double.MaxValue);

        output.SetSourcePlayable(playable);
        graph.Play();
    }

    // Plays an AudioClip through the given source (or creates a temporary one).
    private void PlayOneShotAudio(AudioClip clip, AudioSource source)
    {
        if (clip == null) return;

        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }

        source.clip = clip;
        source.pitch = Random.Range(0.9f, 1.1f);
        source.volume = 1f;
        PitAudio.Play(source);
    }

    // ============================================================
    // Abilities
    // ============================================================
    public void TriggerGreatHeal()
    {
        if (!isUsingAbility && !isJumping && !isAttacking && !isKickingChest && playerStats != null && playerStats.mana >= mpDecreaseAmount && !isDead
            && Mathf.Abs(lastInputX) < 0.1f && Mathf.Abs(lastInputZ) < 0.1f)
        {
            // ARcade: it plays out whole (BeginCast); the heal lands at greatHealDelay.
            BeginCast(PlayerClipState.GreatHeal, Mathf.Max(greatHealDelay, greatHealClip ? greatHealClip.length : 0f));

            if (playFirstHealSound) { if (healSound1 != null) PitAudio.Play(healSound1); }
            else                     { if (healSound2 != null) PitAudio.Play(healSound2); }
            playFirstHealSound = !playFirstHealSound;

            Invoke(nameof(GreatHealComplete), greatHealDelay);
        }
    }

    private void GreatHealComplete()
    {
        if (statsControl != null && playerStats != null)
        {
            // ARcade: never past the health bar's max (IncreaseHP raised the max).
            statsControl.HealHP(healAmount);
            playerStats.mana = Mathf.Max(0, playerStats.mana - mpDecreaseAmount);
        }
    }

    // ARcade: a spell's animation plays out whole - nothing breaks into it
    // (moves, hits, knockback, chest kicks) - and it's drawn on top.
    private PlayerClipState castState;

    private void BeginCast(PlayerClipState state, float seconds)
    {
        SpatialEmulator.Haptics.Play(SpatialEmulator.Haptics.Kind.Rigid, 0.7f);   // ARcade: a spell goes off
        isUsingAbility = true;
        isCasting = true;
        castState = state;
        hasSlashArea = false;
        PlayClipForState(state);
        CancelInvoke(nameof(CastComplete));
        Invoke(nameof(CastComplete), seconds);
    }

    private void CastComplete() { isUsingAbility = false; isCasting = false; }

    /// ARcade: whether a spell can start now (standing still, not busy).
    public bool CanCast => !isUsingAbility && !isJumping && !isAttacking && !isDead && !isKickingChest
        && Mathf.Abs(lastInputX) < 0.1f && Mathf.Abs(lastInputZ) < 0.1f;

    /// ARcade: what a buff costs in mana (every spell costs the same: 15).
    [SerializeField] private int buffManaCost = 15;
    public int BuffManaCost => buffManaCost;

    /// ARcade: a spell's animation is playing (PlayerGlow flashes gold).
    public bool IsCasting => isCasting;
    /// ARcade: Great Heal is playing (she kneels on its glowing plate - her shadow goes under that).
    public bool IsHealing => stateToClipIndex != null && currentClipIndex >= 0 && currentClipIndex == stateToClipIndex[(int)PlayerClipState.GreatHeal];

    /// ARcade: while a sword attack or a Holy Slash is playing, the area its
    /// blow reaches (its hit box, in front of her) - a bomb guy that walks or
    /// leaps into it is batted away (BombGuy).
    public bool ActiveAttackArea(out Bounds area)
    {
        // (Where the blow's hit box was when it landed: the collider is
        // only on for that moment; it counts till the sword's drawn back: SwordDone.)
        area = default;
        if (isDead) return false;
        if (isAttacking && hasAttackArea && SwingAge < SwordDone) { area = attackArea; return true; }
        if (isCasting && castState == PlayerClipState.HolySlash && hasSlashArea) { area = slashArea; return true; }
        return false;
    }

    private Bounds attackArea, slashArea;
    private bool hasAttackArea, hasSlashArea;

    /// ARcade: Sword Buff's cast for show (the ending: LevelPortal) - no mana, no buff.
    public void CastSwordForShow()
    {
        if (isDead || !swordBuffClip) return;
        BeginCast(PlayerClipState.SwordBuff, swordBuffClip.length);
        PitAudio.PlayClip(swordBuffSound);
    }

    /// ARcade: Sword Buff - double damage for buffSeconds, a gold sword.
    public void TriggerSwordBuff()
    {
        if (!CanCast || !swordBuffClip || (playerStats && playerStats.mana < BuffManaCost)) return;
        if (playerStats) playerStats.mana -= BuffManaCost;
        float cast = swordBuffClip.length;
        BeginCast(PlayerClipState.SwordBuff, cast);
        PitAudio.PlayClip(swordBuffSound);
        PlayerBuffs.SwordFrom = Time.time + cast;
        PlayerBuffs.SwordUntil = Time.time + cast + buffSeconds;
    }

    /// ARcade: Shield Buff - invulnerable for buffSeconds (from the cast's
    /// start), a golden outline.
    public void TriggerShieldBuff()
    {
        if (!CanCast || !shieldBuffClip || (playerStats && playerStats.mana < BuffManaCost)) return;
        if (playerStats) playerStats.mana -= BuffManaCost;
        float cast = shieldBuffClip.length;
        BeginCast(PlayerClipState.ShieldBuff, cast);
        PitAudio.PlayClip(shieldBuffSound);
        PlayerBuffs.ShieldFrom = Time.time + cast;
        PlayerBuffs.ShieldUntil = Time.time + cast + buffSeconds;
    }

    public void TriggerAttack()
    {
        // ARcade: in the air too (then back to her falling frames till she lands).
        if (!isAttacking && !isUsingAbility && !isDead && !isKickingChest)
        {
            isAttacking = true;
            SwingCount++;   // ARcade: this swing's number (a foe takes one blow a swing: RoadEnemy)
            swingStartedAt = Time.time;
            PlayClipForState(PlayerClipState.Attack);
            if (attackSound != null) PitAudio.Play(attackSound);

            PerformTriggerAttack();
            Invoke(nameof(AttackComplete), attackDelay);
        }
    }

    private void AttackComplete()
    {
        isAttacking = false;
        // ARcade: swung in the air - her falling frames again, till she lands.
        int jump = stateToClipIndex != null ? stateToClipIndex[(int)PlayerClipState.Jump] : -1;
        if (isJumping && !isDead && jump >= 0 && clipPlayables != null)
        {
            PlayClipForState(PlayerClipState.Jump);
            clipPlayables[jump].SetTime(JumpFallFrom);
        }
    }

    private void PerformTriggerAttack()
    {
        if (attackCollider != null)
        {
            Vector3 newPosition = transform.position + PlayerBuffs.BigNow * (isFacingRight   // (big: her reach with her)
                ? colliderOffset
                : new Vector3(-colliderOffset.x, colliderOffset.y, colliderOffset.z));
            attackCollider.center = transform.InverseTransformPoint(newPosition);

            attackCollider.enabled = true;
            DamageAllEnemiesInAttackCollider();
        }
    }

    private void DamageAllEnemiesInAttackCollider()
    {
        // Force physics to refresh bounds before OverlapBox — we just modified the collider
        Physics.SyncTransforms();

        Collider[] hitEnemies = Physics.OverlapBox(
            attackCollider.bounds.center,
            attackCollider.bounds.extents,
            attackCollider.transform.rotation,
            LayerMask.GetMask("ENEMY"));

        foreach (Collider enemyCollider in hitEnemies)
            DealDamageToAnyEnemy(enemyCollider, attackDamage, "Light Attack");
        BombGuy.HitAll(attackCollider.bounds);   // ARcade: bomb guys are batted away
        RoadFoe.HitAll(attackCollider.bounds, (PlayerBuffs.Sword ? attackDamage * 2 : attackDamage) * (PlayerBuffs.Big ? PlayerBuffs.BigDamage : 1), false);   // ARcade: the levels' own foes and bosses
        attackArea = attackCollider.bounds;      // (and ones that run into it as it lands)
        hasAttackArea = true;

        DisableAttackCollider();
    }

    private void DisableAttackCollider()
    {
        if (attackCollider != null) attackCollider.enabled = false;
    }

    public void TriggerHolySlash()
    {
        if (!isUsingAbility && !isJumping && !isAttacking && !isDead && !isKickingChest
            && Mathf.Abs(lastInputX) < 0.1f && Mathf.Abs(lastInputZ) < 0.1f
            && (playerStats == null || playerStats.mana >= holySlashManaCost))
        {
            if (playerStats != null) playerStats.mana -= holySlashManaCost;
            BeginCast(PlayerClipState.HolySlash, holySlashDelay);
            if (holySlashSound != null) PitAudio.Play(holySlashSound);

            if (holySlashCollider != null)
                Invoke(nameof(EnableHolySlashCollider), holySlashColliderEnableDelay);

        }
    }

    private void EnableHolySlashCollider()
    {
        if (holySlashCollider == null) return;

        Vector3 newPosition = transform.position + PlayerBuffs.BigNow * (isFacingRight
            ? holySlashColliderOffset
            : new Vector3(-holySlashColliderOffset.x, holySlashColliderOffset.y, holySlashColliderOffset.z));
        holySlashCollider.center = transform.InverseTransformPoint(newPosition);

        holySlashCollider.enabled = true;
        Invoke(nameof(DamageAllEnemiesInHolySlash), 0.05f);
    }

    private void DamageAllEnemiesInHolySlash()
    {
        Physics.SyncTransforms();

        Collider[] hitEnemies = Physics.OverlapBox(
            holySlashCollider.bounds.center,
            holySlashCollider.bounds.extents,
            holySlashCollider.transform.rotation,
            LayerMask.GetMask("ENEMY"));

        foreach (Collider enemyCollider in hitEnemies)
        {
            // ARcade: the Holy Slash (the C button) all but one-shots a Slime Boss.
            bool boss = enemyCollider.GetComponentInParent<EnemyStateMachineSlimeBoss>() != null;
            DealDamageToAnyEnemy(enemyCollider, boss ? holySlashBossDamage : holySlashDamage, "Holy Slash");
        }
        BombGuy.HitAll(holySlashCollider.bounds);
        RoadFoe.HitAll(holySlashCollider.bounds, holySlashBossDamage, true);   // ARcade: the levels' own foes and bosses (a heavy blow)
        slashArea = holySlashCollider.bounds;
        hasSlashArea = true;

        DisableHolySlashCollider();
    }

    private void DisableHolySlashCollider()
    {
        if (holySlashCollider != null) holySlashCollider.enabled = false;
    }

    // ARcade: while a spell plays, nothing knocks her back or breaks into its
    // animation (Knockback; TriggerHurt already waits on abilities).
    private bool isCasting;

    // ------------------------------------------------------------
    // Unified damage dispatch — covers all three enemy script flavors.
    // Uses GetComponentInParent so child colliders still route correctly.
    // ------------------------------------------------------------
    private void DealDamageToAnyEnemy(Collider enemyCollider, int damage, string sourceLabel)
    {
        if (PlayerBuffs.Sword) damage *= 2;   // ARcade: Sword Buff
        var enemyClips = enemyCollider.GetComponentInParent<EnemyStateMachineClips>();
        if (enemyClips != null) { enemyClips.OnHit(damage); return; }

        var enemyPurpDragon = enemyCollider.GetComponentInParent<EnemyStateMachinePurpDragon>();
        if (enemyPurpDragon != null) { enemyPurpDragon.OnHit(damage); return; }

        var enemySlimeBoss = enemyCollider.GetComponentInParent<EnemyStateMachineSlimeBoss>();
        if (enemySlimeBoss != null)
        {
            // ARcade: Sword Buffed, three light attacks fell any Slime Boss.
            if (PlayerBuffs.Sword && sourceLabel == "Light Attack")
                damage = Mathf.Max(damage, Mathf.CeilToInt(enemySlimeBoss.MaxHealth / 3f));
            enemySlimeBoss.OnHit(damage);
            return;
        }

        var enemy = enemyCollider.GetComponentInParent<EnemyStateMachine>();
        if (enemy != null) { enemy.OnHit(damage); return; }

        var enemyMimic = enemyCollider.GetComponentInParent<EnemyStateMachineMIMIC>();
        if (enemyMimic != null) { enemyMimic.OnHit(damage); return; }

        Debug.LogWarning($"[{sourceLabel}] No compatible enemy script on {enemyCollider.name}");
    }

    // ============================================================
    // Chest Kick
    // ============================================================
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("CHEST") && !isKickingChest && !isDead && !isCasting)
        {
            kickChestRoutine = StartCoroutine(KickChestRoutine());
        }
    }

    /// ARcade: +1 when something solid is to the right (no walking right),
    /// -1 to the left, 0 for neither. Set each frame by EndlessKnightDirector.
    [System.NonSerialized] public int BlockedDirection;
    /// ARcade: where she can't go past (her position, world x) - the Slime Boss's near side.
    [System.NonSerialized] public float WallMin = float.NegativeInfinity, WallMax = float.PositiveInfinity;
    private const float WallSlack = 0.06f;

    /// ARcade: for the respawn (DeathRespawn).
    public bool IsDead => isDead;
    /// ARcade: her sword swings so far (each one a new number).
    public int SwingCount { get; private set; }
    private float swingStartedAt = -10f;
    /// ARcade: seconds into the swing playing now (-1: none) - its sword comes round in front at SwordLands.
    public float SwingAge => isAttacking ? Time.time - swingStartedAt : -1f;
    /// ARcade: when her sword, wound up behind her, slashes round in front (LightAtk's Frame_16).
    public const float SwordLands = 0.16f;
    /// ARcade: when her sword's no longer out in front (LightAtk's Frame_17, drawing it back): the blow's over.
    public const float SwordDone = 0.31f;
    /// ARcade: the stick pushed all the way (her sprint), and alive.
    public bool IsSprinting => !isDead && Mathf.Abs(PitInput.Move.x) > jogUpTo && Mathf.Abs(PitInput.Move.x) >= Mathf.Abs(PitInput.Move.y);

    // ARcade: how fast she creeps with the stick held mostly up or down (a slow walk).
    private const float CreepSpeed = 0.2f;

    /// ARcade: how fast she drops to the ground when killed in the air (world units per second).
    const float DeathFallSpeed = 5f;

    /// ARcade: kicks a chest (TreasureChest) - false if busy.
    public bool KickChest()
    {
        if (isKickingChest || isDead || isJumping || isAttacking || isUsingAbility) return false;
        kickChestRoutine = StartCoroutine(KickChestRoutine());
        return true;
    }

    private IEnumerator KickChestRoutine()
    {
        isKickingChest = true;
        PlayClipForState(PlayerClipState.KickChest);

        float clipLength = kickChestClip != null ? kickChestClip.length : 1f;
        yield return new WaitForSeconds(clipLength);

        isKickingChest = false;
        kickChestRoutine = null;
        currentClipIndex = -1; // force Idle to actually play even if we were already on it
        PlayClipForState(PlayerClipState.Idle);
    }
}
