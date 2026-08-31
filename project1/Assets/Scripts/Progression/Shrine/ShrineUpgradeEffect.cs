using System;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Progression.Shrine
{
    /// <summary>
    /// 신당 업그레이드가 플레이어 스탯에 주는 효과 하나(#34, `currency_system.md`).
    ///
    /// 업그레이드가 아니라 <b>효과</b> 단위로 스탯을 지정하는 이유: 업그레이드 1종이 스탯 여러 개를
    /// 동시에 올릴 수 있다. '골드/경험치 추가 획득'이 <see cref="StatType.GoldGain"/>과
    /// <see cref="StatType.ExperienceGain"/>을 함께 올리는 것이 그 경우다.
    /// </summary>
    [Serializable]
    public struct ShrineUpgradeEffect
    {
        [SerializeField]
        private StatType _statType;

        [SerializeField]
        private StatModifierType _modifierType;

        [SerializeField, Tooltip("레벨 1당 증가량. Percent는 비율이므로 0.05 = +5%다.")]
        private float _valuePerLevel;

        public ShrineUpgradeEffect(StatType statType, StatModifierType modifierType, float valuePerLevel)
        {
            _statType = statType;
            _modifierType = modifierType;
            _valuePerLevel = valuePerLevel;
        }

        public StatType StatType => _statType;
        public StatModifierType ModifierType => _modifierType;
        public float ValuePerLevel => _valuePerLevel;

        /// <summary>
        /// 지정 레벨에서의 누적 효과량. 레벨당 증가량의 선형 배수다(레벨 3 = 레벨당 값 × 3).
        /// 음수 레벨은 0으로 본다 — 세이브가 손상돼 음수가 들어와도 스탯을 깎지 않아야 한다.
        /// </summary>
        public float ValueAtLevel(int level)
        {
            return _valuePerLevel * Mathf.Max(0, level);
        }
    }
}
