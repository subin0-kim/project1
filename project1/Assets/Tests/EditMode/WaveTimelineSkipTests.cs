using System.Collections.Generic;
using System.Reflection;
using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// 타임라인 시간 점프와 미니 보스 마크 억제 검증(#111).
    ///
    /// 억제(SuppressMiniBossMarksBefore)와 검사(CheckTimelineMarks)의 호출 순서가 뒤바뀌면
    /// 건너뛴 마크가 전부 발행되어 미니 보스가 한꺼번에 쏟아진다. 순서에 민감한 로직이라
    /// 회귀를 잡을 테스트가 필요하다(PR #113 리뷰 지적).
    /// </summary>
    public class WaveTimelineSkipTests
    {
        private const float BossMinuteMark = 10f;

        private GameObject _directorGo;
        private WaveCombatDirector _director;

        [SetUp]
        public void SetUp()
        {
            _directorGo = new GameObject("WaveCombatDirector");
            // AddComponent는 EditMode에서 Awake/OnEnable을 태우지 않으므로 private 필드를 직접 세팅한다.
            _director = _directorGo.AddComponent<WaveCombatDirector>();

            SetPrivateField(_director, "_bossMinuteMark", BossMinuteMark);
            SetPrivateField(_director, "_miniBossMinuteMarks", new List<float> { 3f, 6f, 9f });

            // StartWaves는 WaveDatabase를 요구하므로, 점프 로직 검증에 필요한 런타임 상태만 직접 만든다.
            SetPrivateField(_director, "_isRunning", true);
            SetPrivateField(_director, "_timelineElapsedSeconds", 0f);

            var fired = GetPrivateField<List<bool>>(_director, "_miniBossMarkFired");
            fired.Clear();
            fired.AddRange(new[] { false, false, false });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_directorGo);
        }

        [Test]
        public void SkipToBoss_SuppressesSkippedMiniBossMarks()
        {
            int markCount = 0;
            _director.OnMiniBossMarkReached += _ => markCount++;

            bool entered = _director.SkipToBossPhase();

            Assert.That(entered, Is.True, "보스 페이즈에 진입해야 한다.");
            Assert.That(markCount, Is.EqualTo(0), "건너뛴 미니 보스 마크는 발행되지 않아야 한다.");
            Assert.That(_director.IsBossPhase, Is.True);
            Assert.That(_director.TimelineElapsedSeconds, Is.EqualTo(BossMinuteMark * 60f).Within(0.01f));
        }

        // 억제가 '실제로 무언가를 막고 있는지' 확인하는 대조 테스트.
        // 이게 없으면 마크가 애초에 발행되지 않는 경우와 구분할 수 없다.
        [Test]
        public void SkipWithFireSkippedMarks_FiresEveryPassedMark()
        {
            var fired = new List<float>();
            _director.OnMiniBossMarkReached += minutes => fired.Add(minutes);

            _director.SkipTimelineTo(BossMinuteMark * 60f, fireSkippedMarks: true);

            Assert.That(fired, Is.EqualTo(new[] { 3f, 6f, 9f }));
        }

        [Test]
        public void SkipTimelineTo_DoesNotRewind()
        {
            _director.SkipTimelineTo(300f);

            _director.SkipTimelineTo(100f);

            Assert.That(_director.TimelineElapsedSeconds, Is.EqualTo(300f).Within(0.01f));
        }

        [Test]
        public void SkipTimelineTo_BeforeBossMark_DoesNotEnterBossPhase()
        {
            _director.SkipTimelineTo(5f * 60f);

            Assert.That(_director.IsBossPhase, Is.False);
            Assert.That(_director.TimelineElapsedSeconds, Is.EqualTo(300f).Within(0.01f));
        }

        // 5분으로 점프하면 3분 마크만 소진되고, 6·9분은 남아 있어야 한다.
        [Test]
        public void PartialSkip_OnlySuppressesMarksBeforeTarget()
        {
            _director.SkipTimelineTo(5f * 60f);

            var fired = GetPrivateField<List<bool>>(_director, "_miniBossMarkFired");
            Assert.That(fired[0], Is.True, "3분 마크는 소진되어야 한다.");
            Assert.That(fired[1], Is.False, "6분 마크는 남아 있어야 한다.");
            Assert.That(fired[2], Is.False, "9분 마크는 남아 있어야 한다.");
        }

        [Test]
        public void SkipToBoss_WhenNotRunning_DoesNothing()
        {
            SetPrivateField(_director, "_isRunning", false);

            Assert.That(_director.SkipToBossPhase(), Is.False);
            Assert.That(_director.IsBossPhase, Is.False);
        }

        [Test]
        public void SkipToBoss_Twice_SecondCallIsNoOp()
        {
            Assert.That(_director.SkipToBossPhase(), Is.True);

            Assert.That(_director.SkipToBossPhase(), Is.False, "이미 보스 페이즈면 다시 진입하지 않아야 한다.");
        }

        [Test]
        public void SkipToBoss_WithoutBossMark_DoesNothing()
        {
            SetPrivateField(_director, "_bossMinuteMark", 0f);

            Assert.That(_director.SkipToBossPhase(), Is.False);
            Assert.That(_director.IsBossPhase, Is.False);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"필드 {fieldName}를 찾지 못했습니다.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"필드 {fieldName}를 찾지 못했습니다.");
            return (T)field.GetValue(target);
        }
    }
}
