using UnityEngine;
using Fusion;
using _GAME.Scripts.UI;
using _GAME.Scripts.Combat;

namespace _GAME.Scripts.Core
{
    using _GAME.Scripts.FSM;

    public class GameManager : NetworkBehaviour
    {
        [Header("Round Settings")] [SerializeField] private float roundDuration     = 60f;
        [SerializeField]                            private int   maxRounds         = 3;
        [SerializeField]                            private float countdownDuration = 3f;

        [Header("Map Boundaries")] [SerializeField] private float leftBoundary   = -10f;
        [SerializeField]                            private float rightBoundary  = 10f;
        [SerializeField]                            private float bottomBoundary = -5f;

        [Header("References")] [SerializeField] private GameHUD         gameHUD;
        [SerializeField]                        private HealthSystem    healthSystem;
        [SerializeField]                        private CountdownUI     countdownUI;
        [SerializeField]                        private RoundProgressUI roundProgressUI;

        // Network Properties
        [Networked] public  GameState CurrentState       { get; set; }
        [Networked] public  int       CurrentRound       { get; set; }
        [Networked] public  int       Player1Wins        { get; set; }
        [Networked] public  int       Player2Wins        { get; set; }
        [Networked] public  float     CountdownTimer     { get; set; }
        [Networked] public  bool      GameplayFrozen     { get; set; }
        [Networked] private float     nextRoundStartTime { get; set; }

        // Player References
        private PlayerController player1;
        private PlayerController player2;

        // Game States
        public enum GameState
        {
            WaitingToStart,
            Countdown,
            Fighting,
            RoundEnd,
            GameEnd
        }

        public override void Spawned()
        {
            // Find references
            if (gameHUD == null) gameHUD                 = FindObjectOfType<GameHUD>();
            if (healthSystem == null) healthSystem       = FindObjectOfType<HealthSystem>();
            if (roundProgressUI == null) roundProgressUI = FindObjectOfType<RoundProgressUI>();

            // Find players
            var players = FindObjectsOfType<PlayerController>();
            if (players.Length >= 2)
            {
                player1 = players[0];
                player2 = players[1];
            }

            // Server waits for manual start - KHÔNG AUTO START
            if (HasStateAuthority)
            {
                CurrentState   = GameState.WaitingToStart;
                GameplayFrozen = true;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            switch (CurrentState)
            {
                case GameState.WaitingToStart:
                    // Do nothing, wait for manual start
                    break;
                case GameState.Countdown: UpdateCountdown(); break;
                case GameState.Fighting:
                    UpdateFighting();
                    CheckBoundaries();
                    break;
                case GameState.RoundEnd:
                    // Check if time to start next round
                    if (nextRoundStartTime > 0 && Runner.SimulationTime >= nextRoundStartTime)
                    {
                        nextRoundStartTime = 0;
                        StartRound();
                    }
                    break;
            }
        }

        // ==================== GAME FLOW ====================

        private void StartNewGame()
        {
            CurrentRound = 1;
            Player1Wins  = 0;
            Player2Wins  = 0;

            // RESET ROUND PROGRESS UI
            if (roundProgressUI != null)
            {
                roundProgressUI.RPC_ResetAllProgress();
            }

            StartRound();
        }

        private void StartRound()
        {
            CurrentState   = GameState.Countdown;
            CountdownTimer = countdownDuration;
            GameplayFrozen = true;

            // Reset players
            ResetPlayersForNewRound();

            FreezeAllPlayers();

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
                // UNFREEZE ALL ANIMATIONS + LOGIC
                UnfreezeAllPlayers();

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
            FreezeAllPlayers();

            gameHUD?.StopTimer();

            Debug.Log($"[GameManager] {message}");

            // Update scores
            if (winner == 1)
                Player1Wins++;
            else if (winner == 2) Player2Wins++;

            // UPDATE ROUND PROGRESS UI - NEW LOGIC
            if (roundProgressUI != null && winner > 0)
            {
                int roundIndex  = CurrentRound - 1; // Convert to 0-based index
                int winnerIndex = winner - 1;       // Convert to 0-based index (Player 1 = 0, Player 2 = 1)
                roundProgressUI.RPC_SetRoundResult(roundIndex, winnerIndex);
            }

            // Check game end conditions
            if (ShouldEndGame())
            {
                EndGame();
            }
            else
            {
                // Start next round
                CurrentRound++;
                nextRoundStartTime = Runner.SimulationTime + 2f;
            }
        }

        // ==================== UTILITY ====================
        /// <summary>
        /// Start game manually - called by Start button
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_StartGame()
        {
            if (CurrentState != GameState.WaitingToStart) return;

            Debug.Log("[GameManager] Manual game start triggered!");
            StartNewGame();
        }

        /// <summary>
        /// Public method for UI to call
        /// </summary>
        public void StartGameFromUI()
        {
            RPC_StartGame();
        }

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

            // Reset positions
            if (player1 != null) player1.transform.position = new Vector3(-2f, 0f, 0f);
            if (player2 != null) player2.transform.position = new Vector3(2f, 0f, 0f);

            // Reset states
            ResetPlayerStates();
        }

        /// <summary>
        /// Reset player states for new round - Game Jam Quick Fix
        /// </summary>
        private void ResetPlayerStates()
        {
            ResetPlayerToIdle(player1);
            ResetPlayerToIdle(player2);
        }

        private void ResetPlayerToIdle(PlayerController player)
        {
            if (player == null) return;

            var stateMachine = player.GetComponent<NetworkedStateMachine>();
            if (stateMachine == null) return;

            // Get idle state via reflection
            var field = typeof(PlayerController).GetField("_idleState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var idleState = field?.GetValue(player);
            if (idleState != null)
            {
                stateMachine.ChangeState(idleState as IState);
            }
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

        /// <summary>
        /// Freeze all player animations and logic
        /// </summary>
        private void FreezeAllPlayers()
        {
            SetPlayersFrozen(true);
        }

        /// <summary>
        /// Unfreeze all player animations and logic
        /// </summary>
        private void UnfreezeAllPlayers()
        {
            SetPlayersFrozen(false);
        }

        /// <summary>
        /// Set freeze state for all players - includes animation
        /// </summary>
        private void SetPlayersFrozen(bool frozen)
        {
            if (player1 != null) SetPlayerFrozen(player1, frozen);
            if (player2 != null) SetPlayerFrozen(player2, frozen);
        }

        /// <summary>
        /// Freeze/unfreeze individual player - Game Jam Quick Fix
        /// </summary>
        private void SetPlayerFrozen(PlayerController player, bool frozen)
        {
            if (player == null) return;

            // Freeze/unfreeze animator
            var animator = player.GetComponent<Animator>();
            if (animator != null)
            {
                animator.speed = frozen ? 0f : 1f;
            }

            // Freeze/unfreeze physics
            if (player._rigidbody != null)
            {
                if (frozen)
                {
                    // Store current velocity before freezing
                    var velocityField = typeof(PlayerController).GetField("_frozenVelocity",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (velocityField == null)
                    {
                        // Add private field to PlayerController if not exists
                        Debug.LogWarning("[GameManager] _frozenVelocity field not found in PlayerController");
                    }
                    else
                    {
                        velocityField.SetValue(player, player._rigidbody.velocity);
                    }

                    player._rigidbody.velocity    = Vector2.zero;
                    player._rigidbody.isKinematic = true;
                }
                else
                {
                    player._rigidbody.isKinematic = false;
                    // Restore velocity if needed (for smooth unfreeze)
                }
            }

            // Freeze/unfreeze input
            player.SetInputEnabled(!frozen);
        }

        // ==================== PUBLIC API ====================

        public bool      IsGameplayFrozen() => GameplayFrozen;
        public float     GetCountdownTime() => CountdownTimer;
        public GameState GetCurrentState()  => CurrentState;
    }
}