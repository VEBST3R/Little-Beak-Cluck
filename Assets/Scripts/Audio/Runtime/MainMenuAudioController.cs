using LittleBeakCluck.Infrastructure;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LittleBeakCluck.Audio
{
    [DisallowMultipleComponent]
    public class MainMenuAudioController : MonoBehaviour
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

        private void OnDisable()
        {
            var audio = ServiceLocator.Instance.Get<IAudioService>();
            if (audio == null)
                return;

            // If this scene isn't the active scene (e.g., preloaded additively and hidden),
            // don't change global audio context on disable.
            if (gameObject.scene != SceneManager.GetActiveScene())
                return;

            if (overrideMusic != null)
            {
                audio.ChangeMusicContext(SceneContextResolver.GetCurrentContextKey());
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
                audio.ChangeMusicContext(AudioContextKeys.Menu);
            }
        }
    }
}
