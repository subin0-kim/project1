using System.Collections.Generic;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 런(run) 안에서 적 종류별 '첫 등장'을 한 번만 통과시키는 기록기(#70).
    ///
    /// 등장 연출은 "새 적이 나왔다"를 알리는 장치라 두 번째부터는 소음이다. 스폰은 초당 여러 번
    /// 일어나므로 판정은 호출부가 아니라 여기서 원자적으로 끝낸다 —
    /// <see cref="TryMarkIntroduced"/>는 '조회 후 기록'을 한 번에 처리해 같은 종류가 같은 프레임에
    /// 두 마리 스폰돼도 연출이 두 번 뜨지 않는다.
    ///
    /// MonoBehaviour에 의존하지 않는 순수 C# 클래스다(CLAUDE.md 테스트 용이성 규칙).
    /// </summary>
    public sealed class EnemyIntroductionTracker
    {
        // 종류 식별 키(보통 MonsterData 에셋). 인스턴스가 아니라 종류 단위로 기록해야
        // 풀에서 재사용된 같은 종류의 적이 다시 연출을 띄우지 않는다.
        private readonly HashSet<object> _introduced = new HashSet<object>();

        /// <summary>이번 런에서 지금까지 소개된 적 종류 수.</summary>
        public int IntroducedCount => _introduced.Count;

        /// <summary>
        /// 이 종류가 이번 런에서 처음이면 기록하고 true를 반환한다(= 연출을 재생해야 한다).
        /// 이미 소개했거나 키가 null이면 false.
        /// </summary>
        public bool TryMarkIntroduced(object speciesKey)
        {
            if (speciesKey == null)
            {
                return false;
            }

            return _introduced.Add(speciesKey);
        }

        /// <summary>이 종류가 이미 소개되었는지 조회한다(기록하지 않음).</summary>
        public bool IsIntroduced(object speciesKey)
        {
            return speciesKey != null && _introduced.Contains(speciesKey);
        }

        /// <summary>새 런 시작 시 호출해 기록을 비운다. 다음 런에서는 모든 적이 다시 처음이 된다.</summary>
        public void Reset()
        {
            _introduced.Clear();
        }
    }
}
