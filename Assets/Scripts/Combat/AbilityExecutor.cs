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
    /// and Rogue skills (Backstab, Smoke Bomb, Poison Dagger, Shadow Step).
    /// Damage follows the spec's dice formulas (e.g. Sword Slash 1d8 + STR) via AbilitySO.RollDamage.
    /// Integrates directly with DiceSystem.RollD20 for hit checks vs Armor Class (AC).
    /// </summary>
    public class AbilityExecutor : MonoBehaviour
    {
        #region Singleton

        public static AbilityExecutor Instance { get; private set; }

        #endregion

        #region Constants

        /// <summary>Farthest a War Cry shockwave pushes an adjacent enemy (spec: 1-2 tiles).</summary>
        public const int WarCryMaxPushTiles = 2;

        /// <summary>Blink range used when the ability asset does not set one (spec: 5-7 tiles).</summary>
        public const int BlinkMaxRange = 7;

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
                Debug.Log($"[AbilityExecutor] Target out of range! Distance: {distance}, Max Range: {ability.Range}");
                return false;
            }

            // Identify special named abilities by ID or fallback to standard profile
            string id = ability.AbilityID.ToLowerInvariant();

            // Face the target before the swing/cast
            if (ability.TargetType != AbilityTargetType.Self && targetGridPos != caster.GridPosition)
            {
                caster.FaceTowards(grid.GetWorldPosition(targetGridPos));
            }

            // Trigger animation on caster: the ability's own clip when the model has one (Elira's
            // mage_fireball etc.), otherwise the generic cast or swing
            if (caster.UnitAnimator != null && !caster.TrySetAnimatorTrigger(ability.AbilityID))
            {
                if (ability.TargetType == AbilityTargetType.Self || id.Contains("cast") || id.Contains("spell") || id.Contains("fireball") || id.Contains("frost") || id.Contains("shield") || id.Contains("blink") || id.Contains("mana"))
                {
                    caster.UnitAnimator.SetTrigger("CastSpell");
                }
                else
                {
                    caster.UnitAnimator.SetTrigger("Attack");
                }
            }

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
                return ExecuteBlink(caster, ability, targetGridPos, grid);
            }
            if (id.Contains("smoke_bomb"))
            {
                return ExecuteSmokeBomb(caster, ability, targetGridPos, grid);
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
            if (id.Contains("shadow_step") || id.Contains("shadowstep"))
            {
                return ExecuteShadowStep(caster, targetGridPos, grid);
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
            // Warrior Shield Wall: +4 AC until the next turn; an adjacent attacker that misses takes a 1d6 counterattack
            Debug.Log($"[AbilityExecutor] {caster.UnitName} raises a Shield Wall! +{StatusEffectController.ShieldWallArmorBonus} AC and counterattacks misses until next turn.");
            caster.StatusEffects?.ApplyEffect(StatusEffectType.ShieldWall, durationTurns: Mathf.Max(1, ability.EffectDurationTurns));
            return true;
        }

        private bool ExecuteWarCry(CombatUnit caster, AbilitySO ability, Vector2Int targetGridPos, GridManager grid)
        {
            Debug.Log($"[AbilityExecutor] {caster.UnitName} roars with War Cry!");

            // Spec: 3x3 shockwave that pushes adjacent enemies back 1-2 tiles and deals 1d4 + STR damage
            int damage = ability.RollDamage(GetCasterAttributeBonus(caster));
            AbilityVfx.PlayWarCry(caster);

            // Collect first: pushing units moves them between tiles while we iterate
            List<CombatUnit> targets = new List<CombatUnit>();
            List<GridTile> adjacent = grid.GetTilesInRadius(caster.GridPosition, radius: 1);
            foreach (var tile in adjacent)
            {
                if (tile.IsOccupied && tile.OccupyingUnit != null && tile.OccupyingUnit != caster && IsHostileTo(caster, tile.OccupyingUnit))
                {
                    targets.Add(tile.OccupyingUnit);
                }
            }

            foreach (CombatUnit target in targets)
            {
                Vector2Int pushDir = target.GridPosition - caster.GridPosition;
                Vector3 pushedFrom = target.transform.position;
                for (int step = 0; step < WarCryMaxPushTiles; step++)
                {
                    GridTile pushTile = grid.GetTileAt(target.GridPosition + pushDir);
                    if (pushTile == null || !pushTile.IsWalkable || pushTile.IsOccupied) break;
                    target.MoveToTile(pushTile);
                }
                Debug.Log($"[AbilityExecutor] War Cry knocks {target.UnitName} back to {target.GridPosition}!");
                AbilityVfx.PlayPushSlide(target, pushedFrom);

                target.TakeDamage(damage);
            }

            return true;
        }

        private bool ExecuteIronWill(CombatUnit caster, AbilitySO ability)
        {
            // Iron Will: Restores 30% of maximum HP (+ Rank 2 potency) and removes all debuffs
            int healAmount = Mathf.Max(1, Mathf.RoundToInt(caster.MaxHP * 0.30f)) + Mathf.Max(0, ability.BaseValue);
            Debug.Log($"[AbilityExecutor] {caster.UnitName} activates Iron Will! Restoring {healAmount} HP and shaking off debuffs.");
            caster.Heal(healAmount);

            StatusEffectController effects = caster.StatusEffects;
            if (effects != null)
            {
                effects.RemoveEffect(StatusEffectType.Poison);
                effects.RemoveEffect(StatusEffectType.Frostbite);
                effects.RemoveEffect(StatusEffectType.Blind);
            }
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

        private bool ExecuteBlink(CombatUnit caster, AbilitySO ability, Vector2Int targetGridPos, GridManager grid)
        {
            GridTile targetTile = grid.GetTileAt(targetGridPos);
            if (targetTile == null || !targetTile.IsWalkable || targetTile.IsOccupied)
            {
                Debug.Log($"[AbilityExecutor] Blink failed: Tile at {targetGridPos} is obstructed or invalid.");
                return false;
            }

            // Spec: Blink carries the mage up to 7 tiles
            int maxRange = ability.Range > 0 ? ability.Range : BlinkMaxRange;
            int distance = grid.GetDistance(caster.GridPosition, targetGridPos);
            if (distance > maxRange)
            {
                Debug.Log($"[AbilityExecutor] Blink distance ({distance}) exceeds maximum range {maxRange}.");
                return false;
            }

            Debug.Log($"[AbilityExecutor] {caster.UnitName} blinks to {targetGridPos}!");
            Vector3 blinkedFrom = caster.transform.position;
            caster.MoveToTile(targetTile);
            AbilityVfx.PlayBlink(caster, blinkedFrom);
            return true;
        }

        private bool ExecuteArea3x3Ability(CombatUnit caster, AbilitySO ability, Vector2Int targetGridPos, GridManager grid)
        {
            Debug.Log($"[AbilityExecutor] {caster.UnitName} casts {ability.AbilityName} on 3x3 area centered at {targetGridPos}!");

            int bonus = GetCasterAttributeBonus(caster);

            // One damage roll for the whole blast (e.g. Fireball 2d6); a natural 20 doubles it for that target
            int damage = ability.RollDamage(bonus);

            if (ability.AbilityID.IndexOf("fireball", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                AbilityVfx.PlayFireball(caster, grid.GetWorldPosition(targetGridPos));
            }

            List<CombatUnit> targets = CollectHostilesInArea3x3(caster, targetGridPos, grid);
            foreach (CombatUnit target in targets)
            {
                if (ability.RequiresCheck)
                {
                    DiceResult hitCheck = DiceSystem.RollD20(bonus, target.ArmorClass);
                    Debug.Log($"[AbilityExecutor] {ability.AbilityName} vs {target.UnitName}: {hitCheck}");

                    if (hitCheck.isSuccess)
                    {
                        target.TakeDamage(hitCheck.isCriticalSuccess ? damage * 2 : damage, hitCheck.isCriticalSuccess);
                        ApplyAbilityStatusEffect(target, ability);
                    }
                }
                else
                {
                    target.TakeDamage(damage);
                    ApplyAbilityStatusEffect(target, ability);
                }
            }

            return true;
        }

        /// <summary>
        /// Living hostile units standing in the 3x3 square around <paramref name="center"/>. Reads unit positions
        /// as well as tile occupancy, because a tile's OccupyingUnit can lag behind a unit that just moved.
        /// </summary>
        private static List<CombatUnit> CollectHostilesInArea3x3(CombatUnit caster, Vector2Int center, GridManager grid)
        {
            List<CombatUnit> targets = new List<CombatUnit>();

            foreach (GridTile tile in grid.GetArea3x3(center))
            {
                CombatUnit unit = tile.OccupyingUnit;
                if (unit != null && unit != caster && unit.IsAlive && IsHostileTo(caster, unit) && !targets.Contains(unit))
                {
                    targets.Add(unit);
                }
            }

            if (TurnManager.Instance != null)
            {
                foreach (CombatUnit unit in TurnManager.Instance.ActiveUnits)
                {
                    if (unit == null || unit == caster || !unit.IsAlive || !IsHostileTo(caster, unit) || targets.Contains(unit)) continue;
                    if (grid.GetDistance(center, unit.GridPosition) <= 1)
                    {
                        targets.Add(unit);
                    }
                }
            }

            return targets;
        }

        #endregion

        #region Rogue Abilities

        private bool ExecuteBackstab(CombatUnit caster, AbilitySO ability, Vector2Int targetGridPos, GridManager grid)
        {
            GridTile targetTile = grid.GetTileAt(targetGridPos);
            CombatUnit target = targetTile != null ? targetTile.OccupyingUnit : null;
            if (target == null && TurnManager.Instance != null)
            {
                foreach (var unit in TurnManager.Instance.ActiveUnits)
                {
                    if (unit != null && unit.IsAlive && unit.GridPosition == targetGridPos)
                    {
                        target = unit;
                        if (targetTile != null)
                        {
                            target.EnsureTilePosition(); // unit re-registers its own tile
                        }
                        break;
                    }
                }
            }

            if (target == null)
            {
                Debug.Log("[AbilityExecutor] Backstab requires a living target unit at the selected position.");
                return false;
            }
            int bonus = GetCasterAttributeBonus(caster);

            // Backstab (spec): attack roll with Advantage for 2d6 + AGI damage, doubled when the target is
            // blinded or the rogue struck out of a Shadow Step. A natural 20 doubles it again.
            bool fromShadowStep = caster.StatusEffects != null && caster.StatusEffects.HasEffect(StatusEffectType.AdvantageNextAttack);
            bool targetBlinded = target.StatusEffects != null && target.StatusEffects.HasEffect(StatusEffectType.Blind);

            DiceResult hitCheck = DiceSystem.RollD20(bonus, target.ArmorClass, AdvantageType.Advantage);
            caster.StatusEffects?.ConsumeAdvantageNextAttack();
            Debug.Log($"[AbilityExecutor] {caster.UnitName} executes Backstab on {target.UnitName}: {hitCheck}");

            if (hitCheck.isSuccess)
            {
                int weaponBonus = caster is PlayerUnit player ? player.WeaponDamageBonus + player.ConsumePoisonCoating() : 0;
                int damage = ability.RollDamage(bonus, weaponBonus);

                if (fromShadowStep || targetBlinded)
                {
                    damage *= 2;
                }

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
                    tile.OccupyingUnit.StatusEffects?.ApplyEffect(StatusEffectType.Blind, durationTurns: Mathf.Max(1, ability.EffectDurationTurns));
                }
            }

            return true;
        }

        /// <summary>
        /// Executes the Rogue's Shadow Step ability:
        /// Teleports the Rogue up to 3 tiles to an unoccupied walkable tile without triggering opportunity attacks,
        /// and applies a 1-turn AdvantageNextAttack status effect.
        /// </summary>
        public bool ExecuteShadowStep(CombatUnit caster, Vector2Int targetGridPos, GridManager grid)
        {
            if (caster == null || grid == null) return false;

            GridTile targetTile = grid.GetTileAt(targetGridPos);
            if (targetTile == null || !targetTile.IsWalkable || targetTile.IsOccupied)
            {
                Debug.Log($"[AbilityExecutor] Shadow Step failed: Tile at {targetGridPos} is obstructed or invalid.");
                return false;
            }

            int distance = grid.GetDistance(caster.GridPosition, targetGridPos);
            if (distance > 3)
            {
                Debug.Log($"[AbilityExecutor] Shadow Step distance ({distance}) exceeds maximum range 3.");
                return false;
            }

            Debug.Log($"[AbilityExecutor] {caster.UnitName} melts into shadows and emerges at {targetGridPos}! (Advantage applied to next attack)");
            Vector3 steppedFrom = caster.transform.position;
            caster.MoveToTile(targetTile);
            AbilityVfx.PlayShadowStep(caster, steppedFrom);
            caster.StatusEffects?.ApplyEffect(StatusEffectType.AdvantageNextAttack, durationTurns: 1);
            return true;
        }

        #endregion

        #region Standard Helpers

        private bool ExecuteSingleTargetAbility(CombatUnit caster, AbilitySO ability, Vector2Int targetGridPos, GridManager grid)
        {
            GridTile targetTile = grid.GetTileAt(targetGridPos);
            CombatUnit target = targetTile != null ? targetTile.OccupyingUnit : null;
            if (target == null && TurnManager.Instance != null)
            {
                foreach (var unit in TurnManager.Instance.ActiveUnits)
                {
                    if (unit != null && unit.IsAlive && unit.GridPosition == targetGridPos)
                    {
                        target = unit;
                        if (targetTile != null)
                        {
                            target.EnsureTilePosition(); // unit re-registers its own tile
                        }
                        break;
                    }
                }
            }

            if (target == null)
            {
                Debug.Log($"[AbilityExecutor] Ability {ability.AbilityName} requires a living target unit at {targetGridPos}.");
                return false;
            }
            int bonus = GetCasterAttributeBonus(caster);
            AdvantageType advantage = caster.StatusEffects != null ? caster.StatusEffects.GetAttackRollAdvantageModifier() : AdvantageType.None;

            if (ability.RequiresCheck)
            {
                DiceResult hitCheck = DiceSystem.RollD20(bonus, target.ArmorClass, advantage);
                caster.StatusEffects?.ConsumeAdvantageNextAttack();
                Debug.Log($"[AbilityExecutor] {caster.UnitName} casts {ability.AbilityName} on {target.UnitName}: {hitCheck}");

                if (ability.AppliedEffect == StatusEffectType.Frostbite)
                {
                    AbilityVfx.PlayFrostbite(caster, target, hitCheck.isSuccess);
                }

                if (hitCheck.isSuccess)
                {
                    int weaponBonus = caster is PlayerUnit player ? player.WeaponDamageBonus + player.ConsumePoisonCoating() : 0;
                    int damage = ability.RollDamage(bonus, weaponBonus);

                    if (hitCheck.isCriticalSuccess)
                    {
                        damage *= 2;
                    }

                    target.TakeDamage(damage, hitCheck.isCriticalSuccess);
                    ApplyAbilityStatusEffect(target, ability);

                    // Sword Slash (spec): half of the damage also cleaves an enemy next to the warrior
                    if (ability.AbilityID.IndexOf("sword_slash", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        CleaveAdjacentEnemy(caster, target, damage / 2, grid);
                    }
                }
                else
                {
                    Debug.Log($"[AbilityExecutor] {ability.AbilityName} missed {target.UnitName}!");
                }
            }
            else
            {
                // Direct heal or guaranteed damage
                if (ability.DealsDamage)
                {
                    target.TakeDamage(ability.RollDamage(bonus));
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

        /// <summary>
        /// Deals cleave damage to one other hostile unit standing next to the caster.
        /// </summary>
        private void CleaveAdjacentEnemy(CombatUnit caster, CombatUnit primaryTarget, int cleaveDamage, GridManager grid)
        {
            if (cleaveDamage <= 0) return;

            List<GridTile> adjacent = grid.GetTilesInRadius(caster.GridPosition, radius: 1);
            foreach (var tile in adjacent)
            {
                CombatUnit other = tile.OccupyingUnit;
                if (other == null || other == caster || other == primaryTarget || !other.IsAlive || !IsHostileTo(caster, other)) continue;

                Debug.Log($"[AbilityExecutor] {caster.UnitName}'s swing cleaves into {other.UnitName} for {cleaveDamage} damage!");
                other.TakeDamage(cleaveDamage);
                return;
            }
        }

        private static bool IsHostileTo(CombatUnit caster, CombatUnit other)
        {
            return (caster is EnemyUnit) != (other is EnemyUnit);
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
