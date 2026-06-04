using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 도깨비불 적 컴포넌트.
    /// 플레이어를 향해 직선 이동하다가 일정 거리 내 진입 시 팽창 → 폭발한다.
    /// 팽창 중 스와이프로 처치하면 폭발이 취소된다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public class DokkaebibulEnemy : MonoBehaviour
    {
        private enum State { Idle, Expanding, Exploding }

        [Header("Trigger")]
        [SerializeField, Min(0f)]
        private float _triggerDistance = 2f;

        [Header("Explosion")]
        [SerializeField, Min(0.1f)]
        private float _expandDuration = 1.5f;

        [SerializeField, Min(0f)]
        private float _explodeDamage = 20f;

        [SerializeField, Min(0.1f)]
        private float _explodRadius = 3f;

        [Header("Visual")]
        [SerializeField, Min(1f)]
        private float _expandScale = 2f;

        private EnemyHealth _enemyHealth;
        private Transform _playerTarget;
        private State _state;
        private float _expandTimer;
        private Vector3 _originalScale;

        private void Awake()
        {
            _enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            _state = State.Idle;
            _expandTimer = 0f;
            _originalScale = transform.localScale;

            _enemyHealth.OnDied += HandleDied;

            if (_playerTarget == null)
            {
                PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
                if (playerHealth != null)
                {
                    _playerTarget = playerHealth.transform;
                }
            }
        }

        private void OnDisable()
        {
            _enemyHealth.OnDied -= HandleDied;
            _state = State.Idle;
            transform.localScale = _originalScale;
        }

        private void Update()
        {
            if (!_enemyHealth.IsAlive)
            {
                return;
            }

            switch (_state)
            {
                case State.Idle:
                    UpdateIdle(Time.deltaTime);
                    break;
                case State.Expanding:
                    UpdateExpanding(Time.deltaTime);
                    break;
            }
        }

        private void UpdateIdle(float dt)
        {
            if (_playerTarget == null)
            {
                return;
            }

            float step = _enemyHealth.MoveSpeed * dt;
            transform.position = Vector3.MoveTowards(
                transform.position, _playerTarget.position, step);

            if (Vector2.Distance(transform.position, _playerTarget.position) <= _triggerDistance)
            {
                EnterExpanding();
            }
        }

        private void EnterExpanding()
        {
            _state = State.Expanding;
            _expandTimer = 0f;
        }

        private void UpdateExpanding(float dt)
        {
            _expandTimer += dt;
            float t = Mathf.Clamp01(_expandTimer / _expandDuration);
            transform.localScale = _originalScale * Mathf.Lerp(1f, _expandScale, t);

            if (_expandTimer >= _expandDuration)
            {
                Explode();
            }
        }

        private void Explode()
        {
            _state = State.Exploding;

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _explodRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                PlayerHealth player = hits[i].GetComponent<PlayerHealth>()
                    ?? hits[i].GetComponentInParent<PlayerHealth>();
                if (player != null && player.IsAlive)
                {
                    player.TakeDamage(_explodeDamage, _enemyHealth);
                    break;
                }
            }

            // 폭발은 처치 보상 없음 — 스와이프 처치가 아니므로
            _enemyHealth.Kill(countAsKill: false);
        }

        // 스와이프 처치 등 외부 처치 시 스케일 원복
        private void HandleDied()
        {
            transform.localScale = _originalScale;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _triggerDistance);

            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _explodRadius);
        }
#endif
    }
}
