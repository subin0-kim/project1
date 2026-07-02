using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 강신 스킬 1개의 데이터(gangshin_system.md — 필요한 데이터 항목).
    /// 레벨별 수치 테이블과 표시 정보를 담는다. 레벨 관리는 슬롯 / 강화 카드 시스템(#59, #66)이 담당한다.
    /// </summary>
    [CreateAssetMenu(fileName = "GangshinAbilityData", menuName = "Mukseon/Data/Gangshin Ability Data")]
    public class GangshinAbilityData : ScriptableObject
    {
        [SerializeField]
        private string _abilityId = "gangshin.default";

        [SerializeField]
        private string _displayName = "강신";

        [SerializeField, TextArea]
        private string _description;

        [SerializeField]
        private Sprite _icon;

        [SerializeField, Tooltip("레벨별 수치(index 0 = Lv1). gangshin_balance_mvp.md 참조.")]
        private GangshinAbilityLevel[] _levels =
        {
            new GangshinAbilityLevel(500f, 100f, 0f, false),
        };

        [SerializeField, Min(0f), Tooltip("게이지 충전 배율(기본 충전량 대비). 1 = 기본, 2 = 2배 충전.")]
        private float _gaugeChargeMultiplier = 1f;

        [SerializeField, Tooltip("장착 중 항상 적용되는 패시브 효과. 없을 수 있음(빈 배열).")]
        private GangshinPassiveEffect[] _passiveEffects = new GangshinPassiveEffect[0];

        public string AbilityId => string.IsNullOrWhiteSpace(_abilityId) ? name : _abilityId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public int MaxLevel => _levels != null && _levels.Length > 0 ? _levels.Length : 1;

        /// <summary>게이지 충전 배율(기본 충전량 대비). 음수 방지 위해 0으로 클램프.</summary>
        public float GaugeChargeMultiplier => Mathf.Max(0f, _gaugeChargeMultiplier);

        /// <summary>장착 중 적용할 패시브 효과 목록(없으면 빈 목록).</summary>
        public IReadOnlyList<GangshinPassiveEffect> PassiveEffects =>
            _passiveEffects ?? System.Array.Empty<GangshinPassiveEffect>();

        /// <summary>
        /// 지정 레벨(1-based)의 수치를 반환한다. 범위를 벗어나면 최소 / 최대 레벨로 클램프한다.
        /// 레벨 테이블이 비어 있으면 기본값(모든 필드 0)을 반환한다.
        /// </summary>
        public GangshinAbilityLevel GetLevel(int level)
        {
            if (_levels == null || _levels.Length == 0)
            {
                return default;
            }

            int index = Mathf.Clamp(level - 1, 0, _levels.Length - 1);
            return _levels[index];
        }

        public bool IsValid(out string reason)
        {
            if (_levels == null || _levels.Length == 0)
            {
                reason = "Level table is empty.";
                return false;
            }

            reason = null;
            return true;
        }

        internal void ConfigureForTests(params GangshinAbilityLevel[] levels)
        {
            _levels = levels;
        }
    }
}
