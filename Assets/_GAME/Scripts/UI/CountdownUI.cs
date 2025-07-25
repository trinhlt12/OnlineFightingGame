using UnityEngine;
using TMPro;
using Fusion;

namespace _GAME.Scripts.UI
{
    /// <summary>
    /// Shows "3", "2", "1", "FIGHT!" before each round
    /// </summary>
    public class CountdownUI : NetworkBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject countdownPanel;
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private TextMeshProUGUI roundInfoText;

        [Header("Settings")]
        [SerializeField] private float textAnimationScale = 1.5f;
        [SerializeField] private Color countdownColor = Color.white;
        [SerializeField] private Color fightColor = Color.red;

        private bool                           isAnimating = false;
        private _GAME.Scripts.Core.GameManager cachedGameManager;

        public override void Spawned()
        {
            HideCountdown();
            cachedGameManager = FindObjectOfType<_GAME.Scripts.Core.GameManager>();

        }

        public override void Render()
        {
            // Update countdown display every frame
            UpdateCountdownDisplay();
        }

        // ==================== PUBLIC API ====================

        public void ShowCountdown(int roundNumber)
        {
            if (countdownPanel != null)
                countdownPanel.SetActive(true);

            if (roundInfoText != null)
                roundInfoText.text = $"ROUND {roundNumber}";
        }

        public void HideCountdown()
        {
            if (countdownPanel != null)
                countdownPanel.SetActive(false);

            isAnimating = false;
        }

        // ==================== PRIVATE METHODS ====================

        private void UpdateCountdownDisplay()
        {
            if (cachedGameManager == null || countdownText == null) return;

            // Only show during countdown state
            if (cachedGameManager.GetCurrentState() != _GAME.Scripts.Core.GameManager.GameState.Countdown)
            {
                HideCountdown();
                return;
            }

            // Show countdown panel
            if (countdownPanel != null && !countdownPanel.activeInHierarchy)
            {
                ShowCountdown(cachedGameManager.CurrentRound);
            }

            // Update countdown text
            float timeLeft = cachedGameManager.GetCountdownTime();
            UpdateCountdownText(timeLeft);
        }

        private void UpdateCountdownText(float timeLeft)
        {
            if (countdownText == null) return;

            string displayText;
            Color textColor;

            if (timeLeft > 3f)
            {
                displayText = "GET READY";
                textColor = countdownColor;
            }
            else if (timeLeft > 2f)
            {
                displayText = "3";
                textColor = countdownColor;
            }
            else if (timeLeft > 1f)
            {
                displayText = "2";
                textColor = countdownColor;
            }
            else if (timeLeft > 0f)
            {
                displayText = "1";
                textColor = countdownColor;
            }
            else
            {
                displayText = "FIGHT!";
                textColor = fightColor;
            }

            // Apply text and color
            countdownText.text = displayText;
            countdownText.color = textColor;

            // Simple scale animation
            if (!isAnimating && (displayText == "3" || displayText == "2" || displayText == "1" || displayText == "FIGHT!"))
            {
                AnimateText();
            }
        }

        private void AnimateText()
        {
            if (isAnimating || countdownText == null) return;

            isAnimating = true;

            // Simple scale animation using LeanTween if available, otherwise just set scale
            if (countdownText.transform != null)
            {
                Vector3 originalScale = Vector3.one;
                Vector3 targetScale = Vector3.one * textAnimationScale;

                // Simple animation without external dependencies
                StartCoroutine(ScaleAnimation(originalScale, targetScale));
            }
        }

        private System.Collections.IEnumerator ScaleAnimation(Vector3 fromScale, Vector3 toScale)
        {
            float duration = 0.2f;
            float elapsed = 0f;

            // Scale up
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                countdownText.transform.localScale = Vector3.Lerp(fromScale, toScale, t);
                yield return null;
            }

            // Scale back down
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                countdownText.transform.localScale = Vector3.Lerp(toScale, fromScale, t);
                yield return null;
            }

            countdownText.transform.localScale = fromScale;
            isAnimating = false;
        }

        // ==================== EDITOR TESTING ====================

#if UNITY_EDITOR
        [Header("Testing")]
        [SerializeField] private bool enableTesting = false;

        private void Update()
        {
            if (!enableTesting || !Application.isPlaying) return;

            if (Input.GetKeyDown(KeyCode.C))
            {
                ShowCountdown(1);
                StartCoroutine(TestCountdown());
            }
        }

        private System.Collections.IEnumerator TestCountdown()
        {
            for (int i = 3; i >= 0; i--)
            {
                UpdateCountdownText(i + 0.5f);
                yield return new WaitForSeconds(1f);
            }
            HideCountdown();
        }
#endif
    }
}