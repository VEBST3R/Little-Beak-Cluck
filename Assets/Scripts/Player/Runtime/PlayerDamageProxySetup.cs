using UnityEngine;

namespace LittleBeakCluck.Player
{
    /// <summary>
    /// Утиліта для автоматичного додавання DamageProxy до всіх дочірніх колайдерів
    /// </summary>
    public class PlayerDamageProxySetup : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private bool setupOnAwake = true;
        [SerializeField] private bool includeInactive = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private void Awake()
        {
            if (setupOnAwake)
            {
                SetupDamageProxies();
            }
        }

        [ContextMenu("Setup Damage Proxies")]
        public void SetupDamageProxies()
        {
            PlayerHealth playerHealth = GetComponent<PlayerHealth>();

            if (playerHealth == null)
            {
                Debug.LogError($"[{name}] PlayerDamageProxySetup: PlayerHealth not found on this GameObject!", this);
                return;
            }

            // Отримуємо всі колайдери в дочірніх об'єктах (але не на цьому)
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(includeInactive);
            int setupCount = 0;

            foreach (var collider in colliders)
            {
                // Пропускаємо колайдери на батьківському об'єкті
                if (collider.gameObject == gameObject)
                    continue;

                // Перевіряємо чи вже є DamageProxy
                DamageProxy existingProxy = collider.GetComponent<DamageProxy>();
                if (existingProxy != null)
                {
                    if (showDebugLogs)
                        Debug.Log($"[{name}] DamageProxy already exists on {collider.gameObject.name}", this);
                    continue;
                }

                // Перевіряємо чи вже є PlayerHealth (не потрібен проксі)
                PlayerHealth existingHealth = collider.GetComponent<PlayerHealth>();
                if (existingHealth != null)
                {
                    if (showDebugLogs)
                        Debug.LogWarning($"[{name}] PlayerHealth found on child {collider.gameObject.name} - consider removing it!", this);
                    continue;
                }

                // Додаємо DamageProxy
                DamageProxy proxy = collider.gameObject.AddComponent<DamageProxy>();
                setupCount++;

                if (showDebugLogs)
                    Debug.Log($"[{name}] ✅ Added DamageProxy to {collider.gameObject.name}", this);
            }

            if (showDebugLogs)
            {
                Debug.Log($"[{name}] Setup complete! Added {setupCount} DamageProxy component(s)", this);
            }
        }

        [ContextMenu("Remove All Damage Proxies")]
        public void RemoveAllDamageProxies()
        {
            DamageProxy[] proxies = GetComponentsInChildren<DamageProxy>(true);
            int count = proxies.Length;

            foreach (var proxy in proxies)
            {
                if (Application.isPlaying)
                    Destroy(proxy);
                else
                    DestroyImmediate(proxy);
            }

            if (showDebugLogs)
            {
                Debug.Log($"[{name}] Removed {count} DamageProxy component(s)", this);
            }
        }
    }
}
