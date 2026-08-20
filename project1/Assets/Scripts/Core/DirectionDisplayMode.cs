namespace Mukseon.Core
{
    /// <summary>
    /// 방향 속성 색상의 표시 방식(#83, `combat_system.md` §3 — 표시 방식).
    ///
    /// 세이브에 <b>정수로</b> 직렬화되므로 기존 항목의 번호를 바꾸면 세이브 호환이 깨진다.
    /// 새 항목은 뒤에 추가한다.
    /// </summary>
    public enum DirectionDisplayMode
    {
        /// <summary>외곽선 글로우만 — 적 스프라이트 테두리가 방향 색으로 발광한다.</summary>
        Glow = 0,

        /// <summary>색 오브만 — 적 머리 위 색 구슬로만 표시한다.</summary>
        Orb = 1,

        /// <summary>글로우 + 색 오브 둘 다(기본값).</summary>
        Both = 2,
    }
}
