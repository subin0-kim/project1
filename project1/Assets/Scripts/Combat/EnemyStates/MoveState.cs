namespace Mukseon.Gameplay.Combat
{
    public class MoveState : IState
    {
        private readonly EnemyBase _enemy;

        public MoveState(EnemyBase enemy)
        {
            _enemy = enemy;
        }

        public void Enter() { }

        public void Execute()
        {
            _enemy.ExecuteUpdateMovement();
        }

        public void Exit() { }
    }
}
