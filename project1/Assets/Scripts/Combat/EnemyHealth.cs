using System;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)]
        private float _maxHealth = 10f;

        [SerializeField]
        private bool _destroyOnDeath;

        [SerializeField]
        private bool _disableCollidersOnDeath = true;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsAlive { get; private set; }

        public event Action<float, float> OnDamaged;
        public event Action OnDied;

        private void Awake()
        {
            ResetHealth();
        }

        public void ResetHealth()
        {
            CurrentHealth = Mathf.Max(1f, _maxHealth);
            IsAlive = true;
        }

        public void ApplyDamage(float amount, object source = null)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            float previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            OnDamaged?.Invoke(CurrentHealth, previousHealth - CurrentHealth);

            if (CurrentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            if (!IsAlive)
            {
                return;
            }

            IsAlive = false;

            if (_disableCollidersOnDeath)
            {
                Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    colliders[i].enabled = false;
                }
            }

            OnDied?.Invoke();

            if (_destroyOnDeath)
            {
                Destroy(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }
    }
}
