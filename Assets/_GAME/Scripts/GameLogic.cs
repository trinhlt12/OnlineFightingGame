using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

public class GameLogic : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    [SerializeField]          private NetworkPrefabRef                     playerPrefab;
    [SerializeField]          private CharacterSelectionPlayer       characterSelectionPlayerPrefab;
    [Networked, Capacity(12)] private NetworkDictionary<PlayerRef, Player> Players => default;

    public void PlayerJoined(PlayerRef player)
    {
        Debug.Log($"Player [{player}] joined the game.");

        if (!HasStateAuthority)
            return;

        bool alreadyHas = FindObjectsOfType<CharacterSelectionPlayer>()
            .Any(p => p.Object.HasInputAuthority && p.Object.InputAuthority == player);

        if (!alreadyHas)
        {
            Runner.Spawn(characterSelectionPlayerPrefab, inputAuthority: player);
            Debug.Log($"[GameLogic] Spawned CharacterSelectionPlayer for {player}");
        }
    }


    public void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (Players.TryGet(player, out Player playerBehaviour))
        {
            Players.Remove(player);
            Runner.Despawn(playerBehaviour.Object);
        }
    }
}