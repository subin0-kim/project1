using Mukseon.Gameplay.Combat;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Progression.Cards
{
    /// <summary>
    /// 선택된 강화 카드의 효과를 즉시 적용한다(#66).
    /// 스탯 계열처럼 스탯 시스템 / 스와이프 리스너만으로 끝나는 효과는 여기서 직접 처리하고,
    /// 전담 시스템이 필요한 효과(도깨비불 · 결계 · 강신 등)는 <see cref="TryApply"/>가 false를 돌려
    /// 호출자가 담당 시스템에 위임하도록 한다.
    /// </summary>
    public sealed class CardEffectApplier
    {
        private readonly PlayerStatSystem _statSystem;
        private readonly SwipeAttackEventListener _swipeAttackEventListener;

        // StatModifier 제거 시 출처로 식별되는 객체. 보통 PlayerLevelSystem 인스턴스를 넘긴다.
        private readonly object _source;

        public CardEffectApplier(PlayerStatSystem statSystem, SwipeAttackEventListener swipeAttackEventListener, object source)
        {
            _statSystem = statSystem;
            _swipeAttackEventListener = swipeAttackEventListener;
            _source = source;
        }

        /// <summary>
        /// 효과를 적용했으면 true, 전담 시스템에 위임해야 하는 효과면 false를 반환한다.
        /// 참조가 없어 적용하지 못한 경우에도 "이 계층이 담당하는 효과"이므로 true로 처리해,
        /// 미구현 효과로 오인되어 경고가 발생하는 것을 막는다.
        /// </summary>
        public bool TryApply(SkillData definition)
        {
            if (definition == null)
            {
                return false;
            }

            switch (definition.EffectType)
            {
                case LevelUpSkillEffectType.StatFlat:
                    AddStatModifier(definition.StatType, definition.Value, StatModifierType.Flat);
                    return true;

                case LevelUpSkillEffectType.StatPercent:
                    AddStatModifier(definition.StatType, definition.Value, StatModifierType.Percent);
                    return true;

                case LevelUpSkillEffectType.BonusTargets:
                    _swipeAttackEventListener?.AddBonusTargets(Mathf.RoundToInt(definition.Value));
                    return true;

                case LevelUpSkillEffectType.PickupRadius:
                    // 혼불 자력 반경을 Flat StatModifier로 증가시킨다(#40). SoulCollector가 MagnetRadius 스탯을 읽는다.
                    AddStatModifier(StatType.MagnetRadius, definition.Value, StatModifierType.Flat);
                    return true;

                default:
                    return false;
            }
        }

        private void AddStatModifier(StatType statType, float value, StatModifierType modifierType)
        {
            _statSystem?.AddModifier(statType, new StatModifier(value, modifierType, _source));
        }
    }
}
