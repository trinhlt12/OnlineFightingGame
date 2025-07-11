namespace _GAME.Scripts.FSM.ConcreteState
{
    using _GAME.Scripts.Core;
    using _GAME.Scripts.FSM;
    using UnityEngine;

    /// <summary>
    /// Move state - handles character movement
    /// Single responsibility: manage movement behavior
    /// </summary>
    public class MoveState : NetworkedBaseState<PlayerController>
    {
        public MoveState(PlayerController controller) : base(controller)
        {
        }

        public override void EnterState()
        {
            base.EnterState();

            // Play move animation
            entity.PlayAnimation("Move");
        }

        public override void StateUpdate()
        {
            // Visual updates (animation speed based on movement)
            if (entity.Animator != null)
            {
                entity.Animator.SetFloat("MoveSpeed", Mathf.Abs(entity.CurrentMoveInput));
            }
        }

        public override void StateFixedUpdate()
        {
            if (!HasStateAuthority) return;

            // Call move from controller with current input
            entity.Move(entity.CurrentMoveInput);
        }

        public override void ExitState()
        {
            base.ExitState();
            // Reset animation speed
            if (entity.Animator != null)
            {
                entity.Animator.SetFloat("MoveSpeed", 0f);
            }
        }
    }
}