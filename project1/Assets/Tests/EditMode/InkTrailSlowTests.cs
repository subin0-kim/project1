using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class InkTrailSlowTests
    {
        private GameObject _go;
        private InkTrailSlowSkill _skill;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("InkTrailSlowSkill");
            _skill = _go.AddComponent<InkTrailSlowSkill>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void NotOwned_DoesNotTrigger()
        {
            // 레벨 0 = 미보유. roll 0(무조건 시도)에도 발동하지 않아야 한다.
            bool triggered = _skill.TryRollSlow(0f, out InkTrailSlowSkill.SlowSpec spec);

            Assert.That(_skill.Level, Is.EqualTo(0));
            Assert.That(triggered, Is.False);
            Assert.That(spec.Duration, Is.EqualTo(0f));
        }

        [Test]
        public void Level1_TriggersBelowChance_WithCorrectSpec()
        {
            _skill.ApplyLevel(1);

            bool triggered = _skill.TryRollSlow(0f, out InkTrailSlowSkill.SlowSpec spec);

            Assert.That(triggered, Is.True);
            // Lv1: 감속률 30% → 배수 0.7, 지속 2.0초.
            Assert.That(spec.SlowMultiplier, Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(spec.Duration, Is.EqualTo(2.0f).Within(1e-4f));
        }

        [Test]
        public void Level1_DoesNotTriggerAtOrAboveChance()
        {
            _skill.ApplyLevel(1);

            // roll 0.99는 Lv1 확률(0.3)보다 크므로 미발동.
            bool triggered = _skill.TryRollSlow(0.99f, out _);

            Assert.That(triggered, Is.False);
        }

        [Test]
        public void Level3_HasStrongerSlowAndLongerDuration()
        {
            _skill.ApplyLevel(3);

            bool triggered = _skill.TryRollSlow(0.55f, out InkTrailSlowSkill.SlowSpec spec);

            // Lv3: 확률 0.6 → roll 0.55 발동. 감속률 50% → 배수 0.5, 지속 3.0초.
            Assert.That(triggered, Is.True);
            Assert.That(spec.SlowMultiplier, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(spec.Duration, Is.EqualTo(3.0f).Within(1e-4f));
        }

        [Test]
        public void Level2_BoundaryRollIsExclusive()
        {
            _skill.ApplyLevel(2);

            // 확률 0.45 경계: roll == chance는 미발동(roll < chance 조건).
            Assert.That(_skill.TryRollSlow(0.45f, out _), Is.False);
            Assert.That(_skill.TryRollSlow(0.44f, out _), Is.True);
        }

        [Test]
        public void ApplyLevel_ClampsToMaxLevel()
        {
            _skill.ApplyLevel(99);
            Assert.That(_skill.Level, Is.EqualTo(InkTrailSlowSkill.MaxLevel));

            _skill.ApplyLevel(-5);
            Assert.That(_skill.Level, Is.EqualTo(0));
        }
    }
}
