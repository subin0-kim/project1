using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 강신 슬롯 1개의 런 중 상태(gangshin_system.md — "슬롯별 현재 게이지 수치").
    /// 장착 어빌리티 참조와 게이지 수치를 담으며, 슬롯마다 독립적으로 게이지를 보존한다.
    /// 상태 변경은 <see cref="GangshinSlotState"/>를 통해서만 이루어진다(setter는 internal).
    /// </summary>
    public sealed class GangshinSlot
    {
        /// <summary>장착된 강신 어빌리티. 비어 있으면 null(EditMode 테스트에서도 null 허용).</summary>
        public GangshinAbilityBase Ability { get; internal set; }

        /// <summary>현재 충전된 게이지(교체 시 보존).</summary>
        public float Gauge { get; internal set; }

        /// <summary>발동에 필요한 게이지. 0이면 패시브 전용(게이지 없음).</summary>
        public float RequiredGauge { get; internal set; }

        /// <summary>장착 시점의 발동 레벨(1-based). Activate 호출 시 GangshinSlotContext.Level로 전달된다.</summary>
        public int Level { get; internal set; } = 1;

        /// <summary>슬롯이 점유(강신 보유) 상태인지.</summary>
        public bool IsOccupied { get; internal set; }

        /// <summary>필요 게이지 0 → 게이지 없이 패시브만 제공하는 강신.</summary>
        public bool IsPassiveOnly => IsOccupied && RequiredGauge <= 0f;

        /// <summary>발동 가능(패시브 전용이 아니고 게이지가 필요치 이상).</summary>
        public bool IsReady => IsOccupied && RequiredGauge > 0f && Gauge >= RequiredGauge;

        /// <summary>게이지 정규화 값(0~1). 패시브 전용/필요치 0은 0 나눗셈을 피해 0을 반환.</summary>
        public float NormalizedGauge => RequiredGauge > 0f ? Mathf.Clamp01(Gauge / RequiredGauge) : 0f;
    }
}
