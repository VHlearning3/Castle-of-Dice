using System.Collections.Generic;
using UnityEngine;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// A forest wolf (critical review C7): fast (move 6) and a pack hunter: Advantage on its bite when another
    /// wolf is already next to its prey. Ready for a wolf model; no scene uses it until Vili adds one.
    /// </summary>
    public class WolfUnit : EnemyUnit
    {
        public override void InitializeUnit()
        {
            if (unitName == "Combatant") unitName = "Grey Wolf";
            if (maxHP == 20) maxHP = 14;
            currentHP = maxHP;
            movementRange = 6;
            attackBonus = Mathf.Max(attackBonus, 3);
            ConfigureDamageDice(1, 6, 1);
            ConfigureInitiative(2);
            base.InitializeUnit();
        }

        /// <summary>Whether another living wolf stands next to <paramref name="prey"/>.</summary>
        public bool HasPackmateNear(CombatUnit prey)
        {
            GridManager grid = GridManager.Instance;
            if (prey == null || grid == null || TurnManager.Instance == null) return false;
            IReadOnlyList<CombatUnit> units = TurnManager.Instance.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] is WolfUnit other && other != this && other.IsAlive && grid.GetDistance(other.GridPosition, prey.GridPosition) <= 1)
                {
                    return true;
                }
            }
            return false;
        }

        protected override void PerformAttack(PlayerUnit target, AbilityExecutor abilityExecutor)
        {
            bool pack = HasPackmateNear(target);
            if (pack)
            {
                UI.CombatUIController.Instance?.LogCombatMessage($"The pack closes in: {unitName} bites with Advantage!");
                StatusEffects?.ApplyEffect(Core.StatusEffectType.AdvantageNextAttack, 1);
            }
            base.PerformAttack(target, abilityExecutor);
            if (pack) StatusEffects?.ConsumeAdvantageNextAttack();
        }
    }
}
