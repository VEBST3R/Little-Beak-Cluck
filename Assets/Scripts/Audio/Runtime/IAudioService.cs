using System;
using LittleBeakCluck.Combat;
using LittleBeakCluck.Infrastructure;
using UnityEngine;

namespace LittleBeakCluck.Audio
{
    public interface IAudioService : IGameService
    {
        event Action<bool> MusicEnabledChanged;
        event Action<bool> SfxEnabledChanged;

        bool MusicEnabled { get; }
        bool SfxEnabled { get; }
        GameAudioConfigAsset Config { get; }

        void Configure(GameAudioConfigAsset configAsset);
        void SetMusicEnabled(bool enabled, bool persist = true);
        void SetSfxEnabled(bool enabled, bool persist = true);
        void PlayMusic(AudioClip clip, bool loop = true, float volume = 1f);
        void StopMusic();
        void PlaySfx(AudioClip clip, float volume = 1f);
        void PlayUiClick();
        void PlayPurchase();
        void ChangeMusicContext(string contextKey);
        void PlayWaveVoice(VoiceWaveType type);
        void PlayWinPanelOpened();
        void PlayDefeatPanelOpened();
    }
}
