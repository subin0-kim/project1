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
        private MonsterData _monsterData;

        [SerializeField]
        private EnemyHealth _enemyPrefab;

        [Tooltip("화면 내 항상 유지되어야 하는 이 적의 최소 마릿수. 부족하면 소환 간격마다 채워진다.")]
        [SerializeField, Min(0)]
        private int _minAliveCount = 2;

        [SerializeField, Min(0f)]
        private float _moveSpeed = 1f;

        public string EnemyType => string.IsNullOrWhiteSpace(_enemyType) ? "Default" : _enemyType;
        public MonsterData MonsterData => _monsterData;
        public EnemyHealth EnemyPrefab => _monsterData != null && _monsterData.EnemyPrefab != null ? _monsterData.EnemyPrefab : _enemyPrefab;
        public int MinAliveCount => Mathf.Max(0, _minAliveCount);
        public float MoveSpeed => _monsterData != null ? _monsterData.MoveSpeed : Mathf.Max(0f, _moveSpeed);

        /// <summary>
        /// 동일 종류 판별 키. 같은 MonsterData(없으면 프리팹)를 공유하면 같은 종류로 본다.
        /// </summary>
        public object SpeciesKey
        {
            get
            {
                if (_monsterData != null)
                {
                    return _monsterData;
                }

                return _enemyPrefab != null ? (object)_enemyPrefab : EnemyType;
            }
        }

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

        [Tooltip("이 웨이브가 유지되는 시간(초). VS 타임라인 기준 보통 60초(1분). 0 이하이면 기본 60초.")]
        [SerializeField, Min(0f)]
        private float _durationSeconds = 60f;

        [Tooltip("최소 유지 수를 채우거나 추가 소환할 때의 간격(초).")]
        [SerializeField, Min(0.01f)]
        private float _spawnIntervalSeconds = 1f;

        [Tooltip("이 웨이브에서 화면에 동시에 존재할 수 있는 전체 적의 상한.")]
        [SerializeField, Min(1)]
        private int _maxAliveEnemies = 20;

        [SerializeField]
        private List<WaveEnemySpawnEntry> _enemies = new List<WaveEnemySpawnEntry>();

        private const float DefaultDurationSeconds = 60f;

        public string WaveName => string.IsNullOrWhiteSpace(_waveName) ? "Wave" : _waveName;
        public float DurationSeconds => _durationSeconds <= 0f ? DefaultDurationSeconds : _durationSeconds;
        public float SpawnIntervalSeconds => Mathf.Max(0.01f, _spawnIntervalSeconds);
        public int MaxAliveEnemies => Mathf.Max(1, _maxAliveEnemies);
        public IReadOnlyList<WaveEnemySpawnEntry> Enemies => _enemies;

        /// <summary>
        /// 이 웨이브가 화면에 유지하려는 적의 총 최소 마릿수(모든 종류 합).
        /// </summary>
        public int GetTotalMinAliveCount()
        {
            int total = 0;
            for (int i = 0; i < _enemies.Count; i++)
            {
                WaveEnemySpawnEntry entry = _enemies[i];
                if (entry == null)
                {
                    continue;
                }

                total += entry.MinAliveCount;
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
        /// <summary>웨이브 지속 시간이 경과하여 다음 웨이브로 전환됨.</summary>
        TimeExpired = 1,
        /// <summary>외부 요인(보스 진입, 중지 등)으로 웨이브가 종료됨.</summary>
        Cancelled = 2
    }
}
