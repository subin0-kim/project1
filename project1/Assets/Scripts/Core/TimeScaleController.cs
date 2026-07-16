using UnityEngine;

namespace Mukseon.Core
{
    /// <summary>
    /// <see cref="Time.timeScale"/>의 목표값을 계산하는 순수 로직(#109).
    ///
    /// 핵심은 <b>정지(pause)와 감속(rate)을 분리</b>하는 것이다. 종래에는 게임오버(0)·레벨업(0)·
    /// 강신 히트스톱(0.05~1) 세 시스템이 각자 "이전 값을 저장했다가 복원"하는 방식으로 같은 전역을
    /// 직접 써서, 겹치면 마지막에 복원한 쪽이 이기는 lost-update가 발생했다. 예를 들어 게임오버로
    /// 정지한 상태에서 강신이 끝나면 강신이 기억해 둔 1f로 정지가 풀렸다.
    ///
    /// 정지는 원인 플래그의 합산이고 감속은 별개의 배율이므로, 목표값은 다음 한 줄로 결정된다:
    /// <code>TargetTimeScale = IsPaused ? 0 : Rate</code>
    /// 정지 중에 감속이 1f로 복원되어도 <see cref="IsPaused"/>가 참인 한 0이 유지되고, 정지 원인이
    /// 모두 사라져야 비로소 감속 배율이 드러난다. 복원 순서에 의존하지 않으므로 lost-update가 구조적으로
    /// 성립하지 않는다.
    ///
    /// MonoBehaviour에 의존하지 않아 단위 테스트가 용이하다(<see cref="Input.InputSuppressionState"/>와 동일한 설계).
    /// </summary>
    public sealed class TimeScaleController
    {
        /// <summary>감속 배율 하한. 0으로 내려가면 정지와 구분되지 않으므로 막는다.</summary>
        public const float MinRate = 0.05f;

        /// <summary>감속 배율 상한(= 등속).</summary>
        public const float MaxRate = 1f;

        private PauseReason _activeReasons;
        private float _rate = MaxRate;

        public PauseReason ActiveReasons => _activeReasons;

        /// <summary>정지 원인이 하나라도 있으면 true.</summary>
        public bool IsPaused => _activeReasons != PauseReason.None;

        /// <summary>정지가 아닐 때 적용될 감속 배율(강신 히트스톱 등).</summary>
        public float Rate => _rate;

        /// <summary>현재 상태에서 <see cref="Time.timeScale"/>에 적용되어야 할 값.</summary>
        public float TargetTimeScale => IsPaused ? 0f : _rate;

        /// <summary>
        /// 특정 정지 원인을 켜거나 끈다. 그 결과 <see cref="TargetTimeScale"/>이 실제로 바뀌었을 때만
        /// true를 반환한다(호출 측이 바뀐 순간에만 엔진에 반영하도록).
        /// </summary>
        public bool SetReason(PauseReason reason, bool active)
        {
            if (reason == PauseReason.None)
            {
                return false;
            }

            float before = TargetTimeScale;

            if (active)
            {
                _activeReasons |= reason;
            }
            else
            {
                _activeReasons &= ~reason;
            }

            return !Mathf.Approximately(before, TargetTimeScale);
        }

        /// <summary>
        /// 감속 배율을 설정한다(<see cref="MinRate"/>~<see cref="MaxRate"/>로 클램프).
        /// <see cref="TargetTimeScale"/>이 바뀌었을 때만 true를 반환하므로, 정지 중 호출은 false다.
        /// </summary>
        public bool SetRate(float rate)
        {
            float before = TargetTimeScale;
            _rate = Mathf.Clamp(rate, MinRate, MaxRate);
            return !Mathf.Approximately(before, TargetTimeScale);
        }

        /// <summary>
        /// 모든 정지 원인과 감속을 초기 상태(등속)로 되돌린다. 씬 재시작·플레이 모드 진입 시 사용한다.
        /// <see cref="TargetTimeScale"/>이 바뀌었으면 true를 반환한다.
        /// </summary>
        public bool Reset()
        {
            float before = TargetTimeScale;
            _activeReasons = PauseReason.None;
            _rate = MaxRate;
            return !Mathf.Approximately(before, TargetTimeScale);
        }
    }
}
