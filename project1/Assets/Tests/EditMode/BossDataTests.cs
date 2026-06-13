using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class BossDataTests
    {
        [Test]
        public void PhaseCount_IsThresholdCountPlusOne()
        {
            var data = ScriptableObject.CreateInstance<BossData>();

            try
            {
                data.ConfigureForTests(1000f, 0.66f, 0.33f);
                Assert.That(data.PhaseCount, Is.EqualTo(3));

                data.ConfigureForTests(1000f); // 임계값 없음 → 단일 페이즈
                Assert.That(data.PhaseCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void IsValid_AcceptsStrictlyDescendingThresholds()
        {
            var data = ScriptableObject.CreateInstance<BossData>();

            try
            {
                data.ConfigureForTests(1000f, 0.66f, 0.33f);
                Assert.That(data.IsValid(out _), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void IsValid_RejectsNonDescendingThresholds()
        {
            var data = ScriptableObject.CreateInstance<BossData>();

            try
            {
                data.ConfigureForTests(1000f, 0.33f, 0.66f); // 오름차순 — 무효
                Assert.That(data.IsValid(out string reason), Is.False);
                Assert.That(reason, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void IsValid_RejectsOutOfRangeThreshold()
        {
            var data = ScriptableObject.CreateInstance<BossData>();

            try
            {
                data.ConfigureForTests(1000f, 1.5f); // (0,1) 범위 밖 — 무효
                Assert.That(data.IsValid(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }
    }
}
