using UnityEngine;
using Fusion;
using _GAME.Scripts.UI;
using _GAME.Scripts.Combat;

namespace _GAME.Scripts.Core
{
    using System.Collections.Generic;
    using _GAME.Scripts.Camera;
    using _GAME.Scripts.FSM;
    using Camera = UnityEngine.Camera;

    public class GameManager : NetworkBehaviour
    {
        [Header("Round Settings")] [SerializeField] private float roundDuration     = 60f;
        [SerializeField]                            private int   maxRounds         = 3;
        [SerializeField]                            private float countdownDuration = 3f;

        [Networked] public float   LeftBoundary           { get; set; } = -10f;
        [Networked] public float   RightBoundary          { get; set; } = 10f;
        [Networked] public float   BottomBoundary         { get; set; } = -5f;
        [Networked] public Vector2 NetworkCameraMinBounds { get; set; }
        [Networked] public Vector2 NetworkCameraMaxBounds { get; set; }
        [Networked] public float   NetworkCameraSize      { get; set; }

        private CameraFollow                   _cameraFollow;
        private CharacterSpawnManager          _characterSpawnManager;
        private Dictionary<PlayerRef, Vector3> _playerSpawnPositions = new Dictionary<PlayerRef, Vector3>();

        [Header("References")] [SerializeField] private GameHUD         gameHUD;
        [SerializeField]                        private HealthSystem    healthSystem;
        [SerializeField]                        private CountdownUI     countdownUI;
        [SerializeField]                        private RoundProgressUI roundProgressUI;
        [SerializeField]                        private WinLoseUI       winLoseUI;

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
            if (winLoseUI == null) winLoseUI             = FindObjectOfType<WinLoseUI>();
            _characterSpawnManager = FindObjectOfType<CharacterSpawnManager>();

            // Find players
            var players = FindObjectsOfType<PlayerController>();

            if (players.Length >= 2)
            {
                player1 = players[0];
                player2 = players[1];

                StorePlayerSpawnPositions();
            }

            // Server waits for manual start - NO UTO START
            if (HasStateAuthority)
            {
                CurrentState   = GameState.WaitingToStart;
                GameplayFrozen = true;
            }
        }

        private void StorePlayerSpawnPositions()
        {
            _playerSpawnPositions.Clear();

            if (_characterSpawnManager == null) return;

            // Get spawned characters and their positions
            var spawnedCharacters = _characterSpawnManager.GetAllSpawnedCharacters();

            foreach (var kvp in spawnedCharacters)
            {
                PlayerRef     playerRef = kvp.Key;
                NetworkObject character = kvp.Value;

                if (character != null)
                {
                    Vector3 spawnPos = character.transform.position;
                    _playerSpawnPositions[playerRef] = spawnPos;

                    Debug.Log($"[GameManager] Stored spawn position for player {playerRef}: {spawnPos}");
                }
            }
        }

        private void UpdateBoundariesFromBackground()
        {
            if (!HasStateAuthority) return;

            GameObject backgroundObject = GameObject.FindWithTag("MapBackground");
            if (backgroundObject == null)
            {
                Debug.LogError("[GameManager] Could not find a GameObject with the 'MapBackground' tag.");
                return;
            }

            SpriteRenderer spriteRenderer = backgroundObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                Debug.LogError("[GameManager] 'MapBackground' object does not have a valid SpriteRenderer.");
                return;
            }

            Bounds spriteBounds = spriteRenderer.bounds;

            // Calculate camera bounds
            Camera cam           = Camera.main;
            if (cam == null) cam = FindObjectOfType<Camera>();

            if (cam != null)
            {
                // Set optimal camera size
                float mapWidth     = spriteBounds.size.x;
                float mapHeight    = spriteBounds.size.y;
                float screenAspect = (float)Screen.width / Screen.height;
                float mapAspect    = mapWidth / mapHeight;

                float optimalCameraSize;
                if (screenAspect > mapAspect)
                {
                    optimalCameraSize = mapHeight / 2f;
                }
                else
                {
                    optimalCameraSize = mapWidth / (2f * screenAspect);
                }

                optimalCameraSize *= 0.9f; // Add padding
                optimalCameraSize =  Mathf.Max(optimalCameraSize, 3f);

                // Store networked camera size
                NetworkCameraSize = optimalCameraSize;

                // Calculate bounds with camera size
                float cameraHalfHeight = optimalCameraSize;
                float cameraHalfWidth  = cameraHalfHeight * screenAspect;

                float paddingX = cameraHalfWidth * 0.1f;
                float paddingY = cameraHalfHeight * 0.1f;

                Vector2 minBounds = new Vector2(
                    spriteBounds.min.x + cameraHalfWidth - paddingX,
                    spriteBounds.min.y + cameraHalfHeight - paddingY
                );

                Vector2 maxBounds = new Vector2(
                    spriteBounds.max.x - cameraHalfWidth + paddingX,
                    spriteBounds.max.y - cameraHalfHeight + paddingY
                );

                // Store networked bounds
                NetworkCameraMinBounds = minBounds;
                NetworkCameraMaxBounds = maxBounds;

                // Update game boundaries
                LeftBoundary   = minBounds.x;
                RightBoundary  = maxBounds.x;
                BottomBoundary = minBounds.y;

                Debug.Log($"[GameManager] Server calculated camera bounds: Min={minBounds}, Max={maxBounds}, Size={optimalCameraSize}");

                // Apply immediately on server
                ApplyCameraBounds();
            }
        }
        private void ApplyCameraBounds()
        {
            var cameraFollow = FindObjectOfType<CameraFollow>();
            if (cameraFollow != null)
            {
                // Set camera size
                var cam = cameraFollow.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.orthographicSize = NetworkCameraSize;
                }

                // Set camera bounds
                cameraFollow.SetBounds(NetworkCameraMinBounds, NetworkCameraMaxBounds);

                Debug.Log($"[GameManager] Applied camera bounds: Min={NetworkCameraMinBounds}, Max={NetworkCameraMaxBounds}, Size={NetworkCameraSize}");
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

        public override void Render()
        {
            // Client: Apply camera bounds when they change
            if (!HasStateAuthority && NetworkCameraSize > 0)
            {
                ApplyCameraBounds();
            }
        }

        // ==================== GAME FLOW ====================

        private void StartNewGame()
        {
            CurrentRound = 1;
            Player1Wins  = 0;
            Player2Wins  = 0;

            // Hide win/lose panels
            if (winLoseUI != null)
            {
                winLoseUI.RPC_HideAllPanels();
            }

            // Reset round progress UI
            if (roundProgressUI != null)
            {
                roundProgressUI.RPC_ResetAllProgress();
            }

            StartRound();
        }

        private void StartRound()
        {
            UpdateBoundariesFromBackground();

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

            // DIRECT RPC calls
            if (winLoseUI != null)
            {
                if (Player1Wins > Player2Wins)
                {
                    winLoseUI.RPC_ShowWinForPlayer1();
                }
                else
                {
                    winLoseUI.RPC_ShowWinForPlayer2();
                }
            }
        }

        /// <summary>
        /// Show win/lose result to all clients based on their player index
        /// </summary>
        private void ShowWinLoseToAllClients(int winnerPlayerIndex)
        {
            // Get all players to determine their indices
            var allPlayers = FindObjectsOfType<PlayerController>();

            foreach (var player in allPlayers)
            {
                if (player.Object.InputAuthority != PlayerRef.None)
                {
                    int playerIndex = GetPlayerIndexFromAuthority(player.Object.InputAuthority);
                    winLoseUI.RPC_ShowResult(playerIndex, winnerPlayerIndex - 1);
                }
            }
        }

        /// <summary>
        /// Get player index from InputAuthority - Game Jam Helper
        /// </summary>
        private int GetPlayerIndexFromAuthority(PlayerRef authority)
        {
            // Simple mapping: PlayerId 1 = Player Index 0, PlayerId 2 = Player Index 1
            return authority.PlayerId - 1;
        }

        private void ResetPlayersForNewRound()
        {
            // Reset health
            if (healthSystem != null)
            {
                healthSystem.Player1Health = 100f;
                healthSystem.Player2Health = 100f;
            }

            // Reset positions to original spawn points
            ResetPlayersToSpawnPositions();

            // Reset states
            ResetPlayerStates();
        }

        private void ResetPlayersToSpawnPositions()
        {
            if (_playerSpawnPositions.Count == 0)
            {
                Debug.LogWarning("[GameManager] No stored spawn positions, using fallback positions");
                // Fallback to hardcoded positions if needed
                if (player1 != null) player1.transform.position = new Vector3(-2f, 0f, 0f);
                if (player2 != null) player2.transform.position = new Vector3(2f, 0f, 0f);
                return;
            }

            // Reset each player to their spawn position
            var allPlayers = FindObjectsOfType<PlayerController>();

            foreach (var player in allPlayers)
            {
                if (player.Object != null && player.Object.InputAuthority != PlayerRef.None)
                {
                    PlayerRef playerRef = player.Object.InputAuthority;

                    if (_playerSpawnPositions.TryGetValue(playerRef, out Vector3 spawnPos))
                    {
                        player.transform.position = spawnPos;
                        Debug.Log($"[GameManager] Reset player {playerRef} to spawn position: {spawnPos}");
                    }
                }
            }
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
            return position.x < LeftBoundary || position.x > RightBoundary || position.y < BottomBoundary;
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

        /// <summary>
        /// Reset game to waiting state for rematch - Game Jam Version
        /// </summary>
        public void ResetToWaitingState()
        {
            if (!HasStateAuthority) return;

            var spawnManager = FindObjectOfType<CharacterSpawnManager>();
            if (spawnManager != null)
            {
                if (spawnManager.HasStateAuthority)
                {
                    spawnManager.DespawnAllCharacters();
                    Debug.Log("[GameManager] Called DespawnAllCharacters from spawn manager.");
                }
            }
            else
            {
                Debug.LogWarning("[GameManager] CharacterSpawnManager not found during reset. Cannot despawn old characters.");
            }

            // Reset game state
            CurrentState   = GameState.WaitingToStart;
            CurrentRound   = 1;
            Player1Wins    = 0;
            Player2Wins    = 0;
            GameplayFrozen = true;

            // Reset timers
            CountdownTimer     = 0;
            nextRoundStartTime = 0;

            // Hide/reset UI
            gameHUD?.StopTimer();
            roundProgressUI?.RPC_ResetAllProgress();

            // Unfreeze players for character selection
            UnfreezeAllPlayers();

            Debug.Log("[GameManager] Reset to waiting state for rematch");
        }
    }
}