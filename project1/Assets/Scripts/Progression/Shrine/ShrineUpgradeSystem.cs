using System;
using System.Collections.Generic;
using Mukseon.Core.Persistence;
using UnityEngine;

namespace Mukseon.Gameplay.Progression.Shrine
{
    /// <summary>
    /// 신당 업그레이드의 조회·구매를 담당하는 순수 C# 시스템(#34).
    ///
    /// <see cref="SaveService"/>를 주입받는다. <see cref="SaveGateway"/>를 직접 붙잡지 않는 덕분에
    /// EditMode 테스트가 실제 세이브 파일을 건드리지 않고 구매 전 과정을 검증할 수 있다
    /// (<see cref="Core.DirectionColorSettings"/>가 파일 IO를 분리한 것과 같은 이유).
    ///
    /// 구매는 "잔액 검증 → 차감 → 레벨 증가 → 즉시 저장"이며, 저장이 실패하면 메모리 상태를 원복한다.
    /// 원복하지 않으면 화면에는 골드가 줄고 레벨이 오른 것으로 보이지만 다음 실행에서 되돌아가,
    /// 유저 입장에서는 골드만 사라진 것이 된다.
    /// </summary>
    public sealed class ShrineUpgradeSystem
    {
        private readonly ShrineUpgradeCatalog _catalog;
        private readonly SaveService _saveService;

        public ShrineUpgradeSystem(ShrineUpgradeCatalog catalog, SaveService saveService)
        {
            _catalog = catalog;
            _saveService = saveService;
        }

        /// <summary>보유 골드나 업그레이드 레벨이 바뀐 뒤 발행된다. 화면 갱신 구독용.</summary>
        public event Action OnChanged;

        /// <summary>진열 순서대로의 업그레이드 목록. 카탈로그가 없으면 빈 목록이다.</summary>
        public IReadOnlyList<ShrineUpgradeData> Upgrades =>
            _catalog != null ? _catalog.Upgrades : Array.Empty<ShrineUpgradeData>();

        /// <summary>보유 골드. 세이브가 준비되지 않았으면 0.</summary>
        public long Gold => Save != null ? Save.TotalGold : 0L;

        private SaveData Save => _saveService?.Current;

        /// <summary>현재 구매된 레벨. 한 번도 사지 않았으면 0이다.</summary>
        public int GetLevel(ShrineUpgradeData upgrade)
        {
            SaveData save = Save;
            if (upgrade == null || save?.UpgradeLevels == null)
            {
                return 0;
            }

            // 세이브가 손상돼 음수나 최대 초과 값이 들어와도 화면과 스탯이 같은 값을 보도록 여기서 조인다.
            int stored = save.UpgradeLevels.GetValueOrDefault(upgrade.UpgradeId);
            return Mathf.Clamp(stored, 0, upgrade.MaxLevel);
        }

        public bool IsMaxLevel(ShrineUpgradeData upgrade)
        {
            return upgrade != null && GetLevel(upgrade) >= upgrade.MaxLevel;
        }

        /// <summary>다음 레벨의 구매 비용. 이미 최대 레벨이면 false.</summary>
        public bool TryGetNextCost(ShrineUpgradeData upgrade, out int cost)
        {
            cost = 0;
            return upgrade != null && upgrade.TryGetCost(GetLevel(upgrade) + 1, out cost);
        }

        /// <summary>구매 가능 여부. 구매 버튼의 활성화 조건이다.</summary>
        public bool CanPurchase(ShrineUpgradeData upgrade)
        {
            return Evaluate(upgrade, out _) == ShrinePurchaseResult.Success;
        }

        /// <summary>
        /// 구매를 시도한다. 성공하면 골드를 차감하고 레벨을 1 올린 뒤 즉시 저장하고
        /// <see cref="OnChanged"/>를 발행한다. 실패 시 세이브는 전혀 손대지 않는다.
        /// </summary>
        public ShrinePurchaseResult TryPurchase(ShrineUpgradeData upgrade)
        {
            ShrinePurchaseResult check = Evaluate(upgrade, out int cost);
            if (check != ShrinePurchaseResult.Success)
            {
                return check;
            }

            SaveData save = Save;
            long previousGold = save.TotalGold;
            int previousLevel = GetLevel(upgrade);

            // 실패 시 "값을 되돌린다"가 아니라 "손대기 전 상태로 되돌린다"여야 한다. 원래 키가 없었는데
            // Set(id, 0)으로 되돌리면 구매한 적 없는 항목이 세이브에 0 엔트리로 남는다(동작엔 무해하지만 잔여물이다).
            bool hadEntry = save.UpgradeLevels.ContainsKey(upgrade.UpgradeId);

            save.TotalGold = previousGold - cost;
            save.UpgradeLevels.Set(upgrade.UpgradeId, previousLevel + 1);

            if (!_saveService.Save())
            {
                save.TotalGold = previousGold;

                if (hadEntry)
                {
                    save.UpgradeLevels.Set(upgrade.UpgradeId, previousLevel);
                }
                else
                {
                    save.UpgradeLevels.Remove(upgrade.UpgradeId);
                }

                return ShrinePurchaseResult.SaveFailed;
            }

            OnChanged?.Invoke();
            return ShrinePurchaseResult.Success;
        }

        /// <summary>구매 전 검증. 화면(버튼 활성화)과 구매 경로가 같은 판정을 쓰도록 한 곳에 모은다.</summary>
        private ShrinePurchaseResult Evaluate(ShrineUpgradeData upgrade, out int cost)
        {
            cost = 0;

            SaveData save = Save;
            if (upgrade == null || save?.UpgradeLevels == null)
            {
                return ShrinePurchaseResult.InvalidUpgrade;
            }

            if (IsMaxLevel(upgrade))
            {
                return ShrinePurchaseResult.MaxLevel;
            }

            if (!TryGetNextCost(upgrade, out cost))
            {
                // 최대 레벨이 아닌데 비용이 없다 = 에셋이 깨졌다. 공짜로 팔지 않는다.
                return ShrinePurchaseResult.InvalidUpgrade;
            }

            return save.TotalGold >= cost ? ShrinePurchaseResult.Success : ShrinePurchaseResult.NotEnoughGold;
        }
    }
}
