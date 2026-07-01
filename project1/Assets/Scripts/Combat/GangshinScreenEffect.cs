using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 강신 발동 중 화면 색상 반전 연출(#30). GangshinController의 상태 변화를 구독해
    /// Active 진입 시 풀스크린 오버레이를 페이드 인, 종료 시 페이드 아웃한다.
    ///
    /// 실제 "색상 반전"은 오버레이 그래픽에 색 반전 머티리얼(예: 풀스크린 반전 셰이더 / Blend 설정)을
    /// 인스펙터에서 지정해 얻는다. 머티리얼 미지정 시 지정한 오버레이 색으로 대체 연출(플래시)한다.
    /// 스크립트는 표시 여부(알파)만 제어해 뷰 레이어와 로직을 분리한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GangshinScreenEffect : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("구독할 강신 컨트롤러. 미지정 시 부모에서 탐색.")]
        private GangshinController _controller;

        [SerializeField, Tooltip("풀스크린 오버레이 CanvasGroup. 미지정 시 이 오브젝트에서 탐색.")]
        private CanvasGroup _overlay;

        [Header("Fade")]
        [SerializeField, Range(0f, 1f)]
        private float _activeAlpha = 1f;

        [SerializeField, Min(0f), Tooltip("발동 진입 시 페이드 인 시간(초).")]
        private float _fadeInDuration = 0.12f;

        [SerializeField, Min(0f), Tooltip("발동 종료 시 페이드 아웃 시간(초).")]
        private float _fadeOutDuration = 0.3f;

        private float _targetAlpha;
        private float _fadeRate;

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponentInParent<GangshinController>();
            }

            if (_overlay == null)
            {
                _overlay = GetComponent<CanvasGroup>();
            }

            if (_overlay != null)
            {
                _overlay.alpha = 0f;
                _overlay.blocksRaycasts = false;
                _overlay.interactable = false;
            }
        }

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnStateChanged -= HandleStateChanged;
            }

            if (_overlay != null)
            {
                _overlay.alpha = 0f;
            }

            _targetAlpha = 0f;
        }

        private void HandleStateChanged(GangshinState state)
        {
            bool active = state == GangshinState.Active;
            _targetAlpha = active ? Mathf.Clamp01(_activeAlpha) : 0f;

            float duration = active ? _fadeInDuration : _fadeOutDuration;
            float span = Mathf.Clamp01(_activeAlpha);
            // duration이 0이면 즉시 목표 알파로 전환한다.
            _fadeRate = duration > 0f ? span / duration : float.MaxValue;
        }

        private void Update()
        {
            if (_overlay == null || Mathf.Approximately(_overlay.alpha, _targetAlpha))
            {
                return;
            }

            // 발동 중 Time.timeScale이 낮아지므로 unscaledDeltaTime으로 연출 속도를 일정하게 유지한다.
            _overlay.alpha = Mathf.MoveTowards(_overlay.alpha, _targetAlpha, _fadeRate * Time.unscaledDeltaTime);
        }
    }
}
