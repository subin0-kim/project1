namespace Mukseon.Gameplay.Combat
{
    public class DeadState : IState
    {
        private readonly EnemyBase _enemy;

        public DeadState(EnemyBase enemy)
        {
            _enemy = enemy;
        }

        public void Enter() { }

        public void Execute() { }

        public void Exit() { }
    }
}
