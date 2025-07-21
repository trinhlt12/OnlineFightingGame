using UnityEngine;
using Fusion;
using _GAME.Scripts.Combat;
using _GAME.Scripts.Core;

namespace _GAME.Scripts.Combat
{
    /// <summary>
    /// REFACTORED: Core combo management component
    /// SINGLE RESPONSIBILITY: Manage combo state, validation, and network synchronization ONLY
    /// All attack execution and timing is now handled here, AttackState only manages FSM transitions
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class ComboController : NetworkBehaviour
    {
        [Header("Combo Configuration")] [SerializeField] private ComboDefinitionSO comboDefinition;

        [Header("Energy System")] [SerializeField] private int maxEnergy      = 100;
        [SerializeField]                           private int startingEnergy = 0;

        [Header("Debug")] [SerializeField] private bool enableDebugLogs    = true;
        [SerializeField]                   private bool showComboDebugInfo = false;

        // ==================== NETWORK SYNCHRONIZED PROPERTIES ====================
        [Networked] public byte CurrentComboIndex  { get; private set; } = 0;
        [Networked] public byte CurrentAttackPhase { get; private set; } = 0;
        [Networked] public int  AttackStartTick    { get; private set; }
        [Networked] public int  CurrentEnergy      { get; private set; }
        [Networked] public bool IsExecutingAttack  { get; private set; }

        // ==================== COMPONENTS ====================
        private PlayerController _playerController;

        // ==================== PROPERTIES ====================
        public AttackPhase AttackPhase { get => (AttackPhase)CurrentAttackPhase; private set => CurrentAttackPhase = (byte)value; }

        public bool IsInCombo        => CurrentComboIndex > 0;
        public bool IsAttacking      => IsExecutingAttack;
        public bool CanContinueCombo => CurrentComboIndex < (comboDefinition?.ComboLength ?? 0);
        public bool IsComboComplete  => CurrentComboIndex >= (comboDefinition?.ComboLength ?? 0);

        // ==================== LIFECYCLE ====================
        public override void Spawned()
        {
            _playerController = GetComponent<PlayerController>();

            if (_playerController == null)
            {
                Debug.LogError("[ComboController] PlayerController component not found!");
                enabled = false;
                return;
            }

            if (HasStateAuthority)
            {
                InitializeEnergySystem();
                ResetCombo();
            }

            ValidateConfiguration();

            if (enableDebugLogs) Debug.Log($"[ComboController] Spawned on {(HasStateAuthority ? "Server" : "Client")} - Combo: {(comboDefinition ? comboDefinition.ComboName : "None")}");
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // SINGLE RESPONSIBILITY: Only update attack timing when actively attacking
            if (IsExecutingAttack)
            {
                UpdateAttackTiming();
            }
        }

        // ==================== CORE ATTACK SYSTEM ====================
        /// <summary>
        /// Check if input type is compatible with attack requirements
        /// Fighting game logic: Allow flexibility for better user experience
        /// </summary>
        private bool IsInputTypeCompatible(AttackInputType attackRequirement, AttackInputType playerInput)
        {
            // If attack requires Neutral, allow ANY input type to trigger it
            // This enables attacks while moving - essential for fighting games
            if (attackRequirement == AttackInputType.Neutral)
            {
                return true; // Neutral attacks can be triggered by any input
            }

            // For directional attacks, require exact match
            return attackRequirement == playerInput;
        }

        /// <summary>
        /// Main entry point for attack execution - called by AttackState
        /// </summary>
        public bool TryExecuteAttack(AttackInputType inputType)
        {
            if (!HasStateAuthority)
            {
                Debug.LogWarning("[ComboController] TryExecuteAttack called on non-authority client");
                return false;
            }

            if (!CanPerformAttack(inputType))
            {
                if (enableDebugLogs) Debug.Log($"[ComboController] Attack validation failed for input: {inputType}");
                return false;
            }

            ExecuteAttack(inputType);
            return true;
        }

        /// <summary>
        /// Check if next combo attack can be executed
        /// </summary>
        public bool TryExecuteComboAttack(AttackInputType inputType)
        {
            if (!HasStateAuthority) return false;

            // Must be in combo window to continue
            if (AttackPhase != AttackPhase.ComboWindow)
            {
                if (enableDebugLogs) Debug.Log("[ComboController] Not in combo window, cannot continue combo");
                return false;
            }

            return TryExecuteAttack(inputType);
        }

        /// <summary>
        /// Validate if an attack can be performed
        /// </summary>
        public bool CanPerformAttack(AttackInputType inputType)
        {
            if (!IsConfigurationValid()) return false;

            var attackData = comboDefinition.GetAttackAtIndex(CurrentComboIndex);
            if (attackData == null)
            {
                if (enableDebugLogs) Debug.Log($"[ComboController] No attack at index {CurrentComboIndex}");
                return false;
            }

            // NEW: Use flexible input compatibility instead of strict matching
            if (!IsInputTypeCompatible(attackData.InputType, inputType))
            {
                if (enableDebugLogs) Debug.Log($"[ComboController] Input incompatible: attack requires {attackData.InputType}, got {inputType}");
                return false;
            }

            // Validate energy
            if (CurrentEnergy < attackData.EnergyCost)
            {
                if (enableDebugLogs) Debug.Log($"[ComboController] Insufficient energy: need {attackData.EnergyCost}, have {CurrentEnergy}");
                return false;
            }

            // Validate grounded requirement
            if (attackData.RequiresGrounded && !_playerController.IsGrounded)
            {
                if (enableDebugLogs) Debug.Log("[ComboController] Attack requires grounded state");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Execute attack with proper network synchronization
        /// </summary>
        private void ExecuteAttack(AttackInputType inputType)
        {
            var attackData = comboDefinition.GetAttackAtIndex(CurrentComboIndex);
            if (attackData == null) return;

            if (enableDebugLogs) Debug.Log($"[ComboController] Executing attack: {attackData.AttackName} (Index: {CurrentComboIndex})");

            // Spend energy
            CurrentEnergy = Mathf.Max(0, CurrentEnergy - attackData.EnergyCost);

            // Setup attack state
            AttackStartTick   = Runner.Tick;
            AttackPhase       = AttackPhase.Startup;
            IsExecutingAttack = true;

            // Advance combo index for next potential attack
            CurrentComboIndex++;

            // Notify all clients about attack execution
            RPC_AttackExecuted((byte)(CurrentComboIndex - 1), Runner.Tick, attackData.AttackName);
            if (showComboDebugInfo) LogComboState();
        }

        /// <summary>
        /// Update attack timing and phases - SINGLE SOURCE OF TRUTH
        /// </summary>
        private void UpdateAttackTiming()
        {
            var currentAttack = GetCurrentExecutingAttack();
            if (currentAttack == null)
            {
                CompleteAttack();
                return;
            }

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
            }
            else
            {
                // Attack and combo window completely finished
                CompleteAttack();
            }
        }

        /// <summary>
        /// Complete current attack and determine next state
        /// </summary>
        private void CompleteAttack()
        {
            if (enableDebugLogs) Debug.Log("[ComboController] Attack sequence completed");

            IsExecutingAttack = false;
            AttackPhase       = AttackPhase.None;

            // Reset combo if no more attacks queued
            if (!ShouldContinueCombo())
            {
                ResetCombo();
            }
        }

        /// <summary>
        /// Reset combo state to beginning
        /// </summary>
        public void ResetCombo()
        {
            if (!HasStateAuthority) return;

            if (enableDebugLogs && IsInCombo) Debug.Log("[ComboController] Resetting combo state");

            CurrentComboIndex = 0;
            AttackPhase       = AttackPhase.None;
            IsExecutingAttack = false;
            AttackStartTick   = 0;
        }

        // ==================== HITBOX AND DAMAGE SYSTEM ====================

        /// <summary>
        /// Check if hitbox should be active at current frame
        /// </summary>
        public bool IsHitboxActive()
        {
            if (!IsExecutingAttack) return false;

            var currentAttack = GetCurrentExecutingAttack();
            if (currentAttack == null) return false;

            int elapsedFrames = Runner.Tick - AttackStartTick;
            return currentAttack.IsHitboxActiveAtFrame(elapsedFrames);
        }

        /// <summary>
        /// Get hitbox data for current attack
        /// </summary>
        public (Vector2 center, Vector2 size, LayerMask layers) GetHitboxData()
        {
            var attack = GetCurrentExecutingAttack();
            if (attack == null) return (Vector2.zero, Vector2.zero, 0);

            Vector2 offset = attack.HitboxOffset;
            if (!_playerController.IsFacingRight)
            {
                offset.x = -offset.x;
            }

            Vector2 center = (Vector2)transform.position + offset;
            return (center, attack.HitboxSize, attack.HitLayers);
        }

        /// <summary>
        /// Process successful hit - called by AttackState
        /// </summary>
        public void ProcessHit(PlayerController target)
        {
            if (!HasStateAuthority || target == null) return;

            var attack = GetCurrentExecutingAttack();
            if (attack == null) return;

            // Calculate damage with combo scaling
            float damage = comboDefinition?.GetScaledDamage(CurrentComboIndex - 1) ?? attack.Damage;

            // Add energy for successful hit
            AddEnergy(attack.EnergyGain);
            /*
            Debug.LogWarning($"[ComboController] Hit {target.name} for {damage} damage, gained {attack.EnergyGain} energy");
            */

            // Notify all clients about hit
            RPC_AttackHit(
                (byte)(CurrentComboIndex - 1),
                target.transform.position,
                target.Object.InputAuthority,
                damage
            );
        }

        // ==================== ENERGY SYSTEM ====================

        private void InitializeEnergySystem()
        {
            CurrentEnergy = startingEnergy;
        }

        public void AddEnergy(int amount)
        {
            if (!HasStateAuthority) return;

            CurrentEnergy = Mathf.Clamp(CurrentEnergy + amount, 0, maxEnergy);

            if (enableDebugLogs) Debug.Log($"[ComboController] Energy: {CurrentEnergy}/{maxEnergy} (+{amount})");
        }

        public bool HasEnoughEnergy(int requiredEnergy)
        {
            return CurrentEnergy >= requiredEnergy;
        }

        // ==================== DATA ACCESS ====================

        public AttackDataSO GetCurrentExecutingAttack()
        {
            if (!IsExecutingAttack || CurrentComboIndex == 0) return null;
            return comboDefinition?.GetAttackAtIndex(CurrentComboIndex - 1);
        }

        public AttackDataSO GetNextAttack()
        {
            return comboDefinition?.GetAttackAtIndex(CurrentComboIndex);
        }

        public ComboDefinitionSO GetComboDefinition()
        {
            return comboDefinition;
        }

        public int GetElapsedAttackFrames()
        {
            return IsExecutingAttack ? Runner.Tick - AttackStartTick : 0;
        }

        // ==================== STATE QUERIES ====================

        public bool IsAttackComplete()
        {
            return !IsExecutingAttack;
        }

        public bool ShouldContinueCombo()
        {
            return AttackPhase == AttackPhase.ComboWindow && CanContinueCombo;
        }

        public bool IsInStartupPhase()  => AttackPhase == AttackPhase.Startup;
        public bool IsInActivePhase()   => AttackPhase == AttackPhase.Active;
        public bool IsInRecoveryPhase() => AttackPhase == AttackPhase.Recovery;
        public bool IsInComboWindow()   => AttackPhase == AttackPhase.ComboWindow;

        // ==================== VALIDATION ====================

        private bool IsConfigurationValid()
        {
            return comboDefinition != null && comboDefinition.IsValidCombo();
        }

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

        // ==================== NETWORK RPCs ====================

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        public void RPC_AttackExecuted(byte attackIndex, int startTick, string attackName)
        {
            if (enableDebugLogs) Debug.Log($"[ComboController] RPC_AttackExecuted: {attackName} (Index: {attackIndex}) at tick {startTick}");

            // Visual effects on all clients
            var attackData = comboDefinition?.GetAttackAtIndex(attackIndex);
            if (attackData != null && _playerController != null)
            {
                _playerController.PlayAnimation(attackData.AnimationName);
            }
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        public void RPC_AttackHit(byte attackIndex, Vector3 hitPosition, PlayerRef targetPlayer, float damage)
        {
            if (enableDebugLogs) Debug.Log($"[ComboController] RPC_AttackHit: Attack {attackIndex} hit {targetPlayer} for {damage} damage at {hitPosition}");

            // Hit effects on all clients
            var attackData = comboDefinition?.GetAttackAtIndex(attackIndex);
            if (attackData != null)
            {
                // TODO: Play hit VFX, SFX, screen shake
                // PlayHitEffects(hitPosition, attackData, damage);
            }
        }

        // ==================== DEBUG ====================

        [ContextMenu("Log Combo State")]
        public void LogComboState()
        {
            if (!IsConfigurationValid()) return;

            Debug.Log($"[ComboController] === Combo State Debug ===");
            Debug.Log($"Combo: {comboDefinition.ComboName}");
            Debug.Log($"Current Index: {CurrentComboIndex}/{comboDefinition.ComboLength}");
            Debug.Log($"Attack Phase: {AttackPhase}");
            Debug.Log($"Is Executing: {IsExecutingAttack}");
            Debug.Log($"Energy: {CurrentEnergy}/{maxEnergy}");
            Debug.Log($"Can Continue: {CanContinueCombo}");

            if (IsExecutingAttack)
            {
                var currentAttack = GetCurrentExecutingAttack();
                if (currentAttack != null)
                {
                    int elapsedFrames = GetElapsedAttackFrames();
                    Debug.Log($"Current Attack: {currentAttack.AttackName}");
                    Debug.Log($"Elapsed Frames: {elapsedFrames}/{currentAttack.TotalFrames}");
                }
            }
        }

        [ContextMenu("Force Reset Combo")]
        public void ForceResetCombo()
        {
            if (Application.isPlaying && HasStateAuthority)
            {
                ResetCombo();
                Debug.Log("[ComboController] Combo force reset!");
            }
        }

        // ==================== RUNTIME CONFIGURATION ====================

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
    }
}