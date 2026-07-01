using Mukseon.Core.Input;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    public class TapGestureRecognizerTests
    {
        private const float TravelThreshold = 20f;
        private const float DoubleTapInterval = 0.3f;

        private static TapGestureRecognizer CreateRecognizer()
            => new TapGestureRecognizer(TravelThreshold, DoubleTapInterval);

        // 이동 거리가 임계값 미만이면 탭으로 분류된다.
        [Test]
        public void Press_BelowTravelThreshold_IsTap()
        {
            var recognizer = CreateRecognizer();

            var result = recognizer.RegisterPress(new Vector2(100f, 100f), new Vector2(110f, 105f), 1f); // ≈11px

            Assert.That(result, Is.EqualTo(TapGestureRecognizer.Gesture.Tap));
        }

        // 이동 거리가 임계값 이상이면 드래그(스와이프)로 보고 탭으로 인식하지 않는다(None).
        [Test]
        public void Press_AtOrAboveTravelThreshold_IsNone()
        {
            var recognizer = CreateRecognizer();

            var result = recognizer.RegisterPress(new Vector2(100f, 100f), new Vector2(125f, 100f), 1f); // 25px

            Assert.That(result, Is.EqualTo(TapGestureRecognizer.Gesture.None));
        }

        // 임계값과 정확히 같은 이동 거리는 드래그(None) 쪽으로 분류된다(>= 경계).
        [Test]
        public void Press_ExactlyAtThreshold_IsNone()
        {
            var recognizer = CreateRecognizer();

            var result = recognizer.RegisterPress(Vector2.zero, new Vector2(TravelThreshold, 0f), 1f);

            Assert.That(result, Is.EqualTo(TapGestureRecognizer.Gesture.None));
        }

        // 더블 탭 간격 이내의 두 번째 탭은 더블 탭으로 확정된다.
        [Test]
        public void TwoTapsWithinInterval_SecondIsDoubleTap()
        {
            var recognizer = CreateRecognizer();

            Assert.That(recognizer.RegisterPress(Vector2.zero, Vector2.zero, 1.0f),
                Is.EqualTo(TapGestureRecognizer.Gesture.Tap));
            Assert.That(recognizer.RegisterPress(Vector2.zero, Vector2.zero, 1.2f), // 0.2s ≤ 0.3s
                Is.EqualTo(TapGestureRecognizer.Gesture.DoubleTap));
        }

        // 더블 탭 간격을 넘어선 두 번째 탭은 다시 단일 탭이다.
        [Test]
        public void TwoTapsBeyondInterval_BothAreSingleTaps()
        {
            var recognizer = CreateRecognizer();

            Assert.That(recognizer.RegisterPress(Vector2.zero, Vector2.zero, 1.0f),
                Is.EqualTo(TapGestureRecognizer.Gesture.Tap));
            Assert.That(recognizer.RegisterPress(Vector2.zero, Vector2.zero, 1.5f), // 0.5s > 0.3s
                Is.EqualTo(TapGestureRecognizer.Gesture.Tap));
        }

        // 탭과 탭 사이에 드래그가 끼면 더블 탭 추적이 끊겨, 이어지는 탭은 단일 탭이 된다.
        [Test]
        public void DragBetweenTaps_BreaksDoubleTap()
        {
            var recognizer = CreateRecognizer();

            recognizer.RegisterPress(Vector2.zero, Vector2.zero, 1.0f);                 // 탭
            recognizer.RegisterPress(Vector2.zero, new Vector2(50f, 0f), 1.1f);         // 드래그 → None, 추적 끊김
            var result = recognizer.RegisterPress(Vector2.zero, Vector2.zero, 1.15f);   // 단일 탭이어야 함

            Assert.That(result, Is.EqualTo(TapGestureRecognizer.Gesture.Tap));
        }

        // 세 번 연속 탭은 (탭 → 더블 탭 → 탭)이 되어, 한 번에 두 번의 더블 탭이 나오지 않는다.
        [Test]
        public void TripleTap_DoesNotProduceTwoDoubleTaps()
        {
            var recognizer = CreateRecognizer();

            Assert.That(recognizer.RegisterPress(Vector2.zero, Vector2.zero, 1.0f),
                Is.EqualTo(TapGestureRecognizer.Gesture.Tap));
            Assert.That(recognizer.RegisterPress(Vector2.zero, Vector2.zero, 1.1f),
                Is.EqualTo(TapGestureRecognizer.Gesture.DoubleTap));
            Assert.That(recognizer.RegisterPress(Vector2.zero, Vector2.zero, 1.2f),
                Is.EqualTo(TapGestureRecognizer.Gesture.Tap));
        }

        // Reset 후에는 직전 탭 추적이 사라져, 간격이 짧아도 더블 탭이 아니라 단일 탭이 된다(#42).
        [Test]
        public void Reset_ClearsDoubleTapTracking()
        {
            var recognizer = CreateRecognizer();

            recognizer.RegisterPress(Vector2.zero, Vector2.zero, 1.0f); // 탭
            recognizer.Reset();
            var result = recognizer.RegisterPress(Vector2.zero, Vector2.zero, 1.1f); // 리셋 없으면 더블 탭이었을 입력

            Assert.That(result, Is.EqualTo(TapGestureRecognizer.Gesture.Tap));
        }

        // 시간이 역전되어 두 번째 탭의 시각이 더 과거이면(비단조적 시각 주입), 음수 간격을 더블 탭으로 오인하지 않는다.
        [Test]
        public void NonMonotonicTime_DoesNotProduceDoubleTap()
        {
            var recognizer = CreateRecognizer();

            Assert.That(recognizer.RegisterPress(Vector2.zero, Vector2.zero, 5.0f),
                Is.EqualTo(TapGestureRecognizer.Gesture.Tap));
            // 두 번째 탭이 더 과거 시각(1.0초)으로 들어와도 더블 탭이 아니라 단일 탭으로 처리되어야 한다.
            Assert.That(recognizer.RegisterPress(Vector2.zero, Vector2.zero, 1.0f),
                Is.EqualTo(TapGestureRecognizer.Gesture.Tap));
        }
    }
}
