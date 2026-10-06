using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.World;

namespace CastleOfTheD20.UI
{
    /// <summary>
    /// First-time guidance (critical review D2): a short story intro after New Adventure, and the four basic
    /// combat moves (move, attack, drink a potion, end the turn) when the first fight in Barnaby's cellar
    /// starts. Each shows once per adventure (story flags).
    /// </summary>
    public static class TutorialHints
    {
        public const string IntroSeenFlag = "TutorialIntroSeen";
        public const string CombatHintsSeenFlag = "TutorialCombatSeen";

        public static readonly string[] IntroTitles =
        {
            "Oakhaven, in the Castle's Shadow",
            "The Curse of Stone",
            "Those Who Stand Guard",
            "Your Road"
        };

        public static readonly string[] IntroPages =
        {
            "Above the village of Oakhaven stands the castle of the Gargoyle King. For as long as anyone can remember, its windows " +
            "have been dark, and on quiet nights the villagers hear armour moving on the walls.",

            "The old stories say the Gargoyle King, once a living man, feared death so much that he asked his court sorceress, the Shadow Witch, to make him live forever. " +
            "The ritual worked, after a fashion: he, his castle and everyone sworn to him turned to living stone.",

            "Three guardians still keep the castle: the Cursed Commander at the gates, the Shadow Witch among her books, " +
            "and the Gargoyle King on his throne. Break all three and the curse breaks with them.",

            "Talk to the villagers first. Baldur the smith, Barnaby at the tavern, Mirabel the healer and Elder Othelia all need help, " +
            "and every coin and potion will count. When you are ready, take the forest road north to the castle."
        };

        private const string CombatHintsTitle = "Your First Fight";

        private const string CombatHints =
            "<b>1. Move</b>: press <b>Move</b> and click a blue tile. You may move once per turn.\n\n" +
            "<b>2. Act</b>: click an ability card, then an enemy (red tile); hover an enemy to see your chance. " +
            "Each turn you also have a <b>bonus action</b> (cards marked Bonus, potions) and the row above the bar: Dash, Dodge, Disengage.\n\n" +
            "<b>3. Drink a potion</b>: press <b>[Q]</b> or the potion slot on your card when you are hurt. It uses your bonus action.\n\n" +
            "<b>4. End the turn</b>: press the hourglass button when you are done. Then the enemies act. [C] shows your character sheet.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            DungeonRoomController.OnRoomCombatStarted -= HandleCombatStarted;
            DungeonRoomController.OnRoomCombatStarted += HandleCombatStarted;
        }

        /// <summary>Plays the story intro (New Adventure). Shown once per adventure.</summary>
        public static void ShowIntro(System.Action closed = null)
        {
            if (StoryFlags.Has(IntroSeenFlag))
            {
                closed?.Invoke();
                return;
            }
            StoryFlags.Set(IntroSeenFlag);
            StoryPanelUI.ShowPages(IntroTitles, IntroPages, closed);
        }

        private static void HandleCombatStarted(DungeonRoomController room)
        {
            if (room == null || StoryFlags.Has(CombatHintsSeenFlag)) return;
            if (!string.Equals(room.roomLocation, "Cellar", System.StringComparison.OrdinalIgnoreCase)) return;
            ShowCombatHints();
        }

        /// <summary>The four combat basics (first cellar fight).</summary>
        public static void ShowCombatHints()
        {
            StoryFlags.Set(CombatHintsSeenFlag);
            StoryPanelUI.Show(CombatHintsTitle, CombatHints);
        }
    }
}
