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

        [SerializeField]
        private Camera _camera;

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

        private void HandleAttackExecuted(SwipeDirection direction, Vector2 endScreenPosition)
        {
            if (direction == SwipeDirection.None)
            {
                return;
            }

            // combat_system.md §2: 스와이프 끝점(Touch Up 좌표)에 가장 가까운 적을 우선 타격한다.
            Vector2 attackOrigin = ResolveAttackOrigin(endScreenPosition);
            int hitCount = ApplyDamage(direction, attackOrigin);

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[SwipeAttackEventListener] {direction} swipe hit {hitCount} target(s).");
            }
#endif
        }

        /// <summary>
        /// 타겟팅 기준점을 스와이프 끝점의 월드 좌표로 변환한다(`combat_system.md` §2).
        /// 카메라가 없으면 공격 원점(_attackOrigin)으로 폴백한다.
        /// </summary>
        private Vector2 ResolveAttackOrigin(Vector2 endScreenPosition)
        {
            // Camera.main은 Awake 시점에 아직 null일 수 있으므로 사용 시점에 지연 해석한다.
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera != null)
            {
                Vector3 world = _camera.ScreenToWorldPoint(endScreenPosition);
                return new Vector2(world.x, world.y);
            }

            if (_attackOrigin != null)
            {
                return _attackOrigin.position;
            }

            Debug.LogWarning("[SwipeAttackEventListener] 카메라와 _attackOrigin이 모두 없어 월드 원점(0,0)으로 타겟팅합니다. 씬 설정을 확인하세요.");
            return Vector2.zero;
        }

        private int ApplyDamage(SwipeDirection swipeDirection, Vector2 origin)
        {
            float damage = ResolveDamage();
            if (damage <= 0f)
            {
                return 0;
            }

            int selectedCount = SwipeAttackTargeting.SelectNearestTargets(
                origin,
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

                // 방향 시퀀스는 보스 전용(#84). 시퀀스 적은 타격당 데미지 1 + 인덱스 전진,
                // 그 외 적은 단일 방향 타격으로 정상 데미지를 적용한다.
                bool usesSequence = enemyHealth.UsesAttackSequence;
                float actualDamage = usesSequence ? 1f : damage;
                enemyHealth.ApplyDamage(actualDamage, this);

                if (enemyHealth.IsAlive && usesSequence)
                {
                    enemyHealth.AttackSequence.Advance();
                }
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
