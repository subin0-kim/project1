using System.Collections.Generic;
using Mukseon.Gameplay.Progression.Shrine;
using Mukseon.Gameplay.Stats;
using Mukseon.UI;
using NUnit.Framework;
using UnityEditor;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// 저장소에 커밋된 신당 에셋을 실제로 로드해 검사한다(#34 DoD "신당 화면에 5종 업그레이드 항목이 표시된다").
    ///
    /// <see cref="ChapterAssetTests"/>와 같은 이유다 — 참조가 끊기거나 스탯 번호가 어긋나면 컴파일도
    /// 통과하고 로직 테스트도 다 통과하지만, 게임에서는 항목이 비어 있거나 사도 아무 일이 없다.
    /// </summary>
    public class ShrineAssetTests
    {
        private const string ShrinePath = "Assets/Settings/Data/Shrine";
        private const string CatalogPath = ShrinePath + "/ShrineUpgradeCatalog.asset";
        private const string MudangStatsPath = "Assets/Settings/Data/Stats/PlayerStats_Mudang.asset";
        private const string BaksuStatsPath = "Assets/Settings/Data/Stats/PlayerStats_Baksu.asset";

        /// <summary>`currency_system.md`의 업그레이드 5종과 Max Level.</summary>
        private static readonly (string Id, int MaxLevel)[] ExpectedUpgrades =
        {
            ("shrine.max_health", 10),
            ("shrine.attack_power", 10),
            ("shrine.magnet_radius", 5),
            ("shrine.gangshin_charge", 5),
            ("shrine.gain_bonus", 5),
        };

        [Test]
        public void Catalog_ContainsTheFiveDocumentedUpgrades_InOrder()
        {
            ShrineUpgradeCatalog catalog = LoadCatalog();

            Assert.That(catalog.IsValid(out string reason), Is.True, reason);
            Assert.That(catalog.Upgrades.Count, Is.EqualTo(ExpectedUpgrades.Length));

            for (int i = 0; i < ExpectedUpgrades.Length; i++)
            {
                ShrineUpgradeData upgrade = catalog.Upgrades[i];
                Assert.That(upgrade, Is.Not.Null, $"{i}번 항목의 참조가 끊겼습니다.");
                Assert.That(upgrade.UpgradeId, Is.EqualTo(ExpectedUpgrades[i].Id));
                Assert.That(
                    upgrade.MaxLevel,
                    Is.EqualTo(ExpectedUpgrades[i].MaxLevel),
                    $"'{upgrade.UpgradeId}'의 최대 레벨이 기획과 다릅니다.");
            }
        }

        [Test]
        public void Catalog_CostsRiseWithLevel()
        {
            // "레벨이 오를수록 필요 금화가 증가한다"(07_BalanceAndMonetization.md §7.2).
            ShrineUpgradeCatalog catalog = LoadCatalog();

            foreach (ShrineUpgradeData upgrade in catalog.Upgrades)
            {
                int previous = 0;
                for (int level = 1; level <= upgrade.MaxLevel; level++)
                {
                    Assert.That(upgrade.TryGetCost(level, out int cost), Is.True);
                    Assert.That(
                        cost,
                        Is.GreaterThan(previous),
                        $"'{upgrade.UpgradeId}'의 {level}레벨 비용이 앞 레벨보다 싸거나 같습니다.");
                    previous = cost;
                }
            }
        }

        [Test]
        public void GainBonusUpgrade_RaisesBothGoldAndExperience()
        {
            // 항목 하나가 두 스탯을 함께 올리는 유일한 경우라, 한쪽이 빠져도 티가 나지 않는다.
            ShrineUpgradeData upgrade = LoadCatalog().Find("shrine.gain_bonus");
            Assert.That(upgrade, Is.Not.Null);

            var statTypes = new List<StatType>();
            foreach (ShrineUpgradeEffect effect in upgrade.Effects)
            {
                statTypes.Add(effect.StatType);
            }

            Assert.That(statTypes, Does.Contain(StatType.GoldGain));
            Assert.That(statTypes, Does.Contain(StatType.ExperienceGain));
        }

        [Test]
        public void EveryUpgradeTargetsAStatBothCharactersDefine()
        {
            // PlayerStatSystem.AddModifier는 캐릭터 스탯 정의에 없는 스탯이면 조용히 false를 반환한다.
            // 그러면 유저는 골드를 쓰고도 아무 변화를 얻지 못한다 — 밸런스 문제로 오인하기 가장 쉬운 버그다.
            HashSet<StatType> mudang = LoadDefinedStats(MudangStatsPath);
            HashSet<StatType> baksu = LoadDefinedStats(BaksuStatsPath);

            foreach (ShrineUpgradeData upgrade in LoadCatalog().Upgrades)
            {
                foreach (ShrineUpgradeEffect effect in upgrade.Effects)
                {
                    Assert.That(
                        mudang,
                        Does.Contain(effect.StatType),
                        $"무당 스탯 정의에 '{effect.StatType}'이 없어 '{upgrade.UpgradeId}'가 적용되지 않습니다.");
                    Assert.That(
                        baksu,
                        Does.Contain(effect.StatType),
                        $"박수 스탯 정의에 '{effect.StatType}'이 없어 '{upgrade.UpgradeId}'가 적용되지 않습니다.");
                }
            }
        }

        [Test]
        public void ShrineScene_IsRegisteredInBuildSettings()
        {
            // 씬이 빌드 목록에 없으면 타이틀의 신당 버튼이 런타임에만 실패한다.
            string expected = $"Assets/Scenes/{ScreenFlow.ShrineScene}.unity";

            bool found = false;
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path == expected && scene.enabled)
                {
                    found = true;
                    break;
                }
            }

            Assert.That(found, Is.True, $"빌드 설정에 활성화된 {expected}가 없습니다.");
        }

        private static ShrineUpgradeCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ShrineUpgradeCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, $"카탈로그 에셋을 찾을 수 없습니다: {CatalogPath}");
            return catalog;
        }

        private static HashSet<StatType> LoadDefinedStats(string path)
        {
            var definition = AssetDatabase.LoadAssetAtPath<PlayerStatsDefinition>(path);
            Assert.That(definition, Is.Not.Null, $"스탯 정의 에셋을 찾을 수 없습니다: {path}");

            var defined = new HashSet<StatType>();
            for (int i = 0; i < definition.Stats.Count; i++)
            {
                defined.Add(definition.Stats[i].StatType);
            }

            return defined;
        }
    }
}
