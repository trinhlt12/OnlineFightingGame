namespace _GAME.Scripts.FSM.ConcreteState
{
    using _GAME.Scripts.Core;
    using _GAME.Scripts.FSM;
    using UnityEngine;

    /// <summary>
    /// Move state - handles character movement
    /// Single responsibility: manage movement behavior
    /// Now with automatic "Move" animation playback
    /// </summary>
    public class MoveState : NetworkedBaseState<PlayerController>
    {
        public MoveState(PlayerController controller) : base(controller, "Move")
        {
        }

        public override void EnterState()
        {
            // Base class automatically plays "Move" animation
            base.EnterState();

            // Optional: Add move-specific setup here
            // Example: Enable movement particles, sound effects, etc.
        }

        public override void StateUpdate()
        {
            // Visual updates (animation speed based on movement)
            UpdateMovementAnimation();
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

            // Reset animation speed when leaving move state
            ResetMovementAnimation();
        }

        /// <summary>
        /// Updates movement animation based on input speed
        /// </summary>
        private void UpdateMovementAnimation()
        {
            if (entity.Animator != null)
            {
                // Set animation speed based on movement input
                float moveSpeed = Mathf.Abs(entity.CurrentMoveInput);
                entity.Animator.SetFloat("MoveSpeed", moveSpeed);
            }
        }

        /// <summary>
        /// Resets movement animation parameters
        /// </summary>
        private void ResetMovementAnimation()
        {
            if (entity.Animator != null)
            {
                entity.Animator.SetFloat("MoveSpeed", 0f);
            }
        }

        /// <summary>
        /// Override for custom movement animation logic if needed
        /// </summary>
        protected override void PlayStateAnimation()
        {
            // Default behavior: play "Move" animation
            base.PlayStateAnimation();

            // Optional: Add move-specific animation variations
            // Example: Different animations based on movement direction, speed, etc.

            // Initialize movement animation parameters
            UpdateMovementAnimation();
        }
    }
}