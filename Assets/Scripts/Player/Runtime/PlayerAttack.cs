using System.Collections;
using Lean.Pool;
using LittleBeakCluck.Audio;
using LittleBeakCluck.Combat;
using LittleBeakCluck.Infrastructure;
using LittleBeakCluck.Services;
using LittleBeakCluck.UI;
using UnityEngine;
using System.Reflection;

namespace LittleBeakCluck.Player
{
    [Obfuscation(Feature = "rename", Exclude = true, ApplyToMembers = true)]
    public class PlayerAttack : MonoBehaviour
    {
        [Header("Wave Prefabs (per type)")]
        [SerializeField] private GameObject _highWavePrefab;
        [SerializeField] private GameObject _midWavePrefab;
        [SerializeField] private GameObject _lowWavePrefab;
        [Tooltip("Старе поле – більше не використовується напряму")][SerializeField] private GameObject _voiceWavePrefab; // legacy
        [SerializeField] private Transform _spawnPoint;

        [Header("Selected Type (debug)")]
        [SerializeField] private VoiceWaveType _currentType = VoiceWaveType.Mid;

        [Header("Charging")]
        [SerializeField] private float _minCharge = 0.5f;   // мінімальне стартове значення
        [SerializeField] private float _maxCharge = 3f;     // максимальне значення заряду (повний заряд)
        [SerializeField] private float _chargeSpeed = 1f;   // швидкість накопичення

        [Header("Multi Wave Settings")]
        [SerializeField] private float _waveInterval = 0.15f; // затримка між хвилями при множинному пострілі
        [Tooltip("Cooldown між початками атак (сек.)")]
        [SerializeField] private float _attackCooldown = 1f;
        [Tooltip("Базова відстань між хвилями (залишається сталою). Якщо 0 – використовується швидкість хвилі * інтервал.")]
        [SerializeField] private float _multiWaveSpacing = 0.75f;

        [Header("UI")]
        [SerializeField] private ChargeBar _chargeBar;

        private IInputService _inputService;
        private bool _subscribedToInput;
        private bool _loggedServiceFallback;
        private IPlayerProgressService _progressService;

        private float _currentCharge;
        private bool _isCharging;
        private float _cooldownTimer;
        private Coroutine _wavesRoutine;
        private Vector3 _latchedDirection; // зафіксований напрямок на момент релізу
        private PlayerHealth _playerHealth;
        [SerializeField] private bool _logDebug;

        [Header("Head Switch")]
        [SerializeField] private GameObject _normalHead; // об'єкт head
        [SerializeField] private GameObject _screamHead; // об'єкт scream_head
        [SerializeField] private float _extraScreamHold = 0.05f; // додатковий час після останньої хвилі
        private Coroutine _screamRoutine;

        [Header("Attack Button Animator")]
        [SerializeField] private Animator _attackButtonAnimator;
        [SerializeField] private string _reloadBoolName = "reload";
        private int _reloadBoolHash;
        private bool _canAttack = true;

        [Header("Startup Safety")]
        [Tooltip("If enabled, and player's HP is 0 at scene start (e.g., due to race conditions on some platforms), it will be refilled to MaxHealth to allow attacking.")]
        [SerializeField] private bool _ensureAliveOnStart = true;

        private void Awake()
        {
            TryResolveInputService();
            if (!string.IsNullOrEmpty(_reloadBoolName))
                _reloadBoolHash = Animator.StringToHash(_reloadBoolName);

            ResolvePlayerHealth();
        }

        private void OnEnable()
        {
            ResolvePlayerHealth();
            TryResolveInputService();
            SubscribeInputEvents();
        }

        private void OnDisable()
        {
            UnsubscribeInputEvents();
        }

        private void OnDestroy()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged -= HandleHealthChanged;
                _playerHealth.OnDied -= HandlePlayerDied;
                _playerHealth = null;
            }

            UnsubscribeInputEvents();
        }

        private void Start()
        {
            // Extra safety: after all Awake/OnEnable have run, sync canAttack with actual PlayerHealth state.
            if (_playerHealth != null)
            {
                if (_ensureAliveOnStart && !_playerHealth.IsAlive)
                {
                    // Refill to max to avoid starting dead due to platform-specific init ordering
                    _playerHealth.SetMaxHealth(_playerHealth.MaxHealth, refill: true);
                    LogAttack($"Start -> ensureAliveOnStart: refilled to max (hp={_playerHealth.CurrentHealth:F1}/{_playerHealth.MaxHealth:F1})");
                }
                _canAttack = _playerHealth.IsAlive;
                LogAttack($"Start -> sync canAttack={_canAttack} (hp={_playerHealth.CurrentHealth:F1}/{_playerHealth.MaxHealth:F1})");
            }
        }

        private void Update()
        {
            if (!_canAttack)
                return;

            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
                if (_cooldownTimer <= 0f)
                {
                    _cooldownTimer = 0f;
                    SetReloadAnim(false); // завершення cooldown
                }
            }

            if (_isCharging)
            {
                float chargeSpeed = _chargeSpeed * ResolveChargeSpeedMultiplier();
                _currentCharge += chargeSpeed * Time.deltaTime;
                _currentCharge = Mathf.Clamp(_currentCharge, _minCharge, _maxCharge);
                if (_chargeBar != null)
                    _chargeBar.UpdateBar(_currentCharge, _minCharge, _maxCharge);
            }
        }

        public void SetWaveType(int typeIndex)
        {
            _currentType = (VoiceWaveType)Mathf.Clamp(typeIndex, 0, 2);
        }

        public void SetWaveType(VoiceWaveType type)
        {
            _currentType = type;
        }

        public void StartCharging()
        {
            if (!_canAttack)
            {
                LogAttack("StartCharging aborted: canAttack=false");
                return;
            }

            if (_cooldownTimer > 0f)
            {
                LogAttack($"StartCharging aborted: cooldown={_cooldownTimer:F2}");
                return;
            }
            _isCharging = true;
            _currentCharge = _minCharge;
            _chargeBar?.Show();
            _chargeBar?.UpdateBar(_currentCharge, _minCharge, _maxCharge);
            LogAttack("StartCharging ok");
        }

        public void ReleaseAttack()
        {
            if (!_isCharging)
            {
                LogAttack("ReleaseAttack ignored: not charging");
                return;
            }
            if (!_canAttack)
            {
                _isCharging = false;
                _chargeBar?.Hide();
                LogAttack("ReleaseAttack aborted: canAttack=false");
                return;
            }
            _isCharging = false;
            _latchedDirection = ResolveShotDirection();
            float normalized = Mathf.InverseLerp(_minCharge, _maxCharge, _currentCharge);
            int wavesToSpawn = normalized < 0.5f ? 1 : (normalized < 0.999f ? 2 : 3);
            LogAttack($"ReleaseAttack ok: charge={_currentCharge:F2} norm={normalized:F2} waves={wavesToSpawn}");
            if (_wavesRoutine != null) StopCoroutine(_wavesRoutine);
            _wavesRoutine = StartCoroutine(SpawnWavesCoroutine(wavesToSpawn, normalized));
            if (_screamRoutine != null) StopCoroutine(_screamRoutine);
            _screamRoutine = StartCoroutine(ScreamHeadSequence(wavesToSpawn));
            _cooldownTimer = _attackCooldown;
            _chargeBar?.Hide();
            _currentCharge = 0f;
            SetReloadAnim(true); // початок cooldown

            var audio = ServiceLocator.Instance.Get<IAudioService>();
            audio?.PlayWaveVoice(_currentType);
        }

        private void HandlePlayerDied()
        {
            _canAttack = false;
            _isCharging = false;
            _cooldownTimer = 0f;

            if (_wavesRoutine != null)
            {
                StopCoroutine(_wavesRoutine);
                _wavesRoutine = null;
            }

            if (_screamRoutine != null)
            {
                StopCoroutine(_screamRoutine);
                _screamRoutine = null;
            }

            _chargeBar?.Hide();
            SetHeadState(screaming: false);
            SetReloadAnim(false);
            UnsubscribeInputEvents();
            LogAttack("HandlePlayerDied -> attack disabled");
        }

        private void ResolvePlayerHealth()
        {
            if (_playerHealth != null)
                return;

            if (!TryGetComponent(out _playerHealth))
            {
                _playerHealth = GetComponentInParent<PlayerHealth>();
            }

            if (_playerHealth == null)
                return;

            _playerHealth.OnHealthChanged += HandleHealthChanged;
            _playerHealth.OnDied += HandlePlayerDied;
            _canAttack = _playerHealth.IsAlive;
        }

        private void HandleHealthChanged(float current, float max)
        {
            bool alive = current > 0f;
            if (_canAttack != alive)
            {
                _canAttack = alive;
                LogAttack($"HealthChanged -> canAttack={_canAttack} (hp={current:F1}/{max:F1})");
                if (!alive)
                {
                    // Cancel charging if player just became dead
                    _isCharging = false;
                    _chargeBar?.Hide();
                    if (_wavesRoutine != null)
                    {
                        StopCoroutine(_wavesRoutine);
                        _wavesRoutine = null;
                    }
                    if (_screamRoutine != null)
                    {
                        StopCoroutine(_screamRoutine);
                        _screamRoutine = null;
                    }
                    SetHeadState(screaming: false);
                    SetReloadAnim(false);
                }
            }
        }

        private bool TryResolveInputService()
        {
            if (_inputService != null)
                return true;

            var locator = ServiceLocator.Instance;
            var service = locator.Get<IInputService>();
            if (service == null)
            {
                service = new InputService();
                locator.Register<IInputService>(service);
                if (!_loggedServiceFallback)
                {
                    Debug.LogWarning($"[{name}] PlayerAttack: IInputService was missing; created a fallback instance.", this);
                    _loggedServiceFallback = true;
                }
            }

            _inputService = service;
            return true;
        }

        private void SubscribeInputEvents()
        {
            if (_subscribedToInput)
                return;

            if (!TryResolveInputService() || _inputService == null)
                return;

            _inputService.AttackStarted += HandleAttackStarted;
            _inputService.AttackCanceled += HandleAttackCanceled;
            _subscribedToInput = true;

            if (_inputService.AttackHeld)
            {
                HandleAttackStarted();
            }
        }

        private void UnsubscribeInputEvents()
        {
            if (!_subscribedToInput || _inputService == null)
                return;

            _inputService.AttackStarted -= HandleAttackStarted;
            _inputService.AttackCanceled -= HandleAttackCanceled;
            _subscribedToInput = false;
        }

        private void HandleAttackStarted()
        {
            StartCharging();
        }

        private void HandleAttackCanceled()
        {
            ReleaseAttack();
        }

        private IEnumerator SpawnWavesCoroutine(int count, float normalizedCharge)
        {
            if (count <= 0)
                yield break;

            GameObject prefab = GetPrefabForType(_currentType);
            if (prefab == null)
            {
                Debug.LogError($"Wave prefab for type {_currentType} is not set!");
                yield break;
            }

            float waveSpeed = ExtractWaveSpeed(prefab);
            float travelDuringInterval = waveSpeed * Mathf.Max(0f, _waveInterval);
            float desiredSpacing = _multiWaveSpacing > 0f ? _multiWaveSpacing : travelDuringInterval;
            float spawnOffsetStep = Mathf.Max(0f, desiredSpacing - travelDuringInterval);
            WaitForSeconds intervalWait = _waveInterval > 0f ? new WaitForSeconds(_waveInterval) : null;

            for (int i = 0; i < count; i++)
            {
                Vector3 anchor = _spawnPoint != null ? _spawnPoint.position : transform.position;
                Vector3 spawnPos = anchor - _latchedDirection * spawnOffsetStep * i;
                SpawnSingleWave(prefab, normalizedCharge, _latchedDirection, spawnPos);
                if (i < count - 1 && intervalWait != null)
                    yield return intervalWait;
            }
        }

        private IEnumerator ScreamHeadSequence(int waveCount)
        {
            SetHeadState(screaming: true);
            // Час серії = (waveCount-1)*interval (між хвилями) + невеличка затримка
            float duration = 0f;
            if (waveCount > 1)
                duration = (waveCount - 1) * Mathf.Max(0f, _waveInterval);
            duration += _extraScreamHold; // тримаємо трошки довше
            if (duration > 0f)
                yield return new WaitForSeconds(duration);
            SetHeadState(screaming: false);
        }

        private void SetHeadState(bool screaming)
        {
            if (_normalHead != null) _normalHead.SetActive(!screaming);
            if (_screamHead != null) _screamHead.SetActive(screaming);
        }

        private void SpawnSingleWave(GameObject prefab, float normalizedCharge, Vector3 direction, Vector3 spawnPosition)
        {
            GameObject waveInstance = LeanPool.Spawn(prefab, spawnPosition, Quaternion.identity);
            if (waveInstance.TryGetComponent<VoiceWaveHit>(out var voiceWaveHit))
            {
                float actualCharge = Mathf.Lerp(_minCharge, _maxCharge, normalizedCharge);
                voiceWaveHit.Initialize(actualCharge, direction, _currentType);
                LogAttack($"Spawned wave: {waveInstance.name} charge={actualCharge:F2} dir={direction}");
            }
            else
            {
                LogAttack($"Spawned wave missing VoiceWaveHit: {waveInstance.name}");
            }
        }

        private Vector3 ResolveShotDirection()
        {
            if (_inputService != null)
            {
                Vector2 aim = _inputService.AimAxis;
                if (aim.sqrMagnitude > 0.0001f)
                    return new Vector3(aim.x, aim.y, 0f).normalized;
            }

            if (_spawnPoint != null)
                return _spawnPoint.right.normalized;

            return transform.right;
        }

        private GameObject GetPrefabForType(VoiceWaveType type)
        {
            switch (type)
            {
                case VoiceWaveType.High: return _highWavePrefab != null ? _highWavePrefab : _voiceWavePrefab;
                case VoiceWaveType.Mid: return _midWavePrefab != null ? _midWavePrefab : _voiceWavePrefab;
                case VoiceWaveType.Low: return _lowWavePrefab != null ? _lowWavePrefab : _voiceWavePrefab;
                default: return _voiceWavePrefab;
            }
        }

        private static float ExtractWaveSpeed(GameObject prefab)
        {
            if (prefab != null && prefab.TryGetComponent<VoiceWaveHit>(out var wave))
            {
                return wave.Speed;
            }
            return 0f;
        }

        private void SetReloadAnim(bool state)
        {
            if (_attackButtonAnimator != null && _reloadBoolHash != 0)
            {
                _attackButtonAnimator.SetBool(_reloadBoolHash, state);
            }
        }

        private float ResolveChargeSpeedMultiplier()
        {
            if (_progressService == null)
            {
                _progressService = ServiceLocator.Instance.Get<IPlayerProgressService>();
            }

            if (_progressService == null)
                return 1f;

            float multiplier = _progressService.GetStatMultiplier(PlayerUpgradeType.ChargeSpeed);
            return multiplier > 0f ? multiplier : 1f;
        }

        private void LogAttack(string message)
        {
            if (!_logDebug)
                return;

            Debug.Log($"[PlayerAttack] {message}", this);
        }

        // ---- Diagnostics helpers (safe to call from UI/debug) ----
        public void EnableDebugLogging(bool on = true)
        {
            _logDebug = on;
        }

        public bool IsCharging => _isCharging;
        public bool CanAttack => _canAttack;
        public float Cooldown => _cooldownTimer;
        public float CurrentCharge => _currentCharge;
    }
}
