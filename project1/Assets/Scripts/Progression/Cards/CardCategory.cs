namespace Mukseon.Gameplay.Progression.Cards
{
    /// <summary>
    /// 강화 카드 카테고리(`card_system.md` — 카드 목록, #66).
    /// 추첨 가중치가 카테고리별로 다르므로(보유 중인 스킬/강신만 x2) 분류가 필요하다.
    /// </summary>
    public enum CardCategory
    {
        /// <summary>스탯 강화 카드 — 플레이어 기본 수치 영구 증가. 보유 여부와 무관하게 가중치 x1.</summary>
        Stat = 0,

        /// <summary>스킬 강화 카드 — 미보유 시 획득, 보유 시 레벨업. 보유 중이면 가중치 x2.</summary>
        Skill = 1,

        /// <summary>강신 강화 카드 — 강신 슬롯 추가 또는 레벨업. 보유 중이면 가중치 x2.</summary>
        Gangshin = 2,
    }
}
