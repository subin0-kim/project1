using System.Collections.Generic;
using Mukseon.Core.Input;
using Mukseon.Gameplay.Combat;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Progression
{
    /// <summary>
    /// 스와이프 끝점 당기기(#67, `honbul_system.md`). 스와이프 입력 종료(Touch Up) 시
    /// 끝점 좌표 기준 일정 반경 내 혼불을 중앙(플레이어) 방향으로 당긴다.
    /// 당겨진 혼불은 자력 반경에 진입하면 <see cref="SoulOrb"/>가 자동으로 흡수한다.
    /// 당기기 반경/이동 거리는 혼불 획득 스탯으로 강화 예정이나(#40), 현재는 직렬화 설정값을 사용한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SwipeSoulPuller : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerSwipeAttackController _swipeAttackController;

        [SerializeField]
        private PlayerStatSystem _playerStatSystem;

        [SerializeField]
        private Camera _camera;

        [Header("당기기 폴백 설정")]
        [SerializeField, Min(0f)]
        [Tooltip("StatType.SwipeEndpointPullRadius가 없을 때 사용하는 당기기 반경 폴백값.")]
        private float _pullRadius = 2.5f;

        [SerializeField, Min(0f)]
        [Tooltip("StatType.HonbulMoveDistance가 없을 때 사용하는 이동 거리 폴백값.")]
        private float _moveDistance = 3f;

        /// <summary>스와이프 끝점 당기기 반경. StatType.SwipeEndpointPullRadius가 있으면 그 값을 사용한다(#40).</summary>
        public float PullRadius => Mathf.Max(0f, ResolveStat(StatType.SwipeEndpointPullRadius, _pullRadius));

        /// <summary>한 번 당겨질 때 이동 거리. StatType.HonbulMoveDistance가 있으면 그 값을 사용한다(#40).</summary>
        public float MoveDistance => Mathf.Max(0f, ResolveStat(StatType.HonbulMoveDistance, _moveDistance));

        private void Awake()
        {
            if (_swipeAttackController == null)
            {
                _swipeAttackController = GetComponent<PlayerSwipeAttackController>();
            }

            if (_swipeAttackController == null)
            {
                _swipeAttackController = FindSwipeAttackController();
            }

            if (_playerStatSystem == null)
            {
                _playerStatSystem = GetComponent<PlayerStatSystem>();
            }
        }

        /// <summary>스탯값을 조회하되, 스탯이 정의되지 않아 0이면 폴백값을 반환한다.</summary>
        private float ResolveStat(StatType statType, float fallback)
        {
            if (_playerStatSystem == null)
            {
                return fallback;
            }

            float value = _playerStatSystem.GetValue(statType);
            return value > 0f ? value : fallback;
        }

        private void OnEnable()
        {
            if (_swipeAttackController != null)
            {
                _swipeAttackController.OnAttackExecuted += HandleAttackExecuted;
            }
            else
            {
                Debug.LogWarning("[SwipeSoulPuller] PlayerSwipeAttackController 참조가 없어 스와이프 끝점 당기기가 동작하지 않습니다.");
            }
        }

        private void OnDisable()
        {
            if (_swipeAttackController != null)
            {
                _swipeAttackController.OnAttackExecuted -= HandleAttackExecuted;
            }
        }

        private void HandleAttackExecuted(SwipeDirection direction, Vector2 endScreenPosition)
        {
            // 수거자가 없으면 당겨도 흡수 목적지가 없으므로 건너뛴다.
            if (SoulCollector.ActiveCollector == null)
            {
                return;
            }

            Vector2 endWorld = ResolveWorldPoint(endScreenPosition);
            float radiusSquared = PullRadius * PullRadius;
            float moveDistance = MoveDistance;

            // SoulOrb.Pull은 ActiveSouls를 변경하지 않으므로(흡수/소멸은 Update에서 처리) 순회 중 호출이 안전하다.
            IReadOnlyList<SoulOrb> souls = SoulOrb.ActiveSouls;
            for (int i = 0; i < souls.Count; i++)
            {
                SoulOrb soul = souls[i];
                if (soul == null)
                {
                    continue;
                }

                Vector2 soulPosition = soul.transform.position;
                if ((soulPosition - endWorld).sqrMagnitude <= radiusSquared)
                {
                    soul.Pull(moveDistance);
                }
            }
        }

        /// <summary>스와이프 끝점(스크린 좌표)을 월드 좌표로 변환한다. 카메라가 없으면 스크린 좌표를 폴백으로 사용한다.</summary>
        private Vector2 ResolveWorldPoint(Vector2 screenPosition)
        {
            // Camera.main은 Awake 시점에 아직 null일 수 있으므로 사용 시점에 지연 해석한다.
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera != null)
            {
                Vector3 world = _camera.ScreenToWorldPoint(screenPosition);
                return new Vector2(world.x, world.y);
            }

            return screenPosition;
        }

        private static PlayerSwipeAttackController FindSwipeAttackController()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<PlayerSwipeAttackController>();
#else
            return FindObjectOfType<PlayerSwipeAttackController>();
#endif
        }

        private void OnDrawGizmosSelected()
        {
            // 실제 당기기 판정은 런타임 스와이프 끝점(endWorld) 기준이다(HandleAttackExecuted 참고).
            // 끝점은 런타임에만 알 수 있으므로, 여기서는 당기기 반경의 '크기'만 플레이어 위치에 참고용으로 표시한다.
            Gizmos.color = new Color(0.4f, 1f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, PullRadius);
        }
    }
}
