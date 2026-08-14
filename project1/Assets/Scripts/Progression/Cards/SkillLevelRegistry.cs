using System;
using System.Collections.Generic;

namespace Mukseon.Gameplay.Progression.Cards
{
    /// <summary>
    /// 이번 런에서 카드별로 획득한 레벨을 보관한다(#66).
    /// 카드 풀의 "최대 레벨 제외"와 "보유 카드 가중치 x2" 판정이 모두 이 값을 본다.
    /// MonoBehaviour에 의존하지 않는 순수 C# 저장소다.
    /// </summary>
    public sealed class SkillLevelRegistry
    {
        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>보유 레벨(미보유 = 0). 알 수 없는 ID도 0을 반환한다.</summary>
        public int GetLevel(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return 0;
            }

            return _levels.TryGetValue(cardId, out int level) ? level : 0;
        }

        public bool IsOwned(string cardId) => GetLevel(cardId) > 0;

        /// <summary>레벨을 1 올리고 올라간 레벨을 반환한다. ID가 비어 있으면 0.</summary>
        public int Increment(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return 0;
            }

            int current = GetLevel(cardId) + 1;
            _levels[cardId] = current;
            return current;
        }

        public void Clear()
        {
            _levels.Clear();
        }
    }
}
