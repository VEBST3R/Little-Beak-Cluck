using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace LittleBeakCluck.UI.Settings
{
    [Preserve]
    public static class ControlLayoutSettings
    {
        private const string PlayerPrefsKey = "Controls.InvertLayout";

        public static event Action<bool> InvertedChanged;

        public static bool IsInverted
        {
            get => PlayerPrefs.GetInt(PlayerPrefsKey, 0) == 1;
            set
            {
                bool current = IsInverted;
                if (current == value)
                    return;

                PlayerPrefs.SetInt(PlayerPrefsKey, value ? 1 : 0);
                PlayerPrefs.Save();
                InvertedChanged?.Invoke(value);
            }
        }
    }
}
