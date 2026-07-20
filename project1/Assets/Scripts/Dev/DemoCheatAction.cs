namespace Mukseon.Dev
{
    /// <summary>
    /// 시연용 치트 종류(#111). 실제 실행은 <c>DemoCheatController</c>가, 키 매핑은
    /// <see cref="DemoCheatBindings"/>가 담당한다.
    /// </summary>
    public enum DemoCheatAction
    {
        None = 0,

        /// <summary>무적 토글. 시연 도중 사망으로 흐름이 끊기는 것을 막는다.</summary>
        ToggleInvincible = 1,

        /// <summary>즉시 레벨업. 레벨업 스킬 선택 UI를 바로 보여주기 위한 것.</summary>
        LevelUp = 2,

        /// <summary>화면 내 적 일괄 제거.</summary>
        KillEnemies = 3,

        /// <summary>강신 게이지를 채우고 즉시 발동.</summary>
        ActivateGangshin = 4,

        /// <summary>미장착 강신을 슬롯에 지급. 슬롯 교체 시연용.</summary>
        GrantGangshinSlot = 5,

        /// <summary>보스 구간으로 타임라인 점프. 10분 런을 기다리지 않기 위한 핵심 치트.</summary>
        SkipToBoss = 6,

        /// <summary>치트 안내 오버레이 표시 토글.</summary>
        ToggleOverlay = 7,
    }
}
