using System;
using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 방향 속성 동적 변환(#68, `direction_dynamic_system.md`).
    /// 같은 적을 동일 방향으로 반복 타격할수록 방향이 변환될 확률이 누적되고,
    /// 확률 발동 시 현재 방향을 제외한 3방향 중 하나로 즉시 전환된다(카운트 리셋).
    /// 색상 전환은 <see cref="EnemyHealth.OnDirectionChanged"/>를 통해 색상 시스템(#82)이 자동 반영한다.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    [DisallowMultipleComponent]
    public class EnemyDirectionConverter : MonoBehaviour
    {
        private static readonly SwipeDirection[] Directions =
        {
            SwipeDirection.Up,
            SwipeDirection.Down,
            SwipeDirection.Left,
            SwipeDirection.Right
        };

        [SerializeField]
        private EnemyHealth _enemyHealth;

        [SerializeField]
        private DirectionConversionConfig _config;

        private int _hitCount;
        // 센티넬은 -1: _swipeId는 0에서 시작해 증가하므로(SwipeAttackEventListener) 절대 충돌하지 않는다.
        // int.MinValue를 쓰면 _swipeId가 오버플로우해 int.MinValue로 반전될 때 첫 유효 타격이 잘못 차단될 수 있다.
        private int _lastSwipeId = -1;

        /// <summary>누적 타격 카운트가 변할 때 발생. (현재 카운트, 변환 임박 강도 0~1). 흔들림/깜빡임 연출용.</summary>
        public event Action<int, float> OnHitCountChanged;

        /// <summary>방향 변환이 발동된 순간 발생. (이전 방향, 새 방향). 먹물 번짐 등 변환 연출 트리거용.</summary>
        public event Action<SwipeDirection, SwipeDirection> OnConverted;

        /// <summary>현재 누적 타격 카운트.</summary>
        public int HitCount => _hitCount;

        private void Awake()
        {
            if (_enemyHealth == null)
            {
                _enemyHealth = GetComponent<EnemyHealth>();
            }
        }

        private void OnEnable()
        {
            // 오브젝트 풀에서 재사용될 때마다 카운트를 초기화한다(direction_dynamic_system.md §엣지 케이스).
            ResetCount();
        }

        /// <summary>
        /// 스와이프 타격 1회를 등록한다. 타겟팅(<see cref="SwipeAttackTargeting"/>)이 이미 방향이 일치하는
        /// 적만 선택하므로, 이 호출 자체가 "동일 방향 타격"을 의미한다.
        /// 동일 스와이프(<paramref name="swipeId"/>)로 같은 적이 여러 번(n-hit) 들어와도 카운트는 1회만 증가한다.
        /// </summary>
        public void RegisterDirectionalHit(int swipeId)
        {
            // 시퀀스 적(보스 패턴, #84)은 방향이 시퀀스에서 결정되므로 동적 변환 대상에서 제외한다.
            if (_enemyHealth == null || !_enemyHealth.IsAlive || _enemyHealth.UsesAttackSequence)
            {
                return;
            }

            // n-hit 가드: 같은 스와이프의 중복 타격은 1회만 카운트한다.
            if (swipeId == _lastSwipeId)
            {
                return;
            }

            _lastSwipeId = swipeId;
            _hitCount++;

            if (UnityEngine.Random.value < ConversionChance(_hitCount))
            {
                Convert();
                return;
            }

            OnHitCountChanged?.Invoke(_hitCount, ImminenceIntensity());
        }

        private void Convert()
        {
            SwipeDirection from = _enemyHealth.SwipeDirection;
            SwipeDirection to = PickDifferentDirection(from);

            // 즉시 적용: SetSwipeDirection이 OnDirectionChanged를 발행 → 색상 시스템(#82)이 새 색으로 전환.
            _enemyHealth.SetSwipeDirection(to);
            ResetCount();
            OnConverted?.Invoke(from, to);
        }

        private void ResetCount()
        {
            _hitCount = 0;
            _lastSwipeId = -1;
            OnHitCountChanged?.Invoke(0, 0f);
        }

        private float ConversionChance(int hitCount)
        {
            return _config != null
                ? _config.GetConversionChance(hitCount)
                : DirectionConversionConfig.DefaultConversionChance(hitCount);
        }

        /// <summary>임박 임계값 이상에서만 0이 아닌 강도를 반환한다. 다음 변환 확률을 강도로 사용해 카운트가 높을수록 강해진다.</summary>
        private float ImminenceIntensity()
        {
            int threshold = _config != null ? _config.ImminentThresholdCount : DirectionConversionConfig.DefaultImminentThresholdCount;
            return _hitCount >= threshold ? ConversionChance(_hitCount) : 0f;
        }

        /// <summary>현재 방향을 제외한 나머지 3방향 중 하나를 균등 확률로 고른다.</summary>
        private static SwipeDirection PickDifferentDirection(SwipeDirection current)
        {
            int currentIndex = Array.IndexOf(Directions, current);
            if (currentIndex < 0)
            {
                // 현재 방향이 None 등으로 미정이면 4방향 전체에서 고른다.
                return Directions[UnityEngine.Random.Range(0, Directions.Length)];
            }

            // offset 1~3을 더해 현재 인덱스를 반드시 벗어난 방향을 균등하게 선택한다.
            int offset = UnityEngine.Random.Range(1, Directions.Length);
            return Directions[(currentIndex + offset) % Directions.Length];
        }
    }
}
