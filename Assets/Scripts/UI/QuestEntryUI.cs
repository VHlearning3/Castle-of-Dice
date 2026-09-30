using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// UI component representing a single quest row within the Quest_Tracker_Card list.
    /// Displays quest title, objective with its 'X/X' counter and location, or who to return to once done.
    /// Per MasterSpec §6.2: completed quests are shown with alpha = 0.5.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class QuestEntryUI : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Text References")]
        [Tooltip("Header text displaying quest title (e.g., '• Cellar Pests').")]
        [SerializeField] private TMP_Text titleText;

        [Tooltip("Description & status text displaying objective details.")]
        [SerializeField] private TMP_Text objectiveText;

        [Tooltip("Target counter displaying 'X/X' progression.")]
        [SerializeField] private TMP_Text counterText;

        [Header("Components")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform rectTransform;

        #endregion

        #region Public Properties

        /// <summary>Unique string ID of the tracked quest.</summary>
        public string QuestID { get; private set; }

        /// <summary>Whether this entry is currently flagged as completed.</summary>
        public bool IsCompleted { get; private set; }

        /// <summary>Whether the objective is done and the quest can be handed in.</summary>
        public bool IsReadyToTurnIn { get; private set; }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureComponents();
        }

        #endregion

        #region Setup & Formatting

        private void EnsureComponents()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            // Title + objective + location need three lines; the tracker's layout group reads this height
            LayoutElement layout = GetComponent<LayoutElement>();
            if (layout == null) layout = gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = RowHeight;
            layout.minHeight = RowHeight;
        }

        /// <summary>Height of one quest row in the tracker.</summary>
        public const float RowHeight = 58f;

        /// <summary>
        /// Populates this entry with live QuestSO state and progress.
        /// </summary>
        public void Setup(QuestSO quest, int currentProgress, QuestState state)
        {
            EnsureComponents();

            if (quest == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            QuestID = quest.QuestID;

            int required = Mathf.Max(1, quest.RequiredAmount);
            int current = Mathf.Clamp(currentProgress, 0, required);
            IsCompleted = state == QuestState.Completed;
            IsReadyToTurnIn = !IsCompleted && current >= required;

            // 1. Title
            if (titleText != null)
            {
                titleText.text = IsCompleted
                    ? $"• {quest.QuestTitle} <color=#2ECC71>(Completed)</color>"
                    : $"• {quest.QuestTitle}";
                titleText.color = IsCompleted ? new Color(0.82f, 0.92f, 0.82f, 1f) : UITheme.CreamText;
            }

            // 2. Counter
            string progressRatio = $"{current}/{required}";
            if (counterText != null)
            {
                counterText.text = IsCompleted ? "" : progressRatio;
                counterText.color = IsReadyToTurnIn ? new Color(0.18f, 0.80f, 0.44f, 1f) : new Color(0.96f, 0.78f, 0.20f, 1f);
            }

            // 3. Objective line + where to go
            if (objectiveText != null)
            {
                objectiveText.text = FormatObjective(quest, current, required, IsCompleted);
                objectiveText.color = IsCompleted ? new Color(0.72f, 0.82f, 0.72f, 1f) : new Color(0.85f, 0.88f, 0.90f, 1f);
            }

            // 4. Finished quests fade out (MasterSpec §6.2: alpha = 0.5) before the tracker drops them
            canvasGroup.alpha = IsCompleted ? 0.5f : 1.0f;
        }

        /// <summary>
        /// Builds the objective text for a tracker row: progress and location, or who to return to once done.
        /// </summary>
        public static string FormatObjective(QuestSO quest, int current, int required, bool completed)
        {
            if (quest == null) return "";

            if (completed)
            {
                return "  <color=#2ECC71>Reward received</color>";
            }

            if (current >= required)
            {
                string giver = string.IsNullOrEmpty(quest.QuestGiverName) ? "the quest giver" : quest.QuestGiverName;
                return $"  {quest.ObjectiveSummary}: <color=#2ECC71><b>{required}/{required}</b></color>\n  <color=#2ECC71>Return to {giver}</color>";
            }

            string line = $"  {quest.ObjectiveSummary}: <color=#F1C40F><b>{current}/{required}</b></color>";
            if (!string.IsNullOrEmpty(quest.ObjectiveLocation))
            {
                line += $"\n  <color=#A0AEC0>{quest.ObjectiveLocation}</color>";
            }
            return line;
        }

        /// <summary>
        /// Populates this entry with custom narrative guidance (e.g. initial exploration when no quests are active).
        /// </summary>
        public void SetupCustom(string title, string description, string counter = "", bool isCompleted = false)
        {
            EnsureComponents();
            gameObject.SetActive(true);

            QuestID = null;
            IsCompleted = isCompleted;
            IsReadyToTurnIn = false;

            if (titleText != null)
            {
                titleText.text = $"• {title}";
                titleText.color = UITheme.CreamText;
            }

            if (counterText != null)
            {
                counterText.text = counter;
                counterText.color = new Color(0.96f, 0.78f, 0.20f, 1f);
            }

            if (objectiveText != null)
            {
                string cSuffix = string.IsNullOrEmpty(counter) ? "" : $": <color=#F1C40F><b>{counter}</b></color>";
                objectiveText.text = $"  {description}{cSuffix}";
                objectiveText.color = new Color(0.80f, 0.84f, 0.88f, 1f);
            }

            canvasGroup.alpha = isCompleted ? 0.5f : 1.0f;
        }

        /// <summary>
        /// Programmatic setup for text references when built without pre-made prefabs.
        /// </summary>
        public void ConfigureReferences(TMP_Text title, TMP_Text objective, TMP_Text counter = null)
        {
            titleText = title;
            objectiveText = objective;
            counterText = counter;
            EnsureComponents();
        }

        #endregion
    }
}
