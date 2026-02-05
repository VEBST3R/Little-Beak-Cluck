using System;
using LittleBeakCluck.Infrastructure;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

namespace LittleBeakCluck.Audio
{
    [DisallowMultipleComponent]
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class FootstepAudioPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip[] footstepClips = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private bool randomize = true;
        [SerializeField] private bool preventImmediateRepeat = true;
        [SerializeField] private bool useLocalSource = false;
        [SerializeField] private AudioSource audioSource;

        private int _lastIndex = -1;

        private void Awake()
        {
            if (useLocalSource && audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        [Preserve]
        public void PlayFootstep()
        {
            PlayFootstep(1f);
        }

        [Preserve]
        public void PlayFootstep(float intensity)
        {
            var clip = SelectClip();
            if (clip == null)
                return;

            float finalVolume = Mathf.Clamp01(volume * Mathf.Max(0f, intensity));

            if (useLocalSource && audioSource != null)
            {
                audioSource.PlayOneShot(clip, finalVolume);
                return;
            }

            var audio = ServiceLocator.Instance.Get<IAudioService>();
            audio?.PlaySfx(clip, finalVolume);
        }

        private AudioClip SelectClip()
        {
            if (footstepClips == null || footstepClips.Length == 0)
                return null;

            if (footstepClips.Length == 1)
                return footstepClips[0];

            int index;
            if (randomize)
            {
                do
                {
                    index = UnityEngine.Random.Range(0, footstepClips.Length);
                }
                while (preventImmediateRepeat && footstepClips.Length > 1 && index == _lastIndex);
            }
            else
            {
                index = (_lastIndex + 1) % footstepClips.Length;
            }

            _lastIndex = index;
            return footstepClips[index];
        }
    }
}
