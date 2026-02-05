using UnityEngine;

namespace LittleBeakCluck.Combat
{
    // Компонент, який описує, якою хвилею можна вражати цього ворога
    public class EnemyWaveAffinity : MonoBehaviour
    {
        [SerializeField] private VoiceWaveType _effectiveType = VoiceWaveType.High;

        public VoiceWaveType EffectiveType => _effectiveType;

        // Зараз підтримуємо один тип; легко розширити до списку/бітмаски
        public bool IsEffective(VoiceWaveType type) => type == _effectiveType;

        // Дозволяє змінити тип на льоту (для спавну/скриптів)
        public void SetEffectiveType(VoiceWaveType type)
        {
            _effectiveType = type;
        }
    }
}
