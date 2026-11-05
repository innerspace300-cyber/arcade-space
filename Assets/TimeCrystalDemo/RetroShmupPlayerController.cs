using System.Collections;
using UnityEngine;

namespace TimeCrystal
{
    public class RetroShmupPlayerController : MonoBehaviour
    {
        [Header("Space Movement Settings")]
        public float acceleration = 25f;
        public float deceleration = 5f;
        public float maxSpeed = 8f;
        [Tooltip("ARcade: the ship's speed, world units a second (the picture is 9 wide).")]
        public float ArcadeSpeed = 4.5f;

        [Header("Dash Settings")]
        public float dashSpeed = 12f;
        public float dashDuration = 0.2f;
        public float doubleTapTime = 0.25f;

        [Header("Dash Reversal Toggles")]
        public bool reverseForwardBackwardDashProjectiles = false;
        public bool reverseLeftRightDashProjectiles = false;

        [Header("Dash VFX Prefabs")]
        public GameObject dashVFXLeft;
        public GameObject dashVFXRight;
        public GameObject dashVFXForward;
        public GameObject dashVFXBackward;

        [Header("Dash VFX Spawn Points")]
        public Transform dashVFXLeftPoint;
        public Transform dashVFXRightPoint;
        public Transform dashVFXForwardPoint;
        public Transform dashVFXBackwardPoint;

        public float dashProjectileSpeed = 10f;
        public AudioSource dashAudio;

        [Header("Projectile J")]
        public Transform firePointJ;
        public GameObject projectilePrefabJ;
        public float fireRateJ = 0.3f;
        public float projectileSpeedJ = 10f;
        public AudioSource shootAudioJ;

        [Header("Projectile K")]
        public Transform firePointK;
        public GameObject projectilePrefabK;
        public float fireRateK = 0.2f;
        public float projectileSpeedK = 14f;
        public AudioSource shootAudioK;

        [Header("Projectile L")]
        public Transform firePointL;
        public GameObject projectilePrefabL;
        public float fireRateL = 0.1f;
        public float projectileSpeedL = 20f;
        public AudioSource shootAudioL;

        [Header("Health Settings")]
        public int maxHealth = 8;
        private int currentHealth;

        [Header("Health Bar")]
        [Tooltip("SpriteRenderer for your health bar. frames[0] should be FULL, last should be EMPTY.")]
        public SpriteRenderer healthBarSpriteRenderer;
        public Sprite[] healthBarFrames;

        [Header("Hit/Death VFX")]
        public GameObject hitVFX;
        public Transform hitVFXSpawnPoint;
        public GameObject deathVFX;

        [Header("Lives & Respawn")]
        [Tooltip("Optional: plug in life icon GameObjects (e.g., ship icons). One is disabled per death.")]
        public GameObject[] lifeIcons;
        [Tooltip("Used only if no lifeIcons are provided.")]
        public int extraLivesIfNoIcons = 3;
        [Tooltip("Where the player reappears. If null, uses the starting position.")]
        public Transform respawnPoint;
        [Tooltip("Delay before the player reappears after dying.")]
        public float respawnDelay = 1.5f;
        [Tooltip("How long the player is invincible after respawning.")]
        public float invincibilityDuration = 2.5f;
        [Tooltip("How long it takes the player sprite to fade from 0 -> 1 alpha on respawn.")]
        public float fadeInDuration = 1.0f;
        [Tooltip("Blink frequency while invincible (times per second).")]
        public float flashFrequency = 10f;

        [Header("Optional Audio")]
        public AudioSource damageAudio;
        public AudioSource deathAudio;
        public AudioSource respawnAudio;

        private float fireTimerJ;
        private float fireTimerK;
        private float fireTimerL;

        private bool isDashing = false;
        private Vector2 dashDirection;

        private float lastTapLeftTime = -1f;
        private float lastTapRightTime = -1f;
        private float lastTapUpTime = -1f;
        private float lastTapDownTime = -1f;

        private Rigidbody2D rb;
        private Collider2D col;
        private SpriteRenderer playerSprite;

        private int currentLives;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        private bool canControl = true;
        private bool isInvincible = false;
        private bool isRespawning = false;
        private Color baseSpriteColor;

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.interpolation = RigidbodyInterpolation2D.None;   // (it's moved by hand each frame: HandleMovement)
            col = GetComponent<Collider2D>();
            playerSprite = GetComponentInChildren<SpriteRenderer>();
            if (playerSprite == null)
            {
                Debug.LogWarning("RetroShmupPlayerController: No SpriteRenderer found on player or its children. Flash/fade won't be visible.");
            }
            else
            {
                baseSpriteColor = playerSprite.color;
            }

            currentHealth = maxHealth;
            UpdateHealthBarSprite();

            // Lives setup
            currentLives = (lifeIcons != null && lifeIcons.Length > 0) ? lifeIcons.Length : Mathf.Max(1, extraLivesIfNoIcons);

            // Save initial transform for fallback respawn
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        void Update()
        {
            if (!canControl) return;

            HandleMovement();
            HandleFiring();
            // ARcade: no dash for now (B and C will be a bomb).
        }

        void FixedUpdate()
        {
            if (isDashing)
            {
                rb.linearVelocity = dashDirection * dashSpeed;
            }
            KeepOnScreen();
        }

        // ARcade: the ship stays inside the picture (the original let it fly off).
        const float EdgeMargin = 0.45f;
        /// How much slower it goes while the fire or laser button's held.
        const float FocusSlow = 3f;   // (8.2 down to 5.2)
        /// ...and out of the HUD along the bottom: its parts' rects, in world
        /// space (TcStageDirector.LayoutHud). Pushed out the shortest way -
        /// up, or back sideways - never down.
        public static Rect[] HudBlocks;
        /// Where the scores along the top start (world y; TcStageDirector.LayoutHud) - the boss's bar stays under it.
        public static float HudCeiling = float.MaxValue;

        static Vector2 Kept(Vector2 p)
        {
            var f = TcWorld.Field;
            if (f.width <= 0f) return p;
            p = new Vector2(Mathf.Clamp(p.x, f.xMin + EdgeMargin, f.xMax - EdgeMargin), Mathf.Clamp(p.y, f.yMin + EdgeMargin, f.yMax - EdgeMargin));
            if (HudBlocks == null) return p;
            for (int pass = 0; pass < 2; pass++)
                foreach (var block in HudBlocks)
                {
                    float l = block.xMin - EdgeMargin, r = block.xMax + EdgeMargin, t = block.yMax + EdgeMargin;
                    if (p.x <= l || p.x >= r || p.y >= t || p.y < block.yMin) continue;
                    float up = t - p.y;
                    float left = l > f.xMin + EdgeMargin ? p.x - l : float.MaxValue;
                    float right = r < f.xMax - EdgeMargin ? r - p.x : float.MaxValue;
                    if (up <= left && up <= right) p.y = t;
                    else if (left <= right) p.x = l;
                    else p.x = r;
                }
            return p;
        }

        void KeepOnScreen()
        {
            var f = TcWorld.Field;
            if (f.width <= 0f) return;
            Vector2 p = rb.position;
            Vector2 kept = Kept(p);
            if (kept == p) return;
            rb.position = kept;
            var v = rb.linearVelocity;
            if (kept.x != p.x) v.x = 0f;
            if (kept.y != p.y) v.y = 0f;
            rb.linearVelocity = v;
        }

        /// Flying and in control (not exploding or coming back): may set off a bomb.
        public bool CanAct => canControl && !isRespawning && currentLives > 0;

        /// Lives left (THE TIME CRYSTAL's HUD and game over).
        public int Lives => currentLives;
        public bool IsDead => currentLives <= 0;

        void HandleMovement()
        {
            if (isDashing) return;

            // ARcade: the joystick (TcWorld.Move) in place of the keyboard, and
            // tight arcade movement - full speed the moment it's pushed, a dead
            // stop when it's let go, no drift (the jam build accelerated and
            // slid like a spaceship).
            // Moved right here each frame, not by the physics step (which
            // lagged it a step, and interpolation another).
            // (Its length is how hard: the analog stick's push, TimeCrystalGame.)
            Vector2 input = Vector2.ClampMagnitude(TcWorld.Move, 1f);
            rb.linearVelocity = Vector2.zero;
            // Firing (guns or laser) it slows by FocusSlow - finer through the bullets; let go, full speed again.
            float speed = ArcadeSpeed - (TcWorld.Fire || TcWorld.Laser ? FocusSlow : 0f);
            Vector2 p = Kept((Vector2)transform.position + input * speed * Time.deltaTime);
            transform.position = new Vector3(p.x, p.y, transform.position.z);
            rb.position = p;
        }

        void HandleFiring()
        {
            fireTimerJ += Time.deltaTime;
            fireTimerK += Time.deltaTime;
            fireTimerL += Time.deltaTime;

            if (TcWorld.FireJ && fireTimerJ >= fireRateJ)
            {
                FireProjectile(projectilePrefabJ, firePointJ, projectileSpeedJ);
                GunSound(shootAudioJ);
                fireTimerJ = 0f;
            }

            if (TcWorld.FireK && fireTimerK >= fireRateK)
            {
                FireProjectile(projectilePrefabK, firePointK, projectileSpeedK);
                GunSound(shootAudioK);
                fireTimerK = 0f;
            }

            if (TcWorld.FireL && fireTimerL >= fireRateL)
            {
                FireProjectile(projectilePrefabL, firePointL, projectileSpeedL);
                GunSound(shootAudioL);
                fireTimerL = 0f;
            }
        }

        // ARcade: the three guns firing together are one sound - a volley
        // within GunGap of the last one stays quiet (TcWorld.PlayShot).
        const float GunGap = 0.09f;
        float _lastGunSound = -1f;
        void GunSound(AudioSource source)
        {
            if (!source || Time.time - _lastGunSound < GunGap) return;
            _lastGunSound = Time.time;
            TcWorld.PlayShot(source, GunGap);
        }

        void FireProjectile(GameObject prefab, Transform point, float speed)
        {
            if (prefab != null && point != null)
            {
                GameObject proj = TcWorld.Spawn(prefab, point.position, point.rotation);
                proj.AddComponent<TcOffField>();
                Rigidbody2D prb = proj.GetComponent<Rigidbody2D>();
                if (prb != null)
                    prb.linearVelocity = transform.up * speed;
            }
        }

        void HandleDashInput()
        {
            if (isDashing) return;


            if (TcWorld.LeftPressed)
            {
                if (Time.time - lastTapLeftTime < doubleTapTime)
                {
                    DashLeft();
                }
                lastTapLeftTime = Time.time;
            }

            if (TcWorld.RightPressed)
            {
                if (Time.time - lastTapRightTime < doubleTapTime)
                {
                    DashRight();
                }
                lastTapRightTime = Time.time;
            }

            if (TcWorld.UpPressed)
            {
                if (Time.time - lastTapUpTime < doubleTapTime)
                {
                    DashUp();
                }
                lastTapUpTime = Time.time;
            }

            if (TcWorld.DownPressed)
            {
                if (Time.time - lastTapDownTime < doubleTapTime)
                {
                    DashDown();
                }
                lastTapDownTime = Time.time;
            }
        }

        void DashLeft()
        {
            StartCoroutine(PerformDash(Vector2.left));
            Vector2 dir = reverseLeftRightDashProjectiles ? Vector2.right : Vector2.left;
            SpawnDashProjectile(dashVFXLeft, dashVFXLeftPoint, dir);
        }

        void DashRight()
        {
            StartCoroutine(PerformDash(Vector2.right));
            Vector2 dir = reverseLeftRightDashProjectiles ? Vector2.left : Vector2.right;
            SpawnDashProjectile(dashVFXRight, dashVFXRightPoint, dir);
        }

        void DashUp()
        {
            StartCoroutine(PerformDash(Vector2.up));
            Vector2 dir = reverseForwardBackwardDashProjectiles ? Vector2.down : Vector2.up;
            SpawnDashProjectile(dashVFXForward, dashVFXForwardPoint, dir);
        }

        void DashDown()
        {
            StartCoroutine(PerformDash(Vector2.down));
            Vector2 dir = reverseForwardBackwardDashProjectiles ? Vector2.up : Vector2.down;
            SpawnDashProjectile(dashVFXBackward, dashVFXBackwardPoint, dir);
        }

        IEnumerator PerformDash(Vector2 direction)
        {
            isDashing = true;
            dashDirection = direction;

            TcWorld.PlayShot(dashAudio);

            yield return new WaitForSeconds(dashDuration);
            isDashing = false;
        }

        void SpawnDashProjectile(GameObject prefab, Transform spawnPoint, Vector2 direction)
        {
            if (prefab != null && spawnPoint != null)
            {
                GameObject dashFX = TcWorld.Spawn(prefab, spawnPoint.position, Quaternion.identity);
                dashFX.AddComponent<TcOffField>();
                Rigidbody2D r2d = dashFX.GetComponent<Rigidbody2D>();
                if (r2d != null)
                    r2d.linearVelocity = direction.normalized * dashProjectileSpeed;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (isInvincible || isRespawning) return;

            if (other.CompareTag("ENEMY-BULLET"))
            {
                // ARcade: the bullet bursts where it struck - a crunch and a
                // pixel explosion - then the damage.
                Vector3 at = other.transform.position;
                TcFx.Explode(at, "explosion_small", 1.04f, TcFx.Violet, 18);
                TcFx.NeonBurst(at, 0.78f);
                TcSfx.Play("player_hit", 1f, 0.05f);
                // The whole picture flashes red: you can't miss being hit.
                TcFx.ScreenFlash(new Color(1f, 0.1f, 0.15f), 0.45f, 0.3f);
                TakeDamage(1);
                // ARcade: a pooled orb (TcDanmaku) goes back for reuse; anything else is gone.
                if (other.GetComponent<TcBullet>()) other.gameObject.SetActive(false);
                else Destroy(other.gameObject);
            }
        }

        void TakeDamage(int damage)
        {
            if (isInvincible || isRespawning) return;

            currentHealth -= damage;
            currentHealth = Mathf.Max(currentHealth, 0);

            UpdateHealthBarSprite();
            // ARcade: the ship and the portrait flash white and shake (TcDamageFlash).
            if (TryGetComponent<TcDamageFlash>(out var flash)) flash.Hit();
            SpatialEmulator.Haptics.Play(currentHealth <= 0 ? SpatialEmulator.Haptics.Kind.Error : SpatialEmulator.Haptics.Kind.Heavy);   // (the phone, and the controller)

            if (hitVFX != null && hitVFXSpawnPoint != null)
            {
                TcWorld.SpawnEffect(hitVFX, hitVFXSpawnPoint.position, Quaternion.identity);
            }
            if (damageAudio) damageAudio.PlayOneShot(damageAudio.clip);

            if (currentHealth <= 0)
            {
                HandleDeath();
            }
        }

        void HandleDeath()
        {
            // Play death VFX/audio now
            if (deathVFX != null)
            {
                // ARcade: bigger than it was (1.3x), with TC's own big blast and a louder bang.
                var fx = TcWorld.SpawnEffect(deathVFX, transform.position, Quaternion.identity);
                fx.transform.localScale *= 1.3f;
            }
            TcFx.Explode(transform.position, "white_blast", 1.56f, TcFx.White, 40, 18f);
            TcFx.Explode(transform.position, "explosion_large", 1.3f, TcFx.Orange, 45);
            TcFx.NeonBurst(transform.position, 2.6f);
            if (deathAudio) deathAudio.PlayOneShot(deathAudio.clip);
            TcSfx.Play("player_death", 1f, 0.1f);

            // Consume a life and update UI
            currentLives = Mathf.Max(0, currentLives - 1);
            UpdateLifeIconsUI();

            if (currentLives > 0)
            {
                // Safe respawn sequence
                StartCoroutine(RespawnRoutine());
            }
            else
            {
                // No lives left: CONTINUE? (TimeCrystalGame: a coin goes on
                // from here; otherwise a new game).
                canControl = false;
                if (col) col.enabled = false;
                _hiddenAtGameOver.Clear();
                foreach (var r in GetComponentsInChildren<SpriteRenderer>())
                    if (r.enabled) { _hiddenAtGameOver.Add(r); r.enabled = false; }
            }
        }

        readonly System.Collections.Generic.List<SpriteRenderer> _hiddenAtGameOver = new System.Collections.Generic.List<SpriteRenderer>();

        /// ARcade: a coin at CONTINUE? - `lives` more, right where it went
        /// down (boss fights too), full health, blinking a moment.
        public void Continue(int lives)
        {
            int most = (lifeIcons != null && lifeIcons.Length > 0) ? lifeIcons.Length : lives;
            currentLives = Mathf.Clamp(lives, 1, most);
            UpdateLifeIconsUI();
            currentHealth = maxHealth;
            UpdateHealthBarSprite();
            foreach (var r in _hiddenAtGameOver) if (r) r.enabled = true;
            _hiddenAtGameOver.Clear();
            if (playerSprite)
            {
                playerSprite.enabled = true;
                playerSprite.color = new Color(baseSpriteColor.r, baseSpriteColor.g, baseSpriteColor.b, 1f);
            }
            rb.linearVelocity = Vector2.zero;
            if (col) col.enabled = true;
            canControl = true;
            isRespawning = false;
            StartCoroutine(ContinueRoutine());
        }

        IEnumerator ContinueRoutine()
        {
            isInvincible = true;
            if (respawnAudio) respawnAudio.PlayOneShot(respawnAudio.clip);
            var blink = StartCoroutine(BlinkWhileInvincible());
            yield return new WaitForSeconds(invincibilityDuration);
            isInvincible = false;
            if (blink != null) StopCoroutine(blink);
            if (playerSprite) playerSprite.enabled = true;
        }

        IEnumerator RespawnRoutine()
        {
            isRespawning = true;
            canControl = false;
            isInvincible = true;

            // Stop movement & disable collision
            rb.linearVelocity = Vector2.zero;
            if (col) col.enabled = false;

            // Make sprite invisible instantly (alpha 0) during delay
            if (playerSprite)
            {
                Color c = playerSprite.color;
                c.a = 0f;
                playerSprite.color = c;
                playerSprite.enabled = true; // ensure it renders when we fade back
            }

            // Wait out the respawn delay off-screen/invisible
            yield return new WaitForSeconds(respawnDelay);

            // Move to spawn point (or initial)
            Transform targetPoint = (respawnPoint != null) ? respawnPoint : null;
            transform.position = targetPoint ? targetPoint.position : initialPosition;
            transform.rotation = targetPoint ? targetPoint.rotation : initialRotation;

            // Refill health and bar immediately upon reappear
            currentHealth = maxHealth;
            UpdateHealthBarSprite();

            // Optional respawn audio cue
            if (respawnAudio) respawnAudio.PlayOneShot(respawnAudio.clip);

            // Fade-in & blinking while invincible
            float invincibleTimeRemaining = invincibilityDuration;
            float fadeTimeRemaining = Mathf.Max(0.01f, fadeInDuration);

            // Start blinking while invincible
            Coroutine blinkCo = StartCoroutine(BlinkWhileInvincible());

            // Fade alpha 0 -> 1 smoothly
            if (playerSprite)
            {
                float t = 0f;
                while (t < fadeTimeRemaining)
                {
                    t += Time.deltaTime;
                    float a = Mathf.Clamp01(t / fadeTimeRemaining);
                    Color c = playerSprite.color;
                    c = new Color(baseSpriteColor.r, baseSpriteColor.g, baseSpriteColor.b, a);
                    playerSprite.color = c;
                    yield return null;
                }
                // Ensure final alpha = 1
                Color finalC = playerSprite.color;
                finalC.a = 1f;
                playerSprite.color = finalC;
            }

            // Re-enable collider now that we're visible
            if (col) col.enabled = true;

            // Restore control but keep invincibility for the remainder
            canControl = true;

            // Finish invincibility window
            float elapsed = 0f;
            while (elapsed < invincibleTimeRemaining)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            isInvincible = false;

            // Stop blinking and ensure sprite is visible
            if (blinkCo != null) StopCoroutine(blinkCo);
            if (playerSprite) playerSprite.enabled = true;

            isRespawning = false;
        }

        IEnumerator BlinkWhileInvincible()
        {
            // If no sprite, nothing to blink
            if (!playerSprite) yield break;

            // Blink at flashFrequency while isInvincible == true
            float interval = (flashFrequency <= 0f) ? 0.1f : 1f / flashFrequency;

            // Small protection to avoid zero/NaN
            interval = Mathf.Max(0.03f, interval);

            while (isInvincible)
            {
                playerSprite.enabled = !playerSprite.enabled;
                yield return new WaitForSeconds(interval);
            }

            // Ensure visible at the end
            playerSprite.enabled = true;
        }

        void UpdateHealthBarSprite()
        {
            if (healthBarSpriteRenderer == null || healthBarFrames == null || healthBarFrames.Length == 0) return;

            // frames[0] = FULL, last = EMPTY (damage-based index)
            int damageTaken = Mathf.Clamp(maxHealth - currentHealth, 0, healthBarFrames.Length - 1);
            healthBarSpriteRenderer.sprite = healthBarFrames[damageTaken];
        }

        void UpdateLifeIconsUI()
        {
            if (lifeIcons == null || lifeIcons.Length == 0) return;

            // Simple rule: enable the first 'currentLives' icons, disable the rest
            for (int i = 0; i < lifeIcons.Length; i++)
            {
                if (lifeIcons[i] != null)
                    lifeIcons[i].SetActive(i < currentLives);
            }
        }
    }
}
