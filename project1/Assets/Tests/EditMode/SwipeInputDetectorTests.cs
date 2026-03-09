using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class SwipeInputDetectorTests
    {
        private const string DetectorTypeFullName = "Mukseon.Core.Input.SwipeInputDetector";

        [Test]
        public void TryResolveSwipe_Returns_None_When_Distance_Is_Below_Threshold()
        {
            Type detectorType = ResolveType(DetectorTypeFullName);
            Assert.That(detectorType, Is.Not.Null, $"Type not found: {DetectorTypeFullName}");

            var go = new GameObject("SwipeInputDetectorTest");
            try
            {
                var detector = go.AddComponent(detectorType);
                string direction = InvokeTryResolveSwipe(detectorType, detector, Vector2.zero, new Vector2(10f, 10f));
                Assert.That(direction, Is.EqualTo("None"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [TestCase(0f, 200f, "Up")]
        [TestCase(0f, -200f, "Down")]
        [TestCase(-200f, 0f, "Left")]
        [TestCase(200f, 0f, "Right")]
        public void TryResolveSwipe_Returns_Expected_Cardinal_Direction(float x, float y, string expected)
        {
            Type detectorType = ResolveType(DetectorTypeFullName);
            Assert.That(detectorType, Is.Not.Null, $"Type not found: {DetectorTypeFullName}");

            var go = new GameObject("SwipeInputDetectorTest");
            try
            {
                var detector = go.AddComponent(detectorType);
                string direction = InvokeTryResolveSwipe(detectorType, detector, Vector2.zero, new Vector2(x, y));
                Assert.That(direction, Is.EqualTo(expected));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static Type ResolveType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static string InvokeTryResolveSwipe(Type detectorType, Component detector, Vector2 start, Vector2 end)
        {
            MethodInfo method = detectorType.GetMethod("TryResolveSwipe", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "TryResolveSwipe method not found.");

            object result = method.Invoke(detector, new object[] { start, end });
            return result?.ToString();
        }
    }
}
