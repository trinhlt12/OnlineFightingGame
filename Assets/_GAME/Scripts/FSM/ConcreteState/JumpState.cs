namespace _GAME.Scripts.FSM.ConcreteState
{
    using _GAME.Scripts.Core;
    using _GAME.Scripts.FSM;
    using UnityEngine;

    /// <summary>
    /// Jump state - handles character jumping and air movement
    /// Supports double jump as per GDD specifications
    /// </summary>
    public class JumpState : NetworkedBaseState<PlayerController>
    {
        public JumpState(PlayerController controller) : base(controller, "Jump")
        {
        }

        public override void EnterState()
        {
            base.EnterState();

            // Perform the jump action immediately upon entering the state.
            // This is now safe because the transition to JumpState itself consumes the input.
            if (HasStateAuthority)
            {
                entity.PerformJump();
                var player = entity.GetComponent<PlayerController>();
                player._rigidbody.gravityScale = 1f; // Or your desired jump gravity
            }
        }

        public override void StateFixedUpdate()
        {
            if (!HasStateAuthority) return;

            // Air movement is the only continuous logic needed in JumpState.
            entity.HandleAirMovement(entity.CurrentMoveInput);

            // Double jump is now handled by the FSM transitioning from JumpState back to JumpState.
        }

        public override void ExitState()
        {
            base.ExitState();

            // Only the authority should modify physics properties.
            if (HasStateAuthority)
            {
                // Reset gravity scale when leaving the jump state (e.g., upon landing).
                var player = entity.GetComponent<PlayerController>();
                player._rigidbody.gravityScale = 3f; // Your default gravity scale
            }
        }
    }
}