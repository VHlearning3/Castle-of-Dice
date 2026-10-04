using CastleOfTheD20.Data;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Cooldowns from the critical review (B1): the strongest defensive and control abilities can't be used
    /// every turn. A cooldown of 3 means the ability is used on turn T and is ready again on turn T + 3.
    /// </summary>
    public static class AbilityCooldowns
    {
        /// <summary>Turns before the ability can be used again (0 = every turn).</summary>
        public static int GetCooldownTurns(AbilitySO ability)
        {
            if (ability == null) return 0;
            string id = ability.BaseAbilityID.ToLowerInvariant();

            if (id.Contains("iron_will")) return 3;
            if (id.Contains("mana_shield")) return 3;
            if (id.Contains("smoke_bomb")) return 2;
            if (id.Contains("war_cry")) return 2;
            if (id.Contains("blink")) return 2;

            // Level 4 abilities (B8)
            if (id.Contains("retaliation")) return 3;
            if (id.Contains("arcane_chains")) return 3;
            if (id.Contains("poison_cloud")) return 3;
            return 0;
        }
    }
}
