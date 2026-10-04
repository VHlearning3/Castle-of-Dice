using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// A curse-cult caster (critical review C7). Keeps its distance like any ranged enemy. On its turn it heals
    /// a badly hurt ally in range (1d8+2); otherwise it curses the hero from up to 4 tiles: a d20 roll against
    /// the hero's AC that deals 1d4 and blinds them for a turn (Disadvantage on their next attacks).
    /// </summary>
    public class CultistCaster : EnemyUnit
    {
        public const int SpellRange = 4;

        [SerializeField] private string displayName = "Curse Cultist";

        public override void InitializeUnit()
        {
            unitName = displayName;
            if (maxHP <= 0 || maxHP == 20) maxHP = 18;
            currentHP = maxHP;
            if (armorClass == 12) armorClass = 11;
            attackBonus = Mathf.Max(attackBonus, 3);
            movementRange = 3;
            ConfigureAttackRange(SpellRange);
            ConfigureDamageDice(1, 4, 0);
            base.InitializeUnit();
        }

        public override void ExecuteTurnAction(GridManager gridManager, AbilityExecutor abilityExecutor = null)
        {
            if (IsAlive && TryHealAlly(gridManager ?? GridManager.Instance))
            {
                OnTurnActionFinished();
                return;
            }
            base.ExecuteTurnAction(gridManager, abilityExecutor);
        }

        /// <summary>Heals the most hurt ally under 60% HP within spell range. Returns true when it did.</summary>
        public bool TryHealAlly(GridManager grid)
        {
            if (grid == null || TurnManager.Instance == null) return false;

            EnemyUnit patient = null;
            float worst = 0.6f;
            IReadOnlyList<CombatUnit> units = TurnManager.Instance.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
            {
                if (!(units[i] is EnemyUnit ally) || ally == this || !ally.IsAlive) continue;
                if (grid.GetDistance(gridPosition, ally.GridPosition) > SpellRange) continue;
                float ratio = (float)ally.CurrentHP / Mathf.Max(1, ally.MaxHP);
                if (ratio < worst)
                {
                    worst = ratio;
                    patient = ally;
                }
            }
            if (patient == null) return false;

            int heal = DiceSystem.RollDamage(1, 8) + 2;
            FaceTowards(patient.transform.position);
            PlayAttackAnimation();
            patient.Heal(heal);
            AbilityVfx.PlayPotionHeal(patient);
            Debug.Log($"[CultistCaster] {unitName} mends {patient.UnitName} for {heal}.");
            UI.CombatUIController.Instance?.LogCombatMessage($"{unitName} chants and {patient.UnitName} heals {heal} HP!");
            return true;
        }

        protected override void PerformAttack(PlayerUnit target, AbilityExecutor abilityExecutor)
        {
            if (target == null || !target.IsAlive) return;
            PlayAttackAnimation();
            DiceResult curse = ResolveBasicAttack(target, "curses");
            if (curse.isSuccess && target.IsAlive)
            {
                target.StatusEffects?.ApplyEffect(StatusEffectType.Blind, 1);
                UI.CombatUIController.Instance?.LogCombatMessage($"The curse clouds {target.UnitName}'s eyes!");
            }
        }
    }
}
