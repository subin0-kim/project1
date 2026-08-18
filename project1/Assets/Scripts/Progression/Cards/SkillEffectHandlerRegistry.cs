using System.Collections.Generic;

namespace Mukseon.Gameplay.Progression.Cards
{
    /// <summary>
    /// 전담 시스템에 위임되는 효과 타입 중, 실제로 처리할 시스템이 살아 있는 타입을 추적한다(#66).
    ///
    /// PlayerLevelSystem.OnSkillEffectPending의 구독자는 자기 타입이 아니면 그냥 무시하는 구조라,
    /// 이벤트만으로는 "구독자가 하나라도 있는가"밖에 알 수 없다. 그래서 담당 컴포넌트가 씬에서
    /// 빠져도 카드는 그대로 제시되고, 선택해도 아무 일이 없는 상태(빈 선택)가 경고 없이 지나간다.
    /// 각 시스템이 자기 타입을 등록하게 해서 그 상황을 드러낸다.
    ///
    /// 같은 타입을 여러 컴포넌트가 등록할 수 있으므로 참조 수로 센다 — 하나가 비활성화돼도
    /// 남은 처리자가 있으면 계속 처리 가능한 것으로 본다.
    /// </summary>
    public sealed class SkillEffectHandlerRegistry
    {
        private readonly Dictionary<LevelUpSkillEffectType, int> _handlerCounts =
            new Dictionary<LevelUpSkillEffectType, int>();

        /// <summary>이 타입을 처리하는 시스템이 활성화되었음을 등록한다(구독자의 OnEnable).</summary>
        public void Register(LevelUpSkillEffectType effectType)
        {
            _handlerCounts.TryGetValue(effectType, out int count);
            _handlerCounts[effectType] = count + 1;
        }

        /// <summary>등록을 해제한다(구독자의 OnDisable). 등록된 적이 없으면 아무것도 하지 않는다.</summary>
        public void Unregister(LevelUpSkillEffectType effectType)
        {
            if (!_handlerCounts.TryGetValue(effectType, out int count))
            {
                return;
            }

            if (count <= 1)
            {
                _handlerCounts.Remove(effectType);
                return;
            }

            _handlerCounts[effectType] = count - 1;
        }

        /// <summary>이 효과 타입을 처리할 시스템이 지금 있는지.</summary>
        public bool IsHandled(LevelUpSkillEffectType effectType)
        {
            return _handlerCounts.TryGetValue(effectType, out int count) && count > 0;
        }
    }
}
