using System;
using UnityEngine;

namespace Mukseon.Core.Input
{
    public class SwipeInputDetector : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        private float _minimumSwipeDistance = 75f;

        public float MinimumSwipeDistance => _minimumSwipeDistance;
        public SwipeDirection LastDetectedDirection { get; private set; } = SwipeDirection.None;

        public event Action<SwipeDirection> OnSwipeDetected;

        /// <summary>터치/마우스 버튼이 눌린 순간 발생. 스크린 좌표를 전달합니다.</summary>
        public event Action<Vector2> OnSwipeBegan;
        /// <summary>터치/마우스가 이동 중인 매 프레임 발생. 스크린 좌표를 전달합니다.</summary>
        public event Action<Vector2> OnSwipeMoved;
        /// <summary>터치/마우스 버튼이 떼어진 순간 발생. 스크린 좌표를 전달합니다.</summary>
        public event Action<Vector2> OnSwipeEnded;

        private Vector2 _startPosition;
        private bool _isTrackingSwipe;

        private void Update()
        {
#if UNITY_EDITOR
            if (UnityEngine.Input.touchCount == 0)
            {
                ProcessMouseInputForEditor();
                return;
            }
#endif

            ProcessTouchInput();
        }

        private void ProcessTouchInput()
        {
            if (UnityEngine.Input.touchCount <= 0)
            {
                return;
            }

            Touch touch = UnityEngine.Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    _startPosition = touch.position;
                    _isTrackingSwipe = true;
                    OnSwipeBegan?.Invoke(touch.position);
                    break;
                case TouchPhase.Moved:
                    if (_isTrackingSwipe)
                    {
                        OnSwipeMoved?.Invoke(touch.position);
                    }
                    break;
                case TouchPhase.Canceled:
                case TouchPhase.Ended:
                    if (_isTrackingSwipe)
                    {
                        OnSwipeEnded?.Invoke(touch.position);
                        HandleSwipe(_startPosition, touch.position);
                    }

                    _isTrackingSwipe = false;
                    break;
            }
        }

#if UNITY_EDITOR
        private void ProcessMouseInputForEditor()
        {
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                _startPosition = UnityEngine.Input.mousePosition;
                _isTrackingSwipe = true;
                OnSwipeBegan?.Invoke(_startPosition);
            }

            if (_isTrackingSwipe && UnityEngine.Input.GetMouseButton(0))
            {
                OnSwipeMoved?.Invoke(UnityEngine.Input.mousePosition);
            }

            if (UnityEngine.Input.GetMouseButtonUp(0) && _isTrackingSwipe)
            {
                OnSwipeEnded?.Invoke(UnityEngine.Input.mousePosition);
                HandleSwipe(_startPosition, UnityEngine.Input.mousePosition);
                _isTrackingSwipe = false;
            }
        }
#endif

        private void HandleSwipe(Vector2 startPosition, Vector2 endPosition)
        {
            SwipeDirection direction = TryResolveSwipe(startPosition, endPosition);
            if (direction == SwipeDirection.None)
            {
                return;
            }

            LastDetectedDirection = direction;
            OnSwipeDetected?.Invoke(direction);

#if UNITY_EDITOR
            Debug.Log($"[SwipeInputDetector] Swipe detected: {direction}");
#endif
        }

        public SwipeDirection TryResolveSwipe(Vector2 startPosition, Vector2 endPosition)
        {
            Vector2 delta = endPosition - startPosition;
            if (delta.magnitude < _minimumSwipeDistance)
            {
                return SwipeDirection.None;
            }

            // Dominant axis decides cardinal direction.
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                return delta.x > 0f ? SwipeDirection.Right : SwipeDirection.Left;
            }

            return delta.y > 0f ? SwipeDirection.Up : SwipeDirection.Down;
        }
    }
}
