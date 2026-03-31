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

        private void Update()
        {
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
