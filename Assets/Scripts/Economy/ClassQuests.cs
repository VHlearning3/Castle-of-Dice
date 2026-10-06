using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Economy
{
    /// <summary>
    /// Each hero's own quest (Vili, 2026-10-06). The knight smashes the boarded-up crates in the Castle Hall,
    /// the mage puts the Witch's tomes back in order so the Library's great shelves open on a secret room, and
    /// the rogue picks the Forest Path gate and finds the smugglers' strongbox on the Hidden Trail. A village
    /// NPC hands each one out (Baldur, Othelia, Barnaby); the reward is a weapon upgrade (+1 damage) and a
    /// level, once per adventure, even when the hero finds it before being told about it.
    /// </summary>
    public static class ClassQuests
    {
        /// <summary>One hero's quest.</summary>
        public sealed class Definition
        {
            public CharacterClassType Class;
            public string QuestId;
            public string Title;
            public string Description;
            public string Objective;
            public string Location;
            public string GiverName;

            /// <summary>Story flag set once the reward has been given (saved with the adventure).</summary>
            public string RewardFlag => QuestId + "_rewarded";
        }

        public static readonly Definition Knight = new Definition
        {
            Class = CharacterClassType.Warrior,
            QuestId = "quest_class_warrior",
            Title = "The King's Old Armoury",
            Description = "Baldur says the King's finest blades lie in crates nailed shut in a corner of the Castle Hall. Only a knight's strength can break them.",
            Objective = "Smash the boarded-up crates in the Castle Hall",
            Location = "Castle Hall, north-east corner",
            GiverName = "Baldur"
        };

        public static readonly Definition Mage = new Definition
        {
            Class = CharacterClassType.Mage,
            QuestId = "quest_class_mage",
            Title = "The Witch's Catalogue",
            Description = "Put the Witch's five tomes back on their shelf in the right order and the Library's great bookshelves will open.",
            Objective = "Put the Witch's tomes back in order",
            Location = "The Library, by the north wall",
            GiverName = "Othelia"
        };

        public static readonly Definition Rogue = new Definition
        {
            Class = CharacterClassType.Rogue,
            QuestId = "quest_class_rogue",
            Title = "The Smugglers' Strongbox",
            Description = "Pick the lock of the Forest Path gate and find the strongbox the smugglers left on the Hidden Trail.",
            Objective = "Find the smugglers' strongbox",
            Location = "Hidden Trail, behind the Forest Path gate",
            GiverName = "Barnaby"
        };

        /// <summary>All three, knight first.</summary>
        public static readonly Definition[] All = { Knight, Mage, Rogue };

        /// <summary>The quest of a class.</summary>
        public static Definition For(CharacterClassType cls)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Class == cls) return All[i];
            }
            return null;
        }

        /// <summary>The quest of a hero (null without a class).</summary>
        public static Definition For(PlayerUnit hero)
        {
            return hero != null && hero.CharacterClass != null ? For(hero.CharacterClass.ClassType) : null;
        }

        /// <summary>Builds the runtime quest (no asset). The giver name is left empty so the NPC's own quest still resolves.</summary>
        public static QuestSO Create(Definition def)
        {
            QuestSO quest = ScriptableObject.CreateInstance<QuestSO>();
            quest.name = "Quest_" + def.QuestId + " (Runtime)";
            quest.hideFlags = HideFlags.DontSave;
            quest.Initialize(def.QuestId, def.Title, def.Description, QuestState.NotStarted, 1, 0, 0, null);
            quest.ConfigureObjective(QuestObjectiveType.Manual, def.Objective, def.Location, "");
            return quest;
        }

        /// <summary>Whether this hero's reward has already been given in this adventure.</summary>
        public static bool IsRewarded(Definition def) => def != null && StoryFlags.Has(def.RewardFlag);

        /// <summary>
        /// Gives <paramref name="hero"/> the reward of their own quest: +1 weapon damage and a level, and marks
        /// the quest done (accepting it first if nobody told them about it). Returns false when the hero's
        /// class does not match <paramref name="def"/> or the reward was already given.
        /// </summary>
        public static bool GrantReward(Definition def, PlayerUnit hero)
        {
            if (def == null || hero == null || hero.CharacterClass == null) return false;
            if (hero.CharacterClass.ClassType != def.Class || IsRewarded(def)) return false;

            StoryFlags.Set(def.RewardFlag);
            hero.AddWeaponDamageBonus(1);

            QuestManager quests = QuestManager.Instance;
            if (quests != null && quests.GetQuest(def.QuestId) != null)
            {
                if (quests.GetQuestState(def.QuestId) == QuestState.NotStarted) quests.StartQuest(def.QuestId);
                quests.SetQuestProgress(def.QuestId, 1);
                quests.CompleteQuest(def.QuestId);
            }

            PlayerProgressionManager.Instance?.GrantBonusLevel(def.Title + " completed");
            return true;
        }
    }
}
