using System.Collections.Generic;
using Mukseon.Core;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Mukseon.UI
{
    /// <summary>
    /// 방향 색상 환경설정 오버레이(#83, `combat_system.md` §3): 표시 방식 / 접근성 화살표 /
    /// 방향↔색상 커스텀 매핑을 고르고, 바꾸는 즉시 게임에 반영하고 세이브에 기록한다.
    ///
    /// 씬에 배치하지 않고 <see cref="Open"/> 시점에 스스로 생성해 <c>DontDestroyOnLoad</c>로 살아남는다
    /// (<see cref="ScreenFlow"/>와 같은 패턴). 타이틀과 인게임 양쪽에서 같은 화면을 열어야 하는데,
    /// 씬마다 배치하면 두 벌을 따로 유지해야 하기 때문이다.
    ///
    /// 인게임에서 열 수 있는 것이 이 설정의 요점이다 — 표시 방식은 실제 전투 화면과 대조하지 않으면
    /// 고를 수 없다. 열려 있는 동안 전투를 정지시키고 입력을 막는다.
    /// </summary>
    public sealed class SettingsOverlay : ScreenControllerBase
    {
        // 안내 카드·타이틀(700) 위, 화면 전환 페이드(1000) 아래.
        private const int SettingsSortingOrder = 800;
        private const string RootObjectName = "SettingsOverlayRuntime";
        private const float PanelWidth = 980f;

        private static SettingsOverlay _instance;

        private readonly List<Button> _modeButtons = new List<Button>(3);
        private SettingsColorSection _colorSection;
        private Button _arrowButton;
        private bool _isOpen;

        /// <summary>설정 화면이 떠 있는지. 타이틀 등 "아무 곳이나 탭" 화면이 오탐하지 않도록 노출한다.</summary>
        public static bool IsOpen => _instance != null && _instance._isOpen;

        protected override int SortingOrder => SettingsSortingOrder;

        /// <summary>설정 화면을 연다. 최초 호출 시 런타임 오브젝트를 만든다.</summary>
        public static void Open()
        {
            if (_instance == null)
            {
                var root = new GameObject(RootObjectName);
                // AddComponent가 Awake를 즉시 실행하므로 이 시점에 UI가 조립되고 _instance가 채워진다.
                root.AddComponent<SettingsOverlay>();
            }

            if (_instance != null)
            {
                _instance.Show();
            }
        }

        protected override void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            base.Awake();
            SetVisible(false);

            DirectionColorSettings.OnChanged += HandleSettingsChanged;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        protected override void OnDestroy()
        {
            DirectionColorSettings.OnChanged -= HandleSettingsChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (_instance == this)
            {
                // 정지·입력 억제를 쥔 채 파기되면 원인이 영원히 남는다(#109와 같은 종류의 누수).
                Release();
                _instance = null;
            }

            base.OnDestroy();
        }

        protected override void BuildUi(VisualElement root)
        {
            VisualElement screen = ScreenUiFactory.Screen(root, new Color(0f, 0f, 0f, 0.88f));

            var panel = new VisualElement();
            panel.style.width = PanelWidth;
            panel.style.paddingTop = 34f;
            panel.style.paddingBottom = 34f;
            panel.style.paddingLeft = 44f;
            panel.style.paddingRight = 44f;
            panel.style.backgroundColor = ScreenUiFactory.CardFace;
            ScreenUiFactory.SetBorderRadius(panel, 12f);
            screen.Add(panel);

            Label title = ScreenUiFactory.Text(panel, SettingsScreenContent.Title, 40, ScreenUiFactory.Ink);
            title.style.marginBottom = 24f;

            BuildDisplayModeSection(panel);
            BuildAccessibilitySection(panel);
            BuildColorSection(panel);
            BuildFooter(panel);

            Refresh();
        }

        private void BuildDisplayModeSection(VisualElement parent)
        {
            SectionHeading(parent, SettingsScreenContent.DisplaySection);

            VisualElement row = ScreenUiFactory.Row(parent);
            row.style.marginBottom = 18f;

            IReadOnlyList<SettingsScreenContent.DisplayModeOption> options = SettingsScreenContent.DisplayModes;
            for (int i = 0; i < options.Count; i++)
            {
                DirectionDisplayMode mode = options[i].Mode;
                Button button = ScreenUiFactory.MenuButton(row, options[i].Label, () => Mutate(mode));
                button.style.width = 220f;
                button.style.height = 56f;
                button.style.fontSize = 22;
                _modeButtons.Add(button);
            }
        }

        private void BuildAccessibilitySection(VisualElement parent)
        {
            SectionHeading(parent, SettingsScreenContent.AccessibilitySection);

            VisualElement row = ScreenUiFactory.Row(parent);
            row.style.justifyContent = Justify.Center;

            var label = new Label(SettingsScreenContent.ArrowAssistLabel);
            label.style.color = ScreenUiFactory.Ink;
            label.style.fontSize = 24;
            label.style.marginRight = 18f;
            row.Add(label);

            // 토글 상태는 버튼 텍스트("켬"/"끔")로 표현한다 — 런타임 UI Toolkit의 기본 Toggle은
            // 이 화면의 수묵 톤과 어울리지 않고, 터치 타깃도 체크박스라 너무 작다.
            _arrowButton = ScreenUiFactory.MenuButton(row, SettingsScreenContent.Off, ToggleArrowAssist);
            _arrowButton.style.width = 120f;
            _arrowButton.style.height = 52f;
            _arrowButton.style.fontSize = 22;

            Label hint = ScreenUiFactory.Text(parent, SettingsScreenContent.ArrowAssistHint, 18, ScreenUiFactory.InkDim);
            hint.style.marginTop = 4f;
            hint.style.marginBottom = 18f;
        }

        private void BuildColorSection(VisualElement parent)
        {
            SectionHeading(parent, SettingsScreenContent.ColorSection);

            Label hint = ScreenUiFactory.Text(parent, SettingsScreenContent.ColorHint, 18, ScreenUiFactory.InkDim);
            hint.style.marginBottom = 10f;

            var rows = new VisualElement();
            rows.style.alignItems = Align.Center;
            parent.Add(rows);

            // 팔레트 에셋은 아직 프로젝트에 없다 — 모든 소비자가 정적 디폴트로 폴백하는 상태다(#82).
            // 에셋이 생기면 이 null 자리에 주입하면 설정 화면의 "기본색"도 함께 따라간다.
            _colorSection = new SettingsColorSection(rows, null, Persist);
        }

        private void BuildFooter(VisualElement parent)
        {
            VisualElement row = ScreenUiFactory.Row(parent);
            row.style.marginTop = 22f;

            Button reset = ScreenUiFactory.MenuButton(row, SettingsScreenContent.ResetColors, ResetColors);
            reset.style.width = 200f;
            reset.style.height = 58f;

            Button close = ScreenUiFactory.MenuButton(row, SettingsScreenContent.Close, Close);
            close.style.width = 200f;
            close.style.height = 58f;
            close.style.backgroundColor = ScreenUiFactory.Seal;
        }

        private static void SectionHeading(VisualElement parent, string text)
        {
            Label heading = ScreenUiFactory.Text(parent, text, 26, ScreenUiFactory.Seal);
            heading.style.marginTop = 6f;
            heading.style.marginBottom = 10f;
        }

        private void Show()
        {
            if (_isOpen)
            {
                return;
            }

            _isOpen = true;
            TimeScaleService.SetPause(PauseReason.SettingsOverlay, true);
            GameplayInputGate.SetSuppression(InputSuppressionReason.SettingsOverlay, true);
            Refresh();
            SetVisible(true);
        }

        private void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            SetVisible(false);
            Release();
        }

        // 정지·입력 억제 해제. 여러 번 불려도 안전하다 — 서비스가 상태가 실제로 바뀔 때만 반영한다.
        private static void Release()
        {
            TimeScaleService.SetPause(PauseReason.SettingsOverlay, false);
            GameplayInputGate.SetSuppression(InputSuppressionReason.SettingsOverlay, false);
        }

        // 씬이 바뀌면 이 오버레이가 쥔 정지 원인은 ScreenFlow의 Reset에 지워진다(ScreenFlow.cs).
        // 화면만 남아 조작을 가리는 유령이 되지 않도록 함께 닫는다.
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Close();
        }

        private void Mutate(DirectionDisplayMode mode)
        {
            DirectionColorSettings.SetDisplayMode(mode);
            Persist();
        }

        private void ToggleArrowAssist()
        {
            DirectionColorSettings.SetArrowAssist(!DirectionColorSettings.ArrowAssistEnabled);
            Persist();
        }

        private void ResetColors()
        {
            DirectionColorSettings.ClearCustomColors();
            Persist();
        }

        // 항목 수가 4개 남짓이라 탭마다 즉시 저장해도 비용이 무시할 수준이고,
        // 별도 "저장" 버튼 없이 닫아도 설정이 남는다는 기대에 맞는다.
        private static void Persist()
        {
            DirectionColorSettingsBootstrap.Persist();
        }

        // 설정 변경의 단일 갱신 지점. 색 배정은 다른 방향과 맞바뀔 수 있어(#83) 누른 행만 다시
        // 그리면 부족하고, 항상 전체를 다시 읽어야 화면이 실제 상태와 어긋나지 않는다.
        private void HandleSettingsChanged()
        {
            Refresh();
        }

        private void Refresh()
        {
            IReadOnlyList<SettingsScreenContent.DisplayModeOption> options = SettingsScreenContent.DisplayModes;
            for (int i = 0; i < _modeButtons.Count && i < options.Count; i++)
            {
                bool selected = options[i].Mode == DirectionColorSettings.DisplayMode;
                _modeButtons[i].style.backgroundColor = selected ? ScreenUiFactory.Seal : ScreenUiFactory.CardFaceLocked;
                _modeButtons[i].style.color = selected ? Color.white : ScreenUiFactory.InkDim;
            }

            if (_arrowButton != null)
            {
                bool on = DirectionColorSettings.ArrowAssistEnabled;
                _arrowButton.text = on ? SettingsScreenContent.On : SettingsScreenContent.Off;
                _arrowButton.style.backgroundColor = on ? ScreenUiFactory.Seal : ScreenUiFactory.CardFaceLocked;
                _arrowButton.style.color = on ? Color.white : ScreenUiFactory.InkDim;
            }

            _colorSection?.Refresh();
        }
    }
}
