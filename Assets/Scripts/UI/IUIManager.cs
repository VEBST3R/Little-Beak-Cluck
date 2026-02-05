using LittleBeakCluck.Infrastructure;

namespace LittleBeakCluck.UI
{
    public interface IUIManager : IGameService
    {
        void ShowDefeatMenu();
        void HideDefeatMenu();
        void ShowVictoryMenu();
        void HideVictoryMenu();
        void ShowPauseMenu();
        void HidePauseMenu();
        void HideAllMenus();
        void RestartLevel();
        void LoadMainMenu();
        void ResumeGame();
        void QuitGame();
    }
}
