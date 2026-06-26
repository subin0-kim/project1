using System;
using UnityEngine;

namespace Mukseon.Core.Input
{
    [DisallowMultipleComponent]
    public class GangshinInputDetector : MonoBehaviour
    {
        [SerializeField, Min(0.05f)]
        private float _holdDuration = 0.45f;

        [SerializeField, Min(0.05f)]
        private float _doubleTapInterval = 0.3f;

        [SerializeField, Min(1f)]
        private float _maxTapTravel = 24f;

        public event Action OnActivationRequested;

        private Vector2 _pressStartPosition;
        private float _pressStartTime;
        private float _lastTapTime = float.NegativeInfinity;
        private bool _isPressing;
        private bool _holdTriggered;
        private bool _inputEnabled = true;

        /// <summary>현재 강신(홀드/더블 탭) 입력 감지가 활성 상태인지 여부.</summary>
        public bool IsInputEnabled => _inputEnabled;

        /// <summary>
        /// 외부(게임오버/레벨업 일시정지)에서 강신 입력 감지를 켜고 끈다(#42).
        /// 비활성화 시 진행 중이던 누름/더블 탭 추적을 취소해, 일시정지 경계를 넘는 입력이
        /// 재활성화 직후 강신을 오발동하지 않도록 한다.
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;
            if (!enabled)
            {
                CancelPress();
                _lastTapTime = float.NegativeInfinity;
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
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    UpdateHold(touch.position);
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

            if (_isPressing)
            {
                UpdateHold(UnityEngine.Input.mousePosition);
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
            _pressStartTime = Time.unscaledTime;
            _isPressing = true;
            _holdTriggered = false;
        }

        private void UpdateHold(Vector2 currentPosition)
        {
            if (!_isPressing || _holdTriggered)
            {
                return;
            }

            if (Vector2.Distance(_pressStartPosition, currentPosition) > _maxTapTravel)
            {
                return;
            }

            if (Time.unscaledTime - _pressStartTime >= _holdDuration)
            {
                _holdTriggered = true;
                _lastTapTime = float.NegativeInfinity;
                OnActivationRequested?.Invoke();
            }
        }

        private void EndPress(Vector2 endPosition)
        {
            if (!_isPressing)
            {
                return;
            }

            bool isTap = !_holdTriggered &&
                Vector2.Distance(_pressStartPosition, endPosition) <= _maxTapTravel;

            if (isTap)
            {
                if (Time.unscaledTime - _lastTapTime <= _doubleTapInterval)
                {
                    _lastTapTime = float.NegativeInfinity;
                    OnActivationRequested?.Invoke();
                }
                else
                {
                    _lastTapTime = Time.unscaledTime;
                }
            }

            CancelPress();
        }

        private void CancelPress()
        {
            _isPressing = false;
            _holdTriggered = false;
        }
    }
}
