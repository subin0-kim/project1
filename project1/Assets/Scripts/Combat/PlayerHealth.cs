using System;
using Mukseon.Gameplay.Stats;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStatSystem))]
    public class PlayerHealth : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerStatSystem _playerStatSystem;

        [Header("Settings")]
        [SerializeField, Min(1f)]
        private float _fallbackMaxHealth = 100f;

        private const float AbsoluteMinHealth = 1f;

        [Header("Debug")]
        [SerializeField]
        private bool _showDebugLogs;

        private float _currentHealth;
        private float _resolvedMaxHealth;
        private bool _isDead;
        private bool _isInvincible;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _resolvedMaxHealth;
        public float HealthNormalized => _resolvedMaxHealth > 0f ? Mathf.Clamp01(_currentHealth / _resolvedMaxHealth) : 0f;
        public bool IsAlive => !_isDead;
        public bool IsInvincible => _isInvincible;

        public event Action<float, float> OnHealthChanged;
        public event Action<float> OnDamaged;
        public event Action<float> OnHealed;
        public event Action OnDied;

        private void Awake()
        {
            if (_playerStatSystem == null)
            {
                _playerStatSystem = GetComponent<PlayerStatSystem>();
            }
        }

        private bool _initialized;

        private void Start()
        {
            if (!_initialized)
            {
                ResolveMaxHealth();
                _currentHealth = _resolvedMaxHealth;
                _isDead = false;
                _initialized = true;
            }
        }

        private void OnEnable()
        {
            if (_playerStatSystem != null)
            {
                _playerStatSystem.OnStatChanged += HandleStatChanged;
            }
        }

        private void OnDisable()
        {
            if (_playerStatSystem != null)
            {
                _playerStatSystem.OnStatChanged -= HandleStatChanged;
            }
        }

        public void TakeDamage(float amount, object source = null)
        {
            if (_isDead || amount <= 0f)
            {
                return;
            }

            if (_isInvincible)
            {
#if UNITY_EDITOR
                if (_showDebugLogs)
                {
                    Debug.Log($"[PlayerHealth] Damage blocked (invincible). amount={amount} source={source}");
                }
#endif
                return;
            }

            float previous = _currentHealth;
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);

            float actualDamage = previous - _currentHealth;

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log($"[PlayerHealth] TakeDamage {actualDamage:F1} (source={source}). HP: {previous:F1} → {_currentHealth:F1}");
            }
#endif

            OnDamaged?.Invoke(actualDamage);
            OnHealthChanged?.Invoke(_currentHealth, _resolvedMaxHealth);

            if (_currentHealth <= 0f)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (_isDead || amount <= 0f)
            {
                return;
            }

            float previous = _currentHealth;
            _currentHealth = Mathf.Min(_resolvedMaxHealth, _currentHealth + amount);

            float actualHeal = _currentHealth - previous;
            if (actualHeal > 0f)
            {
                OnHealed?.Invoke(actualHeal);
                OnHealthChanged?.Invoke(_currentHealth, _resolvedMaxHealth);
            }
        }

        public void SetInvincible(bool invincible)
        {
            _isInvincible = invincible;
        }

        public void ResetHealth()
        {
            ResolveMaxHealth();
            _currentHealth = _resolvedMaxHealth;
            _isDead = false;
            OnHealthChanged?.Invoke(_currentHealth, _resolvedMaxHealth);
        }

        private void Die()
        {
            if (_isDead)
            {
                return;
            }

            _isDead = true;

#if UNITY_EDITOR
            if (_showDebugLogs)
            {
                Debug.Log("[PlayerHealth] Player died.");
            }
#endif

            OnDied?.Invoke();
        }

        private void HandleStatChanged(StatType statType, float newValue)
        {
            if (statType != StatType.MaxHealth)
            {
                return;
            }

            float previousMax = _resolvedMaxHealth;
            ResolveMaxHealth();

            if (_resolvedMaxHealth > previousMax)
            {
                float bonus = _resolvedMaxHealth - previousMax;
                _currentHealth = Mathf.Min(_resolvedMaxHealth, _currentHealth + bonus);
            }
            else
            {
                _currentHealth = Mathf.Min(_currentHealth, _resolvedMaxHealth);
            }

            OnHealthChanged?.Invoke(_currentHealth, _resolvedMaxHealth);
        }

        private void ResolveMaxHealth()
        {
            float statValue = _playerStatSystem != null ? _playerStatSystem.GetValue(StatType.MaxHealth) : 0f;
            float fallback = _fallbackMaxHealth > 0f ? _fallbackMaxHealth : 100f;
            _resolvedMaxHealth = statValue > 0f ? statValue : Mathf.Max(AbsoluteMinHealth, fallback);
        }
    }
}
