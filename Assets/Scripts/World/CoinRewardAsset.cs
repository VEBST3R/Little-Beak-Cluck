using System;
using System.Collections.Generic;
using LittleBeakCluck.Enemies;
using UnityEngine;

namespace LittleBeakCluck.World
{
    [CreateAssetMenu(menuName = "LittleBeakCluck/Economy/Coin Reward Config", fileName = "CoinRewardConfig")]
    public class CoinRewardAsset : ScriptableObject
    {
        [Header("Defaults")]
        [Tooltip("Базова нагорода в монетах за хвилю кампанії, якщо не задано інше.")]
        public int defaultCampaignWaveCoins = 25;

        [Tooltip("Діапазон нагород для процедурно згенерованих хвиль.")]
        public Vector2Int proceduralRewardRange = new Vector2Int(15, 30);

        [Tooltip("Кількість монет за вбитого ворога у нескінченному режимі, якщо не задано інше.")]
        public int defaultEndlessCoinsPerKill = 1;

        [Header("Enemy Rewards")]
        [Tooltip("Налаштування нагороди для конкретних типів ворогів.")]
        public List<EnemyCoinRewardEntry> enemyRewards = new();

        [Header("Campaign Wave Rewards")]
        [Tooltip("Перевизначення нагороди для хвиль кампанії (за назвою або індексом).")]
        public List<CampaignWaveRewardEntry> campaignRewards = new();
    }

    [Serializable]
    public class EnemyCoinRewardEntry
    {
        public EnemyConfig enemy;
        [Min(0)] public int coinsPerKill = 1;
    }

    [Serializable]
    public class CampaignWaveRewardEntry
    {
        [Tooltip("Назва хвилі для зіставлення. Залиште порожнім, щоб використовувати лише індекс.")]
        public string waveName = string.Empty;
        [Tooltip("Індекс хвилі (0-базований). Встановіть -1, щоб ігнорувати.")]
        public int waveIndex = -1;
        [Min(0)] public int coins = 25;
    }
}
