using UnityEngine;

namespace Mukseon.Core
{
    /// <summary>
    /// <see cref="Time.timeScale"/>의 유일한 소유자(#109). 게임오버·레벨업·강신·화면 전환은 전역에 직접
    /// 대입하지 말고 반드시 이 파사드를 경유한다. 합성 규칙은 <see cref="TimeScaleController"/> 참고.
    /// </summary>
    public static class TimeScaleService
    {
        private static readonly TimeScaleController _controller = new TimeScaleController();

        public static bool IsPaused => _controller.IsPaused;

        public static PauseReason ActiveReasons => _controller.ActiveReasons;

        /// <summary>
        /// 플레이 모드 진입 시 시간 배율을 등속으로 강제한다(#109).
        ///
        /// <see cref="Time.timeScale"/>은 매니지드 static이 아니라 네이티브 엔진 전역이라, 에디터가
        /// 플레이 종료/재진입 시 1f로 되돌려주지 않는다. 반면 <see cref="_controller"/> 같은 매니지드
        /// 상태는 도메인 리로드로 초기화된다. 이 비대칭 때문에 "게임오버로 0f가 된 채 플레이를 중지하면
        /// 다음 플레이에서 런이 시작되지 않는" 증상이 생긴다 — 컨트롤러는 정지 원인이 없다고 보고하는데
        /// 엔진은 여전히 0f인 불일치 상태다. 그래서 여기서는 상태 변화 여부와 무관하게 무조건 반영한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            _controller.Reset();
            Apply();
        }

        /// <summary>정지 원인을 켜거나 끈다. 원인이 하나라도 남아 있으면 정지가 유지된다.</summary>
        public static void SetPause(PauseReason reason, bool active)
        {
            if (_controller.SetReason(reason, active))
            {
                Apply();
            }
        }

        /// <summary>
        /// 감속 배율을 설정한다(강신 히트스톱 등). 정지 중이라면 정지가 우선하므로 즉시 반영되지 않고,
        /// 정지가 풀린 뒤에 드러난다.
        /// </summary>
        public static void SetRate(float rate)
        {
            if (_controller.SetRate(rate))
            {
                Apply();
            }
        }

        /// <summary>모든 정지 원인과 감속을 등속으로 되돌린다(씬 재시작 등).</summary>
        public static void Reset()
        {
            _controller.Reset();
            Apply();
        }

        private static void Apply()
        {
            Time.timeScale = _controller.TargetTimeScale;
        }
    }
}
