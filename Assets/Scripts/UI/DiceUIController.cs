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
    /// tumbling a blue 3D d20 (<see cref="D20DieGraphic"/>) that lands with the rolled number on top,
    /// Nat 20 critical gold glows, Nat 1 red glows,
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
        [Tooltip("Duration of the die tumble, from throw to landing, in seconds.")]
        [SerializeField] private float rollAnimationDuration = 1.0f;

        [Tooltip("Delay in seconds before the modal automatically dismisses when no reroll scrolls are held (~1.0s per MasterSpec §4.2).")]
        [SerializeField] private float autoDismissDelay = 1.0f;

        [Header("Blue D20 Die")]
        [Tooltip("Procedural d20 drawn behind the roll number. Created at runtime next to the roll text if left empty.")]
        [SerializeField] private D20DieGraphic dieGraphic;

        [Tooltip("Width and height of the die in canvas pixels.")]
        [SerializeField] private float dieSize = 160f;

        [Header("Placement")]
        [Tooltip("Dock the roll to the left edge of the screen so it doesn't cover the combat animations.")]
        [SerializeField] private bool dockToLeftEdge = true;

        [Tooltip("Show the solid coloured panel behind the roll. Off: only the die and outlined text are drawn, and the outcome colour tints the verdict text instead.")]
        [SerializeField] private bool showPanelBackground = false;

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

        /// <summary>Longest a whole roll (tumble + result on screen) may take before auto-continuing.</summary>
        public const float MaxRollSequenceSeconds = 2f;

        private const float FlickerInterval = 0.07f;
        private const float RevealPunchDuration = 0.3f;
        private static readonly string[] RollStrings = BuildRollStrings();
        private static readonly Color RollTextColor = Color.white;
        private static readonly Color CriticalFailTextColor = new Color(1f, 0.55f, 0.5f, 1f);

        private Vector2 rollTextBasePosition;

        private CanvasGroup canvasGroup;
        private Coroutine activeRollCoroutine;
        private int lastHandledRollFrame = -1;
        private DiceResult? lastHandledResult;
        private string currentCheckTitle;
        private bool awaitingDecision;

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
            ClampToMaxSequence(ref rollAnimationDuration, ref autoDismissDelay);
            ApplyLeftEdgeLayout();
            EnsureDieGraphic();

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

        /// <summary>
        /// Shortens the tumble and the result hold so the whole roll never exceeds
        /// <see cref="MaxRollSequenceSeconds"/>, whatever the scene serialized.
        /// </summary>
        public static void ClampToMaxSequence(ref float rollDuration, ref float holdDuration)
        {
            rollDuration = Mathf.Clamp(rollDuration, 0.3f, MaxRollSequenceSeconds);
            holdDuration = Mathf.Clamp(holdDuration, 0f, MaxRollSequenceSeconds - rollDuration);
        }

        /// <summary>
        /// Moves the roll into a compact column at the left edge of the screen (below the hero card,
        /// above the action bar) and drops the solid panel so the centre of the battlefield stays visible.
        /// </summary>
        private void ApplyLeftEdgeLayout()
        {
            if (diceModalPanel == null) return;

            if (!showPanelBackground)
            {
                Image background = diceModalPanel.GetComponent<Image>();
                if (background != null) background.enabled = false;
                if (glowBorderImage != null) glowBorderImage.enabled = false;

                AddReadableOutline(headerText, UITheme.CreamText);
                AddReadableOutline(formulaText, UITheme.SoftText);
                AddReadableOutline(outcomeText, UITheme.CreamText);
                AddReadableOutline(rollValueText, RollTextColor);
            }

            if (!dockToLeftEdge) return;

            RectTransform panel = diceModalPanel.transform as RectTransform;
            if (panel == null) return;

            panel.anchorMin = new Vector2(0f, 0.5f);
            panel.anchorMax = new Vector2(0f, 0.5f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.anchoredPosition = new Vector2(24f, 20f);
            panel.sizeDelta = new Vector2(320f, 440f);

            PlaceInColumn(headerText, 185f, 300f, 44f);
            PlaceInColumn(formulaText, 140f, 300f, 48f);
            PlaceInColumn(rollValueText, 30f, 180f, 120f);
            PlaceInColumn(outcomeText, -85f, 300f, 56f);

            // Continue / Rune of Reroll buttons stack at the bottom of the column
            Button[] buttons = diceModalPanel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                RectTransform rt = buttons[i].transform as RectTransform;
                if (rt == null || rt.parent != panel) continue;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 16f + i * 50f);
                rt.sizeDelta = new Vector2(260f, 42f);
            }
        }

        private static void PlaceInColumn(TMP_Text text, float y, float width, float height)
        {
            if (text == null) return;
            RectTransform rt = text.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(width, height);
        }

        // Without the panel the text sits on the 3D scene, so it gets a dark outline (one material instance, set once).
        private static void AddReadableOutline(TMP_Text text, Color color)
        {
            if (text == null) return;
            text.color = color;
            text.outlineWidth = 0.22f;
            text.outlineColor = new Color32(0, 0, 0, 255);
        }

        private void SetOutcomeColor(Color color)
        {
            if (showPanelBackground)
            {
                if (glowBorderImage != null) glowBorderImage.color = color;
            }
            else if (outcomeText != null)
            {
                outcomeText.color = color;
            }
        }

        /// <summary>
        /// Creates the blue d20 behind the roll number when the scene doesn't provide one,
        /// and sizes the number so it fits on the die's top face.
        /// </summary>
        private void EnsureDieGraphic()
        {
            if (rollValueText == null) return;

            RectTransform textRect = rollValueText.rectTransform;
            rollTextBasePosition = textRect.anchoredPosition;

            if (dieGraphic == null)
            {
                GameObject dieObject = new GameObject("D20Die", typeof(RectTransform), typeof(CanvasRenderer), typeof(D20DieGraphic));
                dieObject.layer = textRect.gameObject.layer;

                RectTransform dieRect = (RectTransform)dieObject.transform;
                dieRect.SetParent(textRect.parent, false);
                dieRect.anchorMin = new Vector2(0.5f, 0.5f);
                dieRect.anchorMax = new Vector2(0.5f, 0.5f);
                dieRect.pivot = new Vector2(0.5f, 0.5f);
                dieRect.sizeDelta = new Vector2(dieSize, dieSize);
                dieRect.localPosition = textRect.localPosition + (Vector3)textRect.rect.center;
                dieRect.SetSiblingIndex(textRect.GetSiblingIndex());

                dieGraphic = dieObject.GetComponent<D20DieGraphic>();
            }

            dieGraphic.raycastTarget = false;
            dieGraphic.Rotation = D20DieGraphic.GetRestingRotation(19);

            float faceFont = Mathf.Max(24f, dieSize * 0.22f);
            rollValueText.fontSizeMax = faceFont;
            rollValueText.fontSizeMin = Mathf.Min(20f, faceFont);
        }

        private void ResetRollVisuals()
        {
            if (rollValueText != null)
            {
                rollValueText.rectTransform.anchoredPosition = rollTextBasePosition;
                rollValueText.rectTransform.localScale = Vector3.one;
                rollValueText.alpha = 1f;
            }

            if (dieGraphic != null)
            {
                dieGraphic.Offset = Vector2.zero;
                dieGraphic.Scale = 1f;
                dieGraphic.Flash = 0f;
            }
        }

        private static string[] BuildRollStrings()
        {
            string[] strings = new string[21];
            for (int i = 0; i < strings.Length; i++) strings[i] = i.ToString();
            return strings;
        }

        private static string RollString(int value)
        {
            return value >= 0 && value < RollStrings.Length ? RollStrings[value] : value.ToString();
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
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
            if (lastHandledRollFrame == Time.frameCount && lastHandledResult.HasValue && lastHandledResult.Value.Equals(result))
            {
                // Same roll shown again this frame, now with its check name (e.g. a dialogue skill check)
                if (!string.IsNullOrEmpty(checkTitle))
                {
                    currentCheckTitle = checkTitle;
                    if (headerText != null) headerText.text = $"CHECK: {checkTitle.ToUpperInvariant()}";
                }
                return;
            }

            currentCheckTitle = checkTitle;

            // A different roll while a reroll choice is still open: the shown result stands
            if (awaitingDecision)
            {
                awaitingDecision = false;
                RerollableRoll.Accept();
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
            ResetRollVisuals();

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
            SetOutcomeColor(normalColor);

            yield return TumbleDieRoutine();

            // Reveal finalized roll on the die's top face
            if (rollValueText != null)
            {
                rollValueText.text = RollString(result.rawRoll);
                rollValueText.alpha = 1f;
                rollValueText.color = result.isCriticalSuccess ? criticalSuccessColor
                    : result.isCriticalFail ? CriticalFailTextColor
                    : RollTextColor;
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
                SetOutcomeColor(criticalSuccessColor);
            }
            else if (result.isCriticalFail)
            {
                if (outcomeText != null) outcomeText.text = "NATURAL 1 - CRITICAL FAILURE!";
                SetOutcomeColor(criticalFailColor);
            }
            else if (result.isSuccess)
            {
                if (outcomeText != null) outcomeText.text = "SUCCESS!";
                SetOutcomeColor(standardSuccessColor);
            }
            else
            {
                if (outcomeText != null) outcomeText.text = "FAILURE";
                SetOutcomeColor(standardFailColor);
            }

            // Landing punch: number pops, die flashes (counts toward the hold time)
            float punch = Mathf.Min(RevealPunchDuration, autoDismissDelay);
            float elapsed = 0f;
            while (elapsed < punch)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / RevealPunchDuration);
                if (rollValueText != null)
                {
                    rollValueText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.45f, 1f, EaseOutBack(t));
                }
                if (dieGraphic != null)
                {
                    dieGraphic.Flash = 1f - t;
                    dieGraphic.Scale = Mathf.Lerp(1.08f, 1f, t);
                }
                yield return null;
            }
            ResetRollVisuals();

            // Only a failed hero roll that can still be rerolled stops on [Continue] / [Use Reroll Scroll]
            if (awaitingDecision && rerollController != null)
            {
                rerollController.PresentDecisionOptions(result, onContinue: AcceptRerollDecision, onReroll: TriggerReroll);
                yield break;
            }

            // Auto-dismiss after display delay (~1.0s per MasterSpec §4.2); tumble + hold stays within 2 s
            float remainingHold = autoDismissDelay - punch;
            while (remainingHold > 0f)
            {
                remainingHold -= Time.unscaledDeltaTime;
                yield return null;
            }

            Dismiss();
        }

        /// <summary>
        /// Throws the blue d20: drops in, tumbles with decaying spin and two small bounces,
        /// then settles with a slight overshoot so a face points straight at the player.
        /// Runs for exactly <see cref="rollAnimationDuration"/> seconds.
        /// </summary>
        private IEnumerator TumbleDieRoutine()
        {
            float duration = rollAnimationDuration;
            float settleStart = duration * 0.7f;

            Quaternion resting = D20DieGraphic.GetRestingRotation(Random.Range(0, D20DieGraphic.FaceCount));
            Quaternion spin = Random.rotationUniform;
            Vector3 axis = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-0.35f, 0.35f));
            if (axis.sqrMagnitude < 0.01f) axis = Vector3.right;
            axis.Normalize();
            const float spinSpeed = 1100f; // degrees per second at the throw

            Quaternion settleFrom = spin;
            float elapsed = 0f;
            float flickerTimer = 0f;

            if (rollValueText != null)
            {
                rollValueText.color = RollTextColor;
                rollValueText.alpha = 0.55f;
                rollValueText.text = RollString(Random.Range(1, 21));
            }

            while (elapsed < duration)
            {
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                float t = Mathf.Clamp01(elapsed / duration);

                // Faint number flicker while tumbling, hidden while the die settles
                if (rollValueText != null)
                {
                    if (elapsed < settleStart)
                    {
                        flickerTimer -= dt;
                        if (flickerTimer <= 0f)
                        {
                            flickerTimer = FlickerInterval;
                            rollValueText.text = RollString(Random.Range(1, 21));
                        }
                    }
                    else
                    {
                        rollValueText.alpha = 0f;
                    }
                }

                // Two decaying hops across the tumble
                float hop = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * 22f * (1f - t);

                if (dieGraphic != null)
                {
                    if (elapsed < settleStart)
                    {
                        float decay = 1f - elapsed / settleStart;
                        spin = Quaternion.AngleAxis(spinSpeed * (0.3f + 0.7f * decay) * dt, axis) * spin;
                        settleFrom = spin;
                        dieGraphic.Rotation = spin;
                    }
                    else
                    {
                        float s = Mathf.Clamp01((elapsed - settleStart) / (duration - settleStart));
                        dieGraphic.Rotation = Quaternion.SlerpUnclamped(settleFrom, resting, EaseOutBack(s));
                    }

                    dieGraphic.Scale = Mathf.LerpUnclamped(0.55f, 1f, EaseOutBack(Mathf.Clamp01(elapsed / 0.25f)));
                    dieGraphic.Offset = new Vector2(0f, hop);
                }

                if (rollValueText != null)
                {
                    rollValueText.rectTransform.anchoredPosition = rollTextBasePosition + new Vector2(0f, hop);
                }

                yield return null;
            }

            if (dieGraphic != null) dieGraphic.Rotation = resting;
            ResetRollVisuals();
        }

        /// <summary>
        /// Executes an immediate reroll of the current D20 check with the exact same parameters.
        /// Called by RuneOfRerollController after consuming a reroll scroll.
        /// </summary>
        public void TriggerReroll()
        {
            if (!awaitingDecision) return;
            awaitingDecision = false;

            // Spends the scroll and rolls again; the outcome callback gets the new result
            RerollableRoll.Reroll();
        }

        /// <summary>
        /// Marks the roll on screen as a failed hero roll that may be rerolled, so it stops on the
        /// [Continue] / [Use Reroll Scroll] choice instead of closing by itself.
        /// </summary>
        public void AwaitRerollDecision()
        {
            awaitingDecision = true;
        }

        /// <summary>True while the modal waits on the reroll choice.</summary>
        public bool IsAwaitingRerollDecision => awaitingDecision;

        private void AcceptRerollDecision()
        {
            Dismiss();
        }

        /// <summary>
        /// Manually or automatically hides the dice popup window. A reroll choice still open is
        /// answered with [Continue].
        /// </summary>
        public void Dismiss()
        {
            if (awaitingDecision)
            {
                awaitingDecision = false;
                HideAfterDismiss();
                RerollableRoll.Accept();
                return;
            }

            HideAfterDismiss();
        }

        private void HideAfterDismiss()
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

            ResetRollVisuals();
            HidePanel();
        }

        #endregion
    }
}
