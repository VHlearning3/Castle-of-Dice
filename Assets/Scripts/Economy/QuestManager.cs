using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Data;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.Economy
{
    /// <summary>
    /// Singleton manager tracking quest life cycles, objective progress, and reward distribution.
    /// Handles the village side quests: Cellar Pests (Barnaby), The Lost Signet Ring (Othelia),
    /// Herbs for the Healer (Mirabel) and Scrap for the Forge (Baldur).
    /// Objectives move on their own: enemy kills, items carried and scrap owned are counted from game events.
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

        // Quests whose giver agreed to a better reward through a D20 dialogue check
        private readonly HashSet<string> negotiatedBonuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Events

        /// <summary>Fired when a quest state transitions: (questID, newState). Also fired when a save is restored.</summary>
        public static event Action<string, QuestState> OnQuestStateUpdated;

        /// <summary>Fired when objective counter changes: (questID, currentAmount, requiredAmount).</summary>
        public static event Action<string, int, int> OnQuestProgressUpdated;

        /// <summary>Fired when a quest is finalized and rewards are granted: (quest, totalGoldAwarded).</summary>
        public static event Action<QuestSO, int> OnQuestCompleted;

        /// <summary>Fired when the player accepts a quest during play (not on save restore).</summary>
        public static event Action<QuestSO> OnQuestAccepted;

        /// <summary>Fired when an accepted quest's counter goes up during play: (quest, current, required).</summary>
        public static event Action<QuestSO, int, int> OnQuestObjectiveAdvanced;

        /// <summary>Fired when an accepted quest's objective becomes complete and can be handed in.</summary>
        public static event Action<QuestSO> OnQuestReadyToTurnIn;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                ManagerDuplicates.Discard(this);
                return;
            }

            instance = this;

            InitializeQuests();
        }

        private void OnEnable()
        {
            InventoryManager.OnScrapMetalChanged += HandleScrapMetalChanged;
            InventoryManager.OnInventoryChanged += HandleInventoryChanged;
            CombatUnit.OnAnyUnitDied += HandleUnitDied;
            GameManager.OnBossDefeated += HandleBossDefeated;
        }

        private void OnDisable()
        {
            InventoryManager.OnScrapMetalChanged -= HandleScrapMetalChanged;
            InventoryManager.OnInventoryChanged -= HandleInventoryChanged;
            CombatUnit.OnAnyUnitDied -= HandleUnitDied;
            GameManager.OnBossDefeated -= HandleBossDefeated;
        }

        private void HandleBossDefeated(string bossId)
        {
            SyncMainQuest();
        }

        /// <summary>
        /// Moves "Break the Castle's Curse" to the step the campaign has reached (Commander, Malakor, King)
        /// and completes it when the King falls. Safe to call any time.
        /// </summary>
        public void SyncMainQuest()
        {
            if (!registeredQuests.TryGetValue(MainQuest.QuestId, out QuestSO quest)) return;

            GameManager gm = GameManager.Instance;
            int step = MainQuest.CountStepsDone(gm);
            MainQuest.UpdateObjective(quest, step);

            QuestState state = GetQuestState(MainQuest.QuestId);
            if (state == QuestState.NotStarted) state = QuestState.InProgress;
            int previous = GetQuestProgress(MainQuest.QuestId);
            questProgress[MainQuest.QuestId] = step;

            QuestState newState = step >= MainQuest.StepCount ? QuestState.Completed : QuestState.InProgress;
            bool changed = newState != state || previous != step;
            questStates[MainQuest.QuestId] = newState;

            if (!changed) return;
            OnQuestProgressUpdated?.Invoke(MainQuest.QuestId, step, MainQuest.StepCount);
            OnQuestStateUpdated?.Invoke(MainQuest.QuestId, newState);
            if (newState == QuestState.Completed && state != QuestState.Completed)
            {
                OnQuestCompleted?.Invoke(quest, 0);
            }
            PlayerHUD.Instance?.UpdateQuestSummaryText();
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

            // The main story quest is built in code, so no scene has to list it
            RegisterQuest(MainQuest.Create());
            SyncMainQuest();

            if (registeredQuests.Count == 1)
            {
                // Player builds can only see quests serialized into questDatabase; flag the misconfiguration
                // instead of silently papering over it with an editor-only AssetDatabase scan.
                Debug.LogError("[QuestManager] questDatabase is empty. Assign the QuestSO assets (ZoneSceneBuilder does this automatically).");
            }
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
            }
            if (!questProgress.ContainsKey(quest.QuestID))
            {
                questProgress[quest.QuestID] = 0;
            }
        }

        #endregion

        #region Quest Operations

        /// <summary>
        /// Accepts a quest. A quest already accepted or finished is left as it is (returns false), so talking to
        /// the giver again never resets progress or hands out the reward twice. Items already carried, scrap
        /// already owned and enemies already slain count straight away.
        /// </summary>
        public bool StartQuest(string questID)
        {
            if (string.IsNullOrEmpty(questID)) return false;

            if (!registeredQuests.TryGetValue(questID, out QuestSO quest))
            {
                Debug.LogWarning($"[QuestManager] Cannot start quest: '{questID}' is not registered.");
                return false;
            }

            QuestState current = GetQuestState(questID);
            if (current == QuestState.InProgress || current == QuestState.Completed)
            {
                Debug.Log($"[QuestManager] Quest '{quest.QuestTitle}' is already {current}; not restarting it.");
                return false;
            }

            questStates[questID] = QuestState.InProgress;
            int progress = Mathf.Clamp(ComputeTrackedProgress(quest, GetQuestProgress(questID)), 0, quest.RequiredAmount);
            questProgress[questID] = progress;

            Debug.Log($"[QuestManager] Quest accepted: {quest.QuestTitle} ({questID}) at {progress}/{quest.RequiredAmount}");

            OnQuestStateUpdated?.Invoke(questID, QuestState.InProgress);
            OnQuestProgressUpdated?.Invoke(questID, progress, quest.RequiredAmount);
            OnQuestAccepted?.Invoke(quest);
            if (progress >= quest.RequiredAmount)
            {
                OnQuestReadyToTurnIn?.Invoke(quest);
            }
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

            ApplyProgress(quest, amount);
        }

        /// <summary>
        /// Increments objective progress for an active quest.
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

            ApplyProgress(quest, GetQuestProgress(questID) + amount);
        }

        /// <summary>
        /// Counts a defeated enemy toward every DefeatEnemies quest it matches. Kills made before the quest was
        /// accepted are remembered too, so clearing the cellar first still counts once Barnaby asks.
        /// </summary>
        public void RecordEnemyDefeated(string enemyName)
        {
            if (string.IsNullOrEmpty(enemyName)) return;

            foreach (KeyValuePair<string, QuestSO> entry in registeredQuests)
            {
                QuestSO quest = entry.Value;
                if (quest == null || !quest.CountsEnemy(enemyName)) continue;

                QuestState state = GetQuestState(entry.Key);
                if (state == QuestState.Completed) continue;

                int next = GetQuestProgress(entry.Key) + 1;
                if (state == QuestState.InProgress)
                {
                    ApplyProgress(quest, next);
                }
                else
                {
                    questProgress[entry.Key] = Mathf.Min(quest.RequiredAmount, next);
                }
            }
        }

        /// <summary>
        /// Re-counts carried items and scrap for every accepted quest (called whenever the inventory changes).
        /// </summary>
        public void RefreshTrackedObjectives()
        {
            foreach (KeyValuePair<string, QuestSO> entry in registeredQuests)
            {
                QuestSO quest = entry.Value;
                if (quest == null || GetQuestState(entry.Key) != QuestState.InProgress) continue;
                if (quest.ObjectiveType != QuestObjectiveType.CollectItem && quest.ObjectiveType != QuestObjectiveType.ScrapMetal) continue;

                ApplyProgress(quest, ComputeTrackedProgress(quest, GetQuestProgress(entry.Key)));
            }
        }

        /// <summary>
        /// Whether an accepted quest's objective is done and the giver can take it back.
        /// </summary>
        public bool IsReadyToTurnIn(string questID)
        {
            if (string.IsNullOrEmpty(questID) || !registeredQuests.TryGetValue(questID, out QuestSO quest)) return false;
            return GetQuestState(questID) == QuestState.InProgress && GetQuestProgress(questID) >= quest.RequiredAmount;
        }

        /// <summary>
        /// Hands a finished quest in: takes the quest items (ring, herbs, scrap), then pays the gold and item
        /// rewards via InventoryManager. Fails while the objective is unfinished.
        /// </summary>
        /// <param name="questID">The quest string ID.</param>
        /// <param name="grantedBonus">Forces the bonus reward; otherwise the negotiated bonus recorded for this quest is used.</param>
        public bool CompleteQuest(string questID, bool grantedBonus = false)
        {
            if (string.IsNullOrEmpty(questID) || !registeredQuests.TryGetValue(questID, out QuestSO quest))
            {
                Debug.LogWarning($"[QuestManager] Cannot complete: Quest '{questID}' not found.");
                return false;
            }

            QuestState state = GetQuestState(questID);
            if (state == QuestState.Completed)
            {
                Debug.Log($"[QuestManager] Quest '{quest.QuestTitle}' is already completed.");
                return false;
            }

            if (state != QuestState.InProgress)
            {
                Debug.Log($"[QuestManager] Quest '{quest.QuestTitle}' has not been accepted yet; cannot complete it.");
                return false;
            }

            // Carried objectives are re-counted so a sold ring or spent scrap can't be handed in
            int progress = ComputeTrackedProgress(quest, GetQuestProgress(questID));
            if (progress < quest.RequiredAmount)
            {
                ApplyProgress(quest, progress);
                Debug.Log($"[QuestManager] Quest '{quest.QuestTitle}' is not finished yet ({progress}/{quest.RequiredAmount}).");
                return false;
            }

            // Mark completed before taking the items so the inventory events don't lower the counter
            questStates[questID] = QuestState.Completed;
            questProgress[questID] = quest.RequiredAmount;

            bool bonus = grantedBonus || HasNegotiatedBonus(questID);
            int totalGold = quest.RewardGold + (bonus ? quest.BonusRewardGold : 0);
            ItemSO rewardItem = bonus && quest.BonusRewardItem != null ? quest.BonusRewardItem : quest.RewardItem;

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory != null)
            {
                TakeObjectiveItems(quest, inventory);
                inventory.AddGold(totalGold);
                if (rewardItem != null)
                {
                    inventory.AddItem(rewardItem, quest.RewardItemAmount);
                }
            }

            Debug.Log($"[QuestManager] Completed quest '{quest.QuestTitle}'! Reward: {totalGold} Gold{(rewardItem != null ? $" + {quest.RewardItemAmount}x {rewardItem.ItemName}" : "")}.");

            OnQuestStateUpdated?.Invoke(questID, QuestState.Completed);
            OnQuestProgressUpdated?.Invoke(questID, quest.RequiredAmount, quest.RequiredAmount);
            OnQuestCompleted?.Invoke(quest, totalGold);
            PlayerHUD.Instance?.UpdateQuestSummaryText();

            return true;
        }

        /// <summary>
        /// The item the giver hands over for this quest, taking a negotiated bonus into account.
        /// </summary>
        public ItemSO GetRewardItem(QuestSO quest)
        {
            if (quest == null) return null;
            return HasNegotiatedBonus(quest.QuestID) && quest.BonusRewardItem != null ? quest.BonusRewardItem : quest.RewardItem;
        }

        /// <summary>
        /// The gold the giver pays for this quest, taking a negotiated bonus into account.
        /// </summary>
        public int GetRewardGold(QuestSO quest)
        {
            if (quest == null) return 0;
            return quest.RewardGold + (HasNegotiatedBonus(quest.QuestID) ? quest.BonusRewardGold : 0);
        }

        private static void TakeObjectiveItems(QuestSO quest, InventoryManager inventory)
        {
            switch (quest.ObjectiveType)
            {
                case QuestObjectiveType.CollectItem:
                    if (quest.ObjectiveItem != null)
                    {
                        inventory.RemoveItem(quest.ObjectiveItem, Mathf.Min(quest.RequiredAmount, inventory.GetItemCount(quest.ObjectiveItem)));
                    }
                    break;

                case QuestObjectiveType.ScrapMetal:
                    inventory.RemoveScrapMetal(Mathf.Min(quest.RequiredAmount, inventory.ScrapMetalCount));
                    break;
            }
        }

        /// <summary>
        /// Current counter for a quest: carried objectives are read from the inventory, the rest keep their count.
        /// </summary>
        private static int ComputeTrackedProgress(QuestSO quest, int storedProgress)
        {
            InventoryManager inventory = InventoryManager.Instance;
            switch (quest.ObjectiveType)
            {
                case QuestObjectiveType.CollectItem:
                    return inventory != null && quest.ObjectiveItem != null ? inventory.GetItemCount(quest.ObjectiveItem) : storedProgress;

                case QuestObjectiveType.ScrapMetal:
                    return inventory != null ? inventory.ScrapMetalCount : storedProgress;

                default:
                    return storedProgress;
            }
        }

        /// <summary>
        /// Sets an accepted quest's counter and raises the HUD / notification events for the change.
        /// </summary>
        private void ApplyProgress(QuestSO quest, int amount)
        {
            string questID = quest.QuestID;
            int previous = GetQuestProgress(questID);
            int next = Mathf.Clamp(amount, 0, quest.RequiredAmount);
            if (next == previous) return;

            questProgress[questID] = next;
            Debug.Log($"[QuestManager] Quest '{quest.QuestTitle}' progress: {next}/{quest.RequiredAmount}");

            OnQuestProgressUpdated?.Invoke(questID, next, quest.RequiredAmount);
            if (next > previous)
            {
                OnQuestObjectiveAdvanced?.Invoke(quest, next, quest.RequiredAmount);
                if (next >= quest.RequiredAmount)
                {
                    OnQuestReadyToTurnIn?.Invoke(quest);
                }
            }
            PlayerHUD.Instance?.UpdateQuestSummaryText();
        }

        private void HandleScrapMetalChanged(int newScrap)
        {
            RefreshTrackedObjectives();
        }

        private void HandleInventoryChanged()
        {
            RefreshTrackedObjectives();
        }

        private void HandleUnitDied(CombatUnit unit)
        {
            if (unit == null || unit is PlayerUnit) return;
            RecordEnemyDefeated(unit.UnitName);
        }

        #endregion

        #region Negotiated Bonus

        /// <summary>
        /// Records whether the player talked the giver into the bonus reward (saved with the game).
        /// </summary>
        public void SetNegotiatedBonus(string questID, bool bonus)
        {
            if (string.IsNullOrEmpty(questID)) return;
            if (bonus) negotiatedBonuses.Add(questID);
            else negotiatedBonuses.Remove(questID);
        }

        /// <summary>
        /// Whether the player talked the giver into the bonus reward.
        /// </summary>
        public bool HasNegotiatedBonus(string questID)
        {
            return !string.IsNullOrEmpty(questID) && negotiatedBonuses.Contains(questID);
        }

        #endregion

        #region Save Support

        /// <summary>
        /// Writes every quest that has left its NotStarted state (or has early kills recorded) as parallel lists for the save file.
        /// </summary>
        public void CaptureState(List<string> questIds, List<int> states, List<int> progress)
        {
            questIds.Clear();
            states.Clear();
            progress.Clear();
            foreach (KeyValuePair<string, QuestState> entry in questStates)
            {
                int count = GetQuestProgress(entry.Key);
                if (entry.Value == QuestState.NotStarted && count <= 0) continue;
                questIds.Add(entry.Key);
                states.Add((int)entry.Value);
                progress.Add(count);
            }
        }

        /// <summary>
        /// Writes the quests with a negotiated bonus for the save file.
        /// </summary>
        public void CaptureBonuses(List<string> questIds)
        {
            questIds.Clear();
            foreach (string id in negotiatedBonuses)
            {
                questIds.Add(id);
            }
        }

        /// <summary>
        /// Restores saved quest states and progress. Works before or after the quests are registered:
        /// RegisterQuest keeps an existing state instead of resetting it to the default.
        /// </summary>
        public void RestoreState(IReadOnlyList<string> questIds, IReadOnlyList<int> states, IReadOnlyList<int> progress)
        {
            if (questIds == null || states == null) return;

            int count = Mathf.Min(questIds.Count, states.Count);
            for (int i = 0; i < count; i++)
            {
                string id = questIds[i];
                if (string.IsNullOrEmpty(id)) continue;

                questStates[id] = (QuestState)states[i];
                questProgress[id] = progress != null && i < progress.Count ? progress[i] : 0;
                OnQuestStateUpdated?.Invoke(id, questStates[id]);
            }

            SyncMainQuest();
            PlayerHUD.Instance?.UpdateQuestSummaryText();
        }

        /// <summary>
        /// Restores the quests with a negotiated bonus from the save file (null = none).
        /// </summary>
        public void RestoreBonuses(IReadOnlyList<string> questIds)
        {
            negotiatedBonuses.Clear();
            if (questIds == null) return;
            for (int i = 0; i < questIds.Count; i++)
            {
                if (!string.IsNullOrEmpty(questIds[i])) negotiatedBonuses.Add(questIds[i]);
            }
        }

        /// <summary>
        /// Resets every registered quest to its default state (New Adventure).
        /// </summary>
        public void ResetAllQuests()
        {
            questStates.Clear();
            questProgress.Clear();
            negotiatedBonuses.Clear();
            foreach (KeyValuePair<string, QuestSO> entry in registeredQuests)
            {
                questStates[entry.Key] = entry.Value.DefaultState;
                questProgress[entry.Key] = 0;
                OnQuestStateUpdated?.Invoke(entry.Key, entry.Value.DefaultState);
            }

            SyncMainQuest();
            PlayerHUD.Instance?.UpdateQuestSummaryText();
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
        /// Returns the quest this villager gives, matched by QuestGiverName inside the NPC's name (null = none).
        /// </summary>
        public QuestSO FindQuestForGiver(string npcName)
        {
            if (string.IsNullOrEmpty(npcName)) return null;

            foreach (KeyValuePair<string, QuestSO> entry in registeredQuests)
            {
                QuestSO quest = entry.Value;
                if (quest != null && !string.IsNullOrEmpty(quest.QuestGiverName)
                    && npcName.IndexOf(quest.QuestGiverName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return quest;
                }
            }
            return null;
        }

        /// <summary>
        /// Scrap that accepted, unfinished scrap quests need for hand-in (the shop will not buy it).
        /// </summary>
        public int GetScrapReservedForQuests()
        {
            int reserved = 0;
            foreach (KeyValuePair<string, QuestSO> entry in registeredQuests)
            {
                QuestSO quest = entry.Value;
                if (quest == null || quest.ObjectiveType != QuestObjectiveType.ScrapMetal) continue;
                if (GetQuestState(quest.QuestID) != QuestState.InProgress) continue;
                reserved += quest.RequiredAmount;
            }
            return reserved;
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
            if (string.IsNullOrEmpty(questID)) return QuestState.NotStarted;
            return questStates.TryGetValue(questID, out QuestState state) ? state : QuestState.NotStarted;
        }

        /// <summary>
        /// Returns the current progress count of an active quest.
        /// </summary>
        public int GetQuestProgress(string questID)
        {
            if (string.IsNullOrEmpty(questID)) return 0;
            return questProgress.TryGetValue(questID, out int count) ? count : 0;
        }

        /// <summary>
        /// Checks whether a quest has finished.
        /// </summary>
        public bool IsQuestCompleted(string questID)
        {
            return GetQuestState(questID) == QuestState.Completed;
        }

        /// <summary>
        /// Returns all registered quest ScriptableObject definitions.
        /// </summary>
        public List<QuestSO> GetAllRegisteredQuests()
        {
            return new List<QuestSO>(registeredQuests.Values);
        }

        /// <summary>
        /// Returns all quests currently marked as InProgress.
        /// </summary>
        public List<QuestSO> GetAllActiveQuests()
        {
            List<QuestSO> list = new List<QuestSO>();
            foreach (var kvp in questStates)
            {
                if (kvp.Value == QuestState.InProgress && registeredQuests.TryGetValue(kvp.Key, out QuestSO q))
                {
                    if (!list.Contains(q)) list.Add(q);
                }
            }
            return list;
        }

        /// <summary>
        /// Returns all tracked quests that are either InProgress or Completed.
        /// </summary>
        public List<QuestSO> GetTrackedQuests()
        {
            List<QuestSO> list = new List<QuestSO>();
            foreach (var kvp in questStates)
            {
                if ((kvp.Value == QuestState.InProgress || kvp.Value == QuestState.Completed) && registeredQuests.TryGetValue(kvp.Key, out QuestSO q))
                {
                    if (!list.Contains(q)) list.Add(q);
                }
            }
            return list;
        }

        #endregion
    }
}
