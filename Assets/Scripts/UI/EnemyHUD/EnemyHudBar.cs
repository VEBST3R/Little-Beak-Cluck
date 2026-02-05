using UnityEngine;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    /// <summary>
    /// UI component responsible for displaying enemy health and wave icon.
    /// Acts as a thin wrapper around the underlying layout so the controller can manipulate it safely.
    /// </summary>
    public class EnemyHudBar : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private Slider _slider;
        [SerializeField] private Image _healthFill;
        [SerializeField] private Image _waveIcon;
        [SerializeField] private CanvasGroup _canvasGroup;

        private RectTransform _rectTransform;

        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = transform as RectTransform);

        private void Awake()
        {
            if (_root == null)
                _root = transform as RectTransform;

            if (_slider == null)
                _slider = GetComponentInChildren<Slider>();

            if (_slider != null)
            {
                _slider.minValue = 0f;
                _slider.maxValue = 1f;
                _slider.wholeNumbers = false;
                _slider.SetValueWithoutNotify(1f);
            }
        }

        public void SetFill(float normalized)
        {
            float clamped = Mathf.Clamp01(normalized);

            if (_slider != null)
            {
                _slider.SetValueWithoutNotify(clamped);
            }

            if (_healthFill != null)
            {
                _healthFill.fillAmount = clamped;
            }
        }

        public void SetWaveSprite(Sprite sprite)
        {
            if (_waveIcon == null) return;
            _waveIcon.sprite = sprite;
            _waveIcon.enabled = sprite != null;
        }

        public void SetWaveColor(Color color)
        {
            if (_waveIcon != null)
            {
                _waveIcon.color = color;
            }
        }

        public void SetBarColor(Color color)
        {
            if (_healthFill != null)
            {
                _healthFill.color = color;
            }
        }

        public void SetActive(bool active)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = active ? 1f : 0f;
                _canvasGroup.interactable = active;
                _canvasGroup.blocksRaycasts = active;
            }
            else
            {
                gameObject.SetActive(active);
            }

            if (_slider != null)
            {
                _slider.gameObject.SetActive(true);
            }
        }
    }
}
