namespace _GAME.Scripts.FSM
{
    using System;

    public class FuncPredicate : IPredicate
    {
        private readonly Func<bool> func;

        public FuncPredicate(Func<bool> func)
        {
            this.func = func;

        }
        public           bool       Evaluate() => this.func.Invoke();
    }
}