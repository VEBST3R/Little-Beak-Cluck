using System;
using UnityEngine;
using UnityEngine.UI;

namespace LittleBeakCluck.UI.Settings
{
    [DisallowMultipleComponent]
    public class ControlInversionSettingsPanel : MonoBehaviour
    {
        [SerializeField] private Toggle invertControlsToggle;

        private bool _syncing;

        private void OnEnable()
        {
            Register(true);
            SyncFromSettings();
        }

        private void OnDisable()
        {
            Register(false);
        }

        private void Register(bool on)
        {
            if (invertControlsToggle == null)
                return;

            if (on)
            {
                invertControlsToggle.onValueChanged.AddListener(OnToggleChanged);
                ControlLayoutSettings.InvertedChanged += OnInvertedChanged;
            }
            else
            {
                invertControlsToggle.onValueChanged.RemoveListener(OnToggleChanged);
                ControlLayoutSettings.InvertedChanged -= OnInvertedChanged;
            }
        }

        private void SyncFromSettings()
        {
            if (invertControlsToggle == null)
                return;
            _syncing = true;
            invertControlsToggle.isOn = ControlLayoutSettings.IsInverted;
            _syncing = false;
        }

        private void OnToggleChanged(bool value)
        {
            if (_syncing)
                return;
            ControlLayoutSettings.IsInverted = value;
        }

        private void OnInvertedChanged(bool value)
        {
            if (invertControlsToggle == null)
                return;
            _syncing = true;
            invertControlsToggle.isOn = value;
            _syncing = false;
        }
    }
}
