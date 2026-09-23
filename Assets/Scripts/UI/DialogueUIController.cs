using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Dialogue;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// User Interface Controller for branching conversations and skill checks.
    /// Manages the dialogue modal panel, speaker portrait, typewriter text typing animation,
    /// dynamic option button spawning with DC check badges, and continue/close buttons.
    /// </summary>
    public class DialogueUIController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Dialogue Window")]
        [Tooltip("Root GameObject of the dialogue window.")]
        [SerializeField] private GameObject dialoguePanel;

        [Tooltip("Text component displaying the speaker's name.")]
        [SerializeField] private TMP_Text speakerNameText;

        [Tooltip("Image component displaying the speaker's 2D portrait.")]
        [SerializeField] private Image speakerPortraitImage;

        [Tooltip("Text component displaying the body text of the dialogue.")]
        [SerializeField] private TMP_Text dialogueBodyText;

        [Header("Choice Buttons")]
        [Tooltip("Parent transform where choice buttons are instantiated.")]
        [SerializeField] private Transform optionsContainer;

        [Tooltip("Button prefab instantiated for each DialogueOption.")]
        [SerializeField] private GameObject optionButtonPrefab;

        [Header("Continue & Close")]
        [Tooltip("Default continue button displayed when a node has no custom options.")]
        [SerializeField] private Button continueButton;

        [Header("Typewriter Settings")]
        [Tooltip("Seconds delay between individual characters during typewriter effect.")]
        [SerializeField] private float typewriterSpeed = 0.02f;

        #endregion

        #region Singleton & Access

        private static DialogueUIController instance;

        /// <summary>
        /// Singleton instance accessor. Lazily discovers the component in the active scene
        /// (including inactive objects) or under any Canvas hierarchy.
        /// </summary>
        public static DialogueUIController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<DialogueUIController>(FindObjectsInactive.Include);
                    if (instance == null)
                    {
                        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                        foreach (var canvas in canvases)
                        {
                            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                            {
                                if (child.name.IndexOf("DialoguePanel", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    child.name.IndexOf("DialogueModal", System.StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    instance = child.GetComponent<DialogueUIController>() ?? child.gameObject.AddComponent<DialogueUIController>();
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
        private Coroutine activeTypewriterCoroutine;
        private readonly List<GameObject> spawnedButtons = new List<GameObject>();
        private string currentFullText = "";

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

            HideDialogue();

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
                continueButton.onClick.AddListener(OnContinueClicked);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
            }
        }

        /// <summary>
        /// Automatically locates required UI components in children if not manually assigned in the Inspector.
        /// </summary>
        private void AutoLocateComponents()
        {
            // 1. Auto-locate dialogue panel
            if (dialoguePanel == null)
            {
                Transform panelTransform = transform.Find("DialoguePanel")
                    ?? transform.Find("Panel")
                    ?? transform.Find("DialogueWindow")
                    ?? transform.Find("Window");

                if (panelTransform == null)
                {
                    foreach (Transform child in transform)
                    {
                        string lower = child.name.ToLowerInvariant();
                        if (lower.Contains("panel") || lower.Contains("dialogue") || lower.Contains("window"))
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

                dialoguePanel = panelTransform != null ? panelTransform.gameObject : gameObject;
            }

            // 2. Auto-locate TMP_Text components
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text txt in texts)
            {
                string lower = txt.name.ToLowerInvariant();
                if (speakerNameText == null && (lower.Contains("name") || lower.Contains("speaker") || lower.Contains("title")))
                {
                    speakerNameText = txt;
                }
                else if (dialogueBodyText == null && (lower.Contains("body") || lower.Contains("text") || lower.Contains("dialogue") || lower.Contains("content") || lower.Contains("message") || lower.Contains("speech")))
                {
                    dialogueBodyText = txt;
                }
            }

            // Fallback for remaining unassigned texts
            if (texts.Length > 0)
            {
                List<TMP_Text> unassigned = new List<TMP_Text>();
                foreach (TMP_Text txt in texts)
                {
                    if (txt != speakerNameText && txt != dialogueBodyText)
                    {
                        unassigned.Add(txt);
                    }
                }

                int idx = 0;
                if (speakerNameText == null && idx < unassigned.Count) speakerNameText = unassigned[idx++];
                if (dialogueBodyText == null && idx < unassigned.Count) dialogueBodyText = unassigned[idx++];
            }

            // Sanitize text properties to prevent clipping or displacement
            if (dialogueBodyText != null)
            {
                dialogueBodyText.margin = Vector4.zero;
                dialogueBodyText.enableAutoSizing = true;
                dialogueBodyText.fontSizeMin = 16f;
                dialogueBodyText.fontSizeMax = 30f;
                dialogueBodyText.textWrappingMode = TextWrappingModes.Normal;
                dialogueBodyText.overflowMode = TextOverflowModes.Overflow;
                dialogueBodyText.raycastTarget = false;
            }

            if (speakerNameText != null)
            {
                speakerNameText.margin = Vector4.zero;
                speakerNameText.enableAutoSizing = true;
                speakerNameText.fontSizeMin = 16f;
                speakerNameText.fontSizeMax = 35f;
                speakerNameText.textWrappingMode = TextWrappingModes.Normal;
                speakerNameText.overflowMode = TextOverflowModes.Overflow;
                speakerNameText.raycastTarget = false;
            }

            // 3. Auto-locate speaker portrait image
            if (speakerPortraitImage == null)
            {
                Image[] images = GetComponentsInChildren<Image>(true);
                foreach (Image img in images)
                {
                    string lower = img.name.ToLowerInvariant();
                    if (lower.Contains("portrait") || lower.Contains("speaker") || lower.Contains("avatar") || lower.Contains("face") || lower.Contains("character"))
                    {
                        speakerPortraitImage = img;
                        break;
                    }
                }
            }

            // 4. Auto-locate options container
            if (optionsContainer == null)
            {
                Transform[] transforms = GetComponentsInChildren<Transform>(true);
                foreach (Transform t in transforms)
                {
                    if (t == transform) continue;
                    string lower = t.name.ToLowerInvariant();
                    if (lower.Contains("options") || lower.Contains("choices") || lower.Contains("buttons") || lower.Contains("optioncontainer") || lower.Contains("choicecontainer"))
                    {
                        optionsContainer = t;
                        break;
                    }
                }
            }

            // 5. Auto-locate continue button
            if (continueButton == null)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);
                foreach (Button btn in buttons)
                {
                    string lower = btn.name.ToLowerInvariant();
                    if (lower.Contains("continue") || lower.Contains("next") || lower.Contains("close") || lower.Contains("proceed"))
                    {
                        continueButton = btn;
                        break;
                    }
                }

                if (continueButton == null && buttons.Length > 0)
                {
                    foreach (Button btn in buttons)
                    {
                        if (optionsContainer == null || !btn.transform.IsChildOf(optionsContainer))
                        {
                            continueButton = btn;
                            break;
                        }
                    }

                    if (continueButton == null)
                    {
                        continueButton = buttons[0];
                    }
                }
            }

            // 6. Auto-locate option button prefab / template if not assigned
            if (optionButtonPrefab == null && optionsContainer != null)
            {
                foreach (Transform child in optionsContainer)
                {
                    string lower = child.name.ToLowerInvariant();
                    if (lower.Contains("option") || lower.Contains("button") || lower.Contains("prefab") || lower.Contains("template"))
                    {
                        optionButtonPrefab = child.gameObject;
                        child.gameObject.SetActive(false);
                        break;
                    }
                }
            }
        }

        private void OnEnable()
        {
            DialogueController.OnDialogueStarted += DisplayDialogueNode;
            DialogueController.OnDialogueUpdated += DisplayDialogueNode;
            DialogueController.OnDialogueEnded += HideDialogue;
        }

        private void OnDisable()
        {
            DialogueController.OnDialogueStarted -= DisplayDialogueNode;
            DialogueController.OnDialogueUpdated -= DisplayDialogueNode;
            DialogueController.OnDialogueEnded -= HideDialogue;
        }

        #endregion

        #region Dialogue Rendering

        /// <summary>
        /// Displays the dialogue node text, speaker info, portrait, and choice buttons.
        /// </summary>
        public void DisplayDialogueNode(DialogueNodeSO node)
        {
            if (node == null) return;

            AutoLocateComponents();

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (dialoguePanel != null && !dialoguePanel.activeSelf)
            {
                dialoguePanel.SetActive(true);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            transform.SetAsLastSibling();
            if (dialoguePanel != null && dialoguePanel != gameObject)
            {
                dialoguePanel.transform.SetAsLastSibling();
            }

            // 1. Speaker Info
            if (speakerNameText != null)
            {
                speakerNameText.text = node.SpeakerName;
            }

            if (speakerPortraitImage != null)
            {
                if (node.SpeakerPortrait != null)
                {
                    speakerPortraitImage.sprite = node.SpeakerPortrait;
                    speakerPortraitImage.enabled = true;
                }
                else
                {
                    speakerPortraitImage.enabled = false;
                }
            }

            // 2. Typewriter Text
            currentFullText = node.DialogueText;
            if (activeTypewriterCoroutine != null)
            {
                StopCoroutine(activeTypewriterCoroutine);
            }
            activeTypewriterCoroutine = StartCoroutine(TypewriterRoutine(currentFullText));

            // 3. Build Options
            BuildOptions(node);
        }

        private IEnumerator TypewriterRoutine(string fullText)
        {
            if (dialogueBodyText == null) yield break;

            dialogueBodyText.text = "";
            for (int i = 0; i < fullText.Length; i++)
            {
                dialogueBodyText.text += fullText[i];
                yield return new WaitForSecondsRealtime(typewriterSpeed);
            }
        }

        private void BuildOptions(DialogueNodeSO node)
        {
            ClearSpawnedButtons();

            if (optionsContainer != null)
            {
                optionsContainer.gameObject.SetActive(true);
            }

            IReadOnlyList<DialogueOption> options = node.Options;

            if (options != null && options.Count > 0)
            {
                if (continueButton != null) continueButton.gameObject.SetActive(false);

                // Determine active player's attribute bonus for skill check requirement previews
                int playerBonus = 2;
                var player = FindAnyObjectByType<Combat.PlayerUnit>();
                if (player != null)
                {
                    playerBonus = player.PrimaryAttributeBonus;
                }

                foreach (var option in options)
                {
                    if (option == null) continue;

                    GameObject btnObj;
                    if (optionButtonPrefab != null && optionsContainer != null)
                    {
                        btnObj = Instantiate(optionButtonPrefab, optionsContainer);
                    }
                    else
                    {
                        // Fallback button
                        btnObj = new GameObject("OptionButton", typeof(RectTransform), typeof(Button), typeof(Image));
                        if (optionsContainer != null) btnObj.transform.SetParent(optionsContainer, false);
                    }

                    btnObj.SetActive(true);
                    spawnedButtons.Add(btnObj);

                    Button btn = btnObj.GetComponent<Button>();
                    TMP_Text btnText = btnObj.GetComponentInChildren<TMP_Text>(true);

                    int neededRoll = Mathf.Clamp(option.TargetDC - playerBonus, 1, 20);
                    string formattedText = option.RequiresCheck
                        ? $"[{option.SkillCheckDescription} | DC {option.TargetDC} (Need {neededRoll}+)] {option.OptionText}"
                        : option.OptionText;

                    if (btnText != null)
                    {
                        btnText.margin = Vector4.zero;
                        btnText.enableAutoSizing = true;
                        btnText.fontSizeMin = 12f;
                        btnText.fontSizeMax = 22f;
                        btnText.textWrappingMode = TextWrappingModes.Normal;
                        btnText.overflowMode = TextOverflowModes.Overflow;
                        btnText.raycastTarget = false;
                        btnText.text = formattedText;
                    }
                    else
                    {
                        Text legacyText = btnObj.GetComponentInChildren<Text>(true);
                        if (legacyText != null)
                        {
                            legacyText.resizeTextForBestFit = true;
                            legacyText.resizeTextMinSize = 12;
                            legacyText.resizeTextMaxSize = 22;
                            legacyText.raycastTarget = false;
                            legacyText.text = formattedText;
                        }
                    }

                    if (btn != null)
                    {
                        DialogueOption capturedOption = option;
                        btn.onClick.AddListener(() => OnOptionSelected(capturedOption));
                    }
                }
            }
            else
            {
                // No options: show continue button
                if (continueButton != null)
                {
                    continueButton.gameObject.SetActive(true);
                }
            }
        }

        private void ClearSpawnedButtons()
        {
            foreach (var btn in spawnedButtons)
            {
                if (btn != null) Destroy(btn);
            }
            spawnedButtons.Clear();

            // Deactivate ALL existing children in optionsContainer so dummy template buttons aren't visible
            if (optionsContainer != null)
            {
                foreach (Transform child in optionsContainer)
                {
                    if (child != null)
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }
        }

        /// <summary>
        /// Immediately hides and destroys choice buttons, used during dice rolls to prevent premature clicks.
        /// </summary>
        public void HideChoiceButtons()
        {
            ClearSpawnedButtons();

            if (optionsContainer != null)
            {
                optionsContainer.gameObject.SetActive(false);
            }

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Controls visibility and raycast blocking of the dialogue window via CanvasGroup without resetting active state.
        /// </summary>
        public void SetDialogueVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            if (dialoguePanel != null && dialoguePanel != gameObject)
            {
                dialoguePanel.SetActive(visible);
            }
        }

        #endregion

        #region User Interaction

        private void OnOptionSelected(DialogueOption option)
        {
            // If text is still typing, finish typing immediately on first click
            if (dialogueBodyText != null && dialogueBodyText.text.Length < currentFullText.Length)
            {
                if (activeTypewriterCoroutine != null) StopCoroutine(activeTypewriterCoroutine);
                dialogueBodyText.text = currentFullText;
            }

            DialogueController.Instance?.SelectOption(option);
        }

        private void OnContinueClicked()
        {
            DialogueController.Instance?.EndDialogue();
        }

        /// <summary>
        /// Hides the dialogue interface and cleans up active typing coroutine and option buttons.
        /// </summary>
        public void HideDialogue()
        {
            if (activeTypewriterCoroutine != null)
            {
                StopCoroutine(activeTypewriterCoroutine);
                activeTypewriterCoroutine = null;
            }

            ClearSpawnedButtons();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (dialoguePanel != null && dialoguePanel != gameObject)
            {
                dialoguePanel.SetActive(false);
            }
            else if (dialoguePanel == gameObject && canvasGroup == null)
            {
                dialoguePanel.SetActive(false);
            }
        }

        #endregion
    }
}
