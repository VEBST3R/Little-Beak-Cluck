using System;
using UnityEngine;
using UnityEngine.UI;
using LittleBeakCluck.Combat;
using LittleBeakCluck.Player;
using LittleBeakCluck.Audio;
using LittleBeakCluck.Infrastructure;

namespace LittleBeakCluck.UI
{
    /// <summary>
    /// Керування вибором типу хвилі БЕЗ інспекторних UnityEvent прив'язок.
    /// Не викликає PlayerAttack через UnityEvent, а робить чистий виклик із коду.
    /// Можна просто призначити тумблери в масиві і все.
    /// </summary>
    public class WaveTypeToggleGroup : MonoBehaviour
    {
        [Serializable]
        private struct WaveToggle
        {
            public Toggle toggle;
            public VoiceWaveType type;
            public Image optionalHighlight; // необов'язково – підсвічування активного
        }

        [Header("References")]
        [SerializeField] private PlayerAttack playerAttack;
        [SerializeField] private WaveToggle[] toggles;
        [SerializeField] private bool enforceSingleOn = true; // гарантуємо одну активну
        [SerializeField] private bool autoSelectFirstIfNone = true;
        [SerializeField] private bool updateHighlightAlpha = true;
        [Range(0f, 1f)][SerializeField] private float inactiveHighlightAlpha = 0.35f;

        private VoiceWaveType _current;
        private bool _initialized;

        private void Awake()
        {
            // Не підписуємо через інспектор UnityEvent, а робимо тут
            for (int i = 0; i < toggles.Length; i++)
            {
                int idx = i; // capture
                if (toggles[i].toggle != null)
                    toggles[i].toggle.onValueChanged.AddListener(v => OnToggleChanged(idx, v));
            }
        }

        private void Start()
        {
            InitializeSelection();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < toggles.Length; i++)
            {
                if (toggles[i].toggle != null)
                    toggles[i].toggle.onValueChanged.RemoveAllListeners();
            }
        }

        private void InitializeSelection()
        {
            if (!_initialized)
            {
                bool anyOn = false;
                for (int i = 0; i < toggles.Length; i++)
                {
                    if (toggles[i].toggle != null && toggles[i].toggle.isOn)
                    {
                        SetActiveInternal(toggles[i].type);
                        anyOn = true;
                        break;
                    }
                }
                if (!anyOn && autoSelectFirstIfNone)
                {
                    for (int i = 0; i < toggles.Length; i++)
                    {
                        if (toggles[i].toggle != null)
                        {
                            toggles[i].toggle.isOn = true; // тригерне onValueChanged
                            break;
                        }
                    }
                }
                _initialized = true;
            }
            UpdateHighlights();
        }

        private void OnToggleChanged(int index, bool state)
        {
            if (!_initialized) return; // дочекаємося повної ініціалізації
            if (!state) // нас цікавить тільки вмикання
            {
                UpdateHighlights();
                return;
            }

            var selected = toggles[index].type;
            if (selected == _current)
            {
                UpdateHighlights();
                return;
            }

            SetActiveInternal(selected);
            PlayToggleSound();

            if (enforceSingleOn)
            {
                // Вимикаємо всі інші, не використовуючи інспекторні евенти зовні
                for (int i = 0; i < toggles.Length; i++)
                {
                    if (i == index) continue;
                    var t = toggles[i].toggle;
                    if (t != null && t.isOn)
                        t.isOn = false; // це теж викличе їх onValueChanged, але вони нічого не зроблять бо state=false
                }
            }
            UpdateHighlights();
        }

        private void SetActiveInternal(VoiceWaveType type)
        {
            _current = type;
            if (playerAttack != null)
                playerAttack.SetWaveType(type);
        }

        private static void PlayToggleSound()
        {
            var audio = ServiceLocator.Instance.Get<IAudioService>();
            audio?.PlayUiClick();
        }

        private void UpdateHighlights()
        {
            if (!updateHighlightAlpha) return;
            for (int i = 0; i < toggles.Length; i++)
            {
                var ht = toggles[i].optionalHighlight;
                if (ht == null) continue;
                bool active = toggles[i].toggle != null && toggles[i].toggle.isOn;
                var c = ht.color;
                c.a = active ? 1f : inactiveHighlightAlpha;
                ht.color = c;
            }
        }

        // Публічний метод примусово задати тип із коду
        public void ForceSelect(VoiceWaveType type)
        {
            for (int i = 0; i < toggles.Length; i++)
            {
                if (toggles[i].type == type)
                {
                    if (toggles[i].toggle != null && !toggles[i].toggle.isOn)
                        toggles[i].toggle.isOn = true; // викличе обробку
                    return;
                }
            }
        }
    }
}
