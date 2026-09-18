using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Base class for all hostile combatants (Skeletons, Zombies, Minions, and Wing Bosses).
    /// Implements automated tactical grid AI: searches for the closest player hero,
    /// advances along the path within attack range, and rolls D20 hit checks vs Player AC.
    /// </summary>
    public class EnemyUnit : CombatUnit
    {
        #region Serialized Fields

        [Header("Enemy Attack Profile")]
        [Tooltip("Base damage dealt on a normal hit.")]
        [SerializeField] private int attackDamage = 4;

        [Tooltip("Attack bonus added to D20 roll (+1 to +5).")]
        [SerializeField] private int attackBonus = 2;

        [Tooltip("Maximum attack reach in grid tiles (1 for melee, 2+ for ranged).")]
        [SerializeField] private int attackRange = 1;

        [Tooltip("Optional AbilitySO defining advanced attacks or boss skills.")]
        [SerializeField] private AbilitySO specialAbility;

        #endregion

        #region Public Properties

        /// <summary>Base damage dealt on successful attack.</summary>
        public int AttackDamage => attackDamage;

        /// <summary>Hit check modifier added to D20.</summary>
        public int AttackBonus => attackBonus;

        /// <summary>Attack distance in grid tiles.</summary>
        public int AttackRange => attackRange;

        /// <summary>Assigned special ability or boss skill.</summary>
        public AbilitySO SpecialAbility => specialAbility;

        #endregion

        #region Tactical AI Routine

        /// <summary>
        /// Executes the enemy's automated tactical turn:
        /// 1. Scans battlefield for closest living PlayerUnit.
        /// 2. If out of range, traverses grid towards the target using BFS pathfinding.
        /// 3. If within attack range, executes a D20 attack roll against the target's Armor Class.
        /// </summary>
        public virtual void ExecuteTurnAction(GridManager gridManager, AbilityExecutor abilityExecutor = null)
        {
            if (!IsAlive) return;

            if (gridManager == null)
            {
                gridManager = GridManager.Instance;
            }

            if (gridManager == null)
            {
                Debug.LogWarning($"[EnemyUnit] {unitName} cannot act: GridManager not found.");
                return;
            }

            // 1. Find closest living PlayerUnit
            PlayerUnit target = FindClosestPlayer(gridManager);
            if (target == null || !target.IsAlive)
            {
                Debug.Log($"[EnemyUnit] {unitName} found no valid player target.");
                return;
            }

            int distance = gridManager.GetDistance(gridPosition, target.GridPosition);

            // 2. If not within attack range, move along path
            if (distance > attackRange)
            {
                MoveTowardsTarget(target.GridPosition, gridManager);
                distance = gridManager.GetDistance(gridPosition, target.GridPosition);
            }

            // 3. If now in range, execute attack
            if (distance <= attackRange)
            {
                PerformAttack(target, abilityExecutor);
            }
        }

        /// <summary>
        /// Finds the nearest living player hero on the grid.
        /// </summary>
        protected virtual PlayerUnit FindClosestPlayer(GridManager gridManager)
        {
            PlayerUnit[] players = FindObjectsByType<PlayerUnit>(FindObjectsSortMode.None);
            PlayerUnit closest = null;
            int minDistance = int.MaxValue;

            foreach (var player in players)
            {
                if (player != null && player.IsAlive)
                {
                    int dist = gridManager.GetDistance(gridPosition, player.GridPosition);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closest = player;
                    }
                }
            }

            return closest;
        }

        /// <summary>
        /// Moves as far along the shortest path toward the target as movement range allows.
        /// </summary>
        protected virtual void MoveTowardsTarget(Vector2Int targetPos, GridManager gridManager)
        {
            List<GridTile> path = gridManager.FindPath(gridPosition, targetPos);
            if (path == null || path.Count == 0) return;

            // Travel up to MovementRange steps, stopping short of the actual occupied player tile
            int steps = Mathf.Min(MovementRange, path.Count - 1);

            // Find the furthest unoccupied tile along the path
            GridTile destinationTile = null;
            for (int i = steps - 1; i >= 0; i--)
            {
                if (!path[i].IsOccupied)
                {
                    destinationTile = path[i];
                    break;
                }
            }

            if (destinationTile != null && destinationTile != currentTile)
            {
                Debug.Log($"[EnemyUnit] {unitName} moved from {gridPosition} to {destinationTile.GridPosition}.");
                MoveToTile(destinationTile);
            }
        }

        /// <summary>
        /// Performs an attack against the target player hero using D20 mechanics.
        /// </summary>
        protected virtual void PerformAttack(PlayerUnit target, AbilityExecutor abilityExecutor)
        {
            if (target == null || !target.IsAlive) return;

            // Check if special ability is configured
            if (specialAbility != null && abilityExecutor != null)
            {
                abilityExecutor.ExecuteAbility(this, specialAbility, target.GridPosition);
                return;
            }

            // Standard basic attack
            AdvantageType advantage = StatusEffects != null ? StatusEffects.GetAttackRollAdvantageModifier() : AdvantageType.None;
            DiceResult hitCheck = DiceSystem.RollD20(attackBonus, target.ArmorClass, advantage);

            Debug.Log($"[EnemyUnit] {unitName} attacks {target.UnitName}: {hitCheck}");

            if (hitCheck.isSuccess)
            {
                // Double damage on Natural 20
                int finalDamage = hitCheck.isCriticalSuccess ? attackDamage * 2 : attackDamage;
                target.TakeDamage(finalDamage, hitCheck.isCriticalSuccess);
            }
            else
            {
                Debug.Log($"[EnemyUnit] {unitName}'s attack missed {target.UnitName}!");
            }
        }

        #endregion
    }
}
