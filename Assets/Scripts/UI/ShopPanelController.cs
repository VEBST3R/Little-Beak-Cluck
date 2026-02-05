using System;
using LittleBeakCluck.Audio;
using LittleBeakCluck.Infrastructure;
using LittleBeakCluck.Player;
using LittleBeakCluck.Services;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LittleBeakCluck.UI
{
    [DisallowMultipleComponent]
    public class ShopPanelController : MonoBehaviour
    {
        [Header("Balance")]
        [SerializeField] private TMP_Text balanceLabel;
        [SerializeField] private string balanceFormat = "{0}";

        [Header("Upgrades")]
        [SerializeField] private UpgradeEntry[] upgradeEntries = Array.Empty<UpgradeEntry>();

        private IPlayerProgressService _progressService;
        private bool _eventsHooked;
        private bool _loggedMissingService;

        private void Awake()
        {
            ResolveProgressService();
        }

        private void OnEnable()
        {
            ResolveProgressService();
            HookEvents(true);
            RegisterButtonHandlers(true);
            RefreshAll();
        }

        private void OnDisable()
        {
            HookEvents(false);
            RegisterButtonHandlers(false);
        }

        private void OnDestroy()
        {
            HookEvents(false);
        }

        private void ResolveProgressService()
        {
            if (_progressService != null)
                return;

            _progressService = ServiceLocator.Instance.Get<IPlayerProgressService>();
            if (_progressService == null && !_loggedMissingService)
            {
                Debug.LogWarning($"[{name}] ShopPanelController could not locate IPlayerProgressService. Shop UI will be disabled until the service becomes available.", this);
                _loggedMissingService = true;
            }
        }

        private void HookEvents(bool register)
        {
            if (_progressService == null)
                return;

            if (register && !_eventsHooked)
            {
                _progressService.BalanceChanged += HandleBalanceChanged;
                _progressService.UpgradeLevelChanged += HandleUpgradeLevelChanged;
                _eventsHooked = true;
            }
            else if (!register && _eventsHooked)
            {
                _progressService.BalanceChanged -= HandleBalanceChanged;
                _progressService.UpgradeLevelChanged -= HandleUpgradeLevelChanged;
                _eventsHooked = false;
            }
        }

        private void RegisterButtonHandlers(bool register)
        {
            if (upgradeEntries == null)
                return;

            for (int i = 0; i < upgradeEntries.Length; i++)
            {
                var entry = upgradeEntries[i];
                if (entry == null || entry.purchaseButton == null)
                    continue;

                if (entry.cachedHandler != null)
                {
                    entry.purchaseButton.onClick.RemoveListener(entry.cachedHandler);
                    entry.cachedHandler = null;
                }

                if (register)
                {
                    var captured = entry;
                    entry.cachedHandler = () => OnPurchaseClicked(captured);
                    entry.purchaseButton.onClick.AddListener(entry.cachedHandler);
                }
            }
        }

        private void RefreshAll()
        {
            if (_progressService == null)
            {
                ResolveProgressService();
                HookEvents(true);
            }

            UpdateBalanceLabel();

            if (upgradeEntries == null)
                return;

            for (int i = 0; i < upgradeEntries.Length; i++)
            {
                var entry = upgradeEntries[i];
                if (entry != null)
                {
                    UpdateEntryView(entry);
                }
            }
        }

        private void UpdateBalanceLabel()
        {
            if (balanceLabel == null)
                return;

            int balance = _progressService != null ? Mathf.Max(0, _progressService.Balance) : 0;
            balanceLabel.text = string.Format(balanceFormat, balance);
        }

        private void HandleBalanceChanged(int balance)
        {
            UpdateBalanceLabel();

            if (upgradeEntries == null)
                return;

            for (int i = 0; i < upgradeEntries.Length; i++)
            {
                var entry = upgradeEntries[i];
                if (entry != null)
                {
                    UpdateEntryView(entry);
                }
            }
        }

        private void HandleUpgradeLevelChanged(PlayerUpgradeType type, int level)
        {
            if (upgradeEntries == null)
                return;

            for (int i = 0; i < upgradeEntries.Length; i++)
            {
                var entry = upgradeEntries[i];
                if (entry != null && entry.type == type)
                {
                    UpdateEntryView(entry);
                    break;
                }
            }
        }

        private void OnPurchaseClicked(UpgradeEntry entry)
        {
            PlayPurchaseSound();

            if (_progressService == null)
            {
                ResolveProgressService();
                if (_progressService == null)
                    return;

                HookEvents(true);
            }

            bool purchased = _progressService.TryPurchaseUpgrade(entry.type);
            if (!purchased)
            {
                UpdateEntryView(entry);
            }
        }

        private void UpdateEntryView(UpgradeEntry entry)
        {
            string label = string.IsNullOrWhiteSpace(entry.displayName) ? entry.type.ToString() : entry.displayName;
            int currentLevel = 0;
            int maxLevel = 0;
            int nextCost = 0;
            bool hasNextCost = false;
            bool isMaxed = false;
            float perLevelBonus = 0f;
            float totalBonusPercent = 0f;

            if (_progressService != null)
            {
                currentLevel = _progressService.GetUpgradeLevel(entry.type);
                maxLevel = _progressService.GetMaxLevel(entry.type);
                isMaxed = maxLevel > 0 && currentLevel >= maxLevel;
                hasNextCost = !isMaxed && _progressService.TryGetNextUpgradeCost(entry.type, out nextCost);
                perLevelBonus = _progressService.GetPerLevelBonus(entry.type) * 100f;
                totalBonusPercent = (_progressService.GetStatMultiplier(entry.type) - 1f) * 100f;
            }

            if (entry.titleLabel != null)
            {
                entry.titleLabel.text = label;
            }

            if (entry.levelLabel != null)
            {
                entry.levelLabel.text = string.Format(entry.levelFormat, currentLevel, maxLevel);
            }

            if (entry.costLabel != null)
            {
                if (_progressService == null)
                {
                    entry.costLabel.text = entry.maxedCostText;
                }
                else if (hasNextCost)
                {
                    entry.costLabel.text = string.Format(entry.costFormat, Mathf.Max(0, nextCost));
                }
                else
                {
                    entry.costLabel.text = entry.maxedCostText;
                }
            }

            if (entry.effectLabel != null)
            {
                if (_progressService == null)
                {
                    entry.effectLabel.text = string.Empty;
                }
                else
                {
                    int perLevelRounded = Mathf.RoundToInt(perLevelBonus);
                    int totalRounded = Mathf.RoundToInt(totalBonusPercent);
                    entry.effectLabel.text = string.Format(entry.effectFormat, perLevelRounded, totalRounded);
                }
            }

            if (entry.purchaseButton != null)
            {
                if (_progressService == null)
                {
                    entry.purchaseButton.interactable = false;
                }
                else if (hasNextCost)
                {
                    entry.purchaseButton.interactable = _progressService.Balance >= nextCost;
                }
                else
                {
                    entry.purchaseButton.interactable = false;
                }
            }

            if (entry.maxedState != null)
            {
                entry.maxedState.SetActive(_progressService != null && (isMaxed || (!hasNextCost && maxLevel > 0)));
            }
        }

        private static void PlayPurchaseSound()
        {
            var audio = ServiceLocator.Instance.Get<IAudioService>();
            audio?.PlayPurchase();
        }

        [Serializable]
        private sealed class UpgradeEntry
        {
            public PlayerUpgradeType type = PlayerUpgradeType.MovementSpeed;
            public string displayName = string.Empty;
            [Header("Labels")]
            public TMP_Text titleLabel;
            public TMP_Text levelLabel;
            public TMP_Text costLabel;
            public TMP_Text effectLabel;
            [Header("Interactions")]
            public Button purchaseButton;
            public GameObject maxedState;
            [Header("Formatting")]
            public string levelFormat = "Lv. {0}/{1}";
            public string costFormat = "{0}";
            public string maxedCostText = "MAX";
            public string effectFormat = "+{0}% per level (+{1}% total)";

            [NonSerialized] public UnityAction cachedHandler;
        }
    }
}
