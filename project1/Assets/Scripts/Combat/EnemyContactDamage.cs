using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyContactDamage : MonoBehaviour
    {
        [SerializeField, Min(0.1f)]
        private float _contactDamage = 10f;

        [SerializeField]
        private bool _destroyOnContact = true;

        private EnemyHealth _enemyHealth;

        private void Awake()
        {
            _enemyHealth = GetComponent<EnemyHealth>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_enemyHealth != null && !_enemyHealth.IsAlive)
            {
                return;
            }

            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                playerHealth = other.GetComponentInParent<PlayerHealth>();
            }

            if (playerHealth == null || !playerHealth.IsAlive)
            {
                return;
            }

            playerHealth.TakeDamage(_contactDamage, _enemyHealth);

            if (_destroyOnContact && _enemyHealth != null && _enemyHealth.IsAlive)
            {
                _enemyHealth.Kill(countAsKill: false);
            }
        }
    }
}
