using System.Collections.Generic;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class MountainKingBossPatternDataTests
    {
        private static BossPhaseDefinition MakePhase(float min, float max, params BossPatternType[] types)
        {
            var patterns = new List<BossPatternDefinition>();
            foreach (BossPatternType type in types)
            {
                patterns.Add(new BossPatternDefinition(type, BossCounterType.BossDirection));
            }

            return new BossPhaseDefinition(patterns, min, max);
        }

        [Test]
        public void IsValid_RequiresAtLeastOnePhaseWithPatterns()
        {
            var data = ScriptableObject.CreateInstance<MountainKingBossPatternData>();

            try
            {
                data.ConfigureForTests(new List<BossPhaseDefinition>());
                Assert.That(data.IsValid(out _), Is.False, "빈 페이즈 목록은 무효여야 한다.");

                data.ConfigureForTests(new List<BossPhaseDefinition> { MakePhase(8f, 10f) });
                Assert.That(data.IsValid(out _), Is.False, "패턴 없는 페이즈는 무효여야 한다.");

                data.ConfigureForTests(new List<BossPhaseDefinition>
                {
                    MakePhase(8f, 10f, BossPatternType.Charge, BossPatternType.Roar)
                });
                Assert.That(data.IsValid(out _), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void GetPhase_ClampsOutOfRangeToLastPhase()
        {
            var data = ScriptableObject.CreateInstance<MountainKingBossPatternData>();

            try
            {
                BossPhaseDefinition p1 = MakePhase(8f, 10f, BossPatternType.Charge);
                BossPhaseDefinition p2 = MakePhase(5f, 7f, BossPatternType.ClawSwipe);
                data.ConfigureForTests(new List<BossPhaseDefinition> { p1, p2 });

                Assert.That(data.GetPhase(0), Is.SameAs(p1));
                Assert.That(data.GetPhase(1), Is.SameAs(p2));
                Assert.That(data.GetPhase(5), Is.SameAs(p2), "범위를 벗어나면 마지막 페이즈로 폴백.");
                Assert.That(data.GetPhase(-3), Is.SameAs(p1), "음수는 첫 페이즈로 클램프.");
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void GetRandomCycleInterval_StaysWithinPhaseBounds()
        {
            var data = ScriptableObject.CreateInstance<MountainKingBossPatternData>();

            try
            {
                data.ConfigureForTests(new List<BossPhaseDefinition>
                {
                    MakePhase(8f, 10f, BossPatternType.Charge)
                });

                for (int i = 0; i < 50; i++)
                {
                    float interval = data.GetRandomCycleInterval(0);
                    Assert.That(interval, Is.InRange(8f, 10f));
                }
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void CycleInterval_MaxClampedToMin_WhenInverted()
        {
            // min=10, max=5 → max는 min으로 끌어올려져 항상 10이 나와야 한다.
            BossPhaseDefinition phase = MakePhase(10f, 5f, BossPatternType.Charge);

            Assert.That(phase.CycleIntervalMinSeconds, Is.EqualTo(10f));
            Assert.That(phase.CycleIntervalMaxSeconds, Is.EqualTo(10f));
        }

        [Test]
        public void CounterBonusMultiplier_ClampedToAtLeastOne()
        {
            var data = ScriptableObject.CreateInstance<MountainKingBossPatternData>();

            try
            {
                data.ConfigureForTests(
                    new List<BossPhaseDefinition> { MakePhase(8f, 10f, BossPatternType.Charge) },
                    counterBonusMultiplier: 0.2f);

                Assert.That(data.CounterBonusMultiplier, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }
    }
}
