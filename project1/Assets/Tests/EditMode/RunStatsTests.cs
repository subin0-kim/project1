using Mukseon.Gameplay.Progression;
using NUnit.Framework;

namespace Mukseon.Tests.EditMode
{
    /// <summary>런 결산 지표 집계 검증(#36).</summary>
    public class RunStatsTests
    {
        private RunStats _stats;

        [SetUp]
        public void SetUp()
        {
            _stats = new RunStats();
        }

        [Test]
        public void NewRun_StartsAtZero()
        {
            Assert.That(_stats.KillCount, Is.EqualTo(0));
            Assert.That(_stats.SurvivalSeconds, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_stats.SoulCollected, Is.EqualTo(0));
        }

        [Test]
        public void Tick_AccumulatesSurvivalTime()
        {
            _stats.Tick(0.5f);
            _stats.Tick(0.25f);

            Assert.That(_stats.SurvivalSeconds, Is.EqualTo(0.75f).Within(0.001f));
        }

        [Test]
        public void Tick_IgnoresNonPositiveDelta()
        {
            // 정지 중에는 Time.deltaTime이 0이므로 생존 시간이 멈춰야 한다.
            _stats.Tick(0f);
            _stats.Tick(-1f);

            Assert.That(_stats.SurvivalSeconds, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void RegisterKill_IncrementsCount()
        {
            _stats.RegisterKill();
            _stats.RegisterKill();
            _stats.RegisterKill();

            Assert.That(_stats.KillCount, Is.EqualTo(3));
        }

        [Test]
        public void RegisterSoul_AccumulatesAmount()
        {
            _stats.RegisterSoul(3);
            _stats.RegisterSoul(5);

            Assert.That(_stats.SoulCollected, Is.EqualTo(8));
        }

        [Test]
        public void RegisterSoul_IgnoresNonPositiveAmount()
        {
            _stats.RegisterSoul(0);
            _stats.RegisterSoul(-4);

            Assert.That(_stats.SoulCollected, Is.EqualTo(0));
        }

        [Test]
        public void Reset_ClearsAllMetrics()
        {
            _stats.Tick(10f);
            _stats.RegisterKill();
            _stats.RegisterSoul(2);

            _stats.Reset();

            Assert.That(_stats.KillCount, Is.EqualTo(0));
            Assert.That(_stats.SurvivalSeconds, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_stats.SoulCollected, Is.EqualTo(0));
        }

        [Test]
        public void FormatDuration_PadsSecondsToTwoDigits()
        {
            Assert.That(RunStats.FormatDuration(0f), Is.EqualTo("0:00"));
            Assert.That(RunStats.FormatDuration(9f), Is.EqualTo("0:09"));
            Assert.That(RunStats.FormatDuration(65f), Is.EqualTo("1:05"));
            Assert.That(RunStats.FormatDuration(600f), Is.EqualTo("10:00"));
        }

        [Test]
        public void FormatDuration_ClampsNegativeToZero()
        {
            Assert.That(RunStats.FormatDuration(-5f), Is.EqualTo("0:00"));
        }
    }
}
