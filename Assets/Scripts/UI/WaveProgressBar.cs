using UnityEngine;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    // Прогрес-бар поточної хвилі: показує, скільки ворогів лишилось у хвилі
    public class WaveProgressBar : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private bool _hideWhenComplete = true;
        [SerializeField] private bool _normalized = true; // показувати 0..1 замість лічильника
        [SerializeField] private bool _forceSliderSetup = true; // примусово фіксуємо параметри слайдера
        [SerializeField] private bool _debugLogs = false;

        private int _total;
        private int _killed;
        private Coroutine _animRoutine;

        private void Awake()
        {
            // Беремо лише Slider на цьому ж об'єкті, щоб не зачепити чужі слайдери в Canvas
            if (_slider == null) _slider = GetComponent<Slider>();
            if (_slider == null)
            {
                Debug.LogWarning("WaveProgressBar: Slider reference is missing on the same GameObject.", this);
            }
        }

        private void OnEnable()
        {
            if (_slider != null && _forceSliderSetup)
            {
                _slider.wholeNumbers = false;
                _slider.interactable = false;
                var nav = _slider.navigation; nav.mode = UnityEngine.UI.Navigation.Mode.None; _slider.navigation = nav;
                _slider.minValue = 0f;
                _slider.maxValue = _normalized ? 1f : (_total > 0 ? _total : 1);
            }
        }

        public void SetWaveTotal(int total)
        {
            _total = Mathf.Max(0, total);
            _killed = 0;
            if (_slider != null)
            {
                _slider.minValue = 0f;
                if (_normalized)
                {
                    _slider.maxValue = 1f;
                    _slider.value = 0f;
                }
                else
                {
                    _slider.maxValue = _total > 0 ? _total : 1;
                    _slider.value = 0f;
                }
                _slider.gameObject.SetActive(true);
            }
            if (_debugLogs) Debug.Log($"[WaveProgressBar] SetWaveTotal total={_total}");
        }

        public void ReportEnemyKilled()
        {
            _killed = Mathf.Clamp(_killed + 1, 0, _total);
            if (_slider != null)
            {
                _slider.value = _normalized
                    ? (_total > 0 ? (float)_killed / _total : 1f)
                    : _killed;
            }
            if (_hideWhenComplete && _killed >= _total)
            {
                _slider?.gameObject.SetActive(false);
            }
            if (_debugLogs) Debug.Log($"[WaveProgressBar] ReportEnemyKilled killed={_killed}/{_total} value={_slider?.value}");
        }

        public void SetWaveComplete()
        {
            if (_slider != null)
            {
                _slider.value = _normalized ? 1f : _slider.maxValue;
                if (_hideWhenComplete)
                    _slider.gameObject.SetActive(false);
            }
            if (_debugLogs) Debug.Log("[WaveProgressBar] SetWaveComplete");
        }

        // Пряме встановлення нормалізованого значення (0..1)
        public void SetNormalized(float value)
        {
            if (_slider == null) return;
            if (!_normalized)
            {
                // якщо бар у режимі лічильника, перекладаємо нормалізоване в діапазон 0.._total
                _slider.value = Mathf.Lerp(0f, _total > 0 ? _total : 1, Mathf.Clamp01(value));
            }
            else
            {
                _slider.value = Mathf.Clamp01(value);
            }
            if (_debugLogs) Debug.Log($"[WaveProgressBar] SetNormalized {value} -> {_slider.value}");
        }

        // Плавно скидає значення до нуля за заданий час і гарантує, що слайдер видимий під час анімації
        public void ResetOverTime(float duration)
        {
            if (_slider == null)
                return;

            _slider.gameObject.SetActive(true);
            if (_animRoutine != null)
                StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(AnimateToZero(duration));
            if (_debugLogs) Debug.Log($"[WaveProgressBar] ResetOverTime duration={duration}");
        }

        private System.Collections.IEnumerator AnimateToZero(float duration)
        {
            float start = _slider.value;
            float t = 0f;
            duration = Mathf.Max(0.0001f, duration);
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float v = Mathf.Lerp(start, 0f, t);
                if (_normalized)
                {
                    _slider.value = Mathf.Clamp01(v);
                }
                else
                {
                    float max = _slider.maxValue <= 0 ? 1f : _slider.maxValue;
                    _slider.value = Mathf.Lerp(start, 0f, t);
                }
                yield return null;
            }
            _slider.value = 0f;
            _animRoutine = null;
        }
    }
}
