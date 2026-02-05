using System;
using LittleBeakCluck.Audio;
using LittleBeakCluck.Infrastructure;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    [DisallowMultipleComponent]
    public class MainMenuPanelSwitcher : MonoBehaviour
    {
        [Serializable]
        private struct PanelLink
        {
            public Button button;
            public GameObject panel;
            [NonSerialized] public UnityAction callback;
        }

        [Header("Setup")]
        [SerializeField] private GameObject defaultPanel;
        [SerializeField] private PanelLink[] links = Array.Empty<PanelLink>();

        private GameObject _currentPanel;

        private void Awake()
        {
            InitializeCurrentPanel();
        }

        private void OnEnable()
        {
            RegisterLinks(true);
        }

        private void OnDisable()
        {
            RegisterLinks(false);
        }

        private void InitializeCurrentPanel()
        {
            _currentPanel = defaultPanel != null ? defaultPanel : FindActivePanel();

            if (_currentPanel == null)
                return;

            ActivatePanel(_currentPanel);
        }

        private GameObject FindActivePanel()
        {
            for (int i = 0; i < links.Length; i++)
            {
                var candidate = links[i].panel;
                if (candidate != null && candidate.activeSelf)
                    return candidate;
            }

            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].panel != null)
                    return links[i].panel;
            }

            return null;
        }

        private void RegisterLinks(bool register)
        {
            for (int i = 0; i < links.Length; i++)
            {
                var entry = links[i];
                if (entry.button == null)
                    continue;

                if (register)
                {
                    if (entry.callback == null)
                    {
                        GameObject targetPanel = entry.panel;
                        entry.callback = () => SwitchTo(targetPanel);
                    }
                    if (entry.callback != null)
                    {
                        entry.button.onClick.RemoveListener(entry.callback);
                    }
                    entry.button.onClick.AddListener(entry.callback);
                }
                else if (entry.callback != null)
                {
                    entry.button.onClick.RemoveListener(entry.callback);
                }

                links[i] = entry;
            }
        }

        private void SwitchTo(GameObject target)
        {
            PlayClickSound();

            if (target == null)
                return;

            if (defaultPanel != null && defaultPanel != target)
            {
                defaultPanel.SetActive(false);
            }

            if (_currentPanel == target)
            {
                ActivatePanel(target);
                return;
            }

            if (_currentPanel != null)
            {
                _currentPanel.SetActive(false);
            }

            ActivatePanel(target);
            _currentPanel = target;
        }

        private void ActivatePanel(GameObject panel)
        {
            if (panel == null)
                return;

            if (!panel.activeSelf)
            {
                panel.SetActive(true);
            }
        }

        private static void PlayClickSound()
        {
            var audio = ServiceLocator.Instance.Get<IAudioService>();
            audio?.PlayUiClick();
        }
    }
}
