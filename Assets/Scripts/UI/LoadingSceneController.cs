using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LittleBeakCluck.UI
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public class LoadingSceneController : MonoBehaviour
    {
        [Header("Scene Flow")]
        [SerializeField] private string targetSceneName = "MainMenu";
        [Tooltip("Seconds to wait before switching once the target scene is ready (0.9 progress).")]
        [Min(0f)][SerializeField] private float activateAfterSeconds = 2f;
        [SerializeField] private bool autoStart = true;
        [Tooltip("When entering Play Mode with multiple scenes open in the Editor, unload all except this loader scene before starting.")]
        [SerializeField] private bool forceUnloadOtherScenesOnStart = true;

        private Coroutine _routine;
        private static bool s_IsLoading;

        private void Awake()
        {
            if (forceUnloadOtherScenesOnStart)
            {
                var loader = gameObject.scene;
                var unloadOps = new List<AsyncOperation>();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if (s == loader)
                        continue;
                    if (s.IsValid() && s.isLoaded)
                    {
                        var u = SceneManager.UnloadSceneAsync(s);
                        if (u != null)
                            unloadOps.Add(u);
                    }
                }

                if (unloadOps.Count > 0)
                {
                    StartCoroutine(WaitUnloadsThenStart(unloadOps));
                    return;
                }
            }

            if (autoStart)
                StartLoading();
        }

        private IEnumerator WaitUnloadsThenStart(List<AsyncOperation> ops)
        {
            bool any;
            do
            {
                any = false;
                foreach (var op in ops)
                {
                    if (op != null && !op.isDone)
                    {
                        any = true;
                        break;
                    }
                }
                yield return null;
            }
            while (any);

            if (autoStart)
                StartLoading();
        }

        public void StartLoading()
        {
            if (_routine != null)
                return;

            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogError("[LoadingSceneController] Target scene name is empty.", this);
                return;
            }

            if (s_IsLoading)
            {
                Debug.Log("[LoadingSceneController] Loading already in progress. Ignoring duplicate start.", this);
                return;
            }

            _routine = StartCoroutine(LoadRoutine());
        }

        private IEnumerator LoadRoutine()
        {
            s_IsLoading = true;
            float start = Time.unscaledTime;

            // If the target scene is already active, nothing to do
            var active = SceneManager.GetActiveScene();
            if (string.Equals(active.name, targetSceneName, System.StringComparison.Ordinal))
            {
                s_IsLoading = false;
                _routine = null;
                yield break;
            }

            // Preload additively (so it stays inactive until we switch)
            var existing = SceneManager.GetSceneByName(targetSceneName);
            bool alreadyLoaded = existing.IsValid() && existing.isLoaded;
            AsyncOperation op = null;
            List<GameObject> hiddenRoots = null;
            if (!alreadyLoaded)
            {
                op = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
                if (op == null)
                {
                    Debug.LogError($"[LoadingSceneController] Failed to start loading scene '{targetSceneName}'.", this);
                    s_IsLoading = false;
                    _routine = null;
                    yield break;
                }
                op.allowSceneActivation = false;
            }
            else
            {
                // Scene is already loaded additively; hide its roots so it doesn't render over loader
                var roots = existing.GetRootGameObjects();
                if (roots != null && roots.Length > 0)
                {
                    hiddenRoots = new List<GameObject>(roots.Length);
                    foreach (var go in roots)
                    {
                        if (go != null && go.activeSelf)
                        {
                            go.SetActive(false);
                            hiddenRoots.Add(go);
                        }
                    }
                }
            }

            const float ready = 0.9f;
            if (!alreadyLoaded)
            {
                while (op.progress < ready)
                    yield return null;
            }

            // Ensure minimum wait since we entered the loading scene
            float waitUntil = start + activateAfterSeconds;
            while (Time.unscaledTime < waitUntil)
            {
                // If Unity exposed the preloaded scene roots early, hide them until activation
                if (hiddenRoots == null)
                {
                    var maybe = SceneManager.GetSceneByName(targetSceneName);
                    if (maybe.IsValid())
                    {
                        var roots = maybe.GetRootGameObjects();
                        if (roots != null && roots.Length > 0)
                        {
                            hiddenRoots = new List<GameObject>(roots.Length);
                            foreach (var go in roots)
                            {
                                if (go != null && go.activeSelf)
                                {
                                    go.SetActive(false);
                                    hiddenRoots.Add(go);
                                }
                            }
                        }
                    }
                }
                yield return null;
            }

            // Finish activation (if needed) and switch active scene
            if (!alreadyLoaded)
            {
                op.allowSceneActivation = true;
                while (!op.isDone)
                    yield return null;
                existing = SceneManager.GetSceneByName(targetSceneName);
            }

            if (hiddenRoots != null)
            {
                // Restore only those we hid
                foreach (var go in hiddenRoots)
                {
                    if (go != null)
                        go.SetActive(true);
                }
            }

            if (existing.IsValid() && existing.isLoaded)
            {
                SceneManager.SetActiveScene(existing);
            }

            // Unload the loader scene
            var loader = gameObject.scene;
            if (loader.IsValid())
            {
                SceneManager.UnloadSceneAsync(loader);
            }

            s_IsLoading = false;
            _routine = null;
        }
    }
}