using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using _GAME.Scripts.CharacterSelection;

namespace UI
{
    using _GAME.Scripts.Core;

    public class MapSelectionCanvas : UICanvas
    {
        [Header("UI References")] [SerializeField] private Button          startButton;
        [SerializeField]                           private Button          backButton;
        [SerializeField]                           private TextMeshProUGUI titleText;
        [SerializeField]                           private TextMeshProUGUI statusText;

        [SerializeField] private Image           mapPreviewImage;
        [SerializeField] private TextMeshProUGUI mapNameText;

        [Header("Debug")] [SerializeField] private bool enableDebugLogs = true;

        private NetworkRunner _runner;
        private bool          _isInitialized = false;

        private void Awake()
        {
            // Setup button listeners
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartButtonClicked);
                startButton.gameObject.SetActive(false); // Hidden by default, will be shown based on conditions
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(OnBackButtonClicked);
            }

            // Set default texts
            if (titleText != null) titleText.text = "Map Selection";

            if (statusText != null) statusText.text = "Loading...";

            // Register this UI with UIManager for proper management
            // Note: This is important for UI loaded from Resources to be properly tracked
            RegisterWithUIManager();
        }

        private void RegisterWithUIManager()
        {
            // Wait a frame to ensure UIManager is fully initialized
            StartCoroutine(RegisterAfterFrame());
        }

        private System.Collections.IEnumerator RegisterAfterFrame()
        {
            yield return null; // Wait one frame for initialization

            if (UIManager.Instance != null)
            {
                UIManager.Instance.RegisterExistingUI(this);
                if (enableDebugLogs) Debug.Log("[MapSelectionCanvas] Successfully registered with UIManager");
            }
            else
            {
                Debug.LogError("[MapSelectionCanvas] UIManager.Instance not found during registration!");
            }
        }

        public override void Show()
        {
            base.Show();

            if (enableDebugLogs) Debug.Log("[MapSelectionCanvas] Showing Map Selection Canvas");

            // Initialize when shown
            if (!_isInitialized)
            {
                Initialize();
            }
            RandomSelectMap();

            UpdateUI();
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            _runner = FindObjectOfType<NetworkRunner>();
            if (_runner == null)
            {
                Debug.LogError("[MapSelectionCanvas] NetworkRunner not found!");
                return;
            }

            _isInitialized = true;

            if (enableDebugLogs) Debug.Log("[MapSelectionCanvas] Initialized successfully");
        }

        private void UpdateUI()
        {
            if (_runner == null) return;

            // Only server can start the game
            bool canStart = _runner.IsServer;

            if (startButton != null)
            {
                startButton.gameObject.SetActive(canStart);
            }

            // Update status text
            if (statusText != null)
            {
                if (canStart)
                {
                    statusText.text = "Ready to start the game!";
                }
                else
                {
                    statusText.text = "Waiting for server to start the game...";
                }
            }

            if (enableDebugLogs) Debug.Log($"[MapSelectionCanvas] UI Updated - IsServer: {_runner.IsServer}");
        }

        private void OnStartButtonClicked()
        {
            if (_runner == null || !_runner.IsServer)
            {
                Debug.LogWarning("[MapSelectionCanvas] Start button clicked but not server!");
                return;
            }

            if (enableDebugLogs) Debug.Log("[MapSelectionCanvas] Start button clicked - requesting game transition through CharacterSelectionState");

            // Instead of calling GameStateManager directly, use the networked state manager
            // This ensures the transition is synchronized across all clients
            if (CharacterSelectionState.Instance != null)
            {
                CharacterSelectionState.Instance.TriggerFinalGameTransition();
            }
            else
            {
                Debug.LogError("[MapSelectionCanvas] CharacterSelectionState.Instance not found! Cannot synchronize game transition.");
            }

            var gameManager = FindObjectOfType<GameManager>();
            if (gameManager != null)
            {
                gameManager.StartGameFromUI();
            }
        }

        private void OnBackButtonClicked()
        {
            if (enableDebugLogs) Debug.Log("[MapSelectionCanvas] Back button clicked");

            // TODO: Implement back to character selection logic
            // This might require resetting game state and going back
            Debug.LogWarning("[MapSelectionCanvas] Back functionality not implemented yet");
        }

        // Method to be called when map selection is complete
        // This will be expanded later when full map selection is implemented
        public void OnMapSelected(int mapIndex)
        {
            if (enableDebugLogs) Debug.Log($"[MapSelectionCanvas] Map selected: {mapIndex}");

            // TODO: Implement map selection logic
            // - Send RPC to all clients about selected map
            // - Update UI to show selected map
            // - Enable start button if server
        }

        // Method to handle when all players are ready for game start
        public void OnAllPlayersReady()
        {
            if (enableDebugLogs) Debug.Log("[MapSelectionCanvas] All players ready for game start");

            UpdateUI();
        }

        private void OnDestroy()
        {
            // Clean up button listeners
            if (startButton != null) startButton.onClick.RemoveListener(OnStartButtonClicked);

            if (backButton != null) backButton.onClick.RemoveListener(OnBackButtonClicked);
        }

        // Debug method to test the canvas
        [ContextMenu("Test Map Selection Canvas")]
        public void TestCanvas()
        {
            Debug.Log("[MapSelectionCanvas] Testing canvas functionality");
            UpdateUI();
        }

        private void RandomSelectMap()
        {
            if (_runner == null || !_runner.IsServer) return;

            var mapManager = MapManager.Instance;
            if (mapManager != null)
            {
                mapManager.RandomSelectAndSpawnMap();
                StartCoroutine(UpdateMapDisplayAfterSpawn());
            }
            else
            {
                Debug.LogError("[MapSelectionCanvas] MapManager not found!");
            }
        }

        private System.Collections.IEnumerator UpdateMapDisplayAfterSpawn()
        {
            // Wait a frame to ensure map is spawned
            yield return null;

            // Update UI display
            UpdateMapDisplay();

            // Verify map is ready
            var mapManager = MapManager.Instance;
            if (mapManager != null && mapManager.IsMapReady())
            {
                if (enableDebugLogs) Debug.Log("[MapSelectionCanvas] Map spawned and UI updated successfully");
            }
        }

        private void UpdateMapDisplay()
        {
            var mapManager = MapManager.Instance;
            if (mapManager == null) return;

            var currentMapData = mapManager.CurrentMapData;
            if (currentMapData == null) return;

            // Update map preview image
            if (mapPreviewImage != null && currentMapData.mapPreviewImage != null)
            {
                mapPreviewImage.sprite = currentMapData.mapPreviewImage;
                mapPreviewImage.gameObject.SetActive(true);
            }

            // Update map name text
            if (mapNameText != null)
            {
                mapNameText.text = currentMapData.mapName;
            }

            if (enableDebugLogs) Debug.Log($"[MapSelectionCanvas] Updated display for map: {currentMapData.mapName}");
        }
    }
}