using UnityEngine;
using _GAME.Scripts.CharacterSelection;
using Fusion;
using UI;

public class CharacterSelectionDebug : MonoBehaviour
{
    [Header("Debug Controls")]
    [SerializeField] private KeyCode debugKey = KeyCode.F1;
    [SerializeField] private KeyCode forceReadyKey = KeyCode.F2;
    [SerializeField] private bool enableDebugLogs = true;

    private void Update()
    {
        if (Input.GetKeyDown(debugKey))
        {
            LogDebugInfo();
        }

        if (Input.GetKeyDown(forceReadyKey))
        {
            ForceToggleReady();
        }
    }

    private void LogDebugInfo()
    {
        if (!enableDebugLogs) return;

        Debug.Log("=== CHARACTER SELECTION DEBUG INFO ===");

        var runner = FindObjectOfType<NetworkRunner>();
        if (runner != null)
        {
            Debug.Log($"[Debug] NetworkRunner - IsHost: {runner.IsServer}, IsClient: {runner.IsClient}, LocalPlayer: {runner.LocalPlayer}");
            Debug.Log($"[Debug] NetworkRunner - IsRunning: {runner.IsRunning}, SessionInfo: {runner.SessionInfo?.Name}");
        }

        // Check CharacterSelectionState
        if (CharacterSelectionState.Instance != null)
        {
            Debug.Log($"[Debug] CharacterSelectionState - HasStateAuthority: {CharacterSelectionState.Instance.HasStateAuthority}");

            var selections = CharacterSelectionState.Instance.GetPlayerSelections();
            Debug.Log($"[Debug] Player selections count: {selections.Count}");

            foreach (var kvp in selections)
            {
                Debug.Log($"[Debug] Player {kvp.Key} - Slot: {kvp.Value.Slot}, Character: {kvp.Value.CharacterIndex}, Ready: {kvp.Value.IsReady}");

                // Check if this is local player
                bool isLocal = runner != null && runner.LocalPlayer == kvp.Key;
                Debug.Log($"[Debug] Player {kvp.Key} - IsLocal: {isLocal}");
            }

            Debug.Log($"[Debug] CanStartGame: {CharacterSelectionState.Instance.CanStartGame()}");
            Debug.Log($"[Debug] IsAllPlayersReady: {CharacterSelectionState.Instance.IsAllPlayersReady()}");

            // Log detailed state
            /*
            CharacterSelectionState.Instance.LogSelectionState();
        */
        }
        else
        {
            Debug.LogError("[Debug] CharacterSelectionState.Instance is NULL!");
        }

        // Check CharacterSelectionCanvas
        var canvas = FindObjectOfType<CharacterSelectionCanvas>();
        if (canvas != null)
        {
            Debug.Log($"[Debug] CharacterSelectionCanvas found - IsInitialized: {canvas.IsInitialized}");
        }
        else
        {
            Debug.LogError("[Debug] CharacterSelectionCanvas not found!");
        }

        // Check CharacterSelectionPlayer objects
        var players = FindObjectsOfType<CharacterSelectionPlayer>();
        Debug.Log($"[Debug] Found {players.Length} CharacterSelectionPlayer objects");

        foreach (var player in players)
        {
            if (player.Object != null)
            {
                Debug.Log($"[Debug] CharacterSelectionPlayer - HasInputAuthority: {player.Object.HasInputAuthority}, InputAuthority: {player.Object.InputAuthority}, HasStateAuthority: {player.Object.HasStateAuthority}");
            }
        }

        Debug.Log("=== END DEBUG INFO ===");
    }

    // Method to manually trigger UI refresh
    public void ForceUIRefresh()
    {
        var canvas = FindObjectOfType<CharacterSelectionCanvas>();
        if (canvas != null && canvas.IsInitialized)
        {
            Debug.Log("[Debug] Forcing UI refresh...");
            canvas.CheckForNetworkStateChanges();
        }
    }

    // Method to manually toggle ready state for debugging
    private void ForceToggleReady()
    {
        var localPlayer = FindObjectsOfType<CharacterSelectionPlayer>();
        var myPlayer = System.Array.Find(localPlayer, p => p.Object.HasInputAuthority);

        if (myPlayer != null && CharacterSelectionState.Instance != null)
        {
            var runner = FindObjectOfType<NetworkRunner>();
            if (runner != null)
            {
                bool currentReady = CharacterSelectionState.Instance.GetPlayerReadyState(runner.LocalPlayer);
                Debug.Log($"[Debug] Force toggling ready state from {currentReady} to {!currentReady}");
                myPlayer.RPC_RequestReadyToggle(!currentReady);
            }
        }
        else
        {
            Debug.LogWarning("[Debug] Cannot toggle ready - local player not found");
        }
    }

    // Method to simulate character selection for debugging
    public void ForceSelectCharacter(int characterIndex)
    {
        var localPlayer = FindObjectsOfType<CharacterSelectionPlayer>();
        var myPlayer = System.Array.Find(localPlayer, p => p.Object.HasInputAuthority);

        if (myPlayer != null)
        {
            Debug.Log($"[Debug] Force selecting character {characterIndex}");
            myPlayer.RPC_RequestCharacterSelection(characterIndex);
        }
        else
        {
            Debug.LogWarning("[Debug] Cannot select character - local player not found");
        }
    }
}