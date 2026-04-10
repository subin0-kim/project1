namespace Mukseon.Gameplay.Combat
{
    public interface IState
    {
        void Enter();
        void Execute();
        void Exit();
    }
}
