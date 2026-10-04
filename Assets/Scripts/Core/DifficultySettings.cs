using UnityEngine;

namespace CastleOfTheD20.Core
{
    /// <summary>Difficulty chosen for the adventure in the main menu.</summary>
    public enum DifficultyLevel
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    /// <summary>
    /// Adventure difficulty (critical review B9): Easy gives the hero +2 AC and one free reroll per fight,
    /// Normal is the rules as written, Hard gives every enemy +2 to hit. Saved with the adventure.
    /// </summary>
    public static class DifficultySettings
    {
        /// <summary>Hero AC bonus on Easy.</summary>
        public const int EasyArmorBonus = 2;

        /// <summary>Enemy attack bonus on Hard.</summary>
        public const int HardEnemyHitBonus = 2;

        private static DifficultyLevel current = DifficultyLevel.Normal;
        private static bool freeRerollUsedThisFight;

        /// <summary>Difficulty of the running adventure.</summary>
        public static DifficultyLevel Current
        {
            get => current;
            set => current = value;
        }

        /// <summary>Extra AC for the hero (Easy).</summary>
        public static int HeroArmorBonus => current == DifficultyLevel.Easy ? EasyArmorBonus : 0;

        /// <summary>Extra to-hit for enemies (Hard).</summary>
        public static int EnemyHitBonus => current == DifficultyLevel.Hard ? HardEnemyHitBonus : 0;

        /// <summary>True on Easy until the fight's free reroll has been spent.</summary>
        public static bool FreeRerollAvailable => current == DifficultyLevel.Easy && !freeRerollUsedThisFight;

        /// <summary>Spends the free reroll. False when there is none left.</summary>
        public static bool TryUseFreeReroll()
        {
            if (!FreeRerollAvailable) return false;
            freeRerollUsedThisFight = true;
            return true;
        }

        /// <summary>Called when a fight starts: the free reroll comes back.</summary>
        public static void OnCombatStarted()
        {
            freeRerollUsedThisFight = false;
        }

        /// <summary>Display name for menus.</summary>
        public static string GetLabel(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Easy: return "Easy";
                case DifficultyLevel.Hard: return "Hard";
                default: return "Normal";
            }
        }

        /// <summary>One-line description for menus.</summary>
        public static string GetDescription(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Easy: return "+2 AC and one free reroll every fight";
                case DifficultyLevel.Hard: return "Enemies get +2 to hit";
                default: return "The rules as written";
            }
        }

        /// <summary>Restores a saved value (unknown values fall back to Normal).</summary>
        public static void Restore(int saved)
        {
            current = saved >= 0 && saved <= 2 ? (DifficultyLevel)saved : DifficultyLevel.Normal;
            freeRerollUsedThisFight = false;
        }

        /// <summary>Back to Normal (new adventure, tests).</summary>
        public static void Reset()
        {
            current = DifficultyLevel.Normal;
            freeRerollUsedThisFight = false;
        }
    }
}
