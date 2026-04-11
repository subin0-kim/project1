using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public class EnemyContactDamage : MonoBehaviour
    {
        [SerializeField, Min(0.1f)]
        private float _contactDamage = 10f;

        [SerializeField]
        private bool _destroyOnContact = true;

        private EnemyBase _enemy;

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_enemy != null && !_enemy.IsAlive)
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

            playerHealth.TakeDamage(_contactDamage, _enemy);

            if (_destroyOnContact && _enemy != null && _enemy.IsAlive)
            {
                _enemy.Kill(countAsKill: false);
            }
        }
    }
}
