using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Master controller for the Rune of Reroll interaction flow on the D20 modal.
    /// Implements MasterSpec §4.2 & §4.3:
    /// - When player possesses a Reroll Scroll, pauses the dice result modal.
    /// - Offers [ Continue ] (Accept result and continue) and [ Use Reroll Scroll (x remaining) ].
    /// - Consumes 1 scroll and triggers an immediate reroll against the same DC/bonus.
    /// </summary>
    public class RuneOfRerollController : MonoBehaviour
    {
        #region Singleton & Access

        public static RuneOfRerollController Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("Button References")]
        [Tooltip("Button allowing the player to accept the roll and continue without rerolling.")]
        [SerializeField] private Button continueButton;

        [Tooltip("Button that consumes 1 scroll to reroll the D20 against the same DC/AC.")]
        [SerializeField] private Button rerollButton;

        [Header("Text Displays")]
        [Tooltip("Text on continue button (e.g. 'Continue').")]
        [SerializeField] private TMP_Text continueButtonText;

        [Tooltip("Text on reroll button (e.g. 'Use Reroll Scroll (2)').")]
        [SerializeField] private TMP_Text rerollButtonText;

        [Header("Button Container (Optional)")]
        [Tooltip("Container holding both decision buttons.")]
        [SerializeField] private GameObject actionButtonsContainer;

        #endregion

        #region Private State

        private Action onContinueCallback;
        private Action onRerollCallback;
        private DiceResult lastResult;

        #endregion

        #region Public Properties

        /// <summary>True if the player possesses at least one reroll scroll.</summary>
        public bool HasRerollScrollAvailable => InventoryManager.Instance != null && InventoryManager.Instance.HasRerollScroll;

        /// <summary>Current count of reroll scrolls in inventory.</summary>
        public int AvailableRerollCount => InventoryManager.Instance != null ? InventoryManager.Instance.RerollScrollCount : 0;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            AutoLocateButtons();
            WireListeners();
            HideActionButtons();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(HandleContinueClicked);
            }
            if (rerollButton != null)
            {
                rerollButton.onClick.RemoveListener(HandleRerollClicked);
            }
        }

        #endregion

        #region Setup & Auto-Discovery

        /// <summary>
        /// Automatically discovers or generates Continue and Reroll buttons in the modal hierarchy.
        /// </summary>
        public void AutoLocateButtons()
        {
            // 1. Locate Continue Button (or DismissButton)
            if (continueButton == null)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);
                foreach (Button btn in buttons)
                {
                    string lower = btn.name.ToLowerInvariant();
                    if (lower.Contains("continue") || lower.Contains("dismiss") || lower.Contains("accept") || lower.Contains("ok"))
                    {
                        continueButton = btn;
                        break;
                    }
                }
            }

            if (continueButton != null && continueButtonText == null)
            {
                continueButtonText = continueButton.GetComponentInChildren<TMP_Text>(true);
            }

            // 2. Locate Reroll Button
            if (rerollButton == null)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);
                foreach (Button btn in buttons)
                {
                    string lower = btn.name.ToLowerInvariant();
                    if (lower.Contains("reroll") || lower.Contains("rune") || lower.Contains("scroll"))
                    {
                        rerollButton = btn;
                        break;
                    }
                }
            }

            if (rerollButton != null && rerollButtonText == null)
            {
                rerollButtonText = rerollButton.GetComponentInChildren<TMP_Text>(true);
            }

            // 3. Create Reroll Button beside Continue Button if not present in prefab
            if (rerollButton == null && continueButton != null)
            {
                EnsureRerollButtonCreated();
            }
        }

        private void EnsureRerollButtonCreated()
        {
            if (continueButton == null) return;

            Transform parent = continueButton.transform.parent;
            GameObject newRerollObj = Instantiate(continueButton.gameObject, parent);
            newRerollObj.name = "RerollButton";
            rerollButton = newRerollObj.GetComponent<Button>();
            rerollButtonText = newRerollObj.GetComponentInChildren<TMP_Text>(true);

            // Position side-by-side with continue button
            RectTransform contRect = continueButton.GetComponent<RectTransform>();
            RectTransform rerollRect = newRerollObj.GetComponent<RectTransform>();

            // Adjust both buttons to share bottom area
            contRect.anchorMin = new Vector2(0.5f, 0f);
            contRect.anchorMax = new Vector2(0.5f, 0f);
            contRect.pivot = new Vector2(0.5f, 0f);
            contRect.anchoredPosition = new Vector2(100f, 20f);
            contRect.sizeDelta = new Vector2(160f, 44f);

            rerollRect.anchorMin = new Vector2(0.5f, 0f);
            rerollRect.anchorMax = new Vector2(0.5f, 0f);
            rerollRect.pivot = new Vector2(0.5f, 0f);
            rerollRect.anchoredPosition = new Vector2(-100f, 20f);
            rerollRect.sizeDelta = new Vector2(210f, 44f);

            // Style reroll button with a subtle arcane/amber tint
            Image btnImg = newRerollObj.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = new Color(0.95f, 0.75f, 0.25f, 1f);
            }

            if (rerollButtonText != null)
            {
                rerollButtonText.text = "Use Reroll Scroll";
                rerollButtonText.fontSize = 12f;
            }
        }

        private void WireListeners()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(HandleContinueClicked);
                continueButton.onClick.AddListener(HandleContinueClicked);
            }

            if (rerollButton != null)
            {
                rerollButton.onClick.RemoveListener(HandleRerollClicked);
                rerollButton.onClick.AddListener(HandleRerollClicked);
            }
        }

        #endregion

        #region Flow & Presentation

        /// <summary>
        /// Evaluates whether the dice modal should pause for player decision.
        /// Returns true if player owns at least one scroll and can choose to reroll.
        /// </summary>
        public bool ShouldPauseForDecision(DiceResult result)
        {
            lastResult = result;
            return HasRerollScrollAvailable;
        }

        /// <summary>
        /// Prepares and displays the decision buttons after the roll animation reveals the result.
        /// </summary>
        public void PresentDecisionOptions(DiceResult result, Action onContinue, Action onReroll)
        {
            lastResult = result;
            onContinueCallback = onContinue;
            onRerollCallback = onReroll;

            int scrolls = AvailableRerollCount;

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
                if (continueButtonText != null)
                {
                    continueButtonText.text = "Continue";
                }
            }

            if (rerollButton != null)
            {
                if (scrolls > 0)
                {
                    rerollButton.gameObject.SetActive(true);
                    rerollButton.interactable = true;
                    if (rerollButtonText != null)
                    {
                        rerollButtonText.text = $"Use Reroll Scroll ({scrolls})";
                    }
                }
                else
                {
                    rerollButton.gameObject.SetActive(false);
                }
            }

            if (actionButtonsContainer != null)
            {
                actionButtonsContainer.SetActive(true);
            }
        }

        /// <summary>
        /// Hides decision buttons when the modal is closed or during rolling animation.
        /// </summary>
        public void HideActionButtons()
        {
            if (rerollButton != null)
            {
                rerollButton.gameObject.SetActive(false);
            }

            // Keep continue button hidden during roll shuffle
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(false);
            }

            if (actionButtonsContainer != null)
            {
                actionButtonsContainer.SetActive(false);
            }
        }

        #endregion

        #region Button Action Handlers

        private void HandleContinueClicked()
        {
            Debug.Log("[RuneOfRerollController] Player chose [Continue] - Accepted roll result.");
            HideActionButtons();

            if (onContinueCallback != null)
            {
                onContinueCallback.Invoke();
                onContinueCallback = null;
            }
            else if (DiceUIController.Instance != null)
            {
                DiceUIController.Instance.Dismiss();
            }
        }

        private void HandleRerollClicked()
        {
            if (!HasRerollScrollAvailable)
            {
                Debug.LogWarning("[RuneOfRerollController] Cannot reroll: No reroll scrolls available.");
                return;
            }

            bool consumed = InventoryManager.Instance.ConsumeRerollScroll();
            if (!consumed) return;

            Debug.Log($"[RuneOfRerollController] Consumed 1 scroll. Triggering reroll for DC {lastResult.targetDC} with bonus {lastResult.bonus}...");
            HideActionButtons();

            if (onRerollCallback != null)
            {
                onRerollCallback.Invoke();
                onRerollCallback = null;
            }
            else if (DiceUIController.Instance != null)
            {
                DiceUIController.Instance.TriggerReroll();
            }
        }

        #endregion
    }
}
