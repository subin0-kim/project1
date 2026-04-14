using System;
using System.Collections.Generic;
using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    public class EnemyHealth : MonoBehaviour
    {
        private static readonly List<EnemyHealth> _activeEnemies = new List<EnemyHealth>();
        public static event Action<EnemyHealth> AnyEnemyDied;
        public static event Action<EnemyHealth, float> AnyEnemyDamaged;

        [SerializeField, Min(1f)]
        private float _maxHealth = 10f;

        [SerializeField]
        private bool _destroyOnDeath;

        [SerializeField]
        private bool _disableCollidersOnDeath = true;

        [SerializeField]
        private SwipeDirection _swipeDirection = SwipeDirection.None;

        [SerializeField]
        private MonsterData _monsterData;

        [SerializeField, Min(0f)]
        private float _moveSpeed = 1f;

        private Collider2D[] _colliders;
        private EnemyAttackSequence _attackSequence;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsAlive { get; private set; }
        public float MoveSpeed => Mathf.Max(0f, _moveSpeed);
        public static IReadOnlyList<EnemyHealth> ActiveEnemies => _activeEnemies;
        public MonsterData MonsterData => _monsterData;
        public string DisplayName => _monsterData != null ? _monsterData.DisplayName : gameObject.name;
        public bool IsBoss => _monsterData != null && _monsterData.IsBoss;
        public EnemyAttackSequence AttackSequence
        {
            get
            {
                if (_attackSequence == null)
                {
                    _attackSequence = GetComponent<EnemyAttackSequence>();
                }

                return _attackSequence;
            }
        }

        public SwipeDirection SwipeDirection
        {
            get
            {
                if (AttackSequence != null)
                {
                    return _attackSequence.CurrentDirection;
                }

                if (_swipeDirection == SwipeDirection.None)
                {
                    _swipeDirection = InferSwipeDirectionFromName(gameObject.name);
                }

                return _swipeDirection;
            }
        }

        public event Action<float, float> OnDamaged;
        public event Action<EnemyHealth, float, object> OnDamagedDetailed;
        public event Action OnDied;
        public event Action<EnemyHealth> OnDeath;

        private void Awake()
        {
            _attackSequence = GetComponent<EnemyAttackSequence>();
            ApplyMonsterData();

            if (_attackSequence == null && _swipeDirection == SwipeDirection.None)
            {
                _swipeDirection = InferSwipeDirectionFromName(gameObject.name);
            }

            _colliders = GetComponentsInChildren<Collider2D>(true);
            ResetHealth();
        }

        private void OnEnable()
        {
            if (!_activeEnemies.Contains(this))
            {
                _activeEnemies.Add(this);
            }
        }

        private void OnDisable()
        {
            _activeEnemies.Remove(this);
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
            float actualDamage = previousHealth - CurrentHealth;
            OnDamaged?.Invoke(CurrentHealth, actualDamage);
            OnDamagedDetailed?.Invoke(this, actualDamage, source);
            AnyEnemyDamaged?.Invoke(this, actualDamage);

            if (CurrentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// Instantly kills this enemy. Use countAsKill=false for self-destruct/contact deaths
        /// that should not grant kill rewards (e.g. Gangshin gauge).
        /// </summary>
        public void Kill(bool countAsKill = true)
        {
            if (!IsAlive)
            {
                return;
            }

            CurrentHealth = 0f;
            Die(countAsKill);
        }

        private void Die(bool countAsKill = true)
        {
            if (!IsAlive)
            {
                return;
            }

            IsAlive = false;

            if (_disableCollidersOnDeath)
            {
                for (int i = 0; i < _colliders.Length; i++)
                {
                    _colliders[i].enabled = false;
                }
            }

            OnDied?.Invoke();
            OnDeath?.Invoke(this);

            if (countAsKill)
            {
                AnyEnemyDied?.Invoke(this);
            }

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

        public void SetMoveSpeed(float moveSpeed)
        {
            _moveSpeed = Mathf.Max(0f, moveSpeed);
        }

        /// <summary>
        /// 풀에서 꺼낸 직후 호출. 체력과 콜라이더를 초기 상태로 복원한다.
        /// 풀 관리 대상은 스스로 Destroy하지 않도록 _destroyOnDeath를 false로 강제한다.
        /// </summary>
        public void PrepareForReuse()
        {
            _destroyOnDeath = false;
            ResetHealth();
            _attackSequence?.ResetSequence();

            if (_disableCollidersOnDeath)
            {
                for (int i = 0; i < _colliders.Length; i++)
                {
                    _colliders[i].enabled = true;
                }
            }
        }

        public void ApplyMonsterData(MonsterData monsterData = null)
        {
            if (monsterData != null)
            {
                _monsterData = monsterData;
            }

            if (_monsterData == null)
            {
                return;
            }

            if (!_monsterData.IsValid(out string reason))
            {
                Debug.LogWarning($"[EnemyHealth] MonsterData '{_monsterData.name}' is invalid. {reason}");
                return;
            }

            _maxHealth = _monsterData.MaxHealth;
            _moveSpeed = _monsterData.MoveSpeed;

            if (_attackSequence != null)
            {
                if (_monsterData.RandomizeSequence)
                {
                    _attackSequence.SetSequence(EnemyAttackSequence.GenerateRandomSequence((int)_maxHealth));
                }
                else if (_monsterData.SwipeDirectionSequence != null && _monsterData.SwipeDirectionSequence.Length > 0)
                {
                    _attackSequence.SetSequence(_monsterData.SwipeDirectionSequence);
                }
            }
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
