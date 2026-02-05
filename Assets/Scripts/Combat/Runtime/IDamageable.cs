using System;

namespace LittleBeakCluck.Combat
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        float CurrentHealth { get; }
        float MaxHealth { get; }

        event Action<float, float> OnHealthChanged;
        event Action OnDied;

        void TakeDamage(DamageInfo info);
    }
}
