using Mukseon.Core;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 강신 발동(Active) 중 부수 효과(#30)를 처리하는 헬퍼: 시간 감속, 공격력 버프, 필살기 발동,
    /// 그리고 어빌리티 미장착 시 레거시 전체 펄스. 슬롯 관리(#59)와 분리해 컨트롤러의 단일 책임을 유지한다.
    /// </summary>
    public sealed class GangshinActivationEffects
    {
        private readonly PlayerStatSystem _statSystem;
        private readonly object _source;
        private readonly bool _buffAttackPower;
        private readonly float _attackBonusPercent;
        private readonly float _activeTimeScale;
        private readonly bool _dealPulse;
        private readonly float _pulseDamage;

        public GangshinActivationEffects(
            PlayerStatSystem statSystem,
            object source,
            bool buffAttackPower,
            float attackBonusPercent,
            float activeTimeScale,
            bool dealPulse,
            float pulseDamage)
        {
            _statSystem = statSystem;
            _source = source;
            _buffAttackPower = buffAttackPower;
            _attackBonusPercent = attackBonusPercent;
            _activeTimeScale = activeTimeScale;
            _dealPulse = dealPulse;
            _pulseDamage = pulseDamage;
        }

        /// <summary>
        /// 발동 진입: 시간 감속 → 공격력 버프 → 장착 어빌리티 발동(미장착 시 레거시 펄스). 발동 원점/레벨/
        /// 대상 적 목록을 <see cref="GangshinSlotContext"/>로 전달한다.
        /// </summary>
        public void Enter(GangshinAbilityBase ability, Vector2 origin, int level)
        {
            TimeScaleService.SetRate(_activeTimeScale);

            if (_buffAttackPower && _statSystem != null && _attackBonusPercent > 0f)
            {
                _statSystem.AddModifier(
                    StatType.AttackPower,
                    new StatModifier(_attackBonusPercent, StatModifierType.Percent, _source));
            }

            if (ability != null)
            {
                ability.Activate(new GangshinSlotContext(origin, level, _source, EnemyHealth.ActiveEnemies));
            }
            else if (_dealPulse)
            {
                ApplyActivationPulse();
            }
        }

        /// <summary>
        /// 발동 종료/취소: 공격력 버프 제거 + 시간 배율 등속 복원.
        /// 게임오버·레벨업으로 정지 중이라면 <see cref="TimeScaleService"/>가 정지를 우선하므로,
        /// 여기서 등속을 복원해도 정지가 풀리지 않는다(#109).
        /// </summary>
        public void Exit()
        {
            if (_statSystem != null)
            {
                _statSystem.RemoveModifiersFromSource(StatType.AttackPower, _source);
            }

            TimeScaleService.SetRate(TimeScaleController.MaxRate);
        }

        private void ApplyActivationPulse()
        {
            if (_pulseDamage <= 0f)
            {
                return;
            }

            var activeEnemies = EnemyHealth.ActiveEnemies;
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                EnemyHealth enemy = activeEnemies[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                enemy.ApplyDamage(_pulseDamage, _source);
            }
        }
    }
}
