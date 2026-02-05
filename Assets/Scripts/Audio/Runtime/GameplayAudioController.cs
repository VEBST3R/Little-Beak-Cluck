using LittleBeakCluck.Infrastructure;
using UnityEngine;

namespace LittleBeakCluck.Audio
{
    [DisallowMultipleComponent]
    public class GameplayAudioController : MonoBehaviour
    {
        [SerializeField] private AudioClip overrideMusic;
        [SerializeField] private bool loop = true;

        private void Start()
        {
            ApplyContext();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                ApplyContext();
            }
        }

        private void ApplyContext()
        {
            var audio = ServiceLocator.Instance.Get<IAudioService>();
            if (audio == null)
                return;

            if (overrideMusic != null)
            {
                audio.PlayMusic(overrideMusic, loop);
            }
            else
            {
                audio.ChangeMusicContext(AudioContextKeys.Gameplay);
            }
        }
    }
}
