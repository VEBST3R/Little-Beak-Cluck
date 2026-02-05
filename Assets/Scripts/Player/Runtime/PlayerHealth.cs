using System;
using LittleBeakCluck.Combat;
using UnityEngine;

namespace LittleBeakCluck.Player
{
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [field: SerializeField] public float MaxHealth { get; private set; } = 100f;
        public float CurrentHealth { get; private set; }
        [SerializeField] private bool enableDebugLogs = false;

        public bool IsAlive => CurrentHealth > 0;

        public event Action<float, float> OnHealthChanged; // current, max
        public event Action OnDied;

        private void Awake()
        {
            CurrentHealth = MaxHealth;
            RaiseHealthChanged();

            var worldBar = GetComponentInChildren<LittleBeakCluck.UI.WorldSpaceHealthBar>(includeInactive: true);
            if (worldBar != null)
            {
                worldBar.SetTarget(this);
            }
        }

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive)
            {
                LogWarning("TakeDamage called while player is dead.");
                return;
            }

            if (info.Amount <= 0)
            {
                LogWarning($"Ignored non-positive damage amount ({info.Amount}).");
                return;
            }

            float oldHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(0, CurrentHealth - info.Amount);

            // Spawn floating damage number for player in the global overlay canvas (unified with enemy damage numbers)
            var dmgMgr = LittleBeakCluck.UI.DamageNumbers.DamageNumbersManager.Instance;
            var color = dmgMgr.GetPlayerDamageColor();
            Vector3 hitWorld = info.TargetRigidbody != null ? (Vector3)info.HitPoint : transform.position;
            dmgMgr.SpawnNumber(info.Amount, hitWorld, color);

            RaiseHealthChanged();

            if (!IsAlive)
            {
                Die();
            }
            else if (enableDebugLogs)
            {
                Debug.Log($"[{name}] Took {info.Amount:F1} damage. HP: {oldHealth:F1} -> {CurrentHealth:F1}", this);
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0) return;
            float oldHealth = CurrentHealth;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            if (CurrentHealth > oldHealth)
            {
                RaiseHealthChanged();
            }
        }

        public void SetMaxHealth(float newMax, bool refill = true)
        {
            if (newMax <= 0) return;
            MaxHealth = newMax;
            if (refill)
            {
                CurrentHealth = MaxHealth;
            }
            else
            {
                CurrentHealth = Mathf.Min(CurrentHealth, MaxHealth);
            }
            RaiseHealthChanged();
        }

        private void Die()
        {
            if (OnDied != null)
            {
                OnDied.Invoke();
            }
            else if (enableDebugLogs)
            {
                Debug.LogWarning($"[{name}] OnDied event has no subscribers.", this);
            }
        }

        private void RaiseHealthChanged() => OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

        private void LogWarning(string message)
        {
            if (!enableDebugLogs)
                return;

            Debug.LogWarning($"[{name}] {message}", this);
        }
    }
}
