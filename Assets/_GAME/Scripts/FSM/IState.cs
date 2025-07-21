namespace _GAME.Scripts.FSM
{
    public interface IState
    {
        public void EnterState();
        public void StateUpdate();
        public void StateFixedUpdate();
        public void ExitState();
    }
}