using UnityEngine;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    /// <summary>
    /// Вішається на той самий GameObject, де знаходиться Toggle (або будь-який інший) і міняє спрайт Image
    /// залежно від стану toggle.isOn.
    /// Можна використати для кожного з трьох тоглів окремо.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    public class ToggleSpriteSwitcher : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Якщо не вказано — буде використано Image на цьому ж об'єкті (якщо є).")]
        [SerializeField] private Image _targetImage;
        [SerializeField] private Toggle _toggle; // можна не задавати вручну — підтягнеться автоматично

        [Header("Sprites")]
        [SerializeField] private Sprite _onSprite;
        [SerializeField] private Sprite _offSprite;

        [Header("Options")]
        [Tooltip("Якщо true — викличе SetNativeSize після зміни спрайта.")]
        [SerializeField] private bool _setNativeSize = false;
        [Tooltip("Оновлювати спрайт у режимі редагування при зміні значень в інспекторі.")]
        [SerializeField] private bool _autoRefreshInEditor = true;

        private void Awake()
        {
            if (_toggle == null)
                _toggle = GetComponent<Toggle>();

            if (_targetImage == null)
                _targetImage = GetComponent<Image>();
        }

        private void OnEnable()
        {
            if (_toggle != null)
            {
                _toggle.onValueChanged.AddListener(OnToggleChanged);
                ApplySprite(_toggle.isOn);
            }
        }

        private void OnDisable()
        {
            if (_toggle != null)
                _toggle.onValueChanged.RemoveListener(OnToggleChanged);
        }

        private void OnValidate()
        {
            if (_autoRefreshInEditor && _toggle != null)
            {
                if (_targetImage == null)
                    _targetImage = GetComponent<Image>();
                ApplySprite(_toggle.isOn);
            }
        }

        private void OnToggleChanged(bool isOn)
        {
            ApplySprite(isOn);
        }

        private void ApplySprite(bool isOn)
        {
            if (_targetImage == null) return;

            var newSprite = isOn ? _onSprite : _offSprite;
            if (newSprite != null && _targetImage.sprite != newSprite)
            {
                _targetImage.sprite = newSprite;
                if (_setNativeSize)
                    _targetImage.SetNativeSize();
            }
        }

        // Публічний метод, якщо треба вручну примусово оновити
        public void Refresh()
        {
            if (_toggle != null)
                ApplySprite(_toggle.isOn);
        }
    }
}
