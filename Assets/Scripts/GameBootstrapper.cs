using LittleBeakCluck.Audio;
using LittleBeakCluck.Infrastructure;
using LittleBeakCluck.Player;
using LittleBeakCluck.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LittleBeakCluck
{
    [DefaultExecutionOrder(-9000)]
    public class GameBootstrapper : MonoBehaviour
    {
        [SerializeField] private PlayerUpgradeConfigAsset playerUpgradeConfig;
        [SerializeField] private GameAudioConfigAsset gameAudioConfig;
        [Header("Bootstrap Rules")]
        [Tooltip("If enabled, the AudioManager will be created/registered ONLY when the active scene name matches 'audioBootstrapSceneName'.")]
        [SerializeField] private bool initializeAudioOnlyInScene = true;
        [SerializeField] private string audioBootstrapSceneName = "MainMenu";
        [Header("Performance")]
        [Tooltip("Desired target frame rate for the application. Set -1 to use platform default.")]
        [SerializeField] private int targetFrameRate = 200;
        [Tooltip("Force vSync off so targetFrameRate is respected.")]
        [SerializeField] private bool disableVSync = true;
        [Header("Platform Fixes")]
        [Tooltip("Автоматично застосовувати фікси для ПК/WebGL (UI масштаб та рендеринг спрайтів)")]
        [SerializeField] private bool enablePlatformFixes = true;

        private void Awake()
        {
            // Apply performance settings early
            if (disableVSync)
            {
                QualitySettings.vSyncCount = 0;
            }
            if (targetFrameRate != 0)
            {
                Application.targetFrameRate = targetFrameRate;
            }

            // Apply platform-specific fixes
            if (enablePlatformFixes)
            {
                ApplyPlatformFixes();
            }

            var serviceLocator = ServiceLocator.Instance;

            if (serviceLocator.Get<IInputService>() == null)
            {
                serviceLocator.Register<IInputService>(new InputService());
            }

            if (serviceLocator.Get<IGameFactory>() == null)
            {
                serviceLocator.Register<IGameFactory>(new GameFactory());
            }

            if (serviceLocator.Get<IPlayerProgressService>() == null)
            {
                var progressService = new PlayerProgressService(playerUpgradeConfig);
                serviceLocator.Register<IPlayerProgressService>(progressService);
            }

            // Bootstrap audio now or wait for the right scene
            TryBootstrapAudio(serviceLocator, forceNow: !initializeAudioOnlyInScene || SceneManager.GetActiveScene().name == audioBootstrapSceneName);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged(Scene prev, Scene next)
        {
            if (!initializeAudioOnlyInScene)
                return;

            if (next.IsValid() && next.name == audioBootstrapSceneName)
            {
                TryBootstrapAudio(ServiceLocator.Instance, forceNow: true);
            }
        }

        private void TryBootstrapAudio(ServiceLocator serviceLocator, bool forceNow)
        {
            if (!forceNow)
                return;

            var audioService = serviceLocator.Get<IAudioService>();
            if (audioService == null)
            {
                AudioManager existingManager = null;
#if UNITY_2022_2_OR_NEWER
                existingManager = Object.FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
#else
                foreach (var candidate in Resources.FindObjectsOfTypeAll<AudioManager>())
                {
                    if (candidate == null)
                        continue;

                    if (!candidate.gameObject.scene.IsValid() || !candidate.gameObject.scene.isLoaded)
                        continue;

                    existingManager = candidate;
                    break;
                }
#endif

                if (existingManager == null)
                {
                    var audioGo = new GameObject("AudioManager");
                    existingManager = audioGo.AddComponent<AudioManager>();
                }

                existingManager.Configure(gameAudioConfig);
                serviceLocator.Register<IAudioService>(existingManager);
            }
            else if (gameAudioConfig != null)
            {
                audioService.Configure(gameAudioConfig);
            }
        }

        private void ApplyPlatformFixes()
        {
            // Додаємо PlatformRenderingFix якщо його ще немає
            if (FindFirstObjectByType<PlatformRenderingFix>() == null)
            {
                var fixGo = new GameObject("PlatformRenderingFix");
                fixGo.AddComponent<PlatformRenderingFix>();
                DontDestroyOnLoad(fixGo);
                Debug.Log("[GameBootstrapper] Додано PlatformRenderingFix");
            }

            // Canvas фікси додаються автоматично через компонент PlatformCanvasFix на кожному Canvas
            // Якщо потрібно, можна автоматично додавати його до всіх Canvas
            bool isMobile = IsMobilePlatform();
            Debug.Log($"[GameBootstrapper] Platform: {(isMobile ? "Mobile" : "Desktop/WebGL")} - фікси активовані");
        }

        private bool IsMobilePlatform()
        {
#if UNITY_EDITOR
            // В Editor перевіряємо Build Target
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            return buildTarget == BuildTarget.Android || buildTarget == BuildTarget.iOS;
#else
            return Application.isMobilePlatform;
#endif
        }
    }
}
