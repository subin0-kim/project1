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

        // 비활성화 시 더블 탭 추적이 리셋되는지 검증한다(#42). 리셋이 없으면 일시정지 직전 들어온 탭이
        // 재개 직후 첫 탭만으로 더블 탭 조건을 충족해 강신이 오발동될 수 있다.
        // 더블 탭 판정은 TapGestureRecognizer가 담당하므로, EditMode에서 Awake가 호출되지 않는 점을 감안해
        // 인식기를 리플렉션으로 주입한 뒤 동작으로 리셋 여부를 확인한다.
        [Test]
        public void SetInputEnabled_False_ResetsDoubleTapTracking()
        {
            var go = new GameObject("GangshinInputDetectorTest");
            try
            {
                var detector = go.AddComponent<GangshinInputDetector>();

                var recognizer = new TapGestureRecognizer(20f, 0.3f);
                var field = typeof(GangshinInputDetector).GetField(
                    "_recognizer",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, "_recognizer 필드를 찾을 수 없습니다.");
                field.SetValue(detector, recognizer);

                // 첫 탭으로 더블 탭 후보 상태를 만든다.
                recognizer.RegisterPress(Vector2.zero, Vector2.zero, 1f);

                detector.SetInputEnabled(false);

                // 리셋되었으므로 곧바로 들어온 탭은 더블 탭이 아니라 단일 탭이어야 한다.
                var result = recognizer.RegisterPress(Vector2.zero, Vector2.zero, 1.1f);
                Assert.That(result, Is.EqualTo(TapGestureRecognizer.Gesture.Tap));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
