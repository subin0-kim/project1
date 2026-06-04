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
        private static readonly SwipeDirection[] _swipeDirections =
            (SwipeDirection[])System.Enum.GetValues(typeof(SwipeDirection));

        /// <summary>현재 씬에 활성화된 나무뿌리 총 개수.</summary>
        public static int ActiveRootCount { get; private set; }

        private EnemyHealth _enemyHealth;
        private bool _counted;

        private void Awake()
        {
            _enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            _enemyHealth.OnDeath += HandleDeath;
            _enemyHealth.SetSwipeDirection(_swipeDirections[Random.Range(0, _swipeDirections.Length)]);
            ActiveRootCount++;
            _counted = true;
        }

        private void OnDisable()
        {
            _enemyHealth.OnDeath -= HandleDeath;
        }

        private void OnDestroy()
        {
            // Kill 없이 파괴되는 경우(씬 언로드 등) 카운터 보정
            if (_counted)
            {
                ActiveRootCount--;
                _counted = false;
            }
        }

        private void HandleDeath(EnemyHealth _)
        {
            if (_counted)
            {
                ActiveRootCount--;
                _counted = false;
            }

            if (PoolManager.Instance != null)
                PoolManager.Instance.Release(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
