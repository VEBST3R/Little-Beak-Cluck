using System.Collections.Generic;
using UnityEngine;

namespace LittleBeakCluck.World
{
    [CreateAssetMenu(menuName = "LittleBeakCluck/Waves/Endless Wave Set", fileName = "EndlessWaveSet")]
    public class EndlessWaveAsset : ScriptableObject
    {
        [SerializeField] private List<EndlessSpawnEntryData> _entries = new();

        [Header("Spawn Behaviour")]
        [Tooltip("Базовий інтервал між спавнами всередині групи, сек. Від'ємне значення означає використовувати інтервал SpawnPoint.")]
        public float defaultSpawnInterval = -1f;

        [Tooltip("Діапазон кількості груп ворогів, які будуть створені в одній хвилі.")]
        public Vector2Int groupsPerWaveRange = new Vector2Int(1, 3);

        [Tooltip("Максимальна кількість хвиль у нескінченному режимі. 0 = безкінечно.")]
        public int maxGeneratedWaves = 0;

        public IReadOnlyList<EndlessSpawnEntryData> Entries => _entries;
    }
}
