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

    // Single RPC method for character selection
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
}