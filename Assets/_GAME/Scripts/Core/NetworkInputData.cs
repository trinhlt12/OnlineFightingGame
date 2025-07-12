namespace _GAME.Scripts.Core
{
    using Fusion;
    using UnityEngine;
    using _GAME.Scripts.Combat; // For AttackInputType enum

    /// <summary>
    /// Network input data structure for synchronizing player inputs across the network
    /// Enhanced with combat system support
    /// </summary>
    public struct NetworkInputData : INetworkInput
    {
        // Movement input (-1 to 1 for horizontal movement)
        public float horizontal;

        // Button states using bitwise operations for efficiency
        public NetworkButtons buttons;
        public NetworkButtons previousButtons; // Track previous frame for input detection

        // Direction the player is aiming/looking (for directional attacks)
        public Vector2 aimDirection;

        // COMBAT SYSTEM - New fields for attack input
        [Tooltip("Type of attack input based on directional input")]
        public AttackInputType attackInputType;

        [Tooltip("Whether attack input has been consumed this frame")]
        public bool attackInputConsumed;

        [Tooltip("Frame when attack input was first pressed (for input buffering)")]
        public int attackInputFrame;

        /// <summary>
        /// Helper method to initialize attack input data
        /// </summary>
        public void SetAttackInput(AttackInputType inputType, int currentFrame)
        {
            attackInputType = inputType;
            attackInputFrame = currentFrame;
            attackInputConsumed = false;
        }

        /// <summary>
        /// Determine attack input type based on directional input
        /// </summary>
        public AttackInputType GetAttackInputType(float horizontal, float vertical)
        {
            const float threshold = 0.3f;

            // Check vertical input first (Up attacks have priority)
            if (vertical > threshold)
                return AttackInputType.Up;
            else if (vertical < -threshold)
                return AttackInputType.Down;
            // Then check horizontal
            else if (Mathf.Abs(horizontal) > threshold)
                return AttackInputType.Forward;
            else
                return AttackInputType.Neutral;
        }
    }

    /// <summary>
    /// Button flags for network synchronization
    /// Using bit flags for efficient network data transmission
    /// </summary>
    [System.Flags]
    public enum NetworkButtons : uint
    {
        None    = 0,
        Jump    = 1 << 0, //Space key
        Attack  = 1 << 1, // Basic attack button (J key)
        Special = 1 << 2, // Special move button (K key) - for future use
        Dodge   = 1 << 3, // Dodge/Dash button (L key) - for future use
        Block   = 1 << 4, // Block button (semicolon key) - for future use
        // Add more buttons as needed - each takes one bit
    }

    /// <summary>
    /// Extension methods for easier button handling
    /// Enhanced with combat-specific input methods
    /// </summary>
    public static class NetworkButtonsExtensions
    {
        public static bool IsPressed(this NetworkButtons buttons, NetworkButtons button)
        {
            return (buttons & button) == button;
        }

        /// <summary>
        /// Checks if a button was pressed down in the current frame.
        /// </summary>
        public static bool WasPressedThisFrame(this NetworkInputData current, NetworkButtons button)
        {
            return current.buttons.IsPressed(button) && !current.previousButtons.IsPressed(button);
        }

        public static bool IsSet(this NetworkButtons buttons, NetworkButtons button)
        {
            return (buttons & button) == button;
        }

        public static NetworkButtons Set(this NetworkButtons buttons, NetworkButtons button, bool pressed)
        {
            if (pressed)
                return buttons | button;
            else
                return buttons & ~button;
        }

        // COMBAT SYSTEM - New extension methods for attack input

        /// <summary>
        /// Checks if attack button was pressed this frame and not consumed
        /// </summary>
        public static bool WasAttackPressedThisFrame(this NetworkInputData current)
        {
            return current.WasPressedThisFrame(NetworkButtons.Attack) && !current.attackInputConsumed;
        }

        /// <summary>
        /// Helper to consume attack input to prevent double-use
        /// </summary>
        public static NetworkInputData ConsumeAttackInput(this NetworkInputData current)
        {
            current.attackInputConsumed = true;
            return current;
        }

        /// <summary>
        /// Check if any movement input is active
        /// Useful for determining if player is trying to move during combat
        /// </summary>
        public static bool HasMovementInput(this NetworkInputData current)
        {
            return Mathf.Abs(current.horizontal) > 0.01f;
        }

        /// <summary>
        /// Check if directional input matches the attack requirement
        /// Used for validating combo inputs
        /// </summary>
        public static bool IsDirectionalInputValid(this NetworkInputData current, AttackInputType requiredType)
        {
            // Neutral attacks don't require specific directional input
            if (requiredType == AttackInputType.Neutral) return true;

            return current.attackInputType == requiredType;
        }

        /// <summary>
        /// Check if jump button was pressed this frame
        /// Maintains compatibility with existing jump system
        /// </summary>
        public static bool WasJumpPressedThisFrame(this NetworkInputData current)
        {
            return current.WasPressedThisFrame(NetworkButtons.Jump);
        }
    }
}