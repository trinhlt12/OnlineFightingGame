using Fusion;
using UnityEngine;

namespace _GAME.Scripts.CharacterSelection
{
    using System;
    using System.Collections.Generic;

    public class CharacterSelectionState : NetworkBehaviour, IPlayerJoined
    {
        public static CharacterSelectionState Instance { get; private set; }

        // Event for UI updates
        public event Action OnSelectionChanged;
        public event Action OnStateSpawned;

        [Networked, Capacity(2)]
        private NetworkDictionary<PlayerRef, PlayerSelectionData> PlayerSelections => default;

        private ChangeDetector _changeDetector;

        public override void Spawned()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(this.gameObject);

                _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

                Debug.Log("CharacterSelectionState spawned and initialized.");
                OnStateSpawned?.Invoke();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public override void Render()
        {
            // Check for changes every render frame and notify UI
            if (_changeDetector != null)
            {
                foreach (var change in _changeDetector.DetectChanges(this, out var previousBuffer, out var currentBuffer))
                {
                    if (change == nameof(PlayerSelections))
                    {
                        Debug.Log("[CharacterSelectionState] Selection change detected, notifying UI");
                        OnSelectionChanged?.Invoke();
                        break;
                    }
                }
            }
        }

        public void PlayerJoined(PlayerRef player)
        {
            if (HasStateAuthority && !PlayerSelections.ContainsKey(player))
            {
                var slotNumber = PlayerSelections.Count + 1;
                PlayerSelections.Add(player, new PlayerSelectionData
                {
                    Slot = slotNumber,
                    CharacterIndex = -1
                });

                Debug.Log($"Player {player} joined with slot {slotNumber}");

                // Notify UI immediately
                OnSelectionChanged?.Invoke();
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

        // Main method for character selection - called from UI
        public void SetCharacterSelection(PlayerRef player, int characterIndex)
        {
            if (HasStateAuthority && PlayerSelections.ContainsKey(player))
            {
                var data = PlayerSelections[player];
                data.CharacterIndex = characterIndex;
                PlayerSelections.Set(player, data);

                Debug.Log($"[CharacterSelectionState] Player {player} selected character index {characterIndex}");

                // Force immediate UI update for state authority
                OnSelectionChanged?.Invoke();
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

        public bool IsAllPlayersReady()
        {
            if (PlayerSelections.Count < 2) return false;

            foreach (var kvp in PlayerSelections)
            {
                if (kvp.Value.CharacterIndex < 0) return false;
            }
            return true;
        }
    }

    public struct PlayerSelectionData : INetworkStruct
    {
        public int Slot;
        public int CharacterIndex;
    }
}