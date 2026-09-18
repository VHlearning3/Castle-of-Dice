namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Represents the current phase in the turn-based combat state machine.
    /// Controls the flow of actions between player, enemies, and resolution phases.
    /// </summary>
    public enum TurnState
    {
        /// <summary>Active player selects an action, moves on the grid, or uses an ability.</summary>
        PlayerTurn,

        /// <summary>Enemy AI calculates movement, range, and executes offensive or defensive actions.</summary>
        EnemyTurn,

        /// <summary>Active abilities, projectile impacts, area damage, and status effect ticks are resolved.</summary>
        ResolveAbilities,

        /// <summary>All encounter objectives are met; enemies are defeated.</summary>
        Victory,

        /// <summary>Hero health has dropped to zero; encounter lost.</summary>
        Defeat
    }

    /// <summary>
    /// Defines advantage or disadvantage modifiers applied to D20 rolls.
    /// </summary>
    public enum AdvantageType
    {
        /// <summary>Standard single D20 roll.</summary>
        None,

        /// <summary>Roll two D20 dice and take the higher result.</summary>
        Advantage,

        /// <summary>Roll two D20 dice and take the lower result.</summary>
        Disadvantage
    }

    /// <summary>
    /// Core hero archetypes available in Castle of the D20.
    /// </summary>
    public enum CharacterClassType
    {
        /// <summary>Sir Roland - Heavy armor, high HP, front-line protector with shield mechanics.</summary>
        Warrior,

        /// <summary>Scholar Elira - High intelligence, ranged area-of-effect spells, crowd control.</summary>
        Mage,

        /// <summary>Shadow-Corvo - High agility, single-target burst, critical strikes, and lockpicking.</summary>
        Rogue
    }

    /// <summary>
    /// Alias enum for CharacterClassType for scriptable object configuration and UI bindings.
    /// </summary>
    public enum CharacterClass
    {
        Warrior = CharacterClassType.Warrior,
        Mage = CharacterClassType.Mage,
        Rogue = CharacterClassType.Rogue
    }

    /// <summary>
    /// Targeting patterns and area profiles for hero and enemy abilities.
    /// </summary>
    public enum AbilityTargetType
    {
        /// <summary>Directly targets a single unit or grid tile.</summary>
        SingleTarget,

        /// <summary>Targets a 3x3 grid area centered around the selected tile.</summary>
        Area3x3,

        /// <summary>Applies directly to the caster (buffs, shields, self-heals).</summary>
        Self
    }

    /// <summary>
    /// Status effects and conditions that can affect combat units during battle.
    /// </summary>
    public enum StatusEffectType
    {
        /// <summary>No active effect.</summary>
        None,

        /// <summary>Deals poison damage (e.g. 1d6) at the start of each turn for a duration.</summary>
        Poison,

        /// <summary>Freezing chill that reduces movement distance by half.</summary>
        Frostbite,

        /// <summary>Impaired vision imposing disadvantage on attack rolls.</summary>
        Blind,

        /// <summary>Magical barrier absorbing incoming damage charges before health is impacted.</summary>
        ManaShield
    }

    /// <summary>
    /// Categories of items found in loot chests, dropped by foes, or sold by Blacksmith Baldur.
    /// </summary>
    public enum ItemType
    {
        /// <summary>Single-use restorative items like health potions.</summary>
        Consumable,

        /// <summary>Permanent or slotted weapon sharpening (+1 damage bonus).</summary>
        WeaponUpgrade,

        /// <summary>Permanent or slotted runic armor reinforcement (+1 Armor Class).</summary>
        ArmorUpgrade,

        /// <summary>Unique story or quest progression artifacts (e.g. Signet Ring, Swamp Herbs).</summary>
        QuestItem,

        /// <summary>Raw metal scrap gathered from castle ruins, redeemable for gold at the blacksmith.</summary>
        ScrapMetal
    }

    /// <summary>
    /// Tracking state for village and dungeon quests.
    /// </summary>
    public enum QuestState
    {
        /// <summary>Quest is discoverable or available from an NPC, but not yet accepted.</summary>
        NotStarted,

        /// <summary>Quest is currently active with pending objectives.</summary>
        InProgress,

        /// <summary>All quest requirements met and rewards distributed.</summary>
        Completed
    }

    /// <summary>
    /// Core character attributes used for D20 skill checks and modifier calculations.
    /// </summary>
    public enum StatType
    {
        /// <summary>No stat modifier applied.</summary>
        None,

        /// <summary>Physical prowess, melee attack scaling, and athletic checks.</summary>
        Strength,

        /// <summary>Arcane mastery, spell power, and magical lore checks.</summary>
        Intelligence,

        /// <summary>Dexterity, stealth, critical hit chances, and lockpicking checks.</summary>
        Agility
    }
}
