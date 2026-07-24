namespace Mukseon.Audio
{
    /// <summary>
    /// 배경음악 트랙(#38). 씬이 아니라 "상황"을 가리킨다 — 전투 씬 안에서도 보스 구간에 들어가면
    /// 트랙이 바뀌어야 하므로 씬 이름으로는 표현할 수 없다.
    /// </summary>
    public enum BgmTrack
    {
        /// <summary>무음(정지).</summary>
        None = 0,

        /// <summary>타이틀·캐릭터 선택. 적막하고 고요한 분위기.</summary>
        Lobby = 1,

        /// <summary>일반 전투. 타악 리듬 중심.</summary>
        Battle = 2,

        /// <summary>보스전. 무겁고 웅장하게.</summary>
        Boss = 3,
    }
}
