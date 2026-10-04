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

        [Header("Damage Dice (0 = flat Attack Damage)")]
        [Tooltip("Dice rolled for a hit, e.g. 2 for \"2d6\". When set, a hit deals the dice plus Damage Dice Bonus.")]
        [SerializeField] protected int damageDiceCount = 0;
        [SerializeField] protected int damageDiceSides = 0;
        [SerializeField] protected int damageDiceBonus = 0;

        [Header("Initiative")]
        [Tooltip("Added to this enemy's d20 initiative roll.")]
        [SerializeField] protected int initiativeBonus = 0;

        #endregion

        #region Public Properties

        /// <summary>
        /// Typical damage of a hit: the flat Attack Damage, or the average of the damage dice when the
        /// enemy rolls them (bosses).
        /// </summary>
        public int AttackDamage => HasDamageDice
            ? Mathf.FloorToInt(damageDiceCount * (damageDiceSides + 1) * 0.5f) + damageDiceBonus
            : attackDamage;

        /// <summary>Hit check modifier added to D20 (Hard difficulty adds +2).</summary>
        public int AttackBonus => attackBonus + DifficultySettings.EnemyHitBonus;

        /// <summary>Added to this enemy's initiative roll.</summary>
        public int InitiativeBonus => initiativeBonus;

        /// <summary>Whether hits roll damage dice instead of dealing flat damage.</summary>
        public bool HasDamageDice => damageDiceCount > 0 && damageDiceSides > 0;

        /// <summary>Damage shown on enemy cards, e.g. "2d6+2" or "4".</summary>
        public string DamageFormula
        {
            get
            {
                if (!HasDamageDice) return attackDamage.ToString();
                string dice = damageDiceCount + "d" + damageDiceSides;
                return damageDiceBonus > 0 ? dice + "+" + damageDiceBonus : dice;
            }
        }

        /// <summary>Rolls the damage of one hit (before a critical doubles it).</summary>
        public virtual int RollAttackDamage()
        {
            if (!HasDamageDice) return attackDamage;
            return Mathf.Max(1, DiceSystem.RollDamage(damageDiceCount, damageDiceSides) + damageDiceBonus);
        }

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

        /// <summary>
        /// Makes hits roll <paramref name="count"/>d<paramref name="sides"/> + <paramref name="bonus"/> instead of
        /// flat damage (critical review B3: boss damage on dice).
        /// </summary>
        public void ConfigureDamageDice(int count, int sides, int bonus)
        {
            damageDiceCount = Mathf.Max(0, count);
            damageDiceSides = Mathf.Max(0, sides);
            damageDiceBonus = bonus;
        }

        /// <summary>Sets the enemy's reach in tiles (1 = melee, more = ranged attacks with line of sight).</summary>
        public void ConfigureAttackRange(int tiles)
        {
            attackRange = Mathf.Max(1, tiles);
        }

        /// <summary>Sets the enemy's initiative modifier.</summary>
        public void ConfigureInitiative(int bonus)
        {
            initiativeBonus = bonus;
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

            // 2. If not within attack range (or a ranged attacker has no line of sight), walk along path.
            //    A ranged attacker the hero has closed in on steps back to a firing spot when it can.
            bool cornered = attackRange > 1 && distance <= 1;
            if (distance > attackRange || !CanAttackFrom(gridPosition, target.GridPosition, gridManager) || cornered)
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

                if (gridManager.GetDistance(gridPosition, target.GridPosition) <= attackRange
                    && CanAttackFrom(gridPosition, target.GridPosition, gridManager))
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
        /// Whether an attack can reach from <paramref name="from"/>: melee always, ranged attacks only with a
        /// line of sight that full cover (pillars, bookshelves, walls) does not block.
        /// </summary>
        protected bool CanAttackFrom(Vector2Int from, Vector2Int targetPos, GridManager gridManager)
        {
            if (gridManager == null) return true;
            if (gridManager.GetDistance(from, targetPos) <= 1) return true;
            return gridManager.HasLineOfSight(from, targetPos);
        }

        /// <summary>
        /// Moves as far along the shortest path toward the target as movement range allows. Ranged attackers
        /// stop at the nearest reachable tile that has the target in range and in sight.
        /// </summary>
        protected virtual void MoveTowardsTarget(Vector2Int targetPos, GridManager gridManager)
        {
            if (attackRange > 1)
            {
                GridTile firingSpot = FindFiringPosition(targetPos, gridManager);
                if (firingSpot != null)
                {
                    if (firingSpot != currentTile)
                    {
                        Debug.Log($"[EnemyUnit] {unitName} moves to a firing position {firingSpot.GridPosition}.");
                        WalkToTile(firingSpot);
                    }
                    return;
                }

                // No firing spot this turn: a cornered caster stays and fights at close range
                if (gridManager.GetDistance(gridPosition, targetPos) <= 1) return;
            }

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
        /// The nearest tile this ranged attacker can reach that has the target within range and line of sight.
        /// Null when none is reachable this turn.
        /// </summary>
        protected GridTile FindFiringPosition(Vector2Int targetPos, GridManager gridManager)
        {
            if (gridManager == null) return null;
            int currentDistance = gridManager.GetDistance(gridPosition, targetPos);
            if (currentDistance > 1 && currentDistance <= attackRange && CanAttackFrom(gridPosition, targetPos, gridManager))
            {
                return currentTile;
            }

            List<GridTile> reachable = gridManager.GetReachableTiles(gridPosition, MovementRange);
            GridTile best = null;
            int bestMove = int.MaxValue;
            for (int i = 0; i < reachable.Count; i++)
            {
                GridTile tile = reachable[i];
                int dist = gridManager.GetDistance(tile.GridPosition, targetPos);
                if (dist > attackRange || dist <= 1) continue; // archers keep out of reach
                if (!CanAttackFrom(tile.GridPosition, targetPos, gridManager)) continue;

                int move = gridManager.GetDistance(gridPosition, tile.GridPosition);
                if (move < bestMove)
                {
                    bestMove = move;
                    best = tile;
                }
            }
            return best;
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
            ResolveBasicAttack(target, "attacks");
        }

        /// <summary>
        /// One d20 attack against the hero: AC plus half cover for ranged attacks, damage dice or flat damage,
        /// double on a natural 20, and the Shield Wall counterattack on a miss. Returns the roll.
        /// </summary>
        protected DiceResult ResolveBasicAttack(PlayerUnit target, string verb)
        {
            AdvantageType advantage = StatusEffects != null ? StatusEffects.GetAttackRollAdvantageModifier() : AdvantageType.None;
            GridManager grid = GridManager.Instance;
            int coverBonus = grid != null ? grid.GetCoverArmorBonus(gridPosition, target.GridPosition) : 0;
            DiceResult hitCheck = DiceSystem.RollD20(AttackBonus, target.ArmorClass + coverBonus, advantage);

            Debug.Log($"[EnemyUnit] {unitName} {verb} {target.UnitName}: {hitCheck}");
            if (grid != null && grid.GetDistance(gridPosition, target.GridPosition) > 1)
            {
                AbilityVfx.PlayRangedBolt(this, target, hitCheck.isSuccess);
            }

            if (hitCheck.isSuccess)
            {
                // Double damage on Natural 20
                int damage = RollAttackDamage();
                int finalDamage = hitCheck.isCriticalSuccess ? damage * 2 : damage;
                target.TakeDamage(finalDamage, hitCheck.isCriticalSuccess);
            }
            else
            {
                Debug.Log($"[EnemyUnit] {unitName}'s attack missed {target.UnitName}!");

                // Shield Wall (spec): the defender strikes back only when the attack misses
                if (grid == null || grid.GetDistance(gridPosition, target.GridPosition) <= 1)
                {
                    target.ResolveCounterAttack(this);
                }
            }

            // Retaliation (warrior, level 4): every melee swing at him is answered, hit or miss
            if (IsAlive && target.IsAlive && (grid == null || grid.GetDistance(gridPosition, target.GridPosition) <= 1))
            {
                target.ResolveRetaliation(this);
            }
            return hitCheck;
        }

        /// <summary>
        /// Free attack against a hero who walks out of this enemy's reach (critical review B2).
        /// Blink and Shadow Step teleport, so they never provoke it.
        /// </summary>
        public virtual void MakeOpportunityAttack(PlayerUnit target)
        {
            if (!IsAlive || target == null || !target.IsAlive) return;
            FaceTowards(target.transform.position);
            PlayAttackAnimation();
            ResolveBasicAttack(target, "lashes out at the fleeing");
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
            hideAfterDeathRoutine = StartCoroutine(HideAfterDeathClip());
        }

        private Coroutine hideAfterDeathRoutine;

        /// <summary>
        /// Brings the enemy back for an encounter retry as it stood when the fight first began: full stats
        /// (a boss also leaves its second phase and forgets summons), a clickable collider again, and no
        /// pending death clip that would hide it a moment later.
        /// </summary>
        public override void Revive()
        {
            if (hideAfterDeathRoutine != null)
            {
                StopCoroutine(hideAfterDeathRoutine);
                hideAfterDeathRoutine = null;
            }

            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = true;

            base.Revive();
            InitializeUnit();
            NotifyHealthChanged();
        }

        private System.Collections.IEnumerator HideAfterDeathClip()
        {
            yield return new WaitForSeconds(deathClipDuration);
            hideAfterDeathRoutine = null;
            base.HideOnDeath();
        }

        #endregion
    }
}
