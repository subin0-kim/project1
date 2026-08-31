using System.Collections.Generic;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Progression.Shrine
{
    /// <summary>
    /// 신당(로비) 영구 업그레이드 1종의 정의(#34, `currency_system.md` / `07_BalanceAndMonetization.md` §7.2).
    ///
    /// 최대 레벨을 별도 필드로 두지 않고 <see cref="_costs"/>의 길이로 정하는 이유: 두 값을 따로 두면
    /// "최대 10레벨인데 비용이 8개"처럼 어긋난 에셋이 만들어지고, 그 상태는 9레벨 구매 시점에야 드러난다.
    /// 레벨 하나당 비용 하나가 반드시 있어야 하므로 목록 길이가 곧 최대 레벨이다.
    /// </summary>
    [CreateAssetMenu(fileName = "ShrineUpgrade", menuName = "Mukseon/Data/Shrine Upgrade")]
    public class ShrineUpgradeData : ScriptableObject
    {
        [SerializeField, Tooltip("세이브 키. SaveData.UpgradeLevels의 키가 되므로 출시 후 바꾸면 레벨이 유실된다.")]
        private string _upgradeId = "shrine.default";

        [SerializeField]
        private string _displayName = "업그레이드";

        [SerializeField, Tooltip("항목 아래 한 줄 설명.")]
        private string _description = string.Empty;

        [SerializeField, Tooltip("효과 표기 단위(예: HP). Percent 효과는 %로 표기되므로 비워 둔다.")]
        private string _effectUnit = string.Empty;

        [SerializeField, Tooltip("레벨별 구매 비용(골드). 첫 항목이 1레벨 비용이며, 목록 길이가 최대 레벨이다.")]
        private List<int> _costs = new List<int>();

        [SerializeField]
        private List<ShrineUpgradeEffect> _effects = new List<ShrineUpgradeEffect>();

        public string UpgradeId => string.IsNullOrWhiteSpace(_upgradeId) ? name : _upgradeId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public string Description => _description;

        /// <summary>최대 레벨 = 정의된 비용의 개수.</summary>
        public int MaxLevel => _costs != null ? _costs.Count : 0;

        public IReadOnlyList<ShrineUpgradeEffect> Effects => _effects;

        /// <summary>
        /// 지정 레벨을 구매하는 비용. <paramref name="level"/>은 1-based(1레벨을 사는 비용 = 1)이며,
        /// 범위 밖이면 false — 최대 레벨 도달 여부 판정을 호출부가 따로 하지 않아도 된다.
        /// </summary>
        public bool TryGetCost(int level, out int cost)
        {
            if (_costs == null || level < 1 || level > _costs.Count)
            {
                cost = 0;
                return false;
            }

            cost = Mathf.Max(0, _costs[level - 1]);
            return true;
        }

        /// <summary>
        /// 지정 레벨의 누적 효과를 사람이 읽는 문구로 만든다(예: "+30 HP", "+15%").
        ///
        /// 첫 번째 효과만 쓴다. 한 업그레이드의 여러 효과는 '같은 크기의 보너스를 여러 스탯에 준다'는
        /// 성격이라(골드/경험치 +5%), 대표값 하나로 표기하는 편이 유저에게 정확하다.
        /// </summary>
        public string FormatEffect(int level)
        {
            if (_effects == null || _effects.Count == 0)
            {
                return string.Empty;
            }

            ShrineUpgradeEffect effect = _effects[0];
            float value = effect.ValueAtLevel(level);

            if (effect.ModifierType == StatModifierType.Percent)
            {
                return $"+{value * 100f:0.#}%";
            }

            return string.IsNullOrEmpty(_effectUnit) ? $"+{value:0.#}" : $"+{value:0.#} {_effectUnit}";
        }

        /// <summary>
        /// 에셋이 쓸 수 있는 상태인지 검사한다. 잘못된 에셋은 신당 화면에서 "살 수 없는 항목"이나
        /// "효과 없는 구매"로 조용히 나타나므로 로드 시점에 걸러낸다.
        /// </summary>
        public bool IsValid(out string reason)
        {
            if (string.IsNullOrWhiteSpace(_upgradeId))
            {
                reason = "UpgradeId가 비어 있습니다.";
                return false;
            }

            if (MaxLevel <= 0)
            {
                reason = $"'{UpgradeId}'에 레벨별 비용이 하나도 없습니다.";
                return false;
            }

            if (_effects == null || _effects.Count == 0)
            {
                reason = $"'{UpgradeId}'에 스탯 효과가 하나도 없습니다.";
                return false;
            }

            for (int i = 0; i < _costs.Count; i++)
            {
                if (_costs[i] < 0)
                {
                    reason = $"'{UpgradeId}'의 {i + 1}레벨 비용이 음수입니다.";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>테스트 전용 구성 헬퍼. 직렬화 필드를 코드로 설정한다.</summary>
        internal void ConfigureForTests(string upgradeId, int[] costs, params ShrineUpgradeEffect[] effects)
        {
            _upgradeId = upgradeId;
            _displayName = upgradeId;
            _costs = costs != null ? new List<int>(costs) : new List<int>();
            _effects = effects != null ? new List<ShrineUpgradeEffect>(effects) : new List<ShrineUpgradeEffect>();
        }

        /// <summary>테스트 전용. 효과 표기 단위를 설정한다.</summary>
        internal void SetEffectUnitForTests(string unit)
        {
            _effectUnit = unit;
        }
    }
}
