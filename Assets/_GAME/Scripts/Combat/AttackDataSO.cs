using UnityEngine;

namespace _GAME.Scripts.Combat
{
    /// <summary>
    /// ScriptableObject defining a single attack in the combo system
    /// This is the fundamental building block - each attack is one "piece"
    /// that designers can mix and match to create different combos
    /// </summary>
    [CreateAssetMenu(fileName = "Attack_", menuName = "Combat/Attack Data", order = 1)]
    public class AttackDataSO : ScriptableObject
    {
        [Header("Basic Info")] [Tooltip("Descriptive name for this attack (for designer reference)")] public string AttackName = "Basic Attack";

        [TextArea(2, 4)] [Tooltip("Description of what this attack does")] public string AttackDescription = "A basic attack";

        [Header("Animation")] [Tooltip("Name of animation to play when executing this attack")] public string AnimationName = "Attack";

        [Range(0.1f, 3f)] [Tooltip("Speed multiplier for the animation")] public float AnimationSpeed = 1f;

        [Header("Timing (Network-Safe Frames)")] [Tooltip("Frames before hit becomes active (wind-up time)")] [Range(1, 30)] public int StartupFrames = 5;

        [Tooltip("Frames where hitbox is active and can hit opponent")] [Range(1, 10)] public int ActiveFrames = 3;

        [Tooltip("Frames after hit ends before player can act again")] [Range(1, 30)] public int RecoveryFrames = 10;

        [Tooltip("Additional frames after recovery where next combo input can be entered")] [Range(0, 20)] public int ComboWindowFrames = 15;

        [Header("Damage & Physics")] [Tooltip("Base damage this attack deals")] [Range(0f, 100f)] public float Damage = 10f;

        [Tooltip("Force applied to hit opponent (X = horizontal, Y = vertical)")] public Vector2 KnockbackForce = new Vector2(5f, 2f);

        [Tooltip("Frames opponent is stunned when hit")] [Range(0, 30)] public int HitstunFrames = 12;

        [Header("Hitbox Configuration")] [Tooltip("Size of the attack hitbox (X = width, Y = height)")] public Vector2 HitboxSize = Vector2.one;

        [Tooltip("Offset from player center where hitbox appears")] public Vector2 HitboxOffset = Vector2.zero;

        [Tooltip("Layers that this attack can hit")] public LayerMask HitLayers = -1;

        [Header("Input Requirements")] [Tooltip("Type of directional input required for this attack")] public AttackInputType InputType = AttackInputType.Neutral;

        [Tooltip("Can this attack only be performed while grounded?")] public bool RequiresGrounded = true;

        [Header("Resources & Energy")] [Tooltip("Energy gained when this attack successfully hits")] [Range(0, 50)] public int EnergyGain = 10;

        [Tooltip("Energy cost to perform this attack (0 = free)")] [Range(0, 100)] public int EnergyCost = 0;

        [Header("Advanced Properties")] [Tooltip("Can this attack be cancelled into special moves?")] public bool AllowSpecialCancel = false;

        [Tooltip("Priority level for trading hits (higher = wins)")] [Range(0, 10)] public int Priority = 5;

        // Computed properties for easier access
        public int TotalFrames      => StartupFrames + ActiveFrames + RecoveryFrames;
        public int ComboInputWindow => TotalFrames + ComboWindowFrames;

        /// <summary>
        /// Validates if this attack data is properly configured
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(AnimationName))
            {
                Debug.LogError($"[AttackDataSO] {name}: AnimationName is required!", this);
                return false;
            }

            if (StartupFrames < 1 || ActiveFrames < 1 || RecoveryFrames < 1)
            {
                Debug.LogError($"[AttackDataSO] {name}: All frame counts must be at least 1!", this);
                return false;
            }

            if (Damage < 0)
            {
                Debug.LogError($"[AttackDataSO] {name}: Damage cannot be negative!", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get frame number when hitbox becomes active
        /// </summary>
        public int GetHitboxActiveFrame()
        {
            return StartupFrames;
        }

        /// <summary>
        /// Get frame number when hitbox becomes inactive
        /// </summary>
        public int GetHitboxInactiveFrame()
        {
            return StartupFrames + ActiveFrames;
        }

        /// <summary>
        /// Check if hitbox should be active at given frame
        /// </summary>
        public bool IsHitboxActiveAtFrame(int frame)
        {
            return frame >= StartupFrames && frame < (StartupFrames + ActiveFrames);
        }

        /// <summary>
        /// Get attack phase at specific frame
        /// </summary>
        public AttackPhase GetPhaseAtFrame(int frame)
        {
            if (frame < StartupFrames)
                return AttackPhase.Startup;
            else if (frame < StartupFrames + ActiveFrames)
                return AttackPhase.Active;
            else if (frame < TotalFrames)
                return AttackPhase.Recovery;
            else if (frame < ComboInputWindow)
                return AttackPhase.ComboWindow;
            else
                return AttackPhase.None;
        }

        // Editor validation
        private void OnValidate()
        {
            // Ensure minimum values
            StartupFrames     = Mathf.Max(1, StartupFrames);
            ActiveFrames      = Mathf.Max(1, ActiveFrames);
            RecoveryFrames    = Mathf.Max(1, RecoveryFrames);
            ComboWindowFrames = Mathf.Max(0, ComboWindowFrames);

            // Ensure positive damage
            Damage = Mathf.Max(0f, Damage);

            // Ensure reasonable hitbox size
            HitboxSize.x = Mathf.Max(0.1f, HitboxSize.x);
            HitboxSize.y = Mathf.Max(0.1f, HitboxSize.y);
        }
    }

    /// <summary>
    /// Types of directional input for attacks
    /// </summary>
    public enum AttackInputType : byte
    {
        None    = 0,
        Neutral = 1, // No directional input (just attack button)
        Forward = 2, // Forward + Attack (→ + Attack)
        Up      = 3, // Up + Attack (↑ + Attack)
        Down    = 4  // Down + Attack (↓ + Attack) - for future use
    }

    /// <summary>
    /// Phases of an attack for network synchronization
    /// </summary>
    public enum AttackPhase : byte
    {
        None        = 0, // Not in attack
        Startup     = 1, // Wind-up before hit
        Active      = 2, // Hitbox is active
        Recovery    = 3, // Cool-down after hit
        ComboWindow = 4  // Window for next combo input
    }
}