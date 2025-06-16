using Fusion;
using UnityEngine;

namespace _GAME.Scripts.CharacterSelection
{
    using System.Collections.Generic;

    public class CharacterSelectionState : NetworkBehaviour, IPlayerJoined
    {
        public static CharacterSelectionState Instance { get; private set; }

        [Networked, Capacity(2)]
        private NetworkDictionary<PlayerRef, PlayerSelectionData> PlayerSelections => default;

        public override void Spawned()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void PlayerJoined(PlayerRef player)
        {
            if (HasStateAuthority && !PlayerSelections.ContainsKey(player))
            {
                PlayerSelections.Add(player, new PlayerSelectionData
                {
                    Slot           = PlayerSelections.Count + 1,
                    CharacterIndex = -1
                });
                Debug.Log($"Player {player} joined with slot {PlayerSelections[player].Slot}");
            }
        }

        public int GetPlayerSlot(PlayerRef player)
        {
            return PlayerSelections.TryGet(player, out var data) ? data.Slot : -1;
        }

        public int GetSelectedCharacter(PlayerRef player)
        {
            return PlayerSelections.TryGet(player, out var data) ? data.CharacterIndex : -1;
        }

        [Rpc(sources: RpcSources.InputAuthority, targets: RpcTargets.StateAuthority)]
        public void RPC_SelectCharacter(int characterIndex, RpcInfo info = default)
        {
            var player = info.Source;

            if (PlayerSelections.ContainsKey(player))
            {
                PlayerSelectionData newData = PlayerSelections[player];
                newData.CharacterIndex = characterIndex;
                PlayerSelections.Set(player, newData);
            }
        }


        public IReadOnlyDictionary<PlayerRef, PlayerSelectionData> GetPlayerSelections()
        {
            Dictionary<PlayerRef, PlayerSelectionData> copy = new();

            foreach (var kvp in PlayerSelections)
            {
                copy[kvp.Key] = kvp.Value;
            }

            return copy;
        }

        public void SetCharacter(PlayerRef player, int characterIndex)
        {
            if (PlayerSelections.ContainsKey(player))
            {
                var data = PlayerSelections[player];
                data.CharacterIndex = characterIndex;
                PlayerSelections.Set(player, data);
            }
        }

    }

    public struct PlayerSelectionData : INetworkStruct
    {
        public int Slot;
        public int CharacterIndex;
    }
}