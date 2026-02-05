using LittleBeakCluck.Audio;
using LittleBeakCluck.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    [DisallowMultipleComponent]
    public class PausePanelController : MonoBehaviour
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button menuButton;

        private IUIManager _uiManager;

        private void Awake()
        {
            TryResolveUiManager();
        }

        private void OnEnable()
        {
            TryResolveUiManager();
            RegisterListeners();
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
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(OnResumeClicked);
                resumeButton.onClick.AddListener(OnResumeClicked);
            }

            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(OnMenuClicked);
                menuButton.onClick.AddListener(OnMenuClicked);
            }
        }

        private void UnregisterListeners()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(OnResumeClicked);
            }

            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(OnMenuClicked);
            }
        }

        private void OnResumeClicked()
        {
            PlayClickSound();

            if (!TryResolveUiManager())
                return;

            _uiManager.ResumeGame();
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
                Debug.LogWarning($"[{name}] Pause panel could not locate UIManager.", this);
                return false;
            }

            return true;
        }

        private static void PlayClickSound()
        {
            var audio = ServiceLocator.Instance.Get<IAudioService>();
            audio?.PlayUiClick();
        }
    }
}
