using LittleBeakCluck.Audio;
using LittleBeakCluck.Infrastructure;
using LittleBeakCluck.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    [DisallowMultipleComponent]
    public class MainMenuGameModeLauncher : MonoBehaviour
    {
        [Header("Scene Settings")]
        [SerializeField] private string gameplaySceneName = "Gameplay";
        [SerializeField] private LoadSceneMode loadMode = LoadSceneMode.Single;

        [Header("Buttons (optional)")]
        [SerializeField] private Button campaignButton;
        [SerializeField] private Button endlessButton;
        [SerializeField] private bool autoRegisterButtons = true;

        [Header("Campaign Options")]
        [SerializeField] private bool resetCampaignProgressOnLaunch = false;

        private IGameModeService _gameModeService;

        private void Awake()
        {
            ResolveGameModeService();
        }

        private void OnEnable()
        {
            if (autoRegisterButtons)
            {
                RegisterButtons(true);
            }
        }

        private void OnDisable()
        {
            if (autoRegisterButtons)
            {
                RegisterButtons(false);
            }
        }

        public void StartCampaignGame()
        {
            PlayClickSound();

            ApplyMode(WaveMode.Campaign);
            if (resetCampaignProgressOnLaunch)
            {
                _gameModeService.ResetCampaignProgress(persist: true);
            }

            LoadGameplayScene();
        }

        public void StartEndlessGame()
        {
            PlayClickSound();

            ApplyMode(WaveMode.Endless);
            LoadGameplayScene();
        }

        private void ApplyMode(WaveMode mode)
        {
            ResolveGameModeService();
            _gameModeService.SetMode(mode, persist: true);
        }

        private void LoadGameplayScene()
        {
            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                Debug.LogWarning("MainMenuGameModeLauncher: Gameplay scene name is empty.", this);
                return;
            }

            SceneManager.LoadScene(gameplaySceneName, loadMode);
        }

        private void ResolveGameModeService()
        {
            if (_gameModeService != null)
                return;

            var locator = ServiceLocator.Instance;
            _gameModeService = locator.Get<IGameModeService>();
            if (_gameModeService == null)
            {
                var createdService = new GameModeService();
                createdService.SetMode(WaveMode.Campaign, persist: false);
                locator.Register<IGameModeService>(createdService);
                _gameModeService = createdService;
            }
        }

        private void RegisterButtons(bool register)
        {
            if (campaignButton != null)
            {
                campaignButton.onClick.RemoveListener(StartCampaignGame);
                if (register)
                {
                    campaignButton.onClick.AddListener(StartCampaignGame);
                }
            }

            if (endlessButton != null)
            {
                endlessButton.onClick.RemoveListener(StartEndlessGame);
                if (register)
                {
                    endlessButton.onClick.AddListener(StartEndlessGame);
                }
            }
        }

        private static void PlayClickSound()
        {
            var audio = ServiceLocator.Instance.Get<IAudioService>();
            audio?.PlayUiClick();
        }
    }
}
