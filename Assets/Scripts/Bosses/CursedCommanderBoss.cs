using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Dialogue;

namespace CastleOfTheD20.Bosses
{
    /// <summary>
    /// Courtyard & Watchtower Boss: The Cursed Commander.
    /// Wields a heavy shield giving high baseline Armor Class.
    /// Spawns 2 Skeleton adds when health drops to 50% or below.
    /// Susceptible to pre-combat dialogue check ("Soldier's Honor"): -2 AC for the first 2 combat rounds.
    /// </summary>
    public class CursedCommanderBoss : EnemyUnit
    {
        #region Serialized Fields

        [Header("Boss Profile")]
        [Tooltip("Prefab instantiated when skeleton reinforcements are summoned at 50% HP.")]
        [SerializeField] private GameObject skeletonAddPrefab;

        [Header("Dialogue Debuff Hook")]
        [Tooltip("Dialogue debuff tag checked at encounter start.")]
        [SerializeField] private string dialogueDebuffTag = "CommanderArmorWeakened";

        [Tooltip("Number of combat rounds the AC debuff lasts.")]
        [SerializeField] private int debuffDurationRounds = 2;

        #endregion

        #region Private State

        private bool hasSpawnedAdds = false;
        private int remainingDebuffRounds = 0;
        private int roundsTracked = 0;

        #endregion

        #region Public Properties

        /// <summary>Whether skeleton reinforcements have already been summoned.</summary>
        public bool HasSpawnedAdds => hasSpawnedAdds;

        /// <summary>Remaining turns of the dialogue AC penalty.</summary>
        public int RemainingDebuffRounds => remainingDebuffRounds;

        /// <summary>
        /// Effective Armor Class. If the pre-combat dialogue check was passed,
        /// AC is reduced by 2 for the first 2 rounds.
        /// </summary>
        public override int ArmorClass
        {
            get
            {
                int baseAc = base.ArmorClass;
                return remainingDebuffRounds > 0 ? Mathf.Max(10, baseAc - 2) : baseAc;
            }
        }

        #endregion

        #region Events

        /// <summary>Fired when skeleton reinforcements are summoned.</summary>
        public static event Action<CursedCommanderBoss> OnReinforcementsSummoned;

        #endregion

        #region Initialization

        public override void InitializeUnit()
        {
            unitName = "Cursed Commander";
            maxHP = 50;
            currentHP = maxHP;
            armorClass = 16; // High base AC with Heavy Shield
            attackDamage = 6;
            attackBonus = 4;
            movementRange = 2;

            base.InitializeUnit();

            CheckDialogueDebuff();
        }

        private void CheckDialogueDebuff()
        {
            DialogueController dialogue = DialogueController.Instance;
            if (dialogue != null && (dialogue.HasCombatDebuff(dialogueDebuffTag) || dialogue.HasCombatDebuff("SoldiersHonor")))
            {
                remainingDebuffRounds = debuffDurationRounds;
                dialogue.ConsumeCombatDebuff(dialogueDebuffTag);
                dialogue.ConsumeCombatDebuff("SoldiersHonor");
                Debug.Log($"[CursedCommander] Soldier's Honor check succeeded! Commander's armor is weakened (-2 AC) for {remainingDebuffRounds} rounds.");
            }
        }

        #endregion

        #region Combat Lifecycle

        public override void TakeDamage(int amount, bool isCritical = false)
        {
            base.TakeDamage(amount, isCritical);

            // Trigger skeleton reinforcements at 50% max HP
            if (IsAlive && !hasSpawnedAdds && currentHP <= (maxHP / 2))
            {
                SpawnSkeletonReinforcements();
            }
        }

        public override void ExecuteTurnAction(GridManager gridManager, AbilityExecutor abilityExecutor = null)
        {
            base.ExecuteTurnAction(gridManager, abilityExecutor);

            // Decrement dialogue AC debuff after each boss action round
            roundsTracked++;
            if (remainingDebuffRounds > 0)
            {
                remainingDebuffRounds--;
                if (remainingDebuffRounds == 0)
                {
                    Debug.Log("[CursedCommander] The Commander recovered his stance. AC weakness has worn off.");
                }
            }
        }

        #endregion

        #region Reinforcements

        /// <summary>
        /// Spawns 2 Skeleton minions on adjacent grid cells.
        /// </summary>
        public void SpawnSkeletonReinforcements()
        {
            hasSpawnedAdds = true;
            Debug.Log("[CursedCommander] \"Arise, guardians of the gate!\" The Commander summons 2 Skeleton adds!");

            GridManager grid = GridManager.Instance;
            if (grid == null) return;

            List<GridTile> adjacentTiles = grid.GetTilesInRadius(gridPosition, radius: 2);
            int spawnedCount = 0;

            foreach (var tile in adjacentTiles)
            {
                if (tile != null && tile.IsWalkable && !tile.IsOccupied && tile.GridPosition != gridPosition)
                {
                    SpawnSingleSkeleton(tile);
                    spawnedCount++;
                    if (spawnedCount >= 2) break;
                }
            }

            OnReinforcementsSummoned?.Invoke(this);
        }

        private void SpawnSingleSkeleton(GridTile tile)
        {
            GameObject addObj;
            if (skeletonAddPrefab != null)
            {
                addObj = Instantiate(skeletonAddPrefab, tile.transform.position, Quaternion.identity);
            }
            else
            {
                // Fallback procedural creation
                addObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                addObj.name = "Armored Skeleton Guard";
                addObj.transform.position = tile.transform.position;
            }

            EnemyUnit skeleton = addObj.GetComponent<EnemyUnit>();
            if (skeleton == null)
            {
                skeleton = addObj.AddComponent<EnemyUnit>();
            }

            skeleton.InitializeUnit();
            skeleton.MoveToTile(tile);

            // Register with TurnManager if combat is actively running
            TurnManager turnMgr = TurnManager.Instance;
            if (turnMgr != null && turnMgr.IsCombatActive)
            {
                List<CombatUnit> currentRoster = new List<CombatUnit>(turnMgr.ActiveUnits) { skeleton };
                turnMgr.StartCombat(currentRoster);
            }
        }

        #endregion
    }
}
