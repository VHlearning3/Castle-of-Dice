using System;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.Core
{
    /// <summary>The six D&D attributes a skill check can use.</summary>
    public enum HeroAttribute
    {
        Strength,
        Dexterity,
        Constitution,
        Intelligence,
        Wisdom,
        Charisma
    }

    /// <summary>
    /// Attribute modifiers for skill checks (critical review A11, C8). Each class has its primary attribute
    /// (Sir Roland STR, Elira INT, Corvo DEX), which grows with level-up attribute bonuses, and fixed
    /// modifiers for the others, so the mage no longer intimidates with Intelligence.
    /// </summary>
    public static class HeroAttributes
    {
        // Rows: Warrior, Mage, Rogue. Columns follow HeroAttribute (STR, DEX, CON, INT, WIS, CHA).
        // The primary attribute's column is replaced by the hero's live PrimaryAttributeBonus.
        private static readonly int[,] ClassModifiers =
        {
            { 3, 0, 2, -1, 1, 1 },  // Warrior: STR
            { -1, 1, 0, 3, 2, 0 },  // Mage: INT
            { 0, 3, 1, 1, 0, 2 }    // Rogue: DEX
        };

        /// <summary>The attribute a class adds to its attacks and level-up growth.</summary>
        public static HeroAttribute GetPrimaryAttribute(CharacterClassType classType)
        {
            switch (classType)
            {
                case CharacterClassType.Mage: return HeroAttribute.Intelligence;
                case CharacterClassType.Rogue: return HeroAttribute.Dexterity;
                default: return HeroAttribute.Strength;
            }
        }

        /// <summary>Modifier the hero adds to a check of <paramref name="attribute"/>.</summary>
        public static int GetModifier(PlayerUnit hero, HeroAttribute attribute)
        {
            if (hero == null) return 2;
            if (hero.CharacterClass == null) return hero.PrimaryAttributeBonus;

            CharacterClassType classType = hero.CharacterClass.ClassType;
            if (attribute == GetPrimaryAttribute(classType)) return hero.PrimaryAttributeBonus;

            int row = Math.Max(0, Math.Min(2, (int)classType));
            return ClassModifiers[row, (int)attribute];
        }

        /// <summary>Short label for check banners, e.g. "STR".</summary>
        public static string GetShortName(HeroAttribute attribute)
        {
            switch (attribute)
            {
                case HeroAttribute.Strength: return "STR";
                case HeroAttribute.Dexterity: return "DEX";
                case HeroAttribute.Constitution: return "CON";
                case HeroAttribute.Intelligence: return "INT";
                case HeroAttribute.Wisdom: return "WIS";
                default: return "CHA";
            }
        }

        /// <summary>
        /// Reads which attributes a check names ("Intimidation / Strength Check", "Persuasion (Charisma/Agility)")
        /// and returns the one the hero is best at. Checks that name none use the class's primary attribute.
        /// </summary>
        public static HeroAttribute ResolveCheckAttribute(PlayerUnit hero, string checkDescription)
        {
            HeroAttribute fallback = hero != null && hero.CharacterClass != null
                ? GetPrimaryAttribute(hero.CharacterClass.ClassType)
                : HeroAttribute.Strength;
            if (string.IsNullOrEmpty(checkDescription)) return fallback;

            string text = checkDescription.ToLowerInvariant();
            bool found = false;
            HeroAttribute best = fallback;
            int bestValue = int.MinValue;

            for (int i = 0; i < 6; i++)
            {
                HeroAttribute attribute = (HeroAttribute)i;
                if (!Mentions(text, attribute)) continue;

                int value = GetModifier(hero, attribute);
                if (!found || value > bestValue)
                {
                    found = true;
                    best = attribute;
                    bestValue = value;
                }
            }

            return found ? best : fallback;
        }

        private static bool Mentions(string text, HeroAttribute attribute)
        {
            switch (attribute)
            {
                case HeroAttribute.Strength:
                    return text.Contains("strength") || text.Contains("(str") || text.Contains("intimidat") || text.Contains("athletic") || text.Contains("break");
                case HeroAttribute.Dexterity:
                    return text.Contains("dexterity") || text.Contains("agility") || text.Contains("lockpick") || text.Contains("stealth") || text.Contains("sleight") || text.Contains("steal");
                case HeroAttribute.Constitution:
                    return text.Contains("constitution") || text.Contains("endurance") || text.Contains("(con");
                case HeroAttribute.Intelligence:
                    return text.Contains("intelligence") || text.Contains("arcan") || text.Contains("lore") || text.Contains("history") || text.Contains("rune");
                case HeroAttribute.Wisdom:
                    return text.Contains("wisdom") || text.Contains("nature") || text.Contains("insight") || text.Contains("perception");
                default:
                    return text.Contains("charisma") || text.Contains("persua") || text.Contains("honor") || text.Contains("deception") || text.Contains("diplomacy");
            }
        }
    }
}
