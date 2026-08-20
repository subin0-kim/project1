using Mukseon.Core;
using Mukseon.Core.Input;
using Mukseon.Core.Persistence;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// 방향 색상 환경설정 검증(#83).
    ///
    /// <see cref="DirectionColorSettings"/>는 정적 상태라 테스트 간에 값이 새면 다른 테스트
    /// (특히 <c>GameGuideContentTests</c>의 팔레트 색 검증)를 조용히 오염시킨다.
    /// SetUp/TearDown에서 반드시 초기화한다.
    /// </summary>
    public class DirectionColorSettingsTests
    {
        // 스와치 목록과 겹치지 않는 임의의 색 — 기본값과 구분되기만 하면 된다.
        private static readonly Color TestColor = new Color(0.10f, 0.20f, 0.30f);
        private static readonly Color OtherColor = new Color(0.40f, 0.50f, 0.60f);

        // 팔레트 에셋 없이 정적 디폴트를 기본색으로 쓰는 조회 경로(실제 런타임과 동일한 상태).
        private static readonly DirectionColorSource DefaultBase = DirectionColorPalette.DefaultColor;

        [SetUp]
        public void SetUp() => DirectionColorSettings.ResetToDefaults();

        [TearDown]
        public void TearDown() => DirectionColorSettings.ResetToDefaults();

        // ---- 기본값 ----

        [Test]
        public void Defaults_ShowBothGlowAndOrb_WithoutArrows()
        {
            Assert.That(DirectionColorSettings.DisplayMode, Is.EqualTo(DirectionDisplayMode.Both));
            Assert.That(DirectionColorSettings.GlowEnabled, Is.True);
            Assert.That(DirectionColorSettings.OrbEnabled, Is.True);
            Assert.That(DirectionColorSettings.ArrowAssistEnabled, Is.False);
        }

        // ---- 표시 방식 ----

        [Test]
        public void DisplayMode_Glow_DisablesOrbOnly()
        {
            DirectionColorSettings.SetDisplayMode(DirectionDisplayMode.Glow);

            Assert.That(DirectionColorSettings.GlowEnabled, Is.True);
            Assert.That(DirectionColorSettings.OrbEnabled, Is.False);
        }

        [Test]
        public void DisplayMode_Orb_DisablesGlowOnly()
        {
            DirectionColorSettings.SetDisplayMode(DirectionDisplayMode.Orb);

            Assert.That(DirectionColorSettings.GlowEnabled, Is.False);
            Assert.That(DirectionColorSettings.OrbEnabled, Is.True);
        }

        // 화살표는 색상 표시 방식과 독립적이어야 한다(DoD).
        [Test]
        public void ArrowAssist_IsIndependentOfDisplayMode()
        {
            DirectionColorSettings.SetArrowAssist(true);
            DirectionColorSettings.SetDisplayMode(DirectionDisplayMode.Glow);

            Assert.That(DirectionColorSettings.ArrowAssistEnabled, Is.True);
            Assert.That(DirectionColorSettings.OrbEnabled, Is.False);

            DirectionColorSettings.SetDisplayMode(DirectionDisplayMode.Orb);
            Assert.That(DirectionColorSettings.ArrowAssistEnabled, Is.True);
        }

        // ---- 변경 이벤트(런타임 즉시 반영의 전달 경로) ----

        [Test]
        public void OnChanged_FiresOnRealChangeOnly()
        {
            int count = 0;
            void Handler() => count++;

            DirectionColorSettings.OnChanged += Handler;
            try
            {
                DirectionColorSettings.SetDisplayMode(DirectionDisplayMode.Glow);
                Assert.That(count, Is.EqualTo(1));

                // 같은 값을 다시 넣으면 구독자를 깨우지 않는다(매 프레임 재빌드 방지).
                DirectionColorSettings.SetDisplayMode(DirectionDisplayMode.Glow);
                Assert.That(count, Is.EqualTo(1));

                DirectionColorSettings.SetArrowAssist(true);
                Assert.That(count, Is.EqualTo(2));
            }
            finally
            {
                DirectionColorSettings.OnChanged -= Handler;
            }
        }

        // ---- 커스텀 색 매핑 ----

        [Test]
        public void SetCustomColor_OverridesResolvedColor()
        {
            DirectionColorSettings.SetCustomColor(SwipeDirection.Up, TestColor, DefaultBase);

            Assert.That(DirectionColorSettings.TryGetCustomColor(SwipeDirection.Up, out Color stored), Is.True);
            Assert.That(Hex(stored), Is.EqualTo(Hex(TestColor)));
            Assert.That(Hex(DirectionColorPalette.Resolve(null, SwipeDirection.Up)), Is.EqualTo(Hex(TestColor)));

            // 손대지 않은 방향은 기본값 그대로여야 한다.
            Assert.That(DirectionColorSettings.TryGetCustomColor(SwipeDirection.Down, out _), Is.False);
        }

        // 두 방향이 같은 색이 되면 "색 = 벨 방향" 규칙이 무너지므로 서로 맞바꾼다.
        [Test]
        public void SetCustomColor_SwapsWhenColorAlreadyUsedByAnotherDirection()
        {
            Color upDefault = DirectionColorPalette.DefaultColor(SwipeDirection.Up);
            Color downDefault = DirectionColorPalette.DefaultColor(SwipeDirection.Down);

            // Up에 Down의 색을 배정 → Down이 Up의 원래 색을 가져가야 한다.
            DirectionColorSettings.SetCustomColor(SwipeDirection.Up, downDefault, DefaultBase);

            Assert.That(Hex(DirectionColorPalette.Resolve(null, SwipeDirection.Up)), Is.EqualTo(Hex(downDefault)));
            Assert.That(Hex(DirectionColorPalette.Resolve(null, SwipeDirection.Down)), Is.EqualTo(Hex(upDefault)));
        }

        [Test]
        public void SetCustomColor_NeverProducesDuplicateColors()
        {
            Color right = DirectionColorPalette.DefaultColor(SwipeDirection.Right);
            DirectionColorSettings.SetCustomColor(SwipeDirection.Up, right, DefaultBase);
            DirectionColorSettings.SetCustomColor(SwipeDirection.Left, right, DefaultBase);

            string up = Hex(DirectionColorPalette.Resolve(null, SwipeDirection.Up));
            string down = Hex(DirectionColorPalette.Resolve(null, SwipeDirection.Down));
            string left = Hex(DirectionColorPalette.Resolve(null, SwipeDirection.Left));
            string rightHex = Hex(DirectionColorPalette.Resolve(null, SwipeDirection.Right));

            Assert.That(new[] { up, down, left, rightHex }, Is.Unique, "네 방향의 색은 항상 서로 달라야 한다.");
        }

        [Test]
        public void ClearCustomColors_KeepsDisplayAndArrowSettings()
        {
            DirectionColorSettings.SetDisplayMode(DirectionDisplayMode.Orb);
            DirectionColorSettings.SetArrowAssist(true);
            DirectionColorSettings.SetCustomColor(SwipeDirection.Left, TestColor, DefaultBase);

            DirectionColorSettings.ClearCustomColors();

            Assert.That(DirectionColorSettings.TryGetCustomColor(SwipeDirection.Left, out _), Is.False);
            Assert.That(DirectionColorSettings.DisplayMode, Is.EqualTo(DirectionDisplayMode.Orb));
            Assert.That(DirectionColorSettings.ArrowAssistEnabled, Is.True);
        }

        // ---- 세이브 연동 ----

        [Test]
        public void WriteTo_ThenApplyFrom_RoundTripsEverySetting()
        {
            DirectionColorSettings.SetDisplayMode(DirectionDisplayMode.Glow);
            DirectionColorSettings.SetArrowAssist(true);
            DirectionColorSettings.SetCustomColor(SwipeDirection.Up, TestColor, DefaultBase);
            DirectionColorSettings.SetCustomColor(SwipeDirection.Right, OtherColor, DefaultBase);

            SaveData data = SaveData.CreateDefault();
            DirectionColorSettings.WriteTo(data);

            DirectionColorSettings.ResetToDefaults();
            DirectionColorSettings.ApplyFrom(data);

            Assert.That(DirectionColorSettings.DisplayMode, Is.EqualTo(DirectionDisplayMode.Glow));
            Assert.That(DirectionColorSettings.ArrowAssistEnabled, Is.True);
            Assert.That(Hex(DirectionColorPalette.Resolve(null, SwipeDirection.Up)), Is.EqualTo(Hex(TestColor)));
            Assert.That(Hex(DirectionColorPalette.Resolve(null, SwipeDirection.Right)), Is.EqualTo(Hex(OtherColor)));
        }

        // 지웠던 색이 세이브에 남아 되살아나면 "기본값 복원"이 동작하지 않는 것처럼 보인다.
        [Test]
        public void WriteTo_DropsClearedOverrides()
        {
            SaveData data = SaveData.CreateDefault();
            DirectionColorSettings.SetCustomColor(SwipeDirection.Up, TestColor, DefaultBase);
            DirectionColorSettings.WriteTo(data);
            Assert.That(data.DirectionColors.Count, Is.EqualTo(1));

            DirectionColorSettings.ClearCustomColors();
            DirectionColorSettings.WriteTo(data);

            Assert.That(data.DirectionColors.Count, Is.EqualTo(0));
        }

        [Test]
        public void ApplyFrom_UnknownDisplayMode_FallsBackToDefault()
        {
            SaveData data = SaveData.CreateDefault();
            data.DirectionDisplayMode = 99;

            DirectionColorSettings.ApplyFrom(data);

            Assert.That(DirectionColorSettings.DisplayMode, Is.EqualTo(DirectionColorSettings.DefaultDisplayMode));
        }

        [Test]
        public void ApplyFrom_Null_ResetsToDefaults()
        {
            DirectionColorSettings.SetDisplayMode(DirectionDisplayMode.Orb);
            DirectionColorSettings.SetArrowAssist(true);

            DirectionColorSettings.ApplyFrom(null);

            Assert.That(DirectionColorSettings.DisplayMode, Is.EqualTo(DirectionColorSettings.DefaultDisplayMode));
            Assert.That(DirectionColorSettings.ArrowAssistEnabled, Is.EqualTo(DirectionColorSettings.DefaultArrowAssist));
        }

        // 저장이 RRGGBB 8비트이므로 비교도 같은 정밀도로 한다.
        private static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);
    }
}
