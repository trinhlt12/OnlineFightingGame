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
                        Debug.Log($"[CharacterSelectionState] Selection change detected on {(HasStateAuthority ? "Host" : "Client")}, notifying UI");
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
            Debug.Log($"[CharacterSelectionState] SetCharacterSelection called - Player: {player}, Character: {characterIndex}, HasStateAuthority: {HasStateAuthority}");

            if (!HasStateAuthority)
            {
                Debug.LogWarning($"[CharacterSelectionState] SetCharacterSelection called on non-authority client - ignoring");
                return;
            }

            if (!PlayerSelections.ContainsKey(player))
            {
                Debug.LogError($"[CharacterSelectionState] Player {player} not found in PlayerSelections dictionary. Available players:");
                foreach (var kvp in PlayerSelections)
                {
                    Debug.LogError($"  - Player: {kvp.Key}, Slot: {kvp.Value.Slot}");
                }

                // Try to add the player
                Debug.Log($"[CharacterSelectionState] Attempting to add missing player {player}");
                PlayerJoined(player);

                // Check again
                if (!PlayerSelections.ContainsKey(player))
                {
                    Debug.LogError($"[CharacterSelectionState] Failed to add player {player} to dictionary");
                    return;
                }
            }

            var data = PlayerSelections[player];
            data.CharacterIndex = characterIndex;
            PlayerSelections.Set(player, data);

            Debug.Log($"[CharacterSelectionState] Successfully updated Player {player} selection to character {characterIndex}");

            // Force immediate UI update for state authority
            OnSelectionChanged?.Invoke();
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

        // Manual method to force UI sync - useful for debugging
        public void ForceUISync()
        {
            Debug.Log($"[CharacterSelectionState] ForceUISync called on {(HasStateAuthority ? "Host" : "Client")}");
            OnSelectionChanged?.Invoke();
        }

        // Method to get detailed selection info for debugging
        public void LogSelectionState()
        {
            Debug.Log($"[CharacterSelectionState] === Selection State on {(HasStateAuthority ? "Host" : "Client")} ===");
            Debug.Log($"[CharacterSelectionState] PlayerSelections.Count: {PlayerSelections.Count}");

            foreach (var kvp in PlayerSelections)
            {
                Debug.Log($"[CharacterSelectionState] Player {kvp.Key}: Slot {kvp.Value.Slot}, Character {kvp.Value.CharacterIndex}");
            }
        }
    }

    public struct PlayerSelectionData : INetworkStruct
    {
        public int Slot;
        public int CharacterIndex;
    }
}