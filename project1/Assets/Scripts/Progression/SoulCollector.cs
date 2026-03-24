using System;
using UnityEngine;

namespace Mukseon.Gameplay.Progression
{
    [DisallowMultipleComponent]
    public class SoulCollector : MonoBehaviour
    {
        [SerializeField]
        private PlayerLevelSystem _playerLevelSystem;

        [SerializeField, Min(0.1f)]
        private float _attractionRadius = 2.5f;

        [SerializeField, Min(0.05f)]
        private float _collectRadius = 0.45f;

        public static SoulCollector ActiveCollector { get; private set; }

        public float AttractionRadius => Mathf.Max(0.1f, _attractionRadius);
        public float CollectRadius => Mathf.Max(0.05f, _collectRadius);

        public event Action<int> OnSoulCollected;

        private void Awake()
        {
            if (_playerLevelSystem == null)
            {
                _playerLevelSystem = GetComponent<PlayerLevelSystem>();
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
            int amount = Mathf.Max(0, experienceAmount);
            if (amount <= 0)
            {
                return;
            }

            _playerLevelSystem?.AddExperience(amount);
            OnSoulCollected?.Invoke(amount);
        }

        public void AddPickupRadius(float amount)
        {
            _attractionRadius = Mathf.Max(0.1f, _attractionRadius + amount);
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
