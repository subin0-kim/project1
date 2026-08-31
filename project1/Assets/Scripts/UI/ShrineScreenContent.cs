using Mukseon.Gameplay.Progression.Shrine;

namespace Mukseon.UI
{
    /// <summary>
    /// 신당 화면(#34)의 문구와 표기 규칙.
    ///
    /// <see cref="SettingsScreenContent"/>·<see cref="GameGuideContent"/>와 같은 이유로 UI에서 분리했다:
    /// "레벨 3에서 다음 레벨의 효과와 비용이 무엇으로 보이는가"는 이 화면의 요구 그 자체라
    /// MonoBehaviour 없이 EditMode에서 검증되어야 한다.
    /// </summary>
    public static class ShrineScreenContent
    {
        public const string Title = "신당";
        public const string Subtitle = "골드를 바쳐 영구히 강해집니다.";
        public const string GoldCaption = "보유 금화";
        public const string Buy = "구매";
        public const string MaxLevel = "최대";
        public const string Back = "뒤로";
        public const string Missing = "업그레이드 데이터가 없습니다.";

        /// <summary>구매 실패 사유. 버튼이 비활성이라 정상 경로에서는 보이지 않지만, 실패는 말해줘야 한다.</summary>
        public const string NotEnoughGold = "금화가 부족합니다.";
        public const string SaveFailed = "저장에 실패했습니다. 다시 시도해 주세요.";

        /// <summary>천 단위 구분 기호를 넣은 재화 표기. 비용이 네 자리를 넘어가 읽기 어려워지기 때문이다.</summary>
        public static string FormatGold(long gold)
        {
            return gold.ToString("N0");
        }

        /// <summary>현재/최대 레벨 표기(예: "Lv.3 / 10").</summary>
        public static string FormatLevel(int level, int maxLevel)
        {
            return $"Lv.{level} / {maxLevel}";
        }

        /// <summary>
        /// 효과 표기. 다음 레벨이 있으면 "현재 → 다음"으로 보여주어 구매가 무엇을 바꾸는지 드러내고,
        /// 최대 레벨이면 화살표 없이 현재 값만 보여준다.
        /// 레벨 0(미구매)은 화살표 앞의 현재 값이 "+0"이라 오히려 지저분하므로 다음 값만 보여준다.
        /// </summary>
        public static string FormatEffect(ShrineUpgradeData upgrade, int level)
        {
            if (upgrade == null)
            {
                return string.Empty;
            }

            string current = upgrade.FormatEffect(level);
            if (level >= upgrade.MaxLevel)
            {
                return current;
            }

            string next = upgrade.FormatEffect(level + 1);
            return level <= 0 ? next : $"{current} → {next}";
        }

        /// <summary>구매 버튼에 쓸 문구. 최대 레벨이면 비용 대신 "최대"를 보여준다.</summary>
        public static string FormatBuyLabel(ShrineUpgradeData upgrade, int level)
        {
            if (upgrade == null || level >= upgrade.MaxLevel || !upgrade.TryGetCost(level + 1, out int cost))
            {
                return MaxLevel;
            }

            return $"{Buy}  {FormatGold(cost)}";
        }

        /// <summary>구매 결과를 유저에게 보여줄 문구로 옮긴다. 성공이면 빈 문자열이다.</summary>
        public static string DescribeFailure(ShrinePurchaseResult result)
        {
            switch (result)
            {
                case ShrinePurchaseResult.NotEnoughGold:
                    return NotEnoughGold;
                case ShrinePurchaseResult.SaveFailed:
                    return SaveFailed;
                default:
                    return string.Empty;
            }
        }
    }
}
