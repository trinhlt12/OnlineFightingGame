namespace _GAME.Scripts.Core
{
    using Fusion;
    using UnityEngine;

    /// <summary>
    /// Network input data structure for synchronizing player inputs across the network
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
        Attack  = 1 << 1,
        Special = 1 << 2,
        Dodge   = 1 << 3,
        Block   = 1 << 4,
        // Add more buttons as needed
    }

    /// <summary>
    /// Extension methods for easier button handling
    /// </summary>
    public static class NetworkButtonsExtensions
    {
        public static bool IsPressed(this NetworkButtons buttons, NetworkButtons button)
        {
            return (buttons & button) == button;
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
    }
}