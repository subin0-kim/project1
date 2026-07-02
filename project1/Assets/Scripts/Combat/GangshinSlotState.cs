using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 강신 4슬롯 시스템의 순수 로직 단일 진실 원천(#59, gangshin_system.md).
    /// 4개 슬롯(각 게이지·필요 게이지·장착 어빌리티)과 장착 인덱스, 그리고 장착 슬롯의 발동
    /// 수명주기(Idle/Ready/Active/Cooldown + 타이머)를 모두 소유한다.
    ///
    /// 게이지는 슬롯마다 독립 저장하므로 교체 시 별도 처리 없이 게이지가 보존된다:
    /// 장착 슬롯만 충전되고 비장착 슬롯 게이지는 그대로 유지된다.
    /// MonoBehaviour에 의존하지 않아 EditMode 테스트가 용이하다.
    /// </summary>
    public sealed class GangshinSlotState
    {
        public const int DefaultCapacity = 4;

        private readonly GangshinSlot[] _slots;
        private readonly float _maxGauge;
        private readonly float _activeDuration;
        private readonly float _cooldownDuration;

        public GangshinSlotState(float maxGauge, float activeDuration, float cooldownDuration, int capacity = DefaultCapacity)
        {
            _maxGauge = Mathf.Max(1f, maxGauge);
            _activeDuration = Mathf.Max(0.1f, activeDuration);
            _cooldownDuration = Mathf.Max(0f, cooldownDuration);

            int cap = Mathf.Max(1, capacity);
            _slots = new GangshinSlot[cap];
            for (int i = 0; i < cap; i++)
            {
                _slots[i] = new GangshinSlot();
            }

            ActiveIndex = -1;
            CurrentState = GangshinState.Idle;
        }

        public GangshinState CurrentState { get; private set; }

        /// <summary>현재 장착 슬롯 인덱스. 보유 강신이 없으면 -1.</summary>
        public int ActiveIndex { get; private set; }

        public int Capacity => _slots.Length;
        public float MaxGauge => _maxGauge;
        public float RemainingActiveTime { get; private set; }
        public float RemainingCooldownTime { get; private set; }

        public IReadOnlyList<GangshinSlot> Slots => _slots;

        /// <summary>보유(점유) 중인 강신 수.</summary>
        public int Count
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i].IsOccupied)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool HasFreeSlot => FindFreeIndex() >= 0;

        /// <summary>현재 장착 슬롯. 보유 강신이 없으면 null.</summary>
        public GangshinSlot ActiveSlot => IsValidActive ? _slots[ActiveIndex] : null;

        // 장착 슬롯 게이지 조회(컨트롤러 public API가 위임).
        public float ActiveGauge => ActiveSlot?.Gauge ?? 0f;
        public float ActiveRequiredGauge => ActiveSlot?.RequiredGauge ?? 0f;
        public float ActiveGaugeNormalized => ActiveSlot?.NormalizedGauge ?? 0f;
        public bool IsActiveReady => CurrentState == GangshinState.Ready;
        public bool IsActivePassiveOnly => ActiveSlot?.IsPassiveOnly ?? false;

        private bool IsValidActive =>
            ActiveIndex >= 0 && ActiveIndex < _slots.Length && _slots[ActiveIndex].IsOccupied;

        /// <summary>
        /// 빈 슬롯에 강신을 추가한다. 성공 시 슬롯 인덱스, 슬롯이 모두 차 있으면 -1을 반환한다.
        /// 첫 강신이 추가되면 자동으로 장착 슬롯이 된다.
        /// </summary>
        public int AddSlot(GangshinAbilityBase ability, float requiredGauge)
        {
            int index = FindFreeIndex();
            if (index < 0)
            {
                return -1;
            }

            GangshinSlot slot = _slots[index];
            slot.Ability = ability;
            slot.RequiredGauge = Mathf.Max(0f, requiredGauge);
            slot.Gauge = 0f;
            slot.IsOccupied = true;

            // 첫 보유 강신은 자동 장착. 게이지가 0이므로 상태는 Idle에서 시작한다.
            if (ActiveIndex < 0)
            {
                ActiveIndex = index;
                CurrentState = ResolveRestingState();
            }

            return index;
        }

        /// <summary>
        /// 슬롯이 모두 찼을 때(레벨업) 기존 강신을 교체한다. 교체된 슬롯의 게이지는 0으로 초기화된다.
        /// 발동(Active) 중인 장착 슬롯은 교체할 수 없다.
        /// </summary>
        public bool ReplaceSlot(int index, GangshinAbilityBase ability, float requiredGauge)
        {
            if (index < 0 || index >= _slots.Length)
            {
                return false;
            }

            // 발동 중인 장착 슬롯 교체는 발동 효과/타임스케일과 충돌하므로 금지한다.
            if (CurrentState == GangshinState.Active && index == ActiveIndex)
            {
                return false;
            }

            GangshinSlot slot = _slots[index];
            slot.Ability = ability;
            slot.RequiredGauge = Mathf.Max(0f, requiredGauge);
            slot.Gauge = 0f;
            slot.IsOccupied = true;

            // 장착 슬롯을 교체하면 게이지가 0으로 리셋되므로 대기 상태를 다시 계산한다(Cooldown 유지).
            if (index == ActiveIndex && CurrentState != GangshinState.Cooldown)
            {
                CurrentState = ResolveRestingState();
            }

            return true;
        }

        /// <summary>
        /// 장착 슬롯을 변경한다. 점유 슬롯만 장착 가능하며, 이미 장착 중이거나 발동(Active) 중이면
        /// 변경하지 않는다. 게이지는 슬롯에 보존되어 있으므로 별도 저장/복원이 필요 없다.
        /// </summary>
        public bool SetActive(int index)
        {
            if (index < 0 || index >= _slots.Length || !_slots[index].IsOccupied)
            {
                return false;
            }

            if (index == ActiveIndex)
            {
                return false;
            }

            // 발동 중에는 교체 불가(발동 효과가 특정 어빌리티에 묶여 있음).
            if (CurrentState == GangshinState.Active)
            {
                return false;
            }

            ActiveIndex = index;

            // 쿨다운은 발동에 대한 전역 잠금이므로 교체해도 유지한다. 그 외에는 새 슬롯 게이지로 재계산.
            if (CurrentState != GangshinState.Cooldown)
            {
                CurrentState = ResolveRestingState();
            }

            return true;
        }

        /// <summary>
        /// 장착 슬롯에만 게이지를 충전한다(비장착 슬롯은 일시정지 유지). 패시브 전용 슬롯과 발동
        /// (Active) 중에는 무시한다. 게이지가 변했으면 true를 반환한다.
        /// </summary>
        public bool AddGaugeToActive(float amount)
        {
            GangshinSlot slot = ActiveSlot;
            if (slot == null || slot.IsPassiveOnly || amount <= 0f || CurrentState == GangshinState.Active)
            {
                return false;
            }

            float previous = slot.Gauge;
            slot.Gauge = Mathf.Clamp(slot.Gauge + amount, 0f, slot.RequiredGauge);

            // Idle에서 필요치를 채우면 발동 가능(Ready) 상태로 전환한다.
            if (CurrentState == GangshinState.Idle && slot.Gauge >= slot.RequiredGauge)
            {
                CurrentState = GangshinState.Ready;
            }

            return !Mathf.Approximately(previous, slot.Gauge);
        }

        /// <summary>발동 가능(Ready) 상태일 때 강신을 발동한다. 성공 시 장착 슬롯 게이지를 0으로 초기화한다.</summary>
        public bool TryActivate()
        {
            if (CurrentState != GangshinState.Ready)
            {
                return false;
            }

            GangshinSlot slot = ActiveSlot;
            if (slot == null || !slot.IsReady)
            {
                return false;
            }

            slot.Gauge = 0f;
            RemainingActiveTime = _activeDuration;
            RemainingCooldownTime = 0f;
            CurrentState = GangshinState.Active;
            return true;
        }

        /// <summary>발동/쿨다운 타이머를 진행한다. 상태가 전이되면 true를 반환한다.</summary>
        public bool Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return false;
            }

            switch (CurrentState)
            {
                case GangshinState.Active:
                    RemainingActiveTime = Mathf.Max(0f, RemainingActiveTime - deltaTime);
                    if (RemainingActiveTime <= 0f)
                    {
                        RemainingCooldownTime = _cooldownDuration;
                        CurrentState = RemainingCooldownTime > 0f ? GangshinState.Cooldown : ResolveRestingState();
                        return true;
                    }

                    break;
                case GangshinState.Cooldown:
                    RemainingCooldownTime = Mathf.Max(0f, RemainingCooldownTime - deltaTime);
                    if (RemainingCooldownTime <= 0f)
                    {
                        CurrentState = ResolveRestingState();
                        return true;
                    }

                    break;
            }

            return false;
        }

        /// <summary>대기 상태(Active/Cooldown이 아닐 때)를 장착 슬롯 게이지로부터 결정한다.</summary>
        private GangshinState ResolveRestingState()
        {
            GangshinSlot slot = ActiveSlot;
            return slot != null && slot.IsReady ? GangshinState.Ready : GangshinState.Idle;
        }

        private int FindFreeIndex()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].IsOccupied)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
