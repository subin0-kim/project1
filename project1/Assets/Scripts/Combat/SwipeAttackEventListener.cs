using System;
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

        [SerializeField, Tooltip("부채살 흩뿌리기(#76). 없으면 일반 단일 방향 스와이프만 동작.")]
        private FanAttackSkill _fanAttackSkill;

        [Header("Attack Settings")]
        [SerializeField, Min(0f)]
        private float _baseDamage = 1f;

        [SerializeField, Min(1)]
        private int _targetsPerAttack = 1;

        private int _bonusTargets;

        // 동적 방향 변환(#68) n-hit 가드용 스와이프 식별자. 스와이프 1회마다 증가한다.
        private int _swipeId;

        [Header("Debug")]
        [SerializeField]
        private bool _showDebugLogs = true;

        private readonly List<EnemyHealth> _targetBuffer = new List<EnemyHealth>(16);
        private readonly List<FanAttackPattern.FanBranch> _fanBranchBuffer = new List<FanAttackPattern.FanBranch>(5);

        /// <summary>부채살 흩뿌리기(#76) 발동 시 발생. (스와이프 방향, 부채 원점=끝점 최근접 적 위치, 부채 레벨)을 전달.</summary>
        public event Action<SwipeDirection, Vector2, int> OnFanAttackTriggered;

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

            if (_fanAttackSkill == null)
            {
                _fanAttackSkill = GetComponent<FanAttackSkill>();
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

            // 동적 변환(#68): 이 스와이프를 식별해 동일 스와이프의 n-hit 중복 카운트를 막는다.
            _swipeId++;

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

            int baseTargets = Mathf.Max(1, ResolveTargetsPerAttack() + _bonusTargets);

            // 부채살 흩뿌리기(#76): 확률 발동 시 스와이프 방향뿐 아니라 부채꼴 다방향을 동시 타격한다.
            // 미발동/미보유 시 기존 일반 단일 방향 스와이프로 폴백.
            bool fanTriggered = _fanAttackSkill != null
                && _fanAttackSkill.TryBuildFanBranches(swipeDirection, _fanBranchBuffer);

            if (!fanTriggered)
            {
                return ApplyDamageToDirection(swipeDirection, origin, baseTargets, damage);
            }

            // VFX 부채는 스와이프 끝점에서 가장 가까운 적에서 펼쳐진다(적이 없으면 끝점 자체).
            Vector2 fanVfxOrigin = ResolveNearestEnemyOrigin(origin);
            OnFanAttackTriggered?.Invoke(swipeDirection, fanVfxOrigin, _fanAttackSkill.Level);

            int totalHits = 0;
            for (int i = 0; i < _fanBranchBuffer.Count; i++)
            {
                FanAttackPattern.FanBranch branch = _fanBranchBuffer[i];

                // 적은 단일 방향 속성을 가지므로 각 방향은 한 번씩만 질의되며 중복 타격이 없다.
                // 동일 방향 추가 타격(Lv3 스와이프 방향)은 BonusTargets로 표현한다.
                int maxTargets = Mathf.Max(1, baseTargets + branch.BonusTargets);
                totalHits += ApplyDamageToDirection(branch.Direction, origin, maxTargets, damage);
            }

            return totalHits;
        }

        /// <summary>
        /// 한 방향에 대해 끝점에서 가까운 순으로 최대 <paramref name="maxTargets"/>개 적을 선택·타격한다.
        /// 타격 적용 직후 _targetBuffer는 다음 호출에서 덮어써지므로, 호출 간 버퍼를 보관하지 않는다.
        /// </summary>
        private int ApplyDamageToDirection(SwipeDirection direction, Vector2 origin, int maxTargets, float damage)
        {
            int selectedCount = SwipeAttackTargeting.SelectNearestTargets(
                origin,
                direction,
                EnemyHealth.ActiveEnemies,
                maxTargets,
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

                if (!enemyHealth.IsAlive)
                {
                    continue;
                }

                if (usesSequence)
                {
                    enemyHealth.AttackSequence.Advance();
                }
                else
                {
                    // 동적 방향 변환(#68): 타겟팅이 방향 일치 적만 선택하므로 이 호출 == 동일 방향 타격.
                    enemyHealth.DirectionConverter?.RegisterDirectionalHit(_swipeId);
                }
            }

            return selectedCount;
        }

        /// <summary>
        /// 스와이프 끝점(<paramref name="point"/>)에서 가장 가까운, 살아있고 타격 가능한 적의 위치를 반환한다.
        /// 부채살 VFX 원점용 — 방향 속성과 무관하게 끝점 최근접 적을 고른다. 적이 없으면 끝점 자체를 반환한다.
        /// </summary>
        private Vector2 ResolveNearestEnemyOrigin(Vector2 point)
        {
            IReadOnlyList<EnemyHealth> enemies = EnemyHealth.ActiveEnemies;
            EnemyHealth nearest = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyHealth enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive || !enemy.IsTargetable)
                {
                    continue;
                }

                float sqr = ((Vector2)enemy.transform.position - point).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = enemy;
                }
            }

            return nearest != null ? (Vector2)nearest.transform.position : point;
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
            int baseTargets = _targetsPerAttack;
            CharacterData characterData = _playerStatSystem?.CharacterData;
            if (characterData != null)
            {
                baseTargets = characterData.TargetsPerAttack;
            }

            // 다중 히트 스탯(#40)은 캐릭터 기본 타격 수보다 클 때만 끌어올린다(기본값을 낮추지 않음).
            if (_playerStatSystem != null)
            {
                float multiHit = _playerStatSystem.GetValue(StatType.MultiHit);
                if (multiHit > 0f)
                {
                    return Mathf.Max(baseTargets, Mathf.RoundToInt(multiHit));
                }
            }

            return baseTargets;
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
