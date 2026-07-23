using System.Collections.Generic;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using UnityEngine;

namespace Mukseon.UI
{
    /// <summary>
    /// 조작 안내 카드(#112)의 정적 콘텐츠 — 안내 문구와 방향 색 범례 항목.
    ///
    /// 카드 UI(<see cref="GameGuideOverlay"/>)에서 분리한 이유:
    /// - 문구·범례가 "핵심 조작을 정확히 전달하는가"라는 기획 요구의 실체라 회귀로 지켜야 한다.
    /// - MonoBehaviour에 묶이지 않은 순수 데이터라 EditMode에서 검증할 수 있다
    ///   (<see cref="Dev.DemoCheatBindings"/>와 같은 구성).
    /// </summary>
    public static class GameGuideContent
    {
        public const string Title = "조작 안내";
        public const string LegendCaption = "적의 색 = 베어야 할 스와이프 방향";
        public const string DismissPrompt = "화면을 탭하면 시작합니다";

        /// <summary>
        /// 안내 문구 5종. 배열 순서가 곧 화면 표시 순서다.
        ///
        /// 문구 주의(이슈 #112):
        /// - "적마다 정해진 방향이 있다"고 쓰지 않는다 — <c>EnemyDirectionConverter</c>(#68)가 방향을
        ///   동적으로 바꾸므로, "지금 보이는 색"을 기준으로 안내한다.
        /// - 방향 시퀀스는 #84에서 보스 전용으로 축소됐으므로 안내에서 제외한다(일반 적은 단일 방향).
        /// </summary>
        public static readonly IReadOnlyList<string> Tips = new[]
        {
            "캐릭터는 화면 중앙에 고정됩니다 — 움직이지 않습니다.",
            "적마다 색으로 방향이 표시됩니다. 그 색과 같은 방향으로 스와이프해야 그 적을 벨 수 있습니다.",
            "스와이프 끝점 주변의 혼불이 딸려옵니다 — 끝점을 혼불 쪽에 두면 회수됩니다.",
            "게이지가 다 차면 더블 탭으로 강신을 발동합니다.",
            "레벨이 오르면 시간이 멈추고 스킬 3개 중 하나를 고릅니다.",
        };

        /// <summary>
        /// 방향 색 범례 항목. 실제 스와이프 방향 4종(None 제외)을 모두 덮어야 한다.
        /// 색 값은 여기 담지 않는다 — 렌더 시점에 <see cref="ResolveColor"/>로 팔레트에서 읽어
        /// 하드코딩을 막고, #83이 방향 색을 사용자 설정으로 바꿔도 자동으로 따라가게 한다.
        /// </summary>
        public static readonly IReadOnlyList<LegendEntry> Legend = new[]
        {
            new LegendEntry(SwipeDirection.Up, "위"),
            new LegendEntry(SwipeDirection.Down, "아래"),
            new LegendEntry(SwipeDirection.Left, "왼쪽"),
            new LegendEntry(SwipeDirection.Right, "오른쪽"),
        };

        /// <summary>
        /// 범례 한 항목의 색을 팔레트에서 해석한다. 팔레트가 없으면 정적 디폴트로 폴백한다.
        /// 항상 이 경로로 색을 얻어야 적 외곽선 글로우·HUD 색 오브와 값이 일치한다(#82, #83).
        /// </summary>
        public static Color ResolveColor(DirectionColorPalette palette, SwipeDirection direction)
        {
            return DirectionColorPalette.Resolve(palette, direction);
        }

        public readonly struct LegendEntry
        {
            public LegendEntry(SwipeDirection direction, string label)
            {
                Direction = direction;
                Label = label;
            }

            public SwipeDirection Direction { get; }
            public string Label { get; }
        }
    }
}
