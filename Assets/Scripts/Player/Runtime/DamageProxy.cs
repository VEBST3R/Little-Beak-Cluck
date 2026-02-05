using System;
using LittleBeakCluck.Combat;
using UnityEngine;

namespace LittleBeakCluck.Player
{
    /// <summary>
    /// Перенаправляє урон від дочірніх колайдерів до батьківського PlayerHealth
    /// </summary>
    public class DamageProxy : MonoBehaviour, IDamageable
    {
        [SerializeField] private PlayerHealth targetHealth;
        [SerializeField] private bool autoFindInParent = true;
        [SerializeField] private bool enableDebugLogs = false;

        // Реалізація інтерфейсу IDamageable - перенаправляємо все на targetHealth
        public bool IsAlive => targetHealth != null && targetHealth.IsAlive;
        public float CurrentHealth => targetHealth != null ? targetHealth.CurrentHealth : 0f;
        public float MaxHealth => targetHealth != null ? targetHealth.MaxHealth : 0f;

        // Події теж перенаправляємо
        public event Action<float, float> OnHealthChanged
        {
            add { if (targetHealth != null) targetHealth.OnHealthChanged += value; }
            remove { if (targetHealth != null) targetHealth.OnHealthChanged -= value; }
        }

        public event Action OnDied
        {
            add { if (targetHealth != null) targetHealth.OnDied += value; }
            remove { if (targetHealth != null) targetHealth.OnDied -= value; }
        }

        private void Awake()
        {
            if (targetHealth == null && autoFindInParent)
            {
                targetHealth = GetComponentInParent<PlayerHealth>();

                if (targetHealth == null)
                {
                    Debug.LogError($"[{name}] DamageProxy: PlayerHealth not found in parent objects!", this);
                }
            }
        }

        public void TakeDamage(DamageInfo info)
        {
            if (targetHealth != null)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[{name}] DamageProxy forwarding {info.Amount} damage to {targetHealth.gameObject.name}", this);
                }
                targetHealth.TakeDamage(info);
            }
            else
            {
                Debug.LogError($"[{name}] DamageProxy: Cannot forward damage - targetHealth is null!", this);
            }
        }
    }
}