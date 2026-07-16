using System;

namespace Mukseon.Core
{
    /// <summary>
    /// 게임 시간을 정지시키는 원인. 게임오버·레벨업 선택·화면 전환은 서로 겹칠 수 있으므로
    /// 비트 플래그로 누적한다. 한 원인이 해제되어도 다른 원인이 남아 있으면 정지를 유지한다.
    /// </summary>
    [Flags]
    public enum PauseReason
    {
        None = 0,
        GameOver = 1 << 0,
        LevelUpSelection = 1 << 1,
        ScreenTransition = 1 << 2,

        /// <summary>
        /// 결과 화면 표시 중. 정화 실패는 <see cref="GameOver"/>가 이미 정지시키지만, 정화 성공(챕터 클리어)은
        /// 아무도 정지시키지 않으므로 결과 화면이 스스로 정지를 소유해야 한다.
        /// </summary>
        ResultScreen = 1 << 3,
    }
}
