using CastleOfTheD20.Data;

namespace CastleOfTheD20.Combat
{
    /// <summary>What a combat option costs: the turn's action or its bonus action.</summary>
    public enum ActionCost
    {
        Action,
        BonusAction
    }

    /// <summary>
    /// The D&D action economy (Vili, 2026-10-06): each turn the hero has a move, an action and a bonus action,
    /// plus one reaction per round. Quick moves are bonus actions: Iron Will (a Second Wind), Blink (a Misty Step),
    /// Shadow Step, drinking a potion and coating a blade with poison. The rogue's Cunning Action makes Dash and
    /// Disengage bonus actions for her.
    /// </summary>
    public static class ActionEconomy
    {
        /// <summary>What using <paramref name="ability"/> costs.</summary>
        public static ActionCost GetCost(AbilitySO ability)
        {
            if (ability == null) return ActionCost.Action;
            string id = ability.BaseAbilityID.ToLowerInvariant();
            if (id.Contains("iron_will") || id.Contains("blink") || id.Contains("shadow_step") || id.Contains("shadowstep"))
            {
                return ActionCost.BonusAction;
            }
            return ActionCost.Action;
        }

        /// <summary>Whether <paramref name="ability"/> is a bonus action.</summary>
        public static bool IsBonusAction(AbilitySO ability) => GetCost(ability) == ActionCost.BonusAction;

        /// <summary>Short label for cards and the character sheet.</summary>
        public static string CostLabel(ActionCost cost) => cost == ActionCost.BonusAction ? "Bonus action" : "Action";
    }
}
