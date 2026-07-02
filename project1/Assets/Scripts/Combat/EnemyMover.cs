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
        /// <summary>매구: 플레이어와 일정 거리를 유지하며 이동</summary>
        KeepDistance,
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

        [SerializeField, Min(0.1f)]
        private float _keepDistance = 5f;

        private EnemyHealth _enemyHealth;
        private Vector3 _spawnPosition;

        // 프레임 단위 슬로우 팩터(1 = 정상). 끈적한 묵액(#75) 등 외부 효과가 매 프레임 요청하며,
        // Tick에서 소비 후 1로 리셋한다. 매 프레임 가장 강한 슬로우(가장 작은 값)만 반영된다.
        private float _slowThisFrame = 1f;

        // 기절(Stun) 남은 시간(초). 0보다 크면 이동을 완전히 정지한다. 강신 파천의 징(#30) 등이 부여한다.
        // 슬로우와 달리 지속 시간 동안 상태가 유지되며, Tick에서 deltaTime만큼 감소한다.
        private float _stunTimer;

        public EnemyMovePattern MovePattern => _movePattern;

        /// <summary>기절 상태 여부. 기절 중에는 이동을 정지한다.</summary>
        public bool IsStunned => _stunTimer > 0f;

        private void Awake()
        {
            _enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            // 풀 재사용 가드: 슬로우/기절 상태로 사망하면 Tick의 조기 반환으로 리셋이 누락되므로 여기서 초기화한다.
            _slowThisFrame = 1f;
            _stunTimer = 0f;
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

            // 기절 중에는 타이머만 감소시키고 이동은 정지한다. 슬로우 요청은 이번 프레임에 소비된 것으로 처리한다.
            if (_stunTimer > 0f)
            {
                _stunTimer = Mathf.Max(0f, _stunTimer - Mathf.Max(0f, deltaTime));
                _slowThisFrame = 1f;
                return;
            }

            float step = _enemyHealth.MoveSpeed * Mathf.Max(0f, deltaTime) * _slowThisFrame;

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
                case EnemyMovePattern.KeepDistance:
                    MoveKeepDistance(step);
                    break;
            }

            // 이번 프레임 슬로우 요청을 소비했으므로 리셋. 자국을 벗어나면 다음 프레임부터 정상 속도로 복원된다.
            _slowThisFrame = 1f;
        }

        /// <summary>
        /// 이번 프레임 이동 속도에 곱할 슬로우 배수(0~1)를 요청한다. 여러 요청 중 가장 강한(작은) 값이 적용된다.
        /// 끈적한 묵액(#75) 자국이 매 프레임 호출한다. Tick에서 소비 후 1로 리셋된다.
        /// </summary>
        public void RequestSlow(float multiplier)
        {
            _slowThisFrame = Mathf.Min(_slowThisFrame, Mathf.Clamp01(multiplier));
        }

        /// <summary>
        /// 기절(Stun)을 부여한다. 지속 시간 동안 이동이 정지된다(강신 파천의 징 #30).
        /// 이미 기절 중이면 남은 시간과 요청 시간 중 더 긴 값을 사용해 기절이 짧아지지 않게 한다.
        /// </summary>
        public void ApplyStun(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            _stunTimer = Mathf.Max(_stunTimer, duration);
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

        /// <summary>플레이어와 _keepDistance 거리를 유지하며 이동 (매구)</summary>
        private void MoveKeepDistance(float step)
        {
            if (_playerTarget == null)
            {
                return;
            }

            Vector3 toEnemy = transform.position - _playerTarget.position;
            float dist = toEnemy.magnitude;

            if (dist > _keepDistance + 0.1f)
            {
                transform.position = Vector3.MoveTowards(transform.position, _playerTarget.position, step);
            }
            else if (dist < _keepDistance - 0.1f)
            {
                Vector3 awayDir = dist > 0.001f ? toEnemy.normalized : Vector3.up;
                transform.position += awayDir * step;
            }
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
