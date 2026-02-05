using System.Collections;
using System.Collections.Generic;
using Lean.Pool;
using LittleBeakCluck.Infrastructure;
using LittleBeakCluck.Player;
using LittleBeakCluck.Services;
using UnityEngine;

namespace LittleBeakCluck.Combat
{
    [RequireComponent(typeof(Collider2D))]
    [UnityEngine.Scripting.Preserve]
    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class VoiceWaveHit : MonoBehaviour
    {
        [Header("Wave Settings")]
        [SerializeField] private float _speed = 10f;
        [SerializeField] private float _maxLifetime = 2f;
        [SerializeField] private float _startScale = 0.15f;   // початковий радіус (масштаб)
        [SerializeField] private float _endScale = 0.44f;      // фінальний радіус (масштаб)
        [SerializeField] private float _knockbackForce = 5f;   // базова сила відкидання (швидкість, не impulse)
        [SerializeField] private float _knockbackUpward = 0.2f; // вертикальна добавка до відкидання (для відчуття удару)
        [SerializeField] private bool _knockbackOnlyX = true;   // штовхати лише по осі X (без вертикалі)
        [SerializeField] private bool _destroyOnHit = false;   // чи знищувати хвилю після першого влучання
        [SerializeField] private float _repeatHitCooldown = 0.25f; // затримка між повторними хітами по тій самій цілі
        [SerializeField] private bool _scaleDamageWithCharge = true;    // масштабувати шкоду від charge
        [SerializeField] private bool _scaleKnockbackWithCharge = true; // масштабувати відкидання від charge
        [SerializeField] private float _knockbackMinFactor = 0.4f;      // мінімальна частка сили (навіть при малому charge)
        [SerializeField] private float _knockbackExtraScale = 1.0f;     // множник зверху (для тонкої настройки)
        [SerializeField] private bool _debugKnockback = false;          // логування значень
        [Header("Physics Movement (optional)")]
        [SerializeField] private bool _useRigidbodyMove = true;         // рух через Rigidbody2D MovePosition
        [SerializeField] private bool _autoConfigureRigidbody = true;   // автоматично налаштувати Rigidbody2D якщо доданий
        [SerializeField] private bool _ensureTriggerCollider = true;    // гарантувати isTrigger
        [SerializeField] private float _debugGizmoRadius = 0.05f;

        private float _damage; // сирий charge
        private Vector3 _direction;
        private float _lifetimeTimer;
        private bool _isInitialized = false;
        private readonly Dictionary<int, float> _lastHitTimes = new(); // id цілі -> час останнього влучання
        private Rigidbody2D _rb;
        private Collider2D _col;
        private Coroutine _lifetimeRoutine;
        private bool _isDespawning;

        private VoiceWaveType _waveType = VoiceWaveType.Mid;
        private static IPlayerProgressService s_ProgressService;

        public float Speed => _speed;

        public void Initialize(float charge, Vector3 direction, VoiceWaveType waveType = VoiceWaveType.Mid)
        {
            _damage = charge;              // charge 0..1 (або вище) – використовується для масштабування
            _direction = direction.normalized;
            _waveType = waveType;
            _lifetimeTimer = 0f;
            _isDespawning = false;
            _lastHitTimes.Clear();

            // Початковий масштаб
            transform.localScale = Vector3.one * _startScale;

            // Орієнтація спрайта у напрямку руху (за замовчуванням спрайт дивиться вправо)
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            _isInitialized = true;
            if (_useRigidbodyMove)
            {
                if (_rb == null)
                {
                    _rb = GetComponent<Rigidbody2D>();
                    if (_rb == null)
                    {
                        _rb = gameObject.AddComponent<Rigidbody2D>();
                    }
                }
                if (_autoConfigureRigidbody && _rb != null)
                {
                    _rb.bodyType = RigidbodyType2D.Kinematic;
                    _rb.gravityScale = 0f;
                    _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                    _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                }

                if (_ensureTriggerCollider)
                {
                    _col = GetComponent<Collider2D>();
                    if (_col != null) _col.isTrigger = true;
                }
            }

            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }

            if (_lifetimeRoutine != null)
            {
                StopCoroutine(_lifetimeRoutine);
            }
            _lifetimeRoutine = StartCoroutine(LifetimeRoutine());
        }

        private void Update()
        {
            if (!_isInitialized) return;

            // Ріст протягом ВСЬОГО життєвого часу: 0.15 -> 0.44
            _lifetimeTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_lifetimeTimer / _maxLifetime);
            float currentScale = Mathf.Lerp(_startScale, _endScale, t);
            transform.localScale = Vector3.one * currentScale;
        }

        private void FixedUpdate()
        {
            if (!_isInitialized) return;

            float delta = Time.fixedDeltaTime;

            if (_useRigidbodyMove && _rb != null)
            {
                _rb.MovePosition(_rb.position + (Vector2)(_direction * (_speed * delta)));
            }
            else
            {
                transform.position += _direction * (_speed * delta);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Ігноруємо гравця (перевіряємо лише теги на самому об'єкті та на корені)
            if (other.CompareTag("Player") || other.transform.root.CompareTag("Player"))
            {
                return;
            }

            // Спробуємо нанести шкоду всьому що імплементує IDamageable (ворогам)
            // Визначаємо цільовий Rigidbody (найчастіше на корені ворога)
            Rigidbody2D targetRb = other.attachedRigidbody ? other.attachedRigidbody : other.GetComponentInParent<Rigidbody2D>();
            // Пробуємо знайти IDamageable саме на об'єкті, де RB (преференція до кореня ворога)
            IDamageable damageable = targetRb ? targetRb.GetComponent<IDamageable>() : null;
            // Якщо не знайшли на RB – шукаємо вгору від колайдера
            if (damageable == null)
                damageable = other.GetComponentInParent<IDamageable>();
            // Останній шанс – по всьому дереву рута
            if (damageable == null)
            {
                var root = other.transform.root;
                damageable = root ? root.GetComponentInChildren<IDamageable>() : null;
            }

            if (damageable != null && damageable.IsAlive)
            {
                // Ключ для анти-спаму по тій самій цілі: прив'язуємось до Rigidbody або кореня трансформа
                if (targetRb == null)
                {
                    // Спробуємо взяти RB із того об'єкта, де висить сам IDamageable
                    if (damageable is Component dc)
                        targetRb = dc.GetComponentInParent<Rigidbody2D>();
                }
                int instanceId = targetRb ? targetRb.GetInstanceID() : other.transform.root.GetInstanceID();
                float now = Time.time;
                if (_lastHitTimes.TryGetValue(instanceId, out var lastTime))
                {
                    if (now - lastTime < _repeatHitCooldown)
                        return; // ще зарано
                }
                _lastHitTimes[instanceId] = now;

                float finalDamage = _scaleDamageWithCharge ? Mathf.Max(1f, _damage * 10f) : _damage;
                finalDamage *= ResolveDamageMultiplier();
                float knockScale = _scaleKnockbackWithCharge ? Mathf.Clamp01(_damage) : 1f;
                // Формула (обмежена): базова * (minFactor + knockScale) * множник; клемимо верхню межу
                float baseKnock = _knockbackForce * (Mathf.Clamp01(_knockbackMinFactor) + knockScale) * _knockbackExtraScale;
                float finalKnock = Mathf.Clamp(baseKnock, 0f, 12f);
                // Напрямок нокбеку
                Vector2 knockDir;
                if (_knockbackOnlyX)
                {
                    // Тільки по осі X: визначаємо знак за напрямком хвилі
                    float signX = Mathf.Abs(_direction.x) > 0.0001f ? Mathf.Sign(_direction.x) : Mathf.Sign(transform.right.x);
                    if (signX == 0f) signX = 1f;
                    knockDir = new Vector2(signX, 0f);
                }
                else
                {
                    // Дозволяємо невелику вертикальну складову для «удару»
                    knockDir = _direction;
                    if (Mathf.Abs(knockDir.y) < 0.01f && _knockbackUpward > 0f)
                        knockDir.y += _knockbackUpward;
                    knockDir = knockDir.normalized;
                }

                // Перевірка на афінність ворога до певного типу хвилі
                var affinity = (damageable as Component)?.GetComponentInParent<EnemyWaveAffinity>();
                if (affinity != null && !affinity.IsEffective(_waveType))
                {
                    if (_debugKnockback)
                        Debug.Log($"[VoiceWaveHit] {other.name} is immune to {_waveType}, expects {affinity.EffectiveType}");
                    return; // хвиля цього типу не діє
                }

                var hitPoint = other.bounds.ClosestPoint(transform.position);
                var info = new DamageInfo
                {
                    Amount = finalDamage,
                    WaveType = _waveType,
                    HitPoint = hitPoint,
                    Direction = knockDir,
                    TargetRigidbody = targetRb,
                    // Інтерпретуємо як швидкість (linearVelocity), а не як імпульс, щоб бути незалежним від mass та платформи
                    KnockbackForce = finalKnock,
                    Source = this
                };
                damageable.TakeDamage(info);
                if (_debugKnockback)
                {
                    Debug.Log($"[VoiceWaveHit] Hit {other.name} dmg={finalDamage:F1} knock={finalKnock:F2} dir={knockDir} rb={(targetRb ? targetRb.name : "none")}");
                }

                if (_destroyOnHit)
                {
                    RequestDespawn();
                }
            }
            else
            {
                if (_debugKnockback)
                {
                    string rootName = other.transform.root ? other.transform.root.name : "<no-root>";
                    Debug.Log($"[VoiceWaveHit] Trigger with {other.name}, but no IDamageable found (root={rootName}).");
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_debugGizmoRadius > 0f)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, _debugGizmoRadius);
            }
        }

        private IEnumerator LifetimeRoutine()
        {
            yield return new WaitForSeconds(_maxLifetime);
            RequestDespawn();
        }

        private void RequestDespawn()
        {
            if (_isDespawning)
                return;

            _isDespawning = true;
            if (_lifetimeRoutine != null)
            {
                StopCoroutine(_lifetimeRoutine);
                _lifetimeRoutine = null;
            }
            LeanPool.Despawn(gameObject);
        }

        private void OnDisable()
        {
            _isInitialized = false;
            if (_lifetimeRoutine != null)
            {
                StopCoroutine(_lifetimeRoutine);
                _lifetimeRoutine = null;
            }
            _lastHitTimes.Clear();
            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
            }
        }

        private static float ResolveDamageMultiplier()
        {
            if (s_ProgressService == null)
            {
                s_ProgressService = ServiceLocator.Instance.Get<IPlayerProgressService>();
            }

            if (s_ProgressService == null)
                return 1f;

            float multiplier = s_ProgressService.GetStatMultiplier(PlayerUpgradeType.WaveDamage);
            return multiplier > 0f ? multiplier : 1f;
        }
    }
}
