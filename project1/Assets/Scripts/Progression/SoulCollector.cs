using System;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Progression
{
    [DisallowMultipleComponent]
    public class SoulCollector : MonoBehaviour
    {
        [SerializeField]
        private PlayerLevelSystem _playerLevelSystem;

        [SerializeField]
        private PlayerStatSystem _playerStatSystem;

        [SerializeField, Min(0.1f)]
        [Tooltip("StatType.MagnetRadius가 정의되지 않았을 때 사용하는 자력 반경 폴백값.")]
        private float _attractionRadius = 2.5f;

        [SerializeField, Min(0.05f)]
        private float _collectRadius = 0.45f;

        public static SoulCollector ActiveCollector { get; private set; }

        /// <summary>자력 반경. StatType.MagnetRadius가 있으면 그 값을, 없으면 직렬화 폴백값을 사용한다(#40).</summary>
        public float AttractionRadius => Mathf.Max(0.1f, PlayerStatSystem.ResolveValueOrDefault(_playerStatSystem, StatType.MagnetRadius, _attractionRadius));
        public float CollectRadius => Mathf.Max(0.05f, _collectRadius);

        public event Action<int> OnSoulCollected;

        private void Awake()
        {
            if (_playerLevelSystem == null)
            {
                _playerLevelSystem = GetComponent<PlayerLevelSystem>();
            }

            if (_playerStatSystem == null)
            {
                _playerStatSystem = GetComponent<PlayerStatSystem>();
            }
        }

        private void OnEnable()
        {
            ActiveCollector = this;
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveCollector, this))
            {
                ActiveCollector = null;
            }
        }

        public void Collect(int experienceAmount)
        {
            int baseAmount = Mathf.Max(0, experienceAmount);
            if (baseAmount <= 0)
            {
                return;
            }

            // 혼불 획득 배율 × 경험치 획득 배율을 적용한다(#40). 두 스탯이 없으면 1배.
            float multiplier = PlayerStatSystem.ResolveValueOrDefault(_playerStatSystem, StatType.HonbulAcquireMultiplier, 1f)
                * PlayerStatSystem.ResolveValueOrDefault(_playerStatSystem, StatType.ExperienceGain, 1f);
            int amount = Mathf.Max(1, Mathf.RoundToInt(baseAmount * multiplier));

            _playerLevelSystem?.AddExperience(amount);
            OnSoulCollected?.Invoke(amount);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, AttractionRadius);

            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, CollectRadius);
        }
    }
}
