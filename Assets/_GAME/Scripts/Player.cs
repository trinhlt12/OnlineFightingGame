using System.Collections;
using System.Collections.Generic;
using _GAME.Scripts.CharacterSelection;
using Fusion;
using Fusion.Addons.SimpleKCC;
using UnityEngine;

public class Player : NetworkBehaviour
{
    [Networked] public  int            PlayerSlot      { get; private set; }

    public override void Spawned()
    {
        base.Spawned();
        if (HasStateAuthority)
        {
            if (CharacterSelectionState.Instance != null)
            {
                PlayerSlot = CharacterSelectionState.Instance.GetPlayerSlot(Object.InputAuthority);

            }
            else
            {
                //Fallback:
                Debug.LogWarning("CharacterSelectionState not found. Cannot assign PlayerSlot.");
            }
        }

    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetInput input))
        {

        }
    }

}