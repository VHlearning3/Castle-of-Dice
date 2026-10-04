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

        [Tooltip("Number of skeleton adds summoned (reduced to 1 for solo hero encounter balance).")]
        [SerializeField] private int skeletonCount = 1;

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
        private bool earnedSoldiersHonor = false;
        private readonly List<EnemyUnit> summonedSkeletons = new List<EnemyUnit>();

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

            // A retry after the hero falls starts the fight over: no leftover summons, reinforcements again at 50%
            hasSpawnedAdds = false;
            roundsTracked = 0;
            remainingDebuffRounds = 0;
            DestroySummonedSkeletons();

            base.InitializeUnit();

            CheckDialogueDebuff();
        }

        private void DestroySummonedSkeletons()
        {
            for (int i = 0; i < summonedSkeletons.Count; i++)
            {
                if (summonedSkeletons[i] == null) continue;
                summonedSkeletons[i].gameObject.SetActive(false); // leaves this frame's combat roll-call at once
                if (Application.isPlaying) Destroy(summonedSkeletons[i].gameObject);
                else DestroyImmediate(summonedSkeletons[i].gameObject);
            }
            summonedSkeletons.Clear();
        }

        private void CheckDialogueDebuff()
        {
            DialogueController dialogue = DialogueController.Instance;
            if (dialogue != null && (dialogue.HasCombatDebuff(dialogueDebuffTag) || dialogue.HasCombatDebuff("SoldiersHonor")))
            {
                earnedSoldiersHonor = true;
                dialogue.ConsumeCombatDebuff(dialogueDebuffTag);
                dialogue.ConsumeCombatDebuff("SoldiersHonor");
            }

            // The hero who won the honor check keeps that edge when retrying the fight
            if (earnedSoldiersHonor)
            {
                remainingDebuffRounds = debuffDurationRounds;
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

        protected override void OnTurnActionFinished()
        {
            base.OnTurnActionFinished();

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
        /// Spawns Skeleton minion on adjacent grid cells (reduced to 1 for solo hero encounter balance).
        /// Boss adds are capped at <see cref="skeletonCount"/> living minions, so a guard already
        /// standing in the room counts towards the cap.
        /// </summary>
        public void SpawnSkeletonReinforcements()
        {
            hasSpawnedAdds = true;

            int toSpawn = skeletonCount - CountLivingAllies();
            if (toSpawn <= 0)
            {
                Debug.Log("[CursedCommander] \"Hold the line!\" The Commander's guard already stands beside him; no reinforcements.");
                return;
            }

            Debug.Log($"[CursedCommander] \"Arise, guardian of the gate!\" The Commander summons {toSpawn} Skeleton add!");

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
                    if (spawnedCount >= toSpawn) break;
                }
            }

            OnReinforcementsSummoned?.Invoke(this);
        }

        private int CountLivingAllies()
        {
            int count = 0;
            TurnManager turnManager = TurnManager.Instance;
            if (turnManager == null) return 0;

            IReadOnlyList<CombatUnit> units = turnManager.ActiveUnits;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] is EnemyUnit ally && ally != this && ally.IsAlive) count++;
            }
            return count;
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

            if (skeletonAddPrefab == null)
            {
                // Same profile as the Courtyard's standing guard (ZoneSceneBuilder)
                skeleton.ConfigureStats("Armored Skeleton Guard", hp: 20, ac: 12, damage: 4, bonus: 2);
            }

            skeleton.InitializeUnit();
            skeleton.MoveToTile(tile);
            summonedSkeletons.Add(skeleton);

            // Register with TurnManager if combat is actively running
            TurnManager.Instance?.AddCombatant(skeleton);
        }

        #endregion
    }
}
