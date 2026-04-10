using UnityEngine;

namespace Mukseon.Gameplay.Combat
{
    public class SpawnState : IState
    {
        private readonly EnemyBase _enemy;
        private readonly StateMachine _stateMachine;
        private readonly IState _moveState;
        private float _elapsed;

        public SpawnState(EnemyBase enemy, StateMachine stateMachine, IState moveState)
        {
            _enemy = enemy;
            _stateMachine = stateMachine;
            _moveState = moveState;
        }

        public void Enter()
        {
            _elapsed = 0f;
        }

        public void Execute()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _enemy.SpawnDuration)
            {
                _stateMachine.ChangeState(_moveState);
            }
        }

        public void Exit() { }
    }
}
