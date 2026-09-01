namespace Mukseon.Gameplay.Progression.Shrine
{
    /// <summary>
    /// 신당 업그레이드 구매 시도의 결과(#34). 실패를 bool 하나로 뭉개지 않는 이유:
    /// 화면이 "골드가 부족합니다"와 "이미 최대입니다"를 구분해 보여줘야 하고,
    /// 저장 실패는 유저가 다시 시도해야 할 전혀 다른 상황이다.
    /// </summary>
    public enum ShrinePurchaseResult
    {
        Success = 0,

        /// <summary>업그레이드가 null이거나 카탈로그·세이브가 준비되지 않았다.</summary>
        InvalidUpgrade = 1,

        /// <summary>이미 최대 레벨이다.</summary>
        MaxLevel = 2,

        /// <summary>보유 골드가 다음 레벨 비용에 미치지 못한다.</summary>
        NotEnoughGold = 3,

        /// <summary>차감·레벨 증가는 유효했으나 영속화에 실패해 되돌렸다.</summary>
        SaveFailed = 4,
    }
}
