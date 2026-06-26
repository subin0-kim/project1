using System;

namespace Mukseon.Core.Input
{
    /// <summary>
    /// 입력을 일시적으로 막는 원인. 게임오버와 레벨업 스킬 선택은 서로 독립적으로 발생할 수 있으므로
    /// 비트 플래그로 두어, 한 원인이 해제되어도 다른 원인이 남아 있으면 입력을 계속 막을 수 있게 한다.
    /// </summary>
    [Flags]
    public enum InputSuppressionReason
    {
        None = 0,
        GameOver = 1 << 0,
        LevelUpSelection = 1 << 1,
    }

    /// <summary>
    /// 여러 게임 상태(게임오버, 레벨업 선택 등)가 각각 입력을 막을 수 있으므로, 활성화된 모든 원인을
    /// 비트 플래그로 누적한다. 원인이 하나도 없을 때만 입력이 활성 상태(<see cref="IsInputEnabled"/>)다.
    /// MonoBehaviour에 의존하지 않는 순수 로직이라 단위 테스트가 용이하다(#42).
    /// </summary>
    public sealed class InputSuppressionState
    {
        private InputSuppressionReason _activeReasons;

        public InputSuppressionReason ActiveReasons => _activeReasons;

        /// <summary>활성화된 억제 원인이 하나도 없으면 true.</summary>
        public bool IsInputEnabled => _activeReasons == InputSuppressionReason.None;

        /// <summary>
        /// 특정 원인을 켜거나 끈다. 그 결과 입력 활성/비활성 상태가 실제로 바뀌었을 때만 true를 반환한다.
        /// (구독자가 상태가 바뀐 순간에만 감지기에 반영하도록 하기 위함)
        /// </summary>
        public bool SetReason(InputSuppressionReason reason, bool active)
        {
            if (reason == InputSuppressionReason.None)
            {
                return false;
            }

            bool wasEnabled = IsInputEnabled;

            if (active)
            {
                _activeReasons |= reason;
            }
            else
            {
                _activeReasons &= ~reason;
            }

            return wasEnabled != IsInputEnabled;
        }

        /// <summary>모든 억제 원인을 해제한다(씬 재시작 등). 상태가 바뀌었으면 true를 반환한다.</summary>
        public bool Clear()
        {
            bool wasEnabled = IsInputEnabled;
            _activeReasons = InputSuppressionReason.None;
            return wasEnabled != IsInputEnabled;
        }
    }
}
