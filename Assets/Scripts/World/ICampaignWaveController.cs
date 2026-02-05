using LittleBeakCluck.Infrastructure;

namespace LittleBeakCluck.World
{
    public interface ICampaignWaveController : IGameService
    {
        void ContinueCampaignAfterVictory();
    }
}
