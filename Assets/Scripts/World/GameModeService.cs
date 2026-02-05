using LittleBeakCluck.Infrastructure;
using UnityEngine;

namespace LittleBeakCluck.World
{
    public class GameModeService : IGameModeService
    {
        private const string ModePrefKey = "GameMode.Current";
        private const string CampaignWavePrefKey = "GameMode.CampaignWave";

        private WaveMode _currentMode;
        private int _campaignStartWave;

        public WaveMode CurrentMode => _currentMode;

        public GameModeService()
        {
            _currentMode = (WaveMode)PlayerPrefs.GetInt(ModePrefKey, (int)WaveMode.Campaign);
            _campaignStartWave = Mathf.Max(0, PlayerPrefs.GetInt(CampaignWavePrefKey, 0));
        }

        public void SetMode(WaveMode mode, bool persist = true)
        {
            _currentMode = mode;
            if (persist)
            {
                PlayerPrefs.SetInt(ModePrefKey, (int)mode);
                PlayerPrefs.Save();
            }
        }

        public int GetCampaignStartWave() => Mathf.Max(0, _campaignStartWave);

        public void SaveCampaignProgress(int completedWaveIndex, bool persist = true)
        {
            int nextWave = Mathf.Max(0, completedWaveIndex + 1);
            _campaignStartWave = nextWave;
            if (persist)
            {
                PlayerPrefs.SetInt(CampaignWavePrefKey, _campaignStartWave);
                PlayerPrefs.Save();
            }
        }

        public void ResetCampaignProgress(bool persist = true)
        {
            _campaignStartWave = 0;
            if (persist)
            {
                PlayerPrefs.DeleteKey(CampaignWavePrefKey);
                PlayerPrefs.Save();
            }
        }
    }
}
