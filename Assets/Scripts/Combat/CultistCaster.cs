using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// A curse-cult caster (critical review C7). Keeps its distance like any ranged enemy. On its turn it heals
    /// a badly hurt ally in range (1d8+2); otherwise it curses the hero from up to 4 tiles: a WIS saving throw
    /// (DC 12) that on a failure deals 1d4 and blinds them for a turn (Disadvantage on their next attacks).
    /// At a quarter of its health it may lose its nerve and surrender (Vili, 2026-10-06).
    /// </summary>
    public class CultistCaster : EnemyUnit
    {
        /// <summary>Range of the curse and the healing chant.</summary>
        public const int SpellRange = 4;

        /// <summary>DC of the WIS save against the curse.</summary>
        public const int CurseSaveDC = 12;

        /// <summary>Health fraction at which the cultist tests its nerve.</summary>
        public const float MoraleThreshold = 0.25f;

        [SerializeField] private string displayName = "Curse Cultist";

        // STR, DEX, CON, INT, WIS, CHA: a strong will, a weak body
        private static readonly int[] CultistSaves = { 0, 1, 0, 1, 2, 1 };

        /// <summary>The cultist's saving throws.</summary>
        protected override int[] SaveProfile => CultistSaves;

        /// <summary>Cultists value their lives over the curse.</summary>
        public override bool CanSurrender => true;

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

        public override void TakeDamage(int amount, bool isCritical = false)
        {
            base.TakeDamage(amount, isCritical);
            if (IsAlive)
            {
                CheckMorale(MoraleThreshold, false, $"{unitName} drops to their knees: \"Mercy! I yield!\"");
            }
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

        /// <summary>
        /// The curse: the hero makes a WIS save (DC 12). On a failure it deals 1d4 and blinds them for a turn.
        /// </summary>
        protected override void PerformAttack(PlayerUnit target, AbilityExecutor abilityExecutor)
        {
            if (target == null || !target.IsAlive) return;
            PlayAttackAnimation();
            FaceTowards(target.transform.position);
            UI.CombatUIController.Instance?.LogCombatMessage($"{unitName} hisses a curse at {target.UnitName} (WIS save, DC {CurseSaveDC})!");

            CombatRules.RollHeroSave(target, HeroAttribute.Wisdom, CurseSaveDC, "Curse / Wisdom save", save =>
            {
                if (target == null || !target.IsAlive) return;
                AbilityVfx.PlayRangedBolt(this, target, !save.isSuccess);
                if (save.isSuccess)
                {
                    UI.CombatUIController.Instance?.LogCombatMessage($"{target.UnitName} shrugs off the curse.");
                    return;
                }
                target.TakeDamage(RollAttackDamage());
                if (target.IsAlive)
                {
                    target.StatusEffects?.ApplyEffect(StatusEffectType.Blind, 1);
                    UI.CombatUIController.Instance?.LogCombatMessage($"The curse clouds {target.UnitName}'s eyes!");
                }
            });
        }
    }
}
