using System;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.Economy
{
    /// <summary>
    /// ScriptableObject defining a village or dungeon quest in Castle of the D20.
    /// Manages objective targets, gold rewards, bonus rewards from D20 negotiation, and reward items.
    /// </summary>
    [CreateAssetMenu(fileName = "NewQuest", menuName = "CastleOfDice/Quest", order = 25)]
    public class QuestSO : ScriptableObject
    {
        #region Serialized Fields

        [Header("Quest Identity")]
        [Tooltip("Unique programmatic identifier (e.g., 'CellarRats', 'LostSignetRing', 'SwampHerbs').")]
        [SerializeField] private string questID = "CellarRats";

        [Tooltip("User-facing quest title shown in quest tracker HUD and journal.")]
        [SerializeField] private string questTitle = "Cellar Infestation";

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

        #endregion
    }
}
