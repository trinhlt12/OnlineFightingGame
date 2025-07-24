namespace _GAME.Scripts.FSM.ConcreteState
{
    using _GAME.Scripts.Core;
    using _GAME.Scripts.FSM;
    using UnityEngine;

    /// <summary>
    /// Dash state - handles character dashing movement
    /// Simple implementation following SOLID principles
    /// </summary>
    public class DashState : NetworkedBaseState<PlayerController>
    {
        private          float dashTimer;
        private readonly float dashDuration = 0.2f; // Quick dash duration
        private          bool  wasJumpStateBefore;

        public DashState(PlayerController controller) : base(controller, "")
        {
            // Animation name will be set conditionally in EnterState
        }

        public override void EnterState()
        {
            if (!HasStateAuthority) return;

            // Check if previous state was jump state for animation decision
            wasJumpStateBefore = entity._stateMachine.PreviousState is JumpState;

            // Set animation name based on ground state
            if (entity.IsGrounded && !wasJumpStateBefore)
            {
                SetAnimationName("Dash");
            }
            else
            {
                SetAnimationName(""); // No animation change for air dash
            }

            base.EnterState();

            // Perform dash movement
            entity.PerformDash();
            dashTimer = 0f;
        }

        public override void StateFixedUpdate()
        {
            if (!HasStateAuthority) return;

            dashTimer += Runner.DeltaTime;

            // Simple timer-based exit condition
            if (dashTimer >= dashDuration)
            {
                // Dash complete, state machine will handle transition
            }
        }

        public override void ExitState()
        {
            base.ExitState();
        }

        /// <summary>
        /// Check if dash is complete
        /// </summary>
        public bool IsDashComplete()
        {
            return dashTimer >= dashDuration;
        }
    }
}