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
            PlayerSlot = PlayerSlotManager.Instance.GetSlot(Object.InputAuthority);
        }

    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetInput input))
        {

        }
    }

}