namespace _GAME.Scripts.FSM.ConcreteState
{
    using _GAME.Scripts.Combat;
    using UnityEngine;
    using _GAME.Scripts.Core;

    /// <summary>
    /// Only triggered when health <= 0, NOT when knocked out of bounds
    /// </summary>
    public class DieState : NetworkedBaseState<PlayerController>
    {
        private GameManager gameManager;
        private bool hasNotifiedGameManager;

        public DieState(PlayerController entity) : base(entity, "Die")
        {
            gameManager = Object.FindObjectOfType<GameManager>();
        }

        public override void EnterState()
        {
            base.EnterState();

            if (EnableStateLogs) Debug.Log("[DieState] Player died - entering die state");

            // Only authority handles game logic
            if (entity.HasStateAuthority)
            {
                // Disable all player input and movement
                entity.SetInputEnabled(false);

                // Stop physics
                if (entity._rigidbody != null)
                {
                    entity._rigidbody.velocity = Vector2.zero;
                    entity._rigidbody.isKinematic = true;
                }

                // Reset combo system
                var comboController = entity.GetComponent<ComboController>();
                comboController?.ResetCombo();

                hasNotifiedGameManager = false;
            }
        }

        public override void StateFixedUpdate()
        {
            // Only server handles game logic
            if (!entity.HasStateAuthority) return;

            // Notify GameManager once (after a small delay to let animation start)
            if (!hasNotifiedGameManager)
            {
                hasNotifiedGameManager = true;
                gameManager?.EndRoundByDeath(entity);
                Debug.Log("[DieState] Notified GameManager of player death");

            }
        }

        public override void StateUpdate()
        {
            // Visual updates only - animation plays automatically
        }

        public override void ExitState()
        {
            if (EnableStateLogs) Debug.Log("[DieState] Exiting die state - round ended");

            // Re-enable physics and input for next round
            if (entity.HasStateAuthority)
            {
                if (entity._rigidbody != null)
                {
                    entity._rigidbody.isKinematic = false;
                }

                entity.SetInputEnabled(true);
            }

            base.ExitState();
        }

        // ==================== STATE VALIDATION ====================

        /// <summary>
        /// Should only enter die state when health is 0
        /// </summary>
        public static bool ShouldEnterDieState(PlayerController player)
        {
            var healthSystem = Object.FindObjectOfType<HealthSystem>();
            if (healthSystem == null) return false;

            // Find player index
            var players = Object.FindObjectsOfType<PlayerController>();
            int playerIndex = -1;

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == player)
                {
                    playerIndex = i;
                    break;
                }
            }

            if (playerIndex < 0) return false;

            // Check health
            float health = playerIndex == 0 ? healthSystem.Player1Health : healthSystem.Player2Health;
            return health <= 0f;
        }
    }
}