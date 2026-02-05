using System;
using System.Collections.Generic;
using LittleBeakCluck.Audio;
using LittleBeakCluck.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace PecksySmash.UI
{
    /// <summary>
    /// Generic panel switcher: attach to a panel root (e.g., Menu, Settings).
    /// Configure button -> target panel pairs in the inspector. When a button is clicked:
    /// - Current panel (the GameObject this component is on) is deactivated.
    /// - Target panel is activated.
    /// Supports optional extra callbacks and optional exclusive group (to auto-disable siblings).
    /// </summary>
    public class PanelSwitcher : MonoBehaviour
    {
        [Serializable]
        public class Switch
        {
            public Button TriggerButton;
            public GameObject TargetPanel;
            [Tooltip("Optional extra objects to enable along with TargetPanel")] public List<GameObject> AlsoEnable = new();
            [Tooltip("Optional extra objects to disable when switching")] public List<GameObject> AlsoDisable = new();
        }

        [Header("Switches")] public List<Switch> Switches = new();
        [Header("Options")] public bool AutoFindButtonsInChildren = false;
        [Tooltip("If true, when switching we disable all sibling root panels except the target.")] public bool ExclusiveSiblings = true;

        private void Awake()
        {
            if (AutoFindButtonsInChildren && Switches.Count == 0)
            {
                foreach (var btn in GetComponentsInChildren<Button>(true))
                {
                    // Skip if already configured
                    if (Switches.Exists(s => s.TriggerButton == btn)) continue;
                    Switches.Add(new Switch { TriggerButton = btn });
                }
            }

            foreach (var sw in Switches)
            {
                if (sw.TriggerButton == null) continue;
                sw.TriggerButton.onClick.AddListener(() => PerformSwitch(sw));
            }
        }

        private void OnDestroy()
        {
            foreach (var sw in Switches)
            {
                if (sw.TriggerButton != null)
                    sw.TriggerButton.onClick.RemoveAllListeners(); // safe: dedicated buttons for this panel
            }
        }

        private void PerformSwitch(Switch sw)
        {
            PlayClickSound();

            if (sw == null) return;
            if (sw.TargetPanel == null)
            {
                Debug.LogWarning($"[PanelSwitcher] TargetPanel not set for button {sw.TriggerButton?.name}", this);
                return;
            }

            // Activate target
            if (!sw.TargetPanel.activeSelf) sw.TargetPanel.SetActive(true);

            // Extra enables
            foreach (var go in sw.AlsoEnable)
            {
                if (go != null && !go.activeSelf) go.SetActive(true);
            }

            // Disable current panel
            if (gameObject.activeSelf) gameObject.SetActive(false);

            // Extra disables
            foreach (var go in sw.AlsoDisable)
            {
                if (go != null && go.activeSelf) go.SetActive(false);
            }

            // Exclusive siblings logic
            if (ExclusiveSiblings)
            {
                var parent = transform.parent;
                if (parent != null)
                {
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        var child = parent.GetChild(i).gameObject;
                        if (child == sw.TargetPanel || child == this.gameObject) continue; // current already disabled above
                        // Consider sibling a panel root if it also has PanelSwitcher OR it's inactive root
                        if (child.GetComponent<PanelSwitcher>() != null)
                        {
                            if (child.activeSelf) child.SetActive(false);
                        }
                    }
                }
            }
        }

        private static void PlayClickSound()
        {
            var audio = ServiceLocator.Instance.Get<IAudioService>();
            audio?.PlayUiClick();
        }
    }
}
