using System;
using System.Collections.Generic;

namespace Mukseon.Gameplay.Progression.Cards
{
    /// <summary>
    /// 추첨 시점 카드 조건 여러 개를 하나로 합친다(#66).
    ///
    /// 조건을 거는 쪽이 여럿(강신 카드 적용기, 후속 교체 UI 등)이므로 단일 델리게이트로 두면
    /// 나중에 등록한 쪽이 앞의 조건을 조용히 덮어쓴다. 등록은 목록에 쌓고 평가는 AND로 하여
    /// 하나라도 거부한 카드는 후보에서 빠지게 한다.
    ///
    /// 해제는 자기가 등록한 조건만 대상으로 하므로, 컴포넌트 비활성화 순서와 무관하게
    /// 남의 조건이 사라지지 않는다.
    /// </summary>
    /// <typeparam name="T">카드 정의 타입(<see cref="CardPool{T}"/>와 동일).</typeparam>
    public sealed class CardEligibilityFilterSet<T> where T : class, ICardDefinition
    {
        private readonly List<Func<T, bool>> _filters = new List<Func<T, bool>>();

        /// <summary>등록된 조건 수.</summary>
        public int Count => _filters.Count;

        /// <summary>
        /// 조건을 등록한다. null이거나 이미 등록된 조건이면 무시하고 false를 반환한다.
        /// (델리게이트 비교는 대상 인스턴스 + 메서드 기준이라 같은 컴포넌트의 중복 등록만 걸러진다.)
        /// </summary>
        public bool Add(Func<T, bool> filter)
        {
            if (filter == null || _filters.Contains(filter))
            {
                return false;
            }

            _filters.Add(filter);
            return true;
        }

        /// <summary>자기가 등록한 조건만 해제한다. 등록된 적이 없으면 false.</summary>
        public bool Remove(Func<T, bool> filter)
        {
            return filter != null && _filters.Remove(filter);
        }

        /// <summary>등록된 모든 조건을 통과하는지 판정한다. 조건이 하나도 없으면 항상 통과.</summary>
        public bool Evaluate(T card)
        {
            for (int i = 0; i < _filters.Count; i++)
            {
                if (!_filters[i](card))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
