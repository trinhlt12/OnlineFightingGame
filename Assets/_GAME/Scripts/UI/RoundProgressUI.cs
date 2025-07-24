using UnityEngine;
using UnityEngine.UI;
using Fusion;

namespace _GAME.Scripts.UI
{
    /// <summary>
    /// Game Jam Version - Round Progress Display
    /// Shows 3 images for round wins, updates from left to right
    /// </summary>
    public class RoundProgressUI : NetworkBehaviour
    {
        [Header("Player 1 Progress")]
        [SerializeField] private Image[] player1RoundImages = new Image[3];

        [Header("Player 2 Progress")]
        [SerializeField] private Image[] player2RoundImages = new Image[3];

        [Header("Sprites")]
        [SerializeField] private Sprite defaultSprite;
        [SerializeField] private Sprite winSprite;

        [Header("Debug")]
        [SerializeField] private bool enableDebug = true;

        public override void Spawned()
        {
            ResetAllProgress();
        }

        // ==================== PUBLIC API ====================

        /// <summary>
        /// Set round result - specific round won by specific player
        /// </summary>
        public void SetRoundResult(int roundIndex, int winnerPlayerIndex)
        {
            // Validate inputs
            if (roundIndex < 0 || roundIndex >= 3)
            {
                if (enableDebug) Debug.LogError($"[RoundProgressUI] Invalid round index: {roundIndex} (should be 0-2)");
                return;
            }

            if (winnerPlayerIndex < 0 || winnerPlayerIndex > 1)
            {
                if (enableDebug) Debug.LogError($"[RoundProgressUI] Invalid winner index: {winnerPlayerIndex} (should be 0-1)");
                return;
            }

            // Set the specific round slot for the winner
            if (winnerPlayerIndex == 0) // Player 1 wins this round
            {
                if (player1RoundImages[roundIndex] != null)
                {
                    player1RoundImages[roundIndex].sprite = winSprite;
                }
                // Ensure Player 2's same slot is default
                if (player2RoundImages[roundIndex] != null)
                {
                    player2RoundImages[roundIndex].sprite = defaultSprite;
                }
            }
            else // Player 2 wins this round
            {
                if (player2RoundImages[roundIndex] != null)
                {
                    player2RoundImages[roundIndex].sprite = winSprite;
                }
                // Ensure Player 1's same slot is default
                if (player1RoundImages[roundIndex] != null)
                {
                    player1RoundImages[roundIndex].sprite = defaultSprite;
                }
            }

            if (enableDebug)
                Debug.Log($"[RoundProgressUI] Round {roundIndex + 1}: Player {winnerPlayerIndex + 1} wins");
        }

        /// <summary>
        /// Reset all progress to default
        /// </summary>
        public void ResetAllProgress()
        {
            // Reset all round slots to default
            for (int i = 0; i < 3; i++)
            {
                if (player1RoundImages[i] != null)
                    player1RoundImages[i].sprite = defaultSprite;

                if (player2RoundImages[i] != null)
                    player2RoundImages[i].sprite = defaultSprite;
            }

            if (enableDebug) Debug.Log("[RoundProgressUI] Reset all round progress");
        }

        // ==================== NETWORK SYNC ====================

        /// <summary>
        /// RPC to sync round result to all clients
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_SetRoundResult(int roundIndex, int winnerPlayerIndex)
        {
            SetRoundResult(roundIndex, winnerPlayerIndex);
        }

        /// <summary>
        /// RPC to reset all progress
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_ResetAllProgress()
        {
            ResetAllProgress();
        }

        // ==================== VALIDATION ====================

        private void OnValidate()
        {
            // Auto-find images if not assigned (Editor helper)
            if (player1RoundImages == null || player1RoundImages.Length != 3)
            {
                player1RoundImages = new Image[3];
            }

            if (player2RoundImages == null || player2RoundImages.Length != 3)
            {
                player2RoundImages = new Image[3];
            }
        }

        // ==================== EDITOR TESTING ====================

#if UNITY_EDITOR
        [Header("Testing")]
        [SerializeField] private bool enableTesting = false;

        private void Update()
        {
            if (!enableTesting || !Application.isPlaying) return;

            // Test round results
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetRoundResult(0, 0); // Round 1 - Player 1 wins
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetRoundResult(0, 1); // Round 1 - Player 2 wins
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetRoundResult(1, 0); // Round 2 - Player 1 wins
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetRoundResult(1, 1); // Round 2 - Player 2 wins
            if (Input.GetKeyDown(KeyCode.Alpha5)) SetRoundResult(2, 0); // Round 3 - Player 1 wins
            if (Input.GetKeyDown(KeyCode.Alpha6)) SetRoundResult(2, 1); // Round 3 - Player 2 wins

            if (Input.GetKeyDown(KeyCode.R)) ResetAllProgress();
        }
#endif
    }
}