using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Gameplay.VFX
{
    /// <summary>
    /// 스와이프 입력 중 TrailRenderer로 먹물 붓터치 궤적을 그립니다.
    /// SwipeInputDetector의 OnSwipeBegan/Moved/Ended 이벤트를 구독하여
    /// 터치 위치를 월드 좌표로 변환하고 이 오브젝트를 이동시킵니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TrailRenderer))]
    public class SwipeTrailEffect : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private SwipeInputDetector _swipeInputDetector;

        [SerializeField]
        private Camera _camera;

        [Header("Trail Settings")]
        [SerializeField, Tooltip("화면에서 트레일이 표시될 월드 Z값")]
        private float _trailWorldZ = 0f;

        [SerializeField, Min(0.01f)]
        private float _trailWidth = 0.3f;

        [SerializeField]
        private float _trailTime = 0.25f;

        [SerializeField]
        private Color _trailColor = new Color(0.05f, 0.02f, 0.02f, 0.85f);

        private TrailRenderer _trail;

        private void Awake()
        {
            _trail = GetComponent<TrailRenderer>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            ConfigureTrail();
        }

        private void OnEnable()
        {
            if (_swipeInputDetector != null)
            {
                _swipeInputDetector.OnSwipeBegan += HandleSwipeBegan;
                _swipeInputDetector.OnSwipeMoved += HandleSwipeMoved;
                _swipeInputDetector.OnSwipeEnded += HandleSwipeEnded;
            }
        }

        private void OnDisable()
        {
            if (_swipeInputDetector != null)
            {
                _swipeInputDetector.OnSwipeBegan -= HandleSwipeBegan;
                _swipeInputDetector.OnSwipeMoved -= HandleSwipeMoved;
                _swipeInputDetector.OnSwipeEnded -= HandleSwipeEnded;
            }
        }

        private void HandleSwipeBegan(Vector2 screenPos)
        {
            transform.position = ScreenToWorld(screenPos);
            _trail.Clear();
            _trail.emitting = true;
        }

        private void HandleSwipeMoved(Vector2 screenPos)
        {
            transform.position = ScreenToWorld(screenPos);
        }

        private void HandleSwipeEnded(Vector2 screenPos)
        {
            transform.position = ScreenToWorld(screenPos);
            _trail.emitting = false;
        }

        private Vector3 ScreenToWorld(Vector2 screenPos)
        {
            if (_camera == null)
            {
                Debug.LogWarning("[SwipeTrailEffect] Camera가 없어 ScreenToWorld 변환에 실패했습니다.");
                return Vector3.zero;
            }

            float depth = _trailWorldZ - _camera.transform.position.z;
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
            world.z = _trailWorldZ;
            return world;
        }

        private void ConfigureTrail()
        {
            if (_trail == null)
            {
                return;
            }

            _trail.time = _trailTime;
            _trail.startWidth = _trailWidth;
            _trail.endWidth = 0f;
            _trail.startColor = _trailColor;
            _trail.endColor = new Color(_trailColor.r, _trailColor.g, _trailColor.b, 0f);
            _trail.numCornerVertices = 6;
            _trail.numCapVertices = 4;
            _trail.emitting = false;
        }
    }
}
