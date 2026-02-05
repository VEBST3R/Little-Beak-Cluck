using LittleBeakCluck.Audio;
using LittleBeakCluck.Infrastructure;
using LittleBeakCluck.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    [DisallowMultipleComponent]
    public class WinPanelController : MonoBehaviour
    {
        [SerializeField] private Button continueButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private bool autoFindWaveManager = true;

        [Header("Coin Display")]
        [SerializeField] private TMP_Text coinsLabel;
        [SerializeField] private string coinsFormat = "{0}";
        [SerializeField] private GameObject coinsRoot;

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
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
                continueButton.onClick.AddListener(OnContinueClicked);
            }

            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(OnMenuClicked);
                menuButton.onClick.AddListener(OnMenuClicked);
            }
        }

        private void UnregisterListeners()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
            }

            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(OnMenuClicked);
            }
        }

        private void OnContinueClicked()
        {
            PlayClickSound();

            if (!TryResolveUiManager())
                return;

            _uiManager.HideVictoryMenu();
            _uiManager.ResumeGame();

            NotifyCampaignController();
        }

        private void OnMenuClicked()
        {
            PlayClickSound();

            if (!TryResolveUiManager())
                return;

            _uiManager.LoadMainMenu();
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
                Debug.LogWarning($"[{name}] Win panel could not locate UIManager.", this);
                return false;
            }

            return true;
        }

        private static void NotifyCampaignController()
        {
            var locator = ServiceLocator.Instance;
            var controller = locator.Get<ICampaignWaveController>();
            controller?.ContinueCampaignAfterVictory();
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
            if (coinsRoot != null)
            {
                bool shouldShow = _resolvedWaveManager != null;
                if (coinsRoot.activeSelf != shouldShow)
                {
                    coinsRoot.SetActive(shouldShow);
                }
            }

            if (coinsLabel == null || _resolvedWaveManager == null)
                return;

            int reward = Mathf.Max(0, _resolvedWaveManager.LastCampaignRewardCoins);
            int totalCoins = Mathf.Max(0, _resolvedWaveManager.TotalCoins);
            coinsLabel.text = string.Format(coinsFormat, reward, totalCoins);
        }

        private static void PlayClickSound()
        {
            var audio = ServiceLocator.Instance.Get<IAudioService>();
            audio?.PlayUiClick();
        }

        private static void PlayPanelOpenSound()
        {
            var audio = ServiceLocator.Instance.Get<IAudioService>();
            audio?.PlayWinPanelOpened();
        }
    }
}
