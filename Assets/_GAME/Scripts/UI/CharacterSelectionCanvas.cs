namespace UI
{
    using System;
    using System.Collections;
    using System.Linq;
    using _GAME.Scripts.CharacterSelection;
    using _GAME.Scripts.Data;
    using Fusion;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    public class CharacterSelectionCanvas : UICanvas
    {
        [SerializeField] private CharacterData[] characterOptions;

        [Header("UI References")] [SerializeField] private Image           player1Portrait;
        [SerializeField]                           private TextMeshProUGUI player1Name;
        [SerializeField]                           private Image           player2Portrait;
        [SerializeField]                           private TextMeshProUGUI player2Name;

        private NetworkRunner            _runner;
        private CharacterSelectionPlayer _localSelectionPlayer;

        private IEnumerator Start()
        {
            _runner = FindObjectOfType<NetworkRunner>();

            var timeout = 2f;
            while (timeout > 0f)
            {
                _localSelectionPlayer = FindObjectsOfType<CharacterSelectionPlayer>()
                    .FirstOrDefault(p => p.HasInputAuthority);

                if (_localSelectionPlayer != null)
                {
                    Debug.Log("✅ Found local CharacterSelectionPlayer");
                    break;
                }

                timeout -= Time.deltaTime;
                yield return null;
            }

            if (_localSelectionPlayer == null)
            {
                Debug.LogError("❌ Still no local CharacterSelectionPlayer after waiting!");
            }

            InvokeRepeating(nameof(UpdatePortraits), 0.1f, 0.2f);
        }


        public void OnCharacterButtonClicked(int characterIndex)
        {
            if (this._runner == null || CharacterSelectionState.Instance == null) return;

            if (_localSelectionPlayer == null)
            {
                Debug.LogWarning("No local CharacterSelectionPlayer found");
                return;
            }

            _localSelectionPlayer.RPC_SelectCharacter(characterIndex);
        }

        private void UpdatePortraits()
        {
            var selections = CharacterSelectionState.Instance?.GetPlayerSelections();
            if (selections == null) return;

            foreach (var kvp in selections)
            {
                var player = kvp.Key;
                var data   = kvp.Value;

                if (data.CharacterIndex < 0 || data.CharacterIndex >= this.characterOptions.Length) continue;

                var sprite = this.characterOptions[data.CharacterIndex].CharacterIcon;
                if (data.Slot == 1)
                {
                    this.player1Portrait.sprite = sprite;
                    this.player1Name.text       = this.characterOptions[data.CharacterIndex].CharacterName;
                }
                else if (data.Slot == 2)
                {
                    this.player2Portrait.sprite = sprite;
                    this.player2Name.text       = this.characterOptions[data.CharacterIndex].CharacterName;
                }
            }
        }
    }
}