using Mukseon.Core.Pool;
using UnityEngine;

namespace Mukseon.Gameplay.Progression
{
    [DisallowMultipleComponent]
    public class SoulOrb : MonoBehaviour
    {
        [SerializeField, Min(1)]
        private int _experienceAmount = 1;

        [SerializeField, Min(0f)]
        private float _scatterSpeed = 1.2f;

        [SerializeField, Min(0.1f)]
        private float _attractSpeed = 6f;

        [SerializeField, Min(0.1f)]
        private float _attractAcceleration = 12f;

        private float _initialAttractSpeed;
        private Vector3 _scatterVelocity;
        private bool _isAttracting;

        private void Awake()
        {
            _initialAttractSpeed = _attractSpeed;
        }

        private void OnEnable()
        {
            float randomX = Random.Range(-1f, 1f);
            float randomY = Random.Range(-1f, 1f);
            Vector3 direction = new Vector3(randomX, randomY, 0f).normalized;
            _scatterVelocity = direction * _scatterSpeed;
            _attractSpeed = _initialAttractSpeed;
            _isAttracting = false;
        }

        private void Update()
        {
            SoulCollector collector = SoulCollector.ActiveCollector;
            if (collector == null)
            {
                return;
            }

            Vector3 targetPosition = collector.transform.position;
            targetPosition.z = transform.position.z;
            Vector3 toTarget = targetPosition - transform.position;
            float distance = toTarget.magnitude;

            if (!_isAttracting)
            {
                if (distance <= collector.AttractionRadius)
                {
                    _isAttracting = true;
                }
                else
                {
                    transform.position += _scatterVelocity * Time.deltaTime;
                    _scatterVelocity = Vector3.Lerp(_scatterVelocity, Vector3.zero, Time.deltaTime * 4f);
                    return;
                }
            }

            if (distance <= collector.CollectRadius)
            {
                collector.Collect(_experienceAmount);

                if (PoolManager.Instance != null)
                {
                    PoolManager.Instance.Release(gameObject);
                }
                else
                {
                    Destroy(gameObject);
                }

                return;
            }

            float currentSpeed = Mathf.Max(0.1f, _attractSpeed);
            _attractSpeed = currentSpeed + _attractAcceleration * Time.deltaTime;
            Vector3 directionToTarget = distance > 0.001f ? toTarget / distance : Vector3.zero;
            transform.position += directionToTarget * _attractSpeed * Time.deltaTime;
        }

        public void SetExperienceAmount(int experienceAmount)
        {
            _experienceAmount = Mathf.Max(1, experienceAmount);
        }
    }
}
