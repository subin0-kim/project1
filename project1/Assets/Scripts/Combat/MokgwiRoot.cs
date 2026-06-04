using Mukseon.Core.Input;
using Mukseon.Core.Pool;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    /// <summary>
    /// 목귀가 소환하는 나무뿌리 장애물.
    /// 활성화 시 방향 속성을 랜덤 할당받아 해당 방향 스와이프의 타격 후보가 된다.
    /// 체력 소진 시 제거되며 전역 뿌리 카운터를 감소시킨다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public class MokgwiRoot : MonoBehaviour
    {
        // SwipeDirection.None 제외 — None이 할당되면 스와이프로 파괴 불가
        private static readonly SwipeDirection[] _swipeDirections = System.Array.FindAll(
            (SwipeDirection[])System.Enum.GetValues(typeof(SwipeDirection)),
            d => d != SwipeDirection.None);

        /// <summary>현재 씬에 활성화된 나무뿌리 총 개수.</summary>
        public static int ActiveRootCount { get; private set; }

        /// <summary>비활성화(풀 반환 포함) 직전 발행. 구독자는 이벤트 내에서 해제해야 한다.</summary>
        public event System.Action<MokgwiRoot> OnDisabled;

        private EnemyHealth _enemyHealth;

        private void Awake()
        {
            _enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            _enemyHealth.OnDeath += HandleDeath;
            _enemyHealth.SetSwipeDirection(_swipeDirections[Random.Range(0, _swipeDirections.Length)]);
            ActiveRootCount++;
        }

        private void OnDisable()
        {
            _enemyHealth.OnDeath -= HandleDeath;
            ActiveRootCount--;
            OnDisabled?.Invoke(this);
            OnDisabled = null; // 풀 반환 후 재사용 시 이전 구독 잔존 방지
        }

        private void HandleDeath(EnemyHealth _)
        {
            if (PoolManager.Instance != null)
                PoolManager.Instance.Release(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
