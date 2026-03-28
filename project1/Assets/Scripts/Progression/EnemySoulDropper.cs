using Mukseon.Gameplay.Combat;
using UnityEngine;

namespace Mukseon.Gameplay.Progression
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemySoulDropper : MonoBehaviour
    {
        [SerializeField]
        private SoulOrb _soulOrbPrefab;

        [SerializeField, Min(1)]
        private int _dropCount = 1;

        [SerializeField, Min(1)]
        private int _experiencePerOrb = 1;

        [SerializeField, Min(0f)]
        private float _dropRadius = 0.3f;

        private EnemyHealth _enemyHealth;

        private void Awake()
        {
            _enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            if (_enemyHealth != null)
            {
                _enemyHealth.OnDeath += HandleEnemyDeath;
            }
        }

        private void OnDisable()
        {
            if (_enemyHealth != null)
            {
                _enemyHealth.OnDeath -= HandleEnemyDeath;
            }
        }

        private void HandleEnemyDeath(EnemyHealth enemy)
        {
            if (_soulOrbPrefab == null)
            {
                return;
            }

            int count = Mathf.Max(1, _dropCount);
            for (int i = 0; i < count; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * _dropRadius;
                Vector3 spawnPosition = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);

                SoulOrb orb = Instantiate(_soulOrbPrefab, spawnPosition, Quaternion.identity);
                orb.SetExperienceAmount(Mathf.Max(1, _experiencePerOrb));
            }
        }
    }
}
