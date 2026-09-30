using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CastleOfTheD20.Audio;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// Quest journal opened with [J]: every accepted quest with who gave it, where to go, the objective counter
    /// and the reward, followed by the quests already handed in. [J] or [Esc] closes it.
    /// </summary>
    public class QuestJournalUI : MonoBehaviour
    {
        #region Singleton

        public static QuestJournalUI Instance { get; private set; }

        #endregion

        #region Private State

        private GameObject journalPanel;
        private TMP_Text bodyText;
        private readonly StringBuilder builder = new StringBuilder(1024);

        #endregion

        #region Public Properties

        /// <summary>Whether the journal is on screen.</summary>
        public bool IsOpen => journalPanel != null && journalPanel.activeSelf;

        /// <summary>Text currently shown in the journal body.</summary>
        public string BodyText => bodyText != null ? bodyText.text : "";

        #endregion

        #region Bootstrap

        /// <summary>
        /// Creates the persistent quest UI (journal + notification banner) once per game session.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateQuestUI()
        {
            if (FindAnyObjectByType<QuestJournalUI>() != null) return;

            GameObject root = new GameObject("Quest_UI");
            DontDestroyOnLoad(root);
            root.AddComponent<QuestJournalUI>();
            if (FindAnyObjectByType<QuestNotificationUI>() == null) root.AddComponent<QuestNotificationUI>();
        }

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
            EnsurePanel();
            journalPanel.SetActive(false);
        }

        private void OnEnable()
        {
            QuestManager.OnQuestStateUpdated += HandleQuestStateUpdated;
            QuestManager.OnQuestProgressUpdated += HandleQuestProgressUpdated;
        }

        private void OnDisable()
        {
            QuestManager.OnQuestStateUpdated -= HandleQuestStateUpdated;
            QuestManager.OnQuestProgressUpdated -= HandleQuestProgressUpdated;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // The expedition map [M] takes over the screen; it restores movement itself when closed
            if (IsOpen && DungeonMapUIController.Instance != null && DungeonMapUIController.Instance.IsMapOpen)
            {
                journalPanel.SetActive(false);
                return;
            }

            if (GameInput.IsJournalHotkeyPressed())
            {
                if (IsOpen) Close();
                else if (CanOpenNow()) Open();
            }
            else if (IsOpen && GameInput.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Shows the journal and pauses exploration movement.
        /// </summary>
        public void Open()
        {
            EnsurePanel();
            Refresh();
            journalPanel.SetActive(true);
            GameInput.SetExplorationInputEnabled(false);
            SFXManager.Instance?.PlaySFX(SFXClipType.ButtonClick);
        }

        /// <summary>
        /// Hides the journal and restores exploration movement.
        /// </summary>
        public void Close()
        {
            if (!IsOpen) return;
            journalPanel.SetActive(false);
            GameInput.SetExplorationInputEnabled(true);
            SFXManager.Instance?.PlaySFX(SFXClipType.ButtonClick);
        }

        /// <summary>
        /// Rebuilds the journal text from QuestManager.
        /// </summary>
        public void Refresh()
        {
            EnsurePanel();
            QuestManager quests = FindAnyObjectByType<QuestManager>();
            bodyText.text = BuildJournalText(quests, builder);
        }

        /// <summary>
        /// Journal contents: active quests first, then completed ones.
        /// </summary>
        public static string BuildJournalText(QuestManager quests, StringBuilder sb = null)
        {
            if (sb == null) sb = new StringBuilder(1024);
            sb.Clear();

            List<QuestSO> tracked = quests != null ? quests.GetTrackedQuests() : null;
            bool anyActive = false;
            bool anyDone = false;

            if (tracked != null)
            {
                for (int i = 0; i < tracked.Count; i++)
                {
                    QuestSO quest = tracked[i];
                    if (quest == null || quests.GetQuestState(quest.QuestID) != QuestState.InProgress) continue;
                    if (!anyActive) sb.Append("<color=#EBC76B><b>ACTIVE</b></color>\n\n");
                    anyActive = true;
                    AppendQuest(sb, quests, quest, false);
                }

                for (int i = 0; i < tracked.Count; i++)
                {
                    QuestSO quest = tracked[i];
                    if (quest == null || quests.GetQuestState(quest.QuestID) != QuestState.Completed) continue;
                    if (!anyDone) sb.Append(anyActive ? "\n<color=#EBC76B><b>COMPLETED</b></color>\n\n" : "<color=#EBC76B><b>COMPLETED</b></color>\n\n");
                    anyDone = true;
                    AppendQuest(sb, quests, quest, true);
                }
            }

            if (!anyActive && !anyDone)
            {
                sb.Append("No quests yet.\n\n<color=#A0AEC0>Villagers marked with <color=#F1C40F><b>!</b></color> have work for you.</color>");
            }

            return sb.ToString();
        }

        #endregion

        #region Formatting

        private static void AppendQuest(StringBuilder sb, QuestManager quests, QuestSO quest, bool completed)
        {
            int required = Mathf.Max(1, quest.RequiredAmount);
            int current = Mathf.Clamp(quests.GetQuestProgress(quest.QuestID), 0, required);
            bool ready = !completed && current >= required;

            sb.Append("<size=22><b>").Append(quest.QuestTitle).Append("</b></size>  ");
            if (completed) sb.Append("<color=#2ECC71>Completed</color>");
            else if (ready) sb.Append("<color=#2ECC71>Ready to hand in</color>");
            else sb.Append("<color=#F1C40F>In progress</color>");
            sb.Append('\n');

            if (!string.IsNullOrEmpty(quest.QuestGiverName))
            {
                sb.Append("<color=#A0AEC0>Given by ").Append(quest.QuestGiverName);
                if (!string.IsNullOrEmpty(quest.ObjectiveLocation)) sb.Append("  •  ").Append(quest.ObjectiveLocation);
                sb.Append("</color>\n");
            }

            if (!completed && !string.IsNullOrEmpty(quest.Description))
            {
                sb.Append(quest.Description).Append('\n');
            }

            if (completed)
            {
                sb.Append("<color=#A0AEC0>Reward received:</color> ");
            }
            else
            {
                sb.Append("Objective: ").Append(quest.ObjectiveSummary).Append(' ');
                sb.Append(ready ? "<color=#2ECC71>" : "<color=#F1C40F>").Append(current).Append('/').Append(required).Append("</color>");
                if (ready)
                {
                    string giver = string.IsNullOrEmpty(quest.QuestGiverName) ? "the quest giver" : quest.QuestGiverName;
                    sb.Append("  <color=#2ECC71>Return to ").Append(giver).Append("</color>");
                }
                sb.Append("\n<color=#A0AEC0>Reward:</color> ");
            }

            ItemSO item = quests.GetRewardItem(quest);
            sb.Append(QuestNotificationUI.FormatRewards(quests.GetRewardGold(quest), item, quest.RewardItemAmount));
            sb.Append("\n\n");
        }

        #endregion

        #region Internals

        private static bool CanOpenNow()
        {
            // Only while exploring a zone (not in the main menu, a fight, a conversation, the shop or the map)
            if (FindAnyObjectByType<PlayerUnit>() == null) return false;

            GameManager game = FindAnyObjectByType<GameManager>();
            if (game != null && game.CurrentMode != GamePlayMode.Exploration) return false;

            DungeonMapUIController map = DungeonMapUIController.Instance;
            return map == null || !map.IsMapOpen;
        }

        private void HandleQuestStateUpdated(string questID, QuestState state)
        {
            if (IsOpen) Refresh();
        }

        private void HandleQuestProgressUpdated(string questID, int current, int required)
        {
            if (IsOpen) Refresh();
        }

        private void EnsurePanel()
        {
            if (journalPanel != null) return;

            Canvas canvas = QuestUIBuilder.CreateOverlayCanvas(transform, "Quest_Journal_Canvas", 70);

            // Dim the world behind the journal
            GameObject dim = new GameObject("Quest_Journal", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dim.transform.SetParent(canvas.transform, false);
            RectTransform dimRect = dim.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            journalPanel = dim;

            RectTransform panel = QuestUIBuilder.CreatePanel(dim.transform, "Journal_Panel",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 660f), new Color(0.06f, 0.07f, 0.1f, 0.97f));

            TMP_Text title = QuestUIBuilder.CreateLabel(panel, "Journal_Title", 26f, FontStyles.Bold, UITheme.GoldAccent,
                TextAlignmentOptions.TopLeft, new Vector4(36f, 24f, 36f, 600f));
            title.text = "QUEST JOURNAL";
            title.characterSpacing = 3f;

            TMP_Text hint = QuestUIBuilder.CreateLabel(panel, "Journal_Close_Hint", 14f, FontStyles.Normal, UITheme.SoftText,
                TextAlignmentOptions.TopRight, new Vector4(36f, 30f, 36f, 600f));
            hint.text = "[J] / [ESC] Close";

            // Gold divider under the title
            GameObject divider = new GameObject("Journal_Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            divider.transform.SetParent(panel, false);
            RectTransform divRect = divider.GetComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0f, 1f);
            divRect.anchorMax = new Vector2(1f, 1f);
            divRect.pivot = new Vector2(0.5f, 1f);
            divRect.anchoredPosition = new Vector2(0f, -70f);
            divRect.sizeDelta = new Vector2(-72f, 3f);
            Image divImage = divider.GetComponent<Image>();
            Sprite divSprite = UITheme.GetSprite("Assets/UI/Sprites/UI_Fantasy_Divider_Gold.png");
            if (divSprite != null)
            {
                divImage.sprite = divSprite;
                divImage.type = Image.Type.Sliced;
            }
            else
            {
                divImage.color = UITheme.GoldAccent;
            }

            bodyText = QuestUIBuilder.CreateLabel(panel, "Journal_Body", 16f, FontStyles.Normal, UITheme.CreamText,
                TextAlignmentOptions.TopLeft, new Vector4(36f, 88f, 36f, 24f));
            bodyText.overflowMode = TextOverflowModes.Ellipsis;
        }

        #endregion
    }
}
