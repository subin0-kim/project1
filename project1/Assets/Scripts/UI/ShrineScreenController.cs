using System.Collections.Generic;
using Mukseon.Core.Persistence;
using Mukseon.Gameplay.Progression.Shrine;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mukseon.UI
{
    /// <summary>
    /// 신당(로비) 화면(#34, `05_ScreenStructure.md` §5.2): 골드로 영구 업그레이드를 구매한다.
    ///
    /// 타이틀에서 들어와 타이틀로 돌아가는 독립 씬이다. 캐릭터 선택과 같은 층위의 화면이고
    /// 목록이 길어 스크롤이 필요하므로, 설정처럼 떠 있는 오버레이가 아니라 씬으로 둔다.
    ///
    /// 구매 판정·저장은 전부 <see cref="ShrineUpgradeSystem"/>이 한다. 이 클래스는 그 결과를 그리고,
    /// 바뀐 뒤 다시 그리는 일만 맡는다.
    /// </summary>
    public class ShrineScreenController : ScreenControllerBase
    {
        private const int ShrineSortingOrder = 700;
        private const float PanelWidth = 1120f;
        private const float ListHeight = 620f;
        private const float RowHeight = 116f;

        [SerializeField, Tooltip("진열할 업그레이드 목록. 비우면 화면이 빈 채로 뜬다.")]
        private ShrineUpgradeCatalog _catalog;

        private readonly List<UpgradeRow> _rows = new List<UpgradeRow>();

        private ShrineUpgradeSystem _system;
        private Label _goldValue;
        private Label _message;

        protected override int SortingOrder => ShrineSortingOrder;

        protected override void BuildUi(VisualElement root)
        {
            _system = new ShrineUpgradeSystem(_catalog, SaveGateway.Service);

            VisualElement screen = ScreenUiFactory.Screen(root, ScreenUiFactory.Backdrop);

            var panel = new VisualElement();
            panel.style.width = PanelWidth;
            panel.style.alignItems = Align.Center;
            screen.Add(panel);

            BuildHeader(panel);
            BuildList(panel);
            BuildFooter(panel);

            Refresh();
        }

        private void BuildHeader(VisualElement parent)
        {
            Label title = ScreenUiFactory.Text(parent, ShrineScreenContent.Title, 48, ScreenUiFactory.Ink);
            title.style.marginBottom = 4f;

            Label subtitle = ScreenUiFactory.Text(parent, ShrineScreenContent.Subtitle, 20, ScreenUiFactory.InkDim);
            subtitle.style.marginBottom = 18f;

            // 보유 금화는 구매 가능 여부를 판단하는 유일한 기준이라 목록 바로 위에 붙여 둔다.
            VisualElement goldRow = ScreenUiFactory.Row(parent);
            goldRow.style.marginBottom = 14f;

            ScreenUiFactory.Text(goldRow, ShrineScreenContent.GoldCaption, 22, ScreenUiFactory.InkDim)
                .style.marginRight = 12f;

            _goldValue = ScreenUiFactory.Text(goldRow, string.Empty, 30, ScreenUiFactory.Seal);
        }

        private void BuildList(VisualElement parent)
        {
            var list = new ScrollView(ScrollViewMode.Vertical);
            list.style.width = PanelWidth;
            list.style.height = ListHeight;
            parent.Add(list);

            if (_catalog == null)
            {
                Debug.LogWarning("[ShrineScreenController] ShrineUpgradeCatalog가 비어 있습니다.", this);
                ScreenUiFactory.Text(list, ShrineScreenContent.Missing, 24, ScreenUiFactory.InkDim);
                return;
            }

            if (!_catalog.IsValid(out string reason))
            {
                Debug.LogWarning($"[ShrineScreenController] ShrineUpgradeCatalog가 유효하지 않습니다: {reason}", this);
            }

            IReadOnlyList<ShrineUpgradeData> upgrades = _system.Upgrades;
            for (int i = 0; i < upgrades.Count; i++)
            {
                ShrineUpgradeData upgrade = upgrades[i];
                if (upgrade != null)
                {
                    _rows.Add(BuildRow(list, upgrade));
                }
            }
        }

        private UpgradeRow BuildRow(VisualElement parent, ShrineUpgradeData upgrade)
        {
            // 클로저가 순회 변수를 캡처하지 않도록 지역 복사본을 쓴다(캐릭터 선택 카드와 동일한 패턴).
            ShrineUpgradeData captured = upgrade;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.height = RowHeight;
            row.style.paddingLeft = 28f;
            row.style.paddingRight = 28f;
            row.style.marginBottom = 10f;
            row.style.backgroundColor = ScreenUiFactory.CardFace;
            ScreenUiFactory.SetBorderRadius(row, 8f);
            parent.Add(row);

            var info = new VisualElement();
            info.style.flexDirection = FlexDirection.Column;
            info.style.alignItems = Align.FlexStart;
            row.Add(info);

            Label name = ScreenUiFactory.Text(info, captured.DisplayName, 28, ScreenUiFactory.Ink);
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            name.style.marginBottom = 4f;

            Label description = ScreenUiFactory.Text(info, captured.Description, 18, ScreenUiFactory.InkDim);
            description.style.unityTextAlign = TextAnchor.MiddleLeft;

            var status = new VisualElement();
            status.style.flexDirection = FlexDirection.Column;
            status.style.alignItems = Align.FlexEnd;
            status.style.marginRight = 24f;
            row.Add(status);

            Label level = ScreenUiFactory.Text(status, string.Empty, 22, ScreenUiFactory.InkDim);
            level.style.marginBottom = 4f;

            Label effect = ScreenUiFactory.Text(status, string.Empty, 24, ScreenUiFactory.Ink);

            Button buy = ScreenUiFactory.MenuButton(row, ShrineScreenContent.Buy, () => HandlePurchase(captured));
            buy.style.width = 220f;
            buy.style.height = 60f;
            buy.style.fontSize = 22;

            return new UpgradeRow(captured, level, effect, buy);
        }

        private void BuildFooter(VisualElement parent)
        {
            // 실패 문구 자리를 미리 비워 두고 높이를 고정한다. 문구가 생길 때 아래 버튼이 밀리지 않게 하기 위함이다.
            _message = ScreenUiFactory.Text(parent, string.Empty, 20, ScreenUiFactory.Seal);
            _message.style.height = 26f;
            _message.style.marginTop = 8f;

            VisualElement buttons = ScreenUiFactory.Row(parent);
            ScreenUiFactory.MenuButton(buttons, ShrineScreenContent.Back, HandleBack);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            BuildDevGoldButton(buttons);
#endif
        }

        private void HandlePurchase(ShrineUpgradeData upgrade)
        {
            ShrinePurchaseResult result = _system.TryPurchase(upgrade);
            _message.text = ShrineScreenContent.DescribeFailure(result);
            Refresh();
        }

        private void HandleBack()
        {
            if (!ScreenFlow.IsTransitioning)
            {
                ScreenFlow.LoadTitle();
            }
        }

        /// <summary>보유 금화와 모든 항목의 레벨·효과·버튼 상태를 현재 세이브 기준으로 다시 그린다.</summary>
        private void Refresh()
        {
            _goldValue.text = ShrineScreenContent.FormatGold(_system.Gold);

            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Refresh(_system);
            }
        }

        /// <summary>
        /// 한 항목의 갱신 대상 요소 묶음. 구매할 때마다 화면을 통째로 다시 조립하면 스크롤 위치가 튀므로,
        /// 이미 만들어 둔 요소의 텍스트와 활성 상태만 바꾼다.
        /// </summary>
        private readonly struct UpgradeRow
        {
            private readonly ShrineUpgradeData _upgrade;
            private readonly Label _level;
            private readonly Label _effect;
            private readonly Button _buy;

            public UpgradeRow(ShrineUpgradeData upgrade, Label level, Label effect, Button buy)
            {
                _upgrade = upgrade;
                _level = level;
                _effect = effect;
                _buy = buy;
            }

            public void Refresh(ShrineUpgradeSystem system)
            {
                int level = system.GetLevel(_upgrade);

                _level.text = ShrineScreenContent.FormatLevel(level, _upgrade.MaxLevel);
                _effect.text = ShrineScreenContent.FormatEffect(_upgrade, level);
                _buy.text = ShrineScreenContent.FormatBuyLabel(_upgrade, level);

                // 골드가 부족하거나 최대 레벨이면 누를 수 없어야 한다(DoD).
                bool purchasable = system.CanPurchase(_upgrade);
                _buy.SetEnabled(purchasable);
                _buy.style.color = purchasable ? ScreenUiFactory.Ink : ScreenUiFactory.InkDim;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 골드 획득 경로(전투 드랍·클리어 보상)는 아직 없다(#34 범위 밖). 그전까지는 신당을 확인할 수
        // 없으므로, 시연용 치트(#111)와 같은 성격의 지급 버튼을 개발 빌드에서만 둔다.
        private const int DevGoldGrant = 5000;

        private void BuildDevGoldButton(VisualElement parent)
        {
            Button grant = ScreenUiFactory.MenuButton(parent, $"[개발] 금화 +{DevGoldGrant:N0}", () =>
            {
                SaveGateway.Current.TotalGold += DevGoldGrant;
                SaveGateway.Service.Save();
                _message.text = string.Empty;
                Refresh();
            });
            grant.style.width = 300f;
        }
#endif
    }
}
