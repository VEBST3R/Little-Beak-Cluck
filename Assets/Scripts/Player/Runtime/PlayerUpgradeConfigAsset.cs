using System;
using System.Collections.Generic;
using UnityEngine;

namespace LittleBeakCluck.Player
{
    [CreateAssetMenu(menuName = "LittleBeakCluck/Upgrades/Player Upgrade Config", fileName = "PlayerUpgradeConfig")]
    public class PlayerUpgradeConfigAsset : ScriptableObject
    {
        [SerializeField] private List<UpgradeEntry> upgrades = new();

        public IReadOnlyList<UpgradeEntry> Entries => upgrades;

        public bool TryGetEntry(PlayerUpgradeType type, out UpgradeEntry entry)
        {
            for (int i = 0; i < upgrades.Count; i++)
            {
                var candidate = upgrades[i];
                if (candidate != null && candidate.type == type)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        [Serializable]
        public class UpgradeEntry
        {
            public PlayerUpgradeType type = PlayerUpgradeType.MovementSpeed;
            [Min(0)] public int maxLevel = 5;
            [Min(0)] public int baseCost = 100;
            [Min(0)] public int costStep = 50;
            [Min(0f)] public float perLevelBonus = 0.1f;
        }
    }
}
