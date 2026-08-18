using System.Collections.Generic;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Progression;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 부채살 흩뿌리기(#76, [무당 전용]) — 발동 방식 ① 스와이프 시 확률 발동.
    /// 스와이프 공격마다 레벨별 확률로 부채꼴 다방향 타격을 발동한다(미발동 시 일반 단일 방향).
    ///
    /// 레벨 추적: <see cref="PlayerLevelSystem.OnSkillEffectPending"/>을 구독해
    /// <see cref="LevelUpSkillEffectType.FanAttackBuff"/> 선택 시 레벨을 갱신한다.
    /// 무당 시작 시 레벨 1 보유는 CharacterData.StartingSkills → PlayerLevelSystem이 부여하며,
    /// 그 부여 또한 동일 이벤트로 전달되므로 이 컴포넌트가 자동으로 레벨 1을 받는다.
    ///
    /// 실제 타격 적용은 <see cref="SwipeAttackEventListener"/>가 <see cref="TryBuildFanBranches"/>를
    /// 질의하여 수행한다. 수치(발동 확률)는 인스펙터에서 관리하며 하드코딩하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FanAttackSkill : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerLevelSystem _playerLevelSystem;

        [SerializeField, Tooltip("연동되는 SkillData의 SkillId. OnEnable 레벨 동기화에 사용 (Skill_FanAttack 기준).")]
        private string _skillId = "fan_attack";

        [Header("Trigger Chance Per Level")]
        [SerializeField, Tooltip("레벨별 발동 확률(0~1). 인덱스 0 = Lv1. skill_balance_mvp.md §5 기준.")]
        private float[] _triggerChancePerLevel = { 0.3f, 0.45f, 0.6f };

        // 현재 보유 레벨. 0 = 미보유(발동하지 않음).
        private int _level;

        public int Level => _level;

        /// <summary>현재 레벨의 발동 확률(0~1). 미보유면 0.</summary>
        public float CurrentTriggerChance => GetTriggerChance(_level);

        private void Awake()
        {
            if (_playerLevelSystem == null)
            {
                _playerLevelSystem = GetComponent<PlayerLevelSystem>();
            }
        }

        private void OnEnable()
        {
            if (_playerLevelSystem != null)
            {
                _playerLevelSystem.OnSkillEffectPending += HandleSkillEffectPending;

                // 이 효과 타입의 처리자가 있음을 알린다. 등록이 없으면 해당 카드가 선택될 때
                // "처리할 시스템이 없다" 경고가 뜬다(빈 선택 감지 — #66).
                _playerLevelSystem.RegisterEffectHandler(LevelUpSkillEffectType.FanAttackBuff);

                // 비활성 중 부여/레벨업 이벤트를 놓쳤을 수 있으므로 현재 레벨을 직접 동기화한다.
                // (최초 로드 시엔 GrantStartingSkills(Start) 이전이라 0이며, 이후 이벤트로 갱신된다.)
                ApplyLevel(_playerLevelSystem.GetSkillLevel(_skillId));
            }
        }

        private void OnDisable()
        {
            if (_playerLevelSystem != null)
            {
                _playerLevelSystem.OnSkillEffectPending -= HandleSkillEffectPending;
                _playerLevelSystem.UnregisterEffectHandler(LevelUpSkillEffectType.FanAttackBuff);
            }
        }

        private void HandleSkillEffectPending(SkillData skill, int nextLevel)
        {
            if (skill == null || skill.EffectType != LevelUpSkillEffectType.FanAttackBuff)
            {
                return;
            }

            ApplyLevel(nextLevel);
        }

        /// <summary>레벨을 직접 설정한다(이벤트 핸들러 및 테스트에서 사용). [0, MaxLevel]로 클램프.</summary>
        public void ApplyLevel(int level)
        {
            _level = Mathf.Clamp(level, 0, FanAttackPattern.MaxLevel);
        }

        /// <summary>
        /// 이번 스와이프에서 부채살이 발동하는지 확률 판정 후, 발동 시 부채 갈래를 채운다.
        /// 발동(true) 시 <paramref name="output"/>에 갈래가 채워지고, 미발동/미보유 시 false.
        /// </summary>
        public bool TryBuildFanBranches(SwipeDirection swipeDirection, List<FanAttackPattern.FanBranch> output)
        {
            float chance = GetTriggerChance(_level);
            if (chance <= 0f)
            {
                output?.Clear();
                return false;
            }

            // chance >= 1이면 항상 발동. Random.value는 [0,1].
            float roll = chance >= 1f ? 0f : Random.value;
            return TryBuildFanBranches(swipeDirection, roll, output);
        }

        /// <summary>
        /// 확률 판정에 사용할 난수(<paramref name="roll"/>, [0,1))를 외부에서 주입하는 결정론적 오버로드(테스트용).
        /// roll &lt; 발동 확률이면 발동.
        /// </summary>
        public bool TryBuildFanBranches(SwipeDirection swipeDirection, float roll, List<FanAttackPattern.FanBranch> output)
        {
            if (output == null)
            {
                return false;
            }

            output.Clear();

            float chance = GetTriggerChance(_level);
            if (chance <= 0f || roll >= chance)
            {
                return false;
            }

            int count = FanAttackPattern.BuildBranches(swipeDirection, _level, output);
            return count > 0;
        }

        private float GetTriggerChance(int level)
        {
            if (level < FanAttackPattern.MinLevel || _triggerChancePerLevel == null || _triggerChancePerLevel.Length == 0)
            {
                return 0f;
            }

            int index = Mathf.Clamp(level - 1, 0, _triggerChancePerLevel.Length - 1);
            return Mathf.Clamp01(_triggerChancePerLevel[index]);
        }
    }
}
