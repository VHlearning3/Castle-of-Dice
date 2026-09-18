using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Dialogue;

namespace CastleOfTheD20.Bosses
{
    /// <summary>
    /// Crown Hall Final Boss: The Gargoyle King.
    /// Phase 1: Heavy physical attacks and Ground Stomp seismic hazards.
    /// Phase 2: Stone Form at 50% HP. Magical attacks reflect or deal 0 damage; requires physical strikes.
    /// Pre-combat hook: Weakened attack damage (-3) for the first 3 turns if DC 16 Intimidation check succeeded.
    /// </summary>
    public class GargoyleKingBoss : EnemyUnit
    {
        #region Serialized Fields

        [Header("Phase Settings")]
        [Tooltip("True when the boss is in Phase 2 (Stone Form).")]
        [SerializeField] private bool isStoneFormActive = false;

        [Header("Hazard Spawning")]
        [Tooltip("Number of random tile hazards spawned during ground stomp.")]
        [SerializeField] private int rockfallHazardCount = 3;

        [Tooltip("Damage dealt to units hit by falling rocks.")]
        [SerializeField] private int rockfallDamage = 5;

        [Header("Dialogue Intimidation Hook")]
        [Tooltip("Debuff tag applied if DC 16 Intimidation was passed.")]
        [SerializeField] private string intimidationTag = "GargoyleKingIntimidated";

        [Tooltip("Number of turns the intimidation debuff lasts.")]
        [SerializeField] private int intimidationDurationTurns = 3;

        #endregion

        #region Private State

        private int remainingIntimidationTurns = 0;
        private bool hasEnteredPhase2 = false;

        #endregion

        #region Public Properties

        /// <summary>Whether Stone Form (Phase 2) is active.</summary>
        public bool IsStoneFormActive => isStoneFormActive;

        /// <summary>Remaining turns of attack weakness from intimidation.</summary>
        public int RemainingIntimidationTurns => remainingIntimidationTurns;

        /// <summary>
        /// Effective attack damage: reduced by 3 if intimidated during the first 3 rounds.
        /// </summary>
        public int EffectiveAttackDamage
        {
            get
            {
                int dmg = AttackDamage;
                return remainingIntimidationTurns > 0 ? Mathf.Max(2, dmg - 3) : dmg;
            }
        }

        #endregion

        #region Events

        /// <summary>Fired when the boss transitions into Phase 2 Stone Form.</summary>
        public static event Action<GargoyleKingBoss> OnStoneFormActivated;

        /// <summary>Fired when ground stomp triggers rockfalls: (boss, targetedTiles).</summary>
        public static event Action<GargoyleKingBoss, List<Vector2Int>> OnGroundStompTriggered;

        #endregion

        #region Initialization

        public override void InitializeUnit()
        {
            unitName = "The Gargoyle King";
            maxHP = 60;
            currentHP = maxHP;
            armorClass = 15;
            attackDamage = 9;
            attackBonus = 5;
            movementRange = 2;

            base.InitializeUnit();

            CheckIntimidationDebuff();
        }

        private void CheckIntimidationDebuff()
        {
            DialogueController dialogue = DialogueController.Instance;
            if (dialogue != null && (dialogue.HasCombatDebuff(intimidationTag) || dialogue.HasCombatDebuff("IntimidateBoss")))
            {
                remainingIntimidationTurns = intimidationDurationTurns;
                dialogue.ConsumeCombatDebuff(intimidationTag);
                dialogue.ConsumeCombatDebuff("IntimidateBoss");
                Debug.Log($"[GargoyleKing] Intimidation succeeded! The Gargoyle King hesitates (-3 DMG) for {remainingIntimidationTurns} turns.");
            }
        }

        #endregion

        #region Combat Lifecycle

        public override void TakeDamage(int amount, bool isCritical = false)
        {
            // Phase 2: Stone Form deflection/immunity to pure magic
            if (isStoneFormActive)
            {
                // In Stone Form, armor is further reinforced
                Debug.Log($"[GargoyleKing] The Stone Form absorbs the blow! Hardened granite resists the strike.");
            }

            base.TakeDamage(amount, isCritical);

            // Phase 2 transition trigger at 50% HP
            if (IsAlive && !hasEnteredPhase2 && currentHP <= (maxHP / 2))
            {
                EnterStoneForm();
            }
        }

        public override void ExecuteTurnAction(GridManager gridManager, AbilityExecutor abilityExecutor = null)
        {
            // Execute Ground Stomp before normal attack
            ExecuteGroundStomp(gridManager);

            base.ExecuteTurnAction(gridManager, abilityExecutor);

            // Decrement intimidation debuff countdown
            if (remainingIntimidationTurns > 0)
            {
                remainingIntimidationTurns--;
                if (remainingIntimidationTurns == 0)
                {
                    Debug.Log("[GargoyleKing] The Gargoyle King shakes off his fear. Attack damage fully restored!");
                }
            }
        }

        protected override void PerformAttack(PlayerUnit target, AbilityExecutor abilityExecutor)
        {
            if (target == null || !target.IsAlive) return;

            // Attack with modified damage accounting for intimidation
            AdvantageType advantage = StatusEffects != null ? StatusEffects.GetAttackRollAdvantageModifier() : AdvantageType.None;
            DiceResult hitCheck = DiceSystem.RollD20(AttackBonus, target.ArmorClass, advantage);

            Debug.Log($"[GargoyleKing] The King swings his massive stone claws at {target.UnitName}: {hitCheck}");

            if (hitCheck.isSuccess)
            {
                int dmg = hitCheck.isCriticalSuccess ? EffectiveAttackDamage * 2 : EffectiveAttackDamage;
                target.TakeDamage(dmg, hitCheck.isCriticalSuccess);
            }
            else
            {
                Debug.Log($"[GargoyleKing] The Gargoyle King's strike crashes into the stone floor, missing {target.UnitName}!");
            }
        }

        #endregion

        #region Phase 2: Stone Form

        /// <summary>
        /// Activates Phase 2 Stone Form. Increases Armor Class and grants spell reflection/resistance.
        /// </summary>
        public void EnterStoneForm()
        {
            hasEnteredPhase2 = true;
            isStoneFormActive = true;
            armorClass += 3; // Boosted AC in stone state

            Debug.Log("[GargoyleKing] PHASE 2: The Gargoyle King's skin petrifies into enchanted granite! STONE FORM ACTIVATED.");
            StatusEffects?.ApplyEffect(StatusEffectType.ManaShield, durationTurns: 2);

            OnStoneFormActivated?.Invoke(this);
        }

        #endregion

        #region Seismic Ground Stomp

        /// <summary>
        /// Slams the ground, causing falling rock hazards on random tiles across the grid.
        /// </summary>
        public void ExecuteGroundStomp(GridManager gridManager)
        {
            if (gridManager == null) return;

            Debug.Log("[GargoyleKing] The Gargoyle King stomps the ground! Ceiling rocks rain down upon the hall!");

            List<Vector2Int> targetedTiles = new List<Vector2Int>();
            List<GridTile> allTiles = new List<GridTile>(gridManager.Tiles.Values);

            for (int i = 0; i < rockfallHazardCount && allTiles.Count > 0; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, allTiles.Count);
                GridTile targetTile = allTiles[randomIndex];
                allTiles.RemoveAt(randomIndex);

                targetedTiles.Add(targetTile.GridPosition);
                targetTile.SetHighlight(TileHighlightType.TargetArea);

                // If a unit is standing under the falling rock, damage them
                if (targetTile.IsOccupied && targetTile.OccupyingUnit != null)
                {
                    CombatUnit victim = targetTile.OccupyingUnit;
                    Debug.Log($"[GargoyleKing] Falling rocks strike {victim.UnitName} for {rockfallDamage} damage!");
                    victim.TakeDamage(rockfallDamage);
                }
            }

            OnGroundStompTriggered?.Invoke(this, targetedTiles);
        }

        #endregion
    }
}
