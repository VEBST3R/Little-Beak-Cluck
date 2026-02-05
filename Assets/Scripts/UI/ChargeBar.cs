using UnityEngine;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    [RequireComponent(typeof(Slider))]
    public class ChargeBar : MonoBehaviour
    {
        private Slider _slider;
        private GameObject _sliderGameObject;

        private void Awake()
        {
            _slider = GetComponent<Slider>();
            _sliderGameObject = _slider.gameObject;
            Hide(); // Hide the bar by default
        }

        /// <summary>
        /// Updates the slider's fill amount.
        /// </summary>
        /// <param name="currentValue">The current charge value.</param>
        /// <param name="minValue">The minimum possible charge.</param>
        /// <param name="maxValue">The maximum possible charge.</param>
        public void UpdateBar(float currentValue, float minValue, float maxValue)
        {
            // Normalize the value to a 0-1 range for the slider
            _slider.value = (currentValue - minValue) / (maxValue - minValue);
        }

        public void Show()
        {
            _sliderGameObject.SetActive(true);
        }

        public void Hide()
        {
            _sliderGameObject.SetActive(false);
        }
    }
}
