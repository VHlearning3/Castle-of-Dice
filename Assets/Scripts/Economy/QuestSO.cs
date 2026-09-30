using System;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.Economy
{
    /// <summary>
    /// How a quest's objective counter moves.
    /// </summary>
    public enum QuestObjectiveType
    {
        /// <summary>Progress only changes through explicit AdvanceQuest / SetQuestProgress calls.</summary>
        Manual,

        /// <summary>Counts defeated enemies whose name contains TargetEnemyName (e.g. "Cellar Rat").</summary>
        DefeatEnemies,

        /// <summary>Counts how many ObjectiveItem the player carries (e.g. the signet ring, swamp herbs).</summary>
        CollectItem,

        /// <summary>Counts the player's scrap metal.</summary>
        ScrapMetal
    }

    /// <summary>
    /// ScriptableObject defining a village or dungeon quest in Castle of the D20.
    /// Manages objective targets, gold rewards, bonus rewards from D20 negotiation, and reward items.
    /// </summary>
    [CreateAssetMenu(fileName = "NewQuest", menuName = "CastleOfDice/Quest", order = 25)]
    public class QuestSO : ScriptableObject
    {
        #region Serialized Fields

        [Header("Quest Identity")]
        [Tooltip("Unique programmatic identifier (e.g., 'quest_cellar_pests', 'quest_swamp_herbs').")]
        [SerializeField] private string questID = "quest_cellar_pests";

        [Tooltip("User-facing quest title shown in quest tracker HUD and journal.")]
        [SerializeField] private string questTitle = "Cellar Pests";

        [Tooltip("Narrative description outlining context, objective, and location.")]
        [TextArea(2, 5)]
        [SerializeField] private string description = "Clear the giant cellar rats beneath the tavern.";

        [Header("Progression Requirements")]
        [Tooltip("Initial state of the quest prior to player acceptance.")]
        [SerializeField] private QuestState defaultState = QuestState.NotStarted;

        [Tooltip("Target counter required to fulfill the quest (e.g., 3 cellar rats, 3 swamp herbs, 1 signet ring).")]
        [Min(1)]
        [SerializeField] private int requiredAmount = 3;

        [Header("Rewards")]
        [Tooltip("Base gold reward received upon standard completion.")]
        [Min(0)]
        [SerializeField] private int rewardGold = 30;

        [Tooltip("Bonus gold granted if the player succeeded in a D20 negotiation dialogue check (e.g. DC 13 Barnaby).")]
        [Min(0)]
        [SerializeField] private int bonusRewardGold = 15;

        [Tooltip("Optional equipment, potion, or rune stone rewarded upon quest completion.")]
        [SerializeField] private ItemSO rewardItem;

        [Tooltip("How many of the reward item are given.")]
        [Min(1)]
        [SerializeField] private int rewardItemAmount = 1;

        [Tooltip("Optional item given INSTEAD of the reward item when the bonus was negotiated (e.g. Mirabel's Greater Health Potion).")]
        [SerializeField] private ItemSO bonusRewardItem;

        [Header("Objective")]
        [Tooltip("How the objective counter moves.")]
        [SerializeField] private QuestObjectiveType objectiveType = QuestObjectiveType.Manual;

        [Tooltip("DefeatEnemies: part of the enemy name that counts (e.g. 'Cellar Rat').")]
        [SerializeField] private string targetEnemyName = "";

        [Tooltip("CollectItem: the item the player has to bring back.")]
        [SerializeField] private ItemSO objectiveItem;

        [Tooltip("Short objective line for the HUD (e.g. 'Slay the giant cellar rats').")]
        [SerializeField] private string objectiveSummary = "";

        [Tooltip("Where the objective is found (e.g. 'Wine cellar under the tavern').")]
        [SerializeField] private string objectiveLocation = "";

        [Header("Quest Giver")]
        [Tooltip("Name of the villager who gives and takes back this quest (matched against VillageNPC names, e.g. 'Barnaby').")]
        [SerializeField] private string questGiverName = "";

        [Tooltip("What the giver says while the quest is still unfinished.")]
        [TextArea(2, 4)]
        [SerializeField] private string inProgressText = "";

        [Tooltip("What the giver says when the player comes back with the objective done.")]
        [TextArea(2, 4)]
        [SerializeField] private string readyText = "";

        [Tooltip("The player's hand-in choice.")]
        [SerializeField] private string turnInOptionText = "";

        [Tooltip("The giver's thanks right after the hand-in.")]
        [TextArea(2, 4)]
        [SerializeField] private string thanksText = "";

        [Tooltip("What the giver says on later visits once the quest is done.")]
        [TextArea(2, 4)]
        [SerializeField] private string completedText = "";

        #endregion

        #region Public Properties

        /// <summary>Unique string ID of this quest.</summary>
        public string QuestID => questID;

        /// <summary>Quest title for UI display.</summary>
        public string QuestTitle => questTitle;

        /// <summary>Quest narrative description.</summary>
        public string Description => description;

        /// <summary>Starting progression state.</summary>
        public QuestState DefaultState => defaultState;

        /// <summary>Target quantity to complete objectives.</summary>
        public int RequiredAmount => requiredAmount;

        /// <summary>Base gold reward.</summary>
        public int RewardGold => rewardGold;

        /// <summary>Additional gold awarded for successful D20 negotiation.</summary>
        public int BonusRewardGold => bonusRewardGold;

        /// <summary>Item reward rewarded upon completion.</summary>
        public ItemSO RewardItem => rewardItem;

        /// <summary>How many of the reward item are given.</summary>
        public int RewardItemAmount => Mathf.Max(1, rewardItemAmount);

        /// <summary>Item given instead of RewardItem when the bonus was negotiated (null = RewardItem either way).</summary>
        public ItemSO BonusRewardItem => bonusRewardItem;

        /// <summary>How the objective counter moves.</summary>
        public QuestObjectiveType ObjectiveType => objectiveType;

        /// <summary>DefeatEnemies: part of the enemy name that counts.</summary>
        public string TargetEnemyName => targetEnemyName;

        /// <summary>CollectItem: the item the player has to bring back.</summary>
        public ItemSO ObjectiveItem => objectiveItem;

        /// <summary>Short objective line for the HUD; falls back to the description.</summary>
        public string ObjectiveSummary => string.IsNullOrEmpty(objectiveSummary) ? description : objectiveSummary;

        /// <summary>Where the objective is found (may be empty).</summary>
        public string ObjectiveLocation => objectiveLocation;

        /// <summary>Name of the villager who gives and takes back this quest (may be empty).</summary>
        public string QuestGiverName => questGiverName;

        /// <summary>Giver's line while the quest is unfinished.</summary>
        public string InProgressText => inProgressText;

        /// <summary>Giver's line when the objective is done.</summary>
        public string ReadyText => readyText;

        /// <summary>The player's hand-in choice.</summary>
        public string TurnInOptionText => turnInOptionText;

        /// <summary>Giver's thanks right after the hand-in.</summary>
        public string ThanksText => thanksText;

        /// <summary>Giver's line on later visits once the quest is done.</summary>
        public string CompletedText => completedText;

        /// <summary>
        /// Whether an enemy with this name counts toward a DefeatEnemies objective.
        /// </summary>
        public bool CountsEnemy(string enemyName)
        {
            return objectiveType == QuestObjectiveType.DefeatEnemies
                && !string.IsNullOrEmpty(targetEnemyName)
                && !string.IsNullOrEmpty(enemyName)
                && enemyName.IndexOf(targetEnemyName, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the quest parameters programmatically (used by editor generators or unit tests).
        /// </summary>
        public void Initialize(
            string id,
            string title,
            string desc,
            QuestState state,
            int reqAmount,
            int gold,
            int bonusGold,
            ItemSO reward)
        {
            questID = id;
            questTitle = title;
            description = desc;
            defaultState = state;
            requiredAmount = reqAmount;
            rewardGold = gold;
            bonusRewardGold = bonusGold;
            rewardItem = reward;
        }

        /// <summary>
        /// Sets how the objective is tracked and who hands the quest out (used by editor generators or unit tests).
        /// </summary>
        public void ConfigureObjective(
            QuestObjectiveType type,
            string summary,
            string location,
            string giverName,
            string enemyName = "",
            ItemSO item = null)
        {
            objectiveType = type;
            objectiveSummary = summary;
            objectiveLocation = location;
            questGiverName = giverName;
            targetEnemyName = enemyName ?? "";
            objectiveItem = item;
        }

        /// <summary>
        /// Sets the reward details beyond gold (used by editor generators or unit tests).
        /// </summary>
        public void ConfigureRewards(int itemAmount, ItemSO bonusItem)
        {
            rewardItemAmount = Mathf.Max(1, itemAmount);
            bonusRewardItem = bonusItem;
        }

        /// <summary>
        /// Sets the giver's lines for the in-progress, ready, thanks and completed visits.
        /// </summary>
        public void ConfigureDialogue(string inProgress, string ready, string turnInOption, string thanks, string completed)
        {
            inProgressText = inProgress;
            readyText = ready;
            turnInOptionText = turnInOption;
            thanksText = thanks;
            completedText = completed;
        }

        #endregion
    }
}
