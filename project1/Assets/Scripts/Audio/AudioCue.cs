namespace Mukseon.Audio
{
    /// <summary>
    /// 효과음(SFX) 재생 지점(#38). 호출부는 클립이 아니라 이 큐만 알면 되므로,
    /// 나중에 임시 클립을 실제 클립으로 교체해도 게임 코드는 한 줄도 바뀌지 않는다.
    ///
    /// 값을 추가할 때는 <c>AudioLibrary</c> 에셋에도 항목을 넣어야 한다 —
    /// 누락되면 <c>AudioLibraryTests</c>가 잡는다.
    /// </summary>
    public enum AudioCue
    {
        None = 0,

        /// <summary>스와이프 공격 발사. 대나무를 베는 듯한 마찰음(Swoosh).</summary>
        Swipe = 1,

        /// <summary>적 피격. 먹물이 종이에 튀는 듯한 짧은 타격음.</summary>
        EnemyHit = 2,

        /// <summary>적 처치. 피격보다 낮고 길게 번지는 소리.</summary>
        EnemyDeath = 3,

        /// <summary>혼불 획득. 맑은 방울/종 소리.</summary>
        SoulCollect = 4,

        /// <summary>레벨업(스킬 선택 열림). 짧은 팡파르.</summary>
        LevelUp = 5,

        /// <summary>강신 발동. 징 소리.</summary>
        GangshinActivate = 6,
    }
}
