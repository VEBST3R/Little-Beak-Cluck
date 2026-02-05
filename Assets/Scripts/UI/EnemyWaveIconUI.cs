using LittleBeakCluck.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    // Показує іконку типу хвилі, яка діє на ворога
    public class EnemyWaveIconUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Image _icon;
        [SerializeField] private EnemyWaveAffinity _affinity; // якщо не задано – візьмемо з батьків

        [Header("Sprites per Type")]
        [SerializeField] private Sprite _highSprite;
        [SerializeField] private Sprite _midSprite;
        [SerializeField] private Sprite _lowSprite;

        private void Awake()
        {
            if (_affinity == null)
                _affinity = GetComponentInParent<EnemyWaveAffinity>();
        }

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (_icon == null || _affinity == null) return;
            _icon.enabled = true;
            switch (_affinity.EffectiveType)
            {
                case VoiceWaveType.High: _icon.sprite = _highSprite; break;
                case VoiceWaveType.Mid: _icon.sprite = _midSprite; break;
                case VoiceWaveType.Low: _icon.sprite = _lowSprite; break;
            }
        }
    }
}
