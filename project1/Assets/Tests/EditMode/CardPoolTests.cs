using System.Collections.Generic;
using Mukseon.Gameplay.Progression;
using Mukseon.Gameplay.Progression.Cards;
using NUnit.Framework;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// 강화 카드 풀 / 가중치 추첨 검증(#66, `card_system.md`).
    /// CardPool은 순수 C#이므로 ScriptableObject 없이 대역 카드(<see cref="FakeCard"/>)로 테스트한다.
    /// </summary>
    public class CardPoolTests
    {
        private const string Mudang = "character.mudang";
        private const string Baksu = "character.baksu";

        // ── 대역 ───────────────────────────────────────────────────────────

        private sealed class FakeCard : ICardDefinition
        {
            public FakeCard(string id, CardCategory category = CardCategory.Skill, int maxLevel = 3, string requiredCharacterId = null)
            {
                CardId = id;
                Category = category;
                MaxLevel = maxLevel;
                RequiredCharacterId = requiredCharacterId;
            }

            public string CardId { get; }
            public CardCategory Category { get; }
            public int MaxLevel { get; }
            public string RequiredCharacterId { get; }
        }

        /// <summary>정해진 정규화 값(0~1)을 순서대로 돌려주는 난수 대역. 구간 경계를 정확히 겨냥할 수 있다.</summary>
        private sealed class ScriptedRandomSource : IRandomSource
        {
            private readonly Queue<float> _normalizedRolls;

            public ScriptedRandomSource(params float[] normalizedRolls)
            {
                _normalizedRolls = new Queue<float>(normalizedRolls);
            }

            public float NextFloat(float maxExclusive)
            {
                float normalized = _normalizedRolls.Count > 0 ? _normalizedRolls.Dequeue() : 0f;
                return normalized * maxExclusive;
            }
        }

        /// <summary>재현 가능한 선형 합동 생성기. 확률 분포 검증에 사용한다.</summary>
        private sealed class SeededRandomSource : IRandomSource
        {
            private uint _state;

            public SeededRandomSource(uint seed)
            {
                _state = seed == 0 ? 1u : seed;
            }

            public float NextFloat(float maxExclusive)
            {
                _state = (1664525u * _state) + 1013904223u;
                float normalized = (_state >> 8) / (float)(1 << 24);
                return normalized * maxExclusive;
            }
        }

        private static CardPool<FakeCard> CreatePool(string characterId, params FakeCard[] cards)
        {
            return new CardPool<FakeCard>(cards, characterId);
        }

        // ── 추첨 장수 / 중복 ────────────────────────────────────────────────

        [Test]
        public void Draw_ReturnsThreeDistinctCards()
        {
            CardPool<FakeCard> pool = CreatePool(Mudang,
                new FakeCard("a"), new FakeCard("b"), new FakeCard("c"), new FakeCard("d"), new FakeCard("e"));
            var results = new List<FakeCard>();

            int drawn = pool.Draw(_ => 0, results, 3, new SeededRandomSource(12345));

            Assert.That(drawn, Is.EqualTo(3));
            Assert.That(results, Has.Count.EqualTo(3));
            Assert.That(results, Is.Unique);
        }

        [Test]
        public void Draw_NeverRepeatsCard_AcrossManyRolls()
        {
            CardPool<FakeCard> pool = CreatePool(Mudang,
                new FakeCard("a"), new FakeCard("b"), new FakeCard("c"), new FakeCard("d"));
            var results = new List<FakeCard>();
            var random = new SeededRandomSource(777);

            for (int i = 0; i < 500; i++)
            {
                pool.Draw(_ => 0, results, 3, random);
                Assert.That(results, Is.Unique, $"{i}번째 추첨에서 중복 카드가 나왔습니다.");
            }
        }

        [Test]
        public void Draw_ReturnsFewerCards_WhenCandidatesAreScarce()
        {
            CardPool<FakeCard> pool = CreatePool(Mudang, new FakeCard("a"), new FakeCard("b"));
            var results = new List<FakeCard>();

            int drawn = pool.Draw(_ => 0, results, 3, new SeededRandomSource(1));

            Assert.That(drawn, Is.EqualTo(2));
        }

        [Test]
        public void Draw_ClearsPreviousResults()
        {
            CardPool<FakeCard> pool = CreatePool(Mudang, new FakeCard("a"));
            var results = new List<FakeCard> { new FakeCard("stale") };

            pool.Draw(_ => 0, results, 3, new SeededRandomSource(1));

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].CardId, Is.EqualTo("a"));
        }

        // ── 최대 레벨 제외 ──────────────────────────────────────────────────

        [Test]
        public void Draw_ExcludesCardsAtMaxLevel()
        {
            CardPool<FakeCard> pool = CreatePool(Mudang,
                new FakeCard("maxed", CardCategory.Skill, maxLevel: 3),
                new FakeCard("open", CardCategory.Skill, maxLevel: 3));
            var results = new List<FakeCard>();

            pool.Draw(id => id == "maxed" ? 3 : 0, results, 3, new SeededRandomSource(42));

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].CardId, Is.EqualTo("open"));
        }

        [Test]
        public void Draw_ReturnsNothing_WhenEveryCardIsMaxed()
        {
            CardPool<FakeCard> pool = CreatePool(Mudang, new FakeCard("a", maxLevel: 1), new FakeCard("b", maxLevel: 1));
            var results = new List<FakeCard>();

            int drawn = pool.Draw(_ => 1, results, 3, new SeededRandomSource(9));

            Assert.That(drawn, Is.Zero);
            Assert.That(results, Is.Empty);
        }

        // ── 캐릭터 전용 필터링 ──────────────────────────────────────────────

        [Test]
        public void Constructor_ExcludesCardsLockedToAnotherCharacter()
        {
            CardPool<FakeCard> pool = CreatePool(Baksu,
                new FakeCard("shared"),
                new FakeCard("fan", CardCategory.Skill, 3, Mudang));

            Assert.That(pool.Count, Is.EqualTo(1));
            Assert.That(pool.Cards[0].CardId, Is.EqualTo("shared"));
        }

        [Test]
        public void Constructor_KeepsCardsLockedToTheActiveCharacter()
        {
            CardPool<FakeCard> pool = CreatePool(Mudang,
                new FakeCard("shared"),
                new FakeCard("fan", CardCategory.Skill, 3, Mudang));

            Assert.That(pool.Count, Is.EqualTo(2));
        }

        [Test]
        public void Constructor_SkipsCharacterFilter_WhenCharacterIsUnknown()
        {
            // 캐릭터를 특정할 수 없을 때 전용 카드를 통째로 잃는 것보다 필터를 적용하지 않는 편이 안전하다.
            CardPool<FakeCard> pool = CreatePool(null, new FakeCard("fan", CardCategory.Skill, 3, Mudang));

            Assert.That(pool.Count, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_RemovesDuplicateDefinitions()
        {
            var card = new FakeCard("a");
            CardPool<FakeCard> pool = CreatePool(Mudang, card, card, new FakeCard("a"));

            Assert.That(pool.Count, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_SkipsNullAndIdlessEntries()
        {
            CardPool<FakeCard> pool = CreatePool(Mudang, null, new FakeCard(" "), new FakeCard("a"));

            Assert.That(pool.Count, Is.EqualTo(1));
            Assert.That(pool.Cards[0].CardId, Is.EqualTo("a"));
        }

        // ── 가중치 ──────────────────────────────────────────────────────────

        [Test]
        public void ResolveWeight_DoublesOwnedSkillAndGangshinCards()
        {
            Assert.That(CardPool<FakeCard>.ResolveWeight(new FakeCard("s", CardCategory.Skill), 1), Is.EqualTo(2f));
            Assert.That(CardPool<FakeCard>.ResolveWeight(new FakeCard("g", CardCategory.Gangshin), 2), Is.EqualTo(2f));
        }

        [Test]
        public void ResolveWeight_UsesBaseWeight_ForUnownedCards()
        {
            Assert.That(CardPool<FakeCard>.ResolveWeight(new FakeCard("s", CardCategory.Skill), 0), Is.EqualTo(1f));
        }

        [Test]
        public void ResolveWeight_KeepsBaseWeight_ForStatCards_EvenWhenOwned()
        {
            // 스탯 카드는 보유 개념이 없어 항상 x1(card_system.md — 가중치 규칙).
            Assert.That(CardPool<FakeCard>.ResolveWeight(new FakeCard("hp", CardCategory.Stat), 3), Is.EqualTo(1f));
        }

        [Test]
        public void Draw_PicksOwnedCard_TwiceAsOften()
        {
            var pool = CreatePool(Mudang,
                new FakeCard("owned", CardCategory.Skill, maxLevel: 99),
                new FakeCard("fresh", CardCategory.Skill, maxLevel: 99));
            var results = new List<FakeCard>();
            var random = new SeededRandomSource(2024);

            const int Trials = 30000;
            int ownedFirst = 0;

            for (int i = 0; i < Trials; i++)
            {
                // 1장만 뽑아 첫 선택의 분포를 본다. 보유(x2) : 미보유(x1) = 2 : 1 이 기대값이다.
                pool.Draw(id => id == "owned" ? 1 : 0, results, 1, random);
                if (results[0].CardId == "owned")
                {
                    ownedFirst++;
                }
            }

            float ratio = ownedFirst / (float)Trials;
            Assert.That(ratio, Is.EqualTo(2f / 3f).Within(0.02f), $"보유 카드 등장 비율이 기대(0.667)와 다릅니다: {ratio}");
        }

        [Test]
        public void Draw_HonoursWeightBoundaries_WithScriptedRolls()
        {
            // 후보 순서는 생성 순서와 같다. 가중치는 owned=2, fresh=1 → 총합 3.
            // 정규화 0.66(=1.98 < 2)은 owned 구간, 0.67(=2.01 >= 2)은 fresh 구간에 떨어진다.
            var pool = CreatePool(Mudang,
                new FakeCard("owned", CardCategory.Skill, maxLevel: 99),
                new FakeCard("fresh", CardCategory.Skill, maxLevel: 99));
            var results = new List<FakeCard>();

            pool.Draw(id => id == "owned" ? 1 : 0, results, 1, new ScriptedRandomSource(0.66f));
            Assert.That(results[0].CardId, Is.EqualTo("owned"));

            pool.Draw(id => id == "owned" ? 1 : 0, results, 1, new ScriptedRandomSource(0.67f));
            Assert.That(results[0].CardId, Is.EqualTo("fresh"));
        }

        // ── 적용 가능 여부 필터 ─────────────────────────────────────────────

        [Test]
        public void Draw_AppliesEligibilityFilter()
        {
            CardPool<FakeCard> pool = CreatePool(Mudang,
                new FakeCard("blocked", CardCategory.Gangshin),
                new FakeCard("allowed"));
            pool.EligibilityFilter = card => card.CardId != "blocked";
            var results = new List<FakeCard>();

            pool.Draw(_ => 0, results, 3, new SeededRandomSource(5));

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].CardId, Is.EqualTo("allowed"));
        }

        // ── SkillData 카테고리 파생 ─────────────────────────────────────────

        [Test]
        public void ResolveCategory_MapsStatEffectsToStatCategory()
        {
            Assert.That(SkillData.ResolveCategory(LevelUpSkillEffectType.StatFlat), Is.EqualTo(CardCategory.Stat));
            Assert.That(SkillData.ResolveCategory(LevelUpSkillEffectType.StatPercent), Is.EqualTo(CardCategory.Stat));
            Assert.That(SkillData.ResolveCategory(LevelUpSkillEffectType.BonusTargets), Is.EqualTo(CardCategory.Stat));
            Assert.That(SkillData.ResolveCategory(LevelUpSkillEffectType.PickupRadius), Is.EqualTo(CardCategory.Stat));
        }

        [Test]
        public void ResolveCategory_MapsGangshinBuffsToGangshinCategory()
        {
            Assert.That(SkillData.ResolveCategory(LevelUpSkillEffectType.SalPulliKummuBuff), Is.EqualTo(CardCategory.Gangshin));
            Assert.That(SkillData.ResolveCategory(LevelUpSkillEffectType.PaCheonJingBuff), Is.EqualTo(CardCategory.Gangshin));
        }

        [Test]
        public void ResolveCategory_MapsRemainingEffectsToSkillCategory()
        {
            Assert.That(SkillData.ResolveCategory(LevelUpSkillEffectType.SummonDokkaebiOrb), Is.EqualTo(CardCategory.Skill));
            Assert.That(SkillData.ResolveCategory(LevelUpSkillEffectType.HealthRegen), Is.EqualTo(CardCategory.Skill));
            Assert.That(SkillData.ResolveCategory(LevelUpSkillEffectType.FanAttackBuff), Is.EqualTo(CardCategory.Skill));
        }
    }
}
