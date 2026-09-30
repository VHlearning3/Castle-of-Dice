using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Core;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Master controller for the top-right Quest HUD Tracker card (Quest_Tracker_Card).
    /// Implements MasterSpec §6.2, AGENTS.md §3.2, and UNITY_SETUP_GUIDE §4:
    /// - Anchored Top-Right [1, 1] with dark fantasy panel and gold divider.
    /// - Displays every accepted quest with its 'X/X' counter and location, or who to return to once done.
    /// - Collapsible via header [-] button (toggles to [+] when minimized).
    /// - Handed-in quests fade to alpha = 0.5 with '(Completed)' for a few seconds, then leave the card (the journal keeps them).
    /// - Self-healing auto-discovery & procedural hierarchy generation if UI objects are missing.
    /// </summary>
    public class QuestHUDUIController : MonoBehaviour
    {
        #region Singleton

        public static QuestHUDUIController Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("Card Root & Rect")]
        [Tooltip("Root RectTransform of the Quest_Tracker_Card.")]
        [SerializeField] private RectTransform cardRectTransform;

        [Tooltip("Background sliced image.")]
        [SerializeField] private Image cardBackgroundImage;

        [Header("Header Row")]
        [Tooltip("Header container holding title and minimize button.")]
        [SerializeField] private RectTransform headerRow;

        [Tooltip("Title text displaying 'QUEST OBJECTIVES'.")]
        [SerializeField] private TMP_Text headerTitleText;

        [Tooltip("Button toggling card between expanded and collapsed states.")]
        [SerializeField] private Button minimizeButton;

        [Tooltip("Text on the minimize button ('[ - ]' or '[ + ]').")]
        [SerializeField] private TMP_Text minimizeButtonText;

        [Tooltip("Decorative gold divider line.")]
        [SerializeField] private GameObject dividerGold;

        [Header("Quest List Container")]
        [Tooltip("Container holding individual quest entry rows with VerticalLayoutGroup.")]
        [SerializeField] private RectTransform questListContainer;

        [Tooltip("Optional prefab for quest entries. If null, entries are procedurally created.")]
        [SerializeField] private GameObject questEntryPrefab;

        [Header("Dimensions & Collapse Settings")]
        [SerializeField] private bool isCollapsed = false;
        [SerializeField] private float cardWidth = 380f;
        [SerializeField] private float collapsedHeight = 36f;
        [SerializeField] private float minExpandedHeight = 140f;

        [Header("Theme Sprites")]
        [SerializeField] private Sprite panelDarkSprite;
        [SerializeField] private Sprite dividerGoldSprite;

        #endregion

        #region Private State

        private readonly List<QuestEntryUI> spawnedEntries = new List<QuestEntryUI>();

        // Quests just handed in stay on the card briefly (faded, "Completed") before dropping off
        private struct RecentCompletion
        {
            public string QuestID;
            public float HideAt;
        }

        private readonly List<RecentCompletion> recentCompletions = new List<RecentCompletion>();
        private readonly List<QuestSO> displayBuffer = new List<QuestSO>();

        /// <summary>How long a handed-in quest stays on the card.</summary>
        public const float CompletedLingerSeconds = 4f;

        #endregion

        #region Public Properties

        /// <summary>Whether the quest card is currently minimized.</summary>
        public bool IsCollapsed => isCollapsed;

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

            LoadThemeSpritesIfMissing();
            AutoLocateOrBuildHierarchy();
            WireListeners();
        }

        private void Start()
        {
            RefreshQuestList();
        }

        private void Update()
        {
            if (recentCompletions.Count == 0) return;

            bool expired = false;
            float now = Time.unscaledTime;
            for (int i = recentCompletions.Count - 1; i >= 0; i--)
            {
                if (now >= recentCompletions[i].HideAt)
                {
                    recentCompletions.RemoveAt(i);
                    expired = true;
                }
            }

            if (expired) RefreshQuestList();
        }

        private void OnEnable()
        {
            QuestManager.OnQuestStateUpdated += HandleQuestStateUpdated;
            QuestManager.OnQuestProgressUpdated += HandleQuestProgressUpdated;
            QuestManager.OnQuestCompleted += HandleQuestCompleted;
            InventoryManager.OnScrapMetalChanged += HandleScrapMetalChanged;

            RefreshQuestList();
        }

        private void OnDisable()
        {
            QuestManager.OnQuestStateUpdated -= HandleQuestStateUpdated;
            QuestManager.OnQuestProgressUpdated -= HandleQuestProgressUpdated;
            QuestManager.OnQuestCompleted -= HandleQuestCompleted;
            InventoryManager.OnScrapMetalChanged -= HandleScrapMetalChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (minimizeButton != null)
            {
                minimizeButton.onClick.RemoveListener(ToggleCollapse);
            }
        }

        #endregion

        #region Collapse / Expand Operations

        /// <summary>
        /// Toggles between expanded and minimized states.
        /// </summary>
        public void ToggleCollapse()
        {
            SetCollapsed(!isCollapsed);
        }

        /// <summary>
        /// Sets explicit collapsed or expanded state and resizes card.
        /// </summary>
        public void SetCollapsed(bool collapsed)
        {
            isCollapsed = collapsed;

            if (minimizeButtonText != null)
            {
                minimizeButtonText.text = isCollapsed ? "[ + ]" : "[ - ]";
            }

            if (questListContainer != null)
            {
                questListContainer.gameObject.SetActive(!isCollapsed);
            }

            if (dividerGold != null)
            {
                dividerGold.SetActive(!isCollapsed);
            }

            if (cardRectTransform != null)
            {
                float targetH = isCollapsed ? collapsedHeight : CalculateExpandedHeight();
                cardRectTransform.sizeDelta = new Vector2(cardWidth, targetH);
            }
        }

        private float CalculateExpandedHeight()
        {
            int visibleCount = 0;
            for (int i = 0; i < spawnedEntries.Count; i++)
            {
                if (spawnedEntries[i] != null && spawnedEntries[i].gameObject.activeSelf)
                {
                    visibleCount++;
                }
            }

            if (visibleCount == 0) return minExpandedHeight;

            // Header ~36px + Divider ~6px + (Entry row + 6px spacing) * count + bottom padding ~14px
            float computed = 44f + (visibleCount * (QuestEntryUI.RowHeight + 6f)) + 14f;
            return Mathf.Max(minExpandedHeight, computed);
        }

        #endregion

        #region Quest List Synchronization

        /// <summary>
        /// Refreshes all quest entries in the tracker card based on QuestManager's current state.
        /// Called automatically on state changes or via QuestHUDUIController.Instance.RefreshQuestList().
        /// </summary>
        public void RefreshQuestList()
        {
            if (questListContainer == null) return;

            QuestManager qm = QuestManager.Instance;
            CollectDisplayedQuests(qm, displayBuffer);

            int entryIndex = 0;

            if (displayBuffer.Count > 0)
            {
                for (int i = 0; i < displayBuffer.Count; i++)
                {
                    QuestSO quest = displayBuffer[i];
                    QuestEntryUI entry = GetOrCreateEntry(entryIndex);
                    entry.Setup(quest, qm.GetQuestProgress(quest.QuestID), qm.GetQuestState(quest.QuestID));
                    entryIndex++;
                }
            }
            else
            {
                // Guidance before any quest is accepted
                QuestEntryUI entry = GetOrCreateEntry(entryIndex);
                entry.SetupCustom(
                    "Oakhaven Village",
                    "Villagers marked with <color=#F1C40F><b>!</b></color> have work for you.\n  <color=#A0AEC0>[J] Quest journal</color>");
                entryIndex++;
            }

            // Deactivate any unused pooled entries
            for (int i = entryIndex; i < spawnedEntries.Count; i++)
            {
                if (spawnedEntries[i] != null)
                {
                    spawnedEntries[i].gameObject.SetActive(false);
                }
            }

            // Recalculate card height if expanded
            if (!isCollapsed && cardRectTransform != null)
            {
                cardRectTransform.sizeDelta = new Vector2(cardWidth, CalculateExpandedHeight());
            }
        }

        /// <summary>
        /// The quests the card shows: every accepted quest, then quests handed in during the last few seconds.
        /// Older completed quests live only in the journal.
        /// </summary>
        public void CollectDisplayedQuests(QuestManager qm, List<QuestSO> result)
        {
            result.Clear();
            if (qm == null) return;

            List<QuestSO> tracked = qm.GetTrackedQuests();
            for (int i = 0; i < tracked.Count; i++)
            {
                QuestSO quest = tracked[i];
                if (quest != null && qm.GetQuestState(quest.QuestID) == QuestState.InProgress) result.Add(quest);
            }

            for (int i = 0; i < tracked.Count; i++)
            {
                QuestSO quest = tracked[i];
                if (quest != null && qm.GetQuestState(quest.QuestID) == QuestState.Completed && IsRecentlyCompleted(quest.QuestID))
                {
                    result.Add(quest);
                }
            }
        }

        private bool IsRecentlyCompleted(string questID)
        {
            for (int i = 0; i < recentCompletions.Count; i++)
            {
                if (string.Equals(recentCompletions[i].QuestID, questID, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// Keeps a just-completed quest on the card for <see cref="CompletedLingerSeconds"/>.
        /// </summary>
        public void MarkRecentlyCompleted(string questID)
        {
            if (string.IsNullOrEmpty(questID) || IsRecentlyCompleted(questID)) return;
            recentCompletions.Add(new RecentCompletion { QuestID = questID, HideAt = Time.unscaledTime + CompletedLingerSeconds });
        }

        private QuestEntryUI GetOrCreateEntry(int index)
        {
            while (spawnedEntries.Count <= index)
            {
                QuestEntryUI newEntry = CreateEntryInstance();
                spawnedEntries.Add(newEntry);
            }

            QuestEntryUI entry = spawnedEntries[index];
            if (entry == null)
            {
                entry = CreateEntryInstance();
                spawnedEntries[index] = entry;
            }

            entry.gameObject.SetActive(true);
            return entry;
        }

        private QuestEntryUI CreateEntryInstance()
        {
            if (questEntryPrefab != null)
            {
                GameObject obj = Instantiate(questEntryPrefab, questListContainer, false);
                QuestEntryUI comp = obj.GetComponent<QuestEntryUI>() ?? obj.AddComponent<QuestEntryUI>();
                return comp;
            }

            // Procedural instantiation if no prefab assigned
            GameObject rowObj = new GameObject($"QuestEntry_{spawnedEntries.Count + 1}", typeof(RectTransform), typeof(CanvasGroup), typeof(QuestEntryUI));
            rowObj.transform.SetParent(questListContainer, false);

            RectTransform rowRect = rowObj.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, QuestEntryUI.RowHeight);

            // Title Text
            GameObject titleObj = new GameObject("Quest_Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(rowObj.transform, false);
            RectTransform tRect = titleObj.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0f, 1f);
            tRect.anchorMax = new Vector2(1f, 1f);
            tRect.pivot = new Vector2(0f, 1f);
            tRect.anchoredPosition = new Vector2(0f, 0f);
            tRect.sizeDelta = new Vector2(0f, 18f);

            TMP_Text titleTMP = titleObj.GetComponent<TextMeshProUGUI>();
            titleTMP.fontSize = 12f;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.TopLeft;
            titleTMP.color = UITheme.CreamText;
            titleTMP.raycastTarget = false;

            // Objective Text
            GameObject objObj = new GameObject("Quest_Objective", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            objObj.transform.SetParent(rowObj.transform, false);
            RectTransform oRect = objObj.GetComponent<RectTransform>();
            oRect.anchorMin = new Vector2(0f, 0f);
            oRect.anchorMax = new Vector2(1f, 1f);
            oRect.pivot = new Vector2(0f, 1f);
            oRect.anchoredPosition = new Vector2(0f, -20f);
            oRect.sizeDelta = new Vector2(0f, -22f);

            TMP_Text objTMP = objObj.GetComponent<TextMeshProUGUI>();
            objTMP.fontSize = 11f;
            objTMP.alignment = TextAlignmentOptions.TopLeft;
            objTMP.color = new Color(0.85f, 0.88f, 0.90f, 1f);
            objTMP.textWrappingMode = TextWrappingModes.Normal;
            objTMP.raycastTarget = false;

            QuestEntryUI entryComp = rowObj.GetComponent<QuestEntryUI>();
            entryComp.ConfigureReferences(titleTMP, objTMP, null);

            return entryComp;
        }

        #endregion

        #region Event Handlers

        private void HandleQuestStateUpdated(string questID, QuestState state)
        {
            RefreshQuestList();
        }

        private void HandleQuestProgressUpdated(string questID, int current, int required)
        {
            RefreshQuestList();
        }

        private void HandleQuestCompleted(QuestSO quest, int goldAwarded)
        {
            if (quest != null) MarkRecentlyCompleted(quest.QuestID);
            RefreshQuestList();
        }

        private void HandleScrapMetalChanged(int newCount)
        {
            RefreshQuestList();
        }

        #endregion

        #region Setup & Auto-Discovery

        private void WireListeners()
        {
            if (minimizeButton != null)
            {
                minimizeButton.onClick.RemoveListener(ToggleCollapse);
                minimizeButton.onClick.AddListener(ToggleCollapse);
            }
        }

        public void AutoLocateOrBuildHierarchy()
        {
            // 1. Resolve Card Root
            if (cardRectTransform == null)
            {
                cardRectTransform = GetComponent<RectTransform>();
            }

            if (cardRectTransform == null)
            {
                Transform found = transform.Find("Quest_Tracker_Card");
                if (found != null) cardRectTransform = found.GetComponent<RectTransform>();
            }

            if (cardRectTransform != null)
            {
                cardRectTransform.anchorMin = new Vector2(1f, 1f);
                cardRectTransform.anchorMax = new Vector2(1f, 1f);
                cardRectTransform.pivot = new Vector2(1f, 1f);
                cardRectTransform.anchoredPosition = new Vector2(-24f, -18f);
                cardRectTransform.sizeDelta = new Vector2(cardWidth, isCollapsed ? collapsedHeight : minExpandedHeight);

                // Background
                cardBackgroundImage = cardRectTransform.GetComponent<Image>();
                if (cardBackgroundImage == null)
                {
                    cardBackgroundImage = cardRectTransform.gameObject.AddComponent<Image>();
                }
                if (panelDarkSprite != null)
                {
                    cardBackgroundImage.sprite = panelDarkSprite;
                    cardBackgroundImage.type = Image.Type.Sliced;
                    cardBackgroundImage.color = Color.white;
                }
            }

            // 2. Resolve Header Row
            Transform rootTr = cardRectTransform != null ? cardRectTransform : transform;
            Transform headerTr = rootTr.Find("Header_Row") ?? rootTr.Find("Quest_Header_Row");
            if (headerTr == null)
            {
                GameObject hrObj = new GameObject("Header_Row", typeof(RectTransform));
                hrObj.transform.SetParent(rootTr, false);
                headerTr = hrObj.transform;
            }

            headerRow = headerTr.GetComponent<RectTransform>();
            headerRow.anchorMin = new Vector2(0f, 1f);
            headerRow.anchorMax = new Vector2(1f, 1f);
            headerRow.pivot = new Vector2(0f, 1f);
            headerRow.anchoredPosition = new Vector2(16f, -8f);
            headerRow.sizeDelta = new Vector2(-32f, 24f);

            // 2A. Header Title Text
            Transform titleTr = headerRow.Find("Header_Title_Text") ?? rootTr.Find("Quest_Header_Text");
            if (titleTr == null)
            {
                GameObject tObj = new GameObject("Header_Title_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                tObj.transform.SetParent(headerRow, false);
                titleTr = tObj.transform;
            }
            else
            {
                titleTr.SetParent(headerRow, false);
            }

            headerTitleText = titleTr.GetComponent<TMP_Text>();
            headerTitleText.text = "QUEST OBJECTIVES";
            headerTitleText.fontSize = 12f;
            headerTitleText.fontStyle = FontStyles.Bold;
            headerTitleText.characterSpacing = 1.5f;
            headerTitleText.color = new Color(0.92f, 0.78f, 0.38f, 1f); // Rich gold
            headerTitleText.alignment = TextAlignmentOptions.MidlineLeft;
            headerTitleText.raycastTarget = false;

            RectTransform htRect = titleTr.GetComponent<RectTransform>();
            htRect.anchorMin = new Vector2(0f, 0f);
            htRect.anchorMax = new Vector2(1f, 1f);
            htRect.pivot = new Vector2(0f, 0.5f);
            htRect.anchoredPosition = new Vector2(0f, 0f);
            htRect.sizeDelta = new Vector2(-46f, 0f);

            // 2B. Minimize Button [ - ]
            Transform btnTr = headerRow.Find("Minimize_Button");
            if (btnTr == null)
            {
                GameObject bObj = new GameObject("Minimize_Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                bObj.transform.SetParent(headerRow, false);
                btnTr = bObj.transform;
            }

            minimizeButton = btnTr.GetComponent<Button>();
            Image btnImg = btnTr.GetComponent<Image>();
            btnImg.color = new Color(0.2f, 0.2f, 0.25f, 0.6f);

            RectTransform bRect = btnTr.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(1f, 0.5f);
            bRect.anchorMax = new Vector2(1f, 0.5f);
            bRect.pivot = new Vector2(1f, 0.5f);
            bRect.anchoredPosition = new Vector2(0f, 0f);
            bRect.sizeDelta = new Vector2(34f, 22f);

            Transform btnTxtTr = btnTr.Find("Text (TMP)") ?? btnTr.Find("Minimize_Text");
            if (btnTxtTr == null)
            {
                GameObject btObj = new GameObject("Minimize_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                btObj.transform.SetParent(btnTr, false);
                btnTxtTr = btObj.transform;
            }

            minimizeButtonText = btnTxtTr.GetComponent<TMP_Text>();
            minimizeButtonText.text = isCollapsed ? "[ + ]" : "[ - ]";
            minimizeButtonText.fontSize = 12f;
            minimizeButtonText.fontStyle = FontStyles.Bold;
            minimizeButtonText.alignment = TextAlignmentOptions.Center;
            minimizeButtonText.color = new Color(0.95f, 0.85f, 0.50f, 1f);
            minimizeButtonText.raycastTarget = false;

            RectTransform btRect = btnTxtTr.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.sizeDelta = Vector2.zero;

            // 3. Gold Divider Line
            Transform divTr = rootTr.Find("Divider_Gold");
            if (divTr == null)
            {
                GameObject divObj = new GameObject("Divider_Gold", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                divObj.transform.SetParent(rootTr, false);
                divTr = divObj.transform;
            }

            dividerGold = divTr.gameObject;
            RectTransform dRect = divTr.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0f, 1f);
            dRect.anchorMax = new Vector2(1f, 1f);
            dRect.pivot = new Vector2(0.5f, 1f);
            dRect.anchoredPosition = new Vector2(0f, -32f);
            dRect.sizeDelta = new Vector2(-32f, 3f);

            Image dImg = divTr.GetComponent<Image>();
            if (dividerGoldSprite != null)
            {
                dImg.sprite = dividerGoldSprite;
                dImg.type = Image.Type.Sliced;
            }

            // 4. Quest List Container
            Transform listTr = rootTr.Find("QuestListContainer");
            if (listTr == null)
            {
                GameObject listObj = new GameObject("QuestListContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
                listObj.transform.SetParent(rootTr, false);
                listTr = listObj.transform;
            }

            questListContainer = listTr.GetComponent<RectTransform>();
            questListContainer.anchorMin = new Vector2(0f, 0f);
            questListContainer.anchorMax = new Vector2(1f, 1f);
            questListContainer.pivot = new Vector2(0f, 1f);
            questListContainer.anchoredPosition = new Vector2(16f, -38f);
            questListContainer.sizeDelta = new Vector2(-32f, -46f);

            VerticalLayoutGroup vlg = questListContainer.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 5. Hide legacy single text if present to prevent visual duplication
            Transform legacySummary = rootTr.Find("ActiveQuestSummaryText");
            if (legacySummary != null)
            {
                legacySummary.gameObject.SetActive(false);
            }

            // 6. Hook existing entries if any
            spawnedEntries.Clear();
            QuestEntryUI[] existingEntries = questListContainer.GetComponentsInChildren<QuestEntryUI>(true);
            spawnedEntries.AddRange(existingEntries);
        }

        public void LoadThemeSpritesIfMissing()
        {
            if (panelDarkSprite == null)
            {
                panelDarkSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Panel_Dark.png");
            }
            if (dividerGoldSprite == null)
            {
                dividerGoldSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png");
            }
        }

        #endregion
    }
}
