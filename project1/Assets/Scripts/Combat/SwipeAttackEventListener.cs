using System.Collections.Generic;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public class SwipeAttackEventListener : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerSwipeAttackController _playerSwipeAttackController;

        [SerializeField]
        private PlayerStatSystem _playerStatSystem;

        [SerializeField]
        private Transform _attackOrigin;

        [Header("Attack Settings")]
        [SerializeField, Min(0.1f)]
        private float _attackRange = 3f;

        [SerializeField, Range(1f, 180f)]
        private float _attackArcAngle = 70f;

        [SerializeField, Min(0f)]
        private float _baseDamage = 1f;

        [SerializeField]
        private LayerMask _targetLayers = ~0;

        [Header("Debug")]
        [SerializeField]
        private bool _showDebugLogs = true;

        private readonly Collider2D[] _overlapBuffer = new Collider2D[64];
        private readonly HashSet<EnemyHealth> _targetBuffer = new HashSet<EnemyHealth>();

        private void Awake()
        {
            if (_playerSwipeAttackController == null)
            {
                _playerSwipeAttackController = GetComponent<PlayerSwipeAttackController>();
            }

            if (_playerStatSystem == null)
            {
                _playerStatSystem = GetComponent<PlayerStatSystem>();
            }

            if (_attackOrigin == null)
            {
                _attackOrigin = transform;
            }
        }

        private void OnEnable()
        {
            if (_playerSwipeAttackController != null)
            {
                _playerSwipeAttackController.OnAttackExecuted += HandleAttackExecuted;
            }
        }

        private void OnDisable()
        {
            if (_playerSwipeAttackController != null)
            {
                _playerSwipeAttackController.OnAttackExecuted -= HandleAttackExecuted;
            }
        }

        private void HandleAttackExecuted(SwipeDirection direction)
        {
            Vector2 attackDirection = SwipeAttackGeometry.ToVector(direction);
            if (attackDirection == Vector2.zero)
            {
                return;
            }

            int hitCount = ApplyDamage(attackDirection);

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[SwipeAttackEventListener] {direction} attack hit {hitCount} target(s).");
            }
#endif
        }

        private int ApplyDamage(Vector2 attackDirection)
        {
            if (_attackOrigin == null)
            {
                return 0;
            }

            float damage = ResolveDamage();
            if (damage <= 0f)
            {
                return 0;
            }

            int overlapCount = Physics2D.OverlapCircleNonAlloc(
                _attackOrigin.position,
                _attackRange,
                _overlapBuffer,
                _targetLayers);

            if (overlapCount <= 0)
            {
                return 0;
            }

            _targetBuffer.Clear();
            Vector2 attackOrigin = _attackOrigin.position;

            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D targetCollider = _overlapBuffer[i];
                if (targetCollider == null)
                {
                    continue;
                }

                EnemyHealth enemyHealth = targetCollider.GetComponentInParent<EnemyHealth>();
                if (enemyHealth == null || !enemyHealth.IsAlive || !_targetBuffer.Add(enemyHealth))
                {
                    continue;
                }

                Vector2 targetPosition = targetCollider.bounds.center;
                bool isInsideArc = SwipeAttackGeometry.IsTargetWithinArc(
                    attackOrigin,
                    attackDirection,
                    targetPosition,
                    _attackArcAngle);

                if (!isInsideArc)
                {
                    continue;
                }

                enemyHealth.ApplyDamage(damage, this);
            }

            return _targetBuffer.Count;
        }

        private float ResolveDamage()
        {
            float damage = Mathf.Max(0f, _baseDamage);

            if (_playerStatSystem != null)
            {
                float attackPower = _playerStatSystem.GetValue(StatType.AttackPower);
                damage += Mathf.Max(0f, attackPower);
            }

            return damage;
        }
    }
}
