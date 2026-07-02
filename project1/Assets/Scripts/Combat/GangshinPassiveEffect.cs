using System;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 강신 패시브 효과 1개(gangshin_system.md — "패시브 효과, 없을 수 있음, 장착 중 항상 적용").
    /// 기존 스탯 시스템을 재사용해, 장착 시 <see cref="StatModifier"/>로 적용하고 교체/해제 시 제거한다.
    /// 슬롯 시스템(#59)이 장착 슬롯의 패시브 목록을 순회하여 PlayerStatSystem에 반영한다.
    /// </summary>
    [Serializable]
    public struct GangshinPassiveEffect
    {
        [SerializeField, Tooltip("영향을 줄 스탯 종류.")]
        private StatType _statType;

        [SerializeField, Tooltip("증감 수치. Flat이면 절대값, Percent이면 비율(0.1 = +10%).")]
        private float _value;

        [SerializeField, Tooltip("적용 방식(Flat 절대값 / Percent 비율).")]
        private StatModifierType _type;

        public GangshinPassiveEffect(StatType statType, float value, StatModifierType type)
        {
            _statType = statType;
            _value = value;
            _type = type;
        }

        public StatType StatType => _statType;
        public float Value => _value;
        public StatModifierType Type => _type;
    }
}
