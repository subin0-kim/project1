namespace Mukseon.Gameplay.Combat
{
    public class ActionState : IState
    {
        private readonly EnemyBase _enemy;
        private readonly StateMachine _stateMachine;
        private readonly IState _moveState;

        public ActionState(EnemyBase enemy, StateMachine stateMachine, IState moveState)
        {
            _enemy = enemy;
            _stateMachine = stateMachine;
            _moveState = moveState;
        }

        public void Enter() { }

        public void Execute()
        {
            _enemy.ExecuteOnTriggerAction();
        }

        public void Exit() { }
    }
}
