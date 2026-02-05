using System;
using System.Collections.Generic;
using LittleBeakCluck.Player;
using UnityEngine;

namespace LittleBeakCluck.Services
{
    public class PlayerProgressService : IPlayerProgressService
    {
        private const string BalanceKey = "LBC_PlayerBalance";
        private const string UpgradeKeyPrefix = "LBC_Upgrade_";

        private readonly Dictionary<PlayerUpgradeType, UpgradeDescriptor> _descriptors = new();

        private readonly Dictionary<PlayerUpgradeType, int> _upgradeLevels = new();
        private int _balance;

        public event Action<int> BalanceChanged;
        public event Action<PlayerUpgradeType, int> UpgradeLevelChanged;

        public int Balance => _balance;

        public PlayerProgressService(PlayerUpgradeConfigAsset configAsset = null)
        {
            BuildDescriptors(configAsset);
            LoadState();
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0)
                return;

            long target = (long)_balance + amount;
            _balance = target >= int.MaxValue ? int.MaxValue : (int)target;
            PersistBalance();
            PlayerPrefs.Save();
            BalanceChanged?.Invoke(_balance);
        }

        public bool TrySpendCoins(int amount)
        {
            if (amount <= 0)
                return false;

            if (_balance < amount)
                return false;

            _balance -= amount;
            PersistBalance();
            PlayerPrefs.Save();
            BalanceChanged?.Invoke(_balance);
            return true;
        }

        public int GetUpgradeLevel(PlayerUpgradeType type)
        {
            return _upgradeLevels.TryGetValue(type, out var level) ? Mathf.Clamp(level, 0, GetMaxLevel(type)) : 0;
        }

        public int GetMaxLevel(PlayerUpgradeType type)
        {
            return _descriptors.TryGetValue(type, out var descriptor) ? descriptor.MaxLevel : 0;
        }

        public bool TryGetNextUpgradeCost(PlayerUpgradeType type, out int cost)
        {
            cost = 0;
            if (!_descriptors.TryGetValue(type, out var descriptor))
                return false;

            int level = GetUpgradeLevel(type);
            if (level >= descriptor.MaxLevel)
                return false;

            cost = descriptor.CostForLevel(level);
            return true;
        }

        public bool TryPurchaseUpgrade(PlayerUpgradeType type)
        {
            if (!_descriptors.TryGetValue(type, out var descriptor))
                return false;

            int currentLevel = GetUpgradeLevel(type);
            if (currentLevel >= descriptor.MaxLevel)
                return false;

            int cost = descriptor.CostForLevel(currentLevel);
            if (!TrySpendCoins(cost))
                return false;

            int newLevel = currentLevel + 1;
            _upgradeLevels[type] = Mathf.Clamp(newLevel, 0, descriptor.MaxLevel);
            PersistUpgrade(type);
            PlayerPrefs.Save();
            UpgradeLevelChanged?.Invoke(type, _upgradeLevels[type]);
            return true;
        }

        public float GetStatMultiplier(PlayerUpgradeType type)
        {
            if (!_descriptors.TryGetValue(type, out var descriptor))
                return 1f;

            int level = GetUpgradeLevel(type);
            return descriptor.MultiplierForLevel(level);
        }

        public float GetPerLevelBonus(PlayerUpgradeType type)
        {
            return _descriptors.TryGetValue(type, out var descriptor) ? descriptor.PerLevelBonus : 0f;
        }

        private void LoadState()
        {
            _balance = Mathf.Max(0, PlayerPrefs.GetInt(BalanceKey, 0));

            _upgradeLevels.Clear();

            foreach (var pair in _descriptors)
            {
                var type = pair.Key;
                string key = BuildUpgradeKey(type);
                int stored = PlayerPrefs.GetInt(key, 0);
                stored = Mathf.Clamp(stored, 0, pair.Value.MaxLevel);
                _upgradeLevels[type] = stored;
            }
        }

        private void PersistBalance()
        {
            PlayerPrefs.SetInt(BalanceKey, Mathf.Max(0, _balance));
        }

        private void PersistUpgrade(PlayerUpgradeType type)
        {
            if (!_descriptors.TryGetValue(type, out var descriptor))
                return;

            int level = GetUpgradeLevel(type);
            PlayerPrefs.SetInt(BuildUpgradeKey(type), Mathf.Clamp(level, 0, descriptor.MaxLevel));
        }

        private static string BuildUpgradeKey(PlayerUpgradeType type)
        {
            return UpgradeKeyPrefix + type;
        }

        private void BuildDescriptors(PlayerUpgradeConfigAsset configAsset)
        {
            _descriptors.Clear();

            IReadOnlyList<PlayerUpgradeConfigAsset.UpgradeEntry> sourceEntries = configAsset != null
                ? configAsset.Entries
                : null;

            if (sourceEntries != null && sourceEntries.Count > 0)
            {
                for (int i = 0; i < sourceEntries.Count; i++)
                {
                    var entry = sourceEntries[i];
                    if (entry == null)
                        continue;

                    var descriptor = new UpgradeDescriptor(
                        Mathf.Max(0, entry.maxLevel),
                        Mathf.Max(0, entry.baseCost),
                        Mathf.Max(0, entry.costStep),
                        Mathf.Max(0f, entry.perLevelBonus));

                    _descriptors[entry.type] = descriptor;
                }
            }

            if (_descriptors.Count == 0)
            {
                _descriptors[PlayerUpgradeType.MovementSpeed] = new UpgradeDescriptor(5, 80, 60, 0.10f);
                _descriptors[PlayerUpgradeType.WaveDamage] = new UpgradeDescriptor(5, 120, 90, 0.20f);
                _descriptors[PlayerUpgradeType.ChargeSpeed] = new UpgradeDescriptor(5, 100, 75, 0.15f);
            }
        }

        private readonly struct UpgradeDescriptor
        {
            public readonly int MaxLevel;
            public readonly int BaseCost;
            public readonly int CostStep;
            public readonly float PerLevelBonus;

            public UpgradeDescriptor(int maxLevel, int baseCost, int costStep, float perLevelBonus)
            {
                MaxLevel = Mathf.Max(0, maxLevel);
                BaseCost = Mathf.Max(0, baseCost);
                CostStep = Mathf.Max(0, costStep);
                PerLevelBonus = Mathf.Max(0f, perLevelBonus);
            }

            public int CostForLevel(int level)
            {
                return BaseCost + CostStep * Mathf.Clamp(level, 0, int.MaxValue);
            }

            public float MultiplierForLevel(int level)
            {
                return 1f + Mathf.Clamp(level, 0, int.MaxValue) * PerLevelBonus;
            }
        }
    }
}
