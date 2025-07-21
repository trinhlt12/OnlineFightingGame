namespace _GAME.Scripts.Data
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "CharacterData", menuName = "GAME/CharacterData", order = 1)]
    public class CharacterData : ScriptableObject
    {
        [Header("Basic Info")]
        public string CharacterName;
        public string CharacterDescription;

        [Header("Visual Assets")]
        public Sprite CharacterIcon;
        public Sprite CharacterPortrait; // Larger image for selection screen
        public GameObject characterPrefab; // Keep original name for compatibility

        [Header("UI Colors")]
        public Color PrimaryColor = Color.white;
        public Color SecondaryColor = Color.gray;

        [Header("Character Stats")]
        public CharacterStats Stats = new CharacterStats();

        [Header("Audio")]
        public AudioClip SelectionSound;
        public AudioClip VoiceLine;

        // Unique identifier for networking
        public int CharacterID => name.GetHashCode();
    }

    [System.Serializable]
    public class CharacterStats
    {
        [Header("Main Stats (1-10)")]
        [Range(1, 10)] public int Speed = 5;
        [Range(1, 10)] public int Strength = 5;
        [Range(1, 10)] public int Defense = 5;
        [Range(1, 10)] public int Agility = 5;

        [Header("Special Ability")]
        public string SpecialAbilityName = "Default Ability";
        [TextArea(2, 4)]
        public string SpecialAbilityDescription = "Character special ability description";
        public Sprite SpecialAbilityIcon;

        [Header("Stat Colors (Optional)")]
        public Color SpeedColor = Color.cyan;
        public Color StrengthColor = Color.red;
        public Color DefenseColor = Color.blue;
        public Color AgilityColor = Color.green;

        // Helper methods to normalize stats (0-1 range for sliders)
        public float GetSpeedNormalized() => Speed / 10f;
        public float GetStrengthNormalized() => Strength / 10f;
        public float GetDefenseNormalized() => Defense / 10f;
        public float GetAgilityNormalized() => Agility / 10f;

        // Constructor to set default values
        public CharacterStats()
        {
            Speed = 5;
            Strength = 5;
            Defense = 5;
            Agility = 5;
            SpecialAbilityName = "Default Ability";
            SpecialAbilityDescription = "Character special ability description";
            SpeedColor = Color.cyan;
            StrengthColor = Color.red;
            DefenseColor = Color.blue;
            AgilityColor = Color.green;
        }
    }
}