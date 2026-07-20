// 시연/개발 전용(#111). DemoCheatController와 함께 출시 빌드에서 제외된다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using Mukseon.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mukseon.Dev
{
    /// <summary>
    /// 치트 키 목록과 활성 상태를 화면에 표시한다(#111).
    ///
    /// 무적 표시가 핵심이다 — 발표자가 무적을 켜둔 걸 잊고 "이 정도 난이도입니다"라고 설명하면
    /// 시연 자체가 거짓이 된다. 켜져 있는 동안은 눈에 띄게 보여야 한다.
    ///
    /// MonoBehaviour가 아니라 <see cref="DemoCheatController"/>가 소유하는 일반 클래스다.
    /// 치트 오브젝트 하나에 컴포넌트를 더 붙일 이유가 없고, 수명이 컨트롤러와 정확히 같다.
    /// </summary>
    internal sealed class DemoCheatOverlay : IDisposable
    {
        // 페이드 오버레이(1000)보다도 위. 화면 전환 중에도 치트 상태는 보여야 한다.
        private const int SortingOrder = 1100;

        private static readonly Color PanelBackground = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color WarningColor = new Color(0.95f, 0.35f, 0.30f, 1f);

        private readonly DemoCheatController _controller;

        private GameObject _root;
        private UIDocument _document;
        private PanelSettings _panelSettings;
        private Label _invincibleLabel;

        private bool _isVisible = true;
        private bool _lastInvincible;

        public DemoCheatOverlay(DemoCheatController controller)
        {
            _controller = controller;
            Build();
        }

        public void ToggleVisible()
        {
            _isVisible = !_isVisible;
            ApplyVisibility();
        }

        /// <summary>무적 상태가 바뀌었을 때만 표시를 갱신한다(매 프레임 스타일 대입 방지).</summary>
        public void Update()
        {
            if (_controller == null || _invincibleLabel == null)
            {
                return;
            }

            bool invincible = _controller.IsInvincible;
            if (invincible == _lastInvincible)
            {
                return;
            }

            _lastInvincible = invincible;
            _invincibleLabel.style.display = invincible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Dispose()
        {
            // PanelSettings는 에셋이 아니라 런타임 인스턴스라 직접 파기하지 않으면 누수된다(#36과 동일).
            if (_panelSettings != null)
            {
                UnityEngine.Object.Destroy(_panelSettings);
                _panelSettings = null;
            }

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            _document = null;
            _invincibleLabel = null;
        }

        private void Build()
        {
            _root = new GameObject("DemoCheatOverlay");
            _root.transform.SetParent(_controller.transform, false);

            _panelSettings = ScreenUiFactory.CreatePanelSettings(SortingOrder);
            _document = _root.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;

            VisualElement root = _document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            VisualElement panel = CreatePanel(root);
            _invincibleLabel = CreateInvincibleLabel(panel);

            CreateTitle(panel);
            CreateBindingList(panel);

            ApplyVisibility();
        }

        // 좌상단 고정. 전투는 화면 중앙에서 일어나므로 구석이 시연 화면을 가장 덜 가린다.
        private static VisualElement CreatePanel(VisualElement parent)
        {
            var panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.left = 16f;
            panel.style.top = 16f;
            panel.style.paddingLeft = 12f;
            panel.style.paddingRight = 12f;
            panel.style.paddingTop = 8f;
            panel.style.paddingBottom = 8f;
            panel.style.backgroundColor = PanelBackground;
            panel.style.flexDirection = FlexDirection.Column;
            panel.pickingMode = PickingMode.Ignore;
            parent.Add(panel);
            return panel;
        }

        private static Label CreateInvincibleLabel(VisualElement parent)
        {
            var label = new Label("● 무적 ON — 시연용 치트");
            label.style.color = WarningColor;
            label.style.fontSize = 22f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 6f;
            label.style.display = DisplayStyle.None;
            label.pickingMode = PickingMode.Ignore;
            parent.Add(label);
            return label;
        }

        private static void CreateTitle(VisualElement parent)
        {
            var title = new Label("시연용 치트");
            title.style.color = ScreenUiFactory.InkDim;
            title.style.fontSize = 16f;
            title.style.marginBottom = 4f;
            title.pickingMode = PickingMode.Ignore;
            parent.Add(title);
        }

        private static void CreateBindingList(VisualElement parent)
        {
            IReadOnlyList<DemoCheatBindings.Binding> bindings = DemoCheatBindings.All;
            for (int i = 0; i < bindings.Count; i++)
            {
                var line = new Label(bindings[i].DisplayText);
                line.style.color = ScreenUiFactory.Ink;
                line.style.fontSize = 14f;
                line.pickingMode = PickingMode.Ignore;
                parent.Add(line);
            }
        }

        private void ApplyVisibility()
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}

#endif
