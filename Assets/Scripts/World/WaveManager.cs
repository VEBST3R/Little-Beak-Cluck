using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using LittleBeakCluck.Combat;
using LittleBeakCluck.Enemies;
using LittleBeakCluck.Infrastructure;
using LittleBeakCluck.Services;
using LittleBeakCluck.Player;
using LittleBeakCluck.UI;
using UnityEngine;

namespace LittleBeakCluck.World
{
    public enum WaveMode
    {
        Campaign,
        Endless
    }

    [Serializable]
    public class WaveEntry
    {
        public GameObject prefab; // який ворог
        public int count = 1;     // скільки штук
        public SpawnPoint spawnPoint; // де спавнити (конкретна точка)
        public string spawnPointKey; // ідентифікатор спавнпоінта для ScriptableObject
        public float spawnInterval = -1f; // інтервал між спавнами цієї групи (якщо <0, використовує значення SpawnPoint)
    }

    [Serializable]
    public class WaveDefinition
    {
        public string name;
        public VoiceWaveType requiredPlayerWave = VoiceWaveType.Mid; // який тип хвилі відкриває цю хвилю
        public List<WaveEntry> entries = new();
        public int rewardCoins;
    }

    [Serializable]
    public class EndlessModeSettings
    {
        [Tooltip("Базовий інтервал між спавнами всередині групи, сек. Від'ємне значення означає використовувати інтервал SpawnPoint.")]
        public float defaultSpawnInterval = -1f;

        [Tooltip("Діапазон кількості груп ворогів, які будуть створені в одній хвилі.")]
        public Vector2Int groupsPerWaveRange = new Vector2Int(1, 3);

        [Tooltip("Максимальна кількість хвиль у нескінченному режимі. 0 = безкінечно.")]
        public int maxGeneratedWaves = 0;

        [Tooltip("Список ворогів, що використовуються для нескінченного режиму.")]
        public List<EndlessPrefabEntry> spawnEntries = new();
    }

    [Serializable]
    public class EndlessPrefabEntry
    {
        public GameObject prefab;
        public string spawnPointKey = string.Empty;
        public int minCount = 1;
        public int maxCount = 3;
        public float spawnInterval = -1f;
        public bool useAllSpawnPoints = false;
    }

    // Керує хвилями: кожна хвиля спавнить визначені типи ворогів
    public class WaveManager : MonoBehaviour, ICampaignWaveController
    {
        public event Action<int, WaveDefinition> WaveStarted;
        public event Action<int, WaveDefinition> WaveCompleted;
        public event Action<int, int> WaveCooldownStarted;

        [Header("Mode")]
        [SerializeField] private WaveMode _mode = WaveMode.Campaign;
        [SerializeField] private EndlessModeSettings _endlessSettings = new();

        [Header("Progress")]
        [Tooltip("Слайдер або бар для індикації прогресу хвилі")] public WaveProgressBar progressBar;

        [Header("Inter-Wave Cooldown")]
        [Tooltip("Затримка між хвилями, сек.")] public float interWaveCooldown = 5f;

        [Header("Campaign Victory Flow")]
        [Tooltip("Час, за який шкала часу сповільнюється до нуля після перемоги в кампанії.")]
        [Min(0f)][SerializeField] private float campaignTimeSlowDuration = 0.5f;
        [Tooltip("Час, за який шкала часу повертається до 1 після натискання продовжити.")]
        [Min(0f)][SerializeField] private float campaignTimeResumeDuration = 0.5f;

        [Header("Debug")][SerializeField] private bool _debugLogs = false;

        [Header("Startup")]
        [SerializeField] private bool _autoStartFirstWave = true;
        [SerializeField] private int _startWaveIndex = 0;

        [Header("Data Assets")]
        [SerializeField] private CampaignWaveAsset _campaignWaveAsset;
        [SerializeField] private EndlessWaveAsset _endlessWaveAsset;
        [SerializeField] private CoinRewardAsset _coinRewardAsset;

        [Header("Spawn Points")]
        [SerializeField] private List<SpawnPointBinding> _spawnPointBindings = new();

        private const float EndlessCooldownHealAmount = 25f;

        private int _currentWaveIndex = -1;
        private int _aliveInWave = 0;
        private WaveDefinition _currentWaveDefinition;
        private IGameModeService _gameModeService;
        private Dictionary<string, SpawnPoint> _spawnPointLookup;
        private readonly List<SpawnPoint> _spawnPointFallback = new();
        private readonly Dictionary<SpawnPoint, string> _spawnPointReverseLookup = new();
        private readonly List<SpawnSelection> _spawnTargetCache = new();
        private readonly List<WaveDefinition> _runtimeWaves = new();
        private readonly HashSet<IDamageable> _processedEnemyDeaths = new();
        private PlayerHealth _playerHealth;
        private Coroutine _endlessHealRoutine;
        private Coroutine _timeScaleRoutine;
        private static FieldInfo s_WaveEntryUseAllField;
        private static FieldInfo s_EndlessEntryUseAllField;
        private int _pendingCampaignWaveIndex = -1;
        private int _sessionCoins;
        private int _progressBalanceBaseline;
        private int _lastCampaignReward;
        private IPlayerProgressService _progressService;
        private bool _progressEventsHooked;

        public event Action<int> CoinsChanged;

        [Serializable]
        private struct SpawnPointBinding
        {
            public string key;
            public SpawnPoint spawnPoint;
        }

        private struct SpawnSelection
        {
            public SpawnPoint SpawnPoint;
            public string Key;
            public int FallbackIndex;
        }

        public int CurrentWaveIndex => _currentWaveIndex;
        public WaveDefinition CurrentWave => _currentWaveDefinition;
        public WaveMode Mode => _mode;
        public bool IsEndless => _mode == WaveMode.Endless;
        public int TotalCoins => Mathf.Max(0, _sessionCoins);
        public int LastCampaignRewardCoins => _lastCampaignReward;

        public int TotalWaveCount
        {
            get
            {
                if (IsEndless)
                {
                    if (_endlessSettings != null && _endlessSettings.maxGeneratedWaves > 0)
                        return _endlessSettings.maxGeneratedWaves;
                    return -1;
                }

                return _runtimeWaves.Count;
            }
        }

        private void ResolveGameModeService()
        {
            var locator = ServiceLocator.Instance;
            _gameModeService = locator.Get<IGameModeService>();

            if (_gameModeService == null)
            {
                var createdService = new GameModeService();
                createdService.SetMode(_mode, persist: false);
                locator.Register<IGameModeService>(createdService);
                _gameModeService = createdService;
            }
            else
            {
                _mode = _gameModeService.CurrentMode;
            }

            if (!IsEndless)
            {
                if (_runtimeWaves.Count > 0)
                {
                    int resumeWave = Mathf.Clamp(_gameModeService.GetCampaignStartWave(), 0, _runtimeWaves.Count - 1);
                    _startWaveIndex = Mathf.Clamp(resumeWave, 0, _runtimeWaves.Count - 1);
                }
                else
                {
                    _startWaveIndex = 0;
                }
            }
        }

        private void Awake()
        {
            _spawnPointLookup = new Dictionary<string, SpawnPoint>(StringComparer.Ordinal);
            ServiceLocator.Instance.Register<ICampaignWaveController>(this);
            CacheSpawnPoints();
            ResolveGameModeService();
            LoadWaveDataFromAssets();
            if (_coinRewardAsset == null)
            {
                Debug.LogWarning("[WaveManager] CoinRewardAsset is not assigned. Coin rewards will remain at zero until configured.", this);
            }
            InitializeProgressService();
            ResetCoinCounters();
        }

        private void Start()
        {
            // Автопошук прогресбару, якщо не призначили в інспекторі
            if (progressBar == null)
            {
                progressBar = FindFirstObjectByType<WaveProgressBar>(FindObjectsInactive.Include);
                if (_debugLogs) Debug.Log($"[WaveManager] Auto-wired progressBar = {(progressBar ? progressBar.name : "null")}");
            }

            _currentWaveDefinition = null;

            if (_gameModeService != null)
            {
                _mode = _gameModeService.CurrentMode;

                if (!IsEndless && _runtimeWaves.Count > 0)
                {
                    int resumeWave = Mathf.Clamp(_gameModeService.GetCampaignStartWave(), 0, _runtimeWaves.Count - 1);
                    _startWaveIndex = Mathf.Clamp(resumeWave, 0, _runtimeWaves.Count - 1);
                }
            }

            if (IsEndless)
            {
                CachePlayerHealth();
            }

            if (_autoStartFirstWave && CanStartWave(_startWaveIndex))
            {
                StartWave(_startWaveIndex);
            }
            else if (_autoStartFirstWave && _debugLogs)
            {
                Debug.LogWarning($"[WaveManager] Auto-start requested but wave index {_startWaveIndex} is invalid for mode {_mode}.");
            }
        }

        public void StartFirstWave() => StartWave(0);

        public void StartWave(int index)
        {
            if (!CanStartWave(index))
            {
                Debug.LogWarning($"WaveManager: Invalid wave index {index}");
                return;
            }

            var wave = BuildWaveForIndex(index);
            if (wave == null)
            {
                Debug.LogWarning($"[WaveManager] Wave definition at index {index} is null. Aborting start.");
                return;
            }

            _currentWaveIndex = index;
            _currentWaveDefinition = wave;
            _aliveInWave = 0;
            _processedEnemyDeaths.Clear(); // Очищуємо перед новою хвилею

            if (!IsEndless)
            {
                RestorePlayerHealthToFull();
            }

            WaveStarted?.Invoke(_currentWaveIndex, wave);

            // Порахувати загальну кількість ворогів у хвилі
            foreach (var entry in wave.entries)
            {
                if (entry != null && entry.prefab != null)
                    _aliveInWave += Mathf.Max(0, entry.count);
            }

            if (_debugLogs) Debug.Log($"[WaveManager] StartWave idx={_currentWaveIndex} total={_aliveInWave}");
            if (progressBar != null) progressBar.SetWaveTotal(_aliveInWave);
            else if (_debugLogs) Debug.LogWarning("[WaveManager] progressBar is not assigned");

            // Якщо хвиля порожня — одразу завершити і перейти до наступної
            if (_aliveInWave == 0)
            {
                if (_debugLogs) Debug.Log("[WaveManager] Wave has 0 enemies. Completing immediately.");
                progressBar?.SetWaveComplete();
                WaveCompleted?.Invoke(_currentWaveIndex, wave);
                StartCoroutine(BeginNextWaveAfterCooldown());
                return;
            }

            StartCoroutine(SpawnWaveCoroutine(wave));
        }

        private IEnumerator SpawnWaveCoroutine(WaveDefinition wave)
        {
            foreach (var entry in wave.entries)
            {
                if (entry == null || entry.prefab == null || entry.count <= 0 || entry.spawnPoint == null)
                    continue;

                // Початкова затримка конкретно для цього спавна (якщо задана у SpawnPoint)
                if (entry.spawnPoint.InitialDelay > 0f)
                    yield return new WaitForSeconds(entry.spawnPoint.InitialDelay);

                for (int i = 0; i < entry.count; i++)
                {
                    var go = entry.spawnPoint.SpawnFromPrefab(entry.prefab);
                    if (_debugLogs) Debug.Log($"[WaveManager] Spawned {go?.name ?? "null"} at {entry.spawnPoint.name}");
                    if (go != null)
                    {
                        // Підпишемось на смерть ворога (якщо він імплементує IDamageable)
                        var damageable = go.GetComponentInChildren<IDamageable>();
                        if (damageable != null)
                        {
                            var capturedDamageable = damageable;
                            damageable.OnDied += () => OnEnemyDied(go, capturedDamageable);
                            if (_debugLogs) Debug.Log($"[WaveManager] Subscribed to OnDied for {go.name}");
                        }
                        else if (_debugLogs)
                        {
                            Debug.LogWarning($"[WaveManager] Spawned object {go.name} has no IDamageable to track.");
                        }
                    }

                    // Затримка між спавнами на цьому спавнпоінті
                    float betweenDelay = entry.spawnInterval >= 0f
                        ? entry.spawnInterval
                        : (entry.spawnPoint != null ? entry.spawnPoint.BetweenDelay : 0f);

                    if (i < entry.count - 1 && betweenDelay > 0f)
                        yield return new WaitForSeconds(betweenDelay);
                }
            }
        }

        // Публічний хук, якщо ворог був заспавнений не через WaveManager: можна вручну зареєструвати
        public void RegisterEnemy(IDamageable damageable)
        {
            if (damageable == null) return;
            _aliveInWave++;
            progressBar?.SetWaveTotal(_aliveInWave);
            damageable.OnDied += () => OnEnemyDied((damageable as Component)?.gameObject, damageable);
            if (_debugLogs) Debug.Log("[WaveManager] External enemy registered");
        }

        private void OnEnemyDied(GameObject go, IDamageable damageable)
        {
            // Захист від повторних викликів
            if (damageable == null || !_processedEnemyDeaths.Add(damageable))
            {
                if (_debugLogs) Debug.Log("[WaveManager] Enemy death already processed, ignoring duplicate callback.");
                return;
            }

            _aliveInWave = Mathf.Max(0, _aliveInWave - 1);
            if (progressBar != null) progressBar.ReportEnemyKilled();
            if (IsEndless)
            {
                int killReward = ResolveEndlessKillReward(go, damageable);
                if (killReward > 0)
                {
                    AwardCoins(killReward);
                }
            }
            if (_debugLogs) Debug.Log($"[WaveManager] Enemy died. Alive left={_aliveInWave}");

            if (_aliveInWave == 0)
            {
                progressBar?.SetWaveComplete();
                WaveCompleted?.Invoke(_currentWaveIndex, CurrentWave);

                if (!IsEndless)
                {
                    _gameModeService?.SaveCampaignProgress(_currentWaveIndex);
                    HandleCampaignWaveVictory();
                    return;
                }

                if (_debugLogs) Debug.Log("[WaveManager] Wave complete. Starting cooldown...");
                // Автоматичний перехід до наступної хвилі з КД та скиданням прогресу
                StartCoroutine(BeginNextWaveAfterCooldown());
            }
        }

        private void HandleCampaignWaveVictory()
        {
            int candidateIndex = _currentWaveIndex + 1;
            _pendingCampaignWaveIndex = CanStartWave(candidateIndex) ? candidateIndex : -1;

            GrantCampaignReward(_currentWaveDefinition?.rewardCoins ?? 0);

            StopTimeScaleRoutine();
            _timeScaleRoutine = StartCoroutine(SlowTimeThenShowVictory());
        }

        private void OnDestroy()
        {
            ReleaseProgressService();
        }

        public void ContinueCampaignAfterVictory()
        {
            if (IsEndless)
                return;

            int nextIndex = _pendingCampaignWaveIndex;
            if (nextIndex < 0 || !CanStartWave(nextIndex))
            {
                int fallbackIndex = _currentWaveIndex + 1;
                nextIndex = CanStartWave(fallbackIndex) ? fallbackIndex : -1;
            }
            _pendingCampaignWaveIndex = -1;

            StopTimeScaleRoutine();
            _timeScaleRoutine = StartCoroutine(RestoreTimeScaleThenStartWave(nextIndex));
        }

        private void StopTimeScaleRoutine()
        {
            if (_timeScaleRoutine == null)
                return;

            StopCoroutine(_timeScaleRoutine);
            _timeScaleRoutine = null;
        }

        private IEnumerator SlowTimeThenShowVictory()
        {
            float duration = Mathf.Max(0.01f, campaignTimeSlowDuration);
            float startScale = Time.timeScale;
            float elapsed = 0f;

            if (duration <= 0.01f)
            {
                Time.timeScale = 0f;
            }
            else
            {
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    Time.timeScale = Mathf.Lerp(startScale, 0f, t);
                    yield return null;
                }

                Time.timeScale = 0f;
            }

            var uiManager = ServiceLocator.Instance.Get<IUIManager>();
            if (uiManager != null)
            {
                uiManager.ShowVictoryMenu();
            }
            else if (_debugLogs)
            {
                Debug.LogWarning("[WaveManager] Campaign victory could not locate UIManager to display win panel.", this);
            }

            _timeScaleRoutine = null;
        }

        private IEnumerator RestoreTimeScaleThenStartWave(int waveIndex)
        {
            float duration = Mathf.Max(0.01f, campaignTimeResumeDuration);
            float startScale = Time.timeScale;
            float elapsed = 0f;

            if (duration <= 0.01f)
            {
                Time.timeScale = 1f;
            }
            else
            {
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    Time.timeScale = Mathf.Lerp(startScale, 1f, t);
                    yield return null;
                }

                Time.timeScale = 1f;
            }

            if (waveIndex >= 0 && CanStartWave(waveIndex))
            {
                StartWave(waveIndex);
            }
            else if (_debugLogs)
            {
                Debug.LogWarning($"[WaveManager] Campaign continue attempted with invalid wave index {waveIndex}.", this);
            }

            _timeScaleRoutine = null;
        }

        private void ResetCoinCounters()
        {
            _lastCampaignReward = 0;
            _progressBalanceBaseline = _progressService != null ? Mathf.Max(0, _progressService.Balance) : 0;
            _sessionCoins = 0;
            CoinsChanged?.Invoke(_sessionCoins);
        }

        private void AwardCoins(int amount)
        {
            if (amount <= 0)
                return;

            if (_progressService == null)
            {
                InitializeProgressService();
            }

            if (_progressService != null)
            {
                _progressService.AddCoins(amount);
            }
            else
            {
                _sessionCoins += amount;
                CoinsChanged?.Invoke(_sessionCoins);
            }
        }

        private void GrantCampaignReward(int amount)
        {
            _lastCampaignReward = Mathf.Max(0, amount);
            AwardCoins(_lastCampaignReward);
        }

        private void InitializeProgressService()
        {
            if (_progressService == null)
            {
                _progressService = ServiceLocator.Instance.Get<IPlayerProgressService>();
            }

            if (_progressService != null && !_progressEventsHooked)
            {
                int currentBalance = Mathf.Max(0, _progressService.Balance);
                _progressBalanceBaseline = Mathf.Max(0, currentBalance - _sessionCoins);
                _progressService.BalanceChanged += HandleProgressBalanceChanged;
                _progressEventsHooked = true;
                HandleProgressBalanceChanged(currentBalance);
            }
        }

        private void ReleaseProgressService()
        {
            if (_progressService != null && _progressEventsHooked)
            {
                _progressService.BalanceChanged -= HandleProgressBalanceChanged;
                _progressEventsHooked = false;
            }

            _progressService = null;
        }

        private void HandleProgressBalanceChanged(int balance)
        {
            int session = Mathf.Max(0, balance - _progressBalanceBaseline);
            _sessionCoins = session;
            CoinsChanged?.Invoke(_sessionCoins);
        }

        private int ResolveEndlessKillReward(GameObject go, IDamageable damageable)
        {
            var enemyConfig = ExtractEnemyConfig(go, damageable);
            return ResolveEndlessKillReward(enemyConfig);
        }

        private EnemyConfig ExtractEnemyConfig(GameObject go, IDamageable damageable)
        {
            if (damageable is EnemyBehaviour behaviour)
                return behaviour.Config;

            if (damageable is Component component)
            {
                var direct = component.GetComponent<EnemyBehaviour>();
                if (direct != null)
                    return direct.Config;

                var parent = component.GetComponentInParent<EnemyBehaviour>();
                if (parent != null)
                    return parent.Config;
            }

            if (go != null)
            {
                var direct = go.GetComponent<EnemyBehaviour>();
                if (direct != null)
                    return direct.Config;

                var parent = go.GetComponentInParent<EnemyBehaviour>();
                if (parent != null)
                    return parent.Config;
            }

            return null;
        }

        private int ResolveEndlessKillReward(EnemyConfig config)
        {
            if (_coinRewardAsset != null)
            {
                if (config != null && _coinRewardAsset.enemyRewards != null)
                {
                    foreach (var entry in _coinRewardAsset.enemyRewards)
                    {
                        if (entry == null || entry.enemy == null)
                            continue;

                        if (entry.enemy == config)
                            return Mathf.Max(0, entry.coinsPerKill);
                    }
                }

                return Mathf.Max(0, _coinRewardAsset.defaultEndlessCoinsPerKill);
            }

            return 0;
        }

        private bool TryGetCampaignRewardOverride(int waveIndex, string waveName, out int reward)
        {
            reward = 0;
            if (_coinRewardAsset == null || _coinRewardAsset.campaignRewards == null || _coinRewardAsset.campaignRewards.Count == 0)
                return false;

            int bestScore = int.MinValue;
            for (int i = 0; i < _coinRewardAsset.campaignRewards.Count; i++)
            {
                var entry = _coinRewardAsset.campaignRewards[i];
                if (entry == null)
                    continue;

                bool hasIndex = entry.waveIndex >= 0;
                bool hasName = !string.IsNullOrWhiteSpace(entry.waveName);

                if (hasIndex && entry.waveIndex != waveIndex)
                    continue;

                if (hasName && !string.Equals(entry.waveName, waveName, StringComparison.Ordinal))
                    continue;

                int score = 0;
                if (hasIndex) score += 2;
                if (hasName) score += 1;

                if (score > bestScore)
                {
                    bestScore = score;
                    reward = Mathf.Max(0, entry.coins);
                }
            }

            return bestScore >= 0;
        }

        private int GetDefaultCampaignReward()
        {
            if (_coinRewardAsset != null)
                return Mathf.Max(0, _coinRewardAsset.defaultCampaignWaveCoins);

            return 0;
        }

        private int GenerateProceduralCampaignReward()
        {
            if (_coinRewardAsset != null)
            {
                var range = _coinRewardAsset.proceduralRewardRange;
                int min = Mathf.Max(0, range.x);
                int max = Mathf.Max(min, range.y);
                if (max > 0)
                    return UnityEngine.Random.Range(min, max + 1);
            }

            return GetDefaultCampaignReward();
        }

        private int ResolveConfiguredCampaignReward(int waveIndex, string waveName)
        {
            if (TryGetCampaignRewardOverride(waveIndex, waveName, out int reward))
                return reward;

            return GetDefaultCampaignReward();
        }

        private int ResolveProceduralCampaignReward(int waveIndex, string waveName)
        {
            if (TryGetCampaignRewardOverride(waveIndex, waveName, out int reward))
                return reward;

            return GenerateProceduralCampaignReward();
        }

        private int ResolveCachedReward(int cachedReward, int waveIndex, string waveName)
        {
            if (TryGetCampaignRewardOverride(waveIndex, waveName, out int reward))
                return reward;

            if (cachedReward > 0)
                return cachedReward;

            return GetDefaultCampaignReward();
        }

        private IEnumerator BeginNextWaveAfterCooldown()
        {
            int nextIndex = _currentWaveIndex + 1;

            // Якщо це була остання хвиля – зупиняємось
            if (!CanStartWave(nextIndex))
                yield break;

            WaveCooldownStarted?.Invoke(_currentWaveIndex, nextIndex);

            StartEndlessCooldownHeal(interWaveCooldown);

            // Плавне скидання прогресбару протягом КД
            if (progressBar != null && interWaveCooldown > 0f)
                progressBar.ResetOverTime(interWaveCooldown);

            if (interWaveCooldown > 0f)
                yield return new WaitForSeconds(interWaveCooldown);

            if (_debugLogs) Debug.Log("[WaveManager] Cooldown finished. Starting next wave");
            StartWave(nextIndex);
        }

        private bool CanStartWave(int index)
        {
            if (index < 0)
                return false;

            if (!IsEndless)
            {
                return index < _runtimeWaves.Count;
            }

            if (_endlessSettings == null)
                return false;

            if (_endlessSettings.maxGeneratedWaves > 0 && index >= _endlessSettings.maxGeneratedWaves)
                return false;

            var pool = GetEndlessSpawnPool();
            return pool != null && pool.Count > 0;
        }

        private WaveDefinition BuildWaveForIndex(int index)
        {
            if (!IsEndless)
            {
                if (index < 0 || index >= _runtimeWaves.Count)
                    return null;
                return _runtimeWaves[index];
            }

            return GenerateEndlessWave(index);
        }

        private void CacheSpawnPoints()
        {
            if (_spawnPointLookup == null)
                _spawnPointLookup = new Dictionary<string, SpawnPoint>(StringComparer.Ordinal);
            else
                _spawnPointLookup.Clear();

            _spawnPointFallback.Clear();
            _spawnPointReverseLookup.Clear();

            foreach (var binding in _spawnPointBindings)
            {
                if (binding.spawnPoint == null)
                {
                    if (_debugLogs)
                    {
                        string keyLabel = string.IsNullOrWhiteSpace(binding.key) ? "<empty>" : binding.key;
                        Debug.LogWarning($"[WaveManager] Spawn point binding for key '{keyLabel}' has no reference.", this);
                    }
                    continue;
                }

                _spawnPointFallback.Add(binding.spawnPoint);
                _spawnPointReverseLookup[binding.spawnPoint] = binding.key ?? string.Empty;

                if (string.IsNullOrWhiteSpace(binding.key))
                {
                    if (_debugLogs)
                    {
                        Debug.LogWarning("[WaveManager] Spawn point binding has empty key.", binding.spawnPoint);
                    }
                    continue;
                }

                if (_spawnPointLookup.ContainsKey(binding.key))
                {
                    if (_debugLogs)
                    {
                        Debug.LogWarning($"[WaveManager] Duplicate spawn point key '{binding.key}' detected.", binding.spawnPoint);
                    }
                    continue;
                }

                _spawnPointLookup.Add(binding.key, binding.spawnPoint);
            }

            if (_spawnPointFallback.Count == 0 && _debugLogs)
            {
                Debug.LogWarning("[WaveManager] No spawn points registered for bindings. Endless mode fallback will not work.", this);
            }
        }

        private void CachePlayerHealth()
        {
            if (_playerHealth != null && _playerHealth.isActiveAndEnabled)
                return;

            _playerHealth = FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);

            if (_playerHealth == null && _debugLogs)
            {
                Debug.LogWarning("[WaveManager] PlayerHealth component not found. Endless cooldown heal will be skipped.", this);
            }
        }

        private void RestorePlayerHealthToFull()
        {
            CachePlayerHealth();
            if (_playerHealth == null)
                return;

            if (!_playerHealth.IsAlive)
            {
                _playerHealth.SetMaxHealth(_playerHealth.MaxHealth, refill: true);
                return;
            }

            float missing = _playerHealth.MaxHealth - _playerHealth.CurrentHealth;
            if (missing > 0f)
            {
                _playerHealth.Heal(missing);
            }
        }

        private void StartEndlessCooldownHeal(float duration)
        {
            if (!IsEndless)
                return;

            CachePlayerHealth();
            if (_playerHealth == null)
                return;

            if (_endlessHealRoutine != null)
            {
                StopCoroutine(_endlessHealRoutine);
                _endlessHealRoutine = null;
            }

            if (duration <= 0f)
            {
                _playerHealth.Heal(EndlessCooldownHealAmount);
                return;
            }

            _endlessHealRoutine = StartCoroutine(HealPlayerOverCooldown(_playerHealth, EndlessCooldownHealAmount, duration));
        }

        private IEnumerator HealPlayerOverCooldown(PlayerHealth target, float totalAmount, float duration)
        {
            if (target == null)
            {
                yield break;
            }

            float healed = 0f;
            while (healed < totalAmount && target != null)
            {
                yield return null;

                float delta = totalAmount * (Time.deltaTime / duration);
                if (delta <= 0f)
                    continue;

                if (healed + delta > totalAmount)
                    delta = totalAmount - healed;

                target.Heal(delta);
                healed += delta;
            }

            _endlessHealRoutine = null;
        }

        private void LoadWaveDataFromAssets()
        {
            if (!IsEndless)
            {
                if (_campaignWaveAsset == null)
                {
                    _runtimeWaves.Clear();
                    if (_debugLogs) Debug.LogWarning("[WaveManager] Campaign wave asset is not assigned.", this);
                    return;
                }

                if (_campaignWaveAsset.useProceduralGeneration)
                {
                    LoadProceduralCampaignWaves();
                }
                else
                {
                    var definitions = BuildDefinitionsFromData(_campaignWaveAsset.Waves);
                    if (definitions.Count == 0)
                    {
                        _runtimeWaves.Clear();
                        if (_debugLogs) Debug.LogWarning("[WaveManager] Campaign asset has no valid waves.", this);
                        return;
                    }

                    _runtimeWaves.Clear();
                    _runtimeWaves.AddRange(definitions);
                }
            }
            else
            {
                if (_endlessWaveAsset == null)
                {
                    if (_debugLogs) Debug.LogWarning("[WaveManager] Endless wave asset is not assigned.", this);
                    return;
                }

                if (_endlessSettings == null)
                {
                    _endlessSettings = new EndlessModeSettings();
                }

                var entries = BuildEndlessEntries(_endlessWaveAsset.Entries);
                if (entries.Count == 0)
                {
                    if (_debugLogs) Debug.LogWarning("[WaveManager] Endless asset has no valid spawn entries.", this);
                }

                _endlessSettings.spawnEntries = entries;
                _endlessSettings.defaultSpawnInterval = _endlessWaveAsset.defaultSpawnInterval;
                int minGroups = Mathf.Max(1, _endlessWaveAsset.groupsPerWaveRange.x);
                int maxGroups = Mathf.Max(minGroups, _endlessWaveAsset.groupsPerWaveRange.y);
                _endlessSettings.groupsPerWaveRange = new Vector2Int(minGroups, maxGroups);
                _endlessSettings.maxGeneratedWaves = Mathf.Max(0, _endlessWaveAsset.maxGeneratedWaves);
            }
        }

        private List<WaveDefinition> BuildDefinitionsFromData(IReadOnlyList<WaveDefinitionData> source)
        {
            var result = new List<WaveDefinition>();
            if (source == null)
                return result;

            for (int index = 0; index < source.Count; index++)
            {
                var definitionData = source[index];
                if (definitionData == null)
                    continue;

                var definition = new WaveDefinition
                {
                    name = definitionData.name,
                    requiredPlayerWave = definitionData.requiredPlayerWave,
                    rewardCoins = ResolveConfiguredCampaignReward(index, definitionData.name)
                };

                if (definitionData.entries != null)
                {
                    foreach (var entryData in definitionData.entries)
                    {
                        if (entryData == null || entryData.prefab == null)
                            continue;

                        bool useAll = ShouldUseAllSpawnPoints(entryData);
                        var targets = BuildSpawnTargets(entryData.spawnPointKey, useAll);
                        if (targets.Count == 0)
                        {
                            if (useAll)
                            {
                                if (_debugLogs)
                                {
                                    Debug.LogWarning($"[WaveManager] Wave '{definitionData.name}' requested spawn on all points, but no spawn points are cached.", this);
                                }
                            }
                            else if (!string.IsNullOrWhiteSpace(entryData.spawnPointKey) && _debugLogs)
                            {
                                Debug.LogWarning($"[WaveManager] No spawn point found for key '{entryData.spawnPointKey}' in wave '{definitionData.name}'. Entry skipped.", this);
                            }
                            continue;
                        }

                        foreach (var target in targets)
                        {
                            var entry = new WaveEntry
                            {
                                prefab = entryData.prefab,
                                count = Mathf.Max(0, entryData.count),
                                spawnPoint = target.SpawnPoint,
                                spawnPointKey = target.Key,
                                spawnInterval = entryData.spawnInterval
                            };

                            definition.entries.Add(entry);
                        }
                    }
                }

                result.Add(definition);
            }

            return result;
        }

        private SpawnPoint ResolveSpawnPoint(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            if (_spawnPointLookup != null && _spawnPointLookup.TryGetValue(key, out var spawnPoint))
                return spawnPoint;

            return null;
        }

        private SpawnPoint ResolveSpawnPointOrFallback(string key)
        {
            var spawn = ResolveSpawnPoint(key);
            if (spawn != null)
                return spawn;

            if (_spawnPointFallback.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, _spawnPointFallback.Count);
                return _spawnPointFallback[index];
            }

            return null;
        }

        private SpawnPoint ResolveSpawnPointWithFallbackIndex(string key, out int fallbackIndex)
        {
            fallbackIndex = -1;
            var spawn = ResolveSpawnPoint(key);
            if (spawn != null)
                return spawn;

            if (_spawnPointFallback.Count > 0)
            {
                fallbackIndex = UnityEngine.Random.Range(0, _spawnPointFallback.Count);
                return _spawnPointFallback[fallbackIndex];
            }

            return null;
        }

        private SpawnPoint ResolveSpawnPointFromCache(string key, int fallbackIndex)
        {
            var spawn = ResolveSpawnPoint(key);
            if (spawn != null)
                return spawn;

            if (fallbackIndex >= 0 && fallbackIndex < _spawnPointFallback.Count)
                return _spawnPointFallback[fallbackIndex];

            return ResolveSpawnPointOrFallback(key);
        }

        private string GetKeyForSpawnPoint(SpawnPoint spawnPoint)
        {
            if (spawnPoint == null)
                return string.Empty;

            if (_spawnPointReverseLookup.TryGetValue(spawnPoint, out var key) && key != null)
                return key;

            return string.Empty;
        }

        private int GetFallbackIndexForSpawn(SpawnPoint spawnPoint)
        {
            if (spawnPoint == null)
                return -1;

            for (int i = 0; i < _spawnPointFallback.Count; i++)
            {
                if (_spawnPointFallback[i] == spawnPoint)
                    return i;
            }

            return -1;
        }

        private List<SpawnSelection> BuildSpawnTargets(string spawnPointKey, bool useAllSpawnPoints)
        {
            _spawnTargetCache.Clear();

            if (useAllSpawnPoints)
            {
                for (int i = 0; i < _spawnPointFallback.Count; i++)
                {
                    var spawn = _spawnPointFallback[i];
                    if (spawn == null)
                        continue;

                    _spawnTargetCache.Add(new SpawnSelection
                    {
                        SpawnPoint = spawn,
                        Key = GetKeyForSpawnPoint(spawn),
                        FallbackIndex = i
                    });
                }

                return _spawnTargetCache;
            }

            var resolved = ResolveSpawnPointWithFallbackIndex(spawnPointKey, out int fallbackIndex);
            if (resolved == null)
                return _spawnTargetCache;

            string resolvedKey = !string.IsNullOrWhiteSpace(spawnPointKey) ? spawnPointKey : GetKeyForSpawnPoint(resolved);
            if (fallbackIndex < 0)
                fallbackIndex = GetFallbackIndexForSpawn(resolved);

            _spawnTargetCache.Add(new SpawnSelection
            {
                SpawnPoint = resolved,
                Key = resolvedKey,
                FallbackIndex = fallbackIndex
            });

            return _spawnTargetCache;
        }

        // Uses reflection so the manager stays compatible with assets authored before the flag existed.
        private static bool ShouldUseAllSpawnPoints(WaveEntryData data)
        {
            if (data == null)
                return false;

            if (s_WaveEntryUseAllField == null)
            {
                s_WaveEntryUseAllField = typeof(WaveEntryData).GetField("useAllSpawnPoints", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            if (s_WaveEntryUseAllField != null && s_WaveEntryUseAllField.FieldType == typeof(bool))
                return (bool)s_WaveEntryUseAllField.GetValue(data);

            return string.IsNullOrWhiteSpace(data.spawnPointKey);
        }

        // Uses reflection so the manager stays compatible with assets authored before the flag existed.
        private static bool ShouldUseAllSpawnPoints(EndlessSpawnEntryData data)
        {
            if (data == null)
                return false;

            if (s_EndlessEntryUseAllField == null)
            {
                s_EndlessEntryUseAllField = typeof(EndlessSpawnEntryData).GetField("useAllSpawnPoints", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            if (s_EndlessEntryUseAllField != null && s_EndlessEntryUseAllField.FieldType == typeof(bool))
                return (bool)s_EndlessEntryUseAllField.GetValue(data);

            return string.IsNullOrWhiteSpace(data.spawnPointKey);
        }

        private List<EndlessPrefabEntry> BuildEndlessEntries(IReadOnlyList<EndlessSpawnEntryData> source)
        {
            var result = new List<EndlessPrefabEntry>();
            if (source == null)
                return result;

            foreach (var entryData in source)
            {
                if (entryData == null || entryData.prefab == null)
                    continue;

                int minCount = Mathf.Max(1, entryData.minCount);
                int maxCount = Mathf.Max(minCount, entryData.maxCount);
                bool useAll = ShouldUseAllSpawnPoints(entryData);

                result.Add(new EndlessPrefabEntry
                {
                    prefab = entryData.prefab,
                    spawnPointKey = entryData.spawnPointKey,
                    minCount = minCount,
                    maxCount = maxCount,
                    spawnInterval = entryData.spawnInterval,
                    useAllSpawnPoints = useAll
                });
            }

            return result;
        }

        private void LoadProceduralCampaignWaves()
        {
            _runtimeWaves.Clear();

            var asset = _campaignWaveAsset;
            var pool = asset?.ProceduralEntries;
            if (pool == null || pool.Count == 0)
            {
                if (_debugLogs)
                    Debug.LogWarning("[WaveManager] Campaign procedural generation requested, but procedural entries list is empty.", this);
                return;
            }

            int configuredWaveCount = asset.Waves != null ? asset.Waves.Count : 0;
            int targetWaveCount = configuredWaveCount > 0 ? configuredWaveCount : Mathf.Max(0, asset.proceduralWaveCount);
            if (targetWaveCount <= 0)
            {
                if (_debugLogs)
                    Debug.LogWarning("[WaveManager] Campaign procedural generation has no waves configured.", this);
                return;
            }

            string cacheId = !string.IsNullOrWhiteSpace(asset.cacheId) ? asset.cacheId : asset.name;
            var cache = CampaignWaveCacheStorage.Load(cacheId);
            bool cacheValid = cache != null && cache.waveCount == targetWaveCount && string.Equals(cache.cacheVersion, asset.cacheVersion, StringComparison.Ordinal);

            if (cacheValid)
            {
                if (TryBuildCampaignWavesFromCache(cache, pool, asset))
                    return;

                if (_debugLogs)
                    Debug.LogWarning("[WaveManager] Campaign wave cache invalid. Regenerating.", this);
            }

            GenerateAndPersistCampaignWaves(pool, asset, targetWaveCount, cacheId);
        }

        private bool TryBuildCampaignWavesFromCache(CampaignWaveCacheFile cache, IReadOnlyList<EndlessSpawnEntryData> pool, CampaignWaveAsset asset)
        {
            if (cache == null)
                return false;

            _runtimeWaves.Clear();

            for (int i = 0; i < cache.waves.Count; i++)
            {
                var record = cache.waves[i];
                string waveName = string.IsNullOrWhiteSpace(record.name) ? $"Campaign Wave #{i + 1}" : record.name;
                var wave = new WaveDefinition
                {
                    name = waveName,
                    requiredPlayerWave = ParseVoiceWave(record.requiredPlayerWave, asset, i),
                    rewardCoins = ResolveCachedReward(record.rewardCoins, i, waveName)
                };

                foreach (var entry in record.entries)
                {
                    if (entry.prefabIndex < 0 || entry.prefabIndex >= pool.Count)
                    {
                        _runtimeWaves.Clear();
                        return false;
                    }

                    var source = pool[entry.prefabIndex];
                    if (source == null || source.prefab == null)
                    {
                        _runtimeWaves.Clear();
                        return false;
                    }

                    var spawnPoint = ResolveSpawnPointFromCache(entry.spawnPointKey, entry.fallbackIndex);
                    if (spawnPoint == null)
                    {
                        if (_debugLogs)
                            Debug.LogWarning($"[WaveManager] Cached campaign entry failed to resolve spawn point (wave {i}).", this);
                        _runtimeWaves.Clear();
                        return false;
                    }

                    wave.entries.Add(new WaveEntry
                    {
                        prefab = source.prefab,
                        count = Mathf.Max(0, entry.count),
                        spawnPoint = spawnPoint,
                        spawnPointKey = entry.spawnPointKey,
                        spawnInterval = entry.spawnInterval
                    });
                }

                _runtimeWaves.Add(wave);
            }

            return _runtimeWaves.Count == cache.waveCount;
        }

        private void GenerateAndPersistCampaignWaves(IReadOnlyList<EndlessSpawnEntryData> pool, CampaignWaveAsset asset, int waveCount, string cacheId)
        {
            _runtimeWaves.Clear();

            int minGroups = Mathf.Max(1, asset.proceduralGroupsPerWaveRange.x);
            int maxGroups = Mathf.Max(minGroups, asset.proceduralGroupsPerWaveRange.y);

            var cacheFile = new CampaignWaveCacheFile
            {
                cacheId = cacheId,
                cacheVersion = asset.cacheVersion,
                waveCount = waveCount
            };

            for (int i = 0; i < waveCount; i++)
            {
                var meta = asset.Waves != null && i < asset.Waves.Count ? asset.Waves[i] : null;
                var wave = new WaveDefinition
                {
                    name = meta?.name ?? $"Campaign Wave #{i + 1}",
                    requiredPlayerWave = meta?.requiredPlayerWave ?? VoiceWaveType.Mid,
                    rewardCoins = ResolveProceduralCampaignReward(i, meta?.name)
                };

                var cacheRecord = new CampaignWaveCacheRecord
                {
                    name = wave.name,
                    requiredPlayerWave = wave.requiredPlayerWave.ToString(),
                    rewardCoins = wave.rewardCoins
                };

                int groupCount = UnityEngine.Random.Range(minGroups, maxGroups + 1);
                for (int g = 0; g < groupCount; g++)
                {
                    int entryIndex = UnityEngine.Random.Range(0, pool.Count);
                    var source = pool[entryIndex];
                    if (source == null || source.prefab == null)
                        continue;

                    bool useAll = ShouldUseAllSpawnPoints(source);
                    var targets = BuildSpawnTargets(source.spawnPointKey, useAll);
                    if (targets.Count == 0)
                    {
                        if (_debugLogs)
                        {
                            if (useAll)
                            {
                                Debug.LogWarning($"[WaveManager] Procedural campaign entry could not spawn '{source.prefab.name}' because no spawn points are cached (wave {i}).", this);
                            }
                            else
                            {
                                Debug.LogWarning($"[WaveManager] Procedural campaign entry could not resolve spawn point (wave {i}).", this);
                            }
                        }
                        continue;
                    }

                    int minCount = Mathf.Max(1, source.minCount);
                    int maxCount = Mathf.Max(minCount, source.maxCount);

                    float spawnInterval = source.spawnInterval >= 0f
                        ? source.spawnInterval
                        : asset.proceduralDefaultSpawnInterval;

                    foreach (var target in targets)
                    {
                        int count = UnityEngine.Random.Range(minCount, maxCount + 1);

                        wave.entries.Add(new WaveEntry
                        {
                            prefab = source.prefab,
                            count = count,
                            spawnPoint = target.SpawnPoint,
                            spawnPointKey = target.Key,
                            spawnInterval = spawnInterval
                        });

                        cacheRecord.entries.Add(new CampaignWaveCacheEntry
                        {
                            prefabIndex = entryIndex,
                            count = count,
                            spawnPointKey = string.IsNullOrWhiteSpace(target.Key) ? source.spawnPointKey : target.Key,
                            fallbackIndex = target.FallbackIndex,
                            spawnInterval = spawnInterval
                        });
                    }
                }

                _runtimeWaves.Add(wave);
                cacheFile.waves.Add(cacheRecord);
            }

            CampaignWaveCacheStorage.Save(cacheId, cacheFile);
        }

        private VoiceWaveType ParseVoiceWave(string raw, CampaignWaveAsset asset, int waveIndex)
        {
            if (!string.IsNullOrWhiteSpace(raw) && Enum.TryParse(raw, out VoiceWaveType parsed))
                return parsed;

            var meta = asset.Waves != null && waveIndex < asset.Waves.Count ? asset.Waves[waveIndex] : null;
            return meta?.requiredPlayerWave ?? VoiceWaveType.Mid;
        }

        private List<EndlessPrefabEntry> GetEndlessSpawnPool()
        {
            if (_endlessSettings == null)
                return null;

            if (_endlessSettings.spawnEntries == null || _endlessSettings.spawnEntries.Count == 0)
                return null;

            return _endlessSettings.spawnEntries;
        }

        private WaveDefinition GenerateEndlessWave(int index)
        {
            var pool = GetEndlessSpawnPool();
            if (pool == null || pool.Count == 0)
            {
                if (_debugLogs) Debug.LogWarning("[WaveManager] Endless mode has no spawn entries configured.");
                return null;
            }

            int minGroups = Mathf.Max(1, _endlessSettings.groupsPerWaveRange.x);
            int maxGroups = Mathf.Max(minGroups, _endlessSettings.groupsPerWaveRange.y);
            int groupCount = UnityEngine.Random.Range(minGroups, maxGroups + 1);

            var wave = new WaveDefinition
            {
                name = $"Endless Wave #{index + 1}",
                requiredPlayerWave = VoiceWaveType.Mid
            };

            for (int i = 0; i < groupCount; i++)
            {
                int entryIndex = UnityEngine.Random.Range(0, pool.Count);
                var prefabEntry = pool[entryIndex];
                if (prefabEntry == null || prefabEntry.prefab == null)
                    continue;

                var targets = BuildSpawnTargets(prefabEntry.spawnPointKey, prefabEntry.useAllSpawnPoints);
                if (targets.Count == 0)
                {
                    if (_debugLogs)
                    {
                        if (prefabEntry.useAllSpawnPoints)
                        {
                            Debug.LogWarning($"[WaveManager] Unable to spawn '{prefabEntry.prefab.name}' because no spawn points are registered.", this);
                        }
                        else
                        {
                            Debug.LogWarning($"[WaveManager] Unable to resolve spawn point for endless entry '{prefabEntry.prefab.name}'.", this);
                        }
                    }
                    continue;
                }

                int minCount = Mathf.Max(1, prefabEntry.minCount);
                int maxCount = Mathf.Max(minCount, prefabEntry.maxCount);
                float spawnInterval = prefabEntry.spawnInterval >= 0f
                    ? prefabEntry.spawnInterval
                    : _endlessSettings.defaultSpawnInterval;

                foreach (var target in targets)
                {
                    int count = UnityEngine.Random.Range(minCount, maxCount + 1);

                    wave.entries.Add(new WaveEntry
                    {
                        prefab = prefabEntry.prefab,
                        count = count,
                        spawnPoint = target.SpawnPoint,
                        spawnPointKey = target.Key,
                        spawnInterval = spawnInterval
                    });
                }
            }

            if (wave.entries.Count == 0)
                return null;

            return wave;
        }
    }
}
