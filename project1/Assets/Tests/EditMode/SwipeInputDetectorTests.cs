using NUnit.Framework;
using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class SwipeInputDetectorTests
    {
        [Test]
        public void TryResolveSwipe_Returns_None_When_Distance_Is_Below_Threshold()
        {
            var go = new GameObject("SwipeInputDetectorTest");
            try
            {
                var detector = go.AddComponent<SwipeInputDetector>();
                SwipeDirection direction = detector.TryResolveSwipe(Vector2.zero, new Vector2(10f, 10f));
                Assert.That(direction, Is.EqualTo(SwipeDirection.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [TestCase(0f, 200f, SwipeDirection.Up)]
        [TestCase(0f, -200f, SwipeDirection.Down)]
        [TestCase(-200f, 0f, SwipeDirection.Left)]
        [TestCase(200f, 0f, SwipeDirection.Right)]
        public void TryResolveSwipe_Returns_Expected_Cardinal_Direction(float x, float y, SwipeDirection expected)
        {
            var go = new GameObject("SwipeInputDetectorTest");
            try
            {
                var detector = go.AddComponent<SwipeInputDetector>();
                SwipeDirection direction = detector.TryResolveSwipe(Vector2.zero, new Vector2(x, y));
                Assert.That(direction, Is.EqualTo(expected));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
