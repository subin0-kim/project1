using System.Collections.Generic;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// 적 첫 등장 기록기 검증(#70).
    ///
    /// 스폰은 초당 여러 번 일어나므로 '한 런에 한 번'이 깨지면 배너가 연달아 뜬다.
    /// 특히 최소 유지 수를 채우는 라운드에서는 같은 종류가 연속으로 소환되므로,
    /// 조회와 기록이 한 호출로 끝나는지가 핵심이다.
    /// </summary>
    public class EnemyIntroductionTrackerTests
    {
        private readonly List<MonsterData> _created = new List<MonsterData>();
        private EnemyIntroductionTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _tracker = new EnemyIntroductionTracker();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void FirstMark_ReturnsTrue()
        {
            MonsterData species = CreateSpecies();

            Assert.That(_tracker.TryMarkIntroduced(species), Is.True);
            Assert.That(_tracker.IntroducedCount, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedMarks_SameSpecies_OnlyFirstPasses()
        {
            MonsterData species = CreateSpecies();

            int passes = 0;
            for (int i = 0; i < 10; i++)
            {
                if (_tracker.TryMarkIntroduced(species))
                {
                    passes++;
                }
            }

            Assert.That(passes, Is.EqualTo(1), "같은 종류는 런 내 한 번만 통과해야 한다.");
            Assert.That(_tracker.IntroducedCount, Is.EqualTo(1));
        }

        [Test]
        public void DifferentSpecies_AreTrackedIndependently()
        {
            MonsterData sonakssi = CreateSpecies();
            MonsterData mangryang = CreateSpecies();

            Assert.That(_tracker.TryMarkIntroduced(sonakssi), Is.True);
            Assert.That(_tracker.TryMarkIntroduced(mangryang), Is.True, "다른 종류는 별개로 소개되어야 한다.");
            Assert.That(_tracker.TryMarkIntroduced(sonakssi), Is.False);
            Assert.That(_tracker.IntroducedCount, Is.EqualTo(2));
        }

        // MonsterData가 없는 엔트리는 표시 이름 문자열이 키가 된다(프리팹만 지정한 경우).
        [Test]
        public void StringKey_BehavesByValue()
        {
            Assert.That(_tracker.TryMarkIntroduced("Dokkaebibul"), Is.True);

            // 매 호출마다 새 인스턴스가 되는 상황을 흉내낸다 — 값이 같으면 같은 종류로 봐야 한다.
            string sameValue = string.Concat("Dokkaebi", "bul");
            Assert.That(_tracker.TryMarkIntroduced(sameValue), Is.False);
        }

        [Test]
        public void NullKey_IsIgnored()
        {
            Assert.That(_tracker.TryMarkIntroduced(null), Is.False);
            Assert.That(_tracker.IsIntroduced(null), Is.False);
            Assert.That(_tracker.IntroducedCount, Is.EqualTo(0));
        }

        [Test]
        public void IsIntroduced_DoesNotRecord()
        {
            MonsterData species = CreateSpecies();

            Assert.That(_tracker.IsIntroduced(species), Is.False);
            Assert.That(_tracker.IntroducedCount, Is.EqualTo(0), "조회만으로 기록되면 안 된다.");
            Assert.That(_tracker.TryMarkIntroduced(species), Is.True);
            Assert.That(_tracker.IsIntroduced(species), Is.True);
        }

        // 새 런(타이틀 → 재시작)에서는 모든 적이 다시 '처음'이어야 한다.
        [Test]
        public void Reset_ClearsRecord()
        {
            MonsterData species = CreateSpecies();
            _tracker.TryMarkIntroduced(species);

            _tracker.Reset();

            Assert.That(_tracker.IntroducedCount, Is.EqualTo(0));
            Assert.That(_tracker.TryMarkIntroduced(species), Is.True);
        }

        private MonsterData CreateSpecies()
        {
            MonsterData species = ScriptableObject.CreateInstance<MonsterData>();
            _created.Add(species);
            return species;
        }
    }
}
