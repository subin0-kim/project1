using System.Collections.Generic;
using Mukseon.Core;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mukseon.UI
{
    /// <summary>
    /// 전투 진입 시 1회 뜨는 조작 안내 카드(#112). 화면을 탭하면 닫히고 런이 시작된다.
    ///
    /// 이 게임의 조작은 장르 관습과 정면으로 어긋난다(중앙 고정 + 방향 매칭). 안내가 없으면 처음 보는
    /// 사람은 "왜 안 죽지" 상태로 첫 시연 시간을 소모한다. #39 튜토리얼의 최소 대체물이라 저장 없이 매 런 뜬다.
    ///
    /// 결과 화면(<see cref="ResultScreenController"/>)과 같은 씬 배치형 오버레이다. 범례 색을 실제 적이
    /// 쓰는 팔레트 에셋 인스턴스에서 읽어야 색이 일치하고(#82) #83 사용자 설정도 따라가므로, 인스펙터에서
    /// 팔레트를 지정할 수 있는 씬 배치가 자가 부트스트랩보다 맞다.
    /// </summary>
    public sealed class GameGuideOverlay : ScreenControllerBase
    {
        // 인게임 HUD(500)·결과(600) 위, 화면 전환 페이드(1000) 아래.
        private const int GuideSortingOrder = 700;
        private const float PanelWidth = 760f;
        private const float ChipDiameter = 26f;

        private enum Phase
        {
            Pending,    // 아직 표시 전(화면 전환 대기 중)
            Shown,      // 표시 중 — 정지·입력 억제 소유
            Dismissed,  // 탭으로 닫힘 — 이번 런에는 다시 뜨지 않음
        }

        [SerializeField]
        [Tooltip("범례 색을 읽을 팔레트. 적 외곽선 글로우와 같은 에셋을 지정한다. 비우면 정적 디폴트 색으로 폴백.")]
        private DirectionColorPalette _palette;

        private Phase _phase = Phase.Pending;

        protected override int SortingOrder => GuideSortingOrder;

        protected override void Awake()
        {
            base.Awake();
            SetVisible(false);
        }

        // 화면 전환(페이드)이 끝난 뒤에 카드를 띄운다.
        // ScreenFlow는 씬 로드 직후 TimeScaleService.Reset()으로 모든 정지 원인을 지운다(ScreenFlow.cs:153).
        // Awake/OnEnable 시점에 정지를 걸면 그 Reset에 함께 지워지므로, 전환이 끝날 때까지 기다렸다가 소유한다.
        private void Update()
        {
            if (_phase != Phase.Pending || ScreenFlow.IsTransitioning)
            {
                return;
            }

            Show();
        }

        private void OnDisable()
        {
            // 카드가 정지·억제를 건 채 씬이 내려가면 원인이 남는다(#109와 같은 종류의 누수).
            Release();
        }

        protected override void BuildUi(VisualElement root)
        {
            // 반투명 암막 — 뒤의 전투 화면을 거의 가려 시선을 카드에 모은다.
            VisualElement screen = ScreenUiFactory.Screen(root, new Color(0f, 0f, 0f, 0.82f));
            // 카드 어디를 탭하든 닫힌다(암막·패널 모두 이벤트가 여기로 버블링된다).
            screen.RegisterCallback<PointerDownEvent>(_ => Dismiss());

            var panel = new VisualElement();
            panel.style.width = PanelWidth;
            panel.style.paddingTop = 44f;
            panel.style.paddingBottom = 44f;
            panel.style.paddingLeft = 52f;
            panel.style.paddingRight = 52f;
            panel.style.backgroundColor = ScreenUiFactory.CardFace;
            panel.style.alignItems = Align.Center;
            ScreenUiFactory.SetBorderRadius(panel, 12f);
            screen.Add(panel);

            Label title = ScreenUiFactory.Text(panel, GameGuideContent.Title, 40, ScreenUiFactory.Ink);
            title.style.marginBottom = 28f;

            BuildLegend(panel);
            BuildTips(panel);

            Label prompt = ScreenUiFactory.Text(panel, GameGuideContent.DismissPrompt, 22, ScreenUiFactory.InkDim);
            prompt.style.marginTop = 32f;
        }

        // 방향 4색 범례 — 카드의 핵심. 색 칩이 문장보다 압도적으로 빠르게 방향-색 대응을 전달한다.
        private void BuildLegend(VisualElement parent)
        {
            Label caption = ScreenUiFactory.Text(parent, GameGuideContent.LegendCaption, 24, ScreenUiFactory.Ink);
            caption.style.marginBottom = 14f;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            row.style.marginBottom = 30f;
            parent.Add(row);

            IReadOnlyList<GameGuideContent.LegendEntry> legend = GameGuideContent.Legend;
            for (int i = 0; i < legend.Count; i++)
            {
                BuildLegendChip(row, legend[i]);
            }
        }

        private void BuildLegendChip(VisualElement parent, GameGuideContent.LegendEntry entry)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.marginLeft = 16f;
            item.style.marginRight = 16f;
            parent.Add(item);

            var chip = new VisualElement();
            chip.style.width = ChipDiameter;
            chip.style.height = ChipDiameter;
            // 색은 반드시 팔레트에서 — 하드코딩 금지(#112). #83으로 색 매핑이 바뀌어도 자동으로 따라간다.
            chip.style.backgroundColor = GameGuideContent.ResolveColor(_palette, entry.Direction);
            chip.style.marginRight = 8f;
            ScreenUiFactory.SetBorderRadius(chip, ChipDiameter * 0.5f);
            item.Add(chip);

            var label = new Label(entry.Label);
            label.style.color = ScreenUiFactory.Ink;
            label.style.fontSize = 22;
            item.Add(label);
        }

        // 안내 문구 5종을 번호 매긴 목록으로 쌓는다.
        private void BuildTips(VisualElement parent)
        {
            IReadOnlyList<string> tips = GameGuideContent.Tips;
            for (int i = 0; i < tips.Count; i++)
            {
                var line = new VisualElement();
                line.style.flexDirection = FlexDirection.Row;
                line.style.width = PanelWidth - 104f; // 좌우 패딩(52*2)을 뺀 내부 폭
                line.style.marginTop = 6f;
                line.style.marginBottom = 6f;
                parent.Add(line);

                var bullet = new Label($"{i + 1}.");
                bullet.style.color = ScreenUiFactory.Seal;
                bullet.style.fontSize = 22;
                bullet.style.minWidth = 28f;
                bullet.style.marginRight = 8f;
                line.Add(bullet);

                var text = new Label(tips[i]);
                text.style.color = ScreenUiFactory.Ink;
                text.style.fontSize = 22;
                text.style.whiteSpace = WhiteSpace.Normal;
                text.style.flexGrow = 1f;
                text.style.flexShrink = 1f;
                line.Add(text);
            }
        }

        private void Show()
        {
            _phase = Phase.Shown;
            TimeScaleService.SetPause(PauseReason.GuideOverlay, true);
            GameplayInputGate.SetSuppression(InputSuppressionReason.GuideOverlay, true);
            SetVisible(true);
        }

        private void Dismiss()
        {
            if (_phase != Phase.Shown)
            {
                return;
            }

            _phase = Phase.Dismissed;
            SetVisible(false);
            Release();
        }

        // 정지·입력 억제 해제. 여러 번 불려도 안전하다 — 서비스가 상태가 실제로 바뀔 때만 반영한다.
        private static void Release()
        {
            TimeScaleService.SetPause(PauseReason.GuideOverlay, false);
            GameplayInputGate.SetSuppression(InputSuppressionReason.GuideOverlay, false);
        }
    }
}
