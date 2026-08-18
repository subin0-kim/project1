using Mukseon.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

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

        /// <summary>슬롯 점유 여부만 보는 테스트를 위한 최소 어빌리티 인스턴스(Data는 비어 있어도 무방).</summary>
        private static GangshinAbilityBase NewAbility()
        {
            return new GameObject("Gangshin").AddComponent<GangshinAbilityMudang>();
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

        // ── placeholder 대체(#66 강신 강화 카드) ────────────────────────────

        [Test]
        public void AddSlot_DoesNotEquipNewAbility_WhenPlaceholderHoldsActiveSlot()
        {
            // 컨트롤러가 Awake에서 어빌리티 없는 슬롯을 시드하므로 ActiveIndex가 0으로 고정된다.
            // 그냥 추가하면 새 강신은 비장착 슬롯에 들어간다 — AddOrAdoptSlot이 필요한 이유.
            var state = NewState();
            state.AddSlot(null, 100f); // placeholder(slot 0, 장착)
            GangshinAbilityBase ability = NewAbility();

            try
            {
                int index = state.AddSlot(ability, 100f);

                Assert.That(index, Is.EqualTo(1));
                Assert.That(state.ActiveIndex, Is.EqualTo(0));
                Assert.That(state.ActiveSlot.Ability, Is.Null, "새 강신이 장착되지 않은 채 슬롯만 차지한다.");
            }
            finally
            {
                Object.DestroyImmediate(ability.gameObject);
            }
        }

        [Test]
        public void AddOrAdoptSlot_ReplacesPlaceholder_SoNewAbilityIsEquipped()
        {
            var state = NewState();
            state.AddSlot(null, 100f); // placeholder(slot 0, 장착)
            GangshinAbilityBase ability = NewAbility();

            try
            {
                int index = state.AddOrAdoptSlot(ability, 80f);

                Assert.That(index, Is.EqualTo(0), "placeholder 자리를 대체해야 한다.");
                Assert.That(state.ActiveIndex, Is.EqualTo(0));
                Assert.That(state.ActiveSlot.Ability, Is.EqualTo(ability));
                Assert.That(state.ActiveRequiredGauge, Is.EqualTo(80f));
                Assert.That(state.Count, Is.EqualTo(1), "슬롯을 낭비하지 않아야 한다.");
            }
            finally
            {
                Object.DestroyImmediate(ability.gameObject);
            }
        }

        [Test]
        public void AddOrAdoptSlot_AddsToFreeSlot_WhenActiveSlotHasAbility()
        {
            var state = NewState();
            GangshinAbilityBase equipped = NewAbility();
            GangshinAbilityBase added = NewAbility();

            try
            {
                state.AddOrAdoptSlot(equipped, 100f); // slot 0(장착)

                int index = state.AddOrAdoptSlot(added, 100f);

                Assert.That(index, Is.EqualTo(1));
                Assert.That(state.ActiveIndex, Is.EqualTo(0), "이미 강신이 장착되어 있으면 장착을 빼앗지 않는다.");
                Assert.That(state.Count, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(equipped.gameObject);
                Object.DestroyImmediate(added.gameObject);
            }
        }

        [Test]
        public void AddOrAdoptSlot_PreservesGauge_WhenAdoptingPlaceholder()
        {
            // placeholder의 게이지는 레거시 전체 펄스를 향해 실제로 차오른 값이다. 초기화하면
            // 강신 획득이 곧 게이지 손실이 되어, 카드 선택에 설명되지 않는 비용이 붙는다.
            var state = NewState();
            state.AddSlot(null, 100f);
            state.AddGaugeToActive(80f);
            GangshinAbilityBase ability = NewAbility();

            try
            {
                state.AddOrAdoptSlot(ability, 100f);

                Assert.That(state.ActiveGauge, Is.EqualTo(80f));
            }
            finally
            {
                Object.DestroyImmediate(ability.gameObject);
            }
        }

        [Test]
        public void AddOrAdoptSlot_ClampsPreservedGauge_ToNewRequirement()
        {
            var state = NewState();
            state.AddSlot(null, 100f);
            state.AddGaugeToActive(100f); // 가득 참 → Ready
            GangshinAbilityBase ability = NewAbility();

            try
            {
                state.AddOrAdoptSlot(ability, 60f); // 새 강신의 필요 게이지가 더 낮다

                Assert.That(state.ActiveGauge, Is.EqualTo(60f));
                Assert.That(state.CurrentState, Is.EqualTo(GangshinState.Ready));
            }
            finally
            {
                Object.DestroyImmediate(ability.gameObject);
            }
        }

        [Test]
        public void ReplaceSlot_StillResetsGauge_ByDefault()
        {
            // 만석 교체는 기존 강신을 버리는 트레이드이므로 초기화가 기본값으로 남아야 한다.
            var state = NewState();
            state.AddSlot(null, 100f);
            state.AddGaugeToActive(80f);
            GangshinAbilityBase ability = NewAbility();

            try
            {
                state.ReplaceSlot(0, ability, 100f);

                Assert.That(state.ActiveGauge, Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(ability.gameObject);
            }
        }

        [Test]
        public void AddOrAdoptSlot_InheritsCooldown_FromPlaceholder()
        {
            // 발동/쿨다운은 슬롯이 아니라 전역 잠금이라는 기존 모델을 따른다(문서화된 동작).
            var state = NewState();
            state.AddSlot(null, 100f);
            state.AddGaugeToActive(100f);
            state.TryActivate();
            state.Tick(5f); // 발동 시간 종료 → Cooldown
            GangshinAbilityBase ability = NewAbility();

            try
            {
                Assert.That(state.CurrentState, Is.EqualTo(GangshinState.Cooldown));

                state.AddOrAdoptSlot(ability, 100f);

                Assert.That(state.CurrentState, Is.EqualTo(GangshinState.Cooldown));
                Assert.That(state.ActiveSlot.Ability, Is.EqualTo(ability));
            }
            finally
            {
                Object.DestroyImmediate(ability.gameObject);
            }
        }

        [Test]
        public void CanAdoptActiveSlot_IsFalse_WhilePlaceholderIsActive()
        {
            // 발동 중에는 장착 슬롯을 건드릴 수 없으므로 대체도 불가하다.
            var state = NewState();
            state.AddSlot(null, 100f);
            state.AddGaugeToActive(100f);
            state.TryActivate();

            Assert.That(state.CurrentState, Is.EqualTo(GangshinState.Active));
            Assert.That(state.CanAdoptActiveSlot, Is.False);
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

        [Test]
        public void ReplaceSlot_Fails_OnUnoccupiedSlot()
        {
            var state = NewState();
            state.AddSlot(null, 100f); // slot 0만 점유

            bool replaced = state.ReplaceSlot(2, null, 100f); // 빈 슬롯 교체 시도

            Assert.That(replaced, Is.False, "빈 슬롯은 ReplaceSlot 대상이 될 수 없어야 한다.");
            Assert.That(state.Slots[2].IsOccupied, Is.False);
            Assert.That(state.Count, Is.EqualTo(1));
        }

        [Test]
        public void AddSlot_StoresLevel()
        {
            var state = NewState();

            int index = state.AddSlot(null, 100f, level: 3);

            Assert.That(state.Slots[index].Level, Is.EqualTo(3), "발동 레벨이 슬롯에 보존되어야 한다.");
        }

        [Test]
        public void ReplaceSlot_UpdatesLevel()
        {
            var state = NewState();
            state.AddSlot(null, 100f, level: 1);

            state.ReplaceSlot(0, null, 90f, level: 2);

            Assert.That(state.Slots[0].Level, Is.EqualTo(2));
        }
    }
}
