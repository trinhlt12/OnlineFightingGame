using UnityEngine;
using Fusion;
using _GAME.Scripts.UI;
using _GAME.Scripts.Combat;

namespace _GAME.Scripts.Core
{
    public class GameManager : NetworkBehaviour
    {
        [Header("Round Settings")] [SerializeField] private float roundDuration     = 60f;
        [SerializeField]                            private int   maxRounds         = 3;
        [SerializeField]                            private float countdownDuration = 3f;

        [Header("Map Boundaries")] [SerializeField] private float leftBoundary   = -10f;
        [SerializeField]                            private float rightBoundary  = 10f;
        [SerializeField]                            private float bottomBoundary = -5f;

        [Header("References")] [SerializeField] private GameHUD      gameHUD;
        [SerializeField]                        private HealthSystem healthSystem;
        [SerializeField]                        private CountdownUI  countdownUI;


        // Network Properties
        [Networked] public GameState CurrentState   { get; set; }
        [Networked] public int       CurrentRound   { get; set; }
        [Networked] public int       Player1Wins    { get; set; }
        [Networked] public int       Player2Wins    { get; set; }
        [Networked] public float     CountdownTimer { get; set; }
        [Networked] public bool      GameplayFrozen { get; set; }

        // Player References
        private PlayerController player1;
        private PlayerController player2;

        // Game States
        public enum GameState
        {
            Countdown,
            Fighting,
            RoundEnd,
            GameEnd
        }

        public override void Spawned()
        {
            // Find references
            if (gameHUD == null) gameHUD           = FindObjectOfType<GameHUD>();
            if (healthSystem == null) healthSystem = FindObjectOfType<HealthSystem>();

            // Find players
            var players = FindObjectsOfType<PlayerController>();
            if (players.Length >= 2)
            {
                player1 = players[0];
                player2 = players[1];
            }

            // Server initializes game
            if (HasStateAuthority)
            {
                StartNewGame();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            switch (CurrentState)
            {
                case GameState.Countdown: UpdateCountdown(); break;
                case GameState.Fighting:
                    UpdateFighting();
                    CheckBoundaries();
                    break;
            }
        }

        // ==================== GAME FLOW ====================

        private void StartNewGame()
        {
            CurrentRound = 1;
            Player1Wins  = 0;
            Player2Wins  = 0;
            StartRound();
        }

        private void StartRound()
        {
            CurrentState   = GameState.Countdown;
            CountdownTimer = countdownDuration;
            GameplayFrozen = true;

            // Reset players
            ResetPlayersForNewRound();

            // Update UI
            gameHUD?.SetRound(CurrentRound);
            countdownUI?.ShowCountdown(CurrentRound);

            Debug.Log($"[GameManager] Starting Round {CurrentRound}");
        }

        private void UpdateCountdown()
        {
            CountdownTimer -= Runner.DeltaTime;

            if (CountdownTimer <= 0f)
            {
                // Start fighting
                CurrentState   = GameState.Fighting;
                GameplayFrozen = false;
                gameHUD?.StartTimer(roundDuration);

                Debug.Log($"[GameManager] Round {CurrentRound} - FIGHT!");
            }
        }

        private void UpdateFighting()
        {
            // Check if time up
            if (gameHUD != null && !gameHUD.IsTimerActive())
            {
                EndRoundByTime();
            }
        }

        // ==================== ROUND END CONDITIONS ====================

        public void EndRoundByDeath(PlayerController deadPlayer)
        {
            if (CurrentState != GameState.Fighting) return;

            int winnerIndex = (deadPlayer == player1) ? 2 : 1;
            EndRound(winnerIndex, $"Player {winnerIndex} wins by KO!");
        }

        public void EndRoundByBoundary(PlayerController knockedPlayer)
        {
            if (CurrentState != GameState.Fighting) return;

            int winnerIndex = (knockedPlayer == player1) ? 2 : 1;
            EndRound(winnerIndex, $"Player {winnerIndex} wins by Ring Out!");
        }

        private void EndRoundByTime()
        {
            // Compare health
            float p1Health = healthSystem.Player1Health;
            float p2Health = healthSystem.Player2Health;

            if (p1Health > p2Health)
                EndRound(1, "Player 1 wins by Health!");
            else if (p2Health > p1Health)
                EndRound(2, "Player 2 wins by Health!");
            else
                EndRound(0, "Draw! No winner this round.");
        }

        private void EndRound(int winner, string message)
        {
            CurrentState   = GameState.RoundEnd;
            GameplayFrozen = true;
            gameHUD?.StopTimer();

            Debug.Log($"[GameManager] {message}");

            // Update scores
            if (winner == 1)
                Player1Wins++;
            else if (winner == 2) Player2Wins++;

            // Check game end conditions
            if (ShouldEndGame())
            {
                EndGame();
            }
            else
            {
                // Start next round after delay
                CurrentRound++;
                Invoke(nameof(StartRound), 2f);
            }
        }

        // ==================== UTILITY ====================

        private bool ShouldEndGame()
        {
            // Win conditions: 2-0 or 2-1
            return Player1Wins >= 2 || Player2Wins >= 2 || CurrentRound >= maxRounds;
        }

        private void EndGame()
        {
            CurrentState = GameState.GameEnd;

            string finalResult = Player1Wins > Player2Wins ? "Player 1 Wins the Match!" : "Player 2 Wins the Match!";

            Debug.Log($"[GameManager] Game Over! {finalResult} ({Player1Wins}-{Player2Wins})");
        }

        private void ResetPlayersForNewRound()
        {
            // Reset health
            if (healthSystem != null)
            {
                healthSystem.Player1Health = 100f;
                healthSystem.Player2Health = 100f;
            }

            // Reset player positions (simple)
            if (player1 != null) player1.transform.position = new Vector3(-2f, 0f, 0f);
            if (player2 != null) player2.transform.position = new Vector3(2f, 0f, 0f);
        }

        private void CheckBoundaries()
        {
            if (player1 != null && IsOutOfBounds(player1.transform.position)) EndRoundByBoundary(player1);

            if (player2 != null && IsOutOfBounds(player2.transform.position)) EndRoundByBoundary(player2);
        }

        private bool IsOutOfBounds(Vector3 position)
        {
            return position.x < leftBoundary || position.x > rightBoundary || position.y < bottomBoundary;
        }

        // ==================== PUBLIC API ====================

        public bool      IsGameplayFrozen() => GameplayFrozen;
        public float     GetCountdownTime() => CountdownTimer;
        public GameState GetCurrentState()  => CurrentState;
    }
}