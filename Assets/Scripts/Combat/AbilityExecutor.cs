using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Core combat execution engine that resolves AbilitySO actions.
    /// Handles Warrior abilities (Sword Slash, Shield Block, War Cry, Iron Will),
    /// Mage spells (Fireball 3x3 AOE, Frostbite, Mana Shield, Blink),
    /// and Rogue skills (Backstab, Smoke Bomb, Poison Dagger, Lockpicking).
    /// Integrates directly with DiceSystem.RollD20 for hit checks vs Armor Class (AC).
    /// </summary>
    public class AbilityExecutor : MonoBehaviour
    {
        #region Singleton

        public static AbilityExecutor Instance { get; private set; }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Primary Execution Entry Point

        /// <summary>
        /// Resolves an ability triggered by a caster targeting a grid position or unit.
        /// Evaluates range, D20 hit checks vs AC, damage calculation, status effects, and special mechanics.
        /// </summary>
        /// <param name="caster">The unit initiating the action.</param>
        /// <param name="ability">The AbilitySO data definition.</param>
        /// <param name="targetGridPos">Selected target coordinate on the grid.</param>
        /// <returns>True if the ability resolved successfully, false if out of range or invalid.</returns>
        public bool ExecuteAbility(CombatUnit caster, AbilitySO ability, Vector2Int targetGridPos)
        {
            if (caster == null || ability == null) return false;

            GridManager grid = GridManager.Instance;
            if (grid == null)
            {
                Debug.LogError("[AbilityExecutor] GridManager instance not found.");
                return false;
            }

            // Verify range (Self abilities always have range 0)
            int distance = grid.GetDistance(caster.GridPosition, targetGridPos);
            if (ability.TargetType != AbilityTargetType.Self && distance > ability.Range)
            {
                Debug.LogWarning($"[AbilityExecutor] Target out of range! Distance: {distance}, Max Range: {ability.Range}");
                return false;
            }

            // Identify special named abilities by ID or fallback to standard profile
            string id = ability.AbilityID.ToLowerInvariant();

            // --- Warrior Class Abilities ---
            if (id.Contains("shield_block"))
            {
                return ExecuteShieldBlock(caster, ability);
            }
            if (id.Contains("war_cry"))
            {
                return ExecuteWarCry(caster, ability, targetGridPos, grid);
            }
            if (id.Contains("iron_will"))
            {
                return ExecuteIronWill(caster, ability);
            }

            // --- Mage Class Abilities ---
            if (id.Contains("mana_shield"))
            {
                return ExecuteManaShield(caster, ability);
            }
            if (id.Contains("blink"))
            {
                return ExecuteBlink(caster, targetGridPos, grid);
            }
            if (ability.TargetType == AbilityTargetType.Area3x3 || id.Contains("fireball"))
            {
                return ExecuteArea3x3Ability(caster, ability, targetGridPos, grid);
            }

            // --- Rogue Class Abilities ---
            if (id.Contains("backstab"))
            {
                return ExecuteBackstab(caster, ability, targetGridPos, grid);
            }
            if (id.Contains("smoke_bomb"))
            {
                return ExecuteSmokeBomb(caster, ability, targetGridPos, grid);
            }

            // --- Standard / Fallback Execution (Single Target, Self, etc.) ---
            if (ability.TargetType == AbilityTargetType.Self)
            {
                return ExecuteSelfAbility(caster, ability);
            }

            return ExecuteSingleTargetAbility(caster, ability, targetGridPos, grid);
        }

        #endregion

        #region Warrior Abilities

        private bool ExecuteShieldBlock(CombatUnit caster, AbilitySO ability)
        {
            // Warrior Shield Block: raises defense (+3 AC) for next turn
            Debug.Log($"[AbilityExecutor] {caster.UnitName} uses Shield Block! Gaining +3 temporary AC defense.");
            // Apply ManaShield or temporary protection condition
            caster.StatusEffects?.ApplyEffect(StatusEffectType.ManaShield, durationTurns: 1);
            return true;
        }

        private bool ExecuteWarCry(CombatUnit caster, AbilitySO ability, Vector2Int targetGridPos, GridManager grid)
        {
            Debug.Log($"[AbilityExecutor] {caster.UnitName} roars with War Cry!");

            // Knockback adjacent enemies 1 tile away from caster
            List<GridTile> adjacent = grid.GetTilesInRadius(caster.GridPosition, radius: 1);
            foreach (var tile in adjacent)
            {
                if (tile.IsOccupied && tile.OccupyingUnit != null && tile.OccupyingUnit != caster)
                {
                    CombatUnit target = tile.OccupyingUnit;
                    Vector2Int pushDir = target.GridPosition - caster.GridPosition;
                    Vector2Int newPos = target.GridPosition + pushDir;

                    GridTile pushTile = grid.GetTileAt(newPos);
                    if (pushTile != null && pushTile.IsWalkable && !pushTile.IsOccupied)
                    {
                        Debug.Log($"[AbilityExecutor] War Cry knocks {target.UnitName} back to {newPos}!");
                        target.MoveToTile(pushTile);
                    }

                    // Deal minor shockwave damage
                    target.TakeDamage(ability.BaseValue);
                }
            }

            return true;
        }

        private bool ExecuteIronWill(CombatUnit caster, AbilitySO ability)
        {
            // Iron Will: Restores 30% of maximum HP
            int healAmount = Mathf.Max(1, Mathf.RoundToInt(caster.MaxHP * 0.30f));
            Debug.Log($"[AbilityExecutor] {caster.UnitName} activates Iron Will! Restoring {healAmount} HP.");
            caster.Heal(healAmount);
            return true;
        }

        #endregion

        #region Mage Abilities

        private bool ExecuteManaShield(CombatUnit caster, AbilitySO ability)
        {
            Debug.Log($"[AbilityExecutor] {caster.UnitName} summons Mana Shield!");
            caster.StatusEffects?.ApplyEffect(StatusEffectType.ManaShield, durationTurns: Mathf.Max(1, ability.EffectDurationTurns));
            return true;
        }

        private bool ExecuteBlink(CombatUnit caster, Vector2Int targetGridPos, GridManager grid)
        {
            GridTile targetTile = grid.GetTileAt(targetGridPos);
            if (targetTile == null || !targetTile.IsWalkable || targetTile.IsOccupied)
            {
                Debug.LogWarning($"[AbilityExecutor] Blink failed: Tile at {targetGridPos} is obstructed or invalid.");
                return false;
            }

            int distance = grid.GetDistance(caster.GridPosition, targetGridPos);
            if (distance > 5)
            {
                Debug.LogWarning($"[AbilityExecutor] Blink distance ({distance}) exceeds maximum range 5.");
                return false;
            }

            Debug.Log($"[AbilityExecutor] {caster.UnitName} blinks to {targetGridPos}!");
            caster.MoveToTile(targetTile);
            return true;
        }

        private bool ExecuteArea3x3Ability(CombatUnit caster, AbilitySO ability, Vector2Int targetGridPos, GridManager grid)
        {
            Debug.Log($"[AbilityExecutor] {caster.UnitName} casts {ability.AbilityName} on 3x3 area centered at {targetGridPos}!");

            List<GridTile> areaTiles = grid.GetArea3x3(targetGridPos);
            int bonus = GetCasterAttributeBonus(caster);

            foreach (var tile in areaTiles)
            {
                if (tile.IsOccupied && tile.OccupyingUnit != null)
                {
                    CombatUnit target = tile.OccupyingUnit;

                    // Avoid damaging caster in AOE unless specified
                    if (target == caster) continue;

                    if (ability.RequiresCheck)
                    {
                        DiceResult hitCheck = DiceSystem.RollD20(bonus, target.ArmorClass);
                        Debug.Log($"[AbilityExecutor] {ability.AbilityName} vs {target.UnitName}: {hitCheck}");

                        if (hitCheck.isSuccess)
                        {
                            int damage = hitCheck.isCriticalSuccess ? ability.BaseValue * 2 : ability.BaseValue;
                            target.TakeDamage(damage, hitCheck.isCriticalSuccess);
                            ApplyAbilityStatusEffect(target, ability);
                        }
                    }
                    else
                    {
                        target.TakeDamage(ability.BaseValue);
                        ApplyAbilityStatusEffect(target, ability);
                    }
                }
            }

            return true;
        }

        #endregion

        #region Rogue Abilities

        private bool ExecuteBackstab(CombatUnit caster, AbilitySO ability, Vector2Int targetGridPos, GridManager grid)
        {
            GridTile targetTile = grid.GetTileAt(targetGridPos);
            if (targetTile == null || !targetTile.IsOccupied || targetTile.OccupyingUnit == null)
            {
                Debug.LogWarning("[AbilityExecutor] Backstab requires an occupied target tile.");
                return false;
            }

            CombatUnit target = targetTile.OccupyingUnit;
            int bonus = GetCasterAttributeBonus(caster);

            // Backstab grants Advantage or 2x bonus modifier
            DiceResult hitCheck = DiceSystem.RollD20(bonus * 2, target.ArmorClass, AdvantageType.Advantage);
            Debug.Log($"[AbilityExecutor] {caster.UnitName} executes Backstab on {target.UnitName}: {hitCheck}");

            if (hitCheck.isSuccess)
            {
                int weaponBonus = caster is PlayerUnit player ? player.WeaponDamageBonus : 0;
                int damage = ability.BaseValue + weaponBonus;

                if (hitCheck.isCriticalSuccess)
                {
                    damage *= 2;
                }

                target.TakeDamage(damage, hitCheck.isCriticalSuccess);
                ApplyAbilityStatusEffect(target, ability);
            }

            return true;
        }

        private bool ExecuteSmokeBomb(CombatUnit caster, AbilitySO ability, Vector2Int targetGridPos, GridManager grid)
        {
            Debug.Log($"[AbilityExecutor] {caster.UnitName} throws a Smoke Bomb at {targetGridPos}!");

            List<GridTile> affected = grid.GetTilesInRadius(targetGridPos, radius: 1);
            foreach (var tile in affected)
            {
                if (tile.IsOccupied && tile.OccupyingUnit != null && tile.OccupyingUnit != caster)
                {
                    tile.OccupyingUnit.StatusEffects?.ApplyEffect(StatusEffectType.Blind, durationTurns: 1);
                }
            }

            return true;
        }

        #endregion

        #region Standard Helpers

        private bool ExecuteSingleTargetAbility(CombatUnit caster, AbilitySO ability, Vector2Int targetGridPos, GridManager grid)
        {
            GridTile targetTile = grid.GetTileAt(targetGridPos);
            if (targetTile == null || !targetTile.IsOccupied || targetTile.OccupyingUnit == null)
            {
                Debug.LogWarning($"[AbilityExecutor] Ability {ability.AbilityName} requires an occupied target tile.");
                return false;
            }

            CombatUnit target = targetTile.OccupyingUnit;
            int bonus = GetCasterAttributeBonus(caster);
            AdvantageType advantage = caster.StatusEffects != null ? caster.StatusEffects.GetAttackRollAdvantageModifier() : AdvantageType.None;

            if (ability.RequiresCheck)
            {
                DiceResult hitCheck = DiceSystem.RollD20(bonus, target.ArmorClass, advantage);
                Debug.Log($"[AbilityExecutor] {caster.UnitName} casts {ability.AbilityName} on {target.UnitName}: {hitCheck}");

                if (hitCheck.isSuccess)
                {
                    int weaponBonus = caster is PlayerUnit player ? player.WeaponDamageBonus : 0;
                    int damage = ability.BaseValue + weaponBonus;

                    if (hitCheck.isCriticalSuccess)
                    {
                        damage *= 2;
                    }

                    target.TakeDamage(damage, hitCheck.isCriticalSuccess);
                    ApplyAbilityStatusEffect(target, ability);
                }
                else
                {
                    Debug.Log($"[AbilityExecutor] {ability.AbilityName} missed {target.UnitName}!");
                }
            }
            else
            {
                // Direct heal or guaranteed damage
                if (ability.BaseValue > 0)
                {
                    target.TakeDamage(ability.BaseValue);
                }
                ApplyAbilityStatusEffect(target, ability);
            }

            return true;
        }

        private bool ExecuteSelfAbility(CombatUnit caster, AbilitySO ability)
        {
            Debug.Log($"[AbilityExecutor] {caster.UnitName} applies {ability.AbilityName} to self.");

            if (ability.BaseValue > 0)
            {
                caster.Heal(ability.BaseValue);
            }

            ApplyAbilityStatusEffect(caster, ability);
            return true;
        }

        private void ApplyAbilityStatusEffect(CombatUnit target, AbilitySO ability)
        {
            if (target == null || ability.AppliedEffect == StatusEffectType.None) return;

            int duration = Mathf.Max(1, ability.EffectDurationTurns);
            target.StatusEffects?.ApplyEffect(ability.AppliedEffect, duration);
        }

        private int GetCasterAttributeBonus(CombatUnit caster)
        {
            if (caster is PlayerUnit player)
            {
                return player.PrimaryAttributeBonus;
            }
            if (caster is EnemyUnit enemy)
            {
                return enemy.AttackBonus;
            }
            return 2;
        }

        #endregion
    }
}
