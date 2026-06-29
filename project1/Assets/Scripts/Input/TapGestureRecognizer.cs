using UnityEngine;

namespace Mukseon.Core.Input
{
    /// <summary>
    /// 한 번의 누름→뗌(press→release) 제스처를 탭/드래그로 분류하고, 연속된 탭의 더블 탭 여부를 판정하는
    /// 순수 로직. MonoBehaviour나 UnityEngine.Input에 의존하지 않아 단위 테스트가 용이하다(#60).
    /// 시간은 호출자가 전달한다(보통 Time.unscaledTime — 일시정지 중에도 일관되게 동작하도록).
    /// </summary>
    public sealed class TapGestureRecognizer
    {
        public enum Gesture
        {
            None = 0,      // 탭이 아님(이동 거리가 임계값 이상 = 드래그/스와이프)
            Tap = 1,       // 단일 탭(더블 탭 후보)
            DoubleTap = 2  // 더블 탭 확정
        }

        // 누름→뗌 이동 거리가 이 값(px) 미만이면 탭, 이상이면 드래그로 본다(#60: 분기 임계값 20px).
        private readonly float _tapTravelThreshold;

        // 직전 탭과 다음 탭 사이 간격이 이 값(초) 이하이면 더블 탭으로 인식한다(#60: 300ms).
        private readonly float _doubleTapInterval;

        // 직전에 인식된 단일 탭의 시각. 더블 탭이 확정되거나 Reset되면 음의 무한대로 초기화한다.
        private float _lastTapTime = float.NegativeInfinity;

        public TapGestureRecognizer(float tapTravelThreshold, float doubleTapInterval)
        {
            _tapTravelThreshold = Mathf.Max(0f, tapTravelThreshold);
            _doubleTapInterval = Mathf.Max(0f, doubleTapInterval);
        }

        public float TapTravelThreshold => _tapTravelThreshold;
        public float DoubleTapInterval => _doubleTapInterval;

        /// <summary>
        /// 완료된 누름→뗌 제스처 하나를 평가한다.
        /// 이동 거리가 임계값 이상이면 드래그로 보고 <see cref="Gesture.None"/>을 반환하며, 더블 탭 추적도 끊는다
        /// (드래그가 직전 탭과 묶여 더블 탭으로 오인되지 않도록).
        /// </summary>
        /// <param name="startPosition">누름 시작 스크린 좌표</param>
        /// <param name="endPosition">뗀 순간 스크린 좌표</param>
        /// <param name="endTime">뗀 시각(초). 보통 Time.unscaledTime.</param>
        public Gesture RegisterPress(Vector2 startPosition, Vector2 endPosition, float endTime)
        {
            // 임계값 이상 이동 → 스와이프(드래그)로 분류. 탭이 아니므로 더블 탭 후보 추적을 끊는다.
            if (Vector2.Distance(startPosition, endPosition) >= _tapTravelThreshold)
            {
                _lastTapTime = float.NegativeInfinity;
                return Gesture.None;
            }

            // 직전 탭과의 간격이 더블 탭 허용 범위 안이면 더블 탭 확정.
            // (_lastTapTime이 음의 무한대인 첫 탭은 간격이 +무한대가 되어 이 분기를 타지 않는다.)
            // 시간이 역전되어(시각 리셋·비단조적 시각 주입 등) elapsed가 음수가 되면 더블 탭으로 오인하지
            // 않도록 0 이상인지도 함께 확인한다.
            float elapsed = endTime - _lastTapTime;
            if (elapsed >= 0f && elapsed <= _doubleTapInterval)
            {
                // 더블 탭 확정 후에는 추적을 끊어, 세 번째 탭이 또다시 더블 탭으로 이어지지 않게 한다.
                _lastTapTime = float.NegativeInfinity;
                return Gesture.DoubleTap;
            }

            // 단일 탭: 다음 탭과의 더블 탭 판정을 위해 시각을 기록한다.
            _lastTapTime = endTime;
            return Gesture.Tap;
        }

        /// <summary>
        /// 더블 탭 추적 상태를 초기화한다. 입력 비활성화/일시정지 경계를 넘는 탭이 재개 직후
        /// 첫 탭만으로 더블 탭을 충족해 오발동하지 않도록 한다(#42).
        /// </summary>
        public void Reset()
        {
            _lastTapTime = float.NegativeInfinity;
        }
    }
}
