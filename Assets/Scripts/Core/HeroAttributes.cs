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

    /// <summary>The D&D skills a check can name, each tied to one attribute.</summary>
    public enum HeroSkill
    {
        Athletics,
        Acrobatics,
        SleightOfHand,
        Stealth,
        Arcana,
        History,
        Investigation,
        Nature,
        Religion,
        AnimalHandling,
        Insight,
        Medicine,
        Perception,
        Survival,
        Deception,
        Intimidation,
        Performance,
        Persuasion
    }

    /// <summary>
    /// Attribute modifiers for skill checks (critical review A11, C8). Each class has its primary attribute
    /// (Sir Roland STR, Elira INT, Corvo DEX), which grows with level-up attribute bonuses, and fixed
    /// modifiers for the others, so the mage no longer intimidates with Intelligence.
    /// D&D rules (Vili, 2026-10-06): a proficiency bonus (+2, +3 at level 5) is added to attacks, to the
    /// class's own skills and saving throws, and sets the ability save DC (8 + proficiency + attribute).
    /// </summary>
    public static class HeroAttributes
    {
        /// <summary>Count of HeroSkill values.</summary>
        public const int SkillCount = 18;

        /// <summary>Base of every ability save DC: 8 + proficiency + attribute.</summary>
        public const int SaveDCBase = 8;

        /// <summary>Proficiency bonus at <paramref name="level"/>: +2 for levels 1-4, +3 at level 5.</summary>
        public static int ProficiencyBonus(int level) => level >= 5 ? 3 : 2;

        /// <summary>The hero's proficiency bonus.</summary>
        public static int GetProficiencyBonus(PlayerUnit hero) => ProficiencyBonus(hero != null ? hero.Level : 1);

        /// <summary>To-hit bonus of the hero's attacks: primary attribute + proficiency.</summary>
        public static int GetAttackBonus(PlayerUnit hero)
        {
            if (hero == null) return 2;
            return hero.PrimaryAttributeBonus + GetProficiencyBonus(hero);
        }

        /// <summary>DC enemies roll their saving throws against: 8 + proficiency + primary attribute.</summary>
        public static int GetSaveDC(PlayerUnit hero)
        {
            if (hero == null) return 12;
            return SaveDCBase + GetProficiencyBonus(hero) + hero.PrimaryAttributeBonus;
        }

        /// <summary>The attribute a skill uses.</summary>
        public static HeroAttribute GetSkillAttribute(HeroSkill skill)
        {
            switch (skill)
            {
                case HeroSkill.Athletics:
                    return HeroAttribute.Strength;
                case HeroSkill.Acrobatics:
                case HeroSkill.SleightOfHand:
                case HeroSkill.Stealth:
                    return HeroAttribute.Dexterity;
                case HeroSkill.Arcana:
                case HeroSkill.History:
                case HeroSkill.Investigation:
                case HeroSkill.Nature:
                case HeroSkill.Religion:
                    return HeroAttribute.Intelligence;
                case HeroSkill.AnimalHandling:
                case HeroSkill.Insight:
                case HeroSkill.Medicine:
                case HeroSkill.Perception:
                case HeroSkill.Survival:
                    return HeroAttribute.Wisdom;
                default:
                    return HeroAttribute.Charisma;
            }
        }

        /// <summary>Player-facing skill name, e.g. "Sleight of Hand".</summary>
        public static string GetSkillName(HeroSkill skill)
        {
            switch (skill)
            {
                case HeroSkill.SleightOfHand: return "Sleight of Hand";
                case HeroSkill.AnimalHandling: return "Animal Handling";
                default: return skill.ToString();
            }
        }

        /// <summary>Skills each class is trained in: the warrior's, the mage's and the rogue's.</summary>
        public static bool IsProficient(CharacterClassType classType, HeroSkill skill)
        {
            switch (classType)
            {
                case CharacterClassType.Warrior:
                    return skill == HeroSkill.Athletics || skill == HeroSkill.Intimidation || skill == HeroSkill.Perception;
                case CharacterClassType.Mage:
                    return skill == HeroSkill.Arcana || skill == HeroSkill.History || skill == HeroSkill.Investigation || skill == HeroSkill.Insight;
                case CharacterClassType.Rogue:
                    return skill == HeroSkill.Stealth || skill == HeroSkill.SleightOfHand || skill == HeroSkill.Acrobatics
                        || skill == HeroSkill.Perception || skill == HeroSkill.Persuasion;
                default:
                    return false;
            }
        }

        /// <summary>Rogue expertise: double proficiency in Stealth and Sleight of Hand.</summary>
        public static bool HasExpertise(CharacterClassType classType, HeroSkill skill)
        {
            return classType == CharacterClassType.Rogue && (skill == HeroSkill.Stealth || skill == HeroSkill.SleightOfHand);
        }

        /// <summary>Saving throws each class is proficient in (warrior STR/CON, mage INT/WIS, rogue DEX/INT).</summary>
        public static bool IsSaveProficient(CharacterClassType classType, HeroAttribute attribute)
        {
            switch (classType)
            {
                case CharacterClassType.Warrior:
                    return attribute == HeroAttribute.Strength || attribute == HeroAttribute.Constitution;
                case CharacterClassType.Mage:
                    return attribute == HeroAttribute.Intelligence || attribute == HeroAttribute.Wisdom;
                case CharacterClassType.Rogue:
                    return attribute == HeroAttribute.Dexterity || attribute == HeroAttribute.Intelligence;
                default:
                    return false;
            }
        }

        /// <summary>Bonus the hero adds to a check of <paramref name="skill"/>: attribute + proficiency when trained.</summary>
        public static int GetSkillBonus(PlayerUnit hero, HeroSkill skill)
        {
            int bonus = GetModifier(hero, GetSkillAttribute(skill));
            if (hero == null || hero.CharacterClass == null) return bonus;

            CharacterClassType classType = hero.CharacterClass.ClassType;
            if (IsProficient(classType, skill))
            {
                int prof = GetProficiencyBonus(hero);
                bonus += HasExpertise(classType, skill) ? prof * 2 : prof;
            }
            return bonus;
        }

        /// <summary>
        /// Bonus the hero adds to a saving throw: attribute, proficiency for the class's two saves, and worn
        /// magic items (the Oathkeeper Shield).
        /// </summary>
        public static int GetSaveBonus(PlayerUnit hero, HeroAttribute attribute)
        {
            int bonus = GetModifier(hero, attribute);
            if (hero != null && hero.CharacterClass != null && IsSaveProficient(hero.CharacterClass.ClassType, attribute))
            {
                bonus += GetProficiencyBonus(hero);
            }
            return bonus + Economy.MagicItems.SavingThrowBonus;
        }

        /// <summary>Bonus a death save gets: no attribute, only worn magic items.</summary>
        public static int GetDeathSaveBonus() => Economy.MagicItems.SavingThrowBonus;

        /// <summary>Attribute score shown on the character sheet: 10 + 2 x modifier (+3 reads as 16).</summary>
        public static int GetScore(int modifier) => 10 + modifier * 2;

        /// <summary>
        /// The bonus for a check described in words ("Persuasion (Charisma/Agility)", "Sleight of Hand / Dexterity
        /// Check"): the best of the skills and attributes it names, so a trained skill adds proficiency.
        /// <paramref name="label"/> names what was used, e.g. "Persuasion +4". Checks that name nothing use the
        /// class's primary attribute.
        /// </summary>
        public static int ResolveCheckBonus(PlayerUnit hero, string checkDescription, out string label)
        {
            HeroAttribute fallback = hero != null && hero.CharacterClass != null
                ? GetPrimaryAttribute(hero.CharacterClass.ClassType)
                : HeroAttribute.Strength;
            int best = GetModifier(hero, fallback);
            string bestName = GetShortName(fallback);
            bool found = false;

            string text = string.IsNullOrEmpty(checkDescription) ? string.Empty : checkDescription.ToLowerInvariant();
            for (int i = 0; i < SkillCount && text.Length > 0; i++)
            {
                HeroSkill skill = (HeroSkill)i;
                if (!MentionsSkill(text, skill)) continue;
                int value = GetSkillBonus(hero, skill);
                if (!found || value > best)
                {
                    found = true;
                    best = value;
                    bestName = GetSkillName(skill);
                }
            }

            for (int i = 0; i < 6 && text.Length > 0; i++)
            {
                HeroAttribute attribute = (HeroAttribute)i;
                if (!MentionsAttributeName(text, attribute)) continue;
                int value = GetModifier(hero, attribute);
                if (!found || value > best)
                {
                    found = true;
                    best = value;
                    bestName = GetShortName(attribute);
                }
            }

            if (!found && text.Length > 0)
            {
                // Older wording without a skill or attribute name: the old keyword table decides the attribute
                HeroAttribute attribute = ResolveCheckAttribute(hero, checkDescription);
                best = GetModifier(hero, attribute);
                bestName = GetShortName(attribute);
            }

            label = bestName + " " + (best < 0 ? "-" : "+") + Math.Abs(best);
            return best;
        }

        /// <summary>Bonus for a check described in words (see the overload with a label).</summary>
        public static int ResolveCheckBonus(PlayerUnit hero, string checkDescription)
        {
            return ResolveCheckBonus(hero, checkDescription, out _);
        }

        private static bool MentionsSkill(string text, HeroSkill skill)
        {
            switch (skill)
            {
                case HeroSkill.Athletics: return text.Contains("athletic") || text.Contains("break") || text.Contains("force");
                case HeroSkill.Acrobatics: return text.Contains("acrobat") || text.Contains("balance");
                case HeroSkill.SleightOfHand: return text.Contains("sleight") || text.Contains("pickpocket") || text.Contains("lockpick");
                case HeroSkill.Stealth: return text.Contains("stealth") || text.Contains("sneak");
                case HeroSkill.Arcana: return text.Contains("arcan") || text.Contains("rune");
                case HeroSkill.History: return text.Contains("history") || text.Contains("lore");
                case HeroSkill.Investigation: return text.Contains("investigat") || text.Contains("search");
                case HeroSkill.Nature: return text.Contains("nature");
                case HeroSkill.Religion: return text.Contains("religio");
                case HeroSkill.AnimalHandling: return text.Contains("animal");
                case HeroSkill.Insight: return text.Contains("insight");
                case HeroSkill.Medicine: return text.Contains("medicine");
                case HeroSkill.Perception: return text.Contains("percept") || text.Contains("spot");
                case HeroSkill.Survival: return text.Contains("surviv") || text.Contains("track");
                case HeroSkill.Deception: return text.Contains("decept") || text.Contains("bluff");
                case HeroSkill.Intimidation: return text.Contains("intimidat");
                case HeroSkill.Performance: return text.Contains("perform");
                default: return text.Contains("persua") || text.Contains("diplomac");
            }
        }

        private static bool MentionsAttributeName(string text, HeroAttribute attribute)
        {
            switch (attribute)
            {
                case HeroAttribute.Strength: return text.Contains("strength") || text.Contains("(str");
                case HeroAttribute.Dexterity: return text.Contains("dexterity") || text.Contains("agility");
                case HeroAttribute.Constitution: return text.Contains("constitution") || text.Contains("endurance") || text.Contains("(con");
                case HeroAttribute.Intelligence: return text.Contains("intelligence");
                case HeroAttribute.Wisdom: return text.Contains("wisdom");
                default: return text.Contains("charisma") || text.Contains("honor");
            }
        }
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
