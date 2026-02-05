using System;
using System.Collections.Generic;
using LittleBeakCluck.Combat;
using UnityEngine;
using UnityEngine.Scripting;
using System.Reflection;

namespace LittleBeakCluck.World
{
    [CreateAssetMenu(menuName = "LittleBeakCluck/Waves/Campaign Wave Set", fileName = "CampaignWaveSet")]
    [Preserve]
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class CampaignWaveAsset : ScriptableObject
    {
        [SerializeField] private List<WaveDefinitionData> _waves = new();
        [Header("Procedural Generation")]
        [Tooltip("Якщо увімкнено – хвилі кампанії будуть генеруватися з пулу, а результат кешуватиметься на диску.")]
        public bool useProceduralGeneration = false;

        [Tooltip("Кількість хвиль, які треба згенерувати, якщо список Waves порожній.")]
        public int proceduralWaveCount = 0;

        [Tooltip("Пул ворогів для процедурної генерації кампанії.")]
        public List<EndlessSpawnEntryData> proceduralEntries = new();

        [Tooltip("Базовий інтервал між спавнами всередині групи для процедурних кампаній.")]
        public float proceduralDefaultSpawnInterval = -1f;

        [Tooltip("Діапазон кількості груп у процедурній кампанійній хвилі.")]
        public Vector2Int proceduralGroupsPerWaveRange = new Vector2Int(1, 3);

        [Tooltip("Унікальний ідентифікатор кешу для цього профілю кампанії.")]
        public string cacheId = "campaign-default";

        [Tooltip("Версія кешу. Змініть, щоб скинути збережені хвилі.")]
        public string cacheVersion = "1";

        public IReadOnlyList<WaveDefinitionData> Waves => _waves;

        public IReadOnlyList<EndlessSpawnEntryData> ProceduralEntries => proceduralEntries;
    }

    [Serializable]
    [Preserve]
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class WaveDefinitionData
    {
        public string name;
        public VoiceWaveType requiredPlayerWave = VoiceWaveType.Mid;
        public List<WaveEntryData> entries = new();
    }

    [Serializable]
    [Preserve]
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class WaveEntryData
    {
        public GameObject prefab;
        [Min(0)] public int count = 1;
        [Tooltip("Ключ спавнпоінта у сцені, до якого буде прив'язаний спавн цієї групи ворогів.")]
        public string spawnPointKey = string.Empty;
        [Tooltip("Якщо увімкнено, вороги цієї групи будуть заспавнені на кожному доступному спавнпоінті, ігноруючи ключ.")]
        [Preserve]
        public bool useAllSpawnPoints = false;
        [Tooltip("Перевизначення інтервалу між спавнами цієї групи, сек. Якщо <0 — використовується налаштування SpawnPoint.")]
        public float spawnInterval = -1f;
    }

    [Serializable]
    [Preserve]
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public class EndlessSpawnEntryData
    {
        public GameObject prefab;
        [Tooltip("Ключ спавнпоінта у сцені, який буде використовуватися. Порожнє значення дозволяє вибрати будь-який доступний спавнпоінт.")]
        public string spawnPointKey = string.Empty;
        [Tooltip("Якщо увімкнено, ця група буде спавнитися на всіх доступних спавнпоінтах одночасно.")]
        [Preserve]
        public bool useAllSpawnPoints = false;
        [Tooltip("Мінімальна кількість ворогів у групі.")]
        public int minCount = 1;
        [Tooltip("Максимальна кількість ворогів у групі.")]
        public int maxCount = 3;
        [Tooltip("Перевизначення інтервалу між спавнами цієї групи, сек. Якщо <0 — використовується значення профілю.")]
        public float spawnInterval = -1f;
    }
}
