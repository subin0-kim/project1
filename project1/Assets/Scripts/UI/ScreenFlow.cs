using System.Collections;
using Mukseon.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Mukseon.UI
{
    /// <summary>
    /// 화면(씬) 전환의 단일 소유자(#36). 비동기 로드 + 페이드를 담당한다.
    ///
    /// 씬에 배치하지 않고 스스로 생성해 <c>DontDestroyOnLoad</c>로 살아남는다. 이 프로젝트에서
    /// 확립된 패턴이다(<see cref="Gameplay.UI.GameplayHudBootstrapper"/>, <c>GameplayInputGate</c>).
    /// </summary>
    [DisallowMultipleComponent]
    public class ScreenFlow : MonoBehaviour
    {
        public const string TitleScene = "Title";
        public const string CharacterSelectScene = "CharacterSelect";
        public const string GameplayScene = "SampleScene";

        private const string RootObjectName = "ScreenFlowRuntime";
        private const float FadeSeconds = 0.25f;

        // 페이드는 모든 화면(HUD 500, 결과 600) 위를 덮어야 한다.
        private const int FadeSortingOrder = 1000;

        private static ScreenFlow _instance;

        private PanelSettings _panelSettings;
        private VisualElement _fade;
        private bool _isTransitioning;

        // resolvedStyle은 레이아웃이 한 번 돌아야 유효하므로, 페이드 시작 시점에 읽으면 부정확하다.
        // 현재 불투명도를 직접 들고 보간한다.
        private float _fadeOpacity;

        public static bool IsTransitioning => _instance != null && _instance._isTransitioning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureScreenFlow()
        {
            if (FindInstance() != null)
            {
                return;
            }

            var root = new GameObject(RootObjectName);
            root.AddComponent<ScreenFlow>();
        }

        private static ScreenFlow FindInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<ScreenFlow>(FindObjectsInactive.Include);
#else
            return Object.FindObjectOfType<ScreenFlow>();
#endif
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildFadeOverlay();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            if (_panelSettings != null)
            {
                Destroy(_panelSettings);
                _panelSettings = null;
            }
        }

        private void BuildFadeOverlay()
        {
            _panelSettings = ScreenUiFactory.CreatePanelSettings(FadeSortingOrder);

            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;

            VisualElement root = document.rootVisualElement;
            root.style.flexGrow = 1f;
            // 페이드가 투명할 때 그 아래 화면의 클릭을 가로채지 않아야 한다.
            root.pickingMode = PickingMode.Ignore;

            _fade = ScreenUiFactory.Screen(root, Color.black);
            _fade.pickingMode = PickingMode.Ignore;
            _fadeOpacity = 0f;
            _fade.style.opacity = _fadeOpacity;
            _fade.style.display = DisplayStyle.None;
        }

        public static void LoadTitle() => Load(TitleScene);

        public static void LoadCharacterSelect() => Load(CharacterSelectScene);

        public static void LoadGameplay() => Load(GameplayScene);

        /// <summary>현재 게임플레이 씬을 다시 로드한다(재도전).</summary>
        public static void ReloadGameplay() => Load(SceneManager.GetActiveScene().name);

        private static void Load(string sceneName)
        {
            ScreenFlow flow = FindInstance();
            if (flow == null)
            {
                // 부트스트랩 전(런타임 초기화 이전)에 호출된 경우의 최후 폴백.
                SceneManager.LoadScene(sceneName);
                return;
            }

            if (flow._isTransitioning)
            {
                return;
            }

            flow.StartCoroutine(flow.TransitionTo(sceneName));
        }

        private IEnumerator TransitionTo(string sceneName)
        {
            _isTransitioning = true;
            TimeScaleService.SetPause(PauseReason.ScreenTransition, true);

            yield return FadeTo(1f);

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
            if (load != null)
            {
                while (!load.isDone)
                {
                    yield return null;
                }
            }

            // 새 씬은 정지 원인이 하나도 없는 상태로 시작해야 한다. 이전 씬의 소유자들이 스스로 해제하긴
            // 하지만(#109), 하나라도 빠뜨리면 런이 정지된 채 시작되는 치명적 증상이 되므로 여기서 확실히 끊는다.
            TimeScaleService.Reset();
            TimeScaleService.SetPause(PauseReason.ScreenTransition, true);

            yield return FadeTo(0f);

            TimeScaleService.SetPause(PauseReason.ScreenTransition, false);
            _isTransitioning = false;
        }

        // 페이드는 정지(timeScale = 0) 중에도 진행되어야 하므로 unscaledDeltaTime으로 보간한다.
        private IEnumerator FadeTo(float targetOpacity)
        {
            if (_fade == null)
            {
                yield break;
            }

            _fade.style.display = DisplayStyle.Flex;

            float startOpacity = _fadeOpacity;
            float elapsed = 0f;

            while (elapsed < FadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / FadeSeconds);
                _fadeOpacity = Mathf.Lerp(startOpacity, targetOpacity, t);
                _fade.style.opacity = _fadeOpacity;
                yield return null;
            }

            _fadeOpacity = targetOpacity;
            _fade.style.opacity = _fadeOpacity;

            if (Mathf.Approximately(targetOpacity, 0f))
            {
                _fade.style.display = DisplayStyle.None;
            }
        }
    }
}
