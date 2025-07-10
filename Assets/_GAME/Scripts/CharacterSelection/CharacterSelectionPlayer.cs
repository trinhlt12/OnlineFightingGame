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
            Debug.Log("This CharacterSelectionPlayer belongs to me (local client)");
        }
        else
        {
            Debug.Log("CharacterSelectionPlayer for another player");
        }
    }

    // Single RPC method for character selection
    [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
    public void RPC_RequestCharacterSelection(int characterIndex, RpcInfo info = default)
    {
        var playerRef = info.Source;

        Debug.Log($"[CharacterSelectionPlayer] Received character selection request: Player {playerRef}, Character {characterIndex}");

        // Directly call the state method
        if (CharacterSelectionState.Instance != null)
        {
            CharacterSelectionState.Instance.SetCharacterSelection(playerRef, characterIndex);
        }
        else
        {
            Debug.LogError("[CharacterSelectionPlayer] CharacterSelectionState.Instance is null!");
        }
    }
}