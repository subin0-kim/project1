using System.Collections.Generic;
using Mukseon.Core.Pool;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 목귀 적 컴포넌트.
    /// 땅속을 이동하며 나무뿌리 장애물을 설치하는 기믹형 적.
    /// 이동 중에는 잠복(무적·비타겟)이고, 지표에 멈출 때만 공격 가능하다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public class MokgwiEnemy : MonoBehaviour
    {
        private enum State
        {
            EnteringScreen,    // 화면 밖 스폰 위치 → 가장자리 진입 (잠복·무적)
            WaitingAtEdge,     // 가장자리 정지 — 공격 가능
            MovingUnderground, // 화면 내 이동 (잠복·무적, 이동 경로에 뿌리 소환)
            Surfaced,          // 목표 지점 정지 — 공격 가능
        }

        [Header("Movement")]
        [SerializeField, Min(0f)]
        private float _waitDuration = 1.5f;

        [SerializeField, Min(0.05f)]
        private float _waypointReachDistance = 0.15f;

        [SerializeField, Min(0.5f)]
        private float _playerAvoidRadius = 2f;

        [Header("Root Spawning")]
        [SerializeField]
        private GameObject _rootPrefab;

        [SerializeField, Min(0.1f)]
        private float _rootSpawnInterval = 1f;

        [SerializeField, Min(1)]
        private int _maxRootsOnMap = 5;

        private EnemyHealth _enemyHealth;
        private Transform _playerTarget;
        private Vector3 _moveTarget;
        private State _state;
        private float _stateTimer;
        private float _rootSpawnTimer;
        private readonly List<EnemyHealth> _spawnedRoots = new List<EnemyHealth>();

        private void Awake()
        {
            _enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            _enemyHealth.OnDied += HandleDied;
            _spawnedRoots.Clear();

            if (_playerTarget == null)
            {
                PlayerHealth ph = FindAnyObjectByType<PlayerHealth>();
                if (ph != null)
                    _playerTarget = ph.transform;
            }

            EnterState(State.EnteringScreen);
        }

        private void OnDisable()
        {
            _enemyHealth.OnDied -= HandleDied;
            _enemyHealth.IsTargetable = true;
            KillSpawnedRoots();
        }

        private void Update()
        {
            if (!_enemyHealth.IsAlive)
                return;

            _stateTimer += Time.deltaTime;

            switch (_state)
            {
                case State.EnteringScreen:
                    MoveToward(_moveTarget);
                    if (Vector2.Distance(transform.position, _moveTarget) < _waypointReachDistance)
                        EnterState(State.WaitingAtEdge);
                    break;

                case State.WaitingAtEdge:
                    if (_stateTimer >= _waitDuration)
                        EnterState(State.MovingUnderground);
                    break;

                case State.MovingUnderground:
                    MoveToward(_moveTarget);
                    UpdateRootSpawn();
                    if (Vector2.Distance(transform.position, _moveTarget) < _waypointReachDistance)
                        EnterState(State.Surfaced);
                    break;

                case State.Surfaced:
                    if (_stateTimer >= _waitDuration)
                        EnterState(State.MovingUnderground);
                    break;
            }
        }

        private void EnterState(State next)
        {
            _state = next;
            _stateTimer = 0f;

            switch (next)
            {
                case State.EnteringScreen:
                    _enemyHealth.IsTargetable = false;
                    _moveTarget = PickScreenEdgeTarget();
                    break;

                case State.WaitingAtEdge:
                    _enemyHealth.IsTargetable = true;
                    break;

                case State.MovingUnderground:
                    _enemyHealth.IsTargetable = false;
                    _rootSpawnTimer = 0f; // 이동 시작 즉시 첫 뿌리 소환
                    _moveTarget = PickInsideTarget();
                    break;

                case State.Surfaced:
                    _enemyHealth.IsTargetable = true;
                    break;
            }
        }

        private void MoveToward(Vector3 target)
        {
            float step = _enemyHealth.MoveSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, target, step);
        }

        private void UpdateRootSpawn()
        {
            _rootSpawnTimer -= Time.deltaTime;
            if (_rootSpawnTimer <= 0f)
            {
                TrySpawnRoot();
                _rootSpawnTimer = _rootSpawnInterval;
            }
        }

        private void TrySpawnRoot()
        {
            if (_rootPrefab == null || PoolManager.Instance == null)
                return;

            if (MokgwiRoot.ActiveRootCount >= _maxRootsOnMap)
                return;

            GameObject rootObj = PoolManager.Instance.GetInactive(
                _rootPrefab, transform.position, Quaternion.identity);

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

        private Vector3 PickScreenEdgeTarget()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return transform.position;

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 camPos = cam.transform.position;

            int side = Random.Range(0, 4);
            switch (side)
            {
                case 0: return new Vector3(Random.Range(camPos.x - halfW * 0.8f, camPos.x + halfW * 0.8f), camPos.y + halfH * 0.85f, 0f); // 상
                case 1: return new Vector3(Random.Range(camPos.x - halfW * 0.8f, camPos.x + halfW * 0.8f), camPos.y - halfH * 0.85f, 0f); // 하
                case 2: return new Vector3(camPos.x - halfW * 0.85f, Random.Range(camPos.y - halfH * 0.8f, camPos.y + halfH * 0.8f), 0f); // 좌
                default: return new Vector3(camPos.x + halfW * 0.85f, Random.Range(camPos.y - halfH * 0.8f, camPos.y + halfH * 0.8f), 0f); // 우
            }
        }

        private Vector3 PickInsideTarget()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return transform.position;

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 camPos = cam.transform.position;

            for (int i = 0; i < 10; i++)
            {
                float x = Random.Range(camPos.x - halfW * 0.7f, camPos.x + halfW * 0.7f);
                float y = Random.Range(camPos.y - halfH * 0.7f, camPos.y + halfH * 0.7f);
                Vector3 candidate = new Vector3(x, y, 0f);

                if (_playerTarget == null ||
                    Vector2.Distance(candidate, _playerTarget.position) >= _playerAvoidRadius)
                {
                    return candidate;
                }
            }

            return transform.position;
        }
    }
}
