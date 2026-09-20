using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// User Interface Controller for the D20 dice rolling overlay.
    /// Listens to DiceSystem.OnDiceRolled and presents an animated dice modal,
    /// showing numerical rolling ticks, Nat 20 critical gold glows, Nat 1 red glows,
    /// and formula breakdowns (Raw + Bonus vs DC -> SUCCESS/FAIL).
    /// </summary>
    public class DiceUIController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Popup Modal & Canvas")]
        [Tooltip("Root GameObject of the dice roll overlay panel.")]
        [SerializeField] private GameObject diceModalPanel;

        [Header("Text Displays")]
        [Tooltip("Title header (e.g., 'D20 ROLL', 'ATTACK CHECK', 'SKILL CHECK').")]
        [SerializeField] private TMP_Text headerText;

        [Tooltip("Large central text showing animated and finalized D20 numerical result.")]
        [SerializeField] private TMP_Text rollValueText;

        [Tooltip("Detailed mathematical formula text: [Raw] + [Bonus] vs DC [Target].")]
        [SerializeField] private TMP_Text formulaText;

        [Tooltip("Result verdict banner (SUCCESS, FAILURE, CRITICAL SUCCESS, CRITICAL FAIL).")]
        [SerializeField] private TMP_Text outcomeText;

        [Header("Visual Glows & Styling")]
        [Tooltip("Image or border tint indicating critical hits or standard outcomes.")]
        [SerializeField] private Image glowBorderImage;

        [SerializeField] private Color normalColor = new Color(0.2f, 0.7f, 1f, 1f); // Cyan
        [SerializeField] private Color criticalSuccessColor = new Color(1f, 0.84f, 0f, 1f); // Gold
        [SerializeField] private Color criticalFailColor = new Color(1f, 0.2f, 0.2f, 1f); // Crimson
        [SerializeField] private Color standardSuccessColor = new Color(0.2f, 0.9f, 0.3f, 1f); // Green
        [SerializeField] private Color standardFailColor = new Color(0.8f, 0.3f, 0.3f, 1f); // Soft Red

        [Header("Animation & Timing")]
        [Tooltip("Duration of the rolling number shuffle animation in seconds.")]
        [SerializeField] private float rollAnimationDuration = 0.8f;

        [Tooltip("Delay in seconds before the modal automatically dismisses.")]
        [SerializeField] private float autoDismissDelay = 2.5f;

        [Tooltip("Optional button allowing the player to tap/click to dismiss early.")]
        [SerializeField] private Button dismissButton;

        #endregion

        #region Private State

        private Coroutine activeRollCoroutine;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            AutoLocateComponents();

            if (diceModalPanel != null)
            {
                diceModalPanel.SetActive(false);
            }

            if (dismissButton != null)
            {
                dismissButton.onClick.AddListener(Dismiss);
            }
        }

        /// <summary>
        /// Automatically locates required UI components in children if not manually assigned in the Inspector.
        /// </summary>
        private void AutoLocateComponents()
        {
            // 1. Auto-locate modal panel
            if (diceModalPanel == null)
            {
                Transform panelTransform = transform.Find("DiceModalPanel")
                    ?? transform.Find("ModalPanel")
                    ?? transform.Find("Panel")
                    ?? transform.Find("DiceModal");

                if (panelTransform == null)
                {
                    foreach (Transform child in transform)
                    {
                        string lower = child.name.ToLowerInvariant();
                        if (lower.Contains("panel") || lower.Contains("modal") || lower.Contains("dice"))
                        {
                            panelTransform = child;
                            break;
                        }
                    }
                }

                if (panelTransform == null && transform.childCount > 0)
                {
                    panelTransform = transform.GetChild(0);
                }

                diceModalPanel = panelTransform != null ? panelTransform.gameObject : gameObject;
            }

            // 2. Auto-locate TMP_Text components
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text txt in texts)
            {
                string lower = txt.name.ToLowerInvariant();
                if (headerText == null && (lower.Contains("header") || lower.Contains("title")))
                {
                    headerText = txt;
                }
                else if (rollValueText == null && (lower.Contains("roll") || lower.Contains("value") || lower.Contains("result") || lower.Contains("number")))
                {
                    rollValueText = txt;
                }
                else if (formulaText == null && (lower.Contains("formula") || lower.Contains("breakdown") || lower.Contains("calc") || lower.Contains("detail")))
                {
                    formulaText = txt;
                }
                else if (outcomeText == null && (lower.Contains("outcome") || lower.Contains("verdict") || lower.Contains("status")))
                {
                    outcomeText = txt;
                }
            }

            // Fallback for remaining unassigned texts by discovery order
            if (texts.Length > 0)
            {
                System.Collections.Generic.List<TMP_Text> unassigned = new System.Collections.Generic.List<TMP_Text>();
                foreach (TMP_Text txt in texts)
                {
                    if (txt != headerText && txt != rollValueText && txt != formulaText && txt != outcomeText)
                    {
                        unassigned.Add(txt);
                    }
                }

                int idx = 0;
                if (headerText == null && idx < unassigned.Count) headerText = unassigned[idx++];
                if (rollValueText == null && idx < unassigned.Count) rollValueText = unassigned[idx++];
                if (formulaText == null && idx < unassigned.Count) formulaText = unassigned[idx++];
                if (outcomeText == null && idx < unassigned.Count) outcomeText = unassigned[idx++];
            }

            // 3. Auto-locate glow border image
            if (glowBorderImage == null)
            {
                Image[] images = GetComponentsInChildren<Image>(true);
                foreach (Image img in images)
                {
                    string lower = img.name.ToLowerInvariant();
                    if (lower.Contains("glow") || lower.Contains("border") || lower.Contains("frame") || lower.Contains("outline"))
                    {
                        glowBorderImage = img;
                        break;
                    }
                }
            }

            // 4. Auto-locate dismiss button
            if (dismissButton == null)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);
                foreach (Button btn in buttons)
                {
                    string lower = btn.name.ToLowerInvariant();
                    if (lower.Contains("dismiss") || lower.Contains("close") || lower.Contains("ok") || lower.Contains("continue"))
                    {
                        dismissButton = btn;
                        break;
                    }
                }

                if (dismissButton == null && buttons.Length > 0)
                {
                    dismissButton = buttons[0];
                }
            }
        }

        private void OnEnable()
        {
            DiceSystem.OnDiceRolled += HandleDiceRolled;
        }

        private void OnDisable()
        {
            DiceSystem.OnDiceRolled -= HandleDiceRolled;
        }

        #endregion

        #region Event Handling

        private void HandleDiceRolled(DiceResult result)
        {
            if (activeRollCoroutine != null)
            {
                StopCoroutine(activeRollCoroutine);
            }

            activeRollCoroutine = StartCoroutine(AnimateRollRoutine(result));
        }

        #endregion

        #region Animation Sequence

        private IEnumerator AnimateRollRoutine(DiceResult result)
        {
            if (diceModalPanel != null)
            {
                diceModalPanel.SetActive(true);
            }

            // Setup Header
            if (headerText != null)
            {
                headerText.text = result.advantageUsed switch
                {
                    AdvantageType.Advantage => "D20 ROLL (ADVANTAGE)",
                    AdvantageType.Disadvantage => "D20 ROLL (DISADVANTAGE)",
                    _ => "D20 ROLL"
                };
            }

            if (formulaText != null) formulaText.text = "Rolling...";
            if (outcomeText != null) outcomeText.text = "";
            if (glowBorderImage != null) glowBorderImage.color = normalColor;

            // Rolling number shuffle effect
            float elapsed = 0f;
            while (elapsed < rollAnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                int randomPreview = Random.Range(1, 21);
                if (rollValueText != null)
                {
                    rollValueText.text = randomPreview.ToString();
                }
                yield return new WaitForSecondsRealtime(0.04f);
            }

            // Reveal finalized roll
            if (rollValueText != null)
            {
                rollValueText.text = result.rawRoll.ToString();
            }

            // Display formula breakdown
            string sign = result.bonus >= 0 ? $"+{result.bonus}" : $"{result.bonus}";
            if (formulaText != null)
            {
                if (result.targetDC > 0)
                {
                    formulaText.text = $"Roll: {result.rawRoll} {sign} = Total: {result.finalTotal}  (vs DC {result.targetDC})";
                }
                else
                {
                    formulaText.text = $"Roll: {result.rawRoll} {sign} = Total: {result.finalTotal}";
                }
            }

            // Format outcome banner and glow colors
            if (result.isCriticalSuccess)
            {
                if (outcomeText != null) outcomeText.text = "NATURAL 20 - CRITICAL SUCCESS!";
                if (glowBorderImage != null) glowBorderImage.color = criticalSuccessColor;
            }
            else if (result.isCriticalFail)
            {
                if (outcomeText != null) outcomeText.text = "NATURAL 1 - CRITICAL FAILURE!";
                if (glowBorderImage != null) glowBorderImage.color = criticalFailColor;
            }
            else if (result.isSuccess)
            {
                if (outcomeText != null) outcomeText.text = "SUCCESS!";
                if (glowBorderImage != null) glowBorderImage.color = standardSuccessColor;
            }
            else
            {
                if (outcomeText != null) outcomeText.text = "FAILURE";
                if (glowBorderImage != null) glowBorderImage.color = standardFailColor;
            }

            // Auto-dismiss after display delay
            yield return new WaitForSecondsRealtime(autoDismissDelay);

            Dismiss();
        }

        /// <summary>
        /// Manually or automatically hides the dice popup window.
        /// </summary>
        public void Dismiss()
        {
            if (activeRollCoroutine != null)
            {
                StopCoroutine(activeRollCoroutine);
                activeRollCoroutine = null;
            }

            if (diceModalPanel != null)
            {
                diceModalPanel.SetActive(false);
            }
        }

        #endregion
    }
}
