using System.Collections.Generic;
using System.Linq;
using _GAME.Scripts.CharacterSelection;
using _GAME.Scripts.Data;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class CharacterSelectionCanvas : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject player1Display;
        [SerializeField] private GameObject player2Display;
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Player Display Components")]
        [SerializeField] private PlayerDisplayUI player1UI;
        [SerializeField] private PlayerDisplayUI player2UI;

        [Header("Character Selection")]
        [SerializeField] private Transform characterSelectionContainer;
        [SerializeField] private GameObject characterButtonPrefab;

        [Header("Character Data")]
        [SerializeField] private CharacterData[] availableCharacters;

        private NetworkRunner _runner;
        private CharacterSelectionPlayer _localPlayer;
        private List<CharacterSelectionButton> _characterButtons = new List<CharacterSelectionButton>();
        private bool _isInitialized = false;
        private bool _isLocalPlayerReady = false;

        public bool IsInitialized => _isInitialized;

        private void Awake()
        {
            // Setup button listeners
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartButtonClicked);
                startButton.gameObject.SetActive(false); // Hidden by default
            }

            if (readyButton != null)
                readyButton.onClick.AddListener(OnReadyButtonClicked);

            if (backButton != null)
                backButton.onClick.AddListener(OnBackButtonClicked);
        }

        public void InitializeFromBridge()
        {
            if (_isInitialized) return;

            _runner = FindObjectOfType<NetworkRunner>();
            if (_runner == null)
            {
                Debug.LogError("[CharacterSelectionCanvas] NetworkRunner not found!");
                return;
            }

            _localPlayer = FindLocalCharacterSelectionPlayer();
            if (_localPlayer == null)
            {
                Debug.LogError("[CharacterSelectionCanvas] Local CharacterSelectionPlayer not found!");
                return;
            }

            SetupCharacterSelection();
            UpdateUI();

            _isInitialized = true;
            Debug.Log("[CharacterSelectionCanvas] Initialized successfully");
        }

        private CharacterSelectionPlayer FindLocalCharacterSelectionPlayer()
        {
            var players = FindObjectsOfType<CharacterSelectionPlayer>();
            return players.FirstOrDefault(p => p.Object.HasInputAuthority);
        }

        private void SetupCharacterSelection()
        {
            if (characterSelectionContainer == null || characterButtonPrefab == null) return;

            // Clear existing buttons
            foreach (var button in _characterButtons)
            {
                if (button != null) DestroyImmediate(button.gameObject);
            }
            _characterButtons.Clear();

            // Create character selection buttons
            for (int i = 0; i < availableCharacters.Length; i++)
            {
                var characterData = availableCharacters[i];
                var buttonObj = Instantiate(characterButtonPrefab, characterSelectionContainer);
                var button = buttonObj.GetComponent<CharacterSelectionButton>();

                if (button != null)
                {
                    button.Initialize(characterData, i);
                    button.OnCharacterSelected += OnCharacterSelected;
                    _characterButtons.Add(button);
                }
            }
        }

        public void CheckForNetworkStateChanges()
        {
            if (!_isInitialized) return;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (CharacterSelectionState.Instance == null) return;

            var selections = CharacterSelectionState.Instance.GetPlayerSelections();
            var isServer = _runner != null && _runner.IsServer;

            // Update player displays
            UpdatePlayerDisplays(selections);

            // Update character selection buttons
            UpdateCharacterButtons(selections);

            // Update start button visibility
            UpdateStartButton(isServer, selections);

            // Update ready button
            UpdateReadyButton();

            // Update status text
            UpdateStatusText(selections);
        }

        private void UpdatePlayerDisplays(IReadOnlyDictionary<PlayerRef, PlayerSelectionData> selections)
        {
            var playerRefs = selections.Keys.ToArray();

            // Update Player 1 Display
            if (playerRefs.Length > 0 && player1UI != null)
            {
                var player1Data = selections[playerRefs[0]];
                bool isLocalPlayer = _runner.LocalPlayer == playerRefs[0];
                player1UI.UpdateDisplay(playerRefs[0], player1Data, availableCharacters, isLocalPlayer);
            }

            // Update Player 2 Display
            if (playerRefs.Length > 1 && player2UI != null)
            {
                var player2Data = selections[playerRefs[1]];
                bool isLocalPlayer = _runner.LocalPlayer == playerRefs[1];
                player2UI.UpdateDisplay(playerRefs[1], player2Data, availableCharacters, isLocalPlayer);
            }
        }

        private void UpdateCharacterButtons(IReadOnlyDictionary<PlayerRef, PlayerSelectionData> selections)
        {
            var selectedCharacters = selections.Values.Select(data => data.CharacterIndex).ToHashSet();

            for (int i = 0; i < _characterButtons.Count; i++)
            {
                var button = _characterButtons[i];
                if (button == null) continue;

                // Check if this character is selected by local player
                var localPlayerData = selections.FirstOrDefault(kvp => kvp.Key == _runner.LocalPlayer);
                bool isSelectedByLocal = localPlayerData.Key != PlayerRef.None && localPlayerData.Value.CharacterIndex == i;

                // Check if this character is locked (selected by someone else)
                bool isLocked = selectedCharacters.Contains(i) && !isSelectedByLocal;

                button.SetSelected(isSelectedByLocal);
                button.SetLocked(isLocked);
            }
        }

        private void UpdateStartButton(bool isServer, IReadOnlyDictionary<PlayerRef, PlayerSelectionData> selections)
        {
            if (startButton == null) return;

            bool canStart = isServer && CharacterSelectionState.Instance.CanStartGame();
            startButton.gameObject.SetActive(canStart);
        }

        private void UpdateReadyButton()
        {
            if (readyButton == null) return;

            var localPlayerData = GetLocalPlayerData();
            if (localPlayerData.HasValue)
            {
                _isLocalPlayerReady = localPlayerData.Value.IsReady;
                var buttonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = _isLocalPlayerReady ? "Unready" : "Ready";
                }

                // Only allow ready if character is selected
                readyButton.interactable = localPlayerData.Value.CharacterIndex >= 0;
            }
        }

        private void UpdateStatusText(IReadOnlyDictionary<PlayerRef, PlayerSelectionData> selections)
        {
            if (statusText == null) return;

            if (selections.Count < 2)
            {
                statusText.text = "Waiting for players...";
            }
            else if (CharacterSelectionState.Instance.CanStartGame())
            {
                statusText.text = _runner.IsServer ? "Ready to start!" : "Waiting for server to start...";
            }
            else
            {
                var readyCount = selections.Values.Count(data => data.IsReady && data.CharacterIndex >= 0);
                statusText.text = $"Players ready: {readyCount}/{selections.Count}";
            }
        }

        private PlayerSelectionData? GetLocalPlayerData()
        {
            if (CharacterSelectionState.Instance == null || _runner == null) return null;

            var selections = CharacterSelectionState.Instance.GetPlayerSelections();
            if (selections.TryGetValue(_runner.LocalPlayer, out var data))
            {
                return data;
            }
            return null;
        }

        private void OnCharacterSelected(int characterIndex)
        {
            if (_localPlayer == null) return;

            Debug.Log($"[CharacterSelectionCanvas] Character {characterIndex} selected");
            _localPlayer.RPC_RequestCharacterSelection(characterIndex);
        }

        private void OnReadyButtonClicked()
        {
            if (_localPlayer == null) return;

            var localPlayerData = GetLocalPlayerData();
            if (localPlayerData.HasValue && localPlayerData.Value.CharacterIndex >= 0)
            {
                bool newReadyState = !_isLocalPlayerReady;
                Debug.Log($"[CharacterSelectionCanvas] Ready button clicked - new state: {newReadyState}");
                _localPlayer.RPC_RequestReadyToggle(newReadyState);
            }
        }

        private void OnStartButtonClicked()
        {
            if (_localPlayer == null || !_runner.IsServer) return;

            Debug.Log("[CharacterSelectionCanvas] Start button clicked");
            _localPlayer.RPC_RequestStartGame();
        }

        private void OnBackButtonClicked()
        {
            Debug.Log("[CharacterSelectionCanvas] Back button clicked");
            // TODO: Implement back to main menu logic
        }

        private void OnDestroy()
        {
            // Clean up button listeners
            foreach (var button in _characterButtons)
            {
                if (button != null)
                {
                    button.OnCharacterSelected -= OnCharacterSelected;
                }
            }
        }
    }

    [System.Serializable]
    public class PlayerDisplayUI
    {
        [Header("Player Display References")]
        public TextMeshProUGUI playerNameText;
        public TextMeshProUGUI characterNameText;
        public Image characterIcon;
        public GameObject readyIndicator;
        public TextMeshProUGUI selectionStatusText;

        [Header("Character Stats Display")]
        public CharacterStatsDisplay characterStatsDisplay;

        public void UpdateDisplay(PlayerRef playerRef, PlayerSelectionData playerData, CharacterData[] availableCharacters, bool isLocalPlayer)
        {
            // Update player name
            if (playerNameText != null)
            {
                string playerName = isLocalPlayer ? "You" : $"Player {playerData.Slot}";
                playerNameText.text = playerName;
            }

            // Update character info and stats
            if (playerData.CharacterIndex >= 0 && playerData.CharacterIndex < availableCharacters.Length)
            {
                var characterData = availableCharacters[playerData.CharacterIndex];

                if (characterNameText != null)
                    characterNameText.text = characterData.CharacterName;

                if (characterIcon != null)
                    characterIcon.sprite = characterData.CharacterIcon;

                // Update character stats
                if (characterStatsDisplay != null)
                {
                    if (characterData.Stats != null)
                    {
                        Debug.Log($"[PlayerDisplayUI] Updating stats for {characterData.CharacterName} - Speed: {characterData.Stats.Speed}, Strength: {characterData.Stats.Strength}");
                        characterStatsDisplay.UpdateStats(characterData.Stats);
                    }
                    else
                    {
                        Debug.LogWarning($"[PlayerDisplayUI] Character {characterData.CharacterName} has null Stats!");
                        characterStatsDisplay.Reset();
                    }
                }
                else
                {
                    Debug.LogWarning($"[PlayerDisplayUI] CharacterStatsDisplay is null for player {playerData.Slot}!");
                }
            }
            else
            {
                if (characterNameText != null)
                    characterNameText.text = "No character selected";

                if (characterIcon != null)
                    characterIcon.sprite = null;

                // Reset stats when no character selected
                if (characterStatsDisplay != null)
                {
                    Debug.Log("[PlayerDisplayUI] Resetting stats - no character selected");
                    characterStatsDisplay.Reset();
                }
            }

            // Update ready indicator
            if (readyIndicator != null)
            {
                readyIndicator.SetActive(playerData.IsReady);
            }

            // Update selection status
            if (selectionStatusText != null)
            {
                if (playerData.CharacterIndex >= 0)
                {
                    selectionStatusText.text = playerData.IsReady ? "Ready!" : "Not Ready";
                }
                else
                {
                    selectionStatusText.text = "Selecting...";
                }
            }
        }
    }
}