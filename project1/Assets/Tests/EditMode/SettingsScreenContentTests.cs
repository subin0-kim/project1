using System.Collections.Generic;
using Mukseon.Core;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using Mukseon.UI;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// 환경설정 화면 콘텐츠 검증(#83).
    ///
    /// 이 목록들이 "유저가 실제로 무엇을 고를 수 있는가"의 전부다. 방향이 하나 빠지면 그 방향만
    /// 색을 못 바꾸고, 스와치가 서로 비슷하면 색맹이 아닌 유저까지 방향을 구분하지 못하게 된다.
    /// </summary>
    public class SettingsScreenContentTests
    {
        private static readonly SwipeDirection[] RealDirections =
        {
            SwipeDirection.Up, SwipeDirection.Down, SwipeDirection.Left, SwipeDirection.Right,
        };

        [Test]
        public void DisplayModes_CoverEveryEnumValueExactlyOnce()
        {
            var seen = new HashSet<DirectionDisplayMode>();
            foreach (SettingsScreenContent.DisplayModeOption option in SettingsScreenContent.DisplayModes)
            {
                Assert.That(seen.Add(option.Mode), Is.True, $"표시 방식 {option.Mode}가 중복되었습니다.");
                Assert.That(string.IsNullOrWhiteSpace(option.Label), Is.False, $"{option.Mode}에 라벨이 없습니다.");
            }

            Assert.That(seen, Is.EquivalentTo((DirectionDisplayMode[])System.Enum.GetValues(typeof(DirectionDisplayMode))),
                "표시 방식 3종을 모두 고를 수 있어야 한다.");
        }

        [Test]
        public void DirectionRows_CoverEveryRealDirectionExactlyOnce()
        {
            var seen = new HashSet<SwipeDirection>();
            foreach (SettingsScreenContent.DirectionRow row in SettingsScreenContent.DirectionRows)
            {
                Assert.That(row.Direction, Is.Not.EqualTo(SwipeDirection.None), "None은 설정 대상이 아니다.");
                Assert.That(seen.Add(row.Direction), Is.True, $"방향 {row.Direction}가 중복되었습니다.");
                Assert.That(string.IsNullOrWhiteSpace(row.Label), Is.False, $"{row.Direction}에 라벨이 없습니다.");
            }

            Assert.That(seen, Is.EquivalentTo(RealDirections), "커스텀 매핑은 실제 방향 4종을 모두 덮어야 한다.");
        }

        // 방향 라벨은 안내 카드(#112)와 같은 표기를 써야 한다.
        // 다르면 "안내의 위"와 "설정의 위"가 같은 것인지 유저가 확신할 수 없다.
        [Test]
        public void DirectionRowLabels_MatchGameGuideLegend()
        {
            foreach (SettingsScreenContent.DirectionRow row in SettingsScreenContent.DirectionRows)
            {
                string legendLabel = null;
                foreach (GameGuideContent.LegendEntry entry in GameGuideContent.Legend)
                {
                    if (entry.Direction == row.Direction)
                    {
                        legendLabel = entry.Label;
                        break;
                    }
                }

                Assert.That(row.Label, Is.EqualTo(legendLabel), $"{row.Direction} 라벨이 안내 카드와 다릅니다.");
            }
        }

        // 방향 4종에 서로 다른 색을 배정하려면 스와치가 최소 4개는 있어야 하고, 모두 달라야 한다.
        [Test]
        public void Swatches_AreDistinct_AndCoverAllDirections()
        {
            IReadOnlyList<SettingsScreenContent.ColorSwatch> swatches = SettingsScreenContent.BuildSwatches(null);
            Assert.That(swatches.Count, Is.GreaterThanOrEqualTo(RealDirections.Length));

            var seen = new HashSet<string>();
            foreach (SettingsScreenContent.ColorSwatch swatch in swatches)
            {
                Assert.That(string.IsNullOrWhiteSpace(swatch.Name), Is.False, "스와치에 이름이 없습니다.");
                Assert.That(seen.Add(ColorUtility.ToHtmlStringRGB(swatch.Color)), Is.True,
                    $"스와치 색 {swatch.Name}이(가) 중복되었습니다.");
            }
        }

        // 기본 매핑 색이 스와치에 없으면 유저가 한 번 바꾼 뒤 "원래 색"으로 되돌릴 방법이 사라진다.
        [Test]
        public void Swatches_ContainEveryDefaultDirectionColor()
        {
            var swatchHexes = new HashSet<string>();
            foreach (SettingsScreenContent.ColorSwatch swatch in SettingsScreenContent.BuildSwatches(null))
            {
                swatchHexes.Add(ColorUtility.ToHtmlStringRGB(swatch.Color));
            }

            foreach (SwipeDirection direction in RealDirections)
            {
                string defaultHex = ColorUtility.ToHtmlStringRGB(DirectionColorPalette.DefaultColor(direction));
                Assert.That(swatchHexes, Does.Contain(defaultHex), $"{direction}의 기본 색이 스와치에 없습니다.");
            }
        }

        // 스와치 앞 4개는 "현재 기본 매핑"이어야 한다. 정적 디폴트에 고정돼 있으면 팔레트 에셋이
        // 배선되는 순간 '현재 색'(팔레트)과 스와치가 어긋나, 어느 스와치에도 선택 링이 붙지 않고
        // 유저가 원래 색으로 되돌릴 수단도 사라진다. 그 증상은 조용해서 원인을 되짚기 어렵다.
        [Test]
        public void BuildSwatches_FollowsPaletteAsset_NotStaticDefaults()
        {
            var palette = ScriptableObject.CreateInstance<DirectionColorPalette>();
            try
            {
                // _up은 [SerializeField] private이라 직렬화 경로로 덮어쓴다(에디터 전용 API 없이).
                JsonUtility.FromJsonOverwrite("{\"_up\":{\"r\":0.1,\"g\":0.2,\"b\":0.3,\"a\":1.0}}", palette);

                string paletteUp = ColorUtility.ToHtmlStringRGB(palette.GetColor(SwipeDirection.Up));
                Assert.That(paletteUp,
                    Is.Not.EqualTo(ColorUtility.ToHtmlStringRGB(DirectionColorPalette.DefaultColor(SwipeDirection.Up))),
                    "테스트 전제가 깨졌다: 팔레트 색이 정적 디폴트와 달라야 의미가 있다.");

                var hexes = new HashSet<string>();
                foreach (SettingsScreenContent.ColorSwatch swatch in SettingsScreenContent.BuildSwatches(palette))
                {
                    hexes.Add(ColorUtility.ToHtmlStringRGB(swatch.Color));
                }

                Assert.That(hexes, Does.Contain(paletteUp), "팔레트가 정한 기본 색이 스와치 목록에 있어야 한다.");
            }
            finally
            {
                ScriptableObject.DestroyImmediate(palette);
            }
        }
    }
}
