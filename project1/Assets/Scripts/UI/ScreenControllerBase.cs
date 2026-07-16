using UnityEngine;
using UnityEngine.UIElements;

namespace Mukseon.UI
{
    /// <summary>
    /// 메타 화면 컨트롤러의 공통 뼈대(#36): UIDocument/PanelSettings 생성과 파기를 담당한다.
    /// PanelSettings는 에셋이 아니라 런타임 인스턴스라 반드시 직접 파기해야 한다
    /// (<see cref="Gameplay.UI.GameplayHudBootstrapper"/>와 동일한 이유).
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class ScreenControllerBase : MonoBehaviour
    {
        private UIDocument _document;
        private PanelSettings _panelSettings;

        /// <summary>화면의 루트 요소. <see cref="BuildUi"/> 이후 유효하다.</summary>
        protected VisualElement Root { get; private set; }

        /// <summary>패널 정렬 순서. 인게임 HUD가 500이므로 그 위에 떠야 하는 화면은 더 큰 값을 쓴다.</summary>
        protected abstract int SortingOrder { get; }

        /// <summary>화면 내용을 조립한다. 구현체는 이 안에서만 UI를 만든다.</summary>
        protected abstract void BuildUi(VisualElement root);

        protected virtual void Awake()
        {
            _panelSettings = ScreenUiFactory.CreatePanelSettings(SortingOrder);

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;

            Root = _document.rootVisualElement;
            Root.style.flexGrow = 1f;

            BuildUi(Root);
        }

        protected virtual void OnDestroy()
        {
            if (_panelSettings != null)
            {
                Destroy(_panelSettings);
                _panelSettings = null;
            }
        }

        /// <summary>화면 표시/숨김.</summary>
        protected void SetVisible(bool visible)
        {
            if (Root != null)
            {
                Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
