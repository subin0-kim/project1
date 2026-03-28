using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public class WaveCombatDirector : MonoBehaviour
    {
        private sealed class SpawnRuntimeEntry
        {
            public WaveEnemySpawnEntry Entry;
            public int Remaining;
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

        [SerializeField, Min(0f)]
        private float _interWaveDelaySeconds = 1.5f;

        [SerializeField]
        private bool _loopWaves;

        [SerializeField]
        private bool _despawnActiveEnemiesOnTimeExpired = true;

        [Header("Fallback")]
        [SerializeField, Min(0.01f)]
        private float _defaultSpawnIntervalSeconds = 1f;

        [SerializeField, Min(1)]
        private int _defaultMaxAliveEnemies = 5;

        [Header("Debug")]
        [SerializeField]
        private bool _showDebugLogs;

        private readonly List<SpawnRuntimeEntry> _spawnRuntime = new List<SpawnRuntimeEntry>();
        private readonly HashSet<EnemyHealth> _aliveEnemies = new HashSet<EnemyHealth>();
        private readonly List<EnemyHealth> _cleanupBuffer = new List<EnemyHealth>(64);

        private int _currentWaveIndex = -1;
        private int _remainingToSpawn;
        private int _spawnRuntimeCursor;
        private int _spawnPointCursor;
        private float _waveElapsedSeconds;
        private float _spawnElapsedSeconds;
        private float _nextWaveDelayRemaining;
        private bool _isRunning;
        private bool _isWaveActive;
        private bool _isWaitingNextWave;

        public int CurrentWaveNumber => _currentWaveIndex + 1;
        public int RemainingEnemyCount => Mathf.Max(0, _remainingToSpawn + _aliveEnemies.Count);
        public bool IsWaveActive => _isWaveActive;
        public bool IsRunning => _isRunning;

        public event Action<int, WaveDefinition> OnWaveStarted;
        public event Action<int, WaveEndReason> OnWaveEnded;
        public event Action<int, int> OnRemainingEnemyCountChanged;
        public event Action OnAllWavesCompleted;

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
            _currentWaveIndex = -1;
            _isWaitingNextWave = false;
            _nextWaveDelayRemaining = 0f;

            BeginNextWave();
        }

        public void StopWaves(bool publishCancellation = true)
        {
            if (!_isRunning && !_isWaveActive && !_isWaitingNextWave)
            {
                return;
            }

            if (publishCancellation && _isWaveActive)
            {
                int waveNumber = CurrentWaveNumber;
                OnWaveEnded?.Invoke(waveNumber, WaveEndReason.Cancelled);
            }

            ResetWaveRuntime();
            CleanupAliveEnemies(false);
            _isWaveActive = false;
            _isWaitingNextWave = false;
            _isRunning = false;
            _currentWaveIndex = -1;
        }

        internal void Tick(float deltaTime)
        {
            if (!_isRunning)
            {
                return;
            }

            float step = Mathf.Max(0f, deltaTime);

            if (_isWaitingNextWave)
            {
                _nextWaveDelayRemaining -= step;
                if (_nextWaveDelayRemaining <= 0f)
                {
                    _isWaitingNextWave = false;
                    BeginNextWave();
                }

                return;
            }

            if (!_isWaveActive)
            {
                return;
            }

            _waveElapsedSeconds += step;
            _spawnElapsedSeconds += step;

            TrySpawnEnemies();

            if (_remainingToSpawn <= 0 && _aliveEnemies.Count <= 0)
            {
                FinishCurrentWave(WaveEndReason.EnemiesCleared);
                return;
            }

            WaveDefinition currentWave = GetCurrentWave();
            if (currentWave != null && currentWave.DurationSeconds > 0f && _waveElapsedSeconds >= currentWave.DurationSeconds)
            {
                FinishCurrentWave(WaveEndReason.TimeExpired);
            }
        }

        private void BeginNextWave()
        {
            if (_waveDatabase == null || _waveDatabase.Waves == null || _waveDatabase.Waves.Count == 0)
            {
                _isRunning = false;
                return;
            }

            int nextWaveIndex = _currentWaveIndex + 1;
            if (nextWaveIndex >= _waveDatabase.Waves.Count)
            {
                if (_loopWaves)
                {
                    nextWaveIndex = 0;
                }
                else
                {
                    _isRunning = false;
                    _isWaveActive = false;
                    OnAllWavesCompleted?.Invoke();
                    return;
                }
            }

            ResetWaveRuntime();
            CleanupAliveEnemies(false);

            _currentWaveIndex = nextWaveIndex;
            _isWaveActive = true;

            WaveDefinition currentWave = GetCurrentWave();
            BuildSpawnRuntime(currentWave);
            NotifyRemainingEnemyCountChanged();
            OnWaveStarted?.Invoke(CurrentWaveNumber, currentWave);

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[WaveCombatDirector] Wave {CurrentWaveNumber} started. SpawnCount={_remainingToSpawn}");
            }
#endif

            if (_remainingToSpawn <= 0 && _aliveEnemies.Count <= 0)
            {
                FinishCurrentWave(WaveEndReason.EnemiesCleared);
            }
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
            _spawnRuntimeCursor = 0;
            _spawnPointCursor = 0;
            _remainingToSpawn = 0;

            if (wave == null || wave.Enemies == null)
            {
                return;
            }

            for (int i = 0; i < wave.Enemies.Count; i++)
            {
                WaveEnemySpawnEntry entry = wave.Enemies[i];
                if (entry == null || entry.EnemyPrefab == null || entry.Count <= 0)
                {
                    continue;
                }

                _spawnRuntime.Add(new SpawnRuntimeEntry
                {
                    Entry = entry,
                    Remaining = entry.Count
                });

                _remainingToSpawn += entry.Count;
            }
        }

        private void TrySpawnEnemies()
        {
            if (_spawnRuntime.Count <= 0 || _remainingToSpawn <= 0)
            {
                return;
            }

            float spawnInterval = ResolveSpawnInterval();
            int maxAliveEnemies = ResolveMaxAliveEnemies();

            while (_spawnElapsedSeconds >= spawnInterval && _remainingToSpawn > 0 && _aliveEnemies.Count < maxAliveEnemies)
            {
                _spawnElapsedSeconds -= spawnInterval;

                if (!TrySpawnOneEnemy())
                {
                    break;
                }
            }
        }

        private bool TrySpawnOneEnemy()
        {
            if (_spawnRuntime.Count <= 0)
            {
                return false;
            }

            for (int i = 0; i < _spawnRuntime.Count; i++)
            {
                int index = (_spawnRuntimeCursor + i) % _spawnRuntime.Count;
                SpawnRuntimeEntry runtimeEntry = _spawnRuntime[index];
                if (runtimeEntry.Remaining <= 0)
                {
                    continue;
                }

                Transform spawnPoint = ResolveSpawnPoint();
                EnemyHealth spawnedEnemy = Instantiate(
                    runtimeEntry.Entry.EnemyPrefab,
                    spawnPoint.position,
                    spawnPoint.rotation);

                spawnedEnemy.SetMoveSpeed(runtimeEntry.Entry.MoveSpeed);
                spawnedEnemy.OnDeath += HandleSpawnedEnemyDeath;
                _aliveEnemies.Add(spawnedEnemy);

                runtimeEntry.Remaining--;
                _remainingToSpawn--;
                _spawnRuntimeCursor = (index + 1) % _spawnRuntime.Count;

                NotifyRemainingEnemyCountChanged();
                return true;
            }

            return false;
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

            return Mathf.Max(0.01f, currentWave.SpawnIntervalSeconds);
        }

        private int ResolveMaxAliveEnemies()
        {
            WaveDefinition currentWave = GetCurrentWave();
            if (currentWave == null)
            {
                return Mathf.Max(1, _defaultMaxAliveEnemies);
            }

            return Mathf.Max(1, currentWave.MaxAliveEnemies);
        }

        private void HandleSpawnedEnemyDeath(EnemyHealth enemyHealth)
        {
            if (enemyHealth == null)
            {
                return;
            }

            enemyHealth.OnDeath -= HandleSpawnedEnemyDeath;
            _aliveEnemies.Remove(enemyHealth);
            NotifyRemainingEnemyCountChanged();
        }

        private void FinishCurrentWave(WaveEndReason endReason)
        {
            int waveNumber = CurrentWaveNumber;

            _isWaveActive = false;

            if (endReason == WaveEndReason.TimeExpired && _despawnActiveEnemiesOnTimeExpired)
            {
                CleanupAliveEnemies(true);
                NotifyRemainingEnemyCountChanged();
            }

            OnWaveEnded?.Invoke(waveNumber, endReason);

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[WaveCombatDirector] Wave {waveNumber} ended. Reason={endReason}");
            }
#endif

            _isWaitingNextWave = true;
            _nextWaveDelayRemaining = Mathf.Max(0f, _interWaveDelaySeconds);

            if (_nextWaveDelayRemaining <= 0f)
            {
                _isWaitingNextWave = false;
                BeginNextWave();
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
                    Destroy(enemy.gameObject);
                }
            }

            _aliveEnemies.Clear();
        }

        private void ResetWaveRuntime()
        {
            _waveElapsedSeconds = 0f;
            _spawnElapsedSeconds = 0f;
            _remainingToSpawn = 0;
            _spawnRuntimeCursor = 0;
            _spawnRuntime.Clear();
            NotifyRemainingEnemyCountChanged();
        }

        private void NotifyRemainingEnemyCountChanged()
        {
            OnRemainingEnemyCountChanged?.Invoke(CurrentWaveNumber, RemainingEnemyCount);
        }
    }
}
