using Mukseon.Gameplay.Stats;

namespace Mukseon.Gameplay.Progression.Shrine
{
    /// <summary>
    /// "어느 스탯에 어떤 보정을 넣을지" 한 쌍(#34). <see cref="ShrineUpgradeModifiers"/>가
    /// 세이브의 업그레이드 레벨을 이 목록으로 번역하고, 주입은 호출부가 한다.
    ///
    /// <see cref="PlayerStatSystem"/>(MonoBehaviour)을 거치지 않고 번역 결과만 검사할 수 있어야
    /// 밸런스 계산이 EditMode에서 검증된다.
    /// </summary>
    public readonly struct ShrineStatModifier
    {
        public ShrineStatModifier(StatType statType, StatModifier modifier)
        {
            StatType = statType;
            Modifier = modifier;
        }

        public StatType StatType { get; }
        public StatModifier Modifier { get; }
    }
}
