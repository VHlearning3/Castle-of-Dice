using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.Economy
{
    /// <summary>
    /// The secret boss quest (Vili, 2026-10-08). The blonde elf in the village tavern warns of her evil twin on top
    /// of the magic tower; that line is the only hint. Nothing shows in the quest tracker or journal until the twin
    /// who steps out of the Tower's great mirror is beaten. She leaves her Black Trident behind, and the quest
    /// appears: take the trident back to the elf. Handing it over plays the secret ending ("You cleansed the
    /// tower"), which also wins the game, so the Gargoyle King need not be fought.
    /// </summary>
    public static class MirrorTwinQuest
    {
        public const string QuestId = "secret_mirror_twin";
        public const string Title = "The Twin in the Mirror";

        /// <summary>Set when the tavern elf has given her warning (only changes the twin's greeting).</summary>
        public const string WarnedFlag = "MirrorTwinWarned";

        /// <summary>Set when the twin is beaten; the mirror stays quiet after that.</summary>
        public const string DefeatedFlag = "MirrorTwinDefeated";

        /// <summary>Set when the hero gives the elf the trident: the secret ending.</summary>
        public const string TridentReturnedFlag = "MirrorTwinTridentReturned";

        /// <summary>Story flag of the secret ending, kept with the finished adventure.</summary>
        public const string SecretEndingFlag = "SecretEnding_TowerCleansed";

        /// <summary>The twin's trident, a quest item (Resources/Items/Item_BlackTrident).</summary>
        public const string TridentItemId = "quest_black_trident";

        /// <summary>No gold: the trident is the reward (Vili 2026-10-08, was 150 gold).</summary>
        public const int RewardGold = 0;

        /// <summary>The tavern elf's warning, word for word.</summary>
        public const string WarningLine = "Beware my evil twin on top of the magic tower, traveler.";

        /// <summary>The quest's objective once the twin is beaten.</summary>
        public const string ReturnObjective = "Return the Black Trident to the blonde elf";

        /// <summary>Whether the twin has been beaten in this adventure.</summary>
        public static bool IsDefeated => StoryFlags.Has(DefeatedFlag);

        /// <summary>Whether the trident is back with the elf (the secret ending has played).</summary>
        public static bool IsTridentReturned => StoryFlags.Has(TridentReturnedFlag);

        /// <summary>The trident's item asset, or null when it cannot be found.</summary>
        public static ItemSO FindTrident()
        {
            InventoryManager inventory = InventoryManager.Instance;
            ItemSO item = inventory != null ? inventory.FindItemByID(TridentItemId) : null;
            return item != null ? item : Resources.Load<ItemSO>("Items/Item_BlackTrident");
        }

        /// <summary>Whether the hero carries the twin's trident.</summary>
        public static bool HasTrident
        {
            get
            {
                InventoryManager inventory = InventoryManager.Instance;
                ItemSO trident = FindTrident();
                return inventory != null && trident != null && inventory.HasItem(trident, 1);
            }
        }

        /// <summary>Builds the runtime quest (no asset), registered by the QuestManager like the hero quests.</summary>
        public static QuestSO Create()
        {
            QuestSO quest = ScriptableObject.CreateInstance<QuestSO>();
            quest.name = "Quest_" + QuestId + " (Runtime)";
            quest.hideFlags = HideFlags.DontSave;
            quest.Initialize(QuestId, Title,
                "The blonde elf in the tavern warned of her evil twin on top of the magic tower. The twin stepped out of the great mirror in the Treasure Tower, trident in hand, and you beat her. She left her Black Trident behind: take it to her sister in the Foaming Tankard.",
                QuestState.NotStarted, 1, RewardGold, 0, null);
            quest.ConfigureObjective(QuestObjectiveType.Manual, ReturnObjective, "The Foaming Tankard", "");
            return quest;
        }

        /// <summary>
        /// Records the victory: the twin leaves the Black Trident, and the quest shows for the first time, asking
        /// the hero to take it back to the tavern.
        /// </summary>
        public static void RecordVictory()
        {
            StoryFlags.Set(DefeatedFlag);

            InventoryManager inventory = InventoryManager.Instance;
            ItemSO trident = FindTrident();
            if (inventory != null && trident != null && !inventory.HasItem(trident, 1)) inventory.AddItem(trident, 1);

            QuestManager quests = QuestManager.Instance;
            if (quests == null || quests.GetQuest(QuestId) == null) return;
            if (quests.GetQuestState(QuestId) == QuestState.NotStarted) quests.StartQuest(QuestId);
        }

        /// <summary>The hero gives the elf the trident: the quest is done and the secret ending's flags are set.</summary>
        public static void ReturnTrident()
        {
            InventoryManager inventory = InventoryManager.Instance;
            ItemSO trident = FindTrident();
            if (inventory != null && trident != null) inventory.RemoveItem(trident, 1);

            StoryFlags.Set(TridentReturnedFlag);
            StoryFlags.Set(SecretEndingFlag);

            QuestManager quests = QuestManager.Instance;
            if (quests == null || quests.GetQuest(QuestId) == null) return;
            if (quests.GetQuestState(QuestId) == QuestState.Completed) return;
            if (quests.GetQuestState(QuestId) == QuestState.NotStarted) quests.StartQuest(QuestId);
            quests.SetQuestProgress(QuestId, 1);
            quests.CompleteQuest(QuestId);
        }
    }
}
