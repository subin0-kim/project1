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
        [SerializeField, Min(0f)]
        private float _baseDamage = 1f;

        [SerializeField, Min(1)]
        private int _targetsPerAttack = 1;

        private int _bonusTargets;

        [Header("Debug")]
        [SerializeField]
        private bool _showDebugLogs = true;

        private readonly List<EnemyHealth> _targetBuffer = new List<EnemyHealth>(16);

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

            ValidateCharacterData();
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
            if (direction == SwipeDirection.None)
            {
                return;
            }

            int hitCount = ApplyDamage(direction);

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[SwipeAttackEventListener] {direction} swipe hit {hitCount} target(s).");
            }
#endif
        }

        private int ApplyDamage(SwipeDirection swipeDirection)
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

            int selectedCount = SwipeAttackTargeting.SelectNearestTargets(
                _attackOrigin.position,
                swipeDirection,
                EnemyHealth.ActiveEnemies,
                Mathf.Max(1, ResolveTargetsPerAttack() + _bonusTargets),
                _targetBuffer);

            if (selectedCount <= 0)
            {
                return 0;
            }

            for (int i = 0; i < selectedCount; i++)
            {
                EnemyHealth enemyHealth = _targetBuffer[i];
                enemyHealth.ApplyDamage(damage, this);
            }

            return selectedCount;
        }

        private float ResolveDamage()
        {
            float damage = Mathf.Max(0f, ResolveBaseDamage());

            if (_playerStatSystem != null)
            {
                float attackPower = _playerStatSystem.GetValue(StatType.AttackPower);
                damage += Mathf.Max(0f, attackPower);
            }

            return damage;
        }

        public void AddBonusTargets(int amount)
        {
            _bonusTargets = Mathf.Max(0, _bonusTargets + Mathf.Max(0, amount));
        }

        public int GetCurrentMaxTargets()
        {
            return Mathf.Max(1, ResolveTargetsPerAttack() + _bonusTargets);
        }

        private float ResolveBaseDamage()
        {
            CharacterData characterData = _playerStatSystem?.CharacterData;
            return characterData != null ? characterData.BaseAttackDamage : _baseDamage;
        }

        private int ResolveTargetsPerAttack()
        {
            CharacterData characterData = _playerStatSystem?.CharacterData;
            return characterData != null ? characterData.TargetsPerAttack : _targetsPerAttack;
        }

        private void ValidateCharacterData()
        {
            CharacterData characterData = _playerStatSystem?.CharacterData;
            if (characterData == null)
            {
                return;
            }

            if (!characterData.IsValid(out string reason))
            {
                Debug.LogWarning($"[SwipeAttackEventListener] CharacterData '{characterData.name}' is invalid. {reason}");
            }
        }
    }
}
