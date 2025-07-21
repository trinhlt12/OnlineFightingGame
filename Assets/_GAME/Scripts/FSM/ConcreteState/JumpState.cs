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

            // First jump when entering state
            if (HasStateAuthority)
            {
                entity.PerformJump(); // ← This will consume the input
                var player = entity.GetComponent<PlayerController>();
                player._rigidbody.gravityScale = 1f;
            }
        }

        public override void StateFixedUpdate()
        {
            if (!HasStateAuthority) return;

            // Air movement
            entity.HandleAirMovement(entity.CurrentMoveInput);

            // Double jump check - input will be consumed if used
            if (entity.WasJumpPressedThisFrame && entity.CanJump)
            {
                entity.PerformJump(); // ← This will consume the input
            }
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