namespace _GAME.Scripts.FSM.ConcreteState
{
    using _GAME.Scripts.Core;
    using _GAME.Scripts.FSM;
    using _GAME.Scripts.Combat;
    using Fusion;
    using UnityEngine;

    /// <summary>
    /// REFACTORED: Attack state for FSM management ONLY
    /// SINGLE RESPONSIBILITY: Handle state transitions and coordinate with ComboController
    /// All combat logic is delegated to ComboController for clean separation of concerns
    /// </summary>
    public class AttackState : NetworkedBaseState<PlayerController>
    {
        // ==================== COMPONENTS ====================
        private ComboController _comboController;

        // ==================== STATE TRACKING ====================
        private AttackDataSO _currentAttackData;
        private int          _lastHitboxCheckFrame = -1;
        private bool         _hasHitThisAttack     = false;

        // ==================== SETTINGS ====================
        [Header("Debug Settings")] private readonly bool _enableCombatLogs = true;

        // ==================== LIFECYCLE ====================

        public AttackState(PlayerController controller) : base(controller, "")
        {
            _comboController = controller.GetComponent<ComboController>();

            if (_comboController == null)
            {
                Debug.LogError("[AttackState] ComboController component not found on PlayerController!");
            }
        }

        public override void EnterState()
        {
            if (!ValidateComponents())
            {
                ForceExitToIdle();
                return;
            }

            if (!HasStateAuthority)
            {
                // Clients enter for visual consistency
                base.EnterState();
                return;
            }

            // SERVER: Execute initial attack or validate combo continuation
            bool attackExecuted = false;

            if (!_comboController.IsInCombo)
            {
                // Starting new combo
                attackExecuted = TryExecuteInitialAttack();
            }
            else if (_comboController.IsInComboWindow())
            {
                // Continuing existing combo
                attackExecuted = TryExecuteComboAttack();
            }

            if (!attackExecuted)
            {
                if (_enableCombatLogs) Debug.LogWarning("[AttackState] Failed to execute attack, exiting to idle");
                ForceExitToIdle();
                return;
            }

            // Update current attack data and animation
            _currentAttackData = _comboController.GetCurrentExecutingAttack();
            if (_currentAttackData != null)
            {
                SetAnimationName(_currentAttackData.AnimationName);
                base.EnterState();
                ResetAttackFlags();

                if (_enableCombatLogs) Debug.Log($"[AttackState] Entered with attack: {_currentAttackData.AttackName}");
            }
            else
            {
                Debug.LogError("[AttackState] No current attack data available after execution!");
                ForceExitToIdle();
            }
        }

        public override void StateFixedUpdate()
        {
            if (!HasStateAuthority || !ValidateComponents()) return;

            // ComboController handles all timing updates automatically
            // We just need to handle hitbox detection and combo input

            HandleHitboxDetection();
            HandleComboInput();

            // Check if we should exit attack state
            if (_comboController.IsAttackComplete())
            {
                if (_enableCombatLogs) Debug.Log("[AttackState] Attack sequence complete, ready to exit");
                // FSM will handle transition based on movement input
            }
        }

        public override void StateUpdate()
        {
            // Visual updates on all clients
            UpdateVisualEffects();
        }

        public override void ExitState()
        {
            if (_enableCombatLogs) Debug.Log("[AttackState] Exiting attack state");

            ResetAttackFlags();
            base.ExitState();
        }

        // ==================== ATTACK EXECUTION ====================

        /// <summary>
        /// Try to execute initial attack (starting new combo)
        /// </summary>
        private bool TryExecuteInitialAttack()
        {
            var inputType = entity.GetCurrentAttackInputType();

            if (_enableCombatLogs) Debug.Log($"[AttackState] Attempting initial attack with input: {inputType}");

            bool success = _comboController.TryExecuteAttack(inputType);

            if (success)
            {
                entity.ConsumeAttackInput();
            }

            return success;
        }

        /// <summary>
        /// Try to execute combo continuation attack
        /// </summary>
        private bool TryExecuteComboAttack()
        {
            var inputType = entity.GetCurrentAttackInputType();

            if (_enableCombatLogs) Debug.Log($"[AttackState] Attempting combo attack with input: {inputType}");

            bool success = _comboController.TryExecuteComboAttack(inputType);

            if (success)
            {
                entity.ConsumeAttackInput();

                // Update to new attack data without exiting state
                _currentAttackData = _comboController.GetCurrentExecutingAttack();
                if (_currentAttackData != null)
                {
                    SetAnimationName(_currentAttackData.AnimationName);
                    PlayCustomAnimation(_currentAttackData.AnimationName);
                    ResetAttackFlags();

                    if (_enableCombatLogs) Debug.Log($"[AttackState] Combo continued with: {_currentAttackData.AttackName}");
                }
            }

            return success;
        }

        // ==================== HITBOX SYSTEM ====================

        /// <summary>
        /// Handle hitbox detection during active frames
        /// </summary>
        private void HandleHitboxDetection()
        {
            if (!_comboController.IsHitboxActive() || _hasHitThisAttack) return;

            // Only check once per frame during active phase
            int currentFrame = _comboController.GetElapsedAttackFrames();
            if (currentFrame == _lastHitboxCheckFrame) return;

            _lastHitboxCheckFrame = currentFrame;
            PerformHitboxCheck();
        }

        /// <summary>
        /// Perform actual hitbox collision detection
        /// </summary>
        private void PerformHitboxCheck()
        {
            var (center, size, layers) = _comboController.GetHitboxData();

            if (size == Vector2.zero) return;

            // Perform overlap detection
            var hitColliders = Physics2D.OverlapBoxAll(center, size, 0f, layers);

            foreach (var collider in hitColliders)
            {
                // Skip self
                if (collider.transform == entity.transform) continue;

                // Check for valid target
                var targetPlayer = collider.GetComponent<PlayerController>();
                if (targetPlayer != null && targetPlayer != entity)
                {
                    ProcessSuccessfulHit(targetPlayer);
                    _hasHitThisAttack = true; // Prevent multiple hits per attack
                    break;                    // Only hit one target per attack
                }
            }

            // Debug visualization
            if (_enableCombatLogs && Application.isEditor)
            {
                DrawHitboxDebug(center, size);
            }
        }

        /// <summary>
        /// Process successful hit on target
        /// </summary>
        /// <summary>
        /// Process successful hit on target using new damage system
        /// </summary>
        private void ProcessSuccessfulHit(PlayerController target)
        {
            if (target == null || _currentAttackData == null) return;

            // Get damage receiver component
            var damageReceiver = target.GetComponent<DamageReceiver>();
            if (damageReceiver == null)
            {
                Debug.LogError($"[AttackState] Target {target.name} has no DamageReceiver component!");
                return;
            }

            // Calculate damage and knockback
            float   damage    = _currentAttackData.Damage;
            Vector2 knockback = _currentAttackData.KnockbackForce;
            int     hitstun   = _currentAttackData.HitstunFrames;

            // Apply directional knockback based on attacker facing
            if (!entity.IsFacingRight)
            {
                knockback.x = -knockback.x;
            }

            // Send damage to target
            bool hitSuccessful = damageReceiver.TakeDamage(damage, knockback, hitstun, entity);

            if (hitSuccessful)
            {
                // Process hit on attacker side (energy gain, combo scaling, etc.)
                _comboController.ProcessHit(target);

                if (_enableCombatLogs)
                {
                    Debug.Log($"[AttackState] Successfully hit {target.name} - Damage: {damage}, Hitstun: {hitstun}");
                }
            }
        }

        // ==================== COMBO INPUT HANDLING ====================

        /// <summary>
        /// Handle combo input during combo window
        /// </summary>
        private void HandleComboInput()
        {
            // Only process combo input during combo window
            if (!_comboController.IsInComboWindow()) return;

            // Check for new attack input
            if (entity.WasAttackPressedThisFrame)
            {
                var inputType = entity.GetCurrentAttackInputType();

                if (_enableCombatLogs) Debug.Log($"[AttackState] Combo input detected: {inputType}");

                // Try to continue combo
                if (_comboController.TryExecuteComboAttack(inputType))
                {
                    entity.ConsumeAttackInput();
                    TriggerNextComboAttack();
                }
                else
                {
                    if (_enableCombatLogs) Debug.Log("[AttackState] Invalid combo input - ending combo");

                    // Invalid combo input ends the combo
                    _comboController.ResetCombo();
                }
            }
        }

        /// <summary>
        /// Handle transition to next combo attack
        /// </summary>
        private void TriggerNextComboAttack()
        {
            // Update attack data for new combo attack
            _currentAttackData = _comboController.GetCurrentExecutingAttack();

            if (_currentAttackData == null)
            {
                Debug.LogError("[AttackState] Failed to get next combo attack data!");
                ForceExitToIdle();
                return;
            }

            // Reset for new attack
            ResetAttackFlags();

            // Update animation
            SetAnimationName(_currentAttackData.AnimationName);
            PlayCustomAnimation(_currentAttackData.AnimationName);

            if (_enableCombatLogs) Debug.Log($"[AttackState] Combo continued to: {_currentAttackData.AttackName}");
        }

        // ==================== VISUAL EFFECTS ====================

        /// <summary>
        /// Update visual effects based on current attack phase
        /// </summary>
        private void UpdateVisualEffects()
        {
            if (_comboController == null) return;

            // Visual effects based on attack phase
            switch (_comboController.AttackPhase)
            {
                case AttackPhase.Startup:
                    // TODO: Wind-up effects
                    break;

                case AttackPhase.Active:
                    // TODO: Active hit effects
                    break;

                case AttackPhase.Recovery:
                    // TODO: Recovery effects
                    break;

                case AttackPhase.ComboWindow:
                    // TODO: Combo input indicator
                    break;
            }
        }

        // ==================== UTILITY METHODS ====================

        /// <summary>
        /// Validate required components
        /// </summary>
        private bool ValidateComponents()
        {
            if (_comboController == null)
            {
                Debug.LogError("[AttackState] ComboController is null!");
                return false;
            }

            if (entity == null)
            {
                Debug.LogError("[AttackState] PlayerController entity is null!");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Reset attack-specific flags
        /// </summary>
        private void ResetAttackFlags()
        {
            _lastHitboxCheckFrame = -1;
            _hasHitThisAttack     = false;
        }

        /// <summary>
        /// Force exit to idle state for error recovery
        /// </summary>
        private void ForceExitToIdle()
        {
            if (_enableCombatLogs) Debug.LogWarning("[AttackState] Force exiting to idle due to error");

            _comboController?.ResetCombo();
            // FSM will handle the transition to idle based on conditions
        }

        /// <summary>
        /// Draw hitbox debug visualization
        /// </summary>
        private void DrawHitboxDebug(Vector2 center, Vector2 size)
        {
            Vector2 halfSize    = size * 0.5f;
            Vector2 topLeft     = new Vector2(center.x - halfSize.x, center.y + halfSize.y);
            Vector2 topRight    = new Vector2(center.x + halfSize.x, center.y + halfSize.y);
            Vector2 bottomLeft  = new Vector2(center.x - halfSize.x, center.y - halfSize.y);
            Vector2 bottomRight = new Vector2(center.x + halfSize.x, center.y - halfSize.y);

            Debug.DrawLine(topLeft, topRight, Color.red, 0.1f);
            Debug.DrawLine(topRight, bottomRight, Color.red, 0.1f);
            Debug.DrawLine(bottomRight, bottomLeft, Color.red, 0.1f);
            Debug.DrawLine(bottomLeft, topLeft, Color.red, 0.1f);
        }

        // ==================== STATE QUERIES ====================

        /// <summary>
        /// Check if attack state should exit (for FSM transitions)
        /// </summary>
        public bool ShouldExitAttackState()
        {
            return _comboController?.IsAttackComplete() ?? true;
        }

        /// <summary>
        /// Get current attack data for external access
        /// </summary>
        public AttackDataSO GetCurrentAttackData()
        {
            return _currentAttackData;
        }

        /// <summary>
        /// Check if currently in combo window (for FSM transitions)
        /// </summary>
        public bool IsInComboWindow()
        {
            return _comboController?.IsInComboWindow() ?? false;
        }

        /// <summary>
        /// Check if can continue combo (for FSM transitions)
        /// </summary>
        public bool CanContinueCombo()
        {
            return _comboController?.ShouldContinueCombo() ?? false;
        }
    }
}