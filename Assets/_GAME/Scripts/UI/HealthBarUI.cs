using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace _GAME.Scripts.UI
{
    /// <summary>
    /// Individual health bar component for fighting game
    /// Handles visual representation and animations for a single player's health
    /// Uses Image Fill instead of Sliders for better performance and customization
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        [Header("Health Bar Components")]
        [SerializeField] private Image healthFillImage; // Main health fill
        [SerializeField] private Image damageFillImage; // Background damage fill animation
        [SerializeField] private Image healthBarBackground; // Background/border
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI healthText; // Optional: show HP numbers

        [Header("Visual Configuration")]
        [SerializeField] private Gradient healthColorGradient;
        [SerializeField] private Color damageColor = Color.red;
        [SerializeField] private Color backgroundTint = Color.gray;

        [Header("Fill Direction")]
        [SerializeField] private Image.FillMethod fillMethod = Image.FillMethod.Horizontal;
        [SerializeField] private bool invertFill = false; // Set true for right-side health bar

        [Header("Animation Settings")]
        [SerializeField] private float damageAnimationDelay = 0.5f;
        [SerializeField] private float damageAnimationSpeed = 2f;
        [SerializeField] private bool enableDamageAnimation = true;

        [Header("Effects")]
        [SerializeField] private GameObject lowHealthEffect;
        [SerializeField] private float lowHealthThreshold = 0.25f;
        [SerializeField] private CanvasGroup flashCanvasGroup;

        // Private variables
        private float currentHealth = 100f;
        private float maxHealth = 100f;
        private bool isLeftSide = true;
        private string playerName = "";
        private Coroutine damageAnimationCoroutine;

        /// <summary>
        /// Initialize the health bar
        /// </summary>
        public void Initialize(string playerName, bool isLeftSide)
        {
            this.playerName = playerName;
            this.isLeftSide = isLeftSide;

            SetupHealthBar();
            SetPlayerName(playerName);

            // Set initial values
            SetHealth(maxHealth, maxHealth);
        }

        /// <summary>
        /// Setup health bar components and orientation
        /// </summary>
        private void SetupHealthBar()
        {
            if (healthFillImage != null)
            {
                // Configure health fill image
                healthFillImage.type = Image.Type.Filled;
                healthFillImage.fillMethod = fillMethod;

                // Set fill direction based on side
                if (fillMethod == Image.FillMethod.Horizontal)
                {
                    healthFillImage.fillOrigin = isLeftSide ? 0 : 1; // 0 = Left, 1 = Right
                }

                healthFillImage.fillAmount = 1f;
            }

            if (damageFillImage != null)
            {
                // Configure damage fill image (background)
                damageFillImage.type = Image.Type.Filled;
                damageFillImage.fillMethod = fillMethod;
                damageFillImage.color = damageColor;

                // Set same fill direction as health
                if (fillMethod == Image.FillMethod.Horizontal)
                {
                    damageFillImage.fillOrigin = isLeftSide ? 0 : 1;
                }

                damageFillImage.fillAmount = 1f;
            }

            // Setup background tint
            if (healthBarBackground != null)
                healthBarBackground.color = backgroundTint;

            // Setup canvas group for flash effects
            if (flashCanvasGroup == null)
                flashCanvasGroup = GetComponent<CanvasGroup>();
        }

        /// <summary>
        /// Set player name display
        /// </summary>
        public void SetPlayerName(string name)
        {
            if (playerNameText != null)
                playerNameText.text = name;
        }

        /// <summary>
        /// Update health values and visual display
        /// </summary>
        public void SetHealth(float currentHP, float maxHP)
        {
            if (maxHP <= 0) maxHP = 1f; // Prevent division by zero

            this.currentHealth = Mathf.Clamp(currentHP, 0f, maxHP);
            this.maxHealth = maxHP;

            float healthPercentage = this.currentHealth / this.maxHealth;

            // Update main health fill
            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = healthPercentage;

                // Update health color based on percentage
                if (healthColorGradient != null)
                    healthFillImage.color = healthColorGradient.Evaluate(healthPercentage);
            }

            // Update health text
            if (healthText != null)
                healthText.text = $"{Mathf.RoundToInt(this.currentHealth)}/{Mathf.RoundToInt(this.maxHealth)}";

            // Handle damage animation
            if (enableDamageAnimation && damageFillImage != null)
                HandleDamageAnimation(healthPercentage);

            // Handle low health effects
            HandleLowHealthEffects(healthPercentage);
        }

        /// <summary>
        /// Handle damage animation (red fill following health decrease)
        /// </summary>
        private void HandleDamageAnimation(float targetHealthPercentage)
        {
            if (damageFillImage.fillAmount > targetHealthPercentage)
            {
                // Start damage animation if not already running
                if (damageAnimationCoroutine != null)
                    StopCoroutine(damageAnimationCoroutine);

                damageAnimationCoroutine = StartCoroutine(AnimateDamage(targetHealthPercentage));
            }
            else
            {
                // Health increased or stayed same, update damage fill immediately
                damageFillImage.fillAmount = targetHealthPercentage;
            }
        }

        /// <summary>
        /// Animate damage fill following health decrease
        /// </summary>
        private System.Collections.IEnumerator AnimateDamage(float targetValue)
        {
            // Wait for delay before starting animation
            yield return new WaitForSeconds(damageAnimationDelay);

            float startValue = damageFillImage.fillAmount;
            float elapsed = 0f;
            float animationDuration = (startValue - targetValue) * damageAnimationSpeed;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animationDuration;
                damageFillImage.fillAmount = Mathf.Lerp(startValue, targetValue, t);
                yield return null;
            }

            damageFillImage.fillAmount = targetValue;
            damageAnimationCoroutine = null;
        }

        /// <summary>
        /// Handle low health visual effects
        /// </summary>
        private void HandleLowHealthEffects(float healthPercentage)
        {
            bool isLowHealth = healthPercentage <= lowHealthThreshold;

            // Toggle low health effect
            if (lowHealthEffect != null)
                lowHealthEffect.SetActive(isLowHealth);

            // Add flash effect for very low health
            if (isLowHealth && flashCanvasGroup != null)
            {
                // Simple pulsing effect
                float pulse = Mathf.PingPong(Time.time * 3f, 1f);
                flashCanvasGroup.alpha = Mathf.Lerp(0.7f, 1f, pulse);
            }
            else if (flashCanvasGroup != null)
            {
                flashCanvasGroup.alpha = 1f;
            }
        }

        /// <summary>
        /// Trigger flash effect when taking damage
        /// </summary>
        public void FlashDamage()
        {
            if (flashCanvasGroup != null)
                StartCoroutine(FlashEffect());
        }

        /// <summary>
        /// Flash effect coroutine
        /// </summary>
        private System.Collections.IEnumerator FlashEffect()
        {
            float originalAlpha = flashCanvasGroup.alpha;

            // Flash white
            flashCanvasGroup.alpha = 0.3f;
            yield return new WaitForSeconds(0.1f);

            flashCanvasGroup.alpha = originalAlpha;
        }

        /// <summary>
        /// Animate health bar entry (for round start)
        /// </summary>
        public void AnimateEntry()
        {
            StartCoroutine(EntryAnimation());
        }

        private System.Collections.IEnumerator EntryAnimation()
        {
            if (healthFillImage == null) yield break;

            // Start from empty
            float targetValue = healthFillImage.fillAmount;
            healthFillImage.fillAmount = 0f;

            if (damageFillImage != null)
                damageFillImage.fillAmount = 0f;

            // Animate to full
            float elapsed = 0f;
            float duration = 1f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float currentValue = Mathf.Lerp(0f, targetValue, t);

                healthFillImage.fillAmount = currentValue;
                if (damageFillImage != null)
                    damageFillImage.fillAmount = currentValue;

                yield return null;
            }

            healthFillImage.fillAmount = targetValue;
            if (damageFillImage != null)
                damageFillImage.fillAmount = targetValue;
        }

        // ==================== PUBLIC GETTERS ====================

        /// <summary>
        /// Get current health value
        /// </summary>
        public float GetCurrentHealth() => currentHealth;

        /// <summary>
        /// Get max health value
        /// </summary>
        public float GetMaxHealth() => maxHealth;

        /// <summary>
        /// Get health percentage (0-1)
        /// </summary>
        public float GetHealthPercentage() => currentHealth / maxHealth;

        /// <summary>
        /// Check if health is at low threshold
        /// </summary>
        public bool IsLowHealth() => GetHealthPercentage() <= lowHealthThreshold;

        // ==================== EDITOR UTILITIES ====================

#if UNITY_EDITOR
        [Header("Editor Testing")]
        [SerializeField] private bool testInEditor = false;

        private void Update()
        {
            if (!testInEditor || !Application.isPlaying) return;

            // Test health changes with number keys
            if (Input.GetKeyDown(KeyCode.Alpha1))
                SetHealth(100f, 100f);
            if (Input.GetKeyDown(KeyCode.Alpha2))
                SetHealth(75f, 100f);
            if (Input.GetKeyDown(KeyCode.Alpha3))
                SetHealth(50f, 100f);
            if (Input.GetKeyDown(KeyCode.Alpha4))
                SetHealth(25f, 100f);
            if (Input.GetKeyDown(KeyCode.Alpha5))
                SetHealth(0f, 100f);

            if (Input.GetKeyDown(KeyCode.F))
                FlashDamage();
        }

        /// <summary>
        /// Validate component setup in editor
        /// </summary>
        private void OnValidate()
        {
            if (healthFillImage == null)
                healthFillImage = GetComponentInChildren<Image>();

            // Auto-setup fill properties if image is assigned
            if (healthFillImage != null && healthFillImage.type != Image.Type.Filled)
            {
                healthFillImage.type = Image.Type.Filled;
                healthFillImage.fillMethod = Image.FillMethod.Horizontal;
            }
        }
#endif
    }
}