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
            var player = entity.GetComponent<PlayerController>();
            player._rigidbody.gravityScale = 1f;

            // Perform the jump when entering state
            if (HasStateAuthority)
            {
                entity.PerformJump();
            }
        }

        public override void StateUpdate()
        {
            // Visual updates for jump state
            // Animation handling is done by base class
        }

        public override void StateFixedUpdate()
        {
            if (!HasStateAuthority) return;

            // Allow air movement while jumping
            entity.HandleAirMovement(entity.CurrentMoveInput);

            // Check for additional jump input (double jump)
            entity.CheckForAdditionalJump();
        }

        public override void ExitState()
        {
            base.ExitState();
            // Clean up when leaving jump state
            var player = entity.GetComponent<PlayerController>();
            player._rigidbody.gravityScale = 3f;
        }

        /// <summary>
        /// Override for custom jump animation logic if needed
        /// </summary>
        protected override void PlayStateAnimation()
        {
            // Default behavior: play "Jump" animation
            base.PlayStateAnimation();

            // Optional: Add jump-specific animation logic
            // Example: Different animations for first jump vs double jump
        }
    }
}