using System.Collections.Generic;
using Mukseon.Core;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using UnityEngine;

namespace Mukseon.UI
{
    /// <summary>
    /// 환경설정 화면(#83)의 정적 콘텐츠 — 문구, 표시 방식 항목, 커스텀 색 스와치 목록.
    ///
    /// <see cref="GameGuideContent"/>와 같은 이유로 UI에서 분리했다: 이 목록들이 "설정이 실제로
    /// 무엇을 고를 수 있게 하는가"라는 요구의 실체라 MonoBehaviour 없이 EditMode에서 검증되어야 한다.
    /// </summary>
    public static class SettingsScreenContent
    {
        public const string Title = "환경설정";
        public const string DisplaySection = "방향 표시 방식";
        public const string AccessibilitySection = "접근성";
        public const string ColorSection = "방향 색상";
        public const string ArrowAssistLabel = "방향 화살표 병행 표시";
        public const string ArrowAssistHint = "색맹·색약이어도 화살표 모양으로 방향을 읽을 수 있습니다.";
        public const string ColorHint = "색을 탭해 방향에 배정합니다. 이미 다른 방향이 쓰는 색을 고르면 서로 맞바뀝니다.";
        public const string ResetColors = "색상 기본값";
        public const string Close = "닫기";
        public const string On = "켬";
        public const string Off = "끔";

        /// <summary>표시 방식 3종. 배열 순서가 곧 버튼 배치 순서다.</summary>
        public static readonly IReadOnlyList<DisplayModeOption> DisplayModes = new[]
        {
            new DisplayModeOption(DirectionDisplayMode.Glow, "외곽선 글로우"),
            new DisplayModeOption(DirectionDisplayMode.Orb, "색 오브"),
            new DisplayModeOption(DirectionDisplayMode.Both, "둘 다"),
        };

        /// <summary>
        /// 커스텀 매핑 대상 방향 4종. <see cref="SwipeDirection.None"/>은 유저가 만나는 개념이 아니므로 제외한다.
        /// 라벨은 <see cref="GameGuideContent.Legend"/>와 같은 표기를 쓴다 — 두 화면이 다른 말을 쓰면
        /// "안내 카드의 위"와 "설정의 위"가 같은 것인지 유저가 확신할 수 없다.
        /// </summary>
        public static readonly IReadOnlyList<DirectionRow> DirectionRows = new[]
        {
            new DirectionRow(SwipeDirection.Up, "위"),
            new DirectionRow(SwipeDirection.Down, "아래"),
            new DirectionRow(SwipeDirection.Left, "왼쪽"),
            new DirectionRow(SwipeDirection.Right, "오른쪽"),
        };

        /// <summary>
        /// 배정 가능한 색 스와치.
        ///
        /// 자유로운 RGB 피커 대신 고정 스와치를 쓰는 이유: 방향 색은 "서로 확실히 구분되는가"가
        /// 전부인데, 슬라이더는 네 방향을 비슷한 색으로 만들어 게임을 망가뜨리기 쉽다. 여기 있는 색은
        /// 모두 어두운 수묵 배경 위에서 대비가 확보되고 서로 충분히 떨어지도록 고른 값이며,
        /// 앞 4개는 기본 매핑(오방색 재구성)과 같은 값이라 "원래 색"이 목록에서 사라지지 않는다.
        /// </summary>
        public static readonly IReadOnlyList<ColorSwatch> Swatches = new[]
        {
            // 앞 4개는 기본 매핑 그대로다. 값을 옮겨 적지 않고 팔레트에서 읽어야, 기본색이 바뀌어도
            // 스와치가 따라가고 유저가 "원래 색"으로 되돌릴 길이 남는다.
            new ColorSwatch("청록", DirectionColorPalette.DefaultColor(SwipeDirection.Up)),
            new ColorSwatch("황금", DirectionColorPalette.DefaultColor(SwipeDirection.Down)),
            new ColorSwatch("적", DirectionColorPalette.DefaultColor(SwipeDirection.Left)),
            new ColorSwatch("녹", DirectionColorPalette.DefaultColor(SwipeDirection.Right)),

            // 나머지는 위 4색 및 서로와 충분히 떨어지도록 고른 대체 색이다.
            new ColorSwatch("남", new Color(0.35f, 0.48f, 0.92f)),
            new ColorSwatch("자", new Color(0.75f, 0.40f, 0.85f)),
            new ColorSwatch("주황", new Color(0.95f, 0.55f, 0.18f)),
            new ColorSwatch("백", new Color(0.90f, 0.89f, 0.85f)),
        };

        public readonly struct DisplayModeOption
        {
            public DisplayModeOption(DirectionDisplayMode mode, string label)
            {
                Mode = mode;
                Label = label;
            }

            public DirectionDisplayMode Mode { get; }
            public string Label { get; }
        }

        public readonly struct DirectionRow
        {
            public DirectionRow(SwipeDirection direction, string label)
            {
                Direction = direction;
                Label = label;
            }

            public SwipeDirection Direction { get; }
            public string Label { get; }
        }

        public readonly struct ColorSwatch
        {
            public ColorSwatch(string name, Color color)
            {
                Name = name;
                Color = color;
            }

            public string Name { get; }
            public Color Color { get; }
        }
    }
}
