using UnityEngine;
using Fusion;
using _GAME.Scripts.UI;

namespace _GAME.Scripts.Combat
{
    using _GAME.Scripts.Core;
    using _GAME.Scripts.FSM.ConcreteState;

    /// <summary>
    /// Simple Health System + HUD Integration - Game Jam Version
    /// </summary>
    public class HealthSystem : NetworkBehaviour
    {
        [Header("Settings")] [SerializeField] private float maxHealth   = 100f;
        [SerializeField]                      private bool  enableDebug = true;

        // Network synced health values
        [Networked] public float Player1Health { get; set; }
        [Networked] public float Player2Health { get; set; }

        private GameHUD gameHUD;

        public override void Spawned()
        {
            gameHUD = FindObjectOfType<GameHUD>();

            if (HasStateAuthority)
            {
                Player1Health = maxHealth;
                Player2Health = maxHealth;
            }

            // Start game
            if (HasStateAuthority && gameHUD != null)
            {
                gameHUD.StartTimer(99f);
                gameHUD.SetRound(1);
            }
        }

        public override void Render()
        {
            // Update HUD every frame
            if (gameHUD != null)
            {
                gameHUD.SetPlayerHealth(0, Player1Health);
                gameHUD.SetPlayerHealth(1, Player2Health);
            }
        }

        /// <summary>
        /// Apply damage to player - called from DamageReceiver
        /// </summary>
        public void TakeDamage(PlayerController damagedPlayer, float damage)
        {
            if (!HasStateAuthority) return;

            int playerIndex = GetPlayerIndex(damagedPlayer);
            if (playerIndex < 0) return;

            if (playerIndex == 0)
            {
                Player1Health = Mathf.Max(0f, Player1Health - damage);
                if (enableDebug) Debug.Log($"Player 1: {Player1Health} HP remaining");

                // CHECK FOR DEATH - trigger DieState
                if (Player1Health <= 0f)
                {
                    TriggerDieState(damagedPlayer);
                }
            }
            else if (playerIndex == 1)
            {
                Player2Health = Mathf.Max(0f, Player2Health - damage);
                if (enableDebug) Debug.Log($"Player 2: {Player2Health} HP remaining");

                // CHECK FOR DEATH - trigger DieState
                if (Player2Health <= 0f)
                {
                    TriggerDieState(damagedPlayer);
                }
            }
        }

        /// <summary>
        /// Trigger DieState for player - Game Jam Quick Implementation
        /// </summary>
        /// <summary>
        /// Trigger DieState for player - Game Jam Quick Implementation
        /// </summary>
        private void TriggerDieState(PlayerController player)
        {
            if (!HasStateAuthority) return;

            // Force player into DieState
            var stateMachine = player.GetComponent<NetworkedStateMachine>();
            if (stateMachine != null)
            {
                // Find DieState and force transition
                var dieState = GetDieStateFromPlayer(player);
                if (dieState != null)
                {
                    stateMachine.ChangeState(dieState);
                }
            }

            if (enableDebug) Debug.Log($"[HealthSystem] Player {player.name} triggered DieState");
        }

        /// <summary>
        /// Get DieState from player - Game Jam Helper
        /// </summary>
        private DieState GetDieStateFromPlayer(PlayerController player)
        {
            // Access private field via reflection (quick Game Jam solution)
            var field = typeof(PlayerController).GetField("_dieState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return field?.GetValue(player) as DieState;
        }

        private void OnPlayerDefeated(int playerIndex)
        {
            Debug.Log($"Player {playerIndex + 1} defeated!");

            if (gameHUD != null) gameHUD.StopTimer();

            // Game over logic here
        }

        /// <summary>
        /// Determine player index - simple method
        /// </summary>
        private int GetPlayerIndex(PlayerController player)
        {
            if (player == null) return -1;

            // Method 1: By InputAuthority PlayerId
            if (player.Object.InputAuthority.PlayerId == 1) return 0;
            if (player.Object.InputAuthority.PlayerId == 2) return 1;

            // Method 2: By GameObject name (fallback)
            if (player.name.Contains("Player1") || player.name.Contains("P1")) return 0;
            if (player.name.Contains("Player2") || player.name.Contains("P2")) return 1;

            // Method 3: By order in scene (last resort)
            var allPlayers = FindObjectsOfType<PlayerController>();
            for (int i = 0; i < allPlayers.Length && i < 2; i++)
            {
                if (allPlayers[i] == player) return i;
            }

            return -1;
        }

        // Public API
        public float GetPlayerHealth(int playerIndex)
        {
            return playerIndex == 0 ? Player1Health : Player2Health;
        }

        public bool IsPlayerAlive(int playerIndex)
        {
            return GetPlayerHealth(playerIndex) > 0f;
        }
    }
}