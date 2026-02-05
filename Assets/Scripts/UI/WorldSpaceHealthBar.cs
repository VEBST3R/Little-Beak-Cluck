using UnityEngine;
using UnityEngine.UI;
using LittleBeakCluck.Player;
using LittleBeakCluck.Combat;

namespace LittleBeakCluck.UI
{
    public class WorldSpaceHealthBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Slider _slider;

        [Header("Target")]
        [Tooltip("Якщо залишити пустим, спробує знайти IDamageable у батьківських об'єктах.")]
        [SerializeField] private MonoBehaviour _targetDamageable;
        private IDamageable _damageable;

        [Header("Orientation")]
        [Tooltip("Примусово тримає канвас вертикальним, навіть якщо батько обертається/падає.")]
        [SerializeField] private bool _keepUpright = true;
        [Tooltip("Розвертати канвас обличчям до головної камери (2D зазвичай можна вимкнути).")]
        [SerializeField] private bool _billboardToCamera = false;
        [Tooltip("Компенсувати інверсію по X від фліпу батьківського масштабу, щоб UI не дзеркалився.")]
        [SerializeField] private bool _counterFlipScaleX = true;

        [Header("Visual Options")]
        [SerializeField] private bool _hideWhenFull = false;

        private bool _subscribed;

        private Vector3 _initialLocalScale;
        private PlayerController _playerController;

        private void Awake()
        {
            if (_slider == null) _slider = GetComponentInChildren<Slider>();

            _initialLocalScale = transform.localScale;

            if (_targetDamageable is IDamageable d)
            {
                SetTarget(d);
            }
            else
            {
                var parentDamageable = GetComponentInParent<IDamageable>();
                if (parentDamageable != null)
                {
                    SetTarget(parentDamageable);
                }
                else
                {
                    var playerHealth = FindFirstObjectByType<PlayerHealth>();
                    if (playerHealth != null)
                    {
                        SetTarget(playerHealth);
                    }
                }
            }

            if (_slider != null)
                _slider.value = 1f;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();

            if (_playerController != null)
            {
                _playerController.OnFlipped -= HandlePlayerFlipped;
                _playerController = null;
            }
        }

        private void Subscribe()
        {
            if (_subscribed || _damageable == null) return;

            _damageable.OnHealthChanged += HandleHealthChanged;
            _damageable.OnDied += HandleDied;
            
            if (_damageable != null)
            {
                HandleHealthChanged(_damageable.CurrentHealth, _damageable.MaxHealth);
            }
            
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _damageable == null) return;

            _damageable.OnHealthChanged -= HandleHealthChanged;
            _damageable.OnDied -= HandleDied;
            _subscribed = false;
        }

        private void HandleHealthChanged(float current, float max)
        {
            float fill = max > 0 ? current / max : 0f;
            if (_slider != null)
                _slider.value = fill;

            if (_hideWhenFull && _slider != null)
                _slider.gameObject.SetActive(fill < 0.999f);
        }

        private void HandleDied()
        {
            if (_slider != null)
                _slider.value = 0f;
        }

        public void SetTarget(IDamageable newTarget)
        {
            if (newTarget == _damageable) return;

            Unsubscribe();
            _damageable = newTarget;
            _targetDamageable = newTarget as MonoBehaviour;

            // Специфічна логіка для гравця (обертання)
            // Спершу відпишемось від попереднього, якщо був
            if (_playerController != null)
            {
                _playerController.OnFlipped -= HandlePlayerFlipped;
                _playerController = null;
            }

            _playerController = (newTarget as Component)?.GetComponent<PlayerController>();
            if (_playerController != null)
            {
                _playerController.OnFlipped += HandlePlayerFlipped;
            }

            Subscribe();
        }

        private void HandlePlayerFlipped(bool isFlippedLeft)
        {
            if (transform == null) return;
            transform.localRotation = isFlippedLeft ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
        }

        private void LateUpdate()
        {
            if (!_keepUpright) return;

            // Тримаємо канвас вертикальним у світі (ігноруємо обертання/падіння батька)
            if (_billboardToCamera && Camera.main != null)
            {
                // Повернути обличчям до камери, зберігши вісь Up
                var fwd = Camera.main.transform.forward;
                transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
            else
            {
                transform.rotation = Quaternion.identity;
            }

            // Компенсуємо фліп масштабу по X, щоб UI не дзеркалився при інверсії батька
            if (_counterFlipScaleX && transform.parent != null)
            {
                float parentSignX = Mathf.Sign(transform.parent.lossyScale.x);
                var ls = transform.localScale;
                ls.x = Mathf.Abs(_initialLocalScale.x) * parentSignX;
                transform.localScale = ls;
            }
        }

        public RectTransform RectTransform => transform as RectTransform;
    }
}
