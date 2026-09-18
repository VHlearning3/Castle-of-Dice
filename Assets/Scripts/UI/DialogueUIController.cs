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

        #region Private State

        private Coroutine activeTypewriterCoroutine;
        private readonly List<GameObject> spawnedButtons = new List<GameObject>();
        private string currentFullText = "";

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (dialoguePanel != null)
            {
                dialoguePanel.SetActive(false);
            }

            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinueClicked);
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

        private void DisplayDialogueNode(DialogueNodeSO node)
        {
            if (node == null) return;

            if (dialoguePanel != null)
            {
                dialoguePanel.SetActive(true);
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

            IReadOnlyList<DialogueOption> options = node.Options;

            if (options != null && options.Count > 0)
            {
                if (continueButton != null) continueButton.gameObject.SetActive(false);

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

                    spawnedButtons.Add(btnObj);

                    Button btn = btnObj.GetComponent<Button>();
                    TMP_Text btnText = btnObj.GetComponentInChildren<TMP_Text>();

                    if (btnText != null)
                    {
                        if (option.RequiresCheck)
                        {
                            btnText.text = $"[D20 DC {option.TargetDC} - {option.SkillCheckDescription}] {option.OptionText}";
                        }
                        else
                        {
                            btnText.text = option.OptionText;
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

        private void HideDialogue()
        {
            if (activeTypewriterCoroutine != null)
            {
                StopCoroutine(activeTypewriterCoroutine);
                activeTypewriterCoroutine = null;
            }

            ClearSpawnedButtons();

            if (dialoguePanel != null)
            {
                dialoguePanel.SetActive(false);
            }
        }

        #endregion
    }
}
