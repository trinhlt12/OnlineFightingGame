using UnityEngine;
using Fusion;

namespace _GAME.Scripts.UI
{
    /// <summary>
    /// Game Jam Version - Simple Win/Lose UI
    /// Shows different panels based on match result
    /// </summary>
    public class WinLoseUI : NetworkBehaviour
    {
        [Header("UI Panels")] [SerializeField] private GameObject winPanel;
        [SerializeField]                       private GameObject losePanel;
        [SerializeField]                       private GameObject drawPanel; // Optional for draws

        [Header("Debug")] [SerializeField] private bool enableDebug = true;

        public override void Spawned()
        {
            // Hide all panels at start
            HideAllPanels();
        }

        // ==================== PUBLIC API ====================

        /// <summary>
        /// Show win panel for local player
        /// </summary>
        public void ShowWin()
        {
            HideAllPanels();

            if (winPanel != null)
            {
                winPanel.SetActive(true);
                if (enableDebug) Debug.Log("[WinLoseUI] Showing WIN panel");
            }
        }

        /// <summary>
        /// Show lose panel for local player
        /// </summary>
        public void ShowLose()
        {
            HideAllPanels();

            if (losePanel != null)
            {
                losePanel.SetActive(true);
                if (enableDebug) Debug.Log("[WinLoseUI] Showing LOSE panel");
            }
        }

        /// <summary>
        /// Show draw panel (if implemented)
        /// </summary>
        public void ShowDraw()
        {
            HideAllPanels();

            if (drawPanel != null)
            {
                drawPanel.SetActive(true);
                if (enableDebug) Debug.Log("[WinLoseUI] Showing DRAW panel");
            }
        }

        /// <summary>
        /// Hide all result panels
        /// </summary>
        public void HideAllPanels()
        {
            if (winPanel != null) winPanel.SetActive(false);
            if (losePanel != null) losePanel.SetActive(false);
            if (drawPanel != null) drawPanel.SetActive(false);

            if (enableDebug) Debug.Log("[WinLoseUI] All panels hidden");
        }

        // ==================== NETWORK SYNC ====================

        // Trong WinLoseUI.cs - thêm targeted RPCs
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_ShowWinForPlayer1()
        {
            int localIndex = GetLocalPlayerIndex();
            if (localIndex == 0)
                ShowWin();
            else
                ShowLose();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_ShowWinForPlayer2()
        {
            int localIndex = GetLocalPlayerIndex();
            if (localIndex == 1)
                ShowWin();
            else
                ShowLose();
        }

        /// <summary>
        /// RPC to show result for specific player
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_ShowResult(int localPlayerIndex, int winnerPlayerIndex)
        {
            // Determine what to show for this client
            if (winnerPlayerIndex < 0) // Draw
            {
                ShowDraw();
            }
            else if (localPlayerIndex == winnerPlayerIndex) // Local player wins
            {
                ShowWin();
            }
            else // Local player loses
            {
                ShowLose();
            }
        }

        /// <summary>
        /// RPC to hide all panels
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_HideAllPanels()
        {
            HideAllPanels();
        }

        // ==================== HELPER METHODS ====================

        /// <summary>
        /// Get local player index for this client
        /// </summary>
        private int GetLocalPlayerIndex()
        {
            // Simple method: check InputAuthority
            var localInputAuthority = Runner.LocalPlayer;

            if (localInputAuthority != null)
            {
                // Assuming Player 1 has PlayerId 1, Player 2 has PlayerId 2
                return localInputAuthority.PlayerId - 1; // Convert to 0-based index
            }

            return -1; // Unknown
        }

        // ==================== EDITOR TESTING ====================

        #if UNITY_EDITOR
        [Header("Testing")] [SerializeField] private bool enableTesting = false;

        private void Update()
        {
            if (!enableTesting || !Application.isPlaying) return;

            // Test controls
            if (Input.GetKeyDown(KeyCode.W)) ShowWin();
            if (Input.GetKeyDown(KeyCode.L)) ShowLose();
            if (Input.GetKeyDown(KeyCode.D)) ShowDraw();
            if (Input.GetKeyDown(KeyCode.H)) HideAllPanels();
        }
        #endif
    }
}