using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _GAME.Scripts.UI
{
    /// <summary>
    /// Simple health bar for fighting game - Game Jam Version
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        [Header("Required Components")]
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Image damageFillImage;
        [SerializeField] private TextMeshProUGUI playerNameText;

        [Header("Configuration")]
        [SerializeField] private bool isLeftSide = true;
        [SerializeField] private Color healthColor = Color.green;
        [SerializeField] private Color lowHealthColor = Color.red;
        [SerializeField] private Color damageColor = Color.red;

        private float currentHealth = 100f;
        private float maxHealth = 100f;

        private void Start()
        {
            SetupFillImages();
            SetHealth(100f, 100f);
        }

        private void SetupFillImages()
        {
            // Setup health fill
            if (healthFillImage != null)
            {
                healthFillImage.type = Image.Type.Filled;
                healthFillImage.fillMethod = Image.FillMethod.Horizontal;
                healthFillImage.fillOrigin = isLeftSide ? 0 : 1;
                healthFillImage.color = healthColor;
            }

            // Setup damage fill
            if (damageFillImage != null)
            {
                damageFillImage.type = Image.Type.Filled;
                damageFillImage.fillMethod = Image.FillMethod.Horizontal;
                damageFillImage.fillOrigin = isLeftSide ? 0 : 1;
                damageFillImage.color = damageColor;
            }
        }

        public void Initialize(string playerName, bool leftSide)
        {
            isLeftSide = leftSide;

            if (playerNameText != null)
                playerNameText.text = playerName;

            SetupFillImages();
        }

        public void SetHealth(float current, float max)
        {
            currentHealth = Mathf.Clamp(current, 0f, max);
            maxHealth = max;

            float percentage = currentHealth / maxHealth;

            // Update health fill
            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = percentage;
                healthFillImage.color = percentage > 0.3f ? healthColor : lowHealthColor;
            }

            // Update damage fill with simple delay
            if (damageFillImage != null)
            {
                if (damageFillImage.fillAmount > percentage)
                {
                    Invoke(nameof(UpdateDamageFill), 0.3f);
                }
                else
                {
                    damageFillImage.fillAmount = percentage;
                }
            }
        }

        private void UpdateDamageFill()
        {
            if (damageFillImage != null && healthFillImage != null)
                damageFillImage.fillAmount = healthFillImage.fillAmount;
        }

        public float GetCurrentHealth() => currentHealth;
        public float GetHealthPercentage() => currentHealth / maxHealth;
    }
}