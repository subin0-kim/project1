using Mukseon.Gameplay.Combat;
using NUnit.Framework;

namespace Mukseon.Tests.EditMode
{
    public class GangshinRuntimeTests
    {
        [Test]
        public void AddGauge_ReachesReady_WhenGaugeHitsMax()
        {
            var runtime = new GangshinRuntime(100f, 5f, 10f);

            runtime.AddGauge(40f);
            runtime.AddGauge(60f);

            Assert.That(runtime.CurrentGauge, Is.EqualTo(100f));
            Assert.That(runtime.CurrentState, Is.EqualTo(GangshinState.Ready));
        }

        [Test]
        public void TryActivate_TransitionsToActive_AndResetsGauge()
        {
            var runtime = new GangshinRuntime(100f, 5f, 10f);
            runtime.AddGauge(100f);

            bool activated = runtime.TryActivate();

            Assert.That(activated, Is.True);
            Assert.That(runtime.CurrentState, Is.EqualTo(GangshinState.Active));
            Assert.That(runtime.CurrentGauge, Is.EqualTo(0f));
            Assert.That(runtime.RemainingActiveTime, Is.EqualTo(5f));
        }

        [Test]
        public void Tick_TransitionsFromActiveToCooldownToReady()
        {
            var runtime = new GangshinRuntime(100f, 2f, 3f);
            runtime.AddGauge(100f);
            runtime.TryActivate();

            runtime.Tick(2f);

            Assert.That(runtime.CurrentState, Is.EqualTo(GangshinState.Cooldown));
            Assert.That(runtime.RemainingCooldownTime, Is.EqualTo(3f));

            runtime.AddGauge(100f);

            runtime.Tick(3f);

            Assert.That(runtime.CurrentState, Is.EqualTo(GangshinState.Ready));
        }

        [Test]
        public void TryActivate_Fails_WhenNotReady()
        {
            var runtime = new GangshinRuntime(100f, 5f, 10f);

            bool activated = runtime.TryActivate();

            Assert.That(activated, Is.False);
            Assert.That(runtime.CurrentState, Is.EqualTo(GangshinState.Idle));
        }
    }
}
