namespace _GAME.Scripts.FSM.ConcreteState
{
    using _GAME.Scripts.Core;
    using _GAME.Scripts.FSM;
    using _GAME.Scripts.Combat;
    using Fusion;
    using UnityEngine;

    /// <summary>
    /// Attack state - handles all combat logic and combo execution
    /// Integrates with ComboController for validation and timing
    /// Handles combo progression internally to avoid state transition overhead
    /// FIXED: Now includes the missing AttackPhase update logic!
    /// </summary>
    public class AttackState : NetworkedBaseState<PlayerController>
    {
        private ComboController _comboController;
        private AttackDataSO    _currentAttack;
        private int             _lastHitboxCheckFrame = -1;
        private bool            _hasTriggeredHitbox   = false;

        [Header("Combat Settings")] private bool _enableCombatLogs = true;

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
            if (!HasStateAuthority)
            {
                // Clients still need to enter the state for visual consistency
                base.EnterState();
                return;
            }

            // Server-side: Get current attack data and validate
            _currentAttack = _comboController?.GetCurrentAttack();
            if (_currentAttack == null)
            {
                if (_enableCombatLogs) Debug.LogError("[AttackState] No current attack data available!");

                // Exit to idle if no attack data
                ForceExitToIdle();
                return;
            }

            // Set animation name dynamically based on current attack
            // This allows each attack in combo to have different animations
            SetCurrentAnimation(_currentAttack.AnimationName);

            base.EnterState();

            // Initialize attack state
            ResetAttackState();

            if (_enableCombatLogs) Debug.Log($"[AttackState] Entered with attack: {_currentAttack.AttackName}");
        }

        public override void StateFixedUpdate()
        {
            if (!HasStateAuthority) return;

            // Validate we still have attack data
            if (_currentAttack == null)
            {
                ForceExitToIdle();
                return;
            }

            // REMOVED: UpdateAttackPhase() - Now handled by ComboController
            // ComboController is the single source of truth for attack phases

            // Check for hitbox activation during active frames
            HandleHitboxLogic();

            // Handle combo input for continuation
            HandleComboProgression();

            // Check if attack sequence is complete
            if (_comboController.IsAttackComplete())
            {
                // Attack finished, exit to appropriate movement state
                ExitToMovementState();
            }
        }

        public override void StateUpdate()
        {
            // Visual updates on all clients
            // Animation and VFX updates happen here

            // Update visual effects based on attack phase
            UpdateVisualEffects();
        }

        public override void ExitState()
        {
            base.ExitState();

            // Clean up attack state
            ResetAttackState();

            if (_enableCombatLogs) Debug.Log("[AttackState] Exited attack state");
        }

        /*/// <summary>
        /// Update attack phase based on elapsed time - THE MISSING LOGIC!
        /// This was originally in ComboController but needs to be here for state-driven approach
        /// </summary>
        private void UpdateAttackPhase()
        {
            if (_currentAttack == null) return;

            // Calculate elapsed frames since attack started
            int elapsedFrames = Runner.Tick - _comboController.AttackStartTick;

            // Update attack phase based on elapsed time
            if (elapsedFrames < _currentAttack.StartupFrames)
            {
                _comboController.AttackPhase = AttackPhase.Startup;
            }
            else if (elapsedFrames < _currentAttack.StartupFrames + _currentAttack.ActiveFrames)
            {
                _comboController.AttackPhase = AttackPhase.Active;
            }
            else if (elapsedFrames < _currentAttack.TotalFrames)
            {
                _comboController.AttackPhase = AttackPhase.Recovery;
            }
            else if (elapsedFrames < _currentAttack.ComboInputWindow)
            {
                _comboController.AttackPhase = AttackPhase.ComboWindow;

                // Start combo window timer if not already running
                if (_comboController.ComboWindowTimer.ExpiredOrNotRunning(Runner))
                {
                    _comboController.ComboWindowTimer = TickTimer.CreateFromTicks(Runner, _currentAttack.ComboWindowFrames);
                }
            }
            else
            {
                // Attack completely finished and combo window expired
                _comboController.AttackPhase = AttackPhase.None;
                // This will trigger IsAttackComplete() to return true, allowing FSM to exit AttackState
            }

            if (_enableCombatLogs && Runner.Tick % 10 == 0) // Log every 10 ticks to avoid spam
            {
                Debug.Log($"[AttackState] Phase: {_comboController.AttackPhase}, Elapsed: {elapsedFrames}/{_currentAttack.TotalFrames}");
            }
        }*/

        /// <summary>
        /// Handle hitbox logic during active frames
        /// </summary>
        private void HandleHitboxLogic()
        {
            if (_comboController.AttackPhase == AttackPhase.Active)
            {
                // Only check hitbox once per frame during active phase
                int currentFrame = Runner.Tick - _comboController.AttackStartTick;

                if (currentFrame != _lastHitboxCheckFrame)
                {
                    _lastHitboxCheckFrame = currentFrame;
                    PerformHitboxCheck();
                }
            }
        }

        /// <summary>
        /// Handle combo progression logic
        /// This replaces the state machine transition approach
        /// </summary>
        private void HandleComboProgression()
        {
            // Only process combo input during combo window
            if (_comboController.AttackPhase != AttackPhase.ComboWindow) return;

            // Check if player wants to continue combo
            if (entity.WasAttackPressedThisFrame && !_comboController.AttackInputConsumed)
            {
                var inputType = entity.GetCurrentAttackInputType();

                if (_enableCombatLogs) Debug.Log($"[AttackState] Combo input detected: {inputType}");

                // Validate if we can continue the combo
                if (_comboController.CanPerformAttack(inputType))
                {
                    // Execute next attack in combo
                    _comboController.ExecuteAttack(inputType);
                    entity.ConsumeAttackInput();

                    // Update to next attack data
                    TriggerNextComboAttack();
                }
                else
                {
                    if (_enableCombatLogs) Debug.Log("[AttackState] Combo input rejected - resetting combo");

                    // Invalid combo input, reset
                    _comboController.ResetCombo();
                    ExitToMovementState();
                }
            }
        }

        /// <summary>
        /// Trigger next attack in combo sequence
        /// Updates attack data and resets state without exiting/entering
        /// </summary>
        private void TriggerNextComboAttack()
        {
            // Get the next attack data (ComboController already incremented index)
            _currentAttack = _comboController.GetPreviousAttack(); // The one we just started

            if (_currentAttack == null)
            {
                if (_enableCombatLogs) Debug.LogError("[AttackState] Failed to get next attack data!");
                ExitToMovementState();
                return;
            }

            // Reset attack state for new attack
            ResetAttackState();

            // REMOVED: _comboController.AttackStartTick = Runner.Tick;
            // ComboController handles its own timing now

            // Update animation for new attack
            SetCurrentAnimation(_currentAttack.AnimationName);
            PlayCustomAnimation(_currentAttack.AnimationName);

            if (_enableCombatLogs) Debug.Log($"[AttackState] Triggered next combo attack: {_currentAttack.AttackName}");
        }

        /// <summary>
        /// Perform hitbox collision detection
        /// This is where actual combat hits are processed
        /// </summary>
        private void PerformHitboxCheck()
        {
            if (_currentAttack == null || _hasTriggeredHitbox) return;

            // Calculate hitbox position relative to player
            Vector2 hitboxCenter = (Vector2)entity.transform.position + GetAdjustedHitboxOffset();
            Vector2 hitboxSize   = _currentAttack.HitboxSize;

            // Perform overlap detection
            var hitColliders = Physics2D.OverlapBoxAll(
                hitboxCenter,
                hitboxSize,
                0f,
                _currentAttack.HitLayers
            );

            foreach (var collider in hitColliders)
            {
                // Skip self
                if (collider.transform == entity.transform) continue;

                // Check if target is valid for hitting
                var targetPlayer = collider.GetComponent<PlayerController>();
                if (targetPlayer != null && targetPlayer != entity)
                {
                    ProcessHit(targetPlayer);
                    _hasTriggeredHitbox = true; // Prevent multiple hits per attack
                    break;                      // Only hit one target per attack
                }
            }

            // Debug visualization in Scene view
            if (_enableCombatLogs && Application.isEditor)
            {
                // Draw hitbox outline for debugging
                DrawHitboxDebug(hitboxCenter, hitboxSize);
            }
        }

        /// <summary>
        /// Process successful hit on target
        /// </summary>
        private void ProcessHit(PlayerController target)
        {
            if (_currentAttack == null || target == null) return;

            // Calculate damage (with combo scaling if applicable)
            float damage = _comboController.GetComboDefinition()?.GetScaledDamage(_comboController.CurrentComboIndex - 1)
                ?? _currentAttack.Damage;

            // Calculate knockback
            Vector2 knockbackDirection = entity.IsFacingRight ? Vector2.right : Vector2.left;
            Vector2 knockbackForce = new Vector2(
                knockbackDirection.x * _currentAttack.KnockbackForce.x,
                _currentAttack.KnockbackForce.y
            );

            // Add energy for successful hit
            _comboController.AddEnergy(_currentAttack.EnergyGain);

            // TODO: Apply damage and knockback to target
            // target.TakeDamage(damage);
            // target.ApplyKnockback(knockbackForce, _currentAttack.HitstunFrames);

            // CHANGED: Use new hit confirmation RPC instead of attack execution RPC
            _comboController.RPC_AttackHit(
                (byte)(_comboController.CurrentComboIndex - 1),
                target.transform.position,
                target.Object.InputAuthority
            );

            if (_enableCombatLogs) Debug.Log($"[AttackState] Hit {target.name} for {damage} damage with {_currentAttack.AttackName}");
        }

        /// <summary>
        /// Get hitbox offset adjusted for player facing direction
        /// </summary>
        private Vector2 GetAdjustedHitboxOffset()
        {
            Vector2 offset = _currentAttack.HitboxOffset;

            // Flip X offset based on facing direction
            if (!entity.IsFacingRight)
            {
                offset.x = -offset.x;
            }

            return offset;
        }

        /// <summary>
        /// Update visual effects based on current attack phase
        /// </summary>
        private void UpdateVisualEffects()
        {
            if (_comboController == null) return;

            // Update visual effects based on attack phase
            switch (_comboController.AttackPhase)
            {
                case AttackPhase.Startup:
                    // TODO: Show wind-up effects
                    break;

                case AttackPhase.Active:
                    // TODO: Show active hit effects, screen shake, etc.
                    break;

                case AttackPhase.Recovery:
                    // TODO: Show recovery effects
                    break;

                case AttackPhase.ComboWindow:
                    // TODO: Show combo input indicator
                    break;
            }
        }

        /// <summary>
        /// Draw hitbox debug visualization using Debug.DrawLine
        /// </summary>
        private void DrawHitboxDebug(Vector2 center, Vector2 size)
        {
            // Calculate corners of the hitbox
            Vector2 halfSize    = size * 0.5f;
            Vector2 topLeft     = new Vector2(center.x - halfSize.x, center.y + halfSize.y);
            Vector2 topRight    = new Vector2(center.x + halfSize.x, center.y + halfSize.y);
            Vector2 bottomLeft  = new Vector2(center.x - halfSize.x, center.y - halfSize.y);
            Vector2 bottomRight = new Vector2(center.x + halfSize.x, center.y - halfSize.y);

            // Draw the rectangle outline
            Debug.DrawLine(topLeft, topRight, Color.red, 0.1f);       // Top edge
            Debug.DrawLine(topRight, bottomRight, Color.red, 0.1f);   // Right edge
            Debug.DrawLine(bottomRight, bottomLeft, Color.red, 0.1f); // Bottom edge
            Debug.DrawLine(bottomLeft, topLeft, Color.red, 0.1f);     // Left edge
        }

        /// <summary>
        /// Reset attack-specific state variables
        /// </summary>
        private void ResetAttackState()
        {
            _lastHitboxCheckFrame = -1;
            _hasTriggeredHitbox   = false;
        }

        /// <summary>
        /// Set the current animation name for this attack
        /// </summary>
        private void SetCurrentAnimation(string animationName)
        {
            /*// Update the base class animation name
            // This is a bit hacky but necessary for dynamic animation names
            var field = typeof(NetworkedBaseState<PlayerController>).GetField("animationName",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(this, animationName);*/
            base.SetAnimationName(animationName);
        }

        /// <summary>
        /// Force exit to idle state (error recovery)
        /// </summary>
        private void ForceExitToIdle()
        {
            if (_enableCombatLogs) Debug.LogWarning("[AttackState] Force exiting to idle due to error");

            _comboController?.ResetCombo();
            // The state machine will handle the transition based on current conditions
        }

        /// <summary>
        /// Exit to appropriate movement state based on input
        /// </summary>
        private void ExitToMovementState()
        {
            // The state machine will automatically transition based on movement input
            // through the existing transitions in PlayerController.InitializeStateMachine()

            if (_enableCombatLogs) Debug.Log("[AttackState] Attack sequence complete, transitioning to movement state");
        }

        /// <summary>
        /// Check if we should exit attack state
        /// Used by state machine transitions
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
            return _currentAttack;
        }

        /// <summary>
        /// Enable/disable combat logging
        /// </summary>
        public void SetCombatLogging(bool enabled)
        {
            _enableCombatLogs = enabled;
        }
    }
}