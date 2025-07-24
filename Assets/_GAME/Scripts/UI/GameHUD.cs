using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System.Collections;

namespace _GAME.Scripts.UI
{
    using _GAME.Scripts.Core;

    /// <summary>
    /// Main HUD for fighting game - manages health bars, timer, and round display
    /// Network-synchronized for consistent display across all clients
    /// </summary>
    public class GameHUD : NetworkBehaviour
    {
        [Header("Health Bar Configuration")]
        [SerializeField] private HealthBarUI leftHealthBar;
        [SerializeField] private HealthBarUI rightHealthBar;

        [Header("Timer Configuration")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private Image timerBackground;
        [SerializeField] private Color normalTimerColor = Color.white;
        [SerializeField] private Color warningTimerColor = Color.red;
        [SerializeField] private float warningTimeThreshold = 10f;

        [Header("Round Display")]
        [SerializeField] private TextMeshProUGUI roundText;
        [SerializeField] private GameObject[] player1RoundIndicators;
        [SerializeField] private GameObject[] player2RoundIndicators;

        [Header("Animation Settings")]
        [SerializeField] private float healthAnimationSpeed = 2f;
        [SerializeField] private bool enableHealthAnimation = true;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Networked properties for timer synchronization
        [Networked] public float NetworkedTimeRemaining { get; set; }
        [Networked] public int NetworkedCurrentRound { get; set; }
        [Networked] public bool NetworkedTimerActive { get; set; }

        // Player references for health tracking
        private PlayerController player1Controller;
        private PlayerController player2Controller;

        // Animation coroutines
        private Coroutine leftHealthAnimCoroutine;
        private Coroutine rightHealthAnimCoroutine;

        public override void Spawned()
        {
            InitializeHUD();

            if (enableDebugLogs)
                Debug.Log("[GameHUD] HUD initialized and ready");
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // Update timer on server
            if (NetworkedTimerActive && NetworkedTimeRemaining > 0)
            {
                NetworkedTimeRemaining -= Runner.DeltaTime;

                if (NetworkedTimeRemaining <= 0)
                {
                    NetworkedTimeRemaining = 0;
                    OnTimeUp();
                }
            }
        }

        public override void Render()
        {
            UpdateTimerDisplay();
            UpdateHealthBars();
        }

        /// <summary>
        /// Initialize HUD components and default values
        /// </summary>
        private void InitializeHUD()
        {
            // Initialize health bars
            if (leftHealthBar != null)
            {
                leftHealthBar.Initialize("Player 1", true); // Left side
                leftHealthBar.SetHealth(100f, 100f);
            }

            if (rightHealthBar != null)
            {
                rightHealthBar.Initialize("Player 2", false); // Right side
                rightHealthBar.SetHealth(100f, 100f);
            }

            // Initialize timer
            if (HasStateAuthority)
            {
                NetworkedTimeRemaining = 99f; // Default fight time
                NetworkedCurrentRound = 1;
                NetworkedTimerActive = false;
            }

            UpdateRoundDisplay();
        }

        /// <summary>
        /// Set player references for health tracking
        /// </summary>
        public void SetPlayerReferences(PlayerController player1, PlayerController player2)
        {
            player1Controller = player1;
            player2Controller = player2;

            if (enableDebugLogs)
                Debug.Log("[GameHUD] Player references set");
        }

        /// <summary>
        /// Start round timer
        /// </summary>
        public void StartRoundTimer(float roundTime = 99f)
        {
            if (!HasStateAuthority) return;

            NetworkedTimeRemaining = roundTime;
            NetworkedTimerActive = true;

            if (enableDebugLogs)
                Debug.Log($"[GameHUD] Round timer started: {roundTime} seconds");
        }

        /// <summary>
        /// Stop round timer
        /// </summary>
        public void StopRoundTimer()
        {
            if (!HasStateAuthority) return;

            NetworkedTimerActive = false;

            if (enableDebugLogs)
                Debug.Log("[GameHUD] Round timer stopped");
        }

        /// <summary>
        /// Update player health display
        /// </summary>
        public void UpdatePlayerHealth(int playerIndex, float currentHealth, float maxHealth)
        {
            if (playerIndex == 0 && leftHealthBar != null)
            {
                if (enableHealthAnimation)
                    AnimateHealthBar(leftHealthBar, currentHealth, maxHealth, ref leftHealthAnimCoroutine);
                else
                    leftHealthBar.SetHealth(currentHealth, maxHealth);
            }
            else if (playerIndex == 1 && rightHealthBar != null)
            {
                if (enableHealthAnimation)
                    AnimateHealthBar(rightHealthBar, currentHealth, maxHealth, ref rightHealthAnimCoroutine);
                else
                    rightHealthBar.SetHealth(currentHealth, maxHealth);
            }
        }

        /// <summary>
        /// Update round information
        /// </summary>
        public void UpdateRound(int roundNumber, int player1Wins, int player2Wins)
        {
            if (!HasStateAuthority) return;

            NetworkedCurrentRound = roundNumber;

            // Update round indicators on all clients
            RPC_UpdateRoundIndicators(player1Wins, player2Wins);
        }

        /// <summary>
        /// Animate health bar changes smoothly
        /// </summary>
        private void AnimateHealthBar(HealthBarUI healthBar, float targetHealth, float maxHealth, ref Coroutine animCoroutine)
        {
            if (animCoroutine != null)
                StopCoroutine(animCoroutine);

            animCoroutine = StartCoroutine(AnimateHealthCoroutine(healthBar, targetHealth, maxHealth));
        }

        private IEnumerator AnimateHealthCoroutine(HealthBarUI healthBar, float targetHealth, float maxHealth)
        {
            float startHealth = healthBar.GetCurrentHealth();
            float elapsed = 0f;
            float animationDuration = Mathf.Abs(targetHealth - startHealth) / (maxHealth * healthAnimationSpeed);

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animationDuration;
                float currentHealth = Mathf.Lerp(startHealth, targetHealth, t);

                healthBar.SetHealth(currentHealth, maxHealth);
                yield return null;
            }

            healthBar.SetHealth(targetHealth, maxHealth);
        }

        /// <summary>
        /// Update timer display every frame
        /// </summary>
        private void UpdateTimerDisplay()
        {
            if (timerText == null) return;

            int minutes = Mathf.FloorToInt(NetworkedTimeRemaining / 60f);
            int seconds = Mathf.FloorToInt(NetworkedTimeRemaining % 60f);

            timerText.text = $"{minutes:00}:{seconds:00}";

            // Change color when time is running low
            if (NetworkedTimeRemaining <= warningTimeThreshold)
            {
                timerText.color = warningTimerColor;
                if (timerBackground != null)
                    timerBackground.color = warningTimerColor;
            }
            else
            {
                timerText.color = normalTimerColor;
                if (timerBackground != null)
                    timerBackground.color = normalTimerColor;
            }
        }

        /// <summary>
        /// Update health bars from player controllers
        /// </summary>
        private void UpdateHealthBars()
        {
            // This will be connected to actual health systems later
            // For now, we keep it ready for integration
        }

        /// <summary>
        /// Update round display
        /// </summary>
        private void UpdateRoundDisplay()
        {
            if (roundText != null)
                roundText.text = $"ROUND {NetworkedCurrentRound}";
        }

        /// <summary>
        /// Called when timer reaches zero
        /// </summary>
        private void OnTimeUp()
        {
            NetworkedTimerActive = false;

            if (enableDebugLogs)
                Debug.Log("[GameHUD] Time's up!");

            // Notify game manager about time up
            // This will trigger timeout win condition logic
        }

        /// <summary>
        /// Network RPC to update round indicators on all clients
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_UpdateRoundIndicators(int player1Wins, int player2Wins)
        {
            // Update player 1 round indicators
            for (int i = 0; i < player1RoundIndicators.Length; i++)
            {
                if (player1RoundIndicators[i] != null)
                    player1RoundIndicators[i].SetActive(i < player1Wins);
            }

            // Update player 2 round indicators
            for (int i = 0; i < player2RoundIndicators.Length; i++)
            {
                if (player2RoundIndicators[i] != null)
                    player2RoundIndicators[i].SetActive(i < player2Wins);
            }

            UpdateRoundDisplay();
        }

        // ==================== PUBLIC API FOR EXTERNAL SYSTEMS ====================

        /// <summary>
        /// Get remaining time (for external systems)
        /// </summary>
        public float GetRemainingTime() => NetworkedTimeRemaining;

        /// <summary>
        /// Check if timer is active
        /// </summary>
        public bool IsTimerActive() => NetworkedTimerActive;

        /// <summary>
        /// Get current round number
        /// </summary>
        public int GetCurrentRound() => NetworkedCurrentRound;

        // ==================== EDITOR TESTING ====================

#if UNITY_EDITOR
        [Header("Editor Testing")]
        [SerializeField] private bool testMode = false;

        private void Update()
        {
            if (!testMode || !Application.isPlaying) return;

            // Test controls in editor
            if (Input.GetKeyDown(KeyCode.Alpha1))
                UpdatePlayerHealth(0, Random.Range(0f, 100f), 100f);

            if (Input.GetKeyDown(KeyCode.Alpha2))
                UpdatePlayerHealth(1, Random.Range(0f, 100f), 100f);

            if (Input.GetKeyDown(KeyCode.T))
                StartRoundTimer(10f);
        }
#endif
    }
}