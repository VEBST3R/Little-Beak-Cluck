using LittleBeakCluck.Audio;
using LittleBeakCluck.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    [DisallowMultipleComponent]
    public class AudioSettingsPanel : MonoBehaviour
    {
        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Toggle sfxToggle;

        private IAudioService _audioService;
        private bool _synchronizing;

        private void OnEnable()
        {
            ResolveService();
            // Спершу синхронізуємо стан з сервісу, потім реєструємо слухачі,
            // щоб уникнути зайвих onValueChanged під час початкового виставлення значень
            SyncFromService();
            RegisterListeners(true);
        }

        private void OnDisable()
        {
            RegisterListeners(false);
            if (_audioService != null)
            {
                _audioService.MusicEnabledChanged -= OnMusicStateChanged;
                _audioService.SfxEnabledChanged -= OnSfxStateChanged;
            }
            _audioService = null;
        }

        private void ResolveService()
        {
            _audioService = ServiceLocator.Instance.Get<IAudioService>();
            if (_audioService != null)
            {
                _audioService.MusicEnabledChanged += OnMusicStateChanged;
                _audioService.SfxEnabledChanged += OnSfxStateChanged;
            }
        }

        private void RegisterListeners(bool register)
        {
            if (musicToggle != null)
            {
                if (register)
                {
                    musicToggle.onValueChanged.AddListener(OnMusicToggleChanged);
                }
                else
                {
                    musicToggle.onValueChanged.RemoveListener(OnMusicToggleChanged);
                }
            }

            if (sfxToggle != null)
            {
                if (register)
                {
                    sfxToggle.onValueChanged.AddListener(OnSfxToggleChanged);
                }
                else
                {
                    sfxToggle.onValueChanged.RemoveListener(OnSfxToggleChanged);
                }
            }
        }

        private void SyncFromService()
        {
            if (_audioService == null)
                return;

            if (musicToggle != null)
            {
                // Програмно оновлюємо без виклику onValueChanged
                musicToggle.SetIsOnWithoutNotify(_audioService.MusicEnabled);
            }

            if (sfxToggle != null)
            {
                sfxToggle.SetIsOnWithoutNotify(_audioService.SfxEnabled);
            }
        }

        private void OnMusicToggleChanged(bool value)
        {
            if (_synchronizing || _audioService == null)
                return;

            _audioService.PlayUiClick();
            _audioService.SetMusicEnabled(value);
        }

        private void OnSfxToggleChanged(bool value)
        {
            if (_synchronizing || _audioService == null)
                return;

            _audioService.PlayUiClick();
            _audioService.SetSfxEnabled(value);
        }

        private void OnMusicStateChanged(bool value)
        {
            if (musicToggle == null)
                return;
            // Сервіс змінив стан — відобразимо без зворотнього виклику
            musicToggle.SetIsOnWithoutNotify(value);
        }

        private void OnSfxStateChanged(bool value)
        {
            if (sfxToggle == null)
                return;
            sfxToggle.SetIsOnWithoutNotify(value);
        }
    }
}
