using UnityEngine;
using Fusion;
using _GAME.Scripts.Core;
using _GAME.Scripts.FSM.ConcreteState;

namespace _GAME.Scripts.Combat
{
    /// <summary>
    /// Handles damage reception and triggers hit state transitions
    /// Network-synchronized component for consistent hit processing
    /// Single Responsibility: Convert hit detection into game state changes
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class DamageReceiver : NetworkBehaviour
    {
        [Header("Defense Properties")]
        [SerializeField] private float damageReduction = 0f;
        [Range(0f, 1f)] [SerializeField] private float knockbackResistance = 0f;

        [Header("Invincibility")]
        [SerializeField] private float invincibilityFramesAfterHit = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool enableDamageDebugLogs = false;

        [Networked] public int     NetworkedHitStartTick      { get; set; }
        [Networked] public int     NetworkedHitstunFrames     { get; set; }
        [Networked] public Vector2 NetworkedKnockbackVelocity { get; set; }
        [Networked] public bool    IsInHitState               { get; set; }
        [Header("Dash Invincibility")]
        [SerializeField] private bool enableDashInvincibility = true;

        // Network property for dash invincibility
        [Networked] public bool IsDashInvincible { get; set; }

        /// <summary>
        /// Set dash invincibility state (called by DashState)
        /// </summary>
        public void SetDashInvincibility(bool isInvincible)
        {
            if (!HasStateAuthority) return;

            IsDashInvincible = isInvincible;

            if (enableDamageDebugLogs)
            {
                Debug.Log($"[DamageReceiver] Dash invincibility: {isInvincible}");
            }
        }
        /// <summary>
        /// Check if player is invincible (includes dash invincibility)
        /// Modify existing TakeDamage() method to use this
        /// </summary>
        private bool IsPlayerInvincible()
        {
            return IsInvincible || (enableDashInvincibility && IsDashInvincible);
        }


        // Network-synchronized properties
        [Networked] private float LastHitTime { get; set; }
        [Networked] private bool IsInvincible { get; set; }

        // Component references
        private PlayerController _playerController;
        private NetworkedStateMachine _stateMachine;
        private HitState _hitState;

        // Events for damage system integration
        public System.Action<float, PlayerController> OnDamageReceived;
        public System.Action<Vector2> OnKnockbackApplied;

        public void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _stateMachine = GetComponent<NetworkedStateMachine>();

            if (_playerController == null)
            {
                Debug.LogError("[DamageReceiver] PlayerController component required!");
                enabled = false;
            }
        }

        public override void FixedUpdateNetwork()
        {
            // Update invincibility status
            if (IsInvincible && HasStateAuthority)
            {
                float timeSinceHit = Runner.SimulationTime - LastHitTime;
                if (timeSinceHit >= invincibilityFramesAfterHit)
                {
                    IsInvincible = false;

                    if (enableDamageDebugLogs) Debug.Log("[DamageReceiver] Invincibility expired");
                }
            }
        }

        // ==================== DAMAGE RECEPTION ====================

        /// <summary>
        /// Main method to receive damage from attacks
        /// Called by AttackState when hit detection occurs
        /// Network-safe - only processes on state authority
        /// </summary>
        public bool TakeDamage(float rawDamage, Vector2 rawKnockback, int hitstunFrames, PlayerController attacker, string hitboxName = "Main")
        {
            // Only process on state authority
            if (!HasStateAuthority)
            {
                Debug.LogWarning("[DamageReceiver] TakeDamage called on non-authority client");
                return false;
            }

            // Check invincibility (includes dash invincibility)
            if (IsPlayerInvincible())
            {
                if (enableDamageDebugLogs)
                {
                    string reason = IsDashInvincible ? "dash invincible" : "regular invincible";
                    Debug.Log($"[DamageReceiver] Damage blocked - player is {reason}");
                }
                return false;
            }

            // Validate damage parameters
            if (rawDamage < 0 || hitstunFrames < 0)
            {
                Debug.LogError($"[DamageReceiver] Invalid damage parameters - Damage: {rawDamage}, Hitstun: {hitstunFrames}");
                return false;
            }

            // Calculate final damage and knockback
            float finalDamage = CalculateFinalDamage(rawDamage);
            Vector2 finalKnockback = CalculateFinalKnockback(rawKnockback, attacker);

            // Apply damage and trigger hit state
            bool hitSuccessful = ProcessHit(finalDamage, finalKnockback, hitstunFrames, attacker, hitboxName);

            if (hitSuccessful)
            {
                // Set invincibility
                LastHitTime = Runner.SimulationTime;
                IsInvincible = true;

                if (enableDamageDebugLogs)
                {
                    Debug.Log($"[DamageReceiver] Hit processed - Damage: {finalDamage}, Knockback: {finalKnockback}, Hitstun: {hitstunFrames}");
                }
            }

            return hitSuccessful;
        }

        /// <summary>
        /// Process the hit and transition to hit state
        /// </summary>
        private bool ProcessHit(float damage, Vector2 knockback, int hitstunFrames, PlayerController attacker, string hitboxName)
        {
            // Ensure we have a hit state
            if (_hitState == null)
            {
                _hitState = GetHitState();
                if (_hitState == null)
                {
                    Debug.LogError("[DamageReceiver] No HitState found - cannot process hit!");
                    return false;
                }
            }

            // Initialize hit state with damage data
            _hitState.InitializeHit(damage, knockback, hitstunFrames, attacker);

            // Transition to hit state
            _stateMachine.ChangeState(_hitState);

            // Fire events for other systems
            OnDamageReceived?.Invoke(damage, attacker);
            OnKnockbackApplied?.Invoke(knockback);

            // Interrupt any current attacks
            InterruptCurrentActions();
            var hudIntegrator = FindObjectOfType<HUDIntegrator>();
            if (hudIntegrator != null)
            {
                hudIntegrator.OnDamageReceived(this._playerController, damage);
            }
            return true;
        }

        /// <summary>
        /// Calculate final damage after reductions
        /// </summary>
        private float CalculateFinalDamage(float rawDamage)
        {
            float finalDamage = rawDamage - damageReduction;
            return Mathf.Max(0f, finalDamage); // Ensure non-negative damage
        }

        /// <summary>
        /// Calculate final knockback with resistance and facing direction
        /// </summary>
        private Vector2 CalculateFinalKnockback(Vector2 rawKnockback, PlayerController attacker)
        {
            Vector2 finalKnockback = rawKnockback;

            // Apply knockback resistance
            finalKnockback *= (1f - knockbackResistance);

            // Determine knockback direction based on attacker position
            if (attacker != null)
            {
                Vector2 directionFromAttacker = (transform.position - attacker.transform.position).normalized;

                // Flip horizontal knockback based on relative positions
                if (directionFromAttacker.x < 0 && finalKnockback.x > 0)
                {
                    finalKnockback.x = -finalKnockback.x;
                }
                else if (directionFromAttacker.x > 0 && finalKnockback.x < 0)
                {
                    finalKnockback.x = -finalKnockback.x;
                }
            }

            return finalKnockback;
        }

        /// <summary>
        /// Get or create hit state for this player
        /// </summary>
        private HitState GetHitState()
        {
            // Try to find existing hit state
            if (_stateMachine != null)
            {
                var currentState = _stateMachine.CurrentState as HitState;
                if (currentState != null) return currentState;
            }

            // Create new hit state if none exists
            return new HitState(_playerController, enableDamageDebugLogs);
        }

        /// <summary>
        /// Interrupt any actions that should be stopped when hit
        /// </summary>
        private void InterruptCurrentActions()
        {
            // Stop any ongoing attacks
            var comboController = GetComponent<ComboController>();
            comboController?.ResetCombo();

            // Cancel movement momentum (let knockback take over)
            var rigidbody = GetComponent<Rigidbody2D>();
            if (rigidbody != null)
            {
                rigidbody.velocity = new Vector2(rigidbody.velocity.x * 0.5f, rigidbody.velocity.y);
            }

            // TODO: Cancel any special abilities in progress
            // TODO: Reset dodge state if applicable
        }

        // ==================== UTILITY METHODS ====================

        /// <summary>
        /// Check if player can be hit right now
        /// </summary>
        public bool CanTakeDamage()
        {
            return !IsInvincible && HasStateAuthority;
        }

        /// <summary>
        /// Get remaining invincibility time
        /// </summary>
        public float GetRemainingInvincibilityTime()
        {
            if (!IsInvincible) return 0f;

            float elapsed = Runner.SimulationTime - LastHitTime;
            return Mathf.Max(0f, invincibilityFramesAfterHit - elapsed);
        }

        /// <summary>
        /// Force set invincibility state (for special abilities, etc.)
        /// </summary>
        public void SetInvincible(bool invincible, float duration = 0f)
        {
            if (!HasStateAuthority) return;

            IsInvincible = invincible;

            if (invincible && duration > 0f)
            {
                LastHitTime = Runner.SimulationTime;
                invincibilityFramesAfterHit = duration;
            }

            if (enableDamageDebugLogs)
            {
                Debug.Log($"[DamageReceiver] Invincibility set to {invincible} for {duration} seconds");
            }
        }

        // ==================== EDITOR DEBUGGING ====================

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            // Draw invincibility indicator
            if (IsInvincible)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 1.5f);

                UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
                    $"Invincible: {GetRemainingInvincibilityTime():F1}s");
            }
        }
#endif

        // ==================== NETWORK DEBUGGING ====================

        /// <summary>
        /// Debug method to test hit simulation (Editor only)
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void DebugSimulateHit(float damage = 10f, Vector2 knockback = default, int hitstun = 12)
        {
            if (!Application.isPlaying) return;

            if (knockback == default) knockback = new Vector2(5f, 2f);

            Debug.Log($"[DamageReceiver] Debug simulating hit - Damage: {damage}, Knockback: {knockback}, Hitstun: {hitstun}");

            TakeDamage(damage, knockback, hitstun, null, "Debug");
        }
    }
}