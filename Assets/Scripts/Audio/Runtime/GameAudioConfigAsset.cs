using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleBeakCluck.Audio
{
    [CreateAssetMenu(fileName = "GameAudioConfig", menuName = "LittleBeakCluck/Audio/Game Audio Config")]
    public class GameAudioConfigAsset : ScriptableObject
    {
        [Serializable]
        public struct MusicEntry
        {
            public string key;
            public AudioClip clip;
        }

        [Serializable]
        public struct SfxEntry
        {
            public string key;
            public AudioClip clip;
        }

        [Header("General Music")]
        [SerializeField] private AudioClip mainMenuMusic;
        [SerializeField] private AudioClip gameplayMusic;
        [SerializeField] private List<MusicEntry> musicEntries = new();

        [Header("General SFX")]
        [SerializeField] private AudioClip buttonClick;
        [SerializeField] private AudioClip purchase;
        [SerializeField] private List<SfxEntry> sfxEntries = new();

        [Header("Panel SFX")]
        [SerializeField] private AudioClip winPanelOpen;
        [SerializeField] private AudioClip defeatPanelOpen;

        [Header("Voice Lines")]
        [SerializeField] private AudioClip roosterVoice;

        public AudioClip MainMenuMusic => mainMenuMusic;
        public AudioClip GameplayMusic => gameplayMusic;
        public AudioClip ButtonClickSfx => buttonClick;
        public AudioClip PurchaseSfx => purchase;
        public AudioClip WinPanelOpenSfx => winPanelOpen;
        public AudioClip DefeatPanelOpenSfx => defeatPanelOpen;
        public AudioClip RoosterVoice => roosterVoice;
        public IReadOnlyList<MusicEntry> MusicEntries => musicEntries;
        public IReadOnlyList<SfxEntry> SfxEntries => sfxEntries;

        public bool TryGetMusic(string key, out AudioClip clip)
        {
            clip = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            for (int i = 0; i < musicEntries.Count; i++)
            {
                if (musicEntries[i].clip != null && string.Equals(musicEntries[i].key, key, StringComparison.OrdinalIgnoreCase))
                {
                    clip = musicEntries[i].clip;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetSfx(string key, out AudioClip clip)
        {
            clip = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            for (int i = 0; i < sfxEntries.Count; i++)
            {
                if (sfxEntries[i].clip != null && string.Equals(sfxEntries[i].key, key, StringComparison.OrdinalIgnoreCase))
                {
                    clip = sfxEntries[i].clip;
                    return true;
                }
            }

            return false;
        }
    }
}
