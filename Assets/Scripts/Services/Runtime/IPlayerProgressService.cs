using System;
using LittleBeakCluck.Infrastructure;
using LittleBeakCluck.Player;

namespace LittleBeakCluck.Services
{
    public interface IPlayerProgressService : IGameService
    {
        event Action<int> BalanceChanged;
        event Action<PlayerUpgradeType, int> UpgradeLevelChanged;

        int Balance { get; }
        void AddCoins(int amount);
        bool TrySpendCoins(int amount);
        int GetUpgradeLevel(PlayerUpgradeType type);
        int GetMaxLevel(PlayerUpgradeType type);
        bool TryGetNextUpgradeCost(PlayerUpgradeType type, out int cost);
        bool TryPurchaseUpgrade(PlayerUpgradeType type);
        float GetStatMultiplier(PlayerUpgradeType type);
        float GetPerLevelBonus(PlayerUpgradeType type);
    }
}
