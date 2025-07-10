namespace UI
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using _GAME.Scripts.CharacterSelection;
    using _GAME.Scripts.Data;
    using Fusion;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    public class CharacterSelectionCanvas : UICanvas
    {
        [Header("Character Selection")]
        [SerializeField] private CharacterData[] characterOptions;
        [SerializeField] private CharacterSelectionButton buttonPrefab;
        [SerializeField] private Transform buttonContainer; // Reference to HorizontalLayout - ButtonContainer

        [Header("Player 1 Display")]
        [SerializeField] private Image player1Portrait;
        [SerializeField] private TextMeshProUGUI player1CharacterName;
        [SerializeField] private TextMeshProUGUI player1PlayerName;
        [SerializeField] private GameObject player1ReadyIndicator;
        [SerializeField] private TextMeshProUGUI player1SelectionStatus;
        [SerializeField] private CharacterStatsDisplay player1StatsDisplay;

        [Header("Player 2 Display")]
        [SerializeField] private Image player2Portrait;
        [SerializeField] private TextMeshProUGUI player2CharacterName;
        [SerializeField] private TextMeshProUGUI player2PlayerName;
        [SerializeField] private GameObject player2ReadyIndicator;
        [SerializeField] private TextMeshProUGUI player2SelectionStatus;
        [SerializeField] private CharacterStatsDisplay player2StatsDisplay;

        [Header("Character Info")]
        [SerializeField] private TextMeshProUGUI characterInfoText;

        [Header("UI Controls")]
        [SerializeField] private Button readyButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TextMeshProUGUI statusText;

        private NetworkRunner _runner;
        private CharacterSelectionPlayer _localSelectionPlayer;
        private bool _isInitialized = false;
        private List<CharacterSelectionButton> _characterButtons = new List<CharacterSelectionButton>();
        private int _selectedCharacterIndex = -1;

        public bool IsInitialized => _isInitialized;

        private void Start()
        {
            SetupUI();
            StartCoroutine(InitializeWhenReady());
        }

        private void SetupUI()
        {
            // Setup button listeners
            if (readyButton != null)
                readyButton.onClick.AddListener(OnReadyButtonClicked);

            if (backButton != null)
                backButton.onClick.AddListener(OnBackButtonClicked);

            // Create character selection buttons dynamically
            CreateCharacterButtons();

            // Initialize displays
            ResetPlayerDisplays();
        }

        private void CreateCharacterButtons()
        {
            if (buttonPrefab == null || buttonContainer == null || characterOptions == null)
            {
                Debug.LogError("[CharacterSelectionCanvas] Missing button prefab, container, or character options");
                return;
            }

            // Clear existing buttons
            foreach (var button in _characterButtons)
            {
                if (button != null) DestroyImmediate(button.gameObject);
            }
            _characterButtons.Clear();

            // Create buttons for each character
            for (int i = 0; i < characterOptions.Length; i++)
            {
                var characterData = characterOptions[i];
                if (characterData == null) continue;

                var buttonObj = Instantiate(buttonPrefab, buttonContainer);
                var button = buttonObj.GetComponent<CharacterSelectionButton>();

                button.Initialize(characterData, i);
                button.OnCharacterSelected += OnCharacterButtonClicked;

                _characterButtons.Add(button);
            }

            Debug.Log($"[CharacterSelectionCanvas] Created {_characterButtons.Count} character buttons");
        }

        private IEnumerator InitializeWhenReady()
        {
            // Wait for NetworkRunner
            yield return new WaitUntil(() => FindObjectOfType<NetworkRunner>() != null);
            _runner = FindObjectOfType<NetworkRunner>();

            // Wait for CharacterSelectionState
            yield return new WaitUntil(() => CharacterSelectionState.Instance != null);

            // Subscribe to events
            CharacterSelectionState.Instance.OnStateSpawned += OnStateSpawned;
            CharacterSelectionState.Instance.OnSelectionChanged += OnSelectionChanged;

            // Wait for local player
            yield return new WaitUntil(() => FindLocalSelectionPlayer() != null);
            _localSelectionPlayer = FindLocalSelectionPlayer();

            _isInitialized = true;
            Debug.Log("[CharacterSelectionCanvas] Initialization complete");

            // Initial UI update
            UpdatePlayerDisplays();
            UpdateReadyButton();
        }

        private CharacterSelectionPlayer FindLocalSelectionPlayer()
        {
            return FindObjectsOfType<CharacterSelectionPlayer>()
                .FirstOrDefault(p => p.Object != null && p.Object.HasInputAuthority);
        }

        private void OnStateSpawned()
        {
            Debug.Log("[CharacterSelectionCanvas] OnStateSpawned event received");
            UpdatePlayerDisplays();
        }

        private void OnSelectionChanged()
        {
            Debug.Log("[CharacterSelectionCanvas] OnSelectionChanged event received");
            UpdatePlayerDisplays();
            UpdateCharacterButtonStates();
            UpdateReadyButton();
        }

        private void OnCharacterButtonClicked(int characterIndex)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[CharacterSelectionCanvas] Canvas not initialized yet");
                return;
            }

            if (_localSelectionPlayer == null)
            {
                Debug.LogError("[CharacterSelectionCanvas] Local selection player is null");
                return;
            }

            Debug.Log($"[CharacterSelectionCanvas] Character button clicked: {characterIndex}");

            // Update local selection immediately for responsive UI
            _selectedCharacterIndex = characterIndex;

            // Update character info immediately
            UpdateCharacterInfo(characterIndex);

            // Update button states immediately
            UpdateCharacterButtonStates();

            // Update local player display immediately
            UpdateLocalPlayerDisplay(characterIndex);

            // Send to network
            _localSelectionPlayer.RPC_RequestCharacterSelection(characterIndex);
        }

        private void UpdateCharacterButtonStates()
        {
            if (CharacterSelectionState.Instance == null) return;

            var selections = CharacterSelectionState.Instance.GetPlayerSelections();
            var selectedIndices = new HashSet<int>();

            // Collect all selected character indices
            foreach (var kvp in selections)
            {
                if (kvp.Value.CharacterIndex >= 0)
                {
                    selectedIndices.Add(kvp.Value.CharacterIndex);
                }
            }

            // Update button states
            for (int i = 0; i < _characterButtons.Count; i++)
            {
                var button = _characterButtons[i];
                var isSelected = i == _selectedCharacterIndex;
                var isLockedByOther = selectedIndices.Contains(i) && !isSelected;

                button.SetSelected(isSelected);
                button.SetLocked(isLockedByOther);
            }
        }

        private void UpdatePlayerDisplays()
        {
            if (CharacterSelectionState.Instance == null) return;

            var selections = CharacterSelectionState.Instance.GetPlayerSelections();

            // Reset displays
            ResetPlayerDisplays();

            // Update each player's display
            foreach (var kvp in selections)
            {
                var player = kvp.Key;
                var data = kvp.Value;
                var characterData = GetCharacterData(data.CharacterIndex);
                var isLocalPlayer = _runner != null && _runner.LocalPlayer == player;

                if (data.Slot == 1)
                {
                    UpdatePlayer1Display(characterData, player, isLocalPlayer);
                }
                else if (data.Slot == 2)
                {
                    UpdatePlayer2Display(characterData, player, isLocalPlayer);
                }
            }
        }

        private void ResetPlayerDisplays()
        {
            // Reset Player 1
            if (player1Portrait != null) player1Portrait.sprite = null;
            if (player1CharacterName != null) player1CharacterName.text = "";
            if (player1PlayerName != null) player1PlayerName.text = "Waiting...";
            if (player1ReadyIndicator != null) player1ReadyIndicator.SetActive(false);
            if (player1SelectionStatus != null) player1SelectionStatus.text = "Selecting...";
            if (player1StatsDisplay != null) player1StatsDisplay.Reset();

            // Reset Player 2
            if (player2Portrait != null) player2Portrait.sprite = null;
            if (player2CharacterName != null) player2CharacterName.text = "";
            if (player2PlayerName != null) player2PlayerName.text = "Waiting...";
            if (player2ReadyIndicator != null) player2ReadyIndicator.SetActive(false);
            if (player2SelectionStatus != null) player2SelectionStatus.text = "Selecting...";
            if (player2StatsDisplay != null) player2StatsDisplay.Reset();
        }

        private void UpdatePlayer1Display(CharacterData characterData, PlayerRef player, bool isLocalPlayer)
        {
            if (characterData != null)
            {
                if (player1Portrait != null)
                {
                    player1Portrait.sprite = characterData.CharacterPortrait ?? characterData.CharacterIcon;
                    player1Portrait.color = Color.white; // Ensure it's visible
                }

                if (player1CharacterName != null)
                    player1CharacterName.text = characterData.CharacterName;

                if (player1SelectionStatus != null)
                    player1SelectionStatus.text = "Selected!";

                // Update stats display
                if (player1StatsDisplay != null && characterData.Stats != null)
                {
                    player1StatsDisplay.UpdateStats(characterData.Stats);
                }
            }
            else
            {
                if (player1SelectionStatus != null)
                    player1SelectionStatus.text = "Selecting...";

                // Reset stats when no character selected
                if (player1StatsDisplay != null)
                    player1StatsDisplay.Reset();
            }

            if (player1PlayerName != null)
                player1PlayerName.text = isLocalPlayer ? "You" : $"Player {player}";

            Debug.Log($"[CharacterSelectionCanvas] Updated Player 1 Display - Character: {characterData?.CharacterName}, IsLocal: {isLocalPlayer}");
        }

        private void UpdatePlayer2Display(CharacterData characterData, PlayerRef player, bool isLocalPlayer)
        {
            if (characterData != null)
            {
                if (player2Portrait != null)
                {
                    player2Portrait.sprite = characterData.CharacterPortrait ?? characterData.CharacterIcon;
                    player2Portrait.color = Color.white; // Ensure it's visible
                }

                if (player2CharacterName != null)
                    player2CharacterName.text = characterData.CharacterName;

                if (player2SelectionStatus != null)
                    player2SelectionStatus.text = "Selected!";

                // Update stats display
                if (player2StatsDisplay != null && characterData.Stats != null)
                {
                    player2StatsDisplay.UpdateStats(characterData.Stats);
                }
            }
            else
            {
                if (player2SelectionStatus != null)
                    player2SelectionStatus.text = "Selecting...";

                // Reset stats when no character selected
                if (player2StatsDisplay != null)
                    player2StatsDisplay.Reset();
            }

            if (player2PlayerName != null)
                player2PlayerName.text = isLocalPlayer ? "You" : $"Player {player}";

            Debug.Log($"[CharacterSelectionCanvas] Updated Player 2 Display - Character: {characterData?.CharacterName}, IsLocal: {isLocalPlayer}");
        }

        private void UpdateLocalPlayerDisplay(int characterIndex)
        {
            var characterData = GetCharacterData(characterIndex);
            if (characterData == null) return;

            // Find which slot this local player is in
            if (CharacterSelectionState.Instance != null && _runner != null)
            {
                var selections = CharacterSelectionState.Instance.GetPlayerSelections();
                foreach (var kvp in selections)
                {
                    if (kvp.Key == _runner.LocalPlayer)
                    {
                        if (kvp.Value.Slot == 1)
                        {
                            UpdatePlayer1Display(characterData, kvp.Key, true);
                        }
                        else if (kvp.Value.Slot == 2)
                        {
                            UpdatePlayer2Display(characterData, kvp.Key, true);
                        }
                        break;
                    }
                }
            }
        }

        private void UpdateCharacterInfo(int characterIndex)
        {
            var characterData = GetCharacterData(characterIndex);
            if (characterData != null && characterInfoText != null)
            {
                string info = $"Selected: {characterData.CharacterName}";
                if (!string.IsNullOrEmpty(characterData.CharacterDescription))
                {
                    info += $"\n{characterData.CharacterDescription}";
                }

                characterInfoText.text = info;
            }
        }

        private CharacterData GetCharacterData(int characterIndex)
        {
            if (characterIndex >= 0 && characterIndex < characterOptions.Length)
            {
                return characterOptions[characterIndex];
            }
            return null;
        }

        private void UpdateReadyButton()
        {
            if (readyButton == null) return;

            bool canReady = _selectedCharacterIndex >= 0;
            readyButton.interactable = canReady;

            // Update status text
            if (statusText != null)
            {
                if (!canReady)
                {
                    statusText.text = "Select a character to continue";
                }
                else if (CharacterSelectionState.Instance?.IsAllPlayersReady() == true)
                {
                    statusText.text = "All players ready! Starting match...";
                }
                else
                {
                    statusText.text = "Waiting for other players...";
                }
            }
        }

        private void OnReadyButtonClicked()
        {
            Debug.Log("[CharacterSelectionCanvas] Ready button clicked");
            // TODO: Implement ready state logic
        }

        private void OnBackButtonClicked()
        {
            Debug.Log("[CharacterSelectionCanvas] Back button clicked");
            // TODO: Implement back to menu logic
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (CharacterSelectionState.Instance != null)
            {
                CharacterSelectionState.Instance.OnStateSpawned -= OnStateSpawned;
                CharacterSelectionState.Instance.OnSelectionChanged -= OnSelectionChanged;
            }

            // Clean up button events
            foreach (var button in _characterButtons)
            {
                if (button != null)
                {
                    button.OnCharacterSelected -= OnCharacterButtonClicked;
                }
            }
        }

        // Legacy methods for compatibility
        public void CheckForNetworkStateChanges()
        {
            if (_isInitialized)
            {
                UpdatePlayerDisplays();
                UpdateCharacterButtonStates();
            }
        }

        public void InitializeFromBridge()
        {
            if (!_isInitialized && CharacterSelectionState.Instance != null)
            {
                _runner = FindObjectOfType<NetworkRunner>();
                _localSelectionPlayer = FindLocalSelectionPlayer();
                _isInitialized = true;
                UpdatePlayerDisplays();
                Debug.Log("[CharacterSelectionCanvas] InitializeFromBridge completed");
            }
        }
    }
}