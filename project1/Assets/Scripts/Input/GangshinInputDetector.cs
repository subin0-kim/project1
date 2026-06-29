using System;
using UnityEngine;

namespace Mukseon.Core.Input
{
    /// <summary>
    /// 강신 발동 입력(더블 탭)을 감지한다. 기획 변경(#60)으로 길게 누르기(홀드) 발동은 폐기되고
    /// 더블 탭으로 단일화되었다. 탭/드래그 분류와 더블 탭 판정은 순수 로직
    /// <see cref="TapGestureRecognizer"/>가 담당하며, 본 컴포넌트는 입력 소스를 읽어 전달하는 얇은 래퍼다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GangshinInputDetector : MonoBehaviour
    {
        // 더블 탭 간격(초). 탭 두 번이 이 시간 이내면 더블 탭으로 인식한다(#60: 300ms).
        [SerializeField, Min(0.05f)]
        private float _doubleTapInterval = 0.3f;

        // 탭/드래그 분기 임계값(px). 이동 거리가 이 값 미만이면 탭, 이상이면 드래그(스와이프)로 본다(#60: 20px).
        [SerializeField, Min(1f)]
        private float _maxTapTravel = 20f;

        /// <summary>더블 탭으로 강신 발동이 요청되었을 때 발생.</summary>
        public event Action OnActivationRequested;

        private TapGestureRecognizer _recognizer;
        private Vector2 _pressStartPosition;
        private bool _isPressing;
        private bool _inputEnabled = true;

        /// <summary>현재 강신(더블 탭) 입력 감지가 활성 상태인지 여부.</summary>
        public bool IsInputEnabled => _inputEnabled;

        private void Awake()
        {
            // 인스펙터에서 덮어쓴 직렬화 값을 반영하기 위해 역직렬화가 끝난 Awake에서 인식기를 만든다.
            _recognizer = new TapGestureRecognizer(_maxTapTravel, _doubleTapInterval);
        }

        /// <summary>
        /// 외부(게임오버/레벨업 일시정지)에서 강신 입력 감지를 켜고 끈다(#42).
        /// 비활성화 시 진행 중이던 누름과 더블 탭 추적을 취소해, 일시정지 경계를 넘는 입력이
        /// 재활성화 직후 강신을 오발동하지 않도록 한다.
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
            if (!enabled)
            {
                CancelPress();
                _recognizer?.Reset();
            }
        }

        private void Update()
        {
            if (!_inputEnabled)
            {
                return;
            }

#if UNITY_EDITOR
            if (UnityEngine.Input.touchCount == 0)
            {
                ProcessMouse();
                return;
            }
#endif

            ProcessTouch();
        }

        private void ProcessTouch()
        {
            if (UnityEngine.Input.touchCount <= 0)
            {
                return;
            }

            Touch touch = UnityEngine.Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    BeginPress(touch.position);
                    break;
                case TouchPhase.Ended:
                    EndPress(touch.position);
                    break;
                case TouchPhase.Canceled:
                    CancelPress();
                    break;
            }
        }

#if UNITY_EDITOR
        private void ProcessMouse()
        {
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                BeginPress(UnityEngine.Input.mousePosition);
            }

            if (UnityEngine.Input.GetMouseButtonUp(0))
            {
                EndPress(UnityEngine.Input.mousePosition);
            }
        }
#endif

        private void BeginPress(Vector2 position)
        {
            _pressStartPosition = position;
            _isPressing = true;
        }

        private void EndPress(Vector2 endPosition)
        {
            if (!_isPressing)
            {
                return;
            }

            _isPressing = false;

            if (_recognizer == null)
            {
                return;
            }

            // 누름→뗌 한 번을 인식기에 넘겨 분류한다. 드래그(스와이프)는 None이라 강신을 발동하지 않는다.
            if (_recognizer.RegisterPress(_pressStartPosition, endPosition, Time.unscaledTime)
                    == TapGestureRecognizer.Gesture.DoubleTap)
            {
#if UNITY_EDITOR
                // 플레이모드 입력 검증용 로그(에디터 전용). 게이지 충전 여부와 무관하게 더블 탭 감지 자체를 확인한다.
                Debug.Log("[GangshinInputDetector] Double tap detected → activation requested");
#endif
                OnActivationRequested?.Invoke();
            }
        }

        private void CancelPress()
        {
            _isPressing = false;
        }
    }
}
