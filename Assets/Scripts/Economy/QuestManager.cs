using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Economy
{
    /// <summary>
    /// Singleton manager tracking quest life cycles, objective progress, and reward distribution.
    /// Handles village side quests: Cellar Rats, Lost Signet Ring, and Swamp Herbs.
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        #region Singleton

        private static QuestManager instance;

        public static QuestManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<QuestManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("QuestManager");
                        instance = go.AddComponent<QuestManager>();
                        Debug.Log("[QuestManager] Auto-created QuestManager GameObject in scene.");
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        #endregion

        #region Serialized Fields

        [Header("Quest Definitions")]
        [Tooltip("Pre-configured QuestSO definitions registered at game start.")]
        [SerializeField] private List<QuestSO> questDatabase = new List<QuestSO>();

        #endregion

        #region Private State

        private readonly Dictionary<string, QuestSO> registeredQuests = new Dictionary<string, QuestSO>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, QuestState> questStates = new Dictionary<string, QuestState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> questProgress = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Events

        /// <summary>Fired when a quest state transitions: (questID, newState).</summary>
        public static event Action<string, QuestState> OnQuestStateUpdated;

        /// <summary>Fired when objective counter increases: (questID, currentAmount, requiredAmount).</summary>
        public static event Action<string, int, int> OnQuestProgressUpdated;

        /// <summary>Fired when a quest is finalized and rewards are granted: (quest, totalGoldAwarded).</summary>
        public static event Action<QuestSO, int> OnQuestCompleted;

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

            InitializeQuests();
        }

        private void OnEnable()
        {
            InventoryManager.OnScrapMetalChanged += HandleScrapMetalChanged;
        }

        private void OnDisable()
        {
            InventoryManager.OnScrapMetalChanged -= HandleScrapMetalChanged;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        #endregion

        #region Quest Initialization

        private void InitializeQuests()
        {
            foreach (var quest in questDatabase)
            {
                if (quest != null && !string.IsNullOrEmpty(quest.QuestID))
                {
                    RegisterQuest(quest);
                }
            }

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:QuestSO");
            foreach (var g in guids)
            {
                string p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                QuestSO q = UnityEditor.AssetDatabase.LoadAssetAtPath<QuestSO>(p);
                if (q != null) RegisterQuest(q);
            }
#endif
        }

        /// <summary>
        /// Registers a QuestSO into the active tracking tables.
        /// </summary>
        public void RegisterQuest(QuestSO quest)
        {
            if (quest == null || string.IsNullOrEmpty(quest.QuestID)) return;

            registeredQuests[quest.QuestID] = quest;

            if (!questStates.ContainsKey(quest.QuestID))
            {
                questStates[quest.QuestID] = quest.DefaultState;
                questProgress[quest.QuestID] = 0;
            }
        }

        #endregion

        #region Quest Operations

        /// <summary>
        /// Starts a quest by setting its state to InProgress.
        /// </summary>
        public bool StartQuest(string questID)
        {
            if (string.IsNullOrEmpty(questID)) return false;

            if (!registeredQuests.ContainsKey(questID))
            {
                Debug.LogWarning($"[QuestManager] Cannot start quest: '{questID}' is not registered.");
                return false;
            }

            questStates[questID] = QuestState.InProgress;
            questProgress[questID] = 0;

            QuestSO quest = registeredQuests[questID];
            Debug.Log($"[QuestManager] Quest accepted: {quest.QuestTitle} ({questID})");

            // If it's a scrap collection quest, initialize with existing scrap count in inventory
            if (questID.IndexOf("scrap", StringComparison.OrdinalIgnoreCase) >= 0 && InventoryManager.Instance != null)
            {
                questProgress[questID] = Mathf.Min(quest.RequiredAmount, InventoryManager.Instance.ScrapMetalCount);
            }

            OnQuestStateUpdated?.Invoke(questID, QuestState.InProgress);
            OnQuestProgressUpdated?.Invoke(questID, questProgress[questID], quest.RequiredAmount);
            PlayerHUD.Instance?.UpdateQuestSummaryText();

            return true;
        }

        /// <summary>
        /// Sets exact progress for an active quest and notifies HUD.
        /// </summary>
        public void SetQuestProgress(string questID, int amount)
        {
            if (string.IsNullOrEmpty(questID) || !registeredQuests.TryGetValue(questID, out QuestSO quest)) return;
            if (GetQuestState(questID) != QuestState.InProgress) return;

            questProgress[questID] = Mathf.Clamp(amount, 0, quest.RequiredAmount);
            Debug.Log($"[QuestManager] Quest '{quest.QuestTitle}' progress updated: {questProgress[questID]}/{quest.RequiredAmount}");

            OnQuestProgressUpdated?.Invoke(questID, questProgress[questID], quest.RequiredAmount);
            PlayerHUD.Instance?.UpdateQuestSummaryText();
        }

        /// <summary>
        /// Increments objective progress for a quest (e.g., killed a rat, collected a swamp herb).
        /// </summary>
        public void AdvanceQuest(string questID, int amount = 1)
        {
            if (string.IsNullOrEmpty(questID) || amount <= 0) return;

            if (!registeredQuests.TryGetValue(questID, out QuestSO quest))
            {
                Debug.LogWarning($"[QuestManager] Unknown quest: '{questID}'.");
                return;
            }

            if (GetQuestState(questID) != QuestState.InProgress)
            {
                Debug.LogWarning($"[QuestManager] Cannot advance quest '{questID}': Quest is not currently InProgress.");
                return;
            }

            questProgress[questID] = Mathf.Min(quest.RequiredAmount, questProgress[questID] + amount);
            Debug.Log($"[QuestManager] Quest '{quest.QuestTitle}' progress: {questProgress[questID]}/{quest.RequiredAmount}");

            OnQuestProgressUpdated?.Invoke(questID, questProgress[questID], quest.RequiredAmount);
            PlayerHUD.Instance?.UpdateQuestSummaryText();
        }

        /// <summary>
        /// Completes a quest, validates objectives, and pays out gold and item rewards via InventoryManager.
        /// </summary>
        /// <param name="questID">The quest string ID.</param>
        /// <param name="grantedBonus">True if the player passed a D20 dialogue negotiation check for bonus gold.</param>
        public bool CompleteQuest(string questID, bool grantedBonus = false)
        {
            if (string.IsNullOrEmpty(questID) || !registeredQuests.TryGetValue(questID, out QuestSO quest))
            {
                Debug.LogWarning($"[QuestManager] Cannot complete: Quest '{questID}' not found.");
                return false;
            }

            if (questStates[questID] == QuestState.Completed)
            {
                Debug.Log($"[QuestManager] Quest '{quest.QuestTitle}' is already completed.");
                return false;
            }

            // Mark completed
            questStates[questID] = QuestState.Completed;
            questProgress[questID] = quest.RequiredAmount;

            // Compute reward payout
            int totalGold = quest.RewardGold + (grantedBonus ? quest.BonusRewardGold : 0);

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory != null)
            {
                inventory.AddGold(totalGold);
                if (quest.RewardItem != null)
                {
                    inventory.AddItem(quest.RewardItem, 1);
                }
            }

            Debug.Log($"[QuestManager] Completed quest '{quest.QuestTitle}'! Reward: {totalGold} Gold{(quest.RewardItem != null ? $" + {quest.RewardItem.ItemName}" : "")}.");

            OnQuestStateUpdated?.Invoke(questID, QuestState.Completed);
            OnQuestCompleted?.Invoke(quest, totalGold);
            PlayerHUD.Instance?.UpdateQuestSummaryText();

            return true;
        }

        private void HandleScrapMetalChanged(int newScrap)
        {
            // Automatically update any active scrap metal quests
            foreach (var kvp in registeredQuests)
            {
                string qid = kvp.Key;
                if (qid.IndexOf("scrap", StringComparison.OrdinalIgnoreCase) >= 0 && GetQuestState(qid) == QuestState.InProgress)
                {
                    SetQuestProgress(qid, newScrap);
                }
            }
        }

        #endregion

        #region Queries

        /// <summary>
        /// Returns the QuestSO definition for a given quest ID.
        /// </summary>
        public QuestSO GetQuest(string questID)
        {
            if (string.IsNullOrEmpty(questID)) return null;
            return registeredQuests.TryGetValue(questID, out QuestSO quest) ? quest : null;
        }

        /// <summary>
        /// Returns the first currently active (InProgress) quest.
        /// </summary>
        public QuestSO GetActiveQuest()
        {
            foreach (var kvp in questStates)
            {
                if (kvp.Value == QuestState.InProgress && registeredQuests.TryGetValue(kvp.Key, out QuestSO q))
                {
                    return q;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the current QuestState (NotStarted, InProgress, Completed).
        /// </summary>
        public QuestState GetQuestState(string questID)
        {
            return questStates.TryGetValue(questID, out QuestState state) ? state : QuestState.NotStarted;
        }

        /// <summary>
        /// Returns the current progress count of an active quest.
        /// </summary>
        public int GetQuestProgress(string questID)
        {
            return questProgress.TryGetValue(questID, out int count) ? count : 0;
        }

        /// <summary>
        /// Checks whether a quest has finished.
        /// </summary>
        public bool IsQuestCompleted(string questID)
        {
            return GetQuestState(questID) == QuestState.Completed;
        }

        #endregion
    }
}
