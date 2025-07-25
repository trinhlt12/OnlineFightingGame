using _GAME.Scripts.CharacterSelection;
using Fusion;
using UnityEngine;

public class CharacterSelectionPlayer : NetworkBehaviour
{
    public override void Spawned()
    {
        base.Spawned();
        if (HasInputAuthority)
        {
            Debug.Log($"This CharacterSelectionPlayer belongs to me (local client) - InputAuthority: {Object.InputAuthority}");
        }
        else
        {
            Debug.Log($"CharacterSelectionPlayer for another player - InputAuthority: {Object.InputAuthority}");
        }
    }

    // RPC method for character selection
    [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
    public void RPC_RequestCharacterSelection(int characterIndex)
    {
        // Use Object.InputAuthority instead of RpcInfo.Source for more reliability
        var playerRef = Object.InputAuthority;

        Debug.Log($"[CharacterSelectionPlayer] Received character selection request: Player {playerRef}, Character {characterIndex}");

        // Validate PlayerRef
        if (playerRef == PlayerRef.None)
        {
            Debug.LogError("[CharacterSelectionPlayer] Invalid PlayerRef (PlayerNone)!");
            return;
        }

        // Ensure player is in the dictionary before trying to set selection
        if (CharacterSelectionState.Instance != null)
        {
            // Check if player exists, if not add them
            var selections = CharacterSelectionState.Instance.GetPlayerSelections();
            if (!selections.ContainsKey(playerRef))
            {
                Debug.LogWarning($"[CharacterSelectionPlayer] Player {playerRef} not found in selections, triggering PlayerJoined");
                CharacterSelectionState.Instance.PlayerJoined(playerRef);
            }

            CharacterSelectionState.Instance.SetCharacterSelection(playerRef, characterIndex);
        }
        else
        {
            Debug.LogError("[CharacterSelectionPlayer] CharacterSelectionState.Instance is null!");
        }
    }

    // RPC method for ready state
    [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
    public void RPC_RequestReadyToggle(bool isReady)
    {
        var playerRef = Object.InputAuthority;

        Debug.Log($"[CharacterSelectionPlayer] Received ready toggle request: Player {playerRef}, IsReady {isReady}");

        // Validate PlayerRef
        if (playerRef == PlayerRef.None)
        {
            Debug.LogError("[CharacterSelectionPlayer] Invalid PlayerRef (PlayerNone)!");
            return;
        }

        // Ensure player is in the dictionary before trying to set ready state
        if (CharacterSelectionState.Instance != null)
        {
            var selections = CharacterSelectionState.Instance.GetPlayerSelections();
            if (!selections.ContainsKey(playerRef))
            {
                Debug.LogWarning($"[CharacterSelectionPlayer] Player {playerRef} not found in selections, triggering PlayerJoined");
                CharacterSelectionState.Instance.PlayerJoined(playerRef);
            }

            CharacterSelectionState.Instance.SetPlayerReady(playerRef, isReady);
        }
        else
        {
            Debug.LogError("[CharacterSelectionPlayer] CharacterSelectionState.Instance is null!");
        }
    }

    // RPC method for starting the game (Host only)
    [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
    public void RPC_RequestStartGame()
    {
        var playerRef = Object.InputAuthority;

        Debug.Log($"[CharacterSelectionPlayer] Received start game request from Player {playerRef}");

        // Only allow if this is coming from the server
        if (!HasStateAuthority || Object.InputAuthority != Runner.LocalPlayer)
        {
            Debug.LogWarning("[CharacterSelectionPlayer] Start game request denied - not from server");
            return;
        }

        if (CharacterSelectionState.Instance != null)
        {
            Debug.Log("[CharacterSelectionPlayer] Delegating to CharacterSelectionState for transition");
            // Delegate to CharacterSelectionState to handle the transition
            // This ensures proper broadcasting to all clients
            CharacterSelectionState.Instance.TriggerGameStartTransition();
        }
        else
        {
            Debug.LogError("[CharacterSelectionPlayer] CharacterSelectionState.Instance is null!");
        }
    }
}