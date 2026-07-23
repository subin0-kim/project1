using System.Collections.Generic;
using System.Reflection;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using Mukseon.UI;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// 조작 안내 카드 콘텐츠 검증(#112).
    ///
    /// 범례가 실제 스와이프 방향 4종을 모두 덮고, 색을 항상 팔레트에서 읽는지(하드코딩 금지)를 지킨다 —
    /// 이 둘이 카드의 DoD("범례 색이 DirectionColorPalette와 항상 일치")의 핵심이다.
    /// </summary>
    public class GameGuideContentTests
    {
        private static readonly SwipeDirection[] RealDirections =
        {
            SwipeDirection.Up, SwipeDirection.Down, SwipeDirection.Left, SwipeDirection.Right,
        };

        [Test]
        public void Legend_CoversEveryRealDirectionExactlyOnce()
        {
            var seen = new HashSet<SwipeDirection>();
            foreach (GameGuideContent.LegendEntry entry in GameGuideContent.Legend)
            {
                Assert.That(entry.Direction, Is.Not.EqualTo(SwipeDirection.None), "None은 범례에 넣지 않는다.");
                Assert.That(seen.Add(entry.Direction), Is.True, $"방향 {entry.Direction}가 중복되었습니다.");
            }

            Assert.That(seen, Is.EquivalentTo(RealDirections), "범례는 실제 방향 4종을 모두 덮어야 한다.");
        }

        [Test]
        public void Legend_EntriesHaveLabels()
        {
            foreach (GameGuideContent.LegendEntry entry in GameGuideContent.Legend)
            {
                Assert.That(string.IsNullOrWhiteSpace(entry.Label), Is.False, $"{entry.Direction}에 라벨이 없습니다.");
            }
        }

        // 팔레트를 지정하면 범례 색이 그 인스턴스 값을 따라야 한다(정적 디폴트가 아니라).
        // 이게 깨지면 #83으로 사용자가 색을 바꿔도 범례가 조용히 틀려진다.
        [Test]
        public void ResolveColor_ReadsFromPaletteInstance()
        {
            DirectionColorPalette palette = MakePalette(
                up: new Color(0.1f, 0.2f, 0.3f),
                down: new Color(0.4f, 0.5f, 0.6f),
                left: new Color(0.7f, 0.8f, 0.9f),
                right: new Color(0.15f, 0.25f, 0.35f));

            try
            {
                foreach (SwipeDirection direction in RealDirections)
                {
                    Color expected = palette.GetColor(direction);
                    Color actual = GameGuideContent.ResolveColor(palette, direction);
                    AssertColorEquals(expected, actual, direction);
                }
            }
            finally
            {
                Object.DestroyImmediate(palette);
            }
        }

        // 팔레트가 없으면 정적 디폴트로 폴백해야 한다(에셋 미지정 씬에서도 색이 나온다).
        [Test]
        public void ResolveColor_NullPalette_FallsBackToStaticDefault()
        {
            foreach (SwipeDirection direction in RealDirections)
            {
                Color expected = DirectionColorPalette.DefaultColor(direction);
                Color actual = GameGuideContent.ResolveColor(null, direction);
                AssertColorEquals(expected, actual, direction);
            }
        }

        [Test]
        public void Tips_AreFiveAndNonEmpty()
        {
            Assert.That(GameGuideContent.Tips.Count, Is.EqualTo(5));
            foreach (string tip in GameGuideContent.Tips)
            {
                Assert.That(string.IsNullOrWhiteSpace(tip), Is.False);
            }
        }

        // 이슈 #112가 명시적으로 금지한 문구. EnemyDirectionConverter(#68)가 방향을 바꾸므로
        // "정해진 방향"은 틀린 안내가 된다 — 색 기반 문구로 유지되는지 지킨다.
        [Test]
        public void Tips_DoNotClaimFixedDirection()
        {
            foreach (string tip in GameGuideContent.Tips)
            {
                Assert.That(tip, Does.Not.Contain("정해진 방향"), "방향이 고정이라는 문구는 금지된다(#68).");
            }
        }

        private static DirectionColorPalette MakePalette(Color up, Color down, Color left, Color right)
        {
            var palette = ScriptableObject.CreateInstance<DirectionColorPalette>();
            SetColorField(palette, "_up", up);
            SetColorField(palette, "_down", down);
            SetColorField(palette, "_left", left);
            SetColorField(palette, "_right", right);
            return palette;
        }

        private static void SetColorField(DirectionColorPalette palette, string fieldName, Color value)
        {
            FieldInfo field = typeof(DirectionColorPalette)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"필드 {fieldName}를 찾지 못했습니다.");
            field.SetValue(palette, value);
        }

        private static void AssertColorEquals(Color expected, Color actual, SwipeDirection direction)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-4f), $"{direction} R");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-4f), $"{direction} G");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-4f), $"{direction} B");
        }
    }
}
