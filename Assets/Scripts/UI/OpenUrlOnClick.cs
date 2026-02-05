using UnityEngine;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class OpenUrlOnClick : MonoBehaviour
    {
        [Header("URL Settings")]
        [SerializeField] private string url = "https://example.com";
        [Tooltip("If true, will try to validate URL on start and log a warning if it's invalid.")]
        [SerializeField] private bool validateOnAwake = true;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_button == null)
            {
                Debug.LogError("[OpenUrlOnClick] Button component not found.", this);
                enabled = false;
                return;
            }

            if (validateOnAwake && !IsUrlLikelyValid(url))
            {
                Debug.LogWarning($"[OpenUrlOnClick] URL looks invalid: '{url}'.", this);
            }
        }

        private void OnEnable()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(OnClicked);
            }
        }

        private void OnDisable()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClicked);
            }
        }

        private void OnClicked()
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Debug.LogWarning("[OpenUrlOnClick] URL is empty.", this);
                return;
            }

            Application.OpenURL(url);
        }

        public void SetUrl(string newUrl)
        {
            url = newUrl;
        }

        private static bool IsUrlLikelyValid(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            return candidate.StartsWith("http://") || candidate.StartsWith("https://");
        }
    }
}
