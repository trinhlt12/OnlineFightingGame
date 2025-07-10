using UnityEngine;
using UnityEngine.UI;
using TMPro;
using _GAME.Scripts.Data;
using System.Collections;

namespace UI
{
    public class CharacterStatsDisplay : MonoBehaviour
    {
        [Header("Stat Bars")]
        [SerializeField] private Slider speedBar;
        [SerializeField] private Slider strengthBar;
        [SerializeField] private Slider defenseBar;
        [SerializeField] private Slider agilityBar;

        [Header("Stat Labels (Optional)")]
        [SerializeField] private TextMeshProUGUI speedLabel;
        [SerializeField] private TextMeshProUGUI strengthLabel;
        [SerializeField] private TextMeshProUGUI defenseLabel;
        [SerializeField] private TextMeshProUGUI agilityLabel;

        [Header("Stat Values (Optional)")]
        [SerializeField] private TextMeshProUGUI speedValue;
        [SerializeField] private TextMeshProUGUI strengthValue;
        [SerializeField] private TextMeshProUGUI defenseValue;
        [SerializeField] private TextMeshProUGUI agilityValue;

        [Header("Animation Settings")]
        [SerializeField] private bool animateOnUpdate = true;
        [SerializeField] private float animationDuration = 0.5f;
        [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Coroutine _animationCoroutine;

        private void Awake()
        {
            InitializeSliders();
            SetupLabels();
        }

        private void InitializeSliders()
        {
            // Setup all sliders
            var sliders = new Slider[] { speedBar, strengthBar, defenseBar, agilityBar };

            foreach (var slider in sliders)
            {
                if (slider != null)
                {
                    slider.minValue = 0f;
                    slider.maxValue = 1f;
                    slider.value = 0f;
                    slider.interactable = false; // Display only
                }
            }
        }

        private void SetupLabels()
        {
            // Set default label texts
            if (speedLabel != null) speedLabel.text = "Speed";
            if (strengthLabel != null) strengthLabel.text = "Strength";
            if (defenseLabel != null) defenseLabel.text = "Defense";
            if (agilityLabel != null) agilityLabel.text = "Agility";
        }

        public void UpdateStats(CharacterStats stats)
        {
            if (stats == null)
            {
                Reset();
                return;
            }

            Debug.Log($"[CharacterStatsDisplay] Updating stats - Speed: {stats.Speed}, Strength: {stats.Strength}, Defense: {stats.Defense}, Agility: {stats.Agility}");

            if (animateOnUpdate)
            {
                AnimateStatsUpdate(stats);
            }
            else
            {
                SetStatsImmediate(stats);
            }
        }

        private void SetStatsImmediate(CharacterStats stats)
        {
            // Update sliders
            if (speedBar != null) speedBar.value = stats.GetSpeedNormalized();
            if (strengthBar != null) strengthBar.value = stats.GetStrengthNormalized();
            if (defenseBar != null) defenseBar.value = stats.GetDefenseNormalized();
            if (agilityBar != null) agilityBar.value = stats.GetAgilityNormalized();

            // Update value texts
            if (speedValue != null) speedValue.text = stats.Speed.ToString();
            if (strengthValue != null) strengthValue.text = stats.Strength.ToString();
            if (defenseValue != null) defenseValue.text = stats.Defense.ToString();
            if (agilityValue != null) agilityValue.text = stats.Agility.ToString();

            // Update colors
            UpdateStatColors(stats);
        }

        private void AnimateStatsUpdate(CharacterStats stats)
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }

            _animationCoroutine = StartCoroutine(AnimateStatsCoroutine(stats));
        }

        private IEnumerator AnimateStatsCoroutine(CharacterStats stats)
        {
            // Store starting values
            float startSpeed = speedBar != null ? speedBar.value : 0f;
            float startStrength = strengthBar != null ? strengthBar.value : 0f;
            float startDefense = defenseBar != null ? defenseBar.value : 0f;
            float startAgility = agilityBar != null ? agilityBar.value : 0f;

            // Target values
            float targetSpeed = stats.GetSpeedNormalized();
            float targetStrength = stats.GetStrengthNormalized();
            float targetDefense = stats.GetDefenseNormalized();
            float targetAgility = stats.GetAgilityNormalized();

            float elapsed = 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / animationDuration;
                float curve = animationCurve.Evaluate(t);

                // Animate sliders
                if (speedBar != null)
                    speedBar.value = Mathf.Lerp(startSpeed, targetSpeed, curve);
                if (strengthBar != null)
                    strengthBar.value = Mathf.Lerp(startStrength, targetStrength, curve);
                if (defenseBar != null)
                    defenseBar.value = Mathf.Lerp(startDefense, targetDefense, curve);
                if (agilityBar != null)
                    agilityBar.value = Mathf.Lerp(startAgility, targetAgility, curve);

                // Update value texts during animation
                if (speedValue != null)
                    speedValue.text = Mathf.RoundToInt(Mathf.Lerp(startSpeed * 10, stats.Speed, curve)).ToString();
                if (strengthValue != null)
                    strengthValue.text = Mathf.RoundToInt(Mathf.Lerp(startStrength * 10, stats.Strength, curve)).ToString();
                if (defenseValue != null)
                    defenseValue.text = Mathf.RoundToInt(Mathf.Lerp(startDefense * 10, stats.Defense, curve)).ToString();
                if (agilityValue != null)
                    agilityValue.text = Mathf.RoundToInt(Mathf.Lerp(startAgility * 10, stats.Agility, curve)).ToString();

                yield return null;
            }

            // Ensure final values are exact
            SetStatsImmediate(stats);
        }

        private void UpdateStatColors(CharacterStats stats)
        {
            // Update slider colors if the stats have custom colors
            if (speedBar != null && speedBar.fillRect != null)
            {
                var speedFill = speedBar.fillRect.GetComponent<Image>();
                if (speedFill != null) speedFill.color = stats.SpeedColor;
            }

            if (strengthBar != null && strengthBar.fillRect != null)
            {
                var strengthFill = strengthBar.fillRect.GetComponent<Image>();
                if (strengthFill != null) strengthFill.color = stats.StrengthColor;
            }

            if (defenseBar != null && defenseBar.fillRect != null)
            {
                var defenseFill = defenseBar.fillRect.GetComponent<Image>();
                if (defenseFill != null) defenseFill.color = stats.DefenseColor;
            }

            if (agilityBar != null && agilityBar.fillRect != null)
            {
                var agilityFill = agilityBar.fillRect.GetComponent<Image>();
                if (agilityFill != null) agilityFill.color = stats.AgilityColor;
            }
        }

        public void Reset()
        {
            Debug.Log("[CharacterStatsDisplay] Resetting stats display");

            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }

            // Reset all sliders to 0
            if (speedBar != null) speedBar.value = 0f;
            if (strengthBar != null) strengthBar.value = 0f;
            if (defenseBar != null) defenseBar.value = 0f;
            if (agilityBar != null) agilityBar.value = 0f;

            // Reset value texts
            if (speedValue != null) speedValue.text = "0";
            if (strengthValue != null) strengthValue.text = "0";
            if (defenseValue != null) defenseValue.text = "0";
            if (agilityValue != null) agilityValue.text = "0";
        }

        // Helper method to set specific stat
        public void SetStatValue(StatType statType, float normalizedValue)
        {
            switch (statType)
            {
                case StatType.Speed:
                    if (speedBar != null) speedBar.value = normalizedValue;
                    break;
                case StatType.Strength:
                    if (strengthBar != null) strengthBar.value = normalizedValue;
                    break;
                case StatType.Defense:
                    if (defenseBar != null) defenseBar.value = normalizedValue;
                    break;
                case StatType.Agility:
                    if (agilityBar != null) agilityBar.value = normalizedValue;
                    break;
            }
        }
    }

    public enum StatType
    {
        Speed,
        Strength,
        Defense,
        Agility
    }
}