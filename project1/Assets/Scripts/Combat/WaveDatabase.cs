using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [Serializable]
    public class WaveEnemySpawnEntry
    {
        [SerializeField]
        private string _enemyType = "Default";

        [SerializeField]
        private EnemyData _monsterData;

        [SerializeField]
        private EnemyBase _enemyPrefab;

        [SerializeField, Min(1)]
        private int _count = 1;

        public string EnemyType => string.IsNullOrWhiteSpace(_enemyType) ? "Default" : _enemyType;
        public EnemyData MonsterData => _monsterData;
        public EnemyBase EnemyPrefab => _monsterData != null && _monsterData.EnemyPrefab != null ? _monsterData.EnemyPrefab : _enemyPrefab;
        public int Count => Mathf.Max(0, _count);

        public bool IsValid(out string reason)
        {
            if (_monsterData != null)
            {
                return _monsterData.IsValid(out reason);
            }

            if (_enemyPrefab == null)
            {
                reason = "Enemy prefab is missing.";
                return false;
            }

            reason = null;
            return true;
        }
    }

    [Serializable]
    public class WaveDefinition
    {
        [SerializeField]
        private string _waveName = "Wave";

        [SerializeField, Min(0f)]
        private float _durationSeconds;

        [SerializeField, Min(0.01f)]
        private float _spawnIntervalSeconds = 1f;

        [SerializeField, Min(1)]
        private int _maxAliveEnemies = 5;

        [SerializeField]
        private List<WaveEnemySpawnEntry> _enemies = new List<WaveEnemySpawnEntry>();

        public string WaveName => string.IsNullOrWhiteSpace(_waveName) ? "Wave" : _waveName;
        public float DurationSeconds => Mathf.Max(0f, _durationSeconds);
        public float SpawnIntervalSeconds => Mathf.Max(0.01f, _spawnIntervalSeconds);
        public int MaxAliveEnemies => Mathf.Max(1, _maxAliveEnemies);
        public IReadOnlyList<WaveEnemySpawnEntry> Enemies => _enemies;

        public int GetTotalSpawnCount()
        {
            int total = 0;
            for (int i = 0; i < _enemies.Count; i++)
            {
                WaveEnemySpawnEntry entry = _enemies[i];
                if (entry == null)
                {
                    continue;
                }

                total += entry.Count;
            }

            return total;
        }
    }

    [CreateAssetMenu(fileName = "WaveDatabase", menuName = "Mukseon/Combat/Wave Database")]
    public class WaveDatabase : ScriptableObject
    {
        [SerializeField]
        private List<WaveDefinition> _waves = new List<WaveDefinition>();

        public IReadOnlyList<WaveDefinition> Waves => _waves;
    }

    public enum WaveEndReason
    {
        EnemiesCleared = 0,
        TimeExpired = 1,
        Cancelled = 2
    }
}
