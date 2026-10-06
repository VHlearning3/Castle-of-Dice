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
    /// Baldur's tip about the rusted greaves ("CommanderArmorWeakened"): -2 AC for the first 2 combat rounds.
    /// </summary>
    public class CursedCommanderBoss : EnemyUnit, IReinforcementSummoner
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


        #endregion

        #region Private State

        private bool hasSpawnedAdds = false;
        private int roundsTracked = 0;
        // Baldur's tip about the rusted greaves: -2 AC for the whole fight (Vili, 2026-10-06)
        private bool armorWeakened = false;
        private readonly List<EnemyUnit> summonedSkeletons = new List<EnemyUnit>();

        // The Courtyard's own skeleton guard: hidden when the fight starts, called in at 50% HP (spec)
        private readonly List<EnemyUnit> reserveGuards = new List<EnemyUnit>();

        #endregion

        #region Public Properties

        // STR, DEX, CON, INT, WIS, CHA: a veteran in heavy armour, strong and stubborn
        private static readonly int[] CommanderSaves = { 4, 0, 3, 0, 2, 1 };

        /// <summary>The Commander's saving throws.</summary>
        protected override int[] SaveProfile => CommanderSaves;

        /// <summary>Whether skeleton reinforcements have already been summoned.</summary>
        public bool HasSpawnedAdds => hasSpawnedAdds;

        /// <summary>Whether Baldur's tip weakens the Commander's armour in this fight.</summary>
        public bool IsArmorWeakened => armorWeakened;

        /// <summary>
        /// Effective Armor Class: 2 lower for the whole fight when the hero asked Baldur about the castle defences.
        /// </summary>
        public override int ArmorClass
        {
            get
            {
                int baseAc = base.ArmorClass;
                return armorWeakened ? Mathf.Max(10, baseAc - 2) : baseAc;
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
            maxHP = 54; // critical review B3: a little more health
            currentHP = maxHP;
            armorClass = 16; // High base AC with Heavy Shield
            attackDamage = 6;
            attackBonus = 4;
            movementRange = 2;
            ConfigureDamageDice(1, 8, 2); // average 6.5 on dice instead of a flat 6
            ConfigureInitiative(1);

            // A retry after the hero falls starts the fight over: no leftover summons, reinforcements again at 50%
            hasSpawnedAdds = false;
            roundsTracked = 0;
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
            // The tip is a saved story flag and is not used up, so it also holds on a retry or after a reload
            if (dialogue != null && (dialogue.HasCombatDebuff(dialogueDebuffTag) || dialogue.HasCombatDebuff("SoldiersHonor")))
            {
                armorWeakened = true;
                Debug.Log("[CursedCommander] Baldur's tip: the Commander's rusted greaves weaken his armour (-2 AC) for the whole fight.");
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

            roundsTracked++;
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

            // The warrior's honour duel (critical review C8): the Commander keeps his word and fights alone
            if (StoryFlags.Has(BossChoices.HonorDuelFlag))
            {
                Debug.Log("[CursedCommander] True to the duel, the Commander calls no one.");
                return;
            }

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
                    if (!CallInReserveGuard(tile))
                    {
                        SpawnSingleSkeleton(tile);
                    }
                    spawnedCount++;
                    if (spawnedCount >= toSpawn) break;
                }
            }

            OnReinforcementsSummoned?.Invoke(this);
        }

        #region Reserve Guards

        /// <summary>The Commander holds back every non-boss ally in his room until he is down to half health.</summary>
        public bool HoldsBackAtStart(EnemyUnit ally)
        {
            return ally != null && ally != this && !(ally is CursedCommanderBoss) && skeletonCount > 0;
        }

        /// <summary>Stores a hidden room ally to call in at 50% HP.</summary>
        public void AddReserve(EnemyUnit ally)
        {
            if (ally != null && !reserveGuards.Contains(ally)) reserveGuards.Add(ally);
        }

        /// <summary>Hidden allies still waiting to be called in.</summary>
        public int ReserveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < reserveGuards.Count; i++)
                {
                    if (reserveGuards[i] != null && !reserveGuards[i].gameObject.activeSelf) count++;
                }
                return count;
            }
        }

        private bool CallInReserveGuard(GridTile tile)
        {
            for (int i = 0; i < reserveGuards.Count; i++)
            {
                EnemyUnit guard = reserveGuards[i];
                if (guard == null || guard.gameObject.activeSelf) continue;

                guard.gameObject.SetActive(true);
                if (!guard.IsAlive) guard.Revive();
                guard.MoveToTile(tile);
                guard.FaceTowards(transform.position + transform.forward);
                TurnManager.Instance?.AddCombatant(guard);
                Debug.Log($"[CursedCommander] {guard.UnitName} answers the Commander's call!");
                return true;
            }
            return false;
        }

        #endregion

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
