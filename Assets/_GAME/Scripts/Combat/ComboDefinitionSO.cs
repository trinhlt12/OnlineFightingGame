using UnityEngine;
using System.Linq;

namespace _GAME.Scripts.Combat
{
    /// <summary>
    /// ScriptableObject defining a complete combo sequence
    /// Contains the "recipe" for a full combo chain made from AttackDataSO pieces
    /// </summary>
    [CreateAssetMenu(fileName = "Combo_", menuName = "Combat/Combo Definition", order = 2)]
    public class ComboDefinitionSO : ScriptableObject
    {
        [Header("Combo Configuration")] [Tooltip("Display name for this combo (for designer reference)")] public string ComboName = "Basic Combo";

        [TextArea(2, 3)] [Tooltip("Description of this combo sequence")] public string ComboDescription = "A basic attack combination";

        [Space(5)] [Tooltip("Sequence of attacks that make up this combo. Drag AttackDataSO assets here.")] public AttackDataSO[] AttackSequence = new AttackDataSO[0];

        [Header("Combo Rules")] [Tooltip("Allow players to buffer attack inputs during recovery frames")] public bool AllowInputBuffering = true;

        [Tooltip("Maximum frames an input can be buffered before combo window")] [Range(0, 10)] public int MaxBufferFrames = 5;

        [Tooltip("Reset combo if an attack misses the target")] public bool ResetOnMiss = true;

        [Tooltip("Reset combo if an attack is blocked")] public bool ResetOnBlock = false;

        [Tooltip("Allow combo to continue even if interrupted by being hit")] public bool AllowTradeHits = false;

        [Header("Advanced Settings")] [Tooltip("Allow special move cancelling during this combo")] public bool AllowSpecialCancelling = false;

        [Tooltip("Specific attacks that can be cancelled into special moves")] public AttackDataSO[] CancellableAttacks = new AttackDataSO[0];

        [Space(5)] [Tooltip("Damage scaling applied to later hits in combo (0.9 = 10% reduction per hit)")] [Range(0.5f, 1f)] public float DamageScaling = 0.9f;

        [Tooltip("Maximum combo length before automatic reset")] [Range(1, 20)] public int MaxComboLength = 10;

        [Header("Audio & Visual")] [Tooltip("Sound effect played when combo starts")] public AudioClip ComboStartSound;

        [Tooltip("Sound effect played when full combo completes")] public AudioClip ComboCompleteSound;

        // Computed properties
        public int  ComboLength => AttackSequence?.Length ?? 0;
        public bool IsEmpty     => ComboLength == 0;

        public float TotalComboDuration
        {
            get
            {
                if (AttackSequence == null) return 0f;
                return AttackSequence.Sum(attack => attack != null ? attack.TotalFrames : 0) / 60f; // Convert to seconds
            }
        }

        /// <summary>
        /// Validates if this combo definition is properly configured
        /// </summary>
        public bool IsValidCombo()
        {
            if (string.IsNullOrEmpty(ComboName))
            {
                Debug.LogError($"[ComboDefinitionSO] {name}: ComboName is required!", this);
                return false;
            }

            if (AttackSequence == null || AttackSequence.Length == 0)
            {
                Debug.LogError($"[ComboDefinitionSO] {name}: AttackSequence cannot be empty!", this);
                return false;
            }

            // Check for null attacks in sequence
            for (int i = 0; i < AttackSequence.Length; i++)
            {
                if (AttackSequence[i] == null)
                {
                    Debug.LogError($"[ComboDefinitionSO] {name}: Attack at index {i} is null!", this);
                    return false;
                }

                if (!AttackSequence[i].IsValid())
                {
                    Debug.LogError($"[ComboDefinitionSO] {name}: Attack '{AttackSequence[i].name}' at index {i} is invalid!", this);
                    return false;
                }
            }

            // Validate cancellable attacks are in the main sequence
            if (AllowSpecialCancelling && CancellableAttacks != null)
            {
                foreach (var cancellableAttack in CancellableAttacks)
                {
                    if (cancellableAttack != null && !AttackSequence.Contains(cancellableAttack))
                    {
                        Debug.LogWarning($"[ComboDefinitionSO] {name}: Cancellable attack '{cancellableAttack.name}' is not in the main AttackSequence!", this);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Get attack data at specific index in the combo
        /// </summary>
        public AttackDataSO GetAttackAtIndex(int index)
        {
            if (index < 0 || index >= ComboLength)
            {
                return null;
            }
            return AttackSequence[index];
        }

        /// <summary>
        /// Check if we've reached the end of the combo
        /// </summary>
        public bool IsComboComplete(int currentIndex)
        {
            return currentIndex >= ComboLength;
        }

        /// <summary>
        /// Check if a specific attack can be cancelled into special moves
        /// </summary>
        public bool CanCancelAttack(AttackDataSO attack)
        {
            if (!AllowSpecialCancelling || attack == null) return false;

            // If no specific cancellable attacks defined, allow all attacks in combo to be cancelled
            if (CancellableAttacks == null || CancellableAttacks.Length == 0)
            {
                return AttackSequence.Contains(attack);
            }

            return CancellableAttacks.Contains(attack);
        }

        /// <summary>
        /// Calculate damage for attack at specific combo index (applying scaling)
        /// </summary>
        public float GetScaledDamage(int comboIndex)
        {
            var attack = GetAttackAtIndex(comboIndex);
            if (attack == null) return 0f;

            // Apply damage scaling: first hit = full damage, subsequent hits scaled
            float scalingMultiplier = Mathf.Pow(DamageScaling, comboIndex);
            return attack.Damage * scalingMultiplier;
        }

        /// <summary>
        /// Get the next valid attack based on input type and current combo position
        /// </summary>
        public AttackDataSO GetNextAttack(int currentIndex, AttackInputType inputType)
        {
            var nextAttack = GetAttackAtIndex(currentIndex);

            // Check if the input type matches what the next attack expects
            if (nextAttack != null && nextAttack.InputType == inputType)
            {
                return nextAttack;
            }

            return null;
        }

        /// <summary>
        /// Reset combo state - useful for debugging and forced resets
        /// </summary>
        public void ResetCombo()
        {
            // This is primarily for editor/debugging purposes
            // Actual combo state is managed by ComboController
            Debug.Log($"[ComboDefinitionSO] {ComboName} combo reset requested");
        }

        /// <summary>
        /// Get detailed combo information for debugging
        /// </summary>
        public string GetComboInfo()
        {
            if (!IsValidCombo()) return "Invalid Combo";

            var info = $"Combo: {ComboName}\n";
            info += $"Length: {ComboLength} attacks\n";
            info += $"Duration: {TotalComboDuration:F2}s\n";
            info += $"Sequence: ";

            for (int i = 0; i < ComboLength; i++)
            {
                info += $"{AttackSequence[i].AttackName}";
                if (i < ComboLength - 1) info += " → ";
            }

            return info;
        }

        // Editor validation
        private void OnValidate()
        {
            // Ensure reasonable values
            MaxBufferFrames = Mathf.Max(0, MaxBufferFrames);
            MaxComboLength  = Mathf.Max(1, MaxComboLength);
            DamageScaling   = Mathf.Clamp(DamageScaling, 0.1f, 1f);

            // Validate combo name
            if (string.IsNullOrEmpty(ComboName))
            {
                ComboName = name; // Use asset name as fallback
            }

            // Ensure AttackSequence is not longer than MaxComboLength
            if (AttackSequence != null && AttackSequence.Length > MaxComboLength)
            {
                Debug.LogWarning($"[ComboDefinitionSO] {name}: AttackSequence length ({AttackSequence.Length}) exceeds MaxComboLength ({MaxComboLength}). Consider adjusting MaxComboLength.");
            }
        }

        // Context menu for editor debugging
        [ContextMenu("Validate Combo")]
        private void ValidateComboFromEditor()
        {
            if (IsValidCombo())
            {
                Debug.Log($"[ComboDefinitionSO] {name}: ✅ Combo is valid!\n{GetComboInfo()}");
            }
            else
            {
                Debug.LogError($"[ComboDefinitionSO] {name}: ❌ Combo validation failed!");
            }
        }

        [ContextMenu("Log Combo Info")]
        private void LogComboInfo()
        {
            Debug.Log($"[ComboDefinitionSO] {GetComboInfo()}");
        }
    }
}