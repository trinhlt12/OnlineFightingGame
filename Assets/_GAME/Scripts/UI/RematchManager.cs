using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;

namespace _GAME.Scripts.UI
{
    using _GAME.Scripts.Core;

    /// <summary>
    /// Game Jam Version - Simple Rematch Voting System
    /// Requires both players to vote for rematch before restarting
    /// </summary>
    public class RematchManager : NetworkBehaviour
    {
        [Header("Win Panel UI")]
        [SerializeField] private Button winPanelRematchButton;
        [SerializeField] private GameObject winPanelWaitingPanel;
        [SerializeField] private TextMeshProUGUI winPanelWaitingText;

        [Header("Lose Panel UI")]
        [SerializeField] private Button losePanelRematchButton;
        [SerializeField] private GameObject losePanelWaitingPanel;
        [SerializeField] private TextMeshProUGUI losePanelWaitingText;

        [Header("Other UI")]
        [SerializeField] private GameObject selectionUICanvas;

        [Header("Settings")]
        [SerializeField] private string waitingMessage = "Waiting for other player to confirm rematch...";

        [Header("Debug")]
        [SerializeField] private bool enableDebug = true;

        // Network Properties
        [Networked] public bool Player1WantsRematch { get; set; }
        [Networked] public bool Player2WantsRematch { get; set; }
        [Networked] public bool RematchInProgress { get; set; }

        // Cache
        private bool localPlayerVoted = false;

        public override void Spawned()
        {
            // Setup both buttons
            if (winPanelRematchButton != null)
            {
                winPanelRematchButton.onClick.AddListener(OnRematchButtonClicked);
            }

            if (losePanelRematchButton != null)
            {
                losePanelRematchButton.onClick.AddListener(OnRematchButtonClicked);
            }

            // Find selection UI if not assigned
            if (selectionUICanvas == null)
            {
                selectionUICanvas = GameObject.Find("SelectionUICanvas");
                if (selectionUICanvas == null)
                {
                    // Try different names
                    selectionUICanvas = GameObject.Find("CharacterSelectionCanvas");
                    selectionUICanvas = selectionUICanvas ?? GameObject.Find("SelectionCanvas");
                }
            }

            ResetRematchState();
        }

        public override void FixedUpdateNetwork()
        {
            // Only server checks for rematch completion
            if (!HasStateAuthority) return;

            // Check if both players voted
            if (Player1WantsRematch && Player2WantsRematch && !RematchInProgress)
            {
                StartRematch();
            }
        }

        public override void Render()
        {
            // Update UI based on network state
            UpdateRematchUI();
        }

        // ==================== PUBLIC API ====================

        /// <summary>
        /// Reset rematch voting state
        /// </summary>
        public void ResetRematchState()
        {
            if (HasStateAuthority)
            {
                Player1WantsRematch = false;
                Player2WantsRematch = false;
                RematchInProgress = false;
            }

            localPlayerVoted = false;

            // Hide both waiting panels
            if (winPanelWaitingPanel != null) winPanelWaitingPanel.SetActive(false);
            if (losePanelWaitingPanel != null) losePanelWaitingPanel.SetActive(false);

            // Enable both buttons
            if (winPanelRematchButton != null) winPanelRematchButton.interactable = true;
            if (losePanelRematchButton != null) losePanelRematchButton.interactable = true;
        }

        /// <summary>
        /// Show rematch options (called when game ends)
        /// </summary>
        public void ShowRematchOptions()
        {
            ResetRematchState();

            // Enable both buttons
            if (winPanelRematchButton != null)
            {
                winPanelRematchButton.gameObject.SetActive(true);
                winPanelRematchButton.interactable = true;
            }

            if (losePanelRematchButton != null)
            {
                losePanelRematchButton.gameObject.SetActive(true);
                losePanelRematchButton.interactable = true;
            }
        }

        /// <summary>
        /// Hide rematch options
        /// </summary>
        public void HideRematchOptions()
        {
            // Hide both buttons
            if (winPanelRematchButton != null)
            {
                winPanelRematchButton.gameObject.SetActive(false);
            }

            if (losePanelRematchButton != null)
            {
                losePanelRematchButton.gameObject.SetActive(false);
            }

            // Hide both waiting panels
            if (winPanelWaitingPanel != null)
            {
                winPanelWaitingPanel.SetActive(false);
            }

            if (losePanelWaitingPanel != null)
            {
                losePanelWaitingPanel.SetActive(false);
            }
        }

        // ==================== BUTTON EVENTS ====================

        /// <summary>
        /// Called when local player clicks rematch button
        /// </summary>
        public void OnRematchButtonClicked()
        {
            if (localPlayerVoted) return;

            int localPlayerIndex = GetLocalPlayerIndex();
            if (localPlayerIndex < 0) return;

            // Send vote to server
            RPC_VoteForRematch(localPlayerIndex);

            localPlayerVoted = true;

            if (enableDebug) Debug.Log($"[RematchManager] Player {localPlayerIndex + 1} voted for rematch");
        }

        // ==================== NETWORK RPCs ====================

        /// <summary>
        /// RPC to register rematch vote
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_VoteForRematch(int playerIndex)
        {
            if (playerIndex == 0)
            {
                Player1WantsRematch = true;
                if (enableDebug) Debug.Log("[RematchManager] Player 1 voted for rematch");
            }
            else if (playerIndex == 1)
            {
                Player2WantsRematch = true;
                if (enableDebug) Debug.Log("[RematchManager] Player 2 voted for rematch");
            }
        }

        /// <summary>
        /// RPC to start rematch process
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_StartRematch()
        {
            if (enableDebug) Debug.Log("[RematchManager] Starting rematch - returning to character selection");

            // Hide game UI
            HideRematchOptions();

            // Hide win/lose panels
            var winLoseUI = FindObjectOfType<WinLoseUI>();
            winLoseUI?.HideAllPanels();

            // Show selection UI
            if (selectionUICanvas != null)
            {
                selectionUICanvas.SetActive(true);
            }
            else
            {
                Debug.LogError("[RematchManager] Selection UI Canvas not found!");
            }
        }

        // ==================== PRIVATE METHODS ====================

        /// <summary>
        /// Start rematch process (server only)
        /// </summary>
        private void StartRematch()
        {
            RematchInProgress = true;

            // Reset game manager to waiting state
            var gameManager = FindObjectOfType<_GAME.Scripts.Core.GameManager>();
            if (gameManager != null)
            {
                gameManager.ResetToWaitingState();
            }

            // Notify all clients
            RPC_StartRematch();

            if (enableDebug) Debug.Log("[RematchManager] Both players voted - starting rematch");
        }

        /// <summary>
        /// Update UI based on voting state
        /// </summary>
        private void UpdateRematchUI()
        {
            // Show waiting panel if local player voted but not both
            bool shouldShowWaiting = localPlayerVoted && !(Player1WantsRematch && Player2WantsRematch);

            // Update win panel waiting state
            if (winPanelWaitingPanel != null)
            {
                winPanelWaitingPanel.SetActive(shouldShowWaiting);
            }

            if (winPanelWaitingText != null && shouldShowWaiting)
            {
                winPanelWaitingText.text = waitingMessage;
            }

            // Update lose panel waiting state
            if (losePanelWaitingPanel != null)
            {
                losePanelWaitingPanel.SetActive(shouldShowWaiting);
            }

            if (losePanelWaitingText != null && shouldShowWaiting)
            {
                losePanelWaitingText.text = waitingMessage;
            }

            // Disable both buttons if local player already voted
            if (winPanelRematchButton != null)
            {
                winPanelRematchButton.interactable = !localPlayerVoted;
            }

            if (losePanelRematchButton != null)
            {
                losePanelRematchButton.interactable = !localPlayerVoted;
            }
        }

        /// <summary>
        /// Get local player index
        /// </summary>
        private int GetLocalPlayerIndex()
        {
            if (Runner != null && Runner.LocalPlayer != null)
            {
                return Runner.LocalPlayer.PlayerId - 1;
            }

            // Fallback method
            var allPlayers = FindObjectsOfType<PlayerController>();
            for (int i = 0; i < allPlayers.Length; i++)
            {
                if (allPlayers[i].Object.HasInputAuthority)
                {
                    return i;
                }
            }

            return -1;
        }

        // ==================== EDITOR TESTING ====================

#if UNITY_EDITOR
        [Header("Testing")]
        [SerializeField] private bool enableTesting = false;

        private void Update()
        {
            if (!enableTesting || !Application.isPlaying) return;

            if (Input.GetKeyDown(KeyCode.M)) OnRematchButtonClicked();
            if (Input.GetKeyDown(KeyCode.N)) ResetRematchState();
        }
#endif
    }
}