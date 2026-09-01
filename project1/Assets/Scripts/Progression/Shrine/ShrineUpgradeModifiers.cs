using System.Collections.Generic;
using Mukseon.Core.Persistence;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Progression.Shrine
{
    /// <summary>
    /// 신당 업그레이드 레벨을 플레이어 스탯 보정으로 번역한다(#34).
    ///
    /// 번역(<see cref="Collect"/>)과 주입(<see cref="Apply"/>)을 나눈 이유: 번역은 순수 계산이라
    /// EditMode에서 그대로 검증할 수 있고, 주입은 <see cref="PlayerStatSystem"/>이 있어야만 가능하다.
    /// </summary>
    public static class ShrineUpgradeModifiers
    {
        /// <summary>
        /// 신당이 넣은 보정임을 표시하는 출처 태그. 같은 스탯에 다른 시스템(스킬·강신)이 넣은 보정과
        /// 구분되어야 <see cref="PlayerStatSystem.RemoveModifiersFromSource"/>로 안전하게 되돌릴 수 있다.
        /// </summary>
        public const string Source = "shrine.upgrade";

        /// <summary>
        /// 카탈로그와 세이브를 대조해 적용할 보정 목록을 만든다. 레벨 0인 항목은 건너뛴다 —
        /// 값이 0인 보정을 넣어도 결과는 같지만, 스탯 창에 의미 없는 항목이 쌓인다.
        /// </summary>
        public static void Collect(
            ShrineUpgradeCatalog catalog,
            SaveData save,
            List<ShrineStatModifier> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();

            if (catalog == null || save?.UpgradeLevels == null)
            {
                return;
            }

            IReadOnlyList<ShrineUpgradeData> upgrades = catalog.Upgrades;
            for (int i = 0; i < upgrades.Count; i++)
            {
                ShrineUpgradeData upgrade = upgrades[i];
                if (upgrade == null)
                {
                    continue;
                }

                // 세이브에 최대 초과·음수가 들어와도 정의된 범위의 효과만 준다.
                int level = Mathf.Clamp(save.UpgradeLevels.GetValueOrDefault(upgrade.UpgradeId), 0, upgrade.MaxLevel);
                if (level <= 0)
                {
                    continue;
                }

                IReadOnlyList<ShrineUpgradeEffect> effects = upgrade.Effects;
                for (int e = 0; e < effects.Count; e++)
                {
                    ShrineUpgradeEffect effect = effects[e];
                    float value = effect.ValueAtLevel(level);
                    if (Mathf.Approximately(value, 0f))
                    {
                        continue;
                    }

                    results.Add(new ShrineStatModifier(
                        effect.StatType,
                        new StatModifier(value, effect.ModifierType, Source)));
                }
            }
        }

        /// <summary>
        /// 보정을 실제 스탯 시스템에 주입하고, 주입에 성공한 개수를 반환한다.
        ///
        /// <see cref="PlayerStatSystem.AddModifier"/>는 캐릭터의 스탯 정의에 없는 스탯이면 false를
        /// 반환한다. 그 경우 업그레이드를 샀는데 아무 일도 일어나지 않은 것이므로, 조용히 넘기지 않고
        /// 경고를 남긴다 — 밸런스 문제로 오인하기 가장 쉬운 종류의 버그다.
        /// </summary>
        public static int Apply(
            ShrineUpgradeCatalog catalog,
            SaveData save,
            PlayerStatSystem statSystem,
            List<ShrineStatModifier> buffer = null)
        {
            if (statSystem == null)
            {
                return 0;
            }

            List<ShrineStatModifier> modifiers = buffer ?? new List<ShrineStatModifier>();
            Collect(catalog, save, modifiers);

            int applied = 0;
            for (int i = 0; i < modifiers.Count; i++)
            {
                ShrineStatModifier entry = modifiers[i];
                if (statSystem.AddModifier(entry.StatType, entry.Modifier))
                {
                    applied++;
                }
                else
                {
                    Debug.LogWarning(
                        $"[ShrineUpgradeModifiers] '{entry.StatType}' 스탯이 캐릭터 정의에 없어 신당 보정이 적용되지 않았습니다.",
                        statSystem);
                }
            }

            return applied;
        }
    }
}
