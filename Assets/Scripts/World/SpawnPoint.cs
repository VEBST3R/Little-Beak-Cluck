using System.Collections;
using System.Collections.Generic;
using LittleBeakCluck.Combat;
using UnityEngine;
using Lean.Pool;

namespace LittleBeakCluck.World
{
    // Універсальна точка спавну з опціями випадкової затримки, кількості та розкиду позиції
    public class SpawnPoint : MonoBehaviour
    {
        [Header("Prefabs to Spawn")]
        [SerializeField] private List<GameObject> _prefabs = new();

        [Header("Spawn Timing")]
        [SerializeField] private bool _spawnOnStart = false; // за замовчуванням керує WaveManager
        [Tooltip("Не спавнити автоматично, якщо у сцені є WaveManager")][SerializeField] private bool _skipAutoSpawnWhenWaveManagerPresent = true;
        [SerializeField] private float _initialDelay = 0f;
        [SerializeField] private int _count = 1;
        [SerializeField] private float _betweenDelay = 0.2f;

        [Header("Position Randomization")]
        [Tooltip("Радіус випадкового кола навколо точки спавну")][SerializeField] private float _randomRadius = 0f;
        [Tooltip("Тримати Z як у точки спавну, ігноруючи Z у префабів")][SerializeField] private bool _lockZToSpawn = true;

        [Header("Enemy Setup (optional)")]
        [Tooltip("Якщо увімкнути, встановить ворогу певний тип сприйняття хвилі після спавну")]
        [SerializeField] private bool _overrideEnemyWaveType = false;
        [SerializeField] private VoiceWaveType _enemyWaveType = VoiceWaveType.High;

        [Header("Parenting")]
        [Tooltip("Прив'язувати заспавнені об'єкти як дочірні до цього об'єкта")]
        [SerializeField] private bool _parentSpawned = false;

        [Header("Identification")]
        [Tooltip("Унікальний ключ, яким посилаються ScriptableObject хвиль")]
        [SerializeField] private string _key = string.Empty;

        public string Key => _key;

        private void Start()
        {
            if (_spawnOnStart)
            {
                bool waveManagerPresent = FindFirstObjectByType<WaveManager>(FindObjectsInactive.Include) != null;
                if (_skipAutoSpawnWhenWaveManagerPresent && waveManagerPresent)
                {
                    // Уникаємо випадкового раннього спавну, коли хвилями керує WaveManager
                    // Debug.Log($"SpawnPoint '{name}': Auto-spawn skipped due to WaveManager present.", this);
                }
                else
                {
                    StartCoroutine(SpawnRoutine());
                }
            }
        }

        // Публічні аксесори для таймінгів — щоб WaveManager міг використовувати темп спавну спавнера
        public float BetweenDelay => _betweenDelay;
        public float InitialDelay => _initialDelay;

        public void TriggerSpawn() => StartCoroutine(SpawnRoutine());

        private IEnumerator SpawnRoutine()
        {
            if (_initialDelay > 0f)
                yield return new WaitForSeconds(_initialDelay);

            for (int i = 0; i < Mathf.Max(1, _count); i++)
            {
                SpawnOnce();
                if (i < _count - 1 && _betweenDelay > 0f)
                    yield return new WaitForSeconds(_betweenDelay);
            }
        }

        public GameObject SpawnOnce()
        {
            if (_prefabs == null || _prefabs.Count == 0)
            {
                Debug.LogWarning("SpawnPoint: No prefabs assigned.", this);
                return null;
            }

            var prefab = _prefabs[Random.Range(0, _prefabs.Count)];
            if (prefab == null)
            {
                Debug.LogWarning("SpawnPoint: Prefab is null.", this);
                return null;
            }

            Vector3 pos = transform.position;
            if (_randomRadius > 0f)
            {
                var offset = Random.insideUnitCircle * _randomRadius;
                pos += new Vector3(offset.x, offset.y, 0f);
            }
            if (_lockZToSpawn)
                pos.z = transform.position.z;

            Quaternion rot = Quaternion.identity;
            var parent = _parentSpawned ? transform : null;
            GameObject go = LeanPool.Spawn(prefab, pos, rot, parent);

            if (_overrideEnemyWaveType)
            {
                var affinity = go.GetComponentInChildren<EnemyWaveAffinity>();
                if (affinity != null)
                    affinity.SetEffectiveType(_enemyWaveType);
            }

            return go;
        }

        public GameObject SpawnFromPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogWarning("SpawnPoint: SpawnFromPrefab received null prefab.", this);
                return null;
            }

            Vector3 pos = transform.position;
            if (_randomRadius > 0f)
            {
                var offset = Random.insideUnitCircle * _randomRadius;
                pos += new Vector3(offset.x, offset.y, 0f);
            }
            if (_lockZToSpawn)
                pos.z = transform.position.z;

            Quaternion rot = Quaternion.identity;
            var parent = _parentSpawned ? transform : null;
            return LeanPool.Spawn(prefab, pos, rot, parent);
        }
    }
}
