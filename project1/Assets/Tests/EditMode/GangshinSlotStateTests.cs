using Mukseon.Gameplay.Combat;
using NUnit.Framework;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// 강신 4슬롯 시스템(#59) 순수 로직 검증. 어빌리티는 MonoBehaviour이므로 null로 두고
    /// 필요 게이지 수치를 직접 주입해 게이지/슬롯 로직만 검증한다.
    /// </summary>
    public class GangshinSlotStateTests
    {
        private static GangshinSlotState NewState()
        {
            return new GangshinSlotState(maxGauge: 100f, activeDuration: 5f, cooldownDuration: 10f);
        }

        [Test]
        public void AddSlot_FirstBecomesActive()
        {
            var state = NewState();

            int index = state.AddSlot(null, 100f);

            Assert.That(index, Is.EqualTo(0));
            Assert.That(state.ActiveIndex, Is.EqualTo(0));
            Assert.That(state.Count, Is.EqualTo(1));
            Assert.That(state.CurrentState, Is.EqualTo(GangshinState.Idle));
        }

        [Test]
        public void AddSlot_ReturnsMinusOne_WhenFull()
        {
            var state = NewState();
            for (int i = 0; i < GangshinSlotState.DefaultCapacity; i++)
            {
                Assert.That(state.AddSlot(null, 100f), Is.GreaterThanOrEqualTo(0));
            }

            int overflow = state.AddSlot(null, 100f);

            Assert.That(overflow, Is.EqualTo(-1));
            Assert.That(state.Count, Is.EqualTo(GangshinSlotState.DefaultCapacity));
            Assert.That(state.HasFreeSlot, Is.False);
        }

        [Test]
        public void AddGaugeToActive_ChargesOnlyActiveSlot()
        {
            var state = NewState();
            state.AddSlot(null, 100f); // slot 0 (active)
            state.AddSlot(null, 100f); // slot 1

            state.AddGaugeToActive(50f);

            Assert.That(state.ActiveGauge, Is.EqualTo(50f));
            Assert.That(state.Slots[1].Gauge, Is.EqualTo(0f));
        }

        [Test]
        public void SwapPreservesGauge()
        {
            var state = NewState();
            state.AddSlot(null, 100f); // slot 0
            state.AddSlot(null, 100f); // slot 1

            state.AddGaugeToActive(50f);   // slot 0 = 50
            state.SetActive(1);
            state.AddGaugeToActive(30f);   // slot 1 = 30

            Assert.That(state.Slots[0].Gauge, Is.EqualTo(50f), "비장착 슬롯 게이지가 보존되어야 한다.");
            Assert.That(state.Slots[1].Gauge, Is.EqualTo(30f));

            state.SetActive(0);
            Assert.That(state.ActiveGauge, Is.EqualTo(50f), "재장착 시 이전 게이지가 복원되어야 한다.");
        }

        [Test]
        public void ReadyThreshold_HonorsRequiredGauge()
        {
            var state = NewState();
            state.AddSlot(null, 90f); // 필요 게이지 90

            state.AddGaugeToActive(80f);
            Assert.That(state.CurrentState, Is.EqualTo(GangshinState.Idle));
            Assert.That(state.IsActiveReady, Is.False);

            state.AddGaugeToActive(10f); // 총 90 → Ready
            Assert.That(state.ActiveGauge, Is.EqualTo(90f));
            Assert.That(state.CurrentState, Is.EqualTo(GangshinState.Ready));
            Assert.That(state.IsActiveReady, Is.True);
        }

        [Test]
        public void AddGaugeToActive_ClampsToRequired()
        {
            var state = NewState();
            state.AddSlot(null, 90f);

            state.AddGaugeToActive(200f);

            Assert.That(state.ActiveGauge, Is.EqualTo(90f));
        }

        [Test]
        public void PassiveOnlySlot_NeverReady_IgnoresGauge()
        {
            var state = NewState();
            state.AddSlot(null, 0f); // 필요 게이지 0 = 패시브 전용

            Assert.That(state.IsActivePassiveOnly, Is.True);

            bool changed = state.AddGaugeToActive(50f);

            Assert.That(changed, Is.False);
            Assert.That(state.ActiveGauge, Is.EqualTo(0f));
            Assert.That(state.CurrentState, Is.EqualTo(GangshinState.Idle));
            Assert.That(state.IsActiveReady, Is.False);
        }

        [Test]
        public void TryActivate_ResetsGauge_AndEntersActive()
        {
            var state = NewState();
            state.AddSlot(null, 100f);
            state.AddGaugeToActive(100f);

            bool activated = state.TryActivate();

            Assert.That(activated, Is.True);
            Assert.That(state.CurrentState, Is.EqualTo(GangshinState.Active));
            Assert.That(state.ActiveGauge, Is.EqualTo(0f));
            Assert.That(state.RemainingActiveTime, Is.EqualTo(5f));
        }

        [Test]
        public void TryActivate_Fails_WhenNotReady()
        {
            var state = NewState();
            state.AddSlot(null, 100f);

            Assert.That(state.TryActivate(), Is.False);
            Assert.That(state.CurrentState, Is.EqualTo(GangshinState.Idle));
        }

        [Test]
        public void Tick_TransitionsFromActiveToCooldownToReady()
        {
            var state = new GangshinSlotState(100f, 2f, 3f);
            state.AddSlot(null, 100f);
            state.AddGaugeToActive(100f);
            state.TryActivate();

            state.Tick(2f);
            Assert.That(state.CurrentState, Is.EqualTo(GangshinState.Cooldown));
            Assert.That(state.RemainingCooldownTime, Is.EqualTo(3f));

            state.AddGaugeToActive(100f); // 쿨다운 중에도 장착 슬롯은 충전 가능
            state.Tick(3f);
            Assert.That(state.CurrentState, Is.EqualTo(GangshinState.Ready));
        }

        [Test]
        public void SetActive_Fails_DuringActive()
        {
            var state = NewState();
            state.AddSlot(null, 100f); // slot 0
            state.AddSlot(null, 100f); // slot 1
            state.AddGaugeToActive(100f);
            state.TryActivate();

            bool swapped = state.SetActive(1);

            Assert.That(swapped, Is.False, "발동(Active) 중에는 교체할 수 없어야 한다.");
            Assert.That(state.ActiveIndex, Is.EqualTo(0));
        }

        [Test]
        public void ReplaceSlot_ResetsGauge_AndPreservesOthers()
        {
            var state = NewState();
            for (int i = 0; i < GangshinSlotState.DefaultCapacity; i++)
            {
                state.AddSlot(null, 100f);
            }

            state.AddGaugeToActive(50f); // slot 0 = 50
            state.SetActive(1);
            state.AddGaugeToActive(40f); // slot 1 = 40

            bool replaced = state.ReplaceSlot(0, null, 100f);

            Assert.That(replaced, Is.True);
            Assert.That(state.Slots[0].Gauge, Is.EqualTo(0f), "교체된 슬롯 게이지는 0으로 초기화되어야 한다.");
            Assert.That(state.Slots[1].Gauge, Is.EqualTo(40f), "다른 슬롯 게이지는 보존되어야 한다.");
            Assert.That(state.Count, Is.EqualTo(GangshinSlotState.DefaultCapacity));
        }
    }
}
