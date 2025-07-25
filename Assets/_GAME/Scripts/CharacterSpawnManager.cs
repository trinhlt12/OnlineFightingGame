using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Fusion;
using _GAME.Scripts.CharacterSelection;
using _GAME.Scripts.Data;

public class CharacterSpawnManager : NetworkBehaviour
{
    [Header("Spawn Configuration")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Character Data")]
    [SerializeField] private CharacterData[] availableCharacters;

    // Track spawned characters for cleanup and management
    private Dictionary<PlayerRef, NetworkObject> spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

    public override void Spawned()
    {
        // Only server should handle spawning logic
        if (!HasStateAuthority)
        {
            if (enableDebugLogs)
                Debug.Log("[CharacterSpawnManager] Client instance - will receive spawned characters from server");
            return;
        }

        if (enableDebugLogs)
            Debug.Log("[CharacterSpawnManager] Server instance ready - waiting for spawn request");
    }

    /// <summary>
    /// Main method to spawn characters based on selection data
    /// Should be called when transitioning from Map Selection to Game
    /// </summary>
    public void SpawnSelectedCharacters()
    {
        // Only server can spawn characters
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[CharacterSpawnManager] SpawnSelectedCharacters called on client - ignoring");
            return;
        }

        if (CharacterSelectionState.Instance == null)
        {
            Debug.LogError("[CharacterSpawnManager] CharacterSelectionState.Instance is null - cannot spawn characters");
            return;
        }

        if (enableDebugLogs)
            Debug.Log("[CharacterSpawnManager] Starting character spawning process");

        // Get player selections from the character selection state
        var playerSelections = CharacterSelectionState.Instance.GetPlayerSelections();

        if (playerSelections.Count == 0)
        {
            Debug.LogError("[CharacterSpawnManager] No player selections found - cannot spawn characters");
            return;
        }

        // Validate that we have enough spawn points
        if (spawnPoints.Length < playerSelections.Count)
        {
            Debug.LogError($"[CharacterSpawnManager] Not enough spawn points! Need {playerSelections.Count}, have {spawnPoints.Length}");
            return;
        }

        // Create list of available spawn positions and shuffle them for randomization
        var availableSpawnPositions = new List<Transform>(spawnPoints);
        ShuffleSpawnPositions(availableSpawnPositions);

        if (enableDebugLogs)
            Debug.Log($"[CharacterSpawnManager] Spawning {playerSelections.Count} characters at randomized positions");

        int spawnIndex = 0;

        // Spawn character for each player
        foreach (var playerSelection in playerSelections)
        {
            var playerRef = playerSelection.Key;
            var selectionData = playerSelection.Value;

            // Validate character selection
            if (selectionData.CharacterIndex < 0 || selectionData.CharacterIndex >= availableCharacters.Length)
            {
                Debug.LogError($"[CharacterSpawnManager] Invalid character index {selectionData.CharacterIndex} for player {playerRef}");
                continue;
            }

            // Get character data and spawn position
            var characterData = availableCharacters[selectionData.CharacterIndex];
            var spawnPosition = availableSpawnPositions[spawnIndex];

            // Spawn the character
            SpawnCharacterForPlayer(playerRef, characterData, spawnPosition);

            spawnIndex++;
        }

        if (enableDebugLogs)
            Debug.Log("[CharacterSpawnManager] Character spawning process completed");
    }

    /// <summary>
    /// Spawns a character for a specific player at a specific position
    /// </summary>
    private void SpawnCharacterForPlayer(PlayerRef playerRef, CharacterData characterData, Transform spawnTransform)
    {
        if (characterData.characterPrefab == null)
        {
            Debug.LogError($"[CharacterSpawnManager] Character prefab is null for {characterData.CharacterName}");
            return;
        }

        // Check if character prefab has NetworkObject component
        var networkObjectComponent = characterData.characterPrefab.GetComponent<NetworkObject>();
        if (networkObjectComponent == null)
        {
            Debug.LogError($"[CharacterSpawnManager] Character prefab {characterData.CharacterName} does not have NetworkObject component");
            return;
        }

        // Check if character prefab has NetworkTransform component
        var networkTransformComponent = characterData.characterPrefab.GetComponent<NetworkTransform>();
        if (networkTransformComponent == null)
        {
            Debug.LogError($"[CharacterSpawnManager] Character prefab {characterData.CharacterName} does not have NetworkTransform component. Adding it automatically.");
        }

        if (enableDebugLogs)
            Debug.Log($"[CharacterSpawnManager] Spawning {characterData.CharacterName} for player {playerRef} at position {spawnTransform.position}");

        // Spawn the networked character object
        var spawnedCharacter = Runner.Spawn(
            characterData.characterPrefab,
            spawnTransform.position,
            spawnTransform.rotation,
            playerRef  // This player will have input authority over this character
        );

        if (spawnedCharacter != null)
        {
            // Store reference for management
            spawnedCharacters[playerRef] = spawnedCharacter;

            // Initialize character with data and explicit position
            InitializeSpawnedCharacter(spawnedCharacter, characterData, playerRef, spawnTransform.position, spawnTransform.rotation);

            if (enableDebugLogs)
                Debug.Log($"[CharacterSpawnManager] Successfully spawned {characterData.CharacterName} for player {playerRef} at {spawnTransform.position}");
        }
        else
        {
            Debug.LogError($"[CharacterSpawnManager] Failed to spawn character for player {playerRef}");
        }
    }

    /// <summary>
    /// Initialize spawned character with specific data and position
    /// </summary>
    private void InitializeSpawnedCharacter(NetworkObject spawnedCharacter, CharacterData characterData, PlayerRef playerRef, Vector3 position, Quaternion rotation)
    {
        // Initialize NetworkedCharacterSetup with position data
        var characterSetup = spawnedCharacter.GetComponent<NetworkedCharacterSetup>();
        if (characterSetup != null)
        {
            characterSetup.SetCharacterData(characterData, position, rotation);
            if (enableDebugLogs)
                Debug.Log($"[CharacterSpawnManager] Initialized NetworkedCharacterSetup for {characterData.CharacterName} at {position}");
        }
        else
        {
            Debug.LogWarning($"[CharacterSpawnManager] NetworkedCharacterSetup component not found on {characterData.CharacterName}");
        }

        // Try to get Player component and initialize it
        var playerComponent = spawnedCharacter.GetComponent<Player>();
        if (playerComponent != null)
        {
            // Set any player-specific data here
            if (enableDebugLogs)
                Debug.Log($"[CharacterSpawnManager] Initialized Player component for {characterData.CharacterName}");
        }

        // Ensure proper NetworkTransform synchronization
        var networkTransform = spawnedCharacter.GetComponent<NetworkTransform>();
        if (networkTransform != null)
        {
            // Force position update for NetworkTransform
            spawnedCharacter.transform.position = position;
            spawnedCharacter.transform.rotation = rotation;

            if (enableDebugLogs)
                Debug.Log($"[CharacterSpawnManager] Set NetworkTransform position for {characterData.CharacterName} to {position}");
        }
        else
        {
            Debug.LogError($"[CharacterSpawnManager] NetworkTransform component missing on {characterData.CharacterName}! Position sync will not work properly.");
        }
    }

    /// <summary>
    /// Shuffles spawn positions for randomization
    /// Uses Fisher-Yates shuffle algorithm for true randomness
    /// </summary>
    private void ShuffleSpawnPositions(List<Transform> positions)
    {
        // Fisher-Yates shuffle for true randomization
        for (int i = positions.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            Transform temp = positions[i];
            positions[i] = positions[randomIndex];
            positions[randomIndex] = temp;
        }

        if (enableDebugLogs)
        {
            Debug.Log("[CharacterSpawnManager] Shuffled spawn positions:");
            for (int i = 0; i < positions.Count; i++)
            {
                Debug.Log($"  Position {i}: {positions[i].name} at {positions[i].position}");
            }
        }
    }

    /// <summary>
    /// Clean up spawned characters (useful for scene transitions)
    /// </summary>
    public void DespawnAllCharacters()
    {
        if (!HasStateAuthority)
        {
            Debug.LogWarning("[CharacterSpawnManager] DespawnAllCharacters called on client - ignoring");
            return;
        }

        if (enableDebugLogs)
            Debug.Log($"[CharacterSpawnManager] Despawning {spawnedCharacters.Count} characters");

        foreach (var kvp in spawnedCharacters)
        {
            if (kvp.Value != null)
            {
                Runner.Despawn(kvp.Value);
                if (enableDebugLogs)
                    Debug.Log($"[CharacterSpawnManager] Despawned character for player {kvp.Key}");
            }
        }

        spawnedCharacters.Clear();
    }

    /// <summary>
    /// Get spawned character for a specific player
    /// </summary>
    public NetworkObject GetSpawnedCharacter(PlayerRef playerRef)
    {
        return spawnedCharacters.TryGetValue(playerRef, out var character) ? character : null;
    }

    /// <summary>
    /// Get all spawned characters
    /// </summary>
    public Dictionary<PlayerRef, NetworkObject> GetAllSpawnedCharacters()
    {
        return new Dictionary<PlayerRef, NetworkObject>(spawnedCharacters);
    }

    /// <summary>
    /// Debug method to manually trigger spawning
    /// </summary>
    [ContextMenu("Debug Spawn Characters")]
    public void DebugSpawnCharacters()
    {
        if (Application.isPlaying)
        {
            SpawnSelectedCharacters();
        }
        else
        {
            Debug.LogWarning("[CharacterSpawnManager] Debug spawn only works in play mode");
        }
    }

    /// <summary>
    /// Validate spawn configuration in editor
    /// </summary>
    private void OnValidate()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[CharacterSpawnManager] No spawn points assigned!");
        }

        if (availableCharacters == null || availableCharacters.Length == 0)
        {
            Debug.LogWarning("[CharacterSpawnManager] No available characters assigned!");
        }
    }

    /// <summary>
    /// Get original spawn position for a player
    /// </summary>
    public Vector3 GetPlayerSpawnPosition(PlayerRef playerRef)
    {
        if (spawnedCharacters.TryGetValue(playerRef, out var character) && character != null)
        {
            // Try to get stored spawn position from NetworkedCharacterSetup
            var characterSetup = character.GetComponent<NetworkedCharacterSetup>();
            if (characterSetup != null)
            {
                return characterSetup.NetworkPosition;
            }

            // Fallback: use current position
            return character.transform.position;
        }

        return Vector3.zero;
    }
    public void ResetAllPlayersToSpawnPositions()
    {
        foreach (var kvp in spawnedCharacters)
        {
            PlayerRef     playerRef = kvp.Key;
            NetworkObject character = kvp.Value;

            if (character != null)
            {
                var characterSetup = character.GetComponent<NetworkedCharacterSetup>();
                if (characterSetup != null)
                {
                    Vector3 spawnPos = characterSetup.NetworkPosition;
                    character.transform.position = spawnPos;

                    if (enableDebugLogs)
                        Debug.Log($"[CharacterSpawnManager] Reset player {playerRef} to spawn position: {spawnPos}");
                }
            }
        }
    }
    public Dictionary<PlayerRef, Vector3> GetAllPlayerSpawnPositions()
    {
        var positions = new Dictionary<PlayerRef, Vector3>();

        foreach (var kvp in spawnedCharacters)
        {
            PlayerRef     playerRef = kvp.Key;
            NetworkObject character = kvp.Value;

            if (character != null)
            {
                var characterSetup = character.GetComponent<NetworkedCharacterSetup>();
                if (characterSetup != null)
                {
                    positions[playerRef] = characterSetup.NetworkPosition;
                }
            }
        }

        return positions;
    }
}