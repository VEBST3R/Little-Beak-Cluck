using System;
using System.Collections;
using System.Reflection;
using LittleBeakCluck.Audio;
using LittleBeakCluck.Combat;
using LittleBeakCluck.Infrastructure;
using LittleBeakCluck.UI;
using UnityEngine;
using UnityEngine.Scripting;

namespace LittleBeakCluck.Enemies
{
    [RequireComponent(typeof(Animator), typeof(Rigidbody2D))]
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class EnemyBehaviour : MonoBehaviour, IDamageable
    {
        private const string PlayerTag = "Player";

        [SerializeField] private EnemyConfig config;
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private bool autoFindPlayer = true;
        [Header("Animation")]
        [SerializeField] private float animatorIdleSpeed = 1f;
        [SerializeField] private float animatorMaxMoveSpeed = 1.25f;
        [Header("Audio")]
        [SerializeField] private Renderer visibilityRenderer;

        public EnemyConfig Config => config;
        public Transform AttackAnchor => attackOrigin != null ? attackOrigin : transform;
        public Vector3 HudWorldOffset => config != null ? config.hudWorldOffset : Vector3.up * 1.5f;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => config != null ? config.maxHealth : 0f;
        public bool IsAlive => _currentHealth > 0f;

        public event Action<float, float> OnHealthChanged;
        public event Action OnDied;

        private Animator _animator;
        private Rigidbody2D _rigidbody;
        private Transform _player;
        private Collider2D[] _enemyColliders;
        private Collider2D[] _playerColliders;
        private bool _collisionsIgnored;

        private float _currentHealth;
        private Vector2 _desiredMoveDir;
        private float _targetSpeed;
        private bool _isAttacking;
        private bool _isKnockedBack;
        private float _lastAttackTime;
        private float _nextPlayerSearchTime;
        private int _attackLayerMask;
        private bool _playerInRange;
        private bool _revealSoundPlayed;

        protected static readonly int SpeedHash = Animator.StringToHash("Speed");
        protected static readonly int AttackHash = Animator.StringToHash("Attack");
        protected static readonly int HitHash = Animator.StringToHash("Hit");

        private int TargetLayerMask => _attackLayerMask;

        protected Animator Animator => _animator;
        protected Rigidbody2D Rigidbody => _rigidbody;
        protected Transform Player => _player;
        protected bool HasPlayer => _player != null;
        protected bool AutoFindPlayer => autoFindPlayer;
        protected bool IsKnockedBack => _isKnockedBack;
        protected bool IsCurrentlyAttacking => _isAttacking;
        protected float LastAttackTime
        {
            get => _lastAttackTime;
            set => _lastAttackTime = value;
        }
        protected float AttackCooldown => config != null ? config.attackCooldown : 0f;

        protected virtual void Awake()
        {
            if (config == null)
            {
                Debug.LogError($"[{name}] Enemy config missing", this);
                enabled = false;
                return;
            }

            _animator = GetComponent<Animator>();
            if (_animator != null)
            {
                _animator.speed = animatorIdleSpeed;
            }
            _rigidbody = GetComponent<Rigidbody2D>();
            _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            CacheEnemyColliders();
            _currentHealth = config.maxHealth;
            _attackLayerMask = config.attackLayer.value != 0 ? config.attackLayer.value : LayerMask.GetMask(PlayerTag);

            if (_attackLayerMask == 0)
            {
                Debug.LogWarning($"[{name}] Attack layer mask is zero. Assign a layer in {nameof(EnemyConfig)} or ensure a layer named '{PlayerTag}' exists.", this);
            }

            if (_rigidbody.bodyType != RigidbodyType2D.Dynamic)
            {
                _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            }

            if ((_rigidbody.constraints & RigidbodyConstraints2D.FreezeRotation) == 0)
            {
                _rigidbody.constraints |= RigidbodyConstraints2D.FreezeRotation;
            }

            if (visibilityRenderer == null)
            {
                visibilityRenderer = GetComponentInChildren<Renderer>();
            }
        }

        protected virtual void OnEnable()
        {
            if (config != null)
            {
                _currentHealth = config.maxHealth;
                OnHealthChanged?.Invoke(_currentHealth, config.maxHealth);
            }

            _collisionsIgnored = false;
            IgnoreCollisionsWithPlayer(false);
            EnemyHudController.Register(this);
            _revealSoundPlayed = false;
        }

        protected virtual void OnDisable()
        {
            IgnoreCollisionsWithPlayer(false);
            EnemyHudController.Unregister(this);
        }

        protected virtual void Start()
        {
            if (!TryAssignPlayer() && autoFindPlayer)
            {
                Debug.LogWarning($"[{name}] Player not found on start. Will keep searching.", this);
                _nextPlayerSearchTime = Time.time + 1f;
            }
        }

        protected virtual void Update()
        {
            TryPlayRevealSound();

            if (!IsAlive)
                return;

            if (_player == null && autoFindPlayer && Time.time >= _nextPlayerSearchTime)
            {
                if (!TryAssignPlayer())
                {
                    _nextPlayerSearchTime = Time.time + 1f;
                    return;
                }
            }

            if (_player == null)
                return;

            Vector2 directionToPlayer = _player.position - transform.position;
            _playerInRange = CheckPlayerInRange();

            if (_playerInRange && !_isAttacking && Time.time >= _lastAttackTime + config.attackCooldown)
            {
                BeginAttack();
            }

            if (_isAttacking)
            {
                _desiredMoveDir = Vector2.zero;
            }
            else
            {
                float raw = _playerInRange ? 0f : Mathf.Sign(directionToPlayer.x);
                _desiredMoveDir = new Vector2(raw, 0f);
            }

            _targetSpeed = _desiredMoveDir.x * config.maxMoveSpeed;
            UpdateAnimatorMovement();

            if (Mathf.Abs(directionToPlayer.x) > 0.01f)
            {
                HandleCharacterFlip(directionToPlayer.x);
            }
        }

        protected virtual void FixedUpdate()
        {
            if (!IsAlive || _isKnockedBack)
                return;

            float currentVelX = _rigidbody.linearVelocity.x;
            float accelRate = Mathf.Abs(_targetSpeed) > 0.01f ? config.moveAcceleration : config.moveDeceleration;
            float maxDelta = accelRate * Time.fixedDeltaTime;
            float newVelX = Mathf.MoveTowards(currentVelX, _targetSpeed, maxDelta);

            Vector2 velocity = _rigidbody.linearVelocity;
            velocity.x = Mathf.Clamp(newVelX, -config.maxMoveSpeed, config.maxMoveSpeed);
            _rigidbody.linearVelocity = velocity;
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive)
                return;

            _currentHealth -= info.Amount;
            OnHealthChanged?.Invoke(_currentHealth, config.maxHealth);
            // Always show damage number, even if this hit kills the enemy
            var hudForNumbers = GetComponentInChildren<WorldSpaceHealthBar>(true);
            var dmgMgr = LittleBeakCluck.UI.DamageNumbers.DamageNumbersManager.Instance;
            var color = dmgMgr.GetColorFor(info.WaveType);
            if (hudForNumbers != null && hudForNumbers.RectTransform != null)
            {
                dmgMgr.SpawnNumberAtAnchor(info.Amount, info.HitPoint, hudForNumbers.RectTransform, color);
            }
            else
            {
                dmgMgr.SpawnNumber(info.Amount, info.HitPoint, color);
            }

            if (!IsAlive)
            {
                HandleDeath();
                return;
            }

            _animator.SetTrigger(HitHash);

            if (info.KnockbackForce > 0f)
            {
                Vector2 direction = info.Direction.sqrMagnitude < 0.001f ? Vector2.left : info.Direction.normalized;
                StartCoroutine(ApplyKnockback(direction, info.KnockbackForce, info.TargetRigidbody));
            }
        }

        [Preserve]
        public void DealDamage()
        {
            if (!IsAlive)
                return;

            Physics2D.SyncTransforms();

            if (!_playerInRange && !CheckPlayerInRange())
                return;

            Vector2 origin = GetAttackOrigin();
            Collider2D[] hits;
            if (TargetLayerMask != 0)
            {
                hits = Physics2D.OverlapBoxAll(origin, config.attackBoxSize, 0f, TargetLayerMask);
            }
            else
            {
                hits = Physics2D.OverlapBoxAll(origin, config.attackBoxSize, 0f);
            }

            foreach (var hit in hits)
            {
                if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
                    continue;

                if (hit.TryGetComponent(out IDamageable damageable))
                {
                    Vector2 dir = (Vector2)transform.right;
                    var damageInfo = new DamageInfo
                    {
                        Amount = config.attackDamage,
                        HitPoint = hit.bounds.ClosestPoint(origin),
                        Direction = dir,
                        TargetRigidbody = hit.attachedRigidbody,
                        Source = this
                    };
                    damageable.TakeDamage(damageInfo);
                    PlayAttackHitSound();
                    break;
                }
            }
        }

        [Preserve]
        public void OnAttackAnimationFinished() => _isAttacking = false;

        protected virtual void BeginAttack()
        {
            _isAttacking = true;
            _lastAttackTime = Time.time;
            _animator.SetTrigger(AttackHash);
        }

        private IEnumerator ApplyKnockback(Vector2 direction, float force, Rigidbody2D targetRb)
        {
            _isKnockedBack = true;
            _targetSpeed = 0f;

            Rigidbody2D rb = targetRb != null ? targetRb : _rigidbody;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // Set initial knockback velocity (independent of mass)
            rb.linearVelocity = direction.normalized * force;

            // Make damping time-step independent and run on physics ticks
            const float baselineStep = 1f / 60f; // interpret config.dampen as per-60fps step
            float elapsed = 0f;
            while (elapsed < config.knockbackRecoverTime)
            {
                float dt = Time.fixedDeltaTime;
                // Convert per-60fps damp to current fixed step using exponential mapping
                float dampPerStep = Mathf.Pow(config.knockbackHorizontalDampen, dt / baselineStep);
                Vector2 v = rb.linearVelocity;
                v.x *= dampPerStep;
                rb.linearVelocity = v;
                elapsed += dt;
                yield return new WaitForFixedUpdate();
            }

            _isKnockedBack = false;
            _targetSpeed = 0f;
        }

        private void HandleDeath()
        {
            OnDied?.Invoke();
            _isAttacking = false;
            _isKnockedBack = false;
            _animator.enabled = false;

            // FIX: Safely remove from Deformation System to prevent WebGL batch corruption
            var skins = GetComponentsInChildren<UnityEngine.U2D.Animation.SpriteSkin>();
            foreach (var skin in skins)
            {
                if (skin != null)
                {
                    skin.enabled = false;
                }
            }

            IgnoreCollisionsWithPlayer(true);

            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.constraints = RigidbodyConstraints2D.None;
            _rigidbody.gravityScale = config.deathGravityScale;

            bool facingRight = transform.eulerAngles.y < 90f || transform.eulerAngles.y > 270f;
            Vector2 pushDir = new Vector2(facingRight ? 1f : -1f, 0.6f).normalized;
            _rigidbody.AddForce(pushDir * config.deathPushForce, ForceMode2D.Impulse);
            _rigidbody.AddTorque((facingRight ? -1f : 1f) * config.deathTorque, ForceMode2D.Impulse);

            Destroy(gameObject, config.deathDespawnDelay);
        }

        protected bool CheckPlayerInRange()
        {
            Vector2 origin = GetAttackOrigin();
            Collider2D hit;
            if (TargetLayerMask != 0)
            {
                hit = Physics2D.OverlapBox(origin, config.attackBoxSize, 0f, TargetLayerMask);
            }
            else
            {
                hit = Physics2D.OverlapBox(origin, config.attackBoxSize, 0f);
            }
            return hit != null;
        }

        protected Vector2 GetAttackOrigin()
        {
            Transform anchor = AttackAnchor;
            Vector3 basePos = anchor.position;
            Vector3 offset = anchor.right * config.attackBoxOffset.x + anchor.up * config.attackBoxOffset.y;
            Vector3 world = basePos + offset;
            return new Vector2(world.x, world.y);
        }

        protected bool TryAssignPlayer()
        {
            if (_player != null)
                return true;

            GameObject player = GameObject.FindGameObjectWithTag(PlayerTag);
            if (player == null)
                return false;

            _player = player.transform;
            CachePlayerColliders();

            if (_collisionsIgnored)
            {
                IgnoreCollisionsWithPlayer(true);
            }
            return true;
        }

        protected void TryPlayRevealSound()
        {
            if (_revealSoundPlayed)
                return;

            if (config == null)
            {
                _revealSoundPlayed = true;
                return;
            }

            if (config.revealSfx == null)
            {
                _revealSoundPlayed = true;
                return;
            }

            if (visibilityRenderer == null)
            {
                visibilityRenderer = GetComponentInChildren<Renderer>();
                if (visibilityRenderer == null)
                    return;
            }

            if (!visibilityRenderer.isVisible)
                return;

            var audio = ServiceLocator.Instance.Get<IAudioService>();
            audio?.PlaySfx(config.revealSfx);
            _revealSoundPlayed = true;
        }

        protected void PlayAttackHitSound()
        {
            if (config == null || config.attackHitSfx == null)
                return;

            var audio = ServiceLocator.Instance.Get<IAudioService>();
            audio?.PlaySfx(config.attackHitSfx);
        }

        protected void HandleCharacterFlip(float moveDirX)
        {
            float yRotation = moveDirX > 0f ? 0f : 180f;
            transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        }

        private void OnDrawGizmosSelected()
        {
            if (config == null)
                return;

            Transform anchor = AttackAnchor;
            Vector3 right = anchor.right;
            Vector3 up = anchor.up;
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Vector3 center = GetAttackOrigin();
            Gizmos.DrawWireCube(center, new Vector3(config.attackBoxSize.x, config.attackBoxSize.y, 0.01f));
            Gizmos.color = Color.red;
            Gizmos.DrawLine(center, center + right * (config.attackBoxSize.x * 0.5f));
        }

        private void CacheEnemyColliders()
        {
            _enemyColliders = GetComponentsInChildren<Collider2D>(true);
        }

        private void CachePlayerColliders()
        {
            if (_player == null)
            {
                _playerColliders = Array.Empty<Collider2D>();
                return;
            }

            _playerColliders = _player.GetComponentsInChildren<Collider2D>(true);

            if (_attackLayerMask == 0 && _playerColliders.Length > 0)
            {
                foreach (var playerCollider in _playerColliders)
                {
                    if (playerCollider == null)
                        continue;

                    _attackLayerMask |= 1 << playerCollider.gameObject.layer;
                }
            }
        }

        private void IgnoreCollisionsWithPlayer(bool shouldIgnore)
        {
            if (_enemyColliders == null || _enemyColliders.Length == 0)
            {
                CacheEnemyColliders();
            }

            if (_player == null)
            {
                TryAssignPlayer();
            }

            if (_playerColliders == null || _playerColliders.Length == 0)
            {
                CachePlayerColliders();
            }

            if (_enemyColliders == null || _playerColliders == null)
                return;

            foreach (var enemyCollider in _enemyColliders)
            {
                if (enemyCollider == null)
                    continue;

                foreach (var playerCollider in _playerColliders)
                {
                    if (playerCollider == null)
                        continue;

                    Physics2D.IgnoreCollision(enemyCollider, playerCollider, shouldIgnore);
                }
            }

            _collisionsIgnored = shouldIgnore;
        }

        private void UpdateAnimatorMovement()
        {
            if (_animator == null)
                return;

            if (_isAttacking)
            {
                _animator.SetFloat(SpeedHash, 0f);
                _animator.speed = animatorIdleSpeed;
                return;
            }

            float horizontalSpeed = GetCurrentHorizontalSpeed();
            float maxSpeed = config != null ? Mathf.Max(0.01f, config.maxMoveSpeed) : 0.01f;
            float normalizedSpeed = Mathf.Clamp01(horizontalSpeed / maxSpeed);

            _animator.SetFloat(SpeedHash, normalizedSpeed);
            float targetAnimSpeed = Mathf.Lerp(animatorIdleSpeed, animatorMaxMoveSpeed, normalizedSpeed);
            _animator.speed = Mathf.Max(animatorIdleSpeed, targetAnimSpeed);
        }

        private float GetCurrentHorizontalSpeed()
        {
            if (_rigidbody != null)
            {
                return Mathf.Abs(_rigidbody.linearVelocity.x);
            }

            if (config != null)
            {
                return Mathf.Abs(_desiredMoveDir.x) * config.maxMoveSpeed;
            }

            return Mathf.Abs(_desiredMoveDir.x);
        }
    }
}
