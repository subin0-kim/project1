namespace Mukseon.Gameplay.Progression.Cards
{
    /// <summary>
    /// 강화 카드 추첨에 필요한 최소 정보(#66). <see cref="CardPool"/>이 ScriptableObject에 의존하지
    /// 않도록 분리한 인터페이스로, EditMode 테스트에서는 순수 C# 대역으로 대체할 수 있다.
    /// 표시 정보(이름/아이콘/설명)는 추첨에 쓰이지 않으므로 여기 포함하지 않는다.
    /// </summary>
    public interface ICardDefinition
    {
        /// <summary>카드 식별자. 보유 레벨 조회 키이자 중복 판정 키다.</summary>
        string CardId { get; }

        CardCategory Category { get; }

        /// <summary>이 레벨에 도달하면 풀에서 제외된다(1 이상).</summary>
        int MaxLevel { get; }

        /// <summary>
        /// 이 카드를 사용할 수 있는 캐릭터 ID. 비어 있으면 전 캐릭터 공용이다.
        /// 예: 부채살 흩뿌리기 = "character.mudang" → 박수 플레이 시 풀에서 제외(`card_system.md`).
        /// </summary>
        string RequiredCharacterId { get; }
    }
}
