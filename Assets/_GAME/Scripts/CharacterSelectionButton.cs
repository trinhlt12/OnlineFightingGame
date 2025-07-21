using UnityEngine;
using UnityEngine.UI;
using TMPro;
using _GAME.Scripts.Data;

namespace UI
{
    public class CharacterSelectionButton : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image characterIcon;
        [SerializeField] private TextMeshProUGUI characterNameText;
        [SerializeField] private Button button;
        [SerializeField] private GameObject selectionBorder; // Optional
        [SerializeField] private Image backgroundImage;

        private CharacterData _characterData;
        private int _characterIndex;
        private bool _isSelected = false;
        private bool _isLocked = false;

        // Events
        public System.Action<int> OnCharacterSelected;

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(OnButtonClicked);
            }
        }

        public void Initialize(CharacterData characterData, int characterIndex)
        {
            _characterData = characterData;
            _characterIndex = characterIndex;

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (_characterData == null) return;

            // Update basic visuals
            if (characterIcon != null)
                characterIcon.sprite = _characterData.CharacterIcon;

            if (characterNameText != null)
                characterNameText.text = _characterData.CharacterName;

            if (backgroundImage != null)
                backgroundImage.color = _characterData.PrimaryColor;

            // Update selection state
            UpdateSelectionState();
            UpdateLockState();
        }

        public void SetSelected(bool selected)
        {
            if (_isSelected == selected) return;

            _isSelected = selected;
            UpdateSelectionState();
        }

        public void SetLocked(bool locked)
        {
            if (_isLocked == locked) return;

            _isLocked = locked;
            UpdateLockState();
        }

        private void UpdateSelectionState()
        {
            if (selectionBorder != null)
            {
                selectionBorder.SetActive(_isSelected);
            }

            // Color feedback
            if (characterIcon != null)
            {
                Color iconColor = _isSelected ? Color.white : new Color(1f, 1f, 1f, 0.8f);
                characterIcon.color = iconColor;
            }
        }

        private void UpdateLockState()
        {
            if (button != null)
            {
                button.interactable = !_isLocked;
            }

            // Visual feedback for locked state
            if (characterIcon != null)
            {
                Color iconColor = _isLocked ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : Color.white;
                characterIcon.color = iconColor;
            }
        }

        private void OnButtonClicked()
        {
            if (_isLocked) return;

            Debug.Log($"[CharacterSelectionButton] Character selected: {_characterData.CharacterName}");
            OnCharacterSelected?.Invoke(_characterIndex);
        }

        public CharacterData GetCharacterData() => _characterData;
        public int GetCharacterIndex() => _characterIndex;
        public bool IsSelected() => _isSelected;
        public bool IsLocked() => _isLocked;
    }
}