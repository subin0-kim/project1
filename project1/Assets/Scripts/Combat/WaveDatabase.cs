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
        /// fallback 우선순위: MonsterData → EnemyPrefab → EnemyType 문자열.
        /// 주의: MonsterData·프리팹을 모두 비워두면 EnemyType 문자열(기본값 "Default")이 키가 되므로,
        /// 그런 엔트리를 여러 개 추가하면서 EnemyType을 따로 지정하지 않으면 모두 한 종류로 묶인다.
        /// (현재 IsValid가 MonsterData/프리팹 중 하나는 반드시 요구하므로 유효한 엔트리에서는 EnemyType 폴백까지 가지 않는다.)
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

        /// <summary>
        /// UI 표시용 종류 이름. <see cref="SpeciesKey"/>와 <b>같은 우선순위</b>(MonsterData → EnemyPrefab → EnemyType)로
        /// 결정해, 키가 가리키는 종류와 화면에 뜨는 이름이 어긋나지 않게 한다.
        ///
        /// 적 인스턴스(<see cref="EnemyHealth.DisplayName"/>)에서 읽지 않는 이유:
        /// <see cref="EnemyHealth.ApplyMonsterData"/>는 인자가 null이면 기존 값을 유지하므로,
        /// MonsterData가 없는 엔트리에 풀에서 재사용된 개체가 배정되면 이전 종류의 이름을 그대로 반환한다.
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (_monsterData != null)
                {
                    return _monsterData.DisplayName;
                }

                return _enemyPrefab != null ? _enemyPrefab.name : EnemyType;
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
