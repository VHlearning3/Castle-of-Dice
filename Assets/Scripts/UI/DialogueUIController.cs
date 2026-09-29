using System;
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
    /// Manages the dialogue modal panel, speaker portrait slot, typewriter text typing animation,
    /// dynamic option button spawning with DC check badges, and continue/close buttons.
    /// Fully styled with dark slate panels, burnished gold frames, and persistent portrait framing.
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

        [Header("Fantasy Theme Sprites & Slots")]
        [Tooltip("9-sliced dark slate panel background sprite.")]
        [SerializeField] private Sprite panelDarkSprite;

        [Tooltip("9-sliced ornate frame for portrait and icon slots.")]
        [SerializeField] private Sprite slotFrameSprite;

        [Tooltip("Filigree gold horizontal divider sprite.")]
        [SerializeField] private Sprite dividerGoldSprite;

        [Tooltip("9-sliced button sprite for choice/continue buttons.")]
        [SerializeField] private Sprite buttonNormalSprite;

        [Tooltip("9-sliced button sprite for hovered state.")]
        [SerializeField] private Sprite buttonHoverSprite;

        [Tooltip("9-sliced button sprite for pressed state.")]
        [SerializeField] private Sprite buttonPressedSprite;

        [Tooltip("Default portrait placeholder sprite shown in the left portrait slot when NPC has no portrait.")]
        [SerializeField] private Sprite defaultPortraitPlaceholder;

        [Tooltip("Frame image holding the speaker portrait.")]
        [SerializeField] private Image portraitFrameImage;

        [Tooltip("Divider image under speaker name.")]
        [SerializeField] private Image nameDividerImage;

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
                                if (child.name.IndexOf("DialoguePanel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    child.name.IndexOf("DialogueModal", StringComparison.OrdinalIgnoreCase) >= 0)
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

            LoadThemeSpritesIfMissing();
            AutoLocateComponents();
            EnsureStyledHierarchy();

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
        /// Resolves theme sprites in Editor or from Resources if unassigned.
        /// </summary>
        public void LoadThemeSpritesIfMissing()
        {
            if (panelDarkSprite == null)
                panelDarkSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            if (slotFrameSprite == null)
                slotFrameSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Slot_Frame.png");
            if (dividerGoldSprite == null)
                dividerGoldSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png");
            if (buttonNormalSprite == null)
                buttonNormalSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Button_Normal.png");
            if (buttonHoverSprite == null)
                buttonHoverSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Button_Hover.png");
            if (buttonPressedSprite == null)
                buttonPressedSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Button_Pressed.png");
            if (defaultPortraitPlaceholder == null)
                defaultPortraitPlaceholder = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Portrait_Placeholder.png");
#if UNITY_EDITOR
            if (optionButtonPrefab == null)
                optionButtonPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/UI/OptionButtonPrefab.prefab");
#endif
        }

        /// <summary>
        /// Automatically locates required UI components in children if not manually assigned in the Inspector.
        /// </summary>
        public void AutoLocateComponents()
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

            // 3. Auto-locate speaker portrait image and frame
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach (Image img in images)
            {
                string lower = img.name.ToLowerInvariant();
                if (speakerPortraitImage == null && (lower == "speaker_portrait" || lower == "portrait" || lower.Contains("avatar") || lower.Contains("face")))
                {
                    speakerPortraitImage = img;
                }
                else if (portraitFrameImage == null && (lower.Contains("portrait_slot") || lower.Contains("portrait_frame") || lower.Contains("portraitframe")))
                {
                    portraitFrameImage = img;
                }
                else if (nameDividerImage == null && (lower.Contains("divider") || lower.Contains("gold_line")))
                {
                    nameDividerImage = img;
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
                    if (lower.Contains("continue") || lower.Contains("next") || lower.Contains("proceed"))
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

        /// <summary>
        /// Restructures and styles the Dialogue UI to match the professional tabletop D&D HUD:
        /// 1. Cinematic bottom-anchored panel with dark slate 9-sliced frame and filigree gold trim.
        /// 2. Framed left-side portrait slot that ALWAYS remains visible for character art or placeholder.
        /// 3. Wide readable dialogue content area with golden engraved speaker title and divider.
        /// 4. Stacked fantasy choice buttons with gold DC skill check badges.
        /// </summary>
        public void EnsureStyledHierarchy()
        {
            LoadThemeSpritesIfMissing();

            if (dialoguePanel == null)
            {
                dialoguePanel = gameObject;
            }

            // A. Style Dialogue Panel
            RectTransform panelRect = dialoguePanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0.5f, 0f);
                panelRect.anchorMax = new Vector2(0.5f, 0f);
                panelRect.pivot = new Vector2(0.5f, 0f);
                panelRect.anchoredPosition = new Vector2(0f, 35f);
                panelRect.sizeDelta = new Vector2(980f, 240f);
            }

            Image panelImg = dialoguePanel.GetComponent<Image>();
            if (panelImg != null && panelDarkSprite != null)
            {
                panelImg.sprite = panelDarkSprite;
                panelImg.type = Image.Type.Sliced;
                panelImg.color = Color.white;
            }

            // B. Left-Side Portrait Slot Frame
            Transform frameTr = dialoguePanel.transform.Find("Portrait_Slot_Frame");
            GameObject frameObj = frameTr != null ? frameTr.gameObject : null;
            if (frameObj == null)
            {
                frameObj = new GameObject("Portrait_Slot_Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                frameObj.transform.SetParent(dialoguePanel.transform, false);
            }

            RectTransform frameRect = frameObj.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0f, 0.5f);
            frameRect.anchorMax = new Vector2(0f, 0.5f);
            frameRect.pivot = new Vector2(0f, 0.5f);
            frameRect.anchoredPosition = new Vector2(24f, 0f);
            frameRect.sizeDelta = new Vector2(136f, 136f);

            portraitFrameImage = frameObj.GetComponent<Image>();
            if (portraitFrameImage != null && slotFrameSprite != null)
            {
                portraitFrameImage.sprite = slotFrameSprite;
                portraitFrameImage.type = Image.Type.Sliced;
                portraitFrameImage.color = Color.white;
            }

            // C. Speaker Portrait Image inside Slot Frame
            Transform portTr = frameObj.transform.Find("Speaker_Portrait");
            if (portTr == null && speakerPortraitImage != null)
            {
                portTr = speakerPortraitImage.transform;
            }

            GameObject portObj = portTr != null ? portTr.gameObject : null;
            if (portObj == null)
            {
                portObj = new GameObject("Speaker_Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            }
            portObj.transform.SetParent(frameObj.transform, false);

            RectTransform portRect = portObj.GetComponent<RectTransform>();
            portRect.anchorMin = Vector2.zero;
            portRect.anchorMax = Vector2.one;
            portRect.pivot = new Vector2(0.5f, 0.5f);
            portRect.anchoredPosition = Vector2.zero;
            portRect.sizeDelta = new Vector2(-16f, -16f);

            speakerPortraitImage = portObj.GetComponent<Image>();
            if (speakerPortraitImage != null)
            {
                if (defaultPortraitPlaceholder != null && speakerPortraitImage.sprite == null)
                {
                    speakerPortraitImage.sprite = defaultPortraitPlaceholder;
                }
                speakerPortraitImage.type = Image.Type.Simple;
                speakerPortraitImage.preserveAspect = true;
                speakerPortraitImage.color = Color.white;
                speakerPortraitImage.enabled = true;
            }

            // D. Right-Side Content Area (Width: ~770px)
            Transform contentTr = dialoguePanel.transform.Find("Dialogue_Content_Area")
                ?? dialoguePanel.transform.Find("NPC_Text_Area");
            GameObject contentObj = contentTr != null ? contentTr.gameObject : null;
            if (contentObj == null)
            {
                contentObj = new GameObject("Dialogue_Content_Area", typeof(RectTransform));
                contentObj.transform.SetParent(dialoguePanel.transform, false);
            }
            else
            {
                contentObj.name = "Dialogue_Content_Area";
            }

            RectTransform contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.offsetMin = new Vector2(185f, 16f);
            contentRect.offsetMax = new Vector2(-26f, -16f);

            // E. Speaker Name Text
            if (speakerNameText != null)
            {
                speakerNameText.transform.SetParent(contentObj.transform, false);
                RectTransform nameRect = speakerNameText.GetComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0f, 1f);
                nameRect.anchorMax = new Vector2(1f, 1f);
                nameRect.pivot = new Vector2(0f, 1f);
                nameRect.anchoredPosition = new Vector2(0f, 0f);
                nameRect.sizeDelta = new Vector2(0f, 32f);

                speakerNameText.fontSize = 20f;
                speakerNameText.fontStyle = FontStyles.Bold;
                speakerNameText.color = UITheme.GoldAccent; // #F6D578
                speakerNameText.alignment = TextAlignmentOptions.TopLeft;
                speakerNameText.enableAutoSizing = false;
                speakerNameText.raycastTarget = false;
            }

            // F. Ornate Gold Divider under Speaker Name
            Transform divTr = contentObj.transform.Find("Name_Divider");
            GameObject divObj = divTr != null ? divTr.gameObject : null;
            if (divObj == null)
            {
                divObj = new GameObject("Name_Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                divObj.transform.SetParent(contentObj.transform, false);
            }

            RectTransform divRect = divObj.GetComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0f, 1f);
            divRect.anchorMax = new Vector2(1f, 1f);
            divRect.pivot = new Vector2(0f, 1f);
            divRect.anchoredPosition = new Vector2(0f, -32f);
            divRect.sizeDelta = new Vector2(0f, 4f);

            nameDividerImage = divObj.GetComponent<Image>();
            if (nameDividerImage != null && dividerGoldSprite != null)
            {
                nameDividerImage.sprite = dividerGoldSprite;
                nameDividerImage.type = Image.Type.Simple;
                nameDividerImage.color = Color.white;
            }

            // G. Dialogue Body Text (Wide readable box)
            if (dialogueBodyText != null)
            {
                dialogueBodyText.transform.SetParent(contentObj.transform, false);
                RectTransform bodyRect = dialogueBodyText.GetComponent<RectTransform>();
                bodyRect.anchorMin = new Vector2(0f, 0f);
                bodyRect.anchorMax = new Vector2(1f, 1f);
                bodyRect.pivot = new Vector2(0f, 1f);
                bodyRect.offsetMin = new Vector2(0f, 0f);
                bodyRect.offsetMax = new Vector2(0f, -44f);

                dialogueBodyText.fontSize = 15.5f;
                dialogueBodyText.color = UITheme.ParchmentText; // #EDE6D8
                dialogueBodyText.alignment = TextAlignmentOptions.TopLeft;
                dialogueBodyText.enableAutoSizing = false;
                dialogueBodyText.lineSpacing = 2f;
                dialogueBodyText.paragraphSpacing = 6f;
                dialogueBodyText.textWrappingMode = TextWrappingModes.Normal;
                dialogueBodyText.overflowMode = TextOverflowModes.Overflow;
                dialogueBodyText.richText = true;
                dialogueBodyText.raycastTarget = false;
            }

            // H. Continue Button (Bottom-Right of Dialogue Panel)
            if (continueButton != null)
            {
                continueButton.transform.SetParent(dialoguePanel.transform, false);
                RectTransform contRect = continueButton.GetComponent<RectTransform>();
                contRect.anchorMin = new Vector2(1f, 0f);
                contRect.anchorMax = new Vector2(1f, 0f);
                contRect.pivot = new Vector2(1f, 0f);
                contRect.anchoredPosition = new Vector2(-24f, 16f);
                contRect.sizeDelta = new Vector2(160f, 38f);

                Image contImg = continueButton.GetComponent<Image>();
                if (contImg != null && buttonNormalSprite != null)
                {
                    contImg.sprite = buttonNormalSprite;
                    contImg.type = Image.Type.Sliced;
                    contImg.color = Color.white;
                }

                TMP_Text contTxt = continueButton.GetComponentInChildren<TMP_Text>(true);
                if (contTxt != null)
                {
                    contTxt.text = "Continue >";
                    contTxt.fontSize = 14.5f;
                    contTxt.fontStyle = FontStyles.Bold;
                    contTxt.color = UITheme.GoldAccent;
                    contTxt.alignment = TextAlignmentOptions.Center;
                    contTxt.enableAutoSizing = false;
                    contTxt.raycastTarget = false;
                }
            }

            // I. Options Container (Stacked above Dialogue Panel)
            if (optionsContainer != null)
            {
                optionsContainer.SetParent(transform, false);
                RectTransform optRect = optionsContainer.GetComponent<RectTransform>();
                optRect.anchorMin = new Vector2(0.5f, 0f);
                optRect.anchorMax = new Vector2(0.5f, 0f);
                optRect.pivot = new Vector2(0.5f, 0f);
                optRect.anchoredPosition = new Vector2(0f, 285f);
                optRect.sizeDelta = new Vector2(840f, 180f);

                VerticalLayoutGroup vlg = optionsContainer.GetComponent<VerticalLayoutGroup>();
                if (vlg == null) vlg = optionsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 8f;
                vlg.childAlignment = TextAnchor.LowerCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.padding = new RectOffset(0, 0, 0, 0);

                ContentSizeFitter csf = optionsContainer.GetComponent<ContentSizeFitter>();
                if (csf == null) csf = optionsContainer.gameObject.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
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
        /// Displays the dialogue node text, speaker info, portrait slot, and choice buttons.
        /// </summary>
        public void DisplayDialogueNode(DialogueNodeSO node)
        {
            if (node == null) return;

            AutoLocateComponents();
            EnsureStyledHierarchy();

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

            // 2. Left Portrait Slot (Always visible and framed)
            if (portraitFrameImage != null)
            {
                portraitFrameImage.enabled = true;
            }

            if (speakerPortraitImage != null)
            {
                speakerPortraitImage.enabled = true;
                speakerPortraitImage.preserveAspect = true;
                if (node.SpeakerPortrait != null)
                {
                    speakerPortraitImage.sprite = node.SpeakerPortrait;
                    speakerPortraitImage.color = Color.white;
                }
                else if (defaultPortraitPlaceholder != null)
                {
                    speakerPortraitImage.sprite = defaultPortraitPlaceholder;
                    speakerPortraitImage.color = Color.white;
                }
                else
                {
                    speakerPortraitImage.color = new Color(0.9f, 0.85f, 0.75f, 0.9f);
                }
            }

            // 3. Typewriter Text
            currentFullText = node.DialogueText;
            if (activeTypewriterCoroutine != null)
            {
                StopCoroutine(activeTypewriterCoroutine);
            }
            activeTypewriterCoroutine = StartCoroutine(TypewriterRoutine(currentFullText));

            // 4. Build Choice Options
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

            DialogueController dialogueController = DialogueController.Instance;
            IReadOnlyList<DialogueOption> options = DialogueController.ComposeOptions(node, dialogueController != null ? dialogueController.SessionOptions : null);

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
                        // Fallback button with fantasy styling
                        btnObj = new GameObject("OptionButton", typeof(RectTransform), typeof(Button), typeof(Image));
                        if (optionsContainer != null) btnObj.transform.SetParent(optionsContainer, false);
                    }

                    btnObj.SetActive(true);
                    spawnedButtons.Add(btnObj);

                    Button btn = btnObj.GetComponent<Button>();
                    Image btnImg = btnObj.GetComponent<Image>();
                    if (btnImg != null && buttonNormalSprite != null && btnImg.sprite == null)
                    {
                        btnImg.sprite = buttonNormalSprite;
                        btnImg.type = Image.Type.Sliced;
                    }

                    TMP_Text btnText = btnObj.GetComponentInChildren<TMP_Text>(true);

                    int neededRoll = Mathf.Clamp(option.TargetDC - playerBonus, 1, 20);

                    string cleanText = option.OptionText ?? "";
                    string formattedText;
                    if (option.RequiresCheck)
                    {
                        string checkTag = $"<color=#F6D378>[{option.SkillCheckDescription} | DC {option.TargetDC} (Need {neededRoll}+)]</color>";
                        if (cleanText.StartsWith("[DC", StringComparison.OrdinalIgnoreCase))
                        {
                            int closeBracket = cleanText.IndexOf(']');
                            if (closeBracket >= 0 && closeBracket < cleanText.Length - 1)
                            {
                                cleanText = cleanText.Substring(closeBracket + 1).TrimStart();
                            }
                        }
                        formattedText = $"{checkTag} {cleanText}";
                    }
                    else if (cleanText.StartsWith("[Exit]", StringComparison.OrdinalIgnoreCase) || cleanText.StartsWith("[Poistu", StringComparison.OrdinalIgnoreCase))
                    {
                        formattedText = $"<color=#E06666>[Exit]</color> {cleanText.Replace("[Exit]", "").Replace("[Poistu]", "").Trim()}";
                    }
                    else if (cleanText.StartsWith("[Blacksmith]", StringComparison.OrdinalIgnoreCase) || cleanText.StartsWith("[Shop]", StringComparison.OrdinalIgnoreCase))
                    {
                        formattedText = $"<color=#F6D378>[Blacksmith]</color> {cleanText.Replace("[Blacksmith]", "").Replace("[Shop]", "").Trim()}";
                    }
                    else if (cleanText.StartsWith("[Fight]", StringComparison.OrdinalIgnoreCase))
                    {
                        formattedText = $"<color=#FF7043>[Fight]</color> {cleanText.Replace("[Fight]", "").Trim()}";
                    }
                    else if (cleanText.StartsWith("[Lore]", StringComparison.OrdinalIgnoreCase))
                    {
                        formattedText = $"<color=#82B1FF>[Lore]</color> {cleanText.Replace("[Lore]", "").Trim()}";
                    }
                    else if (cleanText.StartsWith("[Quests]", StringComparison.OrdinalIgnoreCase))
                    {
                        formattedText = $"<color=#B9F6CA>[Quests]</color> {cleanText.Replace("[Quests]", "").Trim()}";
                    }
                    else if (cleanText.StartsWith("[Accept]", StringComparison.OrdinalIgnoreCase))
                    {
                        formattedText = $"<color=#81C784>[Accept]</color> {cleanText.Replace("[Accept]", "").Trim()}";
                    }
                    else if (cleanText.StartsWith("[Back]", StringComparison.OrdinalIgnoreCase))
                    {
                        formattedText = $"<color=#B0BEC5>[Back]</color> {cleanText.Replace("[Back]", "").Trim()}";
                    }
                    else
                    {
                        formattedText = cleanText;
                    }

                    if (btnText != null)
                    {
                        btnText.margin = new Vector4(20f, 0f, 20f, 0f);
                        btnText.enableAutoSizing = false;
                        btnText.fontSize = 15f;
                        btnText.color = new Color(0.94f, 0.90f, 0.82f, 1f);
                        btnText.alignment = TextAlignmentOptions.MidlineLeft;
                        btnText.textWrappingMode = TextWrappingModes.Normal;
                        btnText.overflowMode = TextOverflowModes.Ellipsis;
                        btnText.raycastTarget = false;
                        btnText.richText = true;
                        btnText.text = formattedText;
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
