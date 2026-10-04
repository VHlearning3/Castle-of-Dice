using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Economy
{
    /// <summary>
    /// The main story quest "Break the Castle's Curse" (critical review C2). It is active from the start of
    /// every adventure and tells the player where to go next: the Cursed Commander, Shadow Mage Malakor, then
    /// the Petrified King. Its steps follow the campaign's defeated bosses, so it can never disagree with them.
    /// </summary>
    public static class MainQuest
    {
        /// <summary>Quest ID used in saves and the tracker.</summary>
        public const string QuestId = "quest_main_curse";

        /// <summary>Number of steps (one per boss).</summary>
        public const int StepCount = 3;

        private static readonly string[] StepObjectives =
        {
            "Defeat the Cursed Commander at the Castle Courtyard",
            "Defeat Shadow Mage Malakor in the Library",
            "Face the Petrified King in the Throne Room"
        };

        private static readonly string[] StepLocations =
        {
            "Castle Courtyard, through the Forest Path",
            "The Library, east wing of the Castle Hall",
            "Throne Room, behind the Castle Hall's great doors"
        };

        /// <summary>Builds the runtime quest definition (no asset needed).</summary>
        public static QuestSO Create()
        {
            QuestSO quest = ScriptableObject.CreateInstance<QuestSO>();
            quest.name = "Quest_BreakTheCurse (Runtime)";
            quest.hideFlags = HideFlags.DontSave;
            quest.Initialize(
                id: QuestId,
                title: "Break the Castle's Curse",
                desc: "The castle above Oakhaven lies under a curse of stone. Defeat its three guardians to break it.",
                state: QuestState.InProgress,
                reqAmount: StepCount,
                gold: 0,
                bonusGold: 0,
                reward: null);
            UpdateObjective(quest, 0);
            return quest;
        }

        /// <summary>How many of the three bosses have fallen, in story order.</summary>
        public static int CountStepsDone(GameManager gm)
        {
            if (gm == null) return 0;
            int steps = 0;
            if (gm.IsCommanderDefeated) steps++;
            if (gm.IsMalakorDefeated) steps++;
            if (gm.IsGargoyleKingDefeated) steps++;
            return steps;
        }

        /// <summary>Objective line for a step (0..2), or the closing line once all are done.</summary>
        public static string GetObjective(int stepsDone)
        {
            if (stepsDone >= StepCount) return "The curse is broken. The castle wakes.";
            return StepObjectives[Mathf.Clamp(stepsDone, 0, StepCount - 1)];
        }

        /// <summary>Rewrites the quest's objective and location for the current step.</summary>
        public static void UpdateObjective(QuestSO quest, int stepsDone)
        {
            if (quest == null) return;
            string location = stepsDone >= StepCount ? "The Throne Room" : StepLocations[Mathf.Clamp(stepsDone, 0, StepCount - 1)];
            quest.ConfigureObjective(QuestObjectiveType.Manual, GetObjective(stepsDone), location, "");
        }
    }
}
