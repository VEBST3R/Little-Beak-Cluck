using LittleBeakCluck.Infrastructure;

namespace LittleBeakCluck.World
{
    public interface IGameModeService : IGameService
    {
        WaveMode CurrentMode { get; }

        /// <summary>
        /// Allows overriding the current game mode, optionally persisting the choice.
        /// </summary>
        void SetMode(WaveMode mode, bool persist = true);

        /// <summary>
        /// Returns the wave index that the campaign should resume from.
        /// </summary>
        int GetCampaignStartWave();

        /// <summary>
        /// Saves campaign progress based on the completed wave.
        /// </summary>
        void SaveCampaignProgress(int completedWaveIndex, bool persist = true);

        /// <summary>
        /// Clears stored campaign progress so a new run starts from the first wave.
        /// </summary>
        void ResetCampaignProgress(bool persist = true);
    }
}
