using Mukseon.Core.Pool;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 매구가 발사하는 투사체.
    /// EnemyHealth를 통해 스와이프 방향 속성을 가지며, 올바른 방향 스와이프로 패리된다.
    /// 패리되지 않으면 플레이어에게 도달 시 즉시 데미지를 입힌다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public class MaeguProjectile : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        private float _lifetime = 6f;

        private Vector2 _moveDirection;
        private float _moveSpeed;
        private float _damage;
        private float _lifetimeRemaining;
        private bool _launched;
        private EnemyHealth _enemyHealth;

        private void Awake()
        {
            _enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            _enemyHealth.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            _enemyHealth.OnDeath -= HandleDeath;
            _launched = false;
        }

        /// <summary>발사 방향, 스와이프 방향 속성, 속도, 데미지를 설정하고 투사체를 활성화한다.</summary>
        public void Launch(Vector2 direction, Core.Input.SwipeDirection swipeDirection, float speed, float damage)
        {
            _moveDirection = direction.normalized;
            _moveSpeed = speed;
            _damage = damage;
            _lifetimeRemaining = _lifetime;

            _enemyHealth.PrepareForReuse();
            _enemyHealth.SetSwipeDirection(swipeDirection);

            _launched = true;
        }

        private void Update()
        {
            if (!_launched || !_enemyHealth.IsAlive)
            {
                return;
            }

            transform.Translate(_moveDirection * _moveSpeed * Time.deltaTime, Space.World);

            _lifetimeRemaining -= Time.deltaTime;
            if (_lifetimeRemaining <= 0f)
            {
                // 수명 만료 — 처치 보상 없이 회수
                _enemyHealth.Kill(countAsKill: false);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_launched || !_enemyHealth.IsAlive)
            {
                return;
            }

            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                playerHealth = other.GetComponentInParent<PlayerHealth>();
            }

            if (playerHealth == null || !playerHealth.IsAlive)
            {
                return;
            }

            playerHealth.TakeDamage(_damage, _enemyHealth);
            _enemyHealth.Kill(countAsKill: false);
        }

        private void HandleDeath(EnemyHealth _)
        {
            _launched = false;
            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Release(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
