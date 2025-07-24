using UnityEngine;
using Fusion;
using _GAME.Scripts.UI;

namespace _GAME.Scripts.Combat
{
    using _GAME.Scripts.Core;

    /// <summary>
    /// Quick integration between GameHUD and existing systems
    /// Game Jam Version - Simple and Fast
    /// </summary>
    public class HUDIntegrator : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private GameHUD gameHUD;
        [Networked] public float Player1Health { get; set; }
        [Networked] public float Player2Health { get; set; }

        private const float MAX_HEALTH = 100f;

        public override void Spawned()
        {
            // Find GameHUD if not assigned
            if (gameHUD == null)
                gameHUD = FindObjectOfType<GameHUD>();

            // Initialize health
            if (HasStateAuthority)
            {
                Player1Health = MAX_HEALTH;
                Player2Health = MAX_HEALTH;
            }

            // Start round
            if (HasStateAuthority && gameHUD != null)
            {
                gameHUD.StartTimer(99f);
                gameHUD.SetRound(1);
            }
        }

        public override void Render()
        {
            // Update HUD with current health values
            if (gameHUD != null)
            {
                gameHUD.SetPlayerHealth(0, Player1Health);
                gameHUD.SetPlayerHealth(1, Player2Health);
            }
        }

        /// <summary>
        /// Apply damage to player (call this from your damage system)
        /// </summary>
        public void DamagePlayer(int playerIndex, float damage)
        {
            if (!HasStateAuthority) return;

            if (playerIndex == 0)
            {
                Player1Health = Mathf.Max(0f, Player1Health - damage);
                Debug.Log($"Player 1 took {damage} damage. Health: {Player1Health}");

                if (Player1Health <= 0f)
                    OnPlayerKnockedOut(0);
            }
            else if (playerIndex == 1)
            {
                Player2Health = Mathf.Max(0f, Player2Health - damage);
                Debug.Log($"Player 2 took {damage} damage. Health: {Player2Health}");

                if (Player2Health <= 0f)
                    OnPlayerKnockedOut(1);
            }
        }

        /// <summary>
        /// Heal player (for testing or power-ups)
        /// </summary>
        public void HealPlayer(int playerIndex, float healAmount)
        {
            if (!HasStateAuthority) return;

            if (playerIndex == 0)
                Player1Health = Mathf.Min(MAX_HEALTH, Player1Health + healAmount);
            else if (playerIndex == 1)
                Player2Health = Mathf.Min(MAX_HEALTH, Player2Health + healAmount);
        }

        /// <summary>
        /// Reset health for new round
        /// </summary>
        public void ResetHealth()
        {
            if (!HasStateAuthority) return;

            Player1Health = MAX_HEALTH;
            Player2Health = MAX_HEALTH;
        }

        /// <summary>
        /// Start new round
        /// </summary>
        public void StartNewRound(int roundNumber)
        {
            if (!HasStateAuthority) return;

            ResetHealth();

            if (gameHUD != null)
            {
                gameHUD.SetRound(roundNumber);
                gameHUD.StartTimer(99f);
            }
        }

        private void OnPlayerKnockedOut(int playerIndex)
        {
            Debug.Log($"Player {playerIndex + 1} is knocked out!");

            if (gameHUD != null)
                gameHUD.StopTimer();

            // Notify game manager about knockout
            // GameManager will handle round end logic
        }

        // ==================== INTEGRATION WITH EXISTING DAMAGE SYSTEM ====================

        /// <summary>
        /// Connect this to your existing DamageReceiver
        /// Add this to DamageReceiver.ProcessDamageAndEnergy method:
        /// </summary>
        public void OnDamageReceived(PlayerController damagedPlayer, float damage)
        {
            if (!HasStateAuthority) return;

            // Determine player index
            int playerIndex = GetPlayerIndex(damagedPlayer);
            if (playerIndex >= 0)
                DamagePlayer(playerIndex, damage);
        }

        /// <summary>
        /// Helper to get player index from PlayerController
        /// Adjust this based on your player identification system
        /// </summary>
        private int GetPlayerIndex(PlayerController player)
        {
            // This is a simple example - adjust based on your system
            if (player == null) return -1;

            // Option 1: Based on NetworkObject InputAuthority
            var players = FindObjectsOfType<PlayerController>();
            for (int i = 0; i < players.Length && i < 2; i++)
            {
                if (players[i] == player)
                    return i;
            }

            return -1;
        }

        // ==================== PUBLIC API ====================

        public float GetPlayerHealth(int playerIndex)
        {
            return playerIndex == 0 ? Player1Health : Player2Health;
        }

        public bool IsPlayerAlive(int playerIndex)
        {
            return GetPlayerHealth(playerIndex) > 0f;
        }

        public GameHUD GetGameHUD() => gameHUD;
    }
}