using System.Collections;
using UnityEngine;
using UnityEngine.Serialization; // Needed for FormerlySerializedAs

public class PlayerScriptAITHNE : MonoBehaviour
{
    // ===========================
    // === Player State Flags ===
    // ===========================
    private bool isJumping = false;
    private bool isAttacking = false;
    private bool isUsingAbility = false;
    private bool isDead = false;
    private bool isHurt = false;
    private bool isHolySlashing = false; // New flag for Holy Slash state

    // ===========================
    // === Adjustable Parameters ===
    // ===========================
    [Header("Movement Parameters")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpDuration = 1.0f;
    [SerializeField] private float jumpHeight = 5f;
    [SerializeField] private float jumpDistance = 5f;
    [SerializeField] private int maxJumps = 2;                  // Maximum number of jumps before cooldown
    [SerializeField] private float jumpCooldownTime = 3f;       // Cooldown duration after max jumps

    [Header("Attack & Ability Parameters")]
    [SerializeField] private float attackDelay = 0.3f;
    [SerializeField] private float holySlashDelay = 0.3f;
    [SerializeField] private float holySlashColliderEnableDelay = 0.2f;

    [FormerlySerializedAs("greatHealDelay")]
    [SerializeField] private float thunderboltDelay = 0.3f;     // Renamed from greatHealDelay

    [FormerlySerializedAs("healAmount")]
    //[SerializeField] private int healAmount = 20;            // Removed

    [SerializeField] private float hurtDelay = 0.5f;            // Reintroduced hurtDelay

    [SerializeField] private int mpDecreaseAmount = 10;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private int holySlashDamage = 20;

    // ===========================
    // === Component References ===
    // ===========================
    private Rigidbody rb;
    // ARcade: the touch joystick is replaced by PitInput.Move (ARcade controls).
    public Animator animator;
    [SerializeField] private BoxCollider attackCollider;
    [SerializeField] private Vector3 colliderOffset = new Vector3(1.0f, 0.0f, 0.0f);
    [SerializeField] private BoxCollider holySlashCollider;
    [SerializeField] private Vector3 holySlashColliderOffset = new Vector3(1.5f, 0.0f, 0.0f);
    public StatsControl statsControl;
    public PlayerStats playerStats;
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    // ===========================
    // === Audio Sources ===
    // ===========================
    [Header("Audio Sources")]
    [FormerlySerializedAs("healSound1")]
    [SerializeField] private AudioSource thunderboltSound1;     // Renamed from healSound1

    [FormerlySerializedAs("healSound2")]
    [SerializeField] private AudioSource thunderboltSound2;     // Renamed from healSound2

    [SerializeField] private AudioSource attackSound;
    [SerializeField] private AudioSource holySlashSound;
    [SerializeField] private AudioSource jumpSound;
    [SerializeField] private AudioSource hurtSound;
    [SerializeField] private AudioSource deathSound;

    // ===========================
    // === Animation States ===
    // ===========================
    [Header("Animation States")]
    [SerializeField] private string idleAnimation = "Idle";
    [SerializeField] private string runAnimation = "Running";
    [SerializeField] private string jumpAnimation = "Jump&Fall";
    [SerializeField] private string attackAnimation = "LightAtk";
    [SerializeField] private string holySlashAnimation = "HolySlash";

    [FormerlySerializedAs("greatHealAnimation")]
    [SerializeField] private string thunderboltAnimation = "Thunderbolt"; // Renamed from greatHealAnimation

    //[SerializeField] private string greatHealAnimation = "GreatHeal"; // Removed
    [SerializeField] private string deathAnimation = "Death";
    [SerializeField] private string hurtAnimation = "Hurt";

    // ===========================
    // === Additional Animation States ===
    // ===========================
    [Header("Additional Animation States")]
    [SerializeField] private string lightningAnimation = "Lightning"; // Name of the Lightning Animation state
    [SerializeField] private string lightningLayerName = "LightningLayer"; // Name of the Animator layer for Lightning

    // ===========================
    // === Lightning Strike References ===
    // ===========================
    [Header("Lightning Strike")]
    [SerializeField] private GameObject lightningStrikeObject; // Reference to the LightningStrike GameObject
    [SerializeField] private Animator lightningStrikeAnimator; // Reference to the LightningStrike Animator

    // ===========================
    // === Internal Flags ===
    // ===========================
    private bool playFirstThunderboltSound = true; // Renamed from playFirstHealSound
    //[SerializeField] private bool playFirstHealSound = true; // Removed
    private float initialYPosition;         // To store the initial ground Y-position
    private float jumpMultiplier = 1f;      // Multiplier for jump height on subsequent jumps

    // ===========================
    // === Jump Variables ===
    // ===========================
    private Vector3 startJumpPosition;
    private Vector3 targetJumpPosition;
    private float jumpTimer = 0f;
    private int jumpCount = 0;

    // ===========================
    // === Jump Cooldown Variables ===
    // ===========================
    private bool isJumpOnCooldown = false;
    private float jumpCooldownTimer = 0f;

    // ===========================
    // === Missing Variable Declarations ===
    // ===========================
    private int lastHealth;                  // Tracks the player's last known health
    private bool isFacingRight = true;      // Tracks the player's facing direction
    private string currentAnimation;        // Tracks the current animation state

    // ===========================
    // === Unity Methods ===
    // ===========================
    void Start()
    {
        // Initialize Rigidbody
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; // Gravity is handled manually
        rb.constraints = RigidbodyConstraints.FreezeRotation; // Allow Y movement

        // Store the initial Y position
        initialYPosition = transform.position.y;

        // Initialize health
        if (playerStats != null)
        {
            jumpCount = 0;
            lastHealth = playerStats.health;
        }

        // Disable colliders at start
        if (attackCollider != null) attackCollider.enabled = false;
        if (holySlashCollider != null) holySlashCollider.enabled = false;

        // Ensure LightningStrike is disabled initially
        if (lightningStrikeObject != null)
        {
            lightningStrikeObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("LightningStrike GameObject is not assigned in the Inspector.");
        }
    }

    void Update()
    {
        // Handle health changes
        if (playerStats != null && playerStats.health < lastHealth)
        {
            TriggerHurt();
            lastHealth = playerStats.health;
        }

        // Handle player death
        if (playerStats != null && playerStats.health <= 0 && !isDead)
        {
            TriggerDeath();
            return; // Exit Update to prevent further actions
        }

        // Handle player movement and idle state
        if (!isDead && !isAttacking && !isUsingAbility && !isHurt)
        {
            float xAxis = PitInput.Move.x;
            float zAxis = PitInput.Move.y;

            // Debug log for joystick input

            // Check for movement input
            if (Mathf.Abs(xAxis) > 0.1f || Mathf.Abs(zAxis) > 0.1f)
            {
                ChangeAnimationState(runAnimation);
                FlipPlayer(xAxis);

                Vector3 move = new Vector3(xAxis, 0, zAxis) * moveSpeed * Time.deltaTime;
                rb.MovePosition(transform.position + move);
            }
            else if (!isJumping && !isHolySlashing)
            {
                ChangeAnimationState(idleAnimation);
            }
        }

        // Handle jumping movement
        if (isJumping)
        {
            PerformArcJump();
        }

        // Handle jump cooldown timer
        if (isJumpOnCooldown)
        {
            jumpCooldownTimer -= Time.deltaTime;
            if (jumpCooldownTimer <= 0f)
            {
                isJumpOnCooldown = false;
                jumpCooldownTimer = 0f;
                jumpCount = 0; // Reset jump count after cooldown
                Debug.Log("Jump cooldown finished.");
            }
        }

        // Adjust sprite sorting order based on Y position
        AdjustSpriteSortingOrder();
    }

    // ===========================
    // === Jump Methods ===
    // ===========================
    /// <summary>
    /// Triggers the jump action.
    /// </summary>
    public void TriggerJump()
    {
        if (!isJumpOnCooldown && !isAttacking && !isUsingAbility && !isDead)
        {
            if (jumpCount < maxJumps)
            {
                // Initiate jump
                isJumping = true;
                jumpCount++;
                jumpTimer = 0f;

                // Increase jump height on subsequent jumps
                jumpMultiplier = 1f + (jumpCount - 1) * 0.5f; // Adjust multiplier as needed

                float xAxis = PitInput.Move.x;
                float zAxis = PitInput.Move.y;

                // Set start and target positions for the jump
                startJumpPosition = transform.position;
                targetJumpPosition = startJumpPosition + new Vector3(xAxis * jumpDistance, 0, zAxis * jumpDistance);

                // Ensure only rotation is frozen
                rb.constraints = RigidbodyConstraints.FreezeRotation;

                // Play jump animation and sound
                ChangeAnimationState(jumpAnimation);
                if (jumpSound != null)
                {
                    PitAudio.Play(jumpSound);
                }

                Debug.Log($"Jump triggered. Current jump count: {jumpCount}");
            }
        }
        else if (isJumpOnCooldown)
        {
            Debug.Log("Jump is on cooldown. Cannot jump.");
        }
    }

    /// <summary>
    /// Handles the jump movement in an arc.
    /// </summary>
    private void PerformArcJump()
    {
        jumpTimer += Time.deltaTime / jumpDuration;

        float totalHeight = jumpHeight * jumpMultiplier;

        // Calculate vertical position using a sine wave for smooth arc
        float curveY = Mathf.Sin(Mathf.PI * jumpTimer) * totalHeight;

        // Calculate horizontal movement
        Vector3 nextPosition = Vector3.Lerp(startJumpPosition, targetJumpPosition, jumpTimer);

        // Apply vertical movement, ensuring the jump ends at the initial ground Y-position
        nextPosition.y = Mathf.Lerp(startJumpPosition.y, initialYPosition, jumpTimer) + curveY;

        rb.MovePosition(nextPosition);

        // Check if jump duration is complete
        if (jumpTimer >= 1f)
        {
            isJumping = false;
            jumpTimer = 0f;

            // Reset position to initial Y-position
            Vector3 finalPosition = rb.position;
            finalPosition.y = initialYPosition;
            rb.MovePosition(finalPosition);

            // Start cooldown if maximum jumps are reached
            if (jumpCount >= maxJumps)
            {
                StartJumpCooldown();
            }
        }
    }

    /// <summary>
    /// Initiates the jump cooldown.
    /// </summary>
    private void StartJumpCooldown()
    {
        isJumpOnCooldown = true;
        jumpCooldownTimer = jumpCooldownTime;
        Debug.Log($"Jump cooldown started for {jumpCooldownTime} seconds.");
    }

    // ===========================
    // === Holy Slash Methods ===
    // ===========================
    /// <summary>
    /// Triggers the Holy Slash ability.
    /// </summary>
    public void TriggerHolySlash()
    {
        // Check if Holy Slash can be triggered
        if (!isUsingAbility && !isAttacking && !isDead)
        {
            StartCoroutine(HolySlashRoutine());
        }
    }

    /// <summary>
    /// Coroutine handling the Holy Slash sequence.
    /// </summary>
    private IEnumerator HolySlashRoutine()
    {
        isUsingAbility = true;

        // If the player is currently jumping, interrupt the jump
        if (isJumping)
        {
            isJumping = false;
            jumpTimer = 0f;
            jumpCount = Mathf.Max(0, jumpCount - 1); // Decrement jumpCount to allow further jumps
            Debug.Log("Jump interrupted by Holy Slash.");

            // Start Holy Slash animation and play sound immediately
            ChangeAnimationState(holySlashAnimation);
            if (holySlashSound != null)
            {
                PitAudio.Play(holySlashSound);
            }

            // Perform Holy Slash attack
            PerformHolySlashAttack();

            // Start descending immediately
            StartCoroutine(MoveDownToInitialY(0.5f)); // Adjust duration as needed to match animation

            // Wait for Holy Slash animation delay
            yield return new WaitForSeconds(holySlashDelay);

            // Holy Slash complete
            isUsingAbility = false;
        }
        else
        {
            // Start Holy Slash animation and play sound immediately
            ChangeAnimationState(holySlashAnimation);
            if (holySlashSound != null)
            {
                PitAudio.Play(holySlashSound);
            }

            // Perform Holy Slash attack
            PerformHolySlashAttack();

            // Start descending immediately
            StartCoroutine(MoveDownToInitialY(0.5f)); // Adjust duration as needed to match animation

            // Wait for Holy Slash animation delay
            yield return new WaitForSeconds(holySlashDelay);

            // Holy Slash complete
            isUsingAbility = false;
        }
    }

    /// <summary>
    /// Initiates the Holy Slash attack logic.
    /// </summary>
    private void PerformHolySlashAttack()
    {
        if (holySlashCollider != null)
        {
            Invoke("EnableHolySlashCollider", holySlashColliderEnableDelay);
        }
    }

    /// <summary>
    /// Enables the Holy Slash collider to detect enemies.
    /// </summary>
    private void EnableHolySlashCollider()
    {
        if (holySlashCollider != null)
        {
            Vector3 newPosition = transform.position + (isFacingRight ? holySlashColliderOffset : new Vector3(-holySlashColliderOffset.x, holySlashColliderOffset.y, holySlashColliderOffset.z));
            holySlashCollider.center = transform.InverseTransformPoint(newPosition);

            holySlashCollider.enabled = true;
            Debug.Log("Holy Slash collider enabled.");

            // Damage enemies shortly after enabling collider
            Invoke("DamageAllEnemiesInHolySlash", 0.05f);
        }
    }

    /// <summary>
    /// Damages all enemies within the Holy Slash collider.
    /// </summary>
    private void DamageAllEnemiesInHolySlash()
    {
        Collider[] hitEnemies = Physics.OverlapBox(
            holySlashCollider.bounds.center,
            holySlashCollider.bounds.extents,
            holySlashCollider.transform.rotation,
            LayerMask.GetMask("ENEMY"));

        Debug.Log("Number of enemies hit with Holy Slash: " + hitEnemies.Length);

        foreach (Collider enemyCollider in hitEnemies)
        {
            // Attempt to get EnemyStateMachine component
            var enemy = enemyCollider.GetComponent<EnemyStateMachine>();
            if (enemy != null)
            {
                Debug.Log("Damaging enemy with Holy Slash (EnemyStateMachine).");
                enemy.OnHit(holySlashDamage);
            }
            else
            {
                // Attempt to get EnemyStateMachineMIMIC component
                var enemyMimic = enemyCollider.GetComponent<EnemyStateMachineMIMIC>();
                if (enemyMimic != null)
                {
                    Debug.Log("Damaging enemy with Holy Slash (EnemyStateMachineMIMIC).");
                    enemyMimic.OnHit(holySlashDamage);
                }
                else
                {
                    Debug.LogWarning("No compatible EnemyStateMachine component found on collided object.");
                }
            }
        }

        // Disable the collider after dealing damage
        DisableHolySlashCollider();
    }

    /// <summary>
    /// Disables the Holy Slash collider.
    /// </summary>
    private void DisableHolySlashCollider()
    {
        if (holySlashCollider != null)
        {
            holySlashCollider.enabled = false;
            Debug.Log("Holy Slash collider disabled.");
        }
    }

    /// <summary>
    /// Coroutine to move the player downward back to the initial Y position after Holy Slash.
    /// </summary>
    private IEnumerator MoveDownToInitialY(float duration)
    {
        isHolySlashing = true;
        float elapsedTime = 0f;
        Vector3 startPosition = transform.position;
        Vector3 endPosition = new Vector3(startPosition.x, initialYPosition, startPosition.z);

        while (elapsedTime < duration)
        {
            Vector3 newPosition = Vector3.Lerp(startPosition, endPosition, elapsedTime / duration);
            rb.MovePosition(newPosition);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        rb.MovePosition(endPosition);
        isHolySlashing = false;
        Debug.Log("Player has moved down to initial Y position after Holy Slash.");
    }

    // ===========================
    // === Attack Methods ===
    // ===========================
    /// <summary>
    /// Triggers the Light Attack.
    /// </summary>
    public void TriggerAttack()
    {
        if (!isAttacking && !isJumping && !isUsingAbility && !isDead)
        {
            isAttacking = true;
            ChangeAnimationState(attackAnimation);
            if (attackSound != null)
            {
                PitAudio.Play(attackSound);
            }

            PerformTriggerAttack();

            Invoke("AttackComplete", attackDelay);
        }
    }

    /// <summary>
    /// Completes the attack by resetting the attack flag.
    /// </summary>
    private void AttackComplete()
    {
        isAttacking = false;
    }

    /// <summary>
    /// Performs the attack logic by enabling the attack collider.
    /// </summary>
    private void PerformTriggerAttack()
    {
        if (attackCollider != null)
        {
            Vector3 newPosition = transform.position + (isFacingRight ? colliderOffset : new Vector3(-colliderOffset.x, colliderOffset.y, colliderOffset.z));
            attackCollider.center = transform.InverseTransformPoint(newPosition);

            attackCollider.enabled = true;
            Debug.Log("Attack collider enabled.");

            DamageAllEnemiesInAttackCollider();
        }
    }

    /// <summary>
    /// Damages all enemies within the attack collider.
    /// </summary>
    private void DamageAllEnemiesInAttackCollider()
    {
        Collider[] hitEnemies = Physics.OverlapBox(
            attackCollider.bounds.center,
            attackCollider.bounds.extents,
            attackCollider.transform.rotation,
            LayerMask.GetMask("ENEMY"));

        Debug.Log("Number of enemies hit with Light Attack: " + hitEnemies.Length);

        foreach (Collider enemyCollider in hitEnemies)
        {
            // Attempt to get EnemyStateMachine component
            var enemy = enemyCollider.GetComponent<EnemyStateMachine>();
            if (enemy != null)
            {
                Debug.Log("Damaging enemy with Light Attack (EnemyStateMachine).");
                enemy.OnHit(attackDamage);
            }
            else
            {
                // Attempt to get EnemyStateMachineMIMIC component
                var enemyMimic = enemyCollider.GetComponent<EnemyStateMachineMIMIC>();
                if (enemyMimic != null)
                {
                    Debug.Log("Damaging enemy with Light Attack (EnemyStateMachineMIMIC).");
                    enemyMimic.OnHit(attackDamage);
                }
                else
                {
                    Debug.LogWarning("No compatible EnemyStateMachine component found on collided object.");
                }
            }
        }

        // Disable the collider after dealing damage
        DisableAttackCollider();
    }

    /// <summary>
    /// Disables the attack collider.
    /// </summary>
    private void DisableAttackCollider()
    {
        if (attackCollider != null)
        {
            attackCollider.enabled = false;
            Debug.Log("Attack collider disabled.");
        }
    }

    // ===========================
    // === Thunderbolt Methods ===
    // ===========================
    /// <summary>
    /// Triggers the Thunderbolt ability.
    /// </summary>
    public void TriggerThunderbolt()
    {
        if (!isUsingAbility && !isJumping && !isAttacking && playerStats != null && playerStats.mana >= 10 && !isDead)
        {
            isUsingAbility = true;
            ChangeAnimationState(thunderboltAnimation); // Renamed
            // The Lightning Strike will be triggered via Animation Event through the LightningStrikeHandler

            // Play alternating Thunderbolt sounds
            if (playFirstThunderboltSound)
            {
                // Replace with Thunderbolt sounds if available
                if (thunderboltSound1 != null) PitAudio.Play(thunderboltSound1); // Renamed from healSound1
            }
            else
            {
                if (thunderboltSound2 != null) PitAudio.Play(thunderboltSound2); // Renamed from healSound2
            }
            playFirstThunderboltSound = !playFirstThunderboltSound; // Toggle for next time

            Invoke("ThunderboltComplete", thunderboltDelay); // Renamed
            // Invoke("GreatHealComplete", greatHealDelay); // Removed
        }
    }

    /// <summary>
    /// Completes the Thunderbolt ability by reducing mana.
    /// </summary>
    private void ThunderboltComplete()
    {
        isUsingAbility = false;

        if (playerStats != null)
        {
            playerStats.mana -= mpDecreaseAmount;
        }
    }

    /// <summary>
    /// Triggers the Lightning Strike animation.
    /// This method is called via an Animation Event in the Thunderbolt animation through the LightningStrikeHandler.
    /// </summary>
    public void TriggerLightningStrike()
    {
        if (lightningStrikeAnimator != null && lightningStrikeObject != null)
        {
            // Enable the LightningStrike GameObject
            lightningStrikeObject.SetActive(true);

            // Play the Lightning Strike animation
            lightningStrikeAnimator.Play("LightningStrike");

            Debug.Log("Lightning Strike triggered.");

            // Optionally, disable the LightningStrike GameObject after the animation completes
            // Assuming the LightningStrike animation duration is known (e.g., 0.5 seconds)
            StartCoroutine(DisableLightningStrikeAfterAnimation(0.5f));
        }
        else
        {
            Debug.LogWarning("LightningStrike Animator or GameObject is not assigned.");
        }
    }

    /// <summary>
    /// Disables the LightningStrike GameObject after the animation completes.
    /// This coroutine is used if not using an Animation Event for deactivation.
    /// </summary>
    /// <param name="delay">Time in seconds to wait before disabling.</param>
    private IEnumerator DisableLightningStrikeAfterAnimation(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (lightningStrikeObject != null)
        {
            lightningStrikeObject.SetActive(false);
            Debug.Log("Lightning Strike disabled.");
        }
    }

    /// <summary>
    /// Disables the LightningStrike GameObject.
    /// This method can be called via an Animation Event at the end of the LightningStrike animation.
    /// </summary>
    public void DisableLightningStrike()
    {
        if (lightningStrikeObject != null)
        {
            lightningStrikeObject.SetActive(false);
            Debug.Log("Lightning Strike disabled via Animation Event.");
        }
        else
        {
            Debug.LogWarning("LightningStrike GameObject is not assigned.");
        }
    }

    // ===========================
    // === Hurt Methods ===
    // ===========================
    /// <summary>
    /// Triggers the Hurt state.
    /// </summary>
    private void TriggerHurt()
    {
        if (!isDead && !isHurt && !isJumping && !isAttacking && !isUsingAbility)
        {
            isHurt = true;
            ChangeAnimationState(hurtAnimation);

            if (hurtSound != null && !hurtSound.isPlaying)
            {
                hurtSound.pitch = Random.Range(0.9f, 1.1f);
                PitAudio.Play(hurtSound);
            }

            Invoke("HurtComplete", hurtDelay);
        }
    }

    /// <summary>
    /// Completes the Hurt state by resetting the hurt flag.
    /// </summary>
    private void HurtComplete()
    {
        isHurt = false;
    }

    // ===========================
    // === Death Methods ===
    // ===========================
    /// <summary>
    /// Triggers the Death state.
    /// </summary>
    private void TriggerDeath()
    {
        isDead = true;
        ChangeAnimationState(deathAnimation);
        rb.constraints = RigidbodyConstraints.FreezeAll; // Freeze all movement
        Debug.Log("Player has died.");

        if (deathSound != null && !deathSound.isPlaying)
        {
            PitAudio.Play(deathSound);
        }

        if (playerSpriteRenderer != null)
        {
            playerSpriteRenderer.sortingLayerName = "TopLayer";
            playerSpriteRenderer.sortingOrder = 10;
            Debug.Log($"Player's sorting layer set to 'TopLayer' with sorting order {playerSpriteRenderer.sortingOrder}");
        }
        else
        {
            Debug.LogWarning("PlayerSpriteRenderer is not assigned!");
        }
    }

    // ===========================
    // === Helper Methods ===
    // ===========================
    /// <summary>
    /// Flips the player sprite based on movement direction.
    /// </summary>
    /// <param name="xAxis">Horizontal input axis.</param>
    void FlipPlayer(float xAxis)
    {
        if (xAxis > 0 && !isFacingRight)
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            isFacingRight = true;
        }
        else if (xAxis < 0 && isFacingRight)
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            isFacingRight = false;
        }
    }

    /// <summary>
    /// Changes the player's animation state on the Base layer.
    /// </summary>
    /// <param name="newAnimation">Name of the new animation state.</param>
    void ChangeAnimationState(string newAnimation)
    {
        // Allow jump, Holy Slash, and Thunderbolt animations to interrupt and restart themselves
        if ((currentAnimation == newAnimation && newAnimation != jumpAnimation && newAnimation != holySlashAnimation && newAnimation != thunderboltAnimation)) return;

        // Play the animation on the Base layer (layer index 0)
        animator.Play(newAnimation, 0, 0f);

        currentAnimation = newAnimation;
    }

    /// <summary>
    /// Adjusts the sprite's sorting order based on the player's Z position.
    /// </summary>
    private void AdjustSpriteSortingOrder()
    {
        if (playerSpriteRenderer != null)
        {
            playerSpriteRenderer.sortingOrder = Mathf.FloorToInt(-transform.position.z * 10);
        }
    }
}