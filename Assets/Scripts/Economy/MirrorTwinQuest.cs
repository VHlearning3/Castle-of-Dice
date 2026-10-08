using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Economy
{
    /// <summary>
    /// The secret boss quest (Vili, 2026-10-08). The blonde elf in the village tavern warns of her evil twin on top
    /// of the magic tower; that line is the only hint. Nothing shows in the quest tracker or journal until the twin
    /// who steps out of the Tower's great mirror is beaten: then the quest appears, already completed.
    /// </summary>
    public static class MirrorTwinQuest
    {
        public const string QuestId = "secret_mirror_twin";
        public const string Title = "The Twin in the Mirror";

        /// <summary>Set when the tavern elf has given her warning (only changes the twin's greeting).</summary>
        public const string WarnedFlag = "MirrorTwinWarned";

        /// <summary>Set when the twin is beaten; the mirror stays quiet after that.</summary>
        public const string DefeatedFlag = "MirrorTwinDefeated";

        public const int RewardGold = 150;

        /// <summary>The tavern elf's warning, word for word.</summary>
        public const string WarningLine = "Beware my evil twin on top of the magic tower, traveler.";

        /// <summary>Whether the twin has been beaten in this adventure.</summary>
        public static bool IsDefeated => StoryFlags.Has(DefeatedFlag);

        /// <summary>Builds the runtime quest (no asset), registered by the QuestManager like the hero quests.</summary>
        public static QuestSO Create()
        {
            QuestSO quest = ScriptableObject.CreateInstance<QuestSO>();
            quest.name = "Quest_" + QuestId + " (Runtime)";
            quest.hideFlags = HideFlags.DontSave;
            quest.Initialize(QuestId, Title,
                "The blonde elf in the tavern warned of her evil twin on top of the magic tower. The twin stepped out of the great mirror in the Treasure Tower, trident in hand, and you beat her.",
                QuestState.NotStarted, 1, RewardGold, 0, null);
            quest.ConfigureObjective(QuestObjectiveType.Manual, "Defeat the twin in the mirror", "Treasure Tower", "");
            return quest;
        }

        /// <summary>
        /// Records the victory and shows the quest for the first time, completed (pays the gold once).
        /// </summary>
        public static void Complete()
        {
            StoryFlags.Set(DefeatedFlag);
            QuestManager quests = QuestManager.Instance;
            if (quests == null || quests.GetQuest(QuestId) == null) return;
            if (quests.GetQuestState(QuestId) == QuestState.Completed) return;
            if (quests.GetQuestState(QuestId) == QuestState.NotStarted) quests.StartQuest(QuestId);
            quests.SetQuestProgress(QuestId, 1);
            quests.CompleteQuest(QuestId);
        }
    }
}
