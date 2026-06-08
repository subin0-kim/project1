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

        private static readonly SwipeDirection[] DirectionPool =
        {
            SwipeDirection.Up,
            SwipeDirection.Down,
            SwipeDirection.Left,
            SwipeDirection.Right
        };

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
        private bool _attackSequenceCached;
        private EnemyDirectionConverter _directionConverter;
        private bool _directionConverterCached;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsAlive { get; private set; }
        public bool IsTargetable { get; set; } = true;
        public float MoveSpeed => Mathf.Max(0f, _moveSpeed);
        public static IReadOnlyList<EnemyHealth> ActiveEnemies => _activeEnemies;
        public MonsterData MonsterData => _monsterData;
        public string DisplayName => _monsterData != null ? _monsterData.DisplayName : gameObject.name;
        public bool IsBoss => _monsterData != null && _monsterData.IsBoss;
        public EnemyAttackSequence AttackSequence
        {
            get
            {
                if (!_attackSequenceCached)
                {
                    _attackSequence = GetComponent<EnemyAttackSequence>();
                    _attackSequenceCached = true;
                }

                return _attackSequence;
            }
        }

        /// <summary>방향 동적 변환(#68) 컴포넌트. 없으면 null(변환 미적용 적).</summary>
        public EnemyDirectionConverter DirectionConverter
        {
            get
            {
                if (!_directionConverterCached)
                {
                    _directionConverter = GetComponent<EnemyDirectionConverter>();
                    _directionConverterCached = true;
                }

                return _directionConverter;
            }
        }

        /// <summary>
        /// 방향 시퀀스 시스템은 보스 전용이다(#84). 비어 있지 않은 시퀀스를 가진 적만
        /// 시퀀스 기반(타격당 데미지 1 + 인덱스 전진)으로 동작하며, 그 외 적은 단일 방향 속성을 사용한다.
        /// </summary>
        public bool UsesAttackSequence => AttackSequence != null && _attackSequence.SequenceLength > 0;

        public SwipeDirection SwipeDirection
        {
            get
            {
                if (UsesAttackSequence)
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

        /// <summary>현재 방향 속성이 (재)배정될 때 발생(#82). 색상 시각화 갱신에 사용된다.</summary>
        public event Action<SwipeDirection> OnDirectionChanged;

        private void Awake()
        {
            _attackSequence = GetComponent<EnemyAttackSequence>();
            _attackSequenceCached = true;
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
            if (!IsAlive || !IsTargetable || amount <= 0f)
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
            OnDirectionChanged?.Invoke(swipeDirection);
        }

        public void SetMoveSpeed(float moveSpeed)
        {
            _moveSpeed = Mathf.Max(0f, moveSpeed);
        }

        /// <summary>
        /// 최대 체력을 직접 설정한다. ApplyMonsterData 이후 미니 보스 배율 적용 시 사용.
        /// PrepareForReuse() 호출 전에 설정해야 변경된 값으로 체력이 초기화된다.
        /// </summary>
        public void SetMaxHealth(float maxHealth)
        {
            _maxHealth = Mathf.Max(1f, maxHealth);
        }

        /// <summary>
        /// 풀에서 꺼낸 직후 호출. 체력과 콜라이더를 초기 상태로 복원한다.
        /// 풀 관리 대상은 스스로 Destroy하지 않도록 _destroyOnDeath를 false로 강제한다.
        /// </summary>
        public void PrepareForReuse()
        {
            _destroyOnDeath = false;
            IsTargetable = true;
            ResetHealth();

            ConfigureDirectionForSpawn();

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

            ConfigureDirectionForSpawn();
        }

        /// <summary>
        /// 스폰 시 방향 상태를 구성한다(#84).
        /// 보스: 방향 시퀀스를 구성한다(랜덤 또는 명시 시퀀스).
        /// 그 외 적: 시퀀스를 비우고 단일 방향 속성을 랜덤 배정한다(`combat_system.md` §1).
        /// </summary>
        private void ConfigureDirectionForSpawn()
        {
            bool isBoss = _monsterData != null && _monsterData.IsBoss;

            if (isBoss && _attackSequence != null)
            {
                if (_monsterData.RandomizeSequence)
                {
                    _attackSequence.SetSequence(EnemyAttackSequence.GenerateRandomSequence((int)_maxHealth));
                }
                else if (_monsterData.SwipeDirectionSequence != null && _monsterData.SwipeDirectionSequence.Length > 0)
                {
                    _attackSequence.SetSequence(_monsterData.SwipeDirectionSequence);
                }
                else
                {
                    _attackSequence.SetSequence(Array.Empty<SwipeDirection>());
                }

                return;
            }

            // 비보스: 시퀀스 미사용. 단일 방향 속성을 랜덤 배정한다.
            // (MokgwiRoot / MaeguProjectile 등은 이후 SetSwipeDirection으로 자체 방향을 덮어쓴다.)
            _attackSequence?.SetSequence(Array.Empty<SwipeDirection>());

            if (_monsterData != null)
            {
                // 방향 변경의 단일 진입점을 유지한다: _swipeDirection 직접 할당 + 이벤트 수동 발행 대신
                // SetSwipeDirection을 경유해, 이후 메서드에 로직이 추가돼도 이 경로가 누락되지 않게 한다.
                SetSwipeDirection(DirectionPool[UnityEngine.Random.Range(0, DirectionPool.Length)]);
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
