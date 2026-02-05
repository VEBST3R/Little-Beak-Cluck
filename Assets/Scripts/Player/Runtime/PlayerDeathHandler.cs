using System.Collections;
using LittleBeakCluck.Infrastructure;
using LittleBeakCluck.UI;
using UnityEngine;

namespace LittleBeakCluck.Player
{
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerDeathHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Animator animator;
        [SerializeField] private Rigidbody2D playerRigidbody;
        [SerializeField] private Collider2D[] collidersToDisable;
        [SerializeField] private GameObject defeatMenuObject;
        [SerializeField] private bool autoFindDefeatMenu = true;
        [SerializeField] private bool preferUiManager = true;

        [Header("Death Physics")]
        [SerializeField] private float deathGravityScale = 1f;
        [SerializeField] private float deathPushForce = 5f;
        [SerializeField] private float deathTorque = 5f;
        [SerializeField] private Vector2 pushDirection = new Vector2(1f, 0.6f);

        [Header("Time Slowdown")]
        [SerializeField] private float slowDuration = 1f;
        [SerializeField] private float minimumFixedDelta = 0.0005f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        private PlayerHealth _playerHealth;
        private float _originalFixedDelta;
        private bool _isDead;
        private RigidbodyType2D _initialBodyType;
        private RigidbodyConstraints2D _initialConstraints;
        private float _initialGravityScale;
        private DefeatMenuController _defeatMenuController;
        private IUIManager _uiManager;

        private void Awake()
        {
            _originalFixedDelta = Time.fixedDeltaTime;
            _playerHealth = GetComponent<PlayerHealth>();

            if (_playerHealth == null)
            {
                Debug.LogError($"[{name}] PlayerHealth not found!", this);
                enabled = false;
                return;
            }

            if (playerRigidbody != null)
            {
                _initialBodyType = playerRigidbody.bodyType;
                _initialConstraints = playerRigidbody.constraints;
                _initialGravityScale = playerRigidbody.gravityScale;
            }

            if (!TryResolveUiManager())
            {
                EnsureDefeatMenuReference(true);
            }

            if (enableDebugLogs)
            {
                string menuStatus = defeatMenuObject != null ? defeatMenuObject.name : "<not assigned>";
                Debug.Log($"[{name}] Initialized. MaxHP={_playerHealth.MaxHealth}, DefeatMenu={menuStatus}", this);
            }
        }

        private void OnEnable()
        {
            if (_playerHealth != null)
            {
                if (!TryResolveUiManager())
                {
                    EnsureDefeatMenuReference(false);
                }

                _playerHealth.OnDied += OnPlayerDied;
                if (enableDebugLogs)
                    Debug.Log($"[{name}] ✅ Subscribed to OnDied event. HP={_playerHealth.CurrentHealth}, IsAlive={_playerHealth.IsAlive}", this);
            }
            else
            {
                Debug.LogError($"[{name}] ❌ PlayerHealth is NULL in OnEnable! Cannot subscribe to OnDied!", this);
            }
        }

        private void OnDisable()
        {
            if (_playerHealth != null)
                _playerHealth.OnDied -= OnPlayerDied;
            RestoreTimeScale();
        }

        private void OnPlayerDied()
        {
            if (_isDead)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[{name}] Already dead!", this);
                return;
            }

            if (enableDebugLogs)
                Debug.Log($"[{name}] PLAYER DIED! Starting death sequence...", this);

            _isDead = true;
            DisablePlayerControl();
            ApplyDeathPhysics();
            if (preferUiManager && TryResolveUiManager())
            {
                _uiManager.HideDefeatMenu();
            }
            else
            {
                EnsureDefeatMenuReference(false);
                bool menuHidden = HideDefeatMenu();
                if (!menuHidden && enableDebugLogs)
                {
                    Debug.LogWarning($"[{name}] DefeatMenu not assigned and could not be found in scene.", this);
                }
            }
            StartCoroutine(DeathSequence());
        }

        private void DisablePlayerControl()
        {
            if (playerController != null) playerController.enabled = false;
            if (animator != null) animator.enabled = false;

            if (collidersToDisable != null)
            {
                foreach (var col in collidersToDisable)
                {
                    if (col != null) col.enabled = false;
                }
            }
        }

        private void ApplyDeathPhysics()
        {
            if (playerRigidbody == null) return;

            playerRigidbody.bodyType = RigidbodyType2D.Dynamic;
            playerRigidbody.constraints = RigidbodyConstraints2D.None;
            playerRigidbody.gravityScale = deathGravityScale;
            playerRigidbody.linearVelocity = Vector2.zero;

            bool facingRight = transform.eulerAngles.y < 90f || transform.eulerAngles.y > 270f;
            Vector2 direction = new Vector2(
                facingRight ? Mathf.Abs(pushDirection.x) : -Mathf.Abs(pushDirection.x),
                pushDirection.y
            );

            if (direction.sqrMagnitude < 0.0001f)
                direction = facingRight ? Vector2.right : Vector2.left;
            else
                direction = direction.normalized;

            playerRigidbody.AddForce(direction * deathPushForce, ForceMode2D.Impulse);
            playerRigidbody.AddTorque((facingRight ? -1f : 1f) * deathTorque, ForceMode2D.Impulse);
        }

        private IEnumerator DeathSequence()
        {
            float elapsed = 0f;
            float startTimeScale = Time.timeScale;

            while (elapsed < slowDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, slowDuration));
                float newTimeScale = Mathf.Lerp(startTimeScale, 0f, t);
                Time.timeScale = newTimeScale;
                Time.fixedDeltaTime = Mathf.Max(minimumFixedDelta, _originalFixedDelta * newTimeScale);
                yield return null;
            }

            Time.timeScale = 0f;
            Time.fixedDeltaTime = Mathf.Max(minimumFixedDelta, _originalFixedDelta * 0.01f);

            bool shown = false;

            if (preferUiManager && TryResolveUiManager())
            {
                _uiManager.ShowDefeatMenu();
                shown = true;
            }
            else
            {
                EnsureDefeatMenuReference(false);
                shown = ShowDefeatMenu();
            }

            if (!shown && enableDebugLogs)
                Debug.LogWarning($"[{name}] DefeatMenu not assigned!", this);
        }

        private void RestoreTimeScale()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _originalFixedDelta;
        }

        public void Resurrect()
        {
            if (!_isDead) return;

            _isDead = false;
            RestoreTimeScale();

            if (playerController != null) playerController.enabled = true;
            if (animator != null) animator.enabled = true;

            if (collidersToDisable != null)
            {
                foreach (var col in collidersToDisable)
                {
                    if (col != null) col.enabled = true;
                }
            }

            if (playerRigidbody != null)
            {
                playerRigidbody.bodyType = _initialBodyType;
                playerRigidbody.constraints = _initialConstraints;
                playerRigidbody.gravityScale = _initialGravityScale;
                playerRigidbody.linearVelocity = Vector2.zero;
                playerRigidbody.angularVelocity = 0f;
            }

            if (preferUiManager && TryResolveUiManager())
            {
                _uiManager.HideDefeatMenu();
            }
            else
            {
                HideDefeatMenu();
            }

            if (enableDebugLogs)
                Debug.Log($"[{name}] Resurrected!", this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (!TryResolveUiManager())
                {
                    EnsureDefeatMenuReference(false);
                }
            }
        }
#endif

        private void EnsureDefeatMenuReference(bool logWarnings)
        {
            if (defeatMenuObject != null)
            {
                CacheDefeatMenuController(defeatMenuObject);
                return;
            }

            if (!autoFindDefeatMenu)
                return;

            DefeatMenuController found = null;
#if UNITY_2022_2_OR_NEWER
            found = Object.FindFirstObjectByType<DefeatMenuController>(FindObjectsInactive.Include);
#else
            foreach (var candidate in Resources.FindObjectsOfTypeAll<DefeatMenuController>())
            {
                if (candidate == null)
                    continue;

                if (!candidate.gameObject.scene.IsValid() || !candidate.gameObject.scene.isLoaded)
                    continue;

                found = candidate;
                break;
            }
#endif

            if (found != null && found.gameObject.scene.IsValid())
            {
                defeatMenuObject = found.MenuRoot;
                _defeatMenuController = found;
                return;
            }

            if (logWarnings && enableDebugLogs)
            {
                Debug.LogWarning($"[{name}] Auto-find failed: defeat menu not located.", this);
            }
        }

        private bool ShowDefeatMenu()
        {
            if (defeatMenuObject == null)
                return false;

            if (!defeatMenuObject.activeSelf)
            {
                defeatMenuObject.SetActive(true);
            }

            if (_defeatMenuController == null)
            {
                CacheDefeatMenuController(defeatMenuObject);
            }

            return true;
        }

        private bool HideDefeatMenu()
        {
            if (defeatMenuObject == null)
                return false;

            if (_defeatMenuController == null)
            {
                CacheDefeatMenuController(defeatMenuObject);
            }

            if (_defeatMenuController != null)
            {
                _defeatMenuController.Hide();
            }
            else if (defeatMenuObject.activeSelf)
            {
                defeatMenuObject.SetActive(false);
            }

            return true;
        }

        private void CacheDefeatMenuController(GameObject menu)
        {
            if (menu == null)
            {
                _defeatMenuController = null;
                return;
            }

            if (_defeatMenuController != null && _defeatMenuController.gameObject == menu)
                return;

            _defeatMenuController = menu.GetComponent<DefeatMenuController>();
        }

        private bool TryResolveUiManager()
        {
            if (!preferUiManager)
                return false;

            if (_uiManager != null)
                return true;

            var locator = ServiceLocator.Instance;
            var service = locator.Get<IUIManager>();
            if (service != null)
            {
                _uiManager = service;
                return true;
            }

#if UNITY_2022_2_OR_NEWER
            var component = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
#else
            UIManager component = null;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<UIManager>())
            {
                if (candidate == null)
                    continue;

                if (!candidate.gameObject.scene.IsValid() || !candidate.gameObject.scene.isLoaded)
                    continue;

                component = candidate;
                break;
            }
#endif

            if (component != null)
            {
                _uiManager = component;
                return true;
            }

            return false;
        }
    }
}
