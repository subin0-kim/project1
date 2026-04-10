using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 창귀 — 플레이어(Vector3.zero)를 향해 직선 돌진하는 암살자형 요괴.
    /// DirectChase 이동 패턴을 사용하며, 접근 시 접촉 데미지 후 MoveState로 복귀합니다.
    /// </summary>
    public class ChanggwiEnemy : EnemyBase
    {
        [SerializeField, Min(0f)]
        private float _contactRange = 0.5f;

        [SerializeField, Min(0f)]
        private float _contactDamage = 10f;

        [SerializeField, Min(0f)]
        private float _damageCooldown = 1f;

        private float _lastDamageTime = float.MinValue;
        private PlayerHealth _playerHealth;

        protected override void Awake()
        {
            base.Awake();
            _playerHealth = FindObjectOfType<PlayerHealth>();
        }

        /// <summary>
        /// 플레이어(Vector3.zero) 방향으로 직선 이동합니다.
        /// 분리 반발 벡터를 이동 방향에 합산하여 주변 적과 겹치지 않도록 합니다.
        /// 접촉 범위 진입 시 ActionState로 전환합니다.
        /// </summary>
        protected override void UpdateMovement()
        {
            Vector2 toPlayer = (Vector2)Vector3.zero - (Vector2)transform.position;
            float distanceToPlayer = toPlayer.magnitude;

            bool cooldownReady = Time.time - _lastDamageTime >= _damageCooldown;
            if (distanceToPlayer <= _contactRange && cooldownReady)
            {
                _stateMachine.ChangeState(_actionState);
                return;
            }

            Vector2 chaseDir = toPlayer.normalized;
            Vector2 separation = ComputeSeparation(ActiveEnemies);
            Vector2 finalDir = (chaseDir + separation).normalized;

            _rigidbody.velocity = finalDir * _data.MoveSpeed;
        }

        /// <summary>
        /// 플레이어에게 접촉 데미지를 1회 적용한 뒤 즉시 MoveState로 복귀합니다.
        /// </summary>
        protected override void OnTriggerAction()
        {
            _lastDamageTime = Time.time;

            if (_playerHealth != null && _playerHealth.IsAlive)
                _playerHealth.TakeDamage(_contactDamage, this);

            _stateMachine.ChangeState(_moveState);
        }
    }
}
