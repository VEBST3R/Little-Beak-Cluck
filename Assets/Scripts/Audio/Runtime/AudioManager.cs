using System;
using System.Collections;
using LittleBeakCluck.Combat;
using LittleBeakCluck.Infrastructure;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace LittleBeakCluck.Audio
{
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour, IAudioService
    {
        private const string MusicPrefKey = "LBC_Audio_MusicEnabled";
        private const string SfxPrefKey = "LBC_Audio_SfxEnabled";

        [SerializeField] private GameAudioConfigAsset defaultConfig;
        [Header("Mixer Setup")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioMixerGroup musicMixerGroup;
        [SerializeField] private AudioMixerGroup sfxMixerGroup;
        [SerializeField] private string musicVolumeParameter = "MusicVolume";
        [SerializeField] private string sfxVolumeParameter = "SfxVolume";
        [SerializeField] private float enabledVolumeDb = 0f;
        [SerializeField] private float disabledVolumeDb = -80f;
        [Header("Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource voiceSource;
        [Range(0f, 1f)][SerializeField] private float musicVolume = 1f;
        [Range(0f, 1f)][SerializeField] private float sfxVolume = 1f;

        [SerializeField] private bool autoDetectContext = true;

        [Header("Voice")]
        [SerializeField, Min(0.1f)] private float lowWavePitch = 0.85f;
        [SerializeField, Min(0.1f)] private float midWavePitch = 1.0f;
        [SerializeField, Min(0.1f)] private float highWavePitch = 1.2f;
        [SerializeField, Range(0f, 1f)] private float waveVoiceVolume = 1f;

        private bool _initialized;
        private bool _musicEnabled = true;
        private bool _sfxEnabled = true;
        private GameAudioConfigAsset _config;
        private string _activeMusicKey;
        private string _currentContext;
        private Coroutine _voicePitchRestoreRoutine;
        private Coroutine _panelPriorityRoutine;
        private bool _panelSfxLockActive;
        private AudioClip _currentPriorityClip;
        private bool _musicPausedForPriority;

        public event Action<bool> MusicEnabledChanged;
        public event Action<bool> SfxEnabledChanged;

        public bool MusicEnabled => _musicEnabled;
        public bool SfxEnabled => _sfxEnabled;
        public GameAudioConfigAsset Config => _config != null ? _config : defaultConfig;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            EnsureSources();
            LoadState();
            RegisterSelf();
            _initialized = true;

            if (autoDetectContext)
            {
                // Defer initial context set until first activeSceneChanged to avoid
                // fighting with loader scene activation state during additive preload.
                SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            }
        }

        public void Configure(GameAudioConfigAsset configAsset)
        {
            _config = configAsset != null ? configAsset : _config;
            if (!_initialized)
                return;

            ApplyMusicState();
            ApplySfxState();

            if (autoDetectContext)
            {
                ChangeMusicContext(SceneContextResolver.GetCurrentContextKey());
            }
        }

        public void SetMusicEnabled(bool enabled, bool persist = true)
        {
            if (_musicEnabled == enabled)
                return;

            _musicEnabled = enabled;
            ApplyMusicState();
            if (persist)
            {
                PlayerPrefs.SetInt(MusicPrefKey, enabled ? 1 : 0);
                PlayerPrefs.Save();
            }

            MusicEnabledChanged?.Invoke(enabled);
        }

        public void SetSfxEnabled(bool enabled, bool persist = true)
        {
            if (_sfxEnabled == enabled)
                return;

            _sfxEnabled = enabled;
            ApplySfxState();
            if (persist)
            {
                PlayerPrefs.SetInt(SfxPrefKey, enabled ? 1 : 0);
                PlayerPrefs.Save();
            }

            SfxEnabledChanged?.Invoke(enabled);
        }

        public void PlayMusic(AudioClip clip, bool loop = true, float volume = 1f)
        {
            EnsureSources();

            if (musicSource == null)
                return;

            _activeMusicKey = clip != null ? clip.name : null;

            bool sameClip = musicSource.clip == clip;
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.volume = Mathf.Clamp01(volume) * musicVolume;

            if (!_musicEnabled || clip == null)
            {
                musicSource.Stop();
                return;
            }

            if (sameClip && musicSource.isPlaying)
                return;

            musicSource.Play();
        }

        public void StopMusic()
        {
            if (musicSource == null)
                return;

            musicSource.Stop();
            musicSource.clip = null;
            _activeMusicKey = null;
        }

        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (!_sfxEnabled || clip == null)
                return;

            if (_panelSfxLockActive && clip != _currentPriorityClip)
                return;

            EnsureSources();

            if (sfxSource == null)
                return;

            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume) * sfxVolume);
        }

        public void PlayUiClick()
        {
            var config = Config;
            if (config == null)
                return;

            var clip = config.ButtonClickSfx;
            if (clip == null)
                return;

            PlaySfx(clip);
        }

        public void PlayPurchase()
        {
            var config = Config;
            if (config == null)
                return;

            var clip = config.PurchaseSfx != null ? config.PurchaseSfx : config.ButtonClickSfx;
            if (clip == null)
                return;

            PlaySfx(clip);
        }

        public void PlayWinPanelOpened()
        {
            var config = Config;
            if (config == null)
                return;

            var clip = config.WinPanelOpenSfx;
            if (clip != null)
            {
                PlayPanelSoundExclusive(clip);
                return;
            }

            PlayUiClick();
        }

        public void PlayDefeatPanelOpened()
        {
            var config = Config;
            if (config == null)
                return;

            var clip = config.DefeatPanelOpenSfx;
            if (clip != null)
            {
                PlayPanelSoundExclusive(clip);
                return;
            }

            PlayUiClick();
        }

        public void PlayWaveVoice(VoiceWaveType type)
        {
            var config = Config;
            if (config == null)
                return;

            var clip = config.RoosterVoice;
            if (clip == null)
                return;

            float pitch = type switch
            {
                VoiceWaveType.Low => lowWavePitch,
                VoiceWaveType.High => highWavePitch,
                _ => midWavePitch
            };

            PlayWaveVoiceInternal(clip, pitch, waveVoiceVolume);
        }

        private void RegisterSelf()
        {
            var locator = ServiceLocator.Instance;
            var registered = locator.Get<IAudioService>();
            if (registered != null && !ReferenceEquals(registered, this))
                return;

            locator.Register<IAudioService>(this);
        }

        private void OnDestroy()
        {
            if (autoDetectContext)
            {
                SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            }

            if (_voicePitchRestoreRoutine != null)
            {
                StopCoroutine(_voicePitchRestoreRoutine);
                _voicePitchRestoreRoutine = null;
            }

            StopPriorityRoutine();
        }

        private void EnsureSources()
        {
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }

            if (voiceSource == null)
            {
                voiceSource = GetComponent<AudioSource>();
                if (voiceSource != null)
                {
                    voiceSource.playOnAwake = false;
                }
            }

            if (voiceSource == null)
            {
                voiceSource = gameObject.AddComponent<AudioSource>();
                voiceSource.playOnAwake = false;
            }

            ApplyMixerAssignments();
        }

        private void ApplyMixerAssignments()
        {
            if (musicSource != null && musicMixerGroup != null)
            {
                musicSource.outputAudioMixerGroup = musicMixerGroup;
            }

            if (sfxSource != null && sfxMixerGroup != null)
            {
                sfxSource.outputAudioMixerGroup = sfxMixerGroup;
            }

            if (voiceSource != null && sfxMixerGroup != null)
            {
                voiceSource.outputAudioMixerGroup = sfxMixerGroup;
            }
        }

        private void LoadState()
        {
            _musicEnabled = PlayerPrefs.GetInt(MusicPrefKey, 1) != 0;
            _sfxEnabled = PlayerPrefs.GetInt(SfxPrefKey, 1) != 0;
            ApplyMusicState();
            ApplySfxState();
        }

        private void ApplyMusicState()
        {
            if (musicSource == null)
                return;

            ApplyMixerVolume(musicVolumeParameter, _musicEnabled);
            musicSource.mute = false;
            if (_musicEnabled && musicSource.clip != null && !musicSource.isPlaying)
            {
                musicSource.Play();
            }
            else if (!_musicEnabled && musicSource.isPlaying)
            {
                musicSource.Pause();
            }
        }

        private void ApplySfxState()
        {
            if (sfxSource == null)
                return;

            ApplyMixerVolume(sfxVolumeParameter, _sfxEnabled);
            sfxSource.mute = false;
        }

        private void ApplyMixerVolume(string parameter, bool enabled)
        {
            if (audioMixer == null || string.IsNullOrEmpty(parameter))
                return;

            float volume = enabled ? enabledVolumeDb : disabledVolumeDb;
            audioMixer.SetFloat(parameter, volume);
        }

        public void ChangeMusicContext(string contextKey)
        {
            // If called while a preloaded scene is not active or scene is switching, avoid changing.
            var active = SceneManager.GetActiveScene();
            if (!active.IsValid() || string.IsNullOrEmpty(active.name))
                return;

            if (string.IsNullOrEmpty(contextKey))
                return;

            if (_currentContext == contextKey && musicSource != null && musicSource.isPlaying)
                return;

            _currentContext = contextKey;

            var config = Config;
            if (config == null)
                return;

            AudioClip clip = null;
            switch (contextKey)
            {
                case AudioContextKeys.Menu:
                    clip = config.MainMenuMusic;
                    break;
                case AudioContextKeys.Gameplay:
                    clip = config.GameplayMusic;
                    break;
            }

            if (clip == null)
            {
                if (!string.IsNullOrEmpty(contextKey) && config.TryGetMusic(contextKey, out var mappedClip))
                {
                    clip = mappedClip;
                }
            }

            if (clip == null)
                return;

            if (_activeMusicKey == clip.name && musicSource != null && musicSource.isPlaying)
                return;

            PlayMusic(clip, loop: true);
        }

        private void HandleActiveSceneChanged(Scene current, Scene next)
        {
            if (!autoDetectContext)
                return;

            ChangeMusicContext(SceneContextResolver.GetCurrentContextKey());
        }

        private void PlayPanelSoundExclusive(AudioClip clip)
        {
            if (!_sfxEnabled || clip == null)
                return;

            EnsureSources();

            if (sfxSource == null)
                return;

            StopPriorityRoutine();

            _panelPriorityRoutine = StartCoroutine(PlayPanelPriorityRoutine(clip));
        }

        private void StopPriorityRoutine()
        {
            if (_panelPriorityRoutine != null)
            {
                StopCoroutine(_panelPriorityRoutine);
                _panelPriorityRoutine = null;
            }

            if (_musicPausedForPriority && musicSource != null)
            {
                musicSource.UnPause();
            }

            _musicPausedForPriority = false;
            _panelSfxLockActive = false;
            _currentPriorityClip = null;
        }

        private IEnumerator PlayPanelPriorityRoutine(AudioClip clip)
        {
            _panelSfxLockActive = true;
            _currentPriorityClip = clip;

            bool pausedMusic = musicSource != null && musicSource.isPlaying;
            if (pausedMusic)
            {
                musicSource.Pause();
            }

            _musicPausedForPriority = pausedMusic;

            if (voiceSource != null && voiceSource != sfxSource)
            {
                voiceSource.Stop();
            }

            if (sfxSource != null)
            {
                sfxSource.Stop();
                float originalPitch = sfxSource.pitch;
                sfxSource.pitch = 1f;
                sfxSource.PlayOneShot(clip, Mathf.Clamp01(sfxVolume));

                yield return new WaitForSecondsRealtime(clip.length);

                sfxSource.pitch = originalPitch;
            }
            else
            {
                yield return new WaitForSecondsRealtime(clip.length);
            }

            _panelSfxLockActive = false;
            _currentPriorityClip = null;

            if (_musicPausedForPriority && musicSource != null)
            {
                musicSource.UnPause();
            }

            _musicPausedForPriority = false;
            _panelPriorityRoutine = null;
        }

        private IEnumerator RestoreVoicePitchAfterDelay(float targetPitch, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }
            else
            {
                yield return null;
            }

            if (voiceSource != null)
            {
                voiceSource.pitch = targetPitch;
            }

            _voicePitchRestoreRoutine = null;
        }

        private void PlayWaveVoiceInternal(AudioClip clip, float pitch, float volumeMultiplier)
        {
            if (!_sfxEnabled || clip == null)
                return;

            if (_panelSfxLockActive)
                return;

            EnsureSources();

            if (voiceSource == null)
                return;

            float clampedPitch = Mathf.Max(0.1f, pitch);
            float finalVolume = Mathf.Clamp01(volumeMultiplier) * sfxVolume;

            float originalPitch = voiceSource.pitch;
            voiceSource.pitch = clampedPitch;
            voiceSource.PlayOneShot(clip, finalVolume);

            if (_voicePitchRestoreRoutine != null)
            {
                StopCoroutine(_voicePitchRestoreRoutine);
                _voicePitchRestoreRoutine = null;
            }

            float delay = clip.length / Mathf.Max(0.1f, clampedPitch);
            _voicePitchRestoreRoutine = StartCoroutine(RestoreVoicePitchAfterDelay(originalPitch, delay));
        }
    }
}
