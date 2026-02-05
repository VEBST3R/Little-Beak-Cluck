using UnityEngine;
using UnityEngine.SceneManagement;

namespace LittleBeakCluck.Audio
{
    public static class SceneContextResolver
    {
        public static string GetCurrentContextKey()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return AudioContextKeys.Menu;

            switch (activeScene.name)
            {
                case "MainMenu":
                    return AudioContextKeys.Menu;
                default:
                    return AudioContextKeys.Gameplay;
            }
        }
    }
}
