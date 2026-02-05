using LittleBeakCluck.Infrastructure;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LittleBeakCluck.UI
{
    [DisallowMultipleComponent]
    [Obfuscation(Feature = "rename", Exclude = true, ApplyToMembers = true)]
    public class UIManager : MonoBehaviour, IUIManager
    {
        [Header("Panels")]
        [SerializeField] private GameObject defeatPanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject playerHudRoot;

        [Header("Behaviour")]
        [SerializeField] private bool deactivatePanelsOnAwake = true;
        [SerializeField] private bool freezeTimeOnDefeat = true;
        [SerializeField] private bool freezeTimeOnVictory = true;
        [SerializeField] private bool freezeTimeOnPause = true;

        [Header("Navigation")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private bool _isPaused;
        private bool _registeredInLocator;

        private void Awake()
        {
            if (deactivatePanelsOnAwake)
            {
                SetPanelActive(defeatPanel, false);
                SetPanelActive(victoryPanel, false);
                SetPanelActive(pausePanel, false);
            }

            SetPlayerHudVisible(true);

            var locator = ServiceLocator.Instance;
            if (locator.Get<IUIManager>() == null)
            {
                locator.Register<IUIManager>(this);
                _registeredInLocator = true;
            }
        }

        private void OnDestroy()
        {
            if (_registeredInLocator && object.ReferenceEquals(ServiceLocator.Instance.Get<IUIManager>(), this))
            {
                // ServiceLocator does not currently support unregistration.
            }
        }

        public void ShowDefeatMenu()
        {
            HideAllMenus();
            SetPanelActive(defeatPanel, true);
            if (freezeTimeOnDefeat)
            {
                PauseTime();
            }
            SetPlayerHudVisible(false);
        }

        public void HideDefeatMenu()
        {
            SetPanelActive(defeatPanel, false);
            SetPlayerHudVisible(true);
        }

        public void ShowVictoryMenu()
        {
            HideAllMenus();
            SetPanelActive(victoryPanel, true);
            if (freezeTimeOnVictory)
            {
                PauseTime();
            }
            SetPlayerHudVisible(false);
        }

        public void HideVictoryMenu()
        {
            SetPanelActive(victoryPanel, false);
            SetPlayerHudVisible(true);
        }

        public void ShowPauseMenu()
        {
            if (_isPaused)
                return;

            SetPanelActive(pausePanel, true);
            SetPanelActive(defeatPanel, false);
            SetPanelActive(victoryPanel, false);

            if (freezeTimeOnPause)
            {
                PauseTime();
            }

            _isPaused = true;
            SetPlayerHudVisible(false);
        }

        public void HidePauseMenu()
        {
            if (!_isPaused)
                return;

            SetPanelActive(pausePanel, false);
            if (freezeTimeOnPause)
            {
                ResumeTime();
            }
            _isPaused = false;
            SetPlayerHudVisible(true);
        }

        public void HideAllMenus()
        {
            SetPanelActive(defeatPanel, false);
            SetPanelActive(victoryPanel, false);
            SetPanelActive(pausePanel, false);
            _isPaused = false;
            SetPlayerHudVisible(true);
        }

        public void RestartLevel()
        {
            ResumeTime();
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        public void LoadMainMenu()
        {
            if (string.IsNullOrWhiteSpace(mainMenuSceneName))
            {
                Debug.LogWarning("UIManager: Main menu scene name is not set.", this);
                return;
            }

            ResumeTime();
            SceneManager.LoadScene(mainMenuSceneName);
        }

        public void ResumeGame()
        {
            HidePauseMenu();
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        }

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel == null)
                return;

            if (panel.activeSelf != active)
            {
                panel.SetActive(active);
            }
        }

        private void SetPlayerHudVisible(bool visible)
        {
            if (playerHudRoot == null)
                return;

            if (playerHudRoot.activeSelf != visible)
            {
                playerHudRoot.SetActive(visible);
            }
        }

        private void PauseTime()
        {
            Time.timeScale = 0f;
        }

        private void ResumeTime()
        {
            Time.timeScale = 1f;
        }
    }
}
