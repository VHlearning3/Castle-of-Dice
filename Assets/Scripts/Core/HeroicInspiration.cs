using System;
using UnityEngine;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Heroic Inspiration (Vili, 2026-10-06): playing the hero as a hero earns one Inspiration at a time: releasing
    /// the Cursed Commander, sparing the Shadow Witch or a surrendering cultist, finishing a villager's quest or the
    /// hero's own quest. It rerolls one failed d20, like the Rune of Fate, and is spent before the runes. Saved as a
    /// story flag.
    /// </summary>
    public static class HeroicInspiration
    {
        /// <summary>Story flag held while the hero is inspired.</summary>
        public const string Flag = "HeroicInspiration";

        /// <summary>Fired when Inspiration is gained (true) or spent (false).</summary>
        public static event Action<bool> OnChanged;

        /// <summary>Whether the hero holds Inspiration.</summary>
        public static bool Has => StoryFlags.Has(Flag);

        // Story flags of good deeds that inspire the hero when they happen
        private static readonly string[] GoodDeedFlags = { "CommanderReleased", "MalakorSpared" };

        static HeroicInspiration()
        {
            Register();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            StoryFlags.OnFlagSet -= HandleFlagSet;
            StoryFlags.OnFlagSet += HandleFlagSet;
            Economy.QuestManager.OnQuestCompleted -= HandleQuestCompleted;
            Economy.QuestManager.OnQuestCompleted += HandleQuestCompleted;
        }

        /// <summary>
        /// Gives the hero Inspiration for <paramref name="reason"/>. A hero holds at most one; returns false when
        /// they already had it.
        /// </summary>
        public static bool Grant(string reason)
        {
            if (Has) return false;
            StoryFlags.Set(Flag);
            Debug.Log("[HeroicInspiration] Inspired: " + reason);
            if (Application.isPlaying)
            {
                UI.LockpickMinigameUI.ShowToast(string.IsNullOrEmpty(reason)
                    ? "You feel inspired! (one free reroll)"
                    : "Inspired: " + reason + " (one free reroll)");
            }
            OnChanged?.Invoke(true);
            return true;
        }

        /// <summary>Uses the Inspiration up. False when the hero had none.</summary>
        public static bool Spend()
        {
            if (!Has) return false;
            StoryFlags.Remove(Flag);
            OnChanged?.Invoke(false);
            return true;
        }

        /// <summary>Whether <paramref name="flag"/> marks a deed worth Inspiration.</summary>
        public static bool IsGoodDeed(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return false;
            for (int i = 0; i < GoodDeedFlags.Length; i++)
            {
                if (string.Equals(flag, GoodDeedFlags[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            // Each hero's own quest (ClassQuests): "<quest id>_rewarded"
            return flag.StartsWith("quest_class_", StringComparison.OrdinalIgnoreCase)
                && flag.EndsWith("_rewarded", StringComparison.OrdinalIgnoreCase);
        }

        private static void HandleFlagSet(string flag)
        {
            if (!IsGoodDeed(flag)) return;
            if (string.Equals(flag, "CommanderReleased", StringComparison.OrdinalIgnoreCase)) Grant("you released the Cursed Commander from his oath");
            else if (string.Equals(flag, "MalakorSpared", StringComparison.OrdinalIgnoreCase)) Grant("you showed the Shadow Witch mercy");
            else Grant("you finished your own quest");
        }

        private static void HandleQuestCompleted(Economy.QuestSO quest, int reward)
        {
            Grant("you helped the people of Oakhaven");
        }
    }
}
