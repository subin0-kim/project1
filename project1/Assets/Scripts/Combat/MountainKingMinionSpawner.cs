using System;
using System.Collections.Generic;
using Mukseon.Core.Pool;
using Mukseon.Gameplay.Progression;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 타락한 산군의 포효(#69)가 소환하는 부하 호랑이 스포너. 부하는 일반 잡몹과 동일한 방향 속성 시스템을
    /// 따르며(<see cref="EnemyHealth.PrepareForReuse"/> → 비보스 랜덤 방향) 오브젝트 풀링으로 관리한다.
    /// 소환 규칙: 보스가 화면 우측이면 우측 3개(상/중/하) 중 랜덤 1개, 좌측이면 좌측 3개 중 랜덤 1개
    /// 포인트에서 전원 몰아서 등장. 스폰/풀링 구조는 <see cref="MiniBossSpawner"/>를 따른다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MountainKingMinionSpawner : MonoBehaviour
    {
        [Header("Minion")]
        [Tooltip("부하 호랑이 전용 MonsterData. 프리팹은 MonsterData.EnemyPrefab을 사용한다.")]
        [SerializeField]
        private MonsterData _minionMonsterData;

        [Tooltip("MonsterData.EnemyPrefab이 비었을 때 사용할 폴백 프리팹.")]
        [SerializeField]
        private EnemyHealth _minionPrefabFallback;

        [Header("Spawn Points")]
        [Tooltip("화면 왼쪽 밖 스폰 포인트 (상/중/하 권장).")]
        [SerializeField]
        private List<Transform> _leftSpawnPoints = new List<Transform>();

        [Tooltip("화면 오른쪽 밖 스폰 포인트 (상/중/하 권장).")]
        [SerializeField]
        private List<Transform> _rightSpawnPoints = new List<Transform>();

        [Header("References")]
        [Tooltip("좌/우 판정 기준 카메라. 비우면 Camera.main을 사용한다.")]
        [SerializeField]
        private Camera _camera;

        [Header("Debug")]
        [SerializeField]
        private bool _showDebugLogs;

        private readonly HashSet<EnemyHealth> _aliveMinions = new HashSet<EnemyHealth>();
        private readonly List<EnemyHealth> _cleanupBuffer = new List<EnemyHealth>(8);

        /// <summary>이번 포효로 소환된 부하가 한 마리 이상 살아있는 동안 true. 1페이즈 패턴 게이팅에 사용.</summary>
        public bool HasActiveMinions => _aliveMinions.Count > 0;
        public int AliveMinionCount => _aliveMinions.Count;

        /// <summary>소환된 부하가 모두 처치되어 전멸한 순간 1회 발행. 컨트롤러가 P1 패턴 재개에 구독.</summary>
        public event Action OnAllMinionsCleared;

        private void OnDestroy()
        {
            UnsubscribeAll();
        }

        /// <summary>
        /// 보스 위치를 기준으로 같은 방향(좌/우)의 스폰 포인트 1개에서 부하 <paramref name="count"/>마리를 소환한다.
        /// 실제로 소환된 마릿수를 반환한다(유효 포인트/프리팹이 없으면 0).
        /// </summary>
        public int SpawnWave(Vector3 bossPosition, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            bool onRight = IsBossOnRight(bossPosition.x, ResolveCameraCenterX());
            Transform point = PickSpawnPoint(onRight);
            if (point == null)
            {
                Debug.LogWarning($"[MountainKingMinionSpawner] {(onRight ? "오른쪽" : "왼쪽")} 스폰 포인트가 없어 부하를 소환할 수 없습니다.", this);
                return 0;
            }

            GameObject prefabGO = ResolveMinionPrefabGO();
            if (prefabGO == null)
            {
                Debug.LogWarning("[MountainKingMinionSpawner] 부하 프리팹이 비어 있어 소환할 수 없습니다.", this);
                return 0;
            }

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                EnemyHealth minion = SpawnOne(prefabGO, point.position);
                if (minion != null)
                {
                    spawned++;
                }
            }

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[MountainKingMinionSpawner] 부하 {spawned}마리 소환 ({(onRight ? "우" : "좌")}측 포인트).");
            }
#endif
            return spawned;
        }

        private EnemyHealth SpawnOne(GameObject prefabGO, Vector3 position)
        {
            GameObject spawnedObj;
            EnemyHealth minion;

            if (PoolManager.Instance != null)
            {
                spawnedObj = PoolManager.Instance.GetInactive(prefabGO, position, Quaternion.identity);
                if (spawnedObj == null)
                {
                    return null;
                }

                minion = spawnedObj.GetComponent<EnemyHealth>();
                if (minion == null)
                {
                    Debug.LogError($"[MountainKingMinionSpawner] {spawnedObj.name}에 EnemyHealth 컴포넌트가 없습니다.", this);
                    return null;
                }

                ApplyMinionData(minion, spawnedObj);
                minion.PrepareForReuse();
                spawnedObj.SetActive(true);
            }
            else
            {
                spawnedObj = Instantiate(prefabGO, position, Quaternion.identity);
                minion = spawnedObj.GetComponent<EnemyHealth>();
                if (minion == null)
                {
                    Debug.LogError($"[MountainKingMinionSpawner] {spawnedObj.name}에 EnemyHealth 컴포넌트가 없습니다.", this);
                    Destroy(spawnedObj);
                    return null;
                }

                ApplyMinionData(minion, spawnedObj);
            }

            Track(minion);
            return minion;
        }

        private void ApplyMinionData(EnemyHealth minion, GameObject go)
        {
            if (_minionMonsterData == null)
            {
                return;
            }

            minion.ApplyMonsterData(_minionMonsterData);

            EnemySoulDropper soulDropper = go.GetComponent<EnemySoulDropper>();
            if (soulDropper != null)
            {
                soulDropper.ApplyMonsterData(_minionMonsterData);
            }
        }

        /// <summary>생존 부하로 추적하고 사망 시 전멸 감지를 위해 구독한다. (테스트에서도 사용)</summary>
        internal void Track(EnemyHealth minion)
        {
            if (minion == null || _aliveMinions.Contains(minion))
            {
                return;
            }

            minion.OnDeath += HandleMinionDeath;
            _aliveMinions.Add(minion);
        }

        private void HandleMinionDeath(EnemyHealth minion)
        {
            if (minion != null)
            {
                minion.OnDeath -= HandleMinionDeath;
            }

            if (!_aliveMinions.Remove(minion))
            {
                return;
            }

            if (_aliveMinions.Count == 0)
            {
                OnAllMinionsCleared?.Invoke();
            }
        }

        private void UnsubscribeAll()
        {
            _cleanupBuffer.Clear();
            foreach (EnemyHealth minion in _aliveMinions)
            {
                if (minion != null)
                {
                    _cleanupBuffer.Add(minion);
                }
            }

            for (int i = 0; i < _cleanupBuffer.Count; i++)
            {
                _cleanupBuffer[i].OnDeath -= HandleMinionDeath;
            }

            _aliveMinions.Clear();
        }

        private GameObject ResolveMinionPrefabGO()
        {
            if (_minionMonsterData != null && _minionMonsterData.EnemyPrefab != null)
            {
                return _minionMonsterData.EnemyPrefab.gameObject;
            }

            return _minionPrefabFallback != null ? _minionPrefabFallback.gameObject : null;
        }

        private Transform PickSpawnPoint(bool onRight)
        {
            List<Transform> points = ResolveSpawnPointsForSide(onRight);
            return PickRandomNonNull(points);
        }

        private float ResolveCameraCenterX()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            return _camera != null ? _camera.transform.position.x : 0f;
        }

        // ── 순수 로직 (테스트 seam) ────────────────────────────────────────

        /// <summary>보스 x좌표가 카메라 중앙보다 오른쪽(이상)이면 true.</summary>
        internal static bool IsBossOnRight(float bossX, float cameraCenterX)
        {
            return bossX >= cameraCenterX;
        }

        internal List<Transform> ResolveSpawnPointsForSide(bool onRight)
        {
            return onRight ? _rightSpawnPoints : _leftSpawnPoints;
        }

        internal static Transform PickRandomNonNull(List<Transform> points)
        {
            if (points == null || points.Count == 0)
            {
                return null;
            }

            // 비어있지 않은(null이 아닌) 포인트만 후보로 모아 랜덤 선택한다.
            int validCount = 0;
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] != null)
                {
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                return null;
            }

            int target = UnityEngine.Random.Range(0, validCount);
            int seen = 0;
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == null)
                {
                    continue;
                }

                if (seen == target)
                {
                    return points[i];
                }

                seen++;
            }

            return null;
        }

        /// <summary>테스트 전용 — 스폰 포인트 리스트를 코드로 주입한다.</summary>
        internal void ConfigureForTests(List<Transform> leftPoints, List<Transform> rightPoints)
        {
            _leftSpawnPoints = leftPoints ?? new List<Transform>();
            _rightSpawnPoints = rightPoints ?? new List<Transform>();
        }
    }
}
