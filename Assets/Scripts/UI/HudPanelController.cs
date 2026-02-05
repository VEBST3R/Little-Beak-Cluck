using LittleBeakCluck.Audio;
using LittleBeakCluck.Infrastructure;
using LittleBeakCluck.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    [DisallowMultipleComponent]
    public class HudPanelController : MonoBehaviour
    {
        [Header("Controls")]
        [SerializeField] private Button pauseButton;

        [Header("Wave Display")]
        [SerializeField] private TMP_Text waveLabel;
        [SerializeField] private string waveTextFormat = "WAVE {0}";
        [SerializeField] private string noWaveText = string.Empty;
        [SerializeField] private string loadingWaveText = "Loading wave";
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private bool autoFindWaveManager = true;

        [Header("Coin Display")]
        [SerializeField] private GameObject coinDisplayRoot;
        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private string coinTextFormat = "{0}";
        [SerializeField] private bool hideCoinsInCampaign = true;

        private IUIManager _uiManager;
        private WaveManager _resolvedWaveManager;
        private bool _waveListenersRegistered;
        private bool _isAwaitingNextWave;
        private bool _coinListenerRegistered;

        private void Awake()
        {
            TryResolveUiManager();
            EnsureWaveManager();
        }

        private void OnEnable()
        {
            TryResolveUiManager();
            RegisterPauseButton(true);
            EnsureWaveManager();
            RefreshWaveLabel();
            UpdateCoinVisibility();
        }

        private void OnDisable()
        {
            RegisterPauseButton(false);
            UnregisterWaveListeners();
            UnregisterCoinListener();
        }

        private void OnDestroy()
        {
            RegisterPauseButton(false);
            UnregisterWaveListeners();
            UnregisterCoinListener();
        }

        private void OnPauseClicked()
        {
            PlayClickSound();

            if (!TryResolveUiManager())
                return;

            _uiManager.ShowPauseMenu();
        }

        private void RegisterPauseButton(bool register)
        {
            if (pauseButton == null)
                return;

            pauseButton.onClick.RemoveListener(OnPauseClicked);
            if (register)
            {
                pauseButton.onClick.AddListener(OnPauseClicked);
            }
        }

        private void EnsureWaveManager()
        {
            if (_resolvedWaveManager == null)
            {
                if (waveManager != null)
                {
                    _resolvedWaveManager = waveManager;
                }
                else if (autoFindWaveManager)
                {
#if UNITY_2022_2_OR_NEWER
                    _resolvedWaveManager = Object.FindFirstObjectByType<WaveManager>(FindObjectsInactive.Include);
#else
                    foreach (var candidate in Resources.FindObjectsOfTypeAll<WaveManager>())
                    {
                        if (candidate == null)
                            continue;

                        if (!candidate.gameObject.scene.IsValid() || !candidate.gameObject.scene.isLoaded)
                            continue;

                        _resolvedWaveManager = candidate;
                        break;
                    }
#endif
                }
            }

            if (_resolvedWaveManager == null)
                return;

            if (!_coinListenerRegistered)
            {
                _resolvedWaveManager.CoinsChanged += HandleCoinsChanged;
                _coinListenerRegistered = true;
                HandleCoinsChanged(_resolvedWaveManager.TotalCoins);
            }

            if (!_waveListenersRegistered)
            {
                _resolvedWaveManager.WaveStarted += HandleWaveStarted;
                _resolvedWaveManager.WaveCooldownStarted += HandleWaveCooldownStarted;
                _resolvedWaveManager.WaveCompleted += HandleWaveCompleted;
                _waveListenersRegistered = true;
            }

            UpdateCoinVisibility();
        }

        private static void PlayClickSound()
        {
            var audio = ServiceLocator.Instance.Get<IAudioService>();
            audio?.PlayUiClick();
        }

        private void UnregisterWaveListeners()
        {
            if (!_waveListenersRegistered || _resolvedWaveManager == null)
                return;

            _resolvedWaveManager.WaveStarted -= HandleWaveStarted;
            _resolvedWaveManager.WaveCooldownStarted -= HandleWaveCooldownStarted;
            _resolvedWaveManager.WaveCompleted -= HandleWaveCompleted;
            _waveListenersRegistered = false;
        }

        private void UnregisterCoinListener()
        {
            if (!_coinListenerRegistered || _resolvedWaveManager == null)
                return;

            _resolvedWaveManager.CoinsChanged -= HandleCoinsChanged;
            _coinListenerRegistered = false;
        }

        private void HandleWaveStarted(int index, WaveDefinition definition)
        {
            UpdateCoinVisibility();
            _isAwaitingNextWave = false;
            UpdateWaveLabel(index);
        }

        private void HandleWaveCooldownStarted(int currentIndex, int nextIndex)
        {
            _isAwaitingNextWave = true;
            UpdateWaveLabel(nextIndex, true);
        }

        private void HandleWaveCompleted(int index, WaveDefinition definition)
        {
            if (_resolvedWaveManager == null)
                return;

            int total = _resolvedWaveManager.TotalWaveCount;
            if (total >= 0 && index + 1 >= total)
            {
                _isAwaitingNextWave = false;
                UpdateWaveLabel(-1);
            }
        }

        private void RefreshWaveLabel()
        {
            if (_isAwaitingNextWave)
            {
                int nextIndex = (_resolvedWaveManager != null) ? _resolvedWaveManager.CurrentWaveIndex + 1 : -1;
                UpdateWaveLabel(nextIndex, true);
                return;
            }

            if (_resolvedWaveManager != null && _resolvedWaveManager.CurrentWaveIndex >= 0)
            {
                UpdateWaveLabel(_resolvedWaveManager.CurrentWaveIndex);
            }
            else
            {
                UpdateWaveLabel(-1);
            }
        }

        private void UpdateWaveLabel(int waveIndex, bool loading = false)
        {
            if (waveLabel == null)
                return;

            if (loading)
            {
                waveLabel.text = loadingWaveText;
                return;
            }

            if (waveIndex < 0)
            {
                waveLabel.text = noWaveText;
            }
            else
            {
                waveLabel.text = string.Format(waveTextFormat, waveIndex + 1);
            }
        }

        private bool TryResolveUiManager()
        {
            if (_uiManager != null)
                return true;

            var locator = ServiceLocator.Instance;
            _uiManager = locator.Get<IUIManager>();

            if (_uiManager == null)
            {
#if UNITY_2022_2_OR_NEWER
                var component = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
#else
                UIManager component = null;
                foreach (var candidate in Resources.FindObjectsOfTypeAll<UIManager>())
                {
                    if (candidate == null)
                        continue;

                    if (!candidate.gameObject.scene.IsValid() || !candidate.gameObject.scene.isLoaded)
                        continue;

                    component = candidate;
                    break;
                }
#endif
                if (component != null)
                {
                    _uiManager = component;
                }
            }

            if (_uiManager == null)
            {
                Debug.LogWarning($"[{name}] HUD panel could not locate UIManager.", this);
                return false;
            }

            return true;
        }

        private void HandleCoinsChanged(int total)
        {
            if (coinLabel != null)
            {
                coinLabel.text = string.Format(coinTextFormat, Mathf.Max(0, total));
            }
        }

        private void UpdateCoinVisibility()
        {
            if (coinDisplayRoot == null)
                return;

            bool shouldShow = _resolvedWaveManager != null;
            if (shouldShow && hideCoinsInCampaign && !_resolvedWaveManager.IsEndless)
            {
                shouldShow = false;
            }

            if (coinDisplayRoot.activeSelf != shouldShow)
            {
                coinDisplayRoot.SetActive(shouldShow);
            }
        }
    }
}
