using UnityEngine;
using Fusion;
using _GAME.Scripts.Combat;
using _GAME.Scripts.Core;

namespace _GAME.Scripts.Combat
{
    /// <summary>
    /// Core combo management component - handles all combo logic and validation
    /// This is the "brain" of the combat system that coordinates between input and execution
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class ComboController : NetworkBehaviour
    {
        [Header("Combo Configuration")] [SerializeField] private ComboDefinitionSO comboDefinition;

        [Header("Energy System")] [SerializeField] private int maxEnergy      = 100;
        [SerializeField]                           private int startingEnergy = 0;

        [Header("Debug")] [SerializeField] private bool enableDebugLogs    = true;
        [SerializeField]                   private bool showComboDebugInfo = false;

        // Network synchronized properties
        [Networked] public byte      CurrentComboIndex   { get; private set; } = 0;
        [Networked] public byte      CurrentAttackPhase  { get; set; }         = 0;
        [Networked] public TickTimer AttackStateTimer    { get; set; }
        [Networked] public TickTimer ComboWindowTimer    { get; set; }
        [Networked] public int       CurrentEnergy       { get; set; }
        [Networked] public bool      AttackInputConsumed { get; set; }
        [Networked] public int       AttackStartTick     { get; set; } // Track when attack started

        // Components
        private PlayerController _playerController;
        private NetworkInputData _lastProcessedInput;

        // Helper properties for cleaner code
        public AttackPhase AttackPhase { get => (AttackPhase)CurrentAttackPhase; set => CurrentAttackPhase = (byte)value; }

        public bool IsInCombo        => CurrentComboIndex > 0;
        public bool CanContinueCombo => CurrentComboIndex < comboDefinition.ComboLength;
        public bool IsComboComplete  => CurrentComboIndex >= comboDefinition.ComboLength;

        public override void Spawned()
        {
            _playerController = GetComponent<PlayerController>();

            if (_playerController == null)
            {
                Debug.LogError("[ComboController] PlayerController component not found!");
                return;
            }

            // Initialize energy system
            if (HasStateAuthority)
            {
                CurrentEnergy = startingEnergy;
                ResetCombo();
            }

            ValidateConfiguration();

            if (enableDebugLogs) Debug.Log($"[ComboController] Spawned on {(HasStateAuthority ? "Server" : "Client")} - Combo: {(comboDefinition ? comboDefinition.ComboName : "None")}");
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // Reset attack input consumption at start of each tick
            AttackInputConsumed = false;

            // Handle input processing
            if (GetInput(out NetworkInputData input))
            {
                ProcessAttackInput(input);
                _lastProcessedInput = input;
            }

            // Update timers and phases
            UpdateAttackTimers();
        }

        /// <summary>
        /// Process attack input and determine if attack should be executed
        /// </summary>
        private void ProcessAttackInput(NetworkInputData input)
        {
            // Check if attack was pressed this frame and not yet consumed
            if (input.WasAttackPressedThisFrame() && !AttackInputConsumed)
            {
                if (enableDebugLogs) Debug.Log($"[ComboController] Attack input detected: {input.attackInputType} at combo index {CurrentComboIndex}");

                // Validate and execute attack
                if (CanPerformAttack(input.attackInputType))
                {
                    ExecuteAttack(input.attackInputType);
                    ConsumeAttackInput();
                }
                else
                {
                    if (enableDebugLogs) Debug.Log($"[ComboController] Attack rejected - validation failed");

                    // Reset combo if invalid attack attempted outside combo window
                    if (AttackPhase != AttackPhase.ComboWindow)
                    {
                        ResetCombo();
                    }
                }
            }
        }

        /// <summary>
        /// Validate if an attack can be performed with given input type
        /// </summary>
        public bool CanPerformAttack(AttackInputType inputType)
        {
            if (comboDefinition == null || !comboDefinition.IsValidCombo())
            {
                if (enableDebugLogs) Debug.LogError("[ComboController] Invalid combo definition!");
                return false;
            }

            // Get the attack we're trying to perform
            var attackData = comboDefinition.GetAttackAtIndex(CurrentComboIndex);
            if (attackData == null)
            {
                if (enableDebugLogs) Debug.Log($"[ComboController] No attack at index {CurrentComboIndex}");
                return false;
            }

            // Validate input type matches attack requirement
            if (attackData.InputType != inputType)
            {
                if (enableDebugLogs) Debug.Log($"[ComboController] Input type mismatch: expected {attackData.InputType}, got {inputType}");
                return false;
            }

            // Validate energy requirement
            if (CurrentEnergy < attackData.EnergyCost)
            {
                if (enableDebugLogs) Debug.Log($"[ComboController] Not enough energy: need {attackData.EnergyCost}, have {CurrentEnergy}");
                return false;
            }

            // Validate grounded requirement
            if (attackData.RequiresGrounded && !_playerController.IsGrounded)
            {
                if (enableDebugLogs) Debug.Log("[ComboController] Attack requires grounded state");
                return false;
            }

            // Validate timing - can attack if not in attack state, or in combo window
            if (CurrentComboIndex == 0) // First attack
            {
                return AttackPhase == AttackPhase.None;
            }
            else // Combo continuation
            {
                return AttackPhase == AttackPhase.ComboWindow;
            }
        }

        /// <summary>
        /// Execute an attack - this handles the actual attack logic
        /// </summary>
        public void ExecuteAttack(AttackInputType inputType)
        {
            if (!HasStateAuthority)
            {
                Debug.LogWarning("[ComboController] ExecuteAttack called on non-authority client");
                return;
            }

            var attackData = comboDefinition.GetAttackAtIndex(CurrentComboIndex);
            if (attackData == null) return;

            if (enableDebugLogs) Debug.Log($"[ComboController] Executing attack: {attackData.AttackName} (Index: {CurrentComboIndex})");

            // Spend energy
            CurrentEnergy = Mathf.Max(0, CurrentEnergy - attackData.EnergyCost);

            // Setup attack timing
            AttackStartTick  = Runner.Tick;
            AttackStateTimer = TickTimer.CreateFromTicks(Runner, attackData.TotalFrames);
            AttackPhase      = AttackPhase.Startup;

            // Notify all clients about attack execution
            RPC_AttackExecuted(CurrentComboIndex, Runner.Tick, attackData.AttackName);

            // Advance combo index for next potential attack
            CurrentComboIndex++;

            if (showComboDebugInfo) LogComboState();
        }

        /// <summary>
        /// Update attack timers and phases - MOVED BACK FROM AttackState
        /// </summary>
        private void UpdateAttackTimers()
        {
            if (AttackStateTimer.ExpiredOrNotRunning(Runner)) return;

            var currentAttack = GetPreviousAttack(); // The attack currently being executed
            if (currentAttack == null) return;

            // Calculate elapsed frames since attack started
            int elapsedFrames = Runner.Tick - AttackStartTick;

            // Update attack phase based on elapsed time
            if (elapsedFrames < currentAttack.StartupFrames)
            {
                AttackPhase = AttackPhase.Startup;
            }
            else if (elapsedFrames < currentAttack.StartupFrames + currentAttack.ActiveFrames)
            {
                AttackPhase = AttackPhase.Active;
            }
            else if (elapsedFrames < currentAttack.TotalFrames)
            {
                AttackPhase = AttackPhase.Recovery;
            }
            else if (elapsedFrames < currentAttack.ComboInputWindow)
            {
                AttackPhase = AttackPhase.ComboWindow;

                // Start combo window timer if not already running
                if (ComboWindowTimer.ExpiredOrNotRunning(Runner))
                {
                    ComboWindowTimer = TickTimer.CreateFromTicks(Runner, currentAttack.ComboWindowFrames);
                }
            }
            else
            {
                // Attack completely finished and combo window expired
                CompleteAttackSequence();
            }
        }

        /// <summary>
        /// Complete the current attack sequence - called when combo window expires
        /// </summary>
        private void CompleteAttackSequence()
        {
            if (enableDebugLogs) Debug.Log($"[ComboController] Attack sequence completed - resetting combo");

            ResetCombo();
        }

        /// <summary>
        /// Reset combo state to beginning
        /// </summary>
        public void ResetCombo()
        {
            if (!HasStateAuthority) return;

            if (enableDebugLogs && IsInCombo) Debug.Log("[ComboController] Resetting combo state");

            CurrentComboIndex   = 0;
            AttackPhase         = AttackPhase.None;
            AttackStateTimer    = default;
            ComboWindowTimer    = default;
            AttackInputConsumed = false;
            AttackStartTick     = 0;
        }

        /// <summary>
        /// Get current attack data being executed
        /// </summary>
        public AttackDataSO GetCurrentAttack()
        {
            return comboDefinition?.GetAttackAtIndex(CurrentComboIndex);
        }

        /// <summary>
        /// Get previous attack data (the one currently being executed)
        /// </summary>
        public AttackDataSO GetPreviousAttack()
        {
            if (CurrentComboIndex == 0) return null;
            return comboDefinition?.GetAttackAtIndex(CurrentComboIndex - 1);
        }

        /// <summary>
        /// Check if we should continue combo (for state machine transitions)
        /// </summary>
        public bool ShouldContinueCombo()
        {
            return AttackPhase == AttackPhase.ComboWindow && CanContinueCombo;
        }

        /// <summary>
        /// Check if attack sequence is complete (for state machine transitions)
        /// </summary>
        public bool IsAttackComplete()
        {
            return AttackPhase == AttackPhase.None;
        }

        /// <summary>
        /// Consume attack input to prevent double-use
        /// </summary>
        private void ConsumeAttackInput()
        {
            AttackInputConsumed = true;
            if (enableDebugLogs) Debug.Log("[ComboController] Attack input consumed");
        }

        /// <summary>
        /// Add energy (called when attacks hit)
        /// </summary>
        public void AddEnergy(int amount)
        {
            if (!HasStateAuthority) return;

            CurrentEnergy = Mathf.Min(maxEnergy, CurrentEnergy + amount);

            if (enableDebugLogs) Debug.Log($"[ComboController] Energy added: +{amount} (Total: {CurrentEnergy}/{maxEnergy})");
        }

        /// <summary>
        /// Validate component configuration
        /// </summary>
        private void ValidateConfiguration()
        {
            if (comboDefinition == null)
            {
                Debug.LogError("[ComboController] ComboDefinition is not assigned!", this);
                return;
            }

            if (!comboDefinition.IsValidCombo())
            {
                Debug.LogError($"[ComboController] ComboDefinition '{comboDefinition.name}' is invalid!", this);
                return;
            }

            if (enableDebugLogs) Debug.Log($"[ComboController] Configuration valid - Combo: {comboDefinition.ComboName} ({comboDefinition.ComboLength} attacks)");
        }

        /// <summary>
        /// RPC to notify all clients about attack execution
        /// </summary>
        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        public void RPC_AttackExecuted(byte attackIndex, int startTick, string attackName)
        {
            if (enableDebugLogs) Debug.Log($"[ComboController] RPC_AttackExecuted received: {attackName} (Index: {attackIndex}) at tick {startTick}");

            // All clients can update visual state here
            // This is where animation triggers, VFX, SFX would be handled

            // Play attack animation on all clients
            if (_playerController != null)
            {
                var attackData = comboDefinition?.GetAttackAtIndex(attackIndex);
                if (attackData != null)
                {
                    _playerController.PlayAnimation(attackData.AnimationName);
                }
            }
        }

        /// <summary>
        /// Debug method to log current combo state
        /// </summary>
        [ContextMenu("Log Combo State")]
        public void LogComboState()
        {
            if (comboDefinition == null) return;

            Debug.Log($"[ComboController] === Combo State Debug ===");
            Debug.Log($"Combo: {comboDefinition.ComboName}");
            Debug.Log($"Current Index: {CurrentComboIndex}/{comboDefinition.ComboLength}");
            Debug.Log($"Attack Phase: {AttackPhase}");
            Debug.Log($"Energy: {CurrentEnergy}/{maxEnergy}");
            Debug.Log($"Is In Combo: {IsInCombo}");
            Debug.Log($"Can Continue: {CanContinueCombo}");
            Debug.Log($"Attack Timer Running: {!AttackStateTimer.ExpiredOrNotRunning(Runner)}");
            Debug.Log($"Combo Window Timer Running: {!ComboWindowTimer.ExpiredOrNotRunning(Runner)}");

            if (IsInCombo)
            {
                var currentAttack = GetPreviousAttack();
                if (currentAttack != null)
                {
                    int elapsedFrames = Runner.Tick - AttackStartTick;
                    Debug.Log($"Current Attack: {currentAttack.AttackName}");
                    Debug.Log($"Elapsed Frames: {elapsedFrames}/{currentAttack.TotalFrames}");
                }
            }
        }

        /// <summary>
        /// Force reset combo for debugging
        /// </summary>
        [ContextMenu("Force Reset Combo")]
        public void ForceResetCombo()
        {
            if (Application.isPlaying && HasStateAuthority)
            {
                ResetCombo();
                Debug.Log("[ComboController] Combo force reset!");
            }
        }

        /// <summary>
        /// Get combo definition for external systems
        /// </summary>
        public ComboDefinitionSO GetComboDefinition()
        {
            return comboDefinition;
        }

        /// <summary>
        /// Set combo definition at runtime (useful for character switching)
        /// </summary>
        public void SetComboDefinition(ComboDefinitionSO newCombo)
        {
            if (!HasStateAuthority)
            {
                Debug.LogWarning("[ComboController] SetComboDefinition called on non-authority client");
                return;
            }

            comboDefinition = newCombo;
            ResetCombo();
            ValidateConfiguration();

            if (enableDebugLogs) Debug.Log($"[ComboController] Combo definition changed to: {(newCombo ? newCombo.ComboName : "None")}");
        }

        /// <summary>
        /// RPC to notify all clients about successful hit
        /// </summary>
        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        public void RPC_AttackHit(byte attackIndex, Vector3 hitPosition, PlayerRef targetPlayer)
        {
            if (enableDebugLogs) Debug.Log($"[ComboController] RPC_AttackHit received: Attack {attackIndex} hit {targetPlayer} at {hitPosition}");

            // All clients can update hit effects here
            // This is where hit VFX, SFX, screen shake would be handled

            var attackData = comboDefinition?.GetAttackAtIndex(attackIndex);
            if (attackData != null)
            {
                // TODO: Play hit effects
                // PlayHitVFX(hitPosition, attackData);
                // PlayHitSFX(attackData);
                // TriggerScreenShake(attackData.KnockbackForce);
            }
        }
    }
}