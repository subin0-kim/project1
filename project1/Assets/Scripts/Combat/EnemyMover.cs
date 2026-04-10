using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    public enum EnemyMovePattern
    {
        /// <summary>창귀: 플레이어 위치를 향해 직선 이동</summary>
        TrackPlayer,
        /// <summary>그슨새: 화면 위에서 수직 낙하</summary>
        VerticalDrop,
        /// <summary>목귀: 스폰 지점에서 위쪽으로 솟아오름</summary>
        RiseFromGround,
    }

    /// <summary>
    /// 적 이동 AI. EnemyHealth.MoveSpeed를 읽어 패턴별로 이동을 처리한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyMover : MonoBehaviour
    {
        [SerializeField]
        private EnemyMovePattern _movePattern = EnemyMovePattern.TrackPlayer;

        [SerializeField]
        private Transform _playerTarget;

        [SerializeField, Min(0.1f)]
        private float _riseHeight = 10f;

        private EnemyHealth _enemyHealth;
        private Vector3 _spawnPosition;

        public EnemyMovePattern MovePattern => _movePattern;

        private void Awake()
        {
            _enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            _spawnPosition = transform.position;

            if (_playerTarget == null)
            {
                PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
                if (playerHealth != null)
                {
                    _playerTarget = playerHealth.transform;
                }
            }

#if UNITY_EDITOR
            Debug.Log($"[EnemyMover] {name} enabled at {transform.position}. target={((_playerTarget != null) ? _playerTarget.name : "NULL")} speed={(_enemyHealth != null ? _enemyHealth.MoveSpeed.ToString() : "N/A")}");
#endif
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            if (_enemyHealth == null)
            {
                _enemyHealth = GetComponent<EnemyHealth>();
            }

            if (_enemyHealth == null || !_enemyHealth.IsAlive)
            {
                return;
            }

            float step = _enemyHealth.MoveSpeed * Mathf.Max(0f, deltaTime);

            switch (_movePattern)
            {
                case EnemyMovePattern.TrackPlayer:
                    MoveTowardPlayer(step);
                    break;
                case EnemyMovePattern.VerticalDrop:
                    MoveVerticalDrop(step);
                    break;
                case EnemyMovePattern.RiseFromGround:
                    MoveRiseFromGround(step);
                    break;
            }
        }

        /// <summary>플레이어 방향으로 직선 이동 (창귀)</summary>
        private void MoveTowardPlayer(float step)
        {
            if (_playerTarget == null)
            {
                return;
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                _playerTarget.position,
                step);
        }

        /// <summary>수직으로 낙하 (그슨새)</summary>
        private void MoveVerticalDrop(float step)
        {
            transform.Translate(Vector3.down * step, Space.World);
        }

        /// <summary>스폰 위치에서 위쪽으로 솟아오름 (목귀)</summary>
        private void MoveRiseFromGround(float step)
        {
            Vector3 target = new Vector3(_spawnPosition.x, _spawnPosition.y + _riseHeight, _spawnPosition.z);
            transform.position = Vector3.MoveTowards(transform.position, target, step);
        }

        /// <summary>외부에서 플레이어 타겟을 직접 지정할 때 사용</summary>
        public void SetPlayerTarget(Transform playerTarget)
        {
            _playerTarget = playerTarget;
        }

        /// <summary>이동 패턴을 런타임에 변경</summary>
        public void SetMovePattern(EnemyMovePattern pattern)
        {
            _movePattern = pattern;
        }
    }
}
