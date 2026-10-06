using System;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// D&D rules shared by heroes and enemies (Vili, 2026-10-06): Advantage and Disadvantage cancel out,
    /// a Dodging target gives attackers Disadvantage, saving throws for enemies (against the hero's save DC)
    /// and for the hero, and the chance to succeed shown when aiming.
    /// </summary>
    public static class CombatRules
    {
        /// <summary>Advantage and Disadvantage together cancel out, as in D&D.</summary>
        public static AdvantageType Combine(bool advantage, bool disadvantage)
        {
            if (advantage == disadvantage) return AdvantageType.None;
            return advantage ? AdvantageType.Advantage : AdvantageType.Disadvantage;
        }

        /// <summary>
        /// Advantage on an attack roll: the attacker's Shadow Step / Blink edge, Disadvantage while the attacker
        /// is blinded or the target is dodging.
        /// </summary>
        public static AdvantageType AttackAdvantage(CombatUnit attacker, CombatUnit target)
        {
            bool advantage = false;
            bool disadvantage = false;

            StatusEffectController mine = attacker != null ? attacker.StatusEffects : null;
            if (mine != null)
            {
                advantage = mine.HasEffect(StatusEffectType.AdvantageNextAttack);
                disadvantage = mine.HasEffect(StatusEffectType.Blind);
            }

            StatusEffectController theirs = target != null ? target.StatusEffects : null;
            if (theirs != null && theirs.HasEffect(StatusEffectType.Dodging)) disadvantage = true;

            return Combine(advantage, disadvantage);
        }

        /// <summary>The lowest d20 face that succeeds: a natural 1 always fails and a natural 20 always succeeds.</summary>
        public static int NeededRoll(int bonus, int targetNumber)
        {
            return Mathf.Clamp(targetNumber - bonus, 2, 20);
        }

        /// <summary>Chance (0..1) that d20 + <paramref name="bonus"/> meets <paramref name="targetNumber"/>.</summary>
        public static float SuccessChance(int bonus, int targetNumber, AdvantageType advantage)
        {
            float single = (21 - NeededRoll(bonus, targetNumber)) / 20f;
            switch (advantage)
            {
                case AdvantageType.Advantage: return 1f - (1f - single) * (1f - single);
                case AdvantageType.Disadvantage: return single * single;
                default: return single;
            }
        }

        /// <summary>Chance as a whole percent, e.g. 55.</summary>
        public static int SuccessPercent(int bonus, int targetNumber, AdvantageType advantage)
        {
            return Mathf.RoundToInt(SuccessChance(bonus, targetNumber, advantage) * 100f);
        }

        #region Saving Throws

        /// <summary>
        /// The saving throw an ability forces on its targets, or false when it uses an attack roll (or nothing).
        /// <paramref name="halfOnSave"/>: a successful save still takes half the damage.
        /// </summary>
        public static bool TryGetSave(AbilitySO ability, out HeroAttribute attribute, out bool halfOnSave)
        {
            attribute = HeroAttribute.Dexterity;
            halfOnSave = false;
            if (ability == null) return false;

            string id = ability.BaseAbilityID.ToLowerInvariant();
            if (id.Contains("fireball"))
            {
                attribute = HeroAttribute.Dexterity;
                halfOnSave = true;
                return true;
            }
            if (id.Contains("war_cry"))
            {
                attribute = HeroAttribute.Strength;
                halfOnSave = true;
                return true;
            }
            if (id.Contains("arcane_chains"))
            {
                attribute = HeroAttribute.Strength;
                halfOnSave = true;
                return true;
            }
            if (id.Contains("poison_cloud"))
            {
                attribute = HeroAttribute.Constitution;
                halfOnSave = true;
                return true;
            }
            if (id.Contains("smoke_bomb"))
            {
                attribute = HeroAttribute.Constitution;
                halfOnSave = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// An enemy's saving throw against <paramref name="dc"/>, shown in the dice window and the combat log.
        /// Resolves at once (enemy rolls are never rerolled).
        /// </summary>
        public static DiceResult RollEnemySave(EnemyUnit enemy, HeroAttribute attribute, int dc, string against)
        {
            int bonus = enemy != null ? enemy.GetSaveBonus(attribute) : 0;
            bool dodging = enemy != null && enemy.StatusEffects != null && enemy.StatusEffects.HasEffect(StatusEffectType.Dodging);
            AdvantageType advantage = attribute == HeroAttribute.Dexterity && dodging ? AdvantageType.Advantage : AdvantageType.None;

            DiceResult save = DiceSystem.RollD20(bonus, dc, advantage);
            string name = enemy != null ? enemy.UnitName : "The enemy";
            string title = $"{name} {HeroAttributes.GetShortName(attribute)} save";
            if (Application.isPlaying && UI.DiceUIController.Instance != null)
            {
                UI.DiceUIController.Instance.ShowDiceRoll(save, title);
            }

            string outcome = save.isSuccess ? "<color=#8FD18F>saved</color>" : "<color=#E87C6A>failed</color>";
            string message = $"{name} rolls a {HeroAttributes.GetShortName(attribute)} save against {against}: {save.finalTotal} vs DC {dc}, {outcome}.";
            Debug.Log("[CombatRules] " + message);
            UI.CombatUIController.Instance?.LogCombatMessage(message);
            return save;
        }

        /// <summary>
        /// The hero's saving throw: attribute + proficiency for the class's saves + the Oathkeeper Shield, with
        /// Advantage on DEX saves while dodging. A failed save can be rerolled (Rune of Fate, Inspiration).
        /// </summary>
        public static DiceResult RollHeroSave(PlayerUnit hero, HeroAttribute attribute, int dc, string title, Action<DiceResult> onResolved)
        {
            int bonus = HeroAttributes.GetSaveBonus(hero, attribute);
            bool dodging = hero != null && hero.StatusEffects != null && hero.StatusEffects.HasEffect(StatusEffectType.Dodging);
            AdvantageType advantage = attribute == HeroAttribute.Dexterity && dodging ? AdvantageType.Advantage : AdvantageType.None;
            return RerollableRoll.Roll(bonus, dc, advantage, onResolved, title);
        }

        #endregion
    }
}
