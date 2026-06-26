using Mukseon.Core.Input;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class GangshinInputDetectorTests
    {
        [Test]
        public void IsInputEnabled_DefaultsToTrue()
        {
            var go = new GameObject("GangshinInputDetectorTest");
            try
            {
                var detector = go.AddComponent<GangshinInputDetector>();
                Assert.That(detector.IsInputEnabled, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SetInputEnabled_TogglesIsInputEnabled()
        {
            var go = new GameObject("GangshinInputDetectorTest");
            try
            {
                var detector = go.AddComponent<GangshinInputDetector>();

                detector.SetInputEnabled(false);
                Assert.That(detector.IsInputEnabled, Is.False);

                detector.SetInputEnabled(true);
                Assert.That(detector.IsInputEnabled, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // 비활성화 → 재활성화 시 더블 탭 추적 상태(_lastTapTime)가 리셋되는지 검증한다.
        // 리셋이 없으면 일시정지 직전 들어온 탭이 재개 직후 첫 탭만으로 더블 탭 조건을 충족해
        // 강신이 오발동될 수 있다(#42). _lastTapTime은 private이므로 리플렉션으로 확인한다.
        [Test]
        public void SetInputEnabled_False_ResetsLastTapTime()
        {
            var go = new GameObject("GangshinInputDetectorTest");
            try
            {
                var detector = go.AddComponent<GangshinInputDetector>();

                var field = typeof(GangshinInputDetector).GetField(
                    "_lastTapTime",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, "_lastTapTime 필드를 찾을 수 없습니다.");

                // 직전 탭 시각이 기록된 상황을 모사한다.
                field.SetValue(detector, 5f);

                detector.SetInputEnabled(false);

                Assert.That((float)field.GetValue(detector), Is.EqualTo(float.NegativeInfinity));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
