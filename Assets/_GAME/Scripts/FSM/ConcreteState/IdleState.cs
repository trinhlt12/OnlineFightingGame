namespace _GAME.Scripts.FSM.ConcreteState
{
    using _GAME.Scripts.Core;
    using _GAME.Scripts.FSM;
    using UnityEngine;

    /// <summary>
    /// Idle state - handles character when not moving
    /// Single responsibility: manage idle behavior
    /// </summary>
    public class IdleState : NetworkedBaseState<PlayerController>
    {
        public IdleState(PlayerController controller) : base(controller)
        {
        }

        public override void EnterState()
        {
            base.EnterState();

            // Stop any movement
            entity.StopMovement();

            // Play idle animation
            entity.PlayAnimation("Idle");
        }

        public override void StateUpdate()
        {
            // Visual updates if needed
            // State transitions are handled by the state machine
        }

        public override void StateFixedUpdate()
        {
            // Physics updates if needed
            // The transition check is handled by state machine using predicates
        }

        public override void ExitState()
        {
            base.ExitState();
            // Clean up when leaving idle state
        }
    }
}