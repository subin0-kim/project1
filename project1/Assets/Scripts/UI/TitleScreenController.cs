using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mukseon.UI
{
    /// <summary>
    /// 타이틀 화면(#36): 로고와 "터치하여 시작". 아무 곳이나 누르면 캐릭터 선택으로 넘어간다.
    /// </summary>
    public class TitleScreenController : ScreenControllerBase
    {
        // 자기 씬에 단독으로 뜨지만, HUD(500)가 DontDestroyOnLoad로 넘어올 수 있으므로 그 위에 둔다.
        private const int TitleSortingOrder = 700;

        private static class Strings
        {
            public const string Title = "묵선";
            public const string Subtitle = "墨線";
            public const string TouchToStart = "터치하여 시작";
            public const string Shrine = "신당";
            public const string Settings = "환경설정";
        }

        protected override int SortingOrder => TitleSortingOrder;

        private Label _prompt;
        private float _blinkTimer;

        protected override void BuildUi(VisualElement root)
        {
            VisualElement screen = ScreenUiFactory.Screen(root, ScreenUiFactory.Backdrop);

            Label title = ScreenUiFactory.Text(screen, Strings.Title, 96, ScreenUiFactory.Ink);
            title.style.marginBottom = 4f;

            Label subtitle = ScreenUiFactory.Text(screen, Strings.Subtitle, 32, ScreenUiFactory.Seal);
            subtitle.style.marginBottom = 120f;

            _prompt = ScreenUiFactory.Text(screen, Strings.TouchToStart, 26, ScreenUiFactory.InkDim);

            // "아무 곳이나 터치"는 버튼 하나로 표현할 수 없으므로 화면 전체에서 포인터를 받는다.
            screen.RegisterCallback<PointerDownEvent>(_ => HandleStart());

            BuildCornerButtons(screen);
        }

        /// <summary>
        /// 화면 아래 양쪽 진입점: 신당(#34)과 환경설정(#83). 둘 다 런을 시작하기 전에 들르는 곳이라
        /// "터치하여 시작"과 같은 화면에 놓이되, 시작 동작과 헷갈리지 않도록 구석에 둔다.
        ///
        /// 화면 전체가 "터치하여 시작"을 받으므로, 버튼의 포인터 이벤트가 화면으로 버블링되면
        /// 신당·설정을 여는 순간 캐릭터 선택으로도 넘어가 버린다. 버튼에서 전파를 끊는다.
        /// </summary>
        private void BuildCornerButtons(VisualElement screen)
        {
            BuildCornerButton(screen, Strings.Shrine, OpenShrine).style.left = 40f;
            BuildCornerButton(screen, Strings.Settings, OpenSettings).style.right = 40f;
        }

        private static Button BuildCornerButton(VisualElement screen, string text, Action onClick)
        {
            Button button = ScreenUiFactory.MenuButton(screen, text, onClick);
            button.style.position = Position.Absolute;
            button.style.bottom = 40f;
            button.style.width = 200f;
            button.style.height = 56f;
            button.style.fontSize = 22;
            button.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            return button;
        }

        private void OpenShrine()
        {
            if (ScreenFlow.IsTransitioning || SettingsOverlay.IsOpen)
            {
                return;
            }

            ScreenFlow.LoadShrine();
        }

        private void OpenSettings()
        {
            if (ScreenFlow.IsTransitioning)
            {
                return;
            }

            SettingsOverlay.Open();
        }

        // 프롬프트 점멸. 화면 전환 중 정지될 수 있으므로 unscaledDeltaTime을 쓴다.
        private void Update()
        {
            if (_prompt == null)
            {
                return;
            }

            _blinkTimer += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(0.25f, 1f, Mathf.PingPong(_blinkTimer, 1f));
            Color color = ScreenUiFactory.InkDim;
            _prompt.style.color = new Color(color.r, color.g, color.b, alpha);
        }

        private void HandleStart()
        {
            // 설정 화면은 별도 패널(정렬 800)이라 이 화면까지 포인터가 내려오지 않지만,
            // 열려 있는 동안 런이 시작되는 사고는 되돌릴 수 없으므로 한 겹 더 막는다.
            if (ScreenFlow.IsTransitioning || SettingsOverlay.IsOpen)
            {
                return;
            }

            ScreenFlow.LoadCharacterSelect();
        }
    }
}
