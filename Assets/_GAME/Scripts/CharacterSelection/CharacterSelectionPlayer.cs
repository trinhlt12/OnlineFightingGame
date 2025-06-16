using _GAME.Scripts.CharacterSelection;
using Fusion;
using UnityEngine;

public class CharacterSelectionPlayer : NetworkBehaviour
{
    [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
    public void RPC_SelectCharacter(int characterIndex)
    {
        CharacterSelectionState.Instance.SetCharacter(Object.InputAuthority, characterIndex);
    }

    public override void Spawned()
    {
        base.Spawned();
        if (HasInputAuthority)
            Debug.Log("This CharacterSelectionPlayer belongs to me (local client)");
        else
            Debug.Log("CharacterSelectionPlayer for another player");
    }

}