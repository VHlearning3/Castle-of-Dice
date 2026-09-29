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
        [SerializeField] protected int attackDamage = 4;

        [Tooltip("Attack bonus added to D20 roll (+1 to +5).")]
        [SerializeField] protected int attackBonus = 2;

        [Tooltip("Maximum attack reach in grid tiles (1 for melee, 2+ for ranged).")]
        [SerializeField] protected int attackRange = 1;

        [Tooltip("Optional AbilitySO defining advanced attacks or boss skills.")]
        [SerializeField] protected AbilitySO specialAbility;

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

        #region Runtime Configuration

        /// <summary>
        /// Applies a combat profile to a unit created at runtime (boss summons), which would otherwise
        /// fight as a default "Combatant".
        /// </summary>
        public void ConfigureStats(string displayName, int hp, int ac, int damage, int bonus)
        {
            unitName = displayName;
            maxHP = Mathf.Max(1, hp);
            currentHP = maxHP;
            armorClass = ac;
            attackDamage = damage;
            attackBonus = bonus;
        }

        /// <summary>Changes the name shown for this unit in combat (e.g. disguising an illusion).</summary>
        public void SetDisplayName(string displayName)
        {
            unitName = displayName;
        }

        #endregion

        #region Tactical AI Routine

        /// <summary>
        /// Executes the enemy's automated tactical turn:
        /// 1. Scans battlefield for closest living PlayerUnit.
        /// 2. If out of range, walks along the grid towards the target using BFS pathfinding.
        /// 3. Once the walk finishes, if within attack range, executes a D20 attack roll against the target's Armor Class.
        /// In Play Mode the attack waits for the walk (<see cref="CombatUnit.IsWalking"/>); callers wait on that too.
        /// </summary>
        public virtual void ExecuteTurnAction(GridManager gridManager, AbilityExecutor abilityExecutor = null)
        {
            if (!IsAlive)
            {
                OnTurnActionFinished();
                return;
            }

            if (gridManager == null)
            {
                gridManager = GridManager.Instance;
            }

            if (gridManager == null)
            {
                Debug.LogWarning($"[EnemyUnit] {unitName} cannot act: GridManager not found.");
                OnTurnActionFinished();
                return;
            }

            // 1. Find closest living PlayerUnit
            PlayerUnit target = FindClosestPlayer(gridManager);
            if (target == null || !target.IsAlive)
            {
                Debug.Log($"[EnemyUnit] {unitName} found no valid player target.");
                OnTurnActionFinished();
                return;
            }

            int distance = gridManager.GetDistance(gridPosition, target.GridPosition);

            // 2. If not within attack range, walk along path
            if (distance > attackRange)
            {
                MoveTowardsTarget(target.GridPosition, gridManager);
            }

            // 3. Attack once the unit has arrived (immediately when it did not walk)
            WhenWalkFinished(() => FinishTurnAction(target, gridManager, abilityExecutor));
        }

        private void FinishTurnAction(PlayerUnit target, GridManager gridManager, AbilityExecutor abilityExecutor)
        {
            if (IsAlive && target != null && target.IsAlive)
            {
                FaceTowards(target.transform.position);

                if (gridManager.GetDistance(gridPosition, target.GridPosition) <= attackRange)
                {
                    PerformAttack(target, abilityExecutor);
                }
            }

            OnTurnActionFinished();
        }

        /// <summary>
        /// Runs at the end of this enemy's turn action, after any walk and attack have resolved.
        /// Bosses tick their per-round countdowns here.
        /// </summary>
        protected virtual void OnTurnActionFinished()
        {
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
                Debug.Log($"[EnemyUnit] {unitName} walks from {gridPosition} to {destinationTile.GridPosition}.");
                WalkToTile(destinationTile);
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
            PlayAttackAnimation();
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

                // Shield Wall (spec): the defender strikes back only when the attack misses
                target.ResolveCounterAttack(this);
            }
        }

        #endregion

        #region Death

        private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");
        private static readonly int DieStateHash = Animator.StringToHash("Die");

        /// <summary>Rigged enemies swing their attack clip as the D20 attack roll resolves.</summary>
        protected void PlayAttackAnimation()
        {
            Animator animator = UnitAnimator;
            if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            {
                animator.SetTrigger(AttackTriggerHash);
            }
        }

        [Header("Death")]
        [Tooltip("Seconds the death clip plays before a rigged enemy is removed from the battlefield.")]
        [SerializeField] private float deathClipDuration = 2.2f;

        [Tooltip("Drops collectible gold (and maybe a potion) on death. Off for illusions and village brawlers.")]
        [SerializeField] private bool dropsLoot = true;

        /// <summary>Whether this enemy drops coin / potion pickups when it dies.</summary>
        public bool DropsLoot
        {
            get => dropsLoot;
            set => dropsLoot = value;
        }

        /// <summary>Raised when a loot-dropping enemy dies in play (World.LootDrops spawns the pickups).</summary>
        public static event Action<EnemyUnit> OnLootDropped;

        public override void Die()
        {
            bool wasAlive = !isDead;
            base.Die();
            if (wasAlive && dropsLoot && Application.isPlaying) OnLootDropped?.Invoke(this);
        }

        /// <summary>
        /// Rigged enemies with a Die clip stay visible while it plays; everyone else vanishes at once.
        /// </summary>
        protected override void HideOnDeath()
        {
            Animator animator = UnitAnimator;
            bool hasDeathClip = animator != null && animator.isActiveAndEnabled
                && animator.runtimeAnimatorController != null && animator.HasState(0, DieStateHash);

            if (!hasDeathClip || !isActiveAndEnabled || !Application.isPlaying)
            {
                base.HideOnDeath();
                return;
            }

            // The corpse must not block clicks or grid raycasts while it falls
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            StartCoroutine(HideAfterDeathClip());
        }

        private System.Collections.IEnumerator HideAfterDeathClip()
        {
            yield return new WaitForSeconds(deathClipDuration);
            base.HideOnDeath();
        }

        #endregion
    }
}
