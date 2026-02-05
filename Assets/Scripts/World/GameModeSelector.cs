using LittleBeakCluck.Infrastructure;
using System.Reflection;
using UnityEngine;

namespace LittleBeakCluck.World
{
    [Obfuscation(Feature = "rename", Exclude = true, ApplyToMembers = true)]
    public class GameModeSelector : MonoBehaviour
    {
        [SerializeField] private WaveMode defaultMode = WaveMode.Campaign;
        [SerializeField] private bool applyOnAwake = true;
        [SerializeField] private bool overrideExistingMode = false;
        [SerializeField] private bool resetCampaignProgressOnModeSwitch = false;

        private IGameModeService _gameModeService;

        private void Awake()
        {
            ResolveService();

            if (applyOnAwake)
            {
                if (overrideExistingMode || _gameModeService.CurrentMode == defaultMode)
                {
                    SetMode(defaultMode, persist: true);
                }
            }
        }

        public void SetMode(WaveMode mode) => SetMode(mode, persist: true);

        public void SetCampaignMode() => SetMode(WaveMode.Campaign, persist: true);

        public void SetEndlessMode() => SetMode(WaveMode.Endless, persist: true);

        public void ResetCampaignProgress()
        {
            ResolveService();
            _gameModeService.ResetCampaignProgress();
        }

        private void SetMode(WaveMode mode, bool persist)
        {
            ResolveService();
            _gameModeService.SetMode(mode, persist);

            if (mode == WaveMode.Campaign && resetCampaignProgressOnModeSwitch)
            {
                _gameModeService.ResetCampaignProgress(persist);
            }
        }

        private void ResolveService()
        {
            if (_gameModeService != null)
                return;

            var locator = ServiceLocator.Instance;
            _gameModeService = locator.Get<IGameModeService>();
            if (_gameModeService == null)
            {
                var createdService = new GameModeService();
                createdService.SetMode(defaultMode, persist: false);
                locator.Register<IGameModeService>(createdService);
                _gameModeService = createdService;
            }
        }
    }
}
