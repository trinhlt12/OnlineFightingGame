namespace _GAME.Scripts.Data
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "CharacterData", menuName = "GAME/CharacterData", order = 1)]
    public class CharacterData : ScriptableObject
    {
        public string CharacterName;
        public Sprite CharacterIcon;
        public GameObject characterPrefab;

        //stats:
    }
}