namespace _GAME.Scripts.CharacterSelection
{
    using Fusion;
    using UnityEngine;

    public class PlayerSlotManager :  NetworkBehaviour, IPlayerJoined
    {
        public static                    PlayerSlotManager                 Instance { get; private set; }
        [Networked][Capacity(2)] private NetworkDictionary<PlayerRef, int> Slots    => default;
        private                          int                               _nextSlot = 1;

        public override void Spawned()
        {
            if(Instance == null) Instance = this;
            else Destroy(gameObject);
        }


        public void PlayerJoined(PlayerRef player)
        {
            if (HasStateAuthority && !Slots.ContainsKey(player))
            {
                Slots.Add(player, _nextSlot++);
            }
        }

        public int GetSlot(PlayerRef player)
        {
            return Slots.TryGet(player, out var slot) ? slot : -1;
        }
    }
}