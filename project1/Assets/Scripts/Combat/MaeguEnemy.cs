using Mukseon.Core.Input;
using Mukseon.Core.Pool;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 매구 적 컴포넌트.
    /// EnemyMover(KeepDistance)와 함께 사용하며, 쿨타임마다 방향 속성 투사체를 발사한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public class MaeguEnemy : MonoBehaviour
    {
        [Header("Projectile")]
        [SerializeField]
        private GameObject _projectilePrefab;

        [SerializeField, Min(0.1f)]
        private float _fireInterval = 2f;

        [SerializeField, Min(0f)]
        private float _projectileSpeed = 5f;

        [SerializeField, Min(0f)]
        private float _projectileDamage = 10f;

        private EnemyHealth _enemyHealth;
        private Transform _playerTarget;
        private float _fireTimer;

        private void Awake()
        {
            _enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            // 활성화 시 처음 발사까지 쿨타임 대기
            _fireTimer = _fireInterval;

            if (_playerTarget == null)
            {
                PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
                if (playerHealth != null)
                {
                    _playerTarget = playerHealth.transform;
                }
            }
        }

        private void Update()
        {
            if (_enemyHealth == null || !_enemyHealth.IsAlive || _playerTarget == null)
            {
                return;
            }

            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                FireProjectile();
                _fireTimer = _fireInterval;
            }
        }

        private void FireProjectile()
        {
            if (_projectilePrefab == null || PoolManager.Instance == null)
            {
                return;
            }

            Vector2 dir = (_playerTarget.position - transform.position).normalized;
            SwipeDirection swipeDir = PickRandomSwipeDirection();

            GameObject obj = PoolManager.Instance.GetInactive(
                _projectilePrefab, transform.position, Quaternion.identity);

            MaeguProjectile projectile = obj.GetComponent<MaeguProjectile>();
            if (projectile != null)
            {
                projectile.Launch(dir, swipeDir, _projectileSpeed, _projectileDamage);
            }

            obj.SetActive(true);
        }

        private static SwipeDirection PickRandomSwipeDirection()
        {
            // SwipeDirection: Up=1, Down=2, Left=3, Right=4
            return (SwipeDirection)Random.Range(1, 5);
        }
    }
}
