using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class BossPatternDefinitionTests
    {
        [Test]
        public void Getters_ClampNegativeTimingsAndDamageToZero()
        {
            var pattern = new BossPatternDefinition(
                BossPatternType.Charge,
                BossCounterType.BossDirection,
                telegraphSeconds: -5f,
                executeSeconds: -1f,
                recoverSeconds: -2f,
                unhandledDamage: -10f,
                counterBonusDamage: -3f);

            Assert.That(pattern.TelegraphSeconds, Is.EqualTo(0f));
            Assert.That(pattern.ExecuteSeconds, Is.EqualTo(0f));
            Assert.That(pattern.RecoverSeconds, Is.EqualTo(0f));
            Assert.That(pattern.UnhandledDamage, Is.EqualTo(0f));
            Assert.That(pattern.CounterBonusDamage, Is.EqualTo(0f));
        }

        [Test]
        public void IndicatorOffset_IsPassedThroughUnclamped()
        {
            var offset = new Vector2(3f, -2f);
            var pattern = new BossPatternDefinition(
                BossPatternType.ClawSwipe,
                BossCounterType.PatternDirection,
                indicatorOffset: offset);

            Assert.That(pattern.IndicatorOffset, Is.EqualTo(offset));
        }

        [Test]
        public void IsCounterable_FalseOnlyForNone()
        {
            var none = new BossPatternDefinition(BossPatternType.Roar, BossCounterType.None);
            var bossDir = new BossPatternDefinition(BossPatternType.Charge, BossCounterType.BossDirection);
            var patternDir = new BossPatternDefinition(BossPatternType.ClawSwipe, BossCounterType.PatternDirection);
            var damage = new BossPatternDefinition(BossPatternType.Charge, BossCounterType.DamageThreshold);

            Assert.That(none.IsCounterable, Is.False);
            Assert.That(bossDir.IsCounterable, Is.True);
            Assert.That(patternDir.IsCounterable, Is.True);
            Assert.That(damage.IsCounterable, Is.True);
        }

        [Test]
        public void ShowsIndicator_TrueOnlyForPatternDirection()
        {
            var none = new BossPatternDefinition(BossPatternType.Roar, BossCounterType.None);
            var bossDir = new BossPatternDefinition(BossPatternType.Charge, BossCounterType.BossDirection);
            var patternDir = new BossPatternDefinition(BossPatternType.ClawSwipe, BossCounterType.PatternDirection);
            var damage = new BossPatternDefinition(BossPatternType.Charge, BossCounterType.DamageThreshold);

            Assert.That(none.ShowsIndicator, Is.False);
            Assert.That(bossDir.ShowsIndicator, Is.False);
            Assert.That(patternDir.ShowsIndicator, Is.True);
            Assert.That(damage.ShowsIndicator, Is.False);
        }

        [Test]
        public void CounterDamageThreshold_IsClampedToZero()
        {
            var positive = new BossPatternDefinition(
                BossPatternType.Charge,
                BossCounterType.DamageThreshold,
                counterDamageThreshold: 50f);
            var negative = new BossPatternDefinition(
                BossPatternType.Charge,
                BossCounterType.DamageThreshold,
                counterDamageThreshold: -10f);

            Assert.That(positive.CounterDamageThreshold, Is.EqualTo(50f));
            Assert.That(negative.CounterDamageThreshold, Is.EqualTo(0f));
        }

        [Test]
        public void ResolvedCounterWindow_FallsBackToTelegraphPlusExecute_WhenUnset()
        {
            var unset = new BossPatternDefinition(
                BossPatternType.Charge,
                BossCounterType.BossDirection,
                telegraphSeconds: 1f,
                executeSeconds: 0.5f,
                counterWindowSeconds: 0f);

            Assert.That(unset.ResolvedCounterWindowSeconds, Is.EqualTo(1.5f));

            var explicitWindow = new BossPatternDefinition(
                BossPatternType.Charge,
                BossCounterType.BossDirection,
                telegraphSeconds: 1f,
                executeSeconds: 0.5f,
                counterWindowSeconds: 2f);

            Assert.That(explicitWindow.ResolvedCounterWindowSeconds, Is.EqualTo(2f));
        }
    }
}
