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
        [SerializeField] private Color criticalSuccessColor = UITheme.CoinGold; // Gold
        [SerializeField] private Color criticalFailColor = new Color(1f, 0.2f, 0.2f, 1f); // Crimson
        [SerializeField] private Color standardSuccessColor = new Color(0.2f, 0.9f, 0.3f, 1f); // Green
        [SerializeField] private Color standardFailColor = new Color(0.8f, 0.3f, 0.3f, 1f); // Soft Red

        [Header("Animation & Timing")]
        [Tooltip("Duration of the rolling number shuffle animation in seconds.")]
        [SerializeField] private float rollAnimationDuration = 0.8f;

        [Tooltip("Delay in seconds before the modal automatically dismisses when no reroll scrolls are held (~1.0s per MasterSpec §4.2).")]
        [SerializeField] private float autoDismissDelay = 1.0f;

        [Tooltip("Optional button allowing the player to tap/click to dismiss early.")]
        [SerializeField] private Button dismissButton;

        [Header("Rune of Reroll Flow")]
        [Tooltip("Controller managing the pause and reroll scroll button flow.")]
        [SerializeField] private RuneOfRerollController rerollController;

        #endregion

        #region Singleton & Access

        private static DiceUIController instance;

        /// <summary>
        /// Singleton instance accessor. Lazily discovers the component in the active scene
        /// (including inactive objects) or under any Canvas hierarchy.
        /// </summary>
        public static DiceUIController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<DiceUIController>(FindObjectsInactive.Include);
                    if (instance == null)
                    {
                        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                        foreach (var canvas in canvases)
                        {
                            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                            {
                                if (child.name.IndexOf("DiceModalPanel", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    child.name.IndexOf("DiceRoll", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    child.name.IndexOf("DicePanel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    instance = child.GetComponent<DiceUIController>() ?? child.gameObject.AddComponent<DiceUIController>();
                                    break;
                                }
                            }
                            if (instance != null) break;
                        }
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        #endregion

        #region Private State

        private CanvasGroup canvasGroup;
        private Coroutine activeRollCoroutine;
        private int lastHandledRollFrame = -1;
        private DiceResult? lastHandledResult;
        private string currentCheckTitle;

        public bool IsDisplaying { get; private set; }

        /// <summary>The final evaluated dice result of the last displayed roll (including any reroll).</summary>
        public DiceResult LastFinalResult => lastHandledResult ?? default;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            AutoLocateComponents();

            // Locate or initialize CanvasGroup for flicker-free show/hide without disabling GameObject
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            HidePanel();

            if (dismissButton != null)
            {
                dismissButton.onClick.RemoveListener(Dismiss);
                dismissButton.onClick.AddListener(Dismiss);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            if (dismissButton != null)
            {
                dismissButton.onClick.RemoveListener(Dismiss);
            }
        }

        /// <summary>
        /// Automatically locates required UI components in children if not manually assigned in the Inspector.
        /// </summary>
        public void AutoLocateComponents()
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

            // 5. Auto-locate or attach RuneOfRerollController
            if (rerollController == null)
            {
                rerollController = GetComponentInChildren<RuneOfRerollController>(true)
                    ?? (diceModalPanel != null ? diceModalPanel.GetComponent<RuneOfRerollController>() : null);

                if (rerollController == null && diceModalPanel != null)
                {
                    rerollController = diceModalPanel.AddComponent<RuneOfRerollController>();
                }
            }

            if (rerollController != null)
            {
                rerollController.AutoLocateButtons();
            }

            // 6. Sanitize text properties for flicker-free, non-clipped presentation
            if (headerText != null)
            {
                headerText.margin = Vector4.zero;
                headerText.enableAutoSizing = true;
                headerText.fontSizeMin = 18f;
                headerText.fontSizeMax = 40f;
                headerText.textWrappingMode = TextWrappingModes.Normal;
                headerText.raycastTarget = false;
            }
            if (rollValueText != null)
            {
                rollValueText.margin = Vector4.zero;
                rollValueText.enableAutoSizing = true;
                rollValueText.fontSizeMin = 36f;
                rollValueText.fontSizeMax = 80f;
                rollValueText.raycastTarget = false;
            }
            if (formulaText != null)
            {
                formulaText.margin = Vector4.zero;
                formulaText.enableAutoSizing = true;
                formulaText.fontSizeMin = 14f;
                formulaText.fontSizeMax = 28f;
                formulaText.textWrappingMode = TextWrappingModes.Normal;
                formulaText.raycastTarget = false;
            }
            if (outcomeText != null)
            {
                outcomeText.margin = Vector4.zero;
                outcomeText.enableAutoSizing = true;
                outcomeText.fontSizeMin = 16f;
                outcomeText.fontSizeMax = 36f;
                outcomeText.textWrappingMode = TextWrappingModes.Normal;
                outcomeText.raycastTarget = false;
            }
            if (dismissButton != null)
            {
                TMP_Text dText = dismissButton.GetComponentInChildren<TMP_Text>(true);
                if (dText != null)
                {
                    dText.margin = Vector4.zero;
                    dText.enableAutoSizing = true;
                    dText.fontSizeMin = 12f;
                    dText.fontSizeMax = 24f;
                    dText.raycastTarget = false;
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

        #region Visibility & Display Helpers

        /// <summary>
        /// Ensures the modal and its hierarchy are active, brings to the top of the canvas, and reveals visually.
        /// </summary>
        public void ShowPanel()
        {
            IsDisplaying = true;

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (diceModalPanel != null && !diceModalPanel.activeSelf)
            {
                diceModalPanel.SetActive(true);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            transform.SetAsLastSibling();
            if (diceModalPanel != null && diceModalPanel != gameObject)
            {
                diceModalPanel.transform.SetAsLastSibling();
            }

            if (dismissButton != null)
            {
                TMP_Text dText = dismissButton.GetComponentInChildren<TMP_Text>(true);
                if (dText != null)
                {
                    dText.text = "CONTINUE";
                }
            }
        }

        /// <summary>
        /// Hides the modal visually without disabling the host script GameObject.
        /// </summary>
        public void HidePanel()
        {
            IsDisplaying = false;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (diceModalPanel != null && diceModalPanel != gameObject)
            {
                diceModalPanel.SetActive(false);
            }
            else if (diceModalPanel == gameObject && canvasGroup == null)
            {
                diceModalPanel.SetActive(false);
            }
        }

        #endregion

        #region Event Handling & Public Dispatch

        /// <summary>
        /// Displays the animated D20 roll popup modal for the provided result.
        /// Deduplicates calls made in the same frame for the same result.
        /// </summary>
        public void ShowDiceRoll(DiceResult result, string checkTitle = null)
        {
            currentCheckTitle = checkTitle;

            if (lastHandledRollFrame == Time.frameCount && lastHandledResult.HasValue && lastHandledResult.Value.Equals(result))
            {
                return;
            }

            lastHandledRollFrame = Time.frameCount;
            lastHandledResult = result;

            if (activeRollCoroutine != null)
            {
                StopCoroutine(activeRollCoroutine);
                activeRollCoroutine = null;
            }

            ShowPanel();
            activeRollCoroutine = StartCoroutine(AnimateRollRoutine(result, checkTitle));
        }

        private void HandleDiceRolled(DiceResult result)
        {
            ShowDiceRoll(result);
        }

        #endregion

        #region Animation Sequence

        private IEnumerator AnimateRollRoutine(DiceResult result, string checkTitle = null)
        {
            ShowPanel();

            if (rerollController != null)
            {
                rerollController.HideActionButtons();
            }

            // Calculate needed roll (raw D20 needed to meet or exceed DC)
            int neededRoll = Mathf.Clamp(result.targetDC - result.bonus, 1, 20);
            string bonusSign = result.bonus >= 0 ? $"+{result.bonus}" : $"{result.bonus}";

            // Setup Header
            if (headerText != null)
            {
                if (!string.IsNullOrEmpty(checkTitle))
                {
                    headerText.text = $"CHECK: {checkTitle.ToUpperInvariant()}";
                }
                else
                {
                    headerText.text = result.advantageUsed switch
                    {
                        AdvantageType.Advantage => "D20 ROLL (ADVANTAGE)",
                        AdvantageType.Disadvantage => "D20 ROLL (DISADVANTAGE)",
                        _ => result.targetDC > 0 ? $"D20 CHECK (DC {result.targetDC})" : "D20 ROLL"
                    };
                }
            }

            // Display target requirements during roll shuffle
            if (formulaText != null)
            {
                if (result.targetDC > 0)
                {
                    formulaText.text = $"Target DC: {result.targetDC}  |  Bonus: {bonusSign}  |  Need to roll: {neededRoll}+";
                }
                else
                {
                    formulaText.text = "Rolling D20...";
                }
            }

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
            if (formulaText != null)
            {
                if (result.targetDC > 0)
                {
                    formulaText.text = $"Roll: {result.rawRoll} {bonusSign} = Total: {result.finalTotal}  (vs DC {result.targetDC} - Needed {neededRoll}+)";
                }
                else
                {
                    formulaText.text = $"Roll: {result.rawRoll} {bonusSign} = Total: {result.finalTotal}";
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

            // Check if player owns a Reroll Scroll to pause for decision
            if (rerollController != null && rerollController.ShouldPauseForDecision(result))
            {
                rerollController.PresentDecisionOptions(result, onContinue: Dismiss, onReroll: TriggerReroll);
                yield break;
            }

            // Auto-dismiss after display delay (~1.0s per MasterSpec §4.2)
            yield return new WaitForSecondsRealtime(autoDismissDelay);

            Dismiss();
        }

        /// <summary>
        /// Executes an immediate reroll of the current D20 check with the exact same parameters.
        /// Called by RuneOfRerollController after consuming a reroll scroll.
        /// </summary>
        public void TriggerReroll()
        {
            if (!lastHandledResult.HasValue) return;
            DiceResult prev = lastHandledResult.Value;

            // Roll new D20 result with matching bonus, DC, and advantage mode
            DiceResult newResult = DiceSystem.RollD20(prev.bonus, prev.targetDC, prev.advantageUsed);
            Debug.Log($"[DiceUIController] Reroll executed: {newResult} (replaced {prev})");

            if (activeRollCoroutine != null)
            {
                StopCoroutine(activeRollCoroutine);
                activeRollCoroutine = null;
            }

            lastHandledResult = newResult;
            activeRollCoroutine = StartCoroutine(AnimateRollRoutine(newResult, currentCheckTitle));
        }

        /// <summary>
        /// Manually or automatically hides the dice popup window.
        /// </summary>
        public void Dismiss()
        {
            IsDisplaying = false;

            if (rerollController != null)
            {
                rerollController.HideActionButtons();
            }

            if (activeRollCoroutine != null)
            {
                StopCoroutine(activeRollCoroutine);
                activeRollCoroutine = null;
            }

            HidePanel();
        }

        #endregion
    }
}
