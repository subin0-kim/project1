using Mukseon.Core;
using NUnit.Framework;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// timeScale 합성 규칙 검증(#109). 순수 로직이라 실제 Time.timeScale을 오염시키지 않는다.
    /// </summary>
    public class TimeScaleControllerTests
    {
        private TimeScaleController _controller;

        [SetUp]
        public void SetUp()
        {
            _controller = new TimeScaleController();
        }

        [Test]
        public void Default_IsRunning_AtFullSpeed()
        {
            Assert.That(_controller.IsPaused, Is.False);
            Assert.That(_controller.TargetTimeScale, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void SetReason_GameOver_PausesToZero()
        {
            bool changed = _controller.SetReason(PauseReason.GameOver, true);

            Assert.That(changed, Is.True);
            Assert.That(_controller.IsPaused, Is.True);
            Assert.That(_controller.TargetTimeScale, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void SetReason_None_IsIgnored()
        {
            bool changed = _controller.SetReason(PauseReason.None, true);

            Assert.That(changed, Is.False);
            Assert.That(_controller.IsPaused, Is.False);
        }

        [Test]
        public void SetReason_ReturnsTrue_OnlyWhenTargetChanges()
        {
            Assert.That(_controller.SetReason(PauseReason.GameOver, true), Is.True);

            // 이미 정지 상태에서 다른 원인이 추가돼도 목표값(0)은 그대로다.
            Assert.That(_controller.SetReason(PauseReason.LevelUpSelection, true), Is.False);
        }

        [Test]
        public void SetReason_MultipleReasons_StayPausedUntilAllCleared()
        {
            _controller.SetReason(PauseReason.GameOver, true);
            _controller.SetReason(PauseReason.LevelUpSelection, true);

            _controller.SetReason(PauseReason.LevelUpSelection, false);
            Assert.That(_controller.IsPaused, Is.True, "게임오버 원인이 남아 있으면 정지를 유지해야 한다.");

            _controller.SetReason(PauseReason.GameOver, false);
            Assert.That(_controller.IsPaused, Is.False);
            Assert.That(_controller.TargetTimeScale, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void SetRate_WhileRunning_AppliesRate()
        {
            bool changed = _controller.SetRate(0.2f);

            Assert.That(changed, Is.True);
            Assert.That(_controller.TargetTimeScale, Is.EqualTo(0.2f).Within(0.001f));
        }

        [Test]
        public void SetRate_ClampsToValidRange()
        {
            _controller.SetRate(0f);
            Assert.That(_controller.Rate, Is.EqualTo(TimeScaleController.MinRate).Within(0.001f),
                "감속 배율이 0까지 내려가면 정지와 구분되지 않는다.");

            _controller.SetRate(5f);
            Assert.That(_controller.Rate, Is.EqualTo(TimeScaleController.MaxRate).Within(0.001f));
        }

        // ── 아래 두 테스트가 #109 구조 수정의 핵심 회귀 방지선이다.
        //    종래에는 정지와 감속이 같은 전역을 두고 경쟁해, 나중에 복원한 쪽이 정지를 덮어썼다.

        [Test]
        public void RestoringRate_DuringGameOver_KeepsPaused()
        {
            // 강신 히트스톱 도중 플레이어가 사망한 뒤 강신이 종료되는 상황.
            _controller.SetRate(0.05f);
            _controller.SetReason(PauseReason.GameOver, true);

            _controller.SetRate(TimeScaleController.MaxRate); // 강신 Exit

            Assert.That(_controller.TargetTimeScale, Is.EqualTo(0f).Within(0.001f),
                "강신 종료가 게임오버 정지를 풀어서는 안 된다.");
        }

        [Test]
        public void ResumingLevelUp_DuringGameOver_KeepsPaused()
        {
            // 게임오버 직후 늦게 수집된 혼불로 레벨업이 열렸다가 선택이 확정되는 상황.
            _controller.SetReason(PauseReason.GameOver, true);
            _controller.SetReason(PauseReason.LevelUpSelection, true);

            _controller.SetReason(PauseReason.LevelUpSelection, false); // 스킬 선택 확정

            Assert.That(_controller.TargetTimeScale, Is.EqualTo(0f).Within(0.001f),
                "레벨업 선택 종료가 게임오버 정지를 풀어서는 안 된다.");
        }

        [Test]
        public void Rate_IsRevealed_AfterPauseClears()
        {
            _controller.SetRate(0.5f);
            _controller.SetReason(PauseReason.GameOver, true);
            Assert.That(_controller.TargetTimeScale, Is.EqualTo(0f).Within(0.001f));

            _controller.SetReason(PauseReason.GameOver, false);

            Assert.That(_controller.TargetTimeScale, Is.EqualTo(0.5f).Within(0.001f),
                "정지가 풀리면 그 아래에 유지되던 감속 배율이 드러나야 한다.");
        }

        [Test]
        public void SetRate_WhilePaused_DoesNotChangeTarget()
        {
            _controller.SetReason(PauseReason.GameOver, true);

            bool changed = _controller.SetRate(0.3f);

            Assert.That(changed, Is.False, "정지 중 감속 변경은 목표값을 바꾸지 않는다.");
            Assert.That(_controller.TargetTimeScale, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Reset_ClearsReasonsAndRate()
        {
            _controller.SetRate(0.1f);
            _controller.SetReason(PauseReason.GameOver, true);
            _controller.SetReason(PauseReason.ScreenTransition, true);

            bool changed = _controller.Reset();

            Assert.That(changed, Is.True);
            Assert.That(_controller.ActiveReasons, Is.EqualTo(PauseReason.None));
            Assert.That(_controller.Rate, Is.EqualTo(1f).Within(0.001f));
            Assert.That(_controller.TargetTimeScale, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Reset_WhenAlreadyRunning_ReportsNoChange()
        {
            Assert.That(_controller.Reset(), Is.False);
        }
    }
}
