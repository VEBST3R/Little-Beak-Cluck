using LittleBeakCluck.Audio;
using LittleBeakCluck.Infrastructure;
using LittleBeakCluck.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    [DisallowMultipleComponent]
    public class DefeatPanelController : MonoBehaviour
    {
        [SerializeField] private Button menuButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private bool autoFindWaveManager = true;

        [Header("Coin Display")]
        [SerializeField] private GameObject coinsRoot;
        [SerializeField] private TMP_Text coinsLabel;
        [SerializeField] private string coinsFormat = "{0}";

        private IUIManager _uiManager;
        private WaveManager _resolvedWaveManager;

        private void Awake()
        {
            TryResolveUiManager();
        }

        private void OnEnable()
        {
            PlayPanelOpenSound();
            TryResolveUiManager();
            EnsureWaveManager();
            RegisterListeners();
            if (coinsRoot != null)
            {
                coinsRoot.SetActive(false);
            }
            UpdateCoinDisplay();
        }

        private void OnDisable()
        {
            UnregisterListeners();
        }

        private void OnDestroy()
        {
            UnregisterListeners();
        }

        private void RegisterListeners()
        {
            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(OnMenuClicked);
                menuButton.onClick.AddListener(OnMenuClicked);
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
                restartButton.onClick.AddListener(OnRestartClicked);
            }
        }

        private void UnregisterListeners()
        {
            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(OnMenuClicked);
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartClicked);
            }
        }

        private void OnMenuClicked()
        {
            PlayClickSound();

            if (!TryResolveUiManager())
                return;

            _uiManager.LoadMainMenu();
        }

        private void OnRestartClicked()
        {
            PlayClickSound();

            if (!TryResolveUiManager())
                return;

            _uiManager.RestartLevel();
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
                Debug.LogWarning($"[{name}] Defeat panel could not locate UIManager.", this);
                return false;
            }

            return true;
        }

        private void EnsureWaveManager()
        {
            if (_resolvedWaveManager != null)
                return;

            if (waveManager != null)
            {
                _resolvedWaveManager = waveManager;
                return;
            }

            if (!autoFindWaveManager)
                return;

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

        private void UpdateCoinDisplay()
        {
            bool shouldShowCoins = false;
            if (_resolvedWaveManager != null)
            {
                shouldShowCoins = _resolvedWaveManager.Mode == WaveMode.Endless;
            }

            if (coinsRoot != null)
            {
                if (coinsRoot.activeSelf != shouldShowCoins)
                {
                    coinsRoot.SetActive(shouldShowCoins);
                }
            }

            if (!shouldShowCoins || coinsLabel == null || _resolvedWaveManager == null)
                return;

            int totalCoins = Mathf.Max(0, _resolvedWaveManager.TotalCoins);
            coinsLabel.text = string.Format(coinsFormat, totalCoins);
        }

        private static void PlayClickSound()
        {
            var audio = ServiceLocator.Instance.Get<IAudioService>();
            audio?.PlayUiClick();
        }

        private static void PlayPanelOpenSound()
        {
            var audio = ServiceLocator.Instance.Get<IAudioService>();
            audio?.PlayDefeatPanelOpened();
        }
    }
}
