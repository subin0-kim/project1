using System.Collections.Generic;
using System.IO;
using Mukseon.Core.Persistence;
using Mukseon.Gameplay.Progression.Shrine;
using Mukseon.Gameplay.Stats;
using Mukseon.UI;
using NUnit.Framework;
using UnityEngine;

namespace Mukseon.Tests.EditMode
{
    /// <summary>
    /// 신당 영구 업그레이드(#34)의 데이터·구매·스탯 번역 검증.
    ///
    /// 구매는 골드를 소모하고 즉시 저장하는, 되돌릴 수 없는 조작이다. 화면 없이 로직만으로
    /// "언제 살 수 있고 / 얼마가 빠지고 / 무엇이 남는가"가 확정되어야 한다.
    /// </summary>
    public class ShrineUpgradeTests
    {
        private readonly List<Object> _created = new List<Object>();
        private string _tempPath;
        private JsonSaveStorage _storage;

        [SetUp]
        public void SetUp()
        {
            // 실제 세이브 파일을 건드리지 않도록 테스트마다 고유한 임시 파일을 쓴다(SaveSystemTests와 동일).
            _tempPath = Path.Combine(Path.GetTempPath(), $"mukseon_shrine_test_{System.Guid.NewGuid():N}.json");
            _storage = new JsonSaveStorage(_tempPath);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();

            if (File.Exists(_tempPath))
            {
                File.Delete(_tempPath);
            }
        }

        // ---- ShrineUpgradeData ----

        [Test]
        public void MaxLevel_IsTheNumberOfAuthoredCosts()
        {
            ShrineUpgradeData upgrade = CreateUpgrade("shrine.test", new[] { 100, 200, 300 });

            Assert.That(upgrade.MaxLevel, Is.EqualTo(3));
        }

        [Test]
        public void TryGetCost_IsOneBased_AndRejectsOutOfRange()
        {
            ShrineUpgradeData upgrade = CreateUpgrade("shrine.test", new[] { 100, 200, 300 });

            Assert.That(upgrade.TryGetCost(1, out int first), Is.True);
            Assert.That(first, Is.EqualTo(100));

            Assert.That(upgrade.TryGetCost(3, out int last), Is.True);
            Assert.That(last, Is.EqualTo(300));

            Assert.That(upgrade.TryGetCost(0, out _), Is.False, "0레벨을 사는 비용은 없다.");
            Assert.That(upgrade.TryGetCost(4, out _), Is.False, "최대 레벨을 넘는 비용은 없다.");
        }

        [Test]
        public void FormatEffect_AccumulatesLinearly_AndUsesUnitForFlat()
        {
            ShrineUpgradeData upgrade = CreateUpgrade(
                "shrine.health",
                new[] { 100, 200, 300 },
                new ShrineUpgradeEffect(StatType.MaxHealth, StatModifierType.Flat, 10f));
            upgrade.SetEffectUnitForTests("HP");

            Assert.That(upgrade.FormatEffect(3), Is.EqualTo("+30 HP"));
        }

        [Test]
        public void FormatEffect_RendersPercentEffectsAsPercentage()
        {
            ShrineUpgradeData upgrade = CreateUpgrade(
                "shrine.power",
                new[] { 100, 200, 300 },
                new ShrineUpgradeEffect(StatType.SwipeDamageMultiplier, StatModifierType.Percent, 0.05f));

            Assert.That(upgrade.FormatEffect(3), Is.EqualTo("+15%"));
        }

        [Test]
        public void TryGetCost_RejectsNonPositiveCosts()
        {
            // 0을 "비용 0"으로 돌려주면 골드 보유량과 무관하게 구매 조건을 통과한다.
            ShrineUpgradeData upgrade = CreateUpgrade("shrine.test", new[] { 100, 0, -50 });

            Assert.That(upgrade.TryGetCost(2, out _), Is.False, "비용 0은 살 수 있는 레벨이 아니다.");
            Assert.That(upgrade.TryGetCost(3, out _), Is.False, "음수 비용은 살 수 있는 레벨이 아니다.");
        }

        [Test]
        public void IsValid_RejectsUpgradesThatCannotBeBoughtOrDoNothing()
        {
            ShrineUpgradeData noCosts = CreateUpgrade("shrine.nocost", new int[0]);
            Assert.That(noCosts.IsValid(out _), Is.False, "비용이 없으면 살 수 없다.");

            ShrineUpgradeData noEffects = CreateUpgrade("shrine.noeffect", new[] { 100 });
            Assert.That(noEffects.IsValid(out _), Is.False, "효과가 없으면 사도 의미가 없다.");

            ShrineUpgradeData zeroCost = CreateUpgrade("shrine.free", new[] { 100, 0 }, DefaultEffect());
            Assert.That(zeroCost.IsValid(out string reason), Is.False, "비용 0인 레벨은 유효하지 않다.");
            Assert.That(reason, Does.Contain("2레벨"));
        }

        [Test]
        public void Catalog_IsValid_RejectsDuplicateUpgradeIds()
        {
            // ID가 겹치면 세이브 키를 공유해 한 항목을 사면 다른 항목의 레벨도 오른다.
            ShrineUpgradeData a = CreateUpgrade("shrine.same", new[] { 100 }, DefaultEffect());
            ShrineUpgradeData b = CreateUpgrade("shrine.same", new[] { 100 }, DefaultEffect());
            ShrineUpgradeCatalog catalog = CreateCatalog(a, b);

            Assert.That(catalog.IsValid(out string reason), Is.False);
            Assert.That(reason, Does.Contain("shrine.same"));
        }

        // ---- 구매 ----

        [Test]
        public void TryPurchase_DeductsGold_RaisesLevel_AndPersists()
        {
            ShrineUpgradeData upgrade = CreateUpgrade("shrine.health", new[] { 500, 1000 }, DefaultEffect());
            var service = CreateService(gold: 1200);
            var system = new ShrineUpgradeSystem(CreateCatalog(upgrade), service);

            int changedCount = 0;
            system.OnChanged += () => changedCount++;

            Assert.That(system.TryPurchase(upgrade), Is.EqualTo(ShrinePurchaseResult.Success));
            Assert.That(system.Gold, Is.EqualTo(700));
            Assert.That(system.GetLevel(upgrade), Is.EqualTo(1));
            Assert.That(changedCount, Is.EqualTo(1));

            // 즉시 저장되어야 한다 — 다시 로드해도 남아 있는지가 진짜 확인이다.
            var reloaded = new SaveService(_storage);
            reloaded.Load();
            Assert.That(reloaded.Current.TotalGold, Is.EqualTo(700));
            Assert.That(reloaded.Current.UpgradeLevels.GetValueOrDefault("shrine.health"), Is.EqualTo(1));
        }

        [Test]
        public void TryPurchase_WithoutEnoughGold_ChangesNothing()
        {
            ShrineUpgradeData upgrade = CreateUpgrade("shrine.health", new[] { 500 }, DefaultEffect());
            var service = CreateService(gold: 499);
            var system = new ShrineUpgradeSystem(CreateCatalog(upgrade), service);

            Assert.That(system.CanPurchase(upgrade), Is.False);
            Assert.That(system.TryPurchase(upgrade), Is.EqualTo(ShrinePurchaseResult.NotEnoughGold));
            Assert.That(system.Gold, Is.EqualTo(499));
            Assert.That(system.GetLevel(upgrade), Is.EqualTo(0));
        }

        [Test]
        public void TryPurchase_AtMaxLevel_IsRejected_EvenWithGold()
        {
            ShrineUpgradeData upgrade = CreateUpgrade("shrine.health", new[] { 500 }, DefaultEffect());
            var service = CreateService(gold: 100000);
            service.Current.UpgradeLevels.Set("shrine.health", 1);
            var system = new ShrineUpgradeSystem(CreateCatalog(upgrade), service);

            Assert.That(system.IsMaxLevel(upgrade), Is.True);
            Assert.That(system.TryGetNextCost(upgrade, out _), Is.False);
            Assert.That(system.TryPurchase(upgrade), Is.EqualTo(ShrinePurchaseResult.MaxLevel));
            Assert.That(system.Gold, Is.EqualTo(100000));
        }

        [Test]
        public void TryPurchase_RollsBackGoldAndLevel_WhenSaveFails()
        {
            // 저장이 실패했는데 메모리만 바뀌면, 화면에는 구매된 것으로 보이지만 다음 실행에서 되돌아간다.
            // 유저에게는 골드만 사라진 것과 같으므로 원복해야 한다.
            ShrineUpgradeData upgrade = CreateUpgrade("shrine.health", new[] { 500 }, DefaultEffect());
            var service = new SaveService(new FailingStorage());
            service.Load();
            service.Current.TotalGold = 1000;

            var system = new ShrineUpgradeSystem(CreateCatalog(upgrade), service);

            int changedCount = 0;
            system.OnChanged += () => changedCount++;

            Assert.That(system.TryPurchase(upgrade), Is.EqualTo(ShrinePurchaseResult.SaveFailed));
            Assert.That(system.Gold, Is.EqualTo(1000), "저장 실패 시 골드가 되돌아와야 한다.");
            Assert.That(system.GetLevel(upgrade), Is.EqualTo(0), "저장 실패 시 레벨이 되돌아와야 한다.");
            Assert.That(changedCount, Is.EqualTo(0), "실패한 구매로 화면을 갱신하면 안 된다.");
        }

        [Test]
        public void TryPurchase_WithZeroCost_IsRejectedInsteadOfSoldForFree()
        {
            // 비용에 0이 들어간 에셋을 무료 구매로 팔면, 잘못된 데이터가 밸런스 붕괴로 곧장 이어진다.
            ShrineUpgradeData upgrade = CreateUpgrade("shrine.free", new[] { 0 }, DefaultEffect());
            var service = CreateService(gold: 0);
            var system = new ShrineUpgradeSystem(CreateCatalog(upgrade), service);

            Assert.That(system.CanPurchase(upgrade), Is.False);
            Assert.That(system.TryPurchase(upgrade), Is.EqualTo(ShrinePurchaseResult.InvalidUpgrade));
            Assert.That(system.GetLevel(upgrade), Is.EqualTo(0));
        }

        [Test]
        public void TryPurchase_LeavesNoSaveEntry_WhenTheFirstPurchaseFails()
        {
            // 원복은 "값을 0으로 되돌린다"가 아니라 "손대기 전 모양으로 되돌린다"여야 한다.
            ShrineUpgradeData upgrade = CreateUpgrade("shrine.health", new[] { 500 }, DefaultEffect());
            var service = new SaveService(new FailingStorage());
            service.Load();
            service.Current.TotalGold = 1000;

            var system = new ShrineUpgradeSystem(CreateCatalog(upgrade), service);

            Assert.That(system.TryPurchase(upgrade), Is.EqualTo(ShrinePurchaseResult.SaveFailed));
            Assert.That(
                service.Current.UpgradeLevels.ContainsKey("shrine.health"),
                Is.False,
                "한 번도 산 적 없는 항목이 세이브에 0 엔트리로 남으면 안 된다.");
        }

        [Test]
        public void TryPurchase_KeepsPreviousLevel_WhenALaterPurchaseFails()
        {
            ShrineUpgradeData upgrade = CreateUpgrade("shrine.health", new[] { 500, 1000 }, DefaultEffect());
            var service = new SaveService(new FailingStorage());
            service.Load();
            service.Current.TotalGold = 5000;
            service.Current.UpgradeLevels.Set("shrine.health", 1);

            var system = new ShrineUpgradeSystem(CreateCatalog(upgrade), service);

            Assert.That(system.TryPurchase(upgrade), Is.EqualTo(ShrinePurchaseResult.SaveFailed));
            Assert.That(system.GetLevel(upgrade), Is.EqualTo(1), "이미 산 레벨까지 잃으면 안 된다.");
            Assert.That(service.Current.UpgradeLevels.ContainsKey("shrine.health"), Is.True);
        }

        [Test]
        public void GetLevel_ClampsCorruptedSaveValues()
        {
            ShrineUpgradeData upgrade = CreateUpgrade("shrine.health", new[] { 500, 1000 }, DefaultEffect());
            var service = CreateService(gold: 0);
            var system = new ShrineUpgradeSystem(CreateCatalog(upgrade), service);

            service.Current.UpgradeLevels.Set("shrine.health", 99);
            Assert.That(system.GetLevel(upgrade), Is.EqualTo(2));

            service.Current.UpgradeLevels.Set("shrine.health", -5);
            Assert.That(system.GetLevel(upgrade), Is.EqualTo(0));
        }

        // ---- 스탯 번역 ----

        [Test]
        public void Collect_ScalesEffectsByLevel_AndTagsThemWithShrineSource()
        {
            ShrineUpgradeData upgrade = CreateUpgrade(
                "shrine.health",
                new[] { 500, 1000, 1500 },
                new ShrineUpgradeEffect(StatType.MaxHealth, StatModifierType.Flat, 10f));

            var save = SaveData.CreateDefault();
            save.UpgradeLevels.Set("shrine.health", 3);

            var results = new List<ShrineStatModifier>();
            ShrineUpgradeModifiers.Collect(CreateCatalog(upgrade), save, results);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].StatType, Is.EqualTo(StatType.MaxHealth));
            Assert.That(results[0].Modifier.Value, Is.EqualTo(30f).Within(0.0001f));
            Assert.That(results[0].Modifier.Type, Is.EqualTo(StatModifierType.Flat));
            Assert.That(results[0].Modifier.Source, Is.EqualTo(ShrineUpgradeModifiers.Source));
        }

        [Test]
        public void Collect_EmitsOneModifierPerEffect()
        {
            // 골드/경험치 보너스처럼 업그레이드 1종이 스탯 2개를 함께 올리는 경우.
            ShrineUpgradeData upgrade = CreateUpgrade(
                "shrine.gain",
                new[] { 3000 },
                new ShrineUpgradeEffect(StatType.GoldGain, StatModifierType.Percent, 0.05f),
                new ShrineUpgradeEffect(StatType.ExperienceGain, StatModifierType.Percent, 0.05f));

            var save = SaveData.CreateDefault();
            save.UpgradeLevels.Set("shrine.gain", 1);

            var results = new List<ShrineStatModifier>();
            ShrineUpgradeModifiers.Collect(CreateCatalog(upgrade), save, results);

            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0].StatType, Is.EqualTo(StatType.GoldGain));
            Assert.That(results[1].StatType, Is.EqualTo(StatType.ExperienceGain));
        }

        [Test]
        public void Collect_SkipsUnpurchasedUpgrades_AndClampsOverflowLevels()
        {
            ShrineUpgradeData unbought = CreateUpgrade("shrine.a", new[] { 500 }, DefaultEffect());
            ShrineUpgradeData overflowed = CreateUpgrade(
                "shrine.b",
                new[] { 500, 1000 },
                new ShrineUpgradeEffect(StatType.MaxHealth, StatModifierType.Flat, 10f));

            var save = SaveData.CreateDefault();
            save.UpgradeLevels.Set("shrine.b", 50);

            var results = new List<ShrineStatModifier>();
            ShrineUpgradeModifiers.Collect(CreateCatalog(unbought, overflowed), save, results);

            Assert.That(results.Count, Is.EqualTo(1), "레벨 0인 항목은 보정을 만들지 않는다.");
            Assert.That(results[0].Modifier.Value, Is.EqualTo(20f).Within(0.0001f), "최대 레벨을 넘겨 적용되면 안 된다.");
        }

        // ---- 화면 표기 ----

        [Test]
        public void ScreenContent_ShowsCurrentToNextTransition()
        {
            ShrineUpgradeData upgrade = CreateUpgrade(
                "shrine.health",
                new[] { 500, 1000, 1500 },
                new ShrineUpgradeEffect(StatType.MaxHealth, StatModifierType.Flat, 10f));
            upgrade.SetEffectUnitForTests("HP");

            // 미구매(0레벨)에서 "+0 HP → +10 HP"는 앞부분이 군더더기라 다음 값만 보여준다.
            Assert.That(ShrineScreenContent.FormatEffect(upgrade, 0), Is.EqualTo("+10 HP"));
            Assert.That(ShrineScreenContent.FormatEffect(upgrade, 1), Is.EqualTo("+10 HP → +20 HP"));
            Assert.That(ShrineScreenContent.FormatEffect(upgrade, 3), Is.EqualTo("+30 HP"), "최대 레벨은 화살표가 없다.");
        }

        [Test]
        public void ScreenContent_BuyLabel_ShowsCostUntilMaxLevel()
        {
            ShrineUpgradeData upgrade = CreateUpgrade("shrine.health", new[] { 500, 1500 }, DefaultEffect());

            Assert.That(ShrineScreenContent.FormatBuyLabel(upgrade, 0), Does.Contain("500"));
            Assert.That(ShrineScreenContent.FormatBuyLabel(upgrade, 1), Does.Contain("1,500"));
            Assert.That(ShrineScreenContent.FormatBuyLabel(upgrade, 2), Is.EqualTo(ShrineScreenContent.MaxLevel));
        }

        [Test]
        public void ScreenContent_FormatLevel_ShowsCurrentAndMax()
        {
            Assert.That(ShrineScreenContent.FormatLevel(3, 10), Is.EqualTo("Lv.3 / 10"));
        }

        // ---- 헬퍼 ----

        private static ShrineUpgradeEffect DefaultEffect()
        {
            return new ShrineUpgradeEffect(StatType.MaxHealth, StatModifierType.Flat, 10f);
        }

        private ShrineUpgradeData CreateUpgrade(string id, int[] costs, params ShrineUpgradeEffect[] effects)
        {
            var upgrade = ScriptableObject.CreateInstance<ShrineUpgradeData>();
            upgrade.ConfigureForTests(id, costs, effects);
            _created.Add(upgrade);
            return upgrade;
        }

        private ShrineUpgradeCatalog CreateCatalog(params ShrineUpgradeData[] upgrades)
        {
            var catalog = ScriptableObject.CreateInstance<ShrineUpgradeCatalog>();
            catalog.ConfigureForTests(upgrades);
            _created.Add(catalog);
            return catalog;
        }

        private SaveService CreateService(long gold)
        {
            var service = new SaveService(_storage);
            service.Load();
            service.Current.TotalGold = gold;
            return service;
        }

        /// <summary>저장이 항상 실패하는 저장소. 구매 롤백 경로를 재현한다.</summary>
        private sealed class FailingStorage : ISaveStorage
        {
            public bool Exists() => false;

            public SaveData Load() => null;

            public bool Save(SaveData data) => false;

            public void Delete()
            {
            }
        }
    }
}
