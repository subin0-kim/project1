namespace Mukseon.Gameplay.Combat
{
    public class StateMachine
    {
        public IState CurrentState { get; private set; }

        public void ChangeState(IState newState)
        {
            if (newState == null || newState == CurrentState)
                return;

            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }

        public void ExecuteCurrentState()
        {
            CurrentState?.Execute();
        }
    }
}
