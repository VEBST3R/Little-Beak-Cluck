using LittleBeakCluck.Audio;
using LittleBeakCluck.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    [RequireComponent(typeof(Button))]
    public class UIButtonClickSound : MonoBehaviour
    {
        [SerializeField] private AudioClip overrideClip;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClicked);
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClicked);
            }
        }

        private void OnClicked()
        {
            var audio = ServiceLocator.Instance.Get<IAudioService>();
            if (audio == null)
                return;

            if (overrideClip != null)
            {
                audio.PlaySfx(overrideClip);
            }
            else
            {
                audio.PlayUiClick();
            }
        }
    }
}
