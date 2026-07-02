using System.Collections.Generic;
using Mukseon.Gameplay.Stats;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 장착 강신의 패시브 효과(#59)를 <see cref="PlayerStatSystem"/>에 적용/해제하는 헬퍼.
    /// 출처를 어빌리티 인스턴스로 지정해, 교체 시 해당 강신의 패시브만 정확히 되돌린다.
    /// 컨트롤러에서 분리해 단일 책임을 유지하고 EditMode 테스트를 용이하게 한다.
    /// </summary>
    public sealed class GangshinPassiveApplier
    {
        private readonly PlayerStatSystem _statSystem;
        private readonly HashSet<StatType> _appliedStats = new HashSet<StatType>();
        private GangshinAbilityBase _activeAbility;

        public GangshinPassiveApplier(PlayerStatSystem statSystem)
        {
            _statSystem = statSystem;
        }

        /// <summary>현재 패시브가 적용된 어빌리티(없으면 null).</summary>
        public GangshinAbilityBase ActiveAbility => _activeAbility;

        /// <summary>
        /// 원하는 어빌리티의 패시브와 실제 적용 상태를 일치시킨다. 대상이 바뀐 경우에만
        /// 이전 패시브를 즉시 해제하고 새 패시브를 활성화한다(장착/교체 후 호출).
        /// </summary>
        public void Sync(GangshinAbilityBase desired)
        {
            if (desired == _activeAbility)
            {
                return;
            }

            Clear();
            Apply(desired);
        }

        /// <summary>적용 중인 패시브를 모두 해제한다(장착 해제 / 컨트롤러 비활성화 시).</summary>
        public void Clear()
        {
            if (_activeAbility != null && _statSystem != null)
            {
                foreach (StatType statType in _appliedStats)
                {
                    _statSystem.RemoveModifiersFromSource(statType, _activeAbility);
                }
            }

            _appliedStats.Clear();
            _activeAbility = null;
        }

        private void Apply(GangshinAbilityBase ability)
        {
            _activeAbility = ability;
            if (ability == null || ability.Data == null || _statSystem == null)
            {
                return;
            }

            IReadOnlyList<GangshinPassiveEffect> passives = ability.Data.PassiveEffects;
            for (int i = 0; i < passives.Count; i++)
            {
                GangshinPassiveEffect passive = passives[i];
                _statSystem.AddModifier(passive.StatType, new StatModifier(passive.Value, passive.Type, ability));
                _appliedStats.Add(passive.StatType);
            }
        }
    }
}
