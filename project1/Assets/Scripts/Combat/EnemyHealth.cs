using System;
using Mukseon.Core.Input;
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

        [SerializeField]
        private SwipeDirection _swipeDirection = SwipeDirection.None;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsAlive { get; private set; }
        public SwipeDirection SwipeDirection
        {
            get
            {
                if (_swipeDirection == SwipeDirection.None)
                {
                    _swipeDirection = InferSwipeDirectionFromName(gameObject.name);
                }

                return _swipeDirection;
            }
        }

        public event Action<float, float> OnDamaged;
        public event Action OnDied;

        private void Awake()
        {
            if (_swipeDirection == SwipeDirection.None)
            {
                _swipeDirection = InferSwipeDirectionFromName(gameObject.name);
            }

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

        public void SetSwipeDirection(SwipeDirection swipeDirection)
        {
            _swipeDirection = swipeDirection;
        }

        private static SwipeDirection InferSwipeDirectionFromName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return SwipeDirection.None;
            }

            string normalizedName = objectName.ToLowerInvariant();
            if (normalizedName.Contains("up"))
            {
                return SwipeDirection.Up;
            }

            if (normalizedName.Contains("down"))
            {
                return SwipeDirection.Down;
            }

            if (normalizedName.Contains("left"))
            {
                return SwipeDirection.Left;
            }

            if (normalizedName.Contains("right"))
            {
                return SwipeDirection.Right;
            }

            return SwipeDirection.None;
        }
    }
}
