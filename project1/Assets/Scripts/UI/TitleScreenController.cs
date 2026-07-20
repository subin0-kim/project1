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
            if (ScreenFlow.IsTransitioning)
            {
                return;
            }

            ScreenFlow.LoadCharacterSelect();
        }
    }
}
