using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// The Tower's treasure guardian (critical review C5, C7): a chest that bites. Its bite (2d6+1) glues the
    /// hero in place: a hit also stops them moving on their next turn.
    /// </summary>
    public class MimicUnit : EnemyUnit
    {
        public override void InitializeUnit()
        {
            unitName = "Mimic";
            maxHP = 32;
            currentHP = maxHP;
            armorClass = 13;
            attackDamage = 8;
            attackBonus = 4;
            movementRange = 3;
            ConfigureDamageDice(2, 6, 1);
            base.InitializeUnit();
        }

        protected override void PerformAttack(PlayerUnit target, AbilityExecutor abilityExecutor)
        {
            if (target == null || !target.IsAlive) return;
            PlayAttackAnimation();
            DiceResult bite = ResolveBasicAttack(target, "bites");
            if (bite.isSuccess && target.IsAlive)
            {
                Debug.Log($"[Mimic] The mimic's glue holds {target.UnitName} fast!");
                UI.CombatUIController.Instance?.LogCombatMessage($"The mimic's sticky tongue holds {target.UnitName}: no moving next turn!");
                target.StatusEffects?.ApplyEffect(StatusEffectType.Immobilized, 1);
            }
        }
    }
}
