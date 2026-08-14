using System;
using System.Collections.Generic;

namespace Mukseon.Gameplay.Progression.Cards
{
    /// <summary>
    /// 강화 카드 풀과 가중치 랜덤 추첨(#66, `card_system.md`).
    /// MonoBehaviour / ScriptableObject에 의존하지 않는 순수 C# 로직이라 EditMode 테스트가 용이하다.
    ///
    /// 책임은 두 단계로 나뉜다.
    /// 1) 생성 시점(런 시작): 캐릭터 전용 카드 필터링 + 중복 정의 제거 — 런 내내 고정된다.
    /// 2) 추첨 시점(레벨업 / 미니 보스): 최대 레벨 카드 제외 + 보유 카드 가중치 x2 + 중복 없는 N장 추첨.
    ///    보유 레벨은 런 중 계속 변하므로 반드시 추첨 때마다 다시 평가한다.
    /// </summary>
    /// <typeparam name="T">카드 정의 타입. 호출자가 원본 타입(예: SkillData)을 그대로 돌려받도록 제네릭이다.</typeparam>
    public sealed class CardPool<T> where T : class, ICardDefinition
    {
        public const int DefaultDrawCount = 3;
        public const float BaseWeight = 1f;

        /// <summary>보유 중인 스킬 / 강신 카드에 적용되는 가중치 배율(`card_system.md` — 가중치 규칙).</summary>
        public const float OwnedWeightMultiplier = 2f;

        private readonly List<T> _cards = new List<T>();

        // 추첨 1회분 작업 버퍼. 레벨업마다 재할당하지 않도록 필드로 유지한다(_candidates[i] ↔ _weights[i] 대응).
        private readonly List<T> _candidates = new List<T>();
        private readonly List<float> _weights = new List<float>();

        /// <summary>
        /// 캐릭터 및 정의 목록으로 풀을 초기화한다.
        /// <paramref name="characterId"/>가 비어 있으면 전용 카드 필터링을 건너뛴다(캐릭터 미확정 상태에서
        /// 전용 카드가 통째로 사라지는 것보다, 필터를 적용하지 않는 편이 덜 위험하다).
        /// </summary>
        public CardPool(IEnumerable<T> definitions, string characterId)
        {
            CharacterId = characterId ?? string.Empty;

            if (definitions == null)
            {
                return;
            }

            // 같은 카드가 목록에 두 번 들어가면 한 번의 추첨에서 2장 등장할 수 있으므로 ID 기준으로 제거한다.
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (T definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.CardId))
                {
                    continue;
                }

                if (!MatchesCharacter(definition, CharacterId))
                {
                    continue;
                }

                if (!seenIds.Add(definition.CardId))
                {
                    continue;
                }

                _cards.Add(definition);
            }
        }

        /// <summary>이 풀이 초기화된 캐릭터 ID. 비어 있으면 전용 카드 필터링이 적용되지 않았다는 뜻이다.</summary>
        public string CharacterId { get; }

        /// <summary>캐릭터 필터를 통과한 카드 전체. 최대 레벨 제외는 추첨 시점에 적용된다.</summary>
        public IReadOnlyList<T> Cards => _cards;

        public int Count => _cards.Count;

        /// <summary>
        /// 추첨 시점에 카드를 추가로 걸러내는 선택적 조건(null이면 미적용).
        /// 지금 선택해도 효과를 적용할 수 없는 카드를 제외해 "빈 선택"을 막는 용도다
        /// (예: 강신 슬롯이 모두 찼는데 교체 UI가 없어 부여할 수 없는 강신 카드).
        /// </summary>
        public Func<T, bool> EligibilityFilter { get; set; }

        /// <summary>
        /// 가중치 랜덤으로 서로 다른 카드 <paramref name="count"/>장을 <paramref name="results"/>에 채운다.
        /// 후보가 모자라면 가능한 만큼만 채운다. 실제로 담긴 장수를 반환한다.
        /// </summary>
        /// <param name="ownedLevelProvider">카드 ID → 현재 보유 레벨(미보유 = 0). 최대 레벨 제외와 가중치 판정에 쓰인다.</param>
        /// <param name="results">결과를 담을 목록. 호출 시 비워진다.</param>
        public int Draw(
            Func<string, int> ownedLevelProvider,
            List<T> results,
            int count = DefaultDrawCount,
            IRandomSource random = null)
        {
            if (results == null)
            {
                return 0;
            }

            results.Clear();

            if (count <= 0 || _cards.Count == 0)
            {
                return 0;
            }

            BuildCandidates(ownedLevelProvider);

            IRandomSource source = random ?? UnityRandomSource.Shared;
            int drawCount = Math.Min(count, _candidates.Count);

            for (int drawn = 0; drawn < drawCount; drawn++)
            {
                int index = PickWeightedIndex(source);
                if (index < 0)
                {
                    break;
                }

                results.Add(_candidates[index]);

                // 뽑힌 카드는 후보에서 제거해 같은 추첨에서 다시 나오지 않게 한다(비복원 추출).
                RemoveCandidateAt(index);
            }

            _candidates.Clear();
            _weights.Clear();
            return results.Count;
        }

        /// <summary>
        /// 카드 1장의 추첨 가중치를 반환한다(`card_system.md` — 가중치 규칙).
        /// 스탯 카드는 보유 개념이 없어 항상 기본 가중치이고, 보유 중인 스킬 / 강신 카드만 x2다.
        /// </summary>
        public static float ResolveWeight(ICardDefinition card, int ownedLevel)
        {
            if (card == null || card.Category == CardCategory.Stat || ownedLevel <= 0)
            {
                return BaseWeight;
            }

            return BaseWeight * OwnedWeightMultiplier;
        }

        /// <summary>
        /// 카드가 해당 캐릭터에게 허용되는지 판정한다. 전용 캐릭터가 지정되지 않은 카드는 공용이다.
        /// </summary>
        public static bool MatchesCharacter(ICardDefinition card, string characterId)
        {
            if (card == null)
            {
                return false;
            }

            string required = card.RequiredCharacterId;
            if (string.IsNullOrWhiteSpace(required))
            {
                return true;
            }

            // 캐릭터를 특정할 수 없으면 필터링 자체를 적용하지 않는다(생성자 주석 참조).
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return true;
            }

            return string.Equals(required.Trim(), characterId.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>최대 레벨에 도달하지 않은 카드만 후보로 모으고, 각 후보의 가중치를 함께 계산한다.</summary>
        private void BuildCandidates(Func<string, int> ownedLevelProvider)
        {
            _candidates.Clear();
            _weights.Clear();

            for (int i = 0; i < _cards.Count; i++)
            {
                T card = _cards[i];
                int ownedLevel = ownedLevelProvider != null ? ownedLevelProvider(card.CardId) : 0;

                if (ownedLevel >= card.MaxLevel)
                {
                    continue;
                }

                if (EligibilityFilter != null && !EligibilityFilter(card))
                {
                    continue;
                }

                _candidates.Add(card);
                _weights.Add(ResolveWeight(card, ownedLevel));
            }
        }

        /// <summary>
        /// 누적 가중치 구간으로 후보 1개를 고른다.
        /// [0, 총합) 구간에서 난수를 뽑아 앞에서부터 가중치를 누적하며, 난수가 누적값보다 작아지는
        /// 첫 후보를 선택한다 — 각 후보가 차지하는 구간 길이가 곧 가중치이므로 확률이 가중치에 비례한다.
        /// </summary>
        private int PickWeightedIndex(IRandomSource source)
        {
            if (_candidates.Count == 0)
            {
                return -1;
            }

            float total = 0f;
            for (int i = 0; i < _weights.Count; i++)
            {
                total += _weights[i];
            }

            // 모든 가중치가 0인 비정상 데이터에서도 추첨이 멈추지 않도록 첫 후보로 폴백한다.
            if (total <= 0f)
            {
                return 0;
            }

            float roll = source.NextFloat(total);
            float cumulative = 0f;

            for (int i = 0; i < _weights.Count; i++)
            {
                cumulative += _weights[i];
                if (roll < cumulative)
                {
                    return i;
                }
            }

            // 부동소수 누적 오차나 상한 포함 난수로 구간을 벗어난 경우 마지막 후보를 고른다.
            return _candidates.Count - 1;
        }

        /// <summary>후보를 O(1)로 제거한다. 순서는 추첨 결과에 영향을 주지 않으므로 마지막 원소와 교환한다.</summary>
        private void RemoveCandidateAt(int index)
        {
            int last = _candidates.Count - 1;
            _candidates[index] = _candidates[last];
            _weights[index] = _weights[last];
            _candidates.RemoveAt(last);
            _weights.RemoveAt(last);
        }
    }
}
