using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 강신 스킬 효과의 추상 기반(#30). GangshinController가 Active 진입 시 장착된 Ability의
    /// <see cref="Activate"/>를 호출한다. 데미지 / 기절 등 실제 전투 로직은 순수 로직
    /// <see cref="GangshinAbilityEffects"/>로 분리해 테스트 용이성을 확보한다.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class GangshinAbilityBase : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField]
        protected GangshinAbilityData _data;

        public GangshinAbilityData Data => _data;

        /// <summary>표시 이름(데이터 미지정 시 오브젝트 이름).</summary>
        public string DisplayName => _data != null ? _data.DisplayName : name;

        /// <summary>
        /// 지정 레벨(1-based)의 발동에 필요한 게이지 비율(0~1). 데이터 미지정 시 1(100%)로 간주한다.
        /// 게이지 임계값 연동(#59)에서 사용.
        /// </summary>
        public float GetRequiredGaugeNormalized(int level)
        {
            return _data != null ? _data.GetLevel(level).RequiredGaugeNormalized : 1f;
        }

        /// <summary>강신을 발동한다. GangshinController가 호출한다.</summary>
        public abstract void Activate(GangshinSlotContext context);

        internal void SetDataForTests(GangshinAbilityData data)
        {
            _data = data;
        }
    }
}
