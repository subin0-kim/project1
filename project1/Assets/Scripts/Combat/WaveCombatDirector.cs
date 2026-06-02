using System;
using System.Collections.Generic;
using Mukseon.Core.Pool;
using Mukseon.Gameplay.Progression;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// Vampire Survivors 스타일의 타임라인 기반 연속 스포너.
    /// 웨이브는 시간 경과로만 전환되며(클리어 조건 없음), 각 웨이브는
    /// 적 종류별 '최소 유지 수'를 채우고, 모두 충족되면 매 간격마다 적을 1마리씩 추가 소환한다.
    /// 지정한 분 마크에 미니 보스 이벤트를, 보스 마크에 보스 진입 이벤트를 발행한다.
    /// 미니 보스/보스의 실제 스폰은 별도 시스템(#71/#37)이 구독하여 처리한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class WaveCombatDirector : MonoBehaviour
    {
        private sealed class SpawnRuntimeEntry
        {
            public WaveEnemySpawnEntry Entry;
            public object SpeciesKey;
        }

        [Header("References")]
        [SerializeField]
        private WaveDatabase _waveDatabase;

        [SerializeField]
        private List<Transform> _spawnPoints = new List<Transform>();

        [SerializeField]
        private bool _collectSpawnPointsFromChildren;

        [Header("Flow")]
        [SerializeField]
        private bool _autoStartOnEnable = true;

        [Tooltip("마지막 웨이브에 도달한 뒤 보스 마크 전까지 타임라인을 처음부터 반복할지 여부.")]
        [SerializeField]
        private bool _loopWaves;

        [Header("Timeline Marks (minutes)")]
        [Tooltip("미니 보스 이벤트를 발행할 분 단위 시간 마크. 예: 3, 6, 9")]
        [SerializeField]
        private List<float> _miniBossMinuteMarks = new List<float> { 3f, 6f, 9f };

        [Tooltip("보스 진입 이벤트를 발행할 분 단위 시간 마크. 이 시점 이후 일반 스포닝 중단. 0 이하이면 보스 마크 없음.")]
        [SerializeField, Min(0f)]
        private float _bossMinuteMark = 10f;

        [Header("Fallback")]
        [SerializeField, Min(0.01f)]
        private float _defaultSpawnIntervalSeconds = 1f;

        [SerializeField, Min(1)]
        private int _defaultMaxAliveEnemies = 20;

        [Header("Debug")]
        [SerializeField]
        private bool _showDebugLogs;

        private readonly List<SpawnRuntimeEntry> _spawnRuntime = new List<SpawnRuntimeEntry>();
        private readonly HashSet<EnemyHealth> _aliveEnemies = new HashSet<EnemyHealth>();
        private readonly Dictionary<EnemyHealth, object> _enemySpeciesKey = new Dictionary<EnemyHealth, object>(64);
        private readonly Dictionary<object, int> _aliveBySpecies = new Dictionary<object, int>(16);
        private readonly List<EnemyHealth> _cleanupBuffer = new List<EnemyHealth>(64);
        private readonly List<bool> _miniBossMarkFired = new List<bool>();

        private int _currentWaveIndex = -1;
        private int _spawnPointCursor;
        private float _timelineElapsedSeconds;
        private float _waveElapsedSeconds;
        private float _spawnElapsedSeconds;
        private bool _isRunning;
        private bool _isWaveActive;
        private bool _bossPhaseStarted;

        public int CurrentWaveNumber => _currentWaveIndex + 1;
        public int RemainingEnemyCount => _aliveEnemies.Count;
        public bool IsWaveActive => _isWaveActive;
        public bool IsRunning => _isRunning;
        public float TimelineElapsedSeconds => _timelineElapsedSeconds;
        public bool IsBossPhase => _bossPhaseStarted;

        public event Action<int, WaveDefinition> OnWaveStarted;
        public event Action<int, WaveEndReason> OnWaveEnded;
        public event Action<int, int> OnRemainingEnemyCountChanged;
        public event Action OnAllWavesCompleted;

        /// <summary>지정 분 마크 도달 시 1회 발행. 인자는 도달한 분(mark) 값. 미니 보스 시스템(#71)이 구독.</summary>
        public event Action<float> OnMiniBossMarkReached;

        /// <summary>보스 마크 도달 시 1회 발행. 일반 스포닝은 이 시점에 중단된다. 보스 시스템(#37)이 구독.</summary>
        public event Action OnBossPhaseStarted;

        private void OnEnable()
        {
            TryCollectSpawnPointsFromChildren();

            if (_autoStartOnEnable)
            {
                StartWaves();
            }
        }

        private void OnDisable()
        {
            StopWaves(false);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void StartWaves()
        {
            TryCollectSpawnPointsFromChildren();

            if (_waveDatabase == null || _waveDatabase.Waves == null || _waveDatabase.Waves.Count == 0)
            {
                Debug.LogWarning("[WaveCombatDirector] WaveDatabase is missing or empty.");
                return;
            }

            StopWaves(false);

            _isRunning = true;
            _bossPhaseStarted = false;
            _timelineElapsedSeconds = 0f;
            _currentWaveIndex = -1;

            ResetMiniBossMarks();
            BeginWave(0);
        }

        public void StopWaves(bool publishCancellation = true)
        {
            if (!_isRunning && !_isWaveActive)
            {
                return;
            }

            if (publishCancellation && _isWaveActive)
            {
                OnWaveEnded?.Invoke(CurrentWaveNumber, WaveEndReason.Cancelled);
            }

            CleanupAliveEnemies(false);
            ResetWaveRuntime();

            _isWaveActive = false;
            _isRunning = false;
            _bossPhaseStarted = false;
            _currentWaveIndex = -1;
        }

        internal void Tick(float deltaTime)
        {
            if (!_isRunning)
            {
                return;
            }

            float step = Mathf.Max(0f, deltaTime);
            _timelineElapsedSeconds += step;

            CheckTimelineMarks();

            // 보스 진입 후에는 일반 스포닝과 웨이브 전환을 모두 중단한다.
            if (_bossPhaseStarted || !_isWaveActive)
            {
                return;
            }

            _waveElapsedSeconds += step;
            _spawnElapsedSeconds += step;

            TrySpawnEnemies();

            WaveDefinition currentWave = GetCurrentWave();
            if (currentWave != null && _waveElapsedSeconds >= currentWave.DurationSeconds)
            {
                AdvanceWave();
            }
        }

        private void CheckTimelineMarks()
        {
            if (_miniBossMinuteMarks != null)
            {
                for (int i = 0; i < _miniBossMinuteMarks.Count; i++)
                {
                    if (i >= _miniBossMarkFired.Count || _miniBossMarkFired[i])
                    {
                        continue;
                    }

                    float markMinutes = _miniBossMinuteMarks[i];
                    if (markMinutes > 0f && _timelineElapsedSeconds >= markMinutes * 60f)
                    {
                        _miniBossMarkFired[i] = true;
                        OnMiniBossMarkReached?.Invoke(markMinutes);

#if UNITY_EDITOR
                        if (_showDebugLogs)
                        {
                            Debug.Log($"[WaveCombatDirector] Mini-boss mark reached: {markMinutes:0.#} min");
                        }
#endif
                    }
                }
            }

            if (!_bossPhaseStarted && _bossMinuteMark > 0f && _timelineElapsedSeconds >= _bossMinuteMark * 60f)
            {
                EnterBossPhase();
            }
        }

        private void EnterBossPhase()
        {
            _bossPhaseStarted = true;

            int endedWaveNumber = CurrentWaveNumber;
            _isWaveActive = false;
            OnWaveEnded?.Invoke(endedWaveNumber, WaveEndReason.Cancelled);

            // 일반 적은 보스 시스템(#37)이 페이드아웃/즉시 제거 방식을 결정하므로
            // 여기서는 살아있는 적을 강제 정리하지 않고 스포닝만 멈춘다.
            OnBossPhaseStarted?.Invoke();
            OnAllWavesCompleted?.Invoke();

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[WaveCombatDirector] Boss phase started at {_bossMinuteMark:0.#} min.");
            }
#endif
        }

        private void BeginWave(int waveIndex)
        {
            ResetWaveRuntime();

            _currentWaveIndex = waveIndex;
            _isWaveActive = true;
            _waveElapsedSeconds = 0f;
            _spawnElapsedSeconds = 0f;

            WaveDefinition currentWave = GetCurrentWave();
            BuildSpawnRuntime(currentWave);
            NotifyRemainingEnemyCountChanged();
            OnWaveStarted?.Invoke(CurrentWaveNumber, currentWave);

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[WaveCombatDirector] Wave {CurrentWaveNumber} started.");
            }
#endif
        }

        private void AdvanceWave()
        {
            if (_waveDatabase == null || _waveDatabase.Waves == null || _waveDatabase.Waves.Count == 0)
            {
                return;
            }

            int waveCount = _waveDatabase.Waves.Count;
            int nextWaveIndex = _currentWaveIndex + 1;

            if (nextWaveIndex >= waveCount)
            {
                if (_loopWaves)
                {
                    nextWaveIndex = 0;
                }
                else
                {
                    // 마지막 웨이브는 보스 마크 전까지 그대로 유지(스폰 지속). 전환만 멈춘다.
                    _waveElapsedSeconds = 0f;
                    return;
                }
            }

            OnWaveEnded?.Invoke(CurrentWaveNumber, WaveEndReason.TimeExpired);

            // VS 스타일: 이전 웨이브의 적은 정리하지 않고 그대로 남긴 채 다음 웨이브로 전환한다.
            BeginWave(nextWaveIndex);
        }

        private WaveDefinition GetCurrentWave()
        {
            if (_waveDatabase == null || _waveDatabase.Waves == null)
            {
                return null;
            }

            if (_currentWaveIndex < 0 || _currentWaveIndex >= _waveDatabase.Waves.Count)
            {
                return null;
            }

            return _waveDatabase.Waves[_currentWaveIndex];
        }

        private void BuildSpawnRuntime(WaveDefinition wave)
        {
            _spawnRuntime.Clear();

            if (wave == null || wave.Enemies == null)
            {
                return;
            }

            for (int i = 0; i < wave.Enemies.Count; i++)
            {
                WaveEnemySpawnEntry entry = wave.Enemies[i];
                if (entry == null)
                {
                    continue;
                }

                if (!entry.IsValid(out string reason))
                {
                    Debug.LogWarning($"[WaveCombatDirector] Wave '{wave.WaveName}' contains invalid enemy entry. {reason}");
                    continue;
                }

                _spawnRuntime.Add(new SpawnRuntimeEntry
                {
                    Entry = entry,
                    SpeciesKey = entry.SpeciesKey
                });
            }
        }

        private void TrySpawnEnemies()
        {
            if (_spawnRuntime.Count <= 0)
            {
                return;
            }

            float spawnInterval = ResolveSpawnInterval();
            int maxAliveEnemies = ResolveMaxAliveEnemies();

            // 간격이 경과한 횟수만큼 소환 라운드를 진행한다.
            while (_spawnElapsedSeconds >= spawnInterval)
            {
                _spawnElapsedSeconds -= spawnInterval;
                RunSpawnRound(maxAliveEnemies);
            }
        }

        /// <summary>
        /// 1회 소환 라운드.
        /// 1) 최소 유지 수에 미달한 종류부터 채운다.
        /// 2) 모든 종류가 최소 수를 충족하면 각 종류를 1마리씩 추가 소환한다(난이도 상승).
        /// 상한(maxAliveEnemies)에 도달하면 중단한다.
        /// </summary>
        private void RunSpawnRound(int maxAliveEnemies)
        {
            bool anyBelowMinimum = false;

            for (int i = 0; i < _spawnRuntime.Count; i++)
            {
                if (_aliveEnemies.Count >= maxAliveEnemies)
                {
                    return;
                }

                SpawnRuntimeEntry runtimeEntry = _spawnRuntime[i];
                int aliveOfSpecies = GetAliveCount(runtimeEntry.SpeciesKey);
                if (aliveOfSpecies < runtimeEntry.Entry.MinAliveCount)
                {
                    anyBelowMinimum = true;
                    SpawnEnemy(runtimeEntry);
                }
            }

            if (anyBelowMinimum)
            {
                return;
            }

            // 모든 종류가 최소 수를 충족 → 각 종류 1마리씩 추가 소환.
            for (int i = 0; i < _spawnRuntime.Count; i++)
            {
                if (_aliveEnemies.Count >= maxAliveEnemies)
                {
                    return;
                }

                SpawnEnemy(_spawnRuntime[i]);
            }
        }

        private void SpawnEnemy(SpawnRuntimeEntry runtimeEntry)
        {
            Transform spawnPoint = ResolveSpawnPoint();
            GameObject prefabGO = runtimeEntry.Entry.EnemyPrefab.gameObject;
            GameObject spawnedObject;
            EnemyHealth spawnedEnemy;

            if (PoolManager.Instance != null)
            {
                spawnedObject = PoolManager.Instance.GetInactive(prefabGO, spawnPoint.position, spawnPoint.rotation);
                spawnedEnemy = spawnedObject.GetComponent<EnemyHealth>();
                spawnedEnemy.ApplyMonsterData(runtimeEntry.Entry.MonsterData);
                spawnedEnemy.SetMoveSpeed(runtimeEntry.Entry.MoveSpeed);
                spawnedEnemy.PrepareForReuse();
                spawnedObject.SetActive(true);
            }
            else
            {
                spawnedObject = Instantiate(prefabGO, spawnPoint.position, spawnPoint.rotation);
                spawnedEnemy = spawnedObject.GetComponent<EnemyHealth>();
                spawnedEnemy.ApplyMonsterData(runtimeEntry.Entry.MonsterData);
                spawnedEnemy.SetMoveSpeed(runtimeEntry.Entry.MoveSpeed);
            }

            EnemySoulDropper soulDropper = spawnedEnemy.GetComponent<EnemySoulDropper>();
            if (soulDropper != null)
            {
                soulDropper.ApplyMonsterData(runtimeEntry.Entry.MonsterData);
            }

            spawnedEnemy.OnDeath += HandleSpawnedEnemyDeath;
            _aliveEnemies.Add(spawnedEnemy);
            _enemySpeciesKey[spawnedEnemy] = runtimeEntry.SpeciesKey;
            IncrementAliveCount(runtimeEntry.SpeciesKey);

            NotifyRemainingEnemyCountChanged();
        }

        private Transform ResolveSpawnPoint()
        {
            if (_spawnPoints == null || _spawnPoints.Count <= 0)
            {
                return transform;
            }

            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                int index = (_spawnPointCursor + i) % _spawnPoints.Count;
                Transform spawnPoint = _spawnPoints[index];
                if (spawnPoint == null)
                {
                    continue;
                }

                _spawnPointCursor = (index + 1) % _spawnPoints.Count;
                return spawnPoint;
            }

            return transform;
        }

        private void TryCollectSpawnPointsFromChildren()
        {
            if (!_collectSpawnPointsFromChildren)
            {
                return;
            }

            _spawnPoints.Clear();
            EnemySpawnPoint[] spawnPointComponents = GetComponentsInChildren<EnemySpawnPoint>(true);
            for (int i = 0; i < spawnPointComponents.Length; i++)
            {
                EnemySpawnPoint spawnPoint = spawnPointComponents[i];
                if (spawnPoint == null)
                {
                    continue;
                }

                _spawnPoints.Add(spawnPoint.transform);
            }
        }

        private float ResolveSpawnInterval()
        {
            WaveDefinition currentWave = GetCurrentWave();
            if (currentWave == null)
            {
                return Mathf.Max(0.01f, _defaultSpawnIntervalSeconds);
            }

            return currentWave.SpawnIntervalSeconds;
        }

        private int ResolveMaxAliveEnemies()
        {
            WaveDefinition currentWave = GetCurrentWave();
            if (currentWave == null)
            {
                return Mathf.Max(1, _defaultMaxAliveEnemies);
            }

            return currentWave.MaxAliveEnemies;
        }

        private void HandleSpawnedEnemyDeath(EnemyHealth enemyHealth)
        {
            if (enemyHealth == null)
            {
                return;
            }

            enemyHealth.OnDeath -= HandleSpawnedEnemyDeath;
            _aliveEnemies.Remove(enemyHealth);

            if (_enemySpeciesKey.TryGetValue(enemyHealth, out object speciesKey))
            {
                _enemySpeciesKey.Remove(enemyHealth);
                DecrementAliveCount(speciesKey);
            }

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(enemyHealth.gameObject);
            }
            else
            {
                Destroy(enemyHealth.gameObject);
            }

            NotifyRemainingEnemyCountChanged();
        }

        private int GetAliveCount(object speciesKey)
        {
            if (speciesKey == null)
            {
                return 0;
            }

            return _aliveBySpecies.TryGetValue(speciesKey, out int count) ? count : 0;
        }

        private void IncrementAliveCount(object speciesKey)
        {
            if (speciesKey == null)
            {
                return;
            }

            _aliveBySpecies.TryGetValue(speciesKey, out int count);
            _aliveBySpecies[speciesKey] = count + 1;
        }

        private void DecrementAliveCount(object speciesKey)
        {
            if (speciesKey == null)
            {
                return;
            }

            if (_aliveBySpecies.TryGetValue(speciesKey, out int count))
            {
                count--;
                if (count <= 0)
                {
                    _aliveBySpecies.Remove(speciesKey);
                }
                else
                {
                    _aliveBySpecies[speciesKey] = count;
                }
            }
        }

        private void CleanupAliveEnemies(bool despawnObjects)
        {
            _cleanupBuffer.Clear();
            foreach (EnemyHealth enemy in _aliveEnemies)
            {
                _cleanupBuffer.Add(enemy);
            }

            for (int i = 0; i < _cleanupBuffer.Count; i++)
            {
                EnemyHealth enemy = _cleanupBuffer[i];
                if (enemy == null)
                {
                    continue;
                }

                enemy.OnDeath -= HandleSpawnedEnemyDeath;

                if (despawnObjects)
                {
                    if (PoolManager.Instance != null)
                    {
                        PoolManager.Instance.Release(enemy.gameObject);
                    }
                    else
                    {
                        Destroy(enemy.gameObject);
                    }
                }
            }

            _aliveEnemies.Clear();
            _enemySpeciesKey.Clear();
            _aliveBySpecies.Clear();
        }

        private void ResetWaveRuntime()
        {
            _waveElapsedSeconds = 0f;
            _spawnElapsedSeconds = 0f;
            _spawnRuntime.Clear();
            NotifyRemainingEnemyCountChanged();
        }

        private void ResetMiniBossMarks()
        {
            _miniBossMarkFired.Clear();
            int count = _miniBossMinuteMarks != null ? _miniBossMinuteMarks.Count : 0;
            for (int i = 0; i < count; i++)
            {
                _miniBossMarkFired.Add(false);
            }
        }

        private void NotifyRemainingEnemyCountChanged()
        {
            OnRemainingEnemyCountChanged?.Invoke(CurrentWaveNumber, RemainingEnemyCount);
        }
    }
}
