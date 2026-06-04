using System.Collections.Generic;
using Mukseon.Core.Pool;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 목귀 적 컴포넌트.
    /// 화면 내를 자유롭게 배회하며 쿨타임마다 나무뿌리 장애물을 소환한다.
    /// 뿌리는 스와이프 공격을 흡수해 다른 적을 보호하는 동선 방해 기믹이다.
    /// 처치 시 자신이 소환한 모든 뿌리가 함께 소멸한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public class MokgwiEnemy : MonoBehaviour
    {
        [Header("Wandering")]
        [SerializeField, Min(0.5f)]
        private float _playerAvoidRadius = 2f;

        [SerializeField, Min(0.05f)]
        private float _waypointReachDistance = 0.2f;

        [Header("Root Spawning")]
        [SerializeField]
        private GameObject _rootPrefab;

        [SerializeField, Min(0.1f)]
        private float _rootSpawnInterval = 3f;

        [SerializeField, Min(1)]
        private int _maxRootsOnMap = 5;

        [SerializeField, Min(0.5f)]
        private float _minRootDistanceFromBody = 1.5f;

        private EnemyHealth _enemyHealth;
        private Transform _playerTarget;
        private Vector3 _wanderTarget;
        private float _spawnTimer;
        private readonly List<EnemyHealth> _spawnedRoots = new List<EnemyHealth>();

        private void Awake()
        {
            _enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            _enemyHealth.OnDied += HandleDied;
            _spawnedRoots.Clear();
            _spawnTimer = _rootSpawnInterval;

            if (_playerTarget == null)
            {
                PlayerHealth ph = FindAnyObjectByType<PlayerHealth>();
                if (ph != null)
                    _playerTarget = ph.transform;
            }

            _wanderTarget = PickWanderTarget();
        }

        private void OnDisable()
        {
            _enemyHealth.OnDied -= HandleDied;
            KillSpawnedRoots();
        }

        private void Update()
        {
            if (!_enemyHealth.IsAlive)
                return;

            UpdateMovement();
            UpdateSpawnTimer();
        }

        private void UpdateMovement()
        {
            float step = _enemyHealth.MoveSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, _wanderTarget, step);

            if (Vector2.Distance(transform.position, _wanderTarget) < _waypointReachDistance)
                _wanderTarget = PickWanderTarget();
        }

        private void UpdateSpawnTimer()
        {
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                TrySpawnRoot();
                _spawnTimer = _rootSpawnInterval;
            }
        }

        private void TrySpawnRoot()
        {
            if (_rootPrefab == null || PoolManager.Instance == null)
                return;

            if (MokgwiRoot.ActiveRootCount >= _maxRootsOnMap)
                return;

            Vector3 spawnPos = PickRootSpawnPosition();
            GameObject rootObj = PoolManager.Instance.GetInactive(
                _rootPrefab, spawnPos, Quaternion.identity);

            EnemyHealth rootHealth = rootObj.GetComponent<EnemyHealth>();
            if (rootHealth != null)
            {
                rootHealth.PrepareForReuse();
                rootHealth.OnDeath += HandleRootDied;
                _spawnedRoots.Add(rootHealth);
            }

            rootObj.SetActive(true);
        }

        private void HandleDied()
        {
            KillSpawnedRoots();
        }

        private void HandleRootDied(EnemyHealth rootHealth)
        {
            rootHealth.OnDeath -= HandleRootDied;
            _spawnedRoots.Remove(rootHealth);
        }

        private void KillSpawnedRoots()
        {
            for (int i = _spawnedRoots.Count - 1; i >= 0; i--)
            {
                EnemyHealth rootHealth = _spawnedRoots[i];
                if (rootHealth != null && rootHealth.IsAlive)
                {
                    rootHealth.OnDeath -= HandleRootDied;
                    rootHealth.Kill(countAsKill: false);
                }
            }
            _spawnedRoots.Clear();
        }

        private Vector3 PickWanderTarget()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return transform.position;

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 camPos = cam.transform.position;

            for (int i = 0; i < 10; i++)
            {
                float x = Random.Range(camPos.x - halfW * 0.85f, camPos.x + halfW * 0.85f);
                float y = Random.Range(camPos.y - halfH * 0.85f, camPos.y + halfH * 0.85f);
                Vector3 candidate = new Vector3(x, y, 0f);

                if (_playerTarget == null ||
                    Vector2.Distance(candidate, _playerTarget.position) >= _playerAvoidRadius)
                {
                    return candidate;
                }
            }

            // 10회 시도 실패 시 현재 위치 유지 (플레이어가 화면 대부분을 점유하는 극단적 케이스)
            return transform.position;
        }

        private Vector3 PickRootSpawnPosition()
        {
            Camera cam = Camera.main;
            Vector3 fallback = transform.position +
                (Vector3)(Random.insideUnitCircle.normalized * _minRootDistanceFromBody * 2f);

            if (cam == null)
                return fallback;

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 camPos = cam.transform.position;

            for (int i = 0; i < 10; i++)
            {
                float x = Random.Range(camPos.x - halfW * 0.8f, camPos.x + halfW * 0.8f);
                float y = Random.Range(camPos.y - halfH * 0.8f, camPos.y + halfH * 0.8f);
                Vector3 candidate = new Vector3(x, y, 0f);

                if (Vector2.Distance(candidate, transform.position) >= _minRootDistanceFromBody)
                    return candidate;
            }

            return fallback;
        }
    }
}
