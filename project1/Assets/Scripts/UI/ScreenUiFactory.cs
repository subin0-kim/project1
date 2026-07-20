using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mukseon.UI
{
    /// <summary>
    /// 메타 화면(타이틀/캐릭터선택/결과)의 VisualElement 생성 헬퍼(#36).
    ///
    /// 이 프로젝트에는 UXML/USS 에셋이 하나도 없고 모든 UI를 C#에서 인라인 스타일로 조립한다
    /// (<see cref="Gameplay.UI.GameplayHudBootstrapper"/>). 신규 화면도 같은 컨벤션을 따른다.
    /// HUD 쪽에 같은 역할의 private 헬퍼가 있지만, 1,200줄이 넘는 그 파일을 건드려 회귀를 만드는 대신
    /// 메타 화면용으로 별도 헬퍼를 둔다.
    ///
    /// HUD가 1920x1080 가로 기준으로 하드코딩돼 있어 좌표계를 맞추기 위해 동일 기준을 쓴다.
    /// (CLAUDE.md의 세로 모드 전환은 HUD 레이아웃 전체가 얽혀 있어 별도 과제다.)
    /// </summary>
    internal static class ScreenUiFactory
    {
        public static readonly Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);

        // 수묵화 톤: 먹빛 배경 + 화선지색 글자 + 낙관(붉은 인장) 강조색.
        public static readonly Color Backdrop = new Color(0.05f, 0.05f, 0.06f, 1f);
        public static readonly Color CardFace = new Color(0.10f, 0.10f, 0.13f, 0.98f);
        public static readonly Color CardFaceLocked = new Color(0.07f, 0.07f, 0.08f, 0.98f);
        public static readonly Color Ink = new Color(0.92f, 0.90f, 0.86f, 1f);
        public static readonly Color InkDim = new Color(0.55f, 0.54f, 0.51f, 1f);
        public static readonly Color Seal = new Color(0.72f, 0.18f, 0.16f, 1f);

        /// <summary>
        /// 런타임 PanelSettings를 만든다. 에셋이 아니라 인스턴스이므로 소유자가 파기해야 누수되지 않는다
        /// (<see cref="ScreenControllerBase.OnDestroy"/>가 담당).
        /// </summary>
        public static PanelSettings CreatePanelSettings(int sortingOrder)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = ReferenceResolution;
            settings.match = 0.5f;
            settings.sortingOrder = sortingOrder;
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UnityDefaultRuntimeTheme");
            return settings;
        }

        /// <summary>화면 전체를 덮고 내용물을 중앙 정렬하는 세로 컬럼.</summary>
        public static VisualElement Screen(VisualElement parent, Color background)
        {
            var screen = new VisualElement();
            screen.style.position = Position.Absolute;
            screen.style.left = 0f;
            screen.style.top = 0f;
            screen.style.right = 0f;
            screen.style.bottom = 0f;
            screen.style.backgroundColor = background;
            screen.style.flexDirection = FlexDirection.Column;
            screen.style.justifyContent = Justify.Center;
            screen.style.alignItems = Align.Center;
            parent.Add(screen);
            return screen;
        }

        /// <summary>가로로 늘어놓는 행(캐릭터 카드 나열용).</summary>
        public static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            row.style.alignItems = Align.Center;
            parent.Add(row);
            return row;
        }

        public static Label Text(VisualElement parent, string text, int fontSize, Color color)
        {
            var label = new Label(text);
            label.style.color = color;
            label.style.fontSize = fontSize;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);
            return label;
        }

        /// <summary>메뉴 버튼. 기본 테마의 패딩을 걷어내고 크기를 직접 지정한다(HUD 카드 버튼과 동일한 방식).</summary>
        public static Button MenuButton(VisualElement parent, string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.width = 260f;
            button.style.height = 64f;
            button.style.marginTop = 10f;
            button.style.marginBottom = 10f;
            button.style.marginLeft = 8f;
            button.style.marginRight = 8f;
            button.style.paddingTop = 0f;
            button.style.paddingBottom = 0f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.fontSize = 24;
            button.style.color = Ink;
            button.style.backgroundColor = CardFace;
            SetBorderRadius(button, 6f);
            parent.Add(button);
            return button;
        }

        /// <summary>내용을 직접 채워 넣는 카드형 버튼(캐릭터 선택용). 기본 text는 비운다.</summary>
        public static Button CardButton(VisualElement parent, Action onClick, float width, float height)
        {
            var button = new Button(onClick) { text = string.Empty };
            button.style.width = width;
            button.style.height = height;
            button.style.marginLeft = 14f;
            button.style.marginRight = 14f;
            button.style.paddingTop = 18f;
            button.style.paddingBottom = 18f;
            button.style.paddingLeft = 18f;
            button.style.paddingRight = 18f;
            button.style.backgroundColor = CardFace;
            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.FlexStart;
            SetBorderRadius(button, 8f);
            parent.Add(button);
            return button;
        }

        /// <summary>결산 항목 한 줄: 좌측 라벨 + 우측 값.</summary>
        public static Label StatRow(VisualElement parent, string caption, float width)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.width = width;
            row.style.marginTop = 8f;
            row.style.marginBottom = 8f;
            parent.Add(row);

            var captionLabel = new Label(caption);
            captionLabel.style.color = InkDim;
            captionLabel.style.fontSize = 22;
            row.Add(captionLabel);

            var valueLabel = new Label(string.Empty);
            valueLabel.style.color = Ink;
            valueLabel.style.fontSize = 22;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(valueLabel);

            return valueLabel;
        }

        public static void SetBorderRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }
    }
}
