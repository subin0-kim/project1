using Mukseon.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mukseon.Gameplay.UI
{
    /// <summary>
    /// 적 첫 등장 연출의 표시 담당(#70). 상단 이름표 배너와 대상 개체를 가리키는 화살표를 소유한다.
    ///
    /// 언제 무엇을 띄울지는 <see cref="EnemyIntroductionPresenter"/>가 정하고, 이 클래스는
    /// '어떻게 보이는가'만 안다. MonoBehaviour가 아니므로 UIDocument 수명은 프레젠터가 관리한다.
    /// </summary>
    internal sealed class EnemyIntroductionView
    {
        private static class Strings
        {
            public const string Caption = "새로운 적";
            public const string Marker = "▼";
        }

        // 화살표를 적 머리 위로 띄우는 월드 오프셋. HUD 방향 오브(1.6)보다 위에 둬서 겹치지 않게 한다.
        private const float MarkerWorldYOffset = 2.3f;

        private readonly VisualElement _root;
        private readonly VisualElement _banner;
        private readonly Label _nameLabel;
        private readonly Label _marker;

        public EnemyIntroductionView(VisualElement root)
        {
            _root = root;
            _banner = BuildBanner(root, out _nameLabel);
            _marker = BuildMarker(root);
        }

        /// <summary>배너에 적 이름을 넣고 표시한다. 화살표는 위치가 정해질 때까지 감춰 둔다.</summary>
        public void ShowBanner(string displayName)
        {
            _nameLabel.text = displayName;
            _banner.style.display = DisplayStyle.Flex;
            _banner.style.opacity = 1f;
            _marker.style.display = DisplayStyle.None;
        }

        /// <summary>배너와 화살표를 모두 감춘다.</summary>
        public void Hide()
        {
            _banner.style.display = DisplayStyle.None;
            _marker.style.display = DisplayStyle.None;
        }

        /// <summary>페이드용 불투명도(0~1)를 배너와 화살표에 함께 적용한다.</summary>
        public void SetOpacity(float opacity)
        {
            float clamped = Mathf.Clamp01(opacity);
            _banner.style.opacity = clamped;
            _marker.style.opacity = clamped;
        }

        public void HideMarker()
        {
            _marker.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// 화살표를 월드 좌표 위(<see cref="MarkerWorldYOffset"/>만큼 상단)로 옮긴다.
        /// 카메라 뒤로 넘어가면 감춘다.
        /// </summary>
        public void UpdateMarker(Camera camera, Vector3 worldPosition)
        {
            if (camera == null)
            {
                HideMarker();
                return;
            }

            Vector3 screenPoint = camera.WorldToScreenPoint(worldPosition + Vector3.up * MarkerWorldYOffset);
            if (screenPoint.z <= 0f)
            {
                HideMarker();
                return;
            }

            // UI Toolkit 패널 좌표는 좌상단 원점이라 스크린 y를 뒤집는다(HUD 월드 요소와 동일한 변환).
            var flipped = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
            IPanel panel = _root?.panel;
            Vector2 panelPos = panel != null ? RuntimePanelUtils.ScreenToPanel(panel, flipped) : flipped;

            _marker.style.display = DisplayStyle.Flex;
            _marker.style.left = panelPos.x;
            _marker.style.top = panelPos.y;
            _marker.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
        }

        private static VisualElement BuildBanner(VisualElement root, out Label nameLabel)
        {
            var banner = new VisualElement();
            banner.style.position = Position.Absolute;
            banner.style.top = 96f;
            banner.style.left = Length.Percent(50f);
            banner.style.translate = new Translate(Length.Percent(-50f), 0f);
            banner.style.paddingTop = 10f;
            banner.style.paddingBottom = 12f;
            banner.style.paddingLeft = 34f;
            banner.style.paddingRight = 34f;
            banner.style.alignItems = Align.Center;
            banner.style.backgroundColor = ScreenUiFactory.CardFace;
            banner.style.display = DisplayStyle.None;
            banner.pickingMode = PickingMode.Ignore;
            ScreenUiFactory.SetBorderRadius(banner, 8f);
            root.Add(banner);

            Label caption = ScreenUiFactory.Text(banner, Strings.Caption, 18, ScreenUiFactory.Seal);
            caption.style.marginBottom = 2f;
            caption.pickingMode = PickingMode.Ignore;

            nameLabel = ScreenUiFactory.Text(banner, string.Empty, 34, ScreenUiFactory.Ink);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.pickingMode = PickingMode.Ignore;

            return banner;
        }

        private static Label BuildMarker(VisualElement root)
        {
            var marker = new Label(Strings.Marker);
            marker.style.position = Position.Absolute;
            marker.style.fontSize = 30f;
            marker.style.color = ScreenUiFactory.Seal;
            marker.style.unityTextAlign = TextAnchor.MiddleCenter;
            marker.style.display = DisplayStyle.None;
            marker.pickingMode = PickingMode.Ignore;
            root.Add(marker);
            return marker;
        }
    }
}
