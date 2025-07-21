namespace _GAME.Scripts.FSM
{
    public interface ITransition
    {
        IState     To        { get; }
        IPredicate Condition { get; }
    }
}