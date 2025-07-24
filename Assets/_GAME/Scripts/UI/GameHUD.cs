using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;

namespace _GAME.Scripts.UI
{
    /// <summary>
    /// Simple Game HUD for fighting game - Game Jam Version
    /// </summary>
    public class GameHUD : NetworkBehaviour
    {
        [Header("Health Bars")]
        [SerializeField] private HealthBarUI leftHealthBar;
        [SerializeField] private HealthBarUI rightHealthBar;

        [Header("Timer")]
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("Round Info")]
        [SerializeField] private TextMeshProUGUI roundText;

        // Network synced timer
        [Networked] public float TimeRemaining { get; set; }
        [Networked] public bool TimerActive { get; set; }
        [Networked] public int CurrentRound { get; set; }

        // Player health tracking
        private float player1Health = 100f;
        private float player2Health = 100f;
        private const float MAX_HEALTH = 100f;

        public override void Spawned()
        {
            // Initialize
            if (leftHealthBar != null)
                leftHealthBar.Initialize("Player 1", true);

            if (rightHealthBar != null)
                rightHealthBar.Initialize("Player 2", false);

            // Server sets initial values
            if (HasStateAuthority)
            {
                TimeRemaining = 99f;
                TimerActive = false;
                CurrentRound = 1;
            }

            UpdateRoundDisplay();
        }

        public override void FixedUpdateNetwork()
        {
            // Only server updates timer
            if (!HasStateAuthority) return;

            if (TimerActive && TimeRemaining > 0)
            {
                TimeRemaining -= Runner.DeltaTime;

                if (TimeRemaining <= 0)
                {
                    TimeRemaining = 0;
                    TimerActive = false;
                    OnTimeUp();
                }
            }
        }

        public override void Render()
        {
            UpdateTimerDisplay();
        }

        // ==================== PUBLIC API ====================

        public void StartTimer(float seconds = 99f)
        {
            if (!HasStateAuthority) return;

            TimeRemaining = seconds;
            TimerActive = true;
        }

        public void StopTimer()
        {
            if (!HasStateAuthority) return;
            TimerActive = false;
        }

        public void SetPlayerHealth(int playerIndex, float health)
        {
            health = Mathf.Clamp(health, 0f, MAX_HEALTH);

            if (playerIndex == 0)
            {
                player1Health = health;
                if (leftHealthBar != null)
                    leftHealthBar.SetHealth(health, MAX_HEALTH);
            }
            else if (playerIndex == 1)
            {
                player2Health = health;
                if (rightHealthBar != null)
                    rightHealthBar.SetHealth(health, MAX_HEALTH);
            }
        }

        public void SetRound(int roundNumber)
        {
            if (!HasStateAuthority) return;

            CurrentRound = roundNumber;
            UpdateRoundDisplay();
        }

        // ==================== INTERNAL METHODS ====================

        private void UpdateTimerDisplay()
        {
            if (timerText == null) return;

            int minutes = Mathf.FloorToInt(TimeRemaining / 60f);
            int seconds = Mathf.FloorToInt(TimeRemaining % 60f);

            timerText.text = $"{minutes:00}:{seconds:00}";

            // Red color when low time
            if (TimeRemaining <= 10f)
                timerText.color = Color.red;
            else
                timerText.color = Color.white;
        }

        private void UpdateRoundDisplay()
        {
            if (roundText != null)
                roundText.text = $"ROUND {CurrentRound}";
        }

        private void OnTimeUp()
        {
            Debug.Log("[GameHUD] Time's up!");
            // Game Manager will handle timeout logic
        }

        // ==================== GETTERS ====================

        public float GetTimeRemaining() => TimeRemaining;
        public bool IsTimerActive() => TimerActive;
        public float GetPlayer1Health() => player1Health;
        public float GetPlayer2Health() => player2Health;

        // ==================== EDITOR TESTING ====================

#if UNITY_EDITOR
        [Header("Testing")]
        [SerializeField] private bool enableTesting = false;

        private void Update()
        {
            if (!enableTesting || !Application.isPlaying) return;

            if (Input.GetKeyDown(KeyCode.Alpha1))
                SetPlayerHealth(0, Random.Range(0f, 100f));
            if (Input.GetKeyDown(KeyCode.Alpha2))
                SetPlayerHealth(1, Random.Range(0f, 100f));
            if (Input.GetKeyDown(KeyCode.T))
                StartTimer(10f);
            if (Input.GetKeyDown(KeyCode.S))
                StopTimer();
        }
#endif
    }
}