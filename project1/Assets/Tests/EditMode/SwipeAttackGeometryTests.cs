using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class SwipeAttackGeometryTests
    {
        [TestCase(SwipeDirection.Up, 0f, 1f)]
        [TestCase(SwipeDirection.Down, 0f, -1f)]
        [TestCase(SwipeDirection.Left, -1f, 0f)]
        [TestCase(SwipeDirection.Right, 1f, 0f)]
        [TestCase(SwipeDirection.None, 0f, 0f)]
        public void ToVector_ReturnsExpectedDirection(SwipeDirection direction, float x, float y)
        {
            Vector2 result = SwipeAttackGeometry.ToVector(direction);
            Assert.That(result, Is.EqualTo(new Vector2(x, y)));
        }

        [Test]
        public void IsTargetWithinArc_ReturnsTrue_WhenTargetInFront()
        {
            bool result = SwipeAttackGeometry.IsTargetWithinArc(
                Vector2.zero,
                Vector2.right,
                new Vector2(5f, 0f),
                90f);

            Assert.That(result, Is.True);
        }

        [Test]
        public void IsTargetWithinArc_ReturnsFalse_WhenTargetOutsideArc()
        {
            bool result = SwipeAttackGeometry.IsTargetWithinArc(
                Vector2.zero,
                Vector2.right,
                new Vector2(0f, 5f),
                90f);

            Assert.That(result, Is.False);
        }
    }
}
