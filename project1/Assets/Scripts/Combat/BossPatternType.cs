namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 타락한 산군(#69)의 공격 패턴 종류. 1페이즈 핵심 3종만 정의한다.
    /// 2페이즈 신규 패턴(연속 할퀴기 / 광란 돌진 / 포효 강화)은 후속 커밋에서 추가한다.
    /// </summary>
    public enum BossPatternType
    {
        /// <summary>돌진 — 화면 바깥에서 중앙을 향해 직선 돌진. 방향 표시 후 발동.</summary>
        Charge = 0,

        /// <summary>발톱 할퀴기 — 한 방향으로 거대한 발톱을 뻗는다.</summary>
        ClawSwipe = 1,

        /// <summary>포효 — 부하 호랑이를 소환한다. 카운터 불가.</summary>
        Roar = 2,

        /// <summary>연속 할퀴기(2페이즈) — 여러 방향으로 연속 발톱 공격. 표시된 방향을 순서대로 스와이프해 파훼.</summary>
        SequenceClaw = 3,
    }
}
