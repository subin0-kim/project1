namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 보스 패턴의 카운터(파훼) 입력 종류(#69).
    /// 요구 방향은 <see cref="MountainKingBossController"/>가 이 종류에 따라 해석한다.
    /// </summary>
    public enum BossCounterType
    {
        /// <summary>카운터 불가 — 미처리 시 피해만 수령(포효).</summary>
        None = 0,

        /// <summary>보스의 현재 방향 속성으로 카운터(돌진).</summary>
        BossDirection = 1,

        /// <summary>패턴이 예고 시점에 굴린 방향으로 카운터. 인디케이터에 표시되는 방향과 동일(발톱 할퀴기).</summary>
        PatternDirection = 2,
    }
}
