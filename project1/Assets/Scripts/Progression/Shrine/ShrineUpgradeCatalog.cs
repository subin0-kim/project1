using System.Collections.Generic;
using UnityEngine;

namespace Mukseon.Gameplay.Progression.Shrine
{
    /// <summary>
    /// 신당에 진열되는 업그레이드 목록(#34). 신당 화면이 이 목록을 순회해 항목을 만들고,
    /// 런 시작 시 <see cref="ShrineUpgradeModifiers"/>가 같은 목록으로 스탯 보정을 재구성한다.
    ///
    /// <see cref="Stats.CharacterDatabase"/>와 같은 구조다 — 목록형 에셋은 화면과 런타임이 같은
    /// 출처를 봐야 "상점에는 있는데 효과는 안 붙는" 어긋남이 생기지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "ShrineUpgradeCatalog", menuName = "Mukseon/Data/Shrine Upgrade Catalog")]
    public class ShrineUpgradeCatalog : ScriptableObject
    {
        [SerializeField, Tooltip("신당 화면에 표시할 순서대로 나열한다.")]
        private List<ShrineUpgradeData> _upgrades = new List<ShrineUpgradeData>();

        /// <summary>표시 순서대로의 업그레이드 목록. null 항목이 섞일 수 있으므로 사용 측에서 걸러야 한다.</summary>
        public IReadOnlyList<ShrineUpgradeData> Upgrades => _upgrades;

        /// <summary>ID로 업그레이드를 찾는다. 없으면 null.</summary>
        public ShrineUpgradeData Find(string upgradeId)
        {
            if (string.IsNullOrWhiteSpace(upgradeId))
            {
                return null;
            }

            for (int i = 0; i < _upgrades.Count; i++)
            {
                ShrineUpgradeData upgrade = _upgrades[i];
                if (upgrade != null && upgrade.UpgradeId == upgradeId)
                {
                    return upgrade;
                }
            }

            return null;
        }

        /// <summary>
        /// 목록이 화면에 쓸 수 있는 상태인지 검사한다. ID 중복이 특히 위험하다 —
        /// 세이브 키가 겹쳐 두 항목이 같은 레벨을 공유하게 되고, 하나를 사면 다른 하나도 오른다.
        /// </summary>
        public bool IsValid(out string reason)
        {
            if (_upgrades.Count == 0)
            {
                reason = "업그레이드 목록이 비어 있습니다.";
                return false;
            }

            var seenIds = new HashSet<string>();
            for (int i = 0; i < _upgrades.Count; i++)
            {
                ShrineUpgradeData upgrade = _upgrades[i];
                if (upgrade == null)
                {
                    reason = $"{i}번 항목이 비어 있습니다.";
                    return false;
                }

                if (!upgrade.IsValid(out string upgradeReason))
                {
                    reason = upgradeReason;
                    return false;
                }

                if (!seenIds.Add(upgrade.UpgradeId))
                {
                    reason = $"UpgradeId가 중복됩니다: '{upgrade.UpgradeId}'";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>테스트 전용 구성 헬퍼. 직렬화 필드를 코드로 설정한다.</summary>
        internal void ConfigureForTests(params ShrineUpgradeData[] upgrades)
        {
            _upgrades = upgrades != null
                ? new List<ShrineUpgradeData>(upgrades)
                : new List<ShrineUpgradeData>();
        }
    }
}
