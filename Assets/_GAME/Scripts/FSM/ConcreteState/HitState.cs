using UnityEngine;
using Fusion;
using _GAME.Scripts.Core;
using _GAME.Scripts.FSM;

namespace _GAME.Scripts.FSM.ConcreteState
{
    using _GAME.Scripts.Combat;

    /// <summary>
    /// Hit State - handles character when being hit by attacks
    /// Manages hitstun timing, knockback physics, and network synchronization
    /// Follows SOLID principles and integrates with existing FSM architecture
    /// </summary>
    public class HitState : NetworkedBaseState<PlayerController>
    {
        [Header("Debug")]
        private readonly bool _enableHitLogs;

        /*// Network-synchronized hit data
        [Networked] private int HitStartTick { get; set; }
        [Networked] private int HitstunFrames { get; set; }
        [Networked] private Vector2 KnockbackVelocity { get; set; }*/

        private DamageReceiver _damageReceiver;

        // Local state tracking
        private Rigidbody2D _rigidbody;
        private bool _hasAppliedKnockback;
        private Vector2 _originalKnockback;

        // Constants for network-safe physics
        private const float KNOCKBACK_DECAY_RATE = 0.85f;
        private const float MIN_KNOCKBACK_THRESHOLD = 0.1f;

        public HitState(PlayerController controller, bool enableDebugLogs = false) : base(controller, "Hit")
        {
            _enableHitLogs = enableDebugLogs;
            _rigidbody = controller.GetComponent<Rigidbody2D>();
            this._damageReceiver = controller.GetComponent<DamageReceiver>();

            if (_rigidbody == null)
            {
                Debug.LogError("[HitState] Rigidbody2D component required for knockback physics!");
            }
        }

        public override void EnterState()
        {
            base.EnterState();

            if (_enableHitLogs) Debug.Log("[HitState] Entering hit state - character is stunned");

            // Initialize hit state (only on authority)
            if (HasStateAuthority)
            {
                _damageReceiver.NetworkedHitStartTick         = Runner.Tick;
                _hasAppliedKnockback = false;

                // Disable player input during hitstun
                entity.SetInputEnabled(false);

                // Stop any ongoing attacks/combos
                var comboController = entity.GetComponent<ComboController>();
                comboController?.ResetCombo();
            }

            // Apply visual effects on ALL clients (not just authority)
            ApplyHitVisualEffects();
        }

        public override void StateFixedUpdate()
        {
            if (!HasStateAuthority) return;

            // Apply knockback physics
            HandleKnockbackPhysics();

            // Check if hitstun should end
            if (IsHitstunComplete())
            {
                if (_enableHitLogs) Debug.Log("[HitState] Hitstun complete - ready to exit");

                // FSM will handle transition based on player input/state
            }
        }

        public override void StateUpdate()
        {
            // Visual updates for all clients
            UpdateHitVisuals();
        }

        public override void ExitState()
        {
            if (_enableHitLogs) Debug.Log("[HitState] Exiting hit state - player can act again");

            // Re-enable player input
            entity.SetInputEnabled(true);

            // Reset hit-related flags
            _hasAppliedKnockback = false;
            _damageReceiver.NetworkedKnockbackVelocity = Vector2.zero;

            // Clear visual effects
            ClearHitVisualEffects();

            base.ExitState();
        }

        // ==================== HIT INITIALIZATION ====================

        /// <summary>
        /// Initialize hit state with attack data (called by DamageReceiver)
        /// Network-safe - only call on state authority
        /// </summary>
        public void InitializeHit(float damage, Vector2 knockback, int hitstunFrames, PlayerController attacker)
        {
            if (!entity.HasStateAuthority) return;

            // Set networked data thông qua DamageReceiver
            _damageReceiver.NetworkedHitStartTick      = entity.Runner.Tick;
            _damageReceiver.NetworkedHitstunFrames     = hitstunFrames;
            _damageReceiver.NetworkedKnockbackVelocity = knockback;
            _damageReceiver.IsInHitState               = true;

            _originalKnockback = knockback;
            ProcessDamageAndEnergy(damage, attacker);
        }

        /// <summary>
        /// Process damage application and energy changes
        /// </summary>
        private void ProcessDamageAndEnergy(float damage, PlayerController attacker)
        {
            // Apply damage to health system
            /*var healthComponent = entity.GetComponent<HealthComponent>();
            healthComponent?.TakeDamage(damage, attacker);*/

            // Add energy for being hit (5 points per GDD)
            /*var energyComponent = entity.GetComponent<EnergyComponent>();
            energyComponent?.AddEnergy(5);*/

            // Give energy to attacker (10 points per successful hit)
            /*if (attacker != null)
            {
                var attackerEnergy = attacker.GetComponent<EnergyComponent>();
                attackerEnergy?.AddEnergy(10);
            }*/
        }

        // ==================== PHYSICS AND TIMING ====================

        /// <summary>
        /// Handle knockback physics during hitstun
        /// </summary>
        private void HandleKnockbackPhysics()
        {
            if (_rigidbody == null) return;

            // Apply initial knockback force
            if (!_hasAppliedKnockback && this._damageReceiver.NetworkedKnockbackVelocity.magnitude > MIN_KNOCKBACK_THRESHOLD)
            {
                // Face away from knockback direction for visual feedback
                if (_damageReceiver.NetworkedKnockbackVelocity.x != 0)
                {
                    /*
                    entity.SetFacingDirection(KnockbackVelocity.x < 0);
                */
                }

                // Apply knockback velocity
                _rigidbody.velocity  = _damageReceiver.NetworkedKnockbackVelocity;
                _hasAppliedKnockback = true;

                if (_enableHitLogs) Debug.Log($"[HitState] Applied knockback velocity: {_damageReceiver.NetworkedKnockbackVelocity}");
            }

            // Decay knockback over time for natural-feeling physics
            if (_hasAppliedKnockback)
            {
                _damageReceiver.NetworkedKnockbackVelocity *= KNOCKBACK_DECAY_RATE;

                if (_damageReceiver.NetworkedKnockbackVelocity.magnitude < MIN_KNOCKBACK_THRESHOLD)
                {
                    _damageReceiver.NetworkedKnockbackVelocity = Vector2.zero;
                }
            }
        }

        /// <summary>
        /// Check if hitstun duration is complete
        /// </summary>
        public bool IsHitstunComplete()
        {
            if (this._damageReceiver.NetworkedHitStartTick == 0) return true; // Not properly initialized

            int elapsedFrames = Runner.Tick - this._damageReceiver.NetworkedHitStartTick;
            return elapsedFrames >= this._damageReceiver.NetworkedHitstunFrames;
        }

        /// <summary>
        /// Get remaining hitstun frames
        /// </summary>
        public int GetRemainingHitstunFrames()
        {
            if (this._damageReceiver.NetworkedHitStartTick == 0) return 0;

            int elapsedFrames = Runner.Tick - this._damageReceiver.NetworkedHitStartTick;
            return Mathf.Max(0, _damageReceiver.NetworkedHitstunFrames - elapsedFrames);
        }

        // ==================== VISUAL EFFECTS ====================

        /// <summary>
        /// Apply visual effects when hit
        /// </summary>
        private void ApplyHitVisualEffects()
        {
            // Flash/blink effect for hit feedback
            var spriteRenderer = entity.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                // Simple color tint for hit effect
                spriteRenderer.color = Color.red;
            }

            // Particle effects for hit impact
            // TODO: Trigger hit particle system
            // TODO: Screen shake for impactful hits
            // TODO: Sound effect for hit feedback
        }

        /// <summary>
        /// Update visual effects during hitstun
        /// </summary>
        private void UpdateHitVisuals()
        {
            // Blinking effect during hitstun
            var spriteRenderer = entity.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                float blinkRate = 0.1f;
                bool shouldBlink = (Time.time % blinkRate) < (blinkRate * 0.5f);
                spriteRenderer.color = shouldBlink ? Color.white : Color.red;
            }
        }

        /// <summary>
        /// Clear visual effects when exiting hit state
        /// </summary>
        private void ClearHitVisualEffects()
        {
            var spriteRenderer = entity.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }

            // TODO: Stop particle effects
            // TODO: Stop screen shake
        }

        // ==================== STATE QUERIES ====================

        /// <summary>
        /// Check if player can exit hit state (for FSM transitions)
        /// </summary>
        public bool CanExitHitState()
        {
            return IsHitstunComplete();
        }

        /// <summary>
        /// Check if hit state should transition to specific state
        /// </summary>
        public bool ShouldTransitionToGroundState()
        {
            return IsHitstunComplete() && entity.IsGrounded;
        }

        /// <summary>
        /// Check if hit state should transition to air state
        /// </summary>
        public bool ShouldTransitionToAirState()
        {
            return IsHitstunComplete() && !entity.IsGrounded;
        }

        /// <summary>
        /// Get current hit data for external systems
        /// </summary>
        public (int remainingFrames, Vector2 knockback, bool isComplete) GetHitData()
        {
            return (GetRemainingHitstunFrames(), this._damageReceiver.NetworkedKnockbackVelocity, IsHitstunComplete());
        }

    }
}