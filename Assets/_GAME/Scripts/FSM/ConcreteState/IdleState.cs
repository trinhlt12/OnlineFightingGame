namespace _GAME.Scripts.FSM.ConcreteState
{
    using _GAME.Scripts.Core;
    using _GAME.Scripts.FSM;
    using UnityEngine;

    /// <summary>
    /// Idle state - handles character when not moving
    /// Single responsibility: manage idle behavior
    /// Now with automatic "Idle" animation playback
    /// </summary>
    public class IdleState : NetworkedBaseState<PlayerController>
    {
        public IdleState(PlayerController controller) : base(controller, "Idle")
        {
        }

        public override void EnterState()
        {
            // Base class automatically plays "Idle" animation
            base.EnterState();

            // Stop any movement when entering idle
            entity.StopMovement();
        }

        public override void StateUpdate()
        {
            // Visual updates if needed
            // State transitions are handled by the state machine using predicates

            // Optional: Add idle-specific visual effects here
            // Example: Update animation speed, particle effects, etc.
        }

        public override void StateFixedUpdate()
        {
            // Physics updates if needed
            // The transition check is handled by state machine using predicates

            // Optional: Add idle-specific physics logic here
            // Example: Apply friction, slight movements, etc.
        }

        public override void ExitState()
        {
            base.ExitState();

            // Clean up when leaving idle state
            // Optional: Stop idle-specific effects, reset parameters, etc.
        }

        /// <summary>
        /// Override if you need custom idle animation logic
        /// </summary>
        protected override void PlayStateAnimation()
        {
            // Default behavior: play "Idle" animation
            base.PlayStateAnimation();

            // Optional: Add idle-specific animation logic here
            // Example: Random idle variations, breathing animations, etc.
        }
    }
}