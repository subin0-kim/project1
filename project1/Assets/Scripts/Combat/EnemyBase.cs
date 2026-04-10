using System;
using System.Collections.Generic;
using Mukseon.Core.Input;
using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class EnemyBase : MonoBehaviour
    {
        private static readonly List<EnemyBase> _activeEnemies = new List<EnemyBase>();
        public static IReadOnlyList<EnemyBase> ActiveEnemies => _activeEnemies;

        [SerializeField]
        protected EnemyData _data;

        [SerializeField, Min(0f)]
        private float _separationRadius = 1f;

        [SerializeField, Min(0f)]
        private float _separationForce = 5f;

        protected float _currentHp;
        protected bool _isDead;

        protected Rigidbody2D _rigidbody;
        protected StateMachine _stateMachine;

        protected SpawnState _spawnState;
        protected MoveState _moveState;
        protected ActionState _actionState;
        protected DeadState _deadState;

        public event Action OnDeath;
        public event Action OnDamaged;

        /// <summary>
        /// 스폰 연출 시간(초). 0이면 즉시 MoveState로 전환.
        /// 하위 클래스에서 override하여 연출 길이를 조정합니다.
        /// </summary>
        public virtual float SpawnDuration => 0f;

        protected virtual void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _stateMachine = new StateMachine();

            _moveState = new MoveState(this);
            _spawnState = new SpawnState(this, _stateMachine, _moveState);
            _actionState = new ActionState(this, _stateMachine, _moveState);
            _deadState = new DeadState(this);
        }

        protected virtual void OnEnable()
        {
            if (!_activeEnemies.Contains(this))
                _activeEnemies.Add(this);

            Initialize();
        }

        protected virtual void OnDisable()
        {
            _activeEnemies.Remove(this);
        }

        public void Initialize()
        {
            if (_data == null)
            {
                Debug.LogWarning($"[EnemyBase] EnemyData가 할당되지 않았습니다: {gameObject.name}");
                _currentHp = 1f;
            }
            else
            {
                _currentHp = _data.MaxHealth;
            }

            _isDead = false;
            _stateMachine.ChangeState(_spawnState);
        }

        /// <summary>
        /// EnemyManager가 매 틱 직접 호출. Update() 사용 금지.
        /// </summary>
        public void ManualUpdate()
        {
            _stateMachine.ExecuteCurrentState();
        }

        public void TakeDamage(float amount)
        {
            if (_isDead || amount <= 0f)
                return;

            _currentHp -= amount;
            OnDamaged?.Invoke();

            if (_currentHp <= 0f)
                Die();
        }

        public void Die()
        {
            if (_isDead)
                return;

            _isDead = true;
            _stateMachine.ChangeState(_deadState);
            OnDeath?.Invoke();
            // TODO: ObjectPool.Return(this)
        }

        public SwipeDirection GetSwipeDirection()
        {
            return _data != null ? _data.SwipeDirection : SwipeDirection.None;
        }

        /// <summary>
        /// 주변 적과의 반발 벡터를 계산합니다. (EnemyManager 또는 UpdateMovement에서 호출)
        /// </summary>
        public void ApplySeparationForce(IReadOnlyList<EnemyBase> neighbors)
        {
            Vector2 sep = ComputeSeparation(neighbors);
            if (sep != Vector2.zero)
                _rigidbody.AddForce(sep.normalized * _separationForce, ForceMode2D.Force);
        }

        /// <summary>
        /// 반발 벡터만 반환합니다. UpdateMovement()에서 이동 벡터와 합산할 때 사용합니다.
        /// </summary>
        protected Vector2 ComputeSeparation(IReadOnlyList<EnemyBase> neighbors)
        {
            Vector2 separation = Vector2.zero;
            for (int i = 0; i < neighbors.Count; i++)
            {
                EnemyBase neighbor = neighbors[i];
                if (neighbor == null || neighbor == this)
                    continue;

                Vector2 diff = (Vector2)(transform.position - neighbor.transform.position);
                float distance = diff.magnitude;
                if (distance > 0f && distance < _separationRadius)
                    separation += diff / distance;
            }

            return separation;
        }

        // State 클래스에서 protected 추상 메서드에 접근하기 위한 internal 브리지
        internal void ExecuteUpdateMovement() => UpdateMovement();
        internal void ExecuteOnTriggerAction() => OnTriggerAction();

        /// <summary>요괴별 이동 로직. MoveState.Execute()에서 호출됩니다.</summary>
        protected abstract void UpdateMovement();

        /// <summary>요괴별 고유 행동. ActionState.Execute()에서 호출됩니다.</summary>
        protected abstract void OnTriggerAction();
    }
}
