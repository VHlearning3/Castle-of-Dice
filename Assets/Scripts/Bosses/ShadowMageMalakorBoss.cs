using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Dialogue;

namespace CastleOfTheD20.Bosses
{
    /// <summary>
    /// Library & Arcane Wing Boss: Shadow Mage Malakor.
    /// Teleports across the grid when struck by attacks.
    /// Spawns illusionary decoy duplicates (Mirror Images).
    /// Pre-combat hook: If the player passed the DC 14 Arcane Heresy check, the true Malakor is revealed immediately.
    /// </summary>
    public class ShadowMageMalakorBoss : EnemyUnit
    {
        #region Serialized Fields

        [Header("Illusion & Teleportation")]
        [Tooltip("Prefab instantiated for decoy illusions.")]
        [SerializeField] private GameObject illusionPrefab;

        [Tooltip("Number of illusion decoys summoned.")]
        [SerializeField] private int decoyCount = 2;

        [Header("Dialogue Hook")]
        [Tooltip("Tag identifying successful Arcane Heresy dialogue check.")]
        [SerializeField] private string arcaneHeresyTag = "ArcaneHeresy";

        #endregion

        #region Private State

        private bool isRealMalakorRevealed = false;
        private bool hasSpawnedIllusions = false;
        private readonly List<EnemyUnit> activeDecoys = new List<EnemyUnit>();

        #endregion

        #region Public Properties

        /// <summary>Whether the genuine Malakor is revealed through arcane insight.</summary>
        public bool IsRealMalakorRevealed => isRealMalakorRevealed;

        /// <summary>Active mirror image decoys on the grid.</summary>
        public IReadOnlyList<EnemyUnit> ActiveDecoys => activeDecoys;

        #endregion

        #region Events

        /// <summary>Fired when Malakor teleports: (boss, fromPos, toPos).</summary>
        public static event Action<ShadowMageMalakorBoss, Vector2Int, Vector2Int> OnBossTeleported;

        /// <summary>Fired when mirror illusions are conjured.</summary>
        public static event Action<ShadowMageMalakorBoss, int> OnIllusionsSpawned;

        #endregion

        #region Initialization

        public override void InitializeUnit()
        {
            unitName = "Shadow Mage Malakor";
            maxHP = 40;
            currentHP = maxHP;
            armorClass = 13;
            attackDamage = 8;
            attackBonus = 5;
            movementRange = 3;

            base.InitializeUnit();

            CheckArcaneHeresyDebuff();
        }

        private void CheckArcaneHeresyDebuff()
        {
            DialogueController dialogue = DialogueController.Instance;
            if (dialogue != null && (dialogue.HasCombatDebuff(arcaneHeresyTag) || dialogue.HasCombatDebuff("MalakorRevealed")))
            {
                isRealMalakorRevealed = true;
                dialogue.ConsumeCombatDebuff(arcaneHeresyTag);
                dialogue.ConsumeCombatDebuff("MalakorRevealed");

                unitName = "[True] Shadow Mage Malakor";
                Debug.Log("[ShadowMageMalakor] Arcane Heresy check succeeded! Malakor's true form is pierced through his glamour!");
            }
        }

        #endregion

        #region Combat Lifecycle

        public override void TakeDamage(int amount, bool isCritical = false)
        {
            base.TakeDamage(amount, isCritical);

            if (IsAlive)
            {
                // First hit spawns illusions if not yet created
                if (!hasSpawnedIllusions)
                {
                    SpawnIllusionDecoys();
                }

                // Teleport to a safe grid coordinate upon sustaining damage
                TeleportToRandomTile();
            }
        }

        #endregion

        #region Teleportation

        /// <summary>
        /// Teleports Malakor to a random unoccupied walkable tile on the grid.
        /// </summary>
        public void TeleportToRandomTile()
        {
            GridManager grid = GridManager.Instance;
            if (grid == null) return;

            Vector2Int previousPos = gridPosition;
            List<GridTile> candidateTiles = new List<GridTile>();

            foreach (var kvp in grid.Tiles)
            {
                GridTile tile = kvp.Value;
                if (tile != null && tile.IsWalkable && !tile.IsOccupied)
                {
                    int dist = grid.GetDistance(previousPos, tile.GridPosition);
                    if (dist >= 3 && dist <= 7)
                    {
                        candidateTiles.Add(tile);
                    }
                }
            }

            if (candidateTiles.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, candidateTiles.Count);
                GridTile chosenTile = candidateTiles[randomIndex];

                MoveToTile(chosenTile);
                Debug.Log($"[ShadowMageMalakor] Malakor slips through shadows! Teleported from {previousPos} to {chosenTile.GridPosition}.");
                OnBossTeleported?.Invoke(this, previousPos, chosenTile.GridPosition);
            }
        }

        #endregion

        #region Illusion Decoys

        /// <summary>
        /// Spawns mirror illusion clones across the battlefield.
        /// </summary>
        public void SpawnIllusionDecoys()
        {
            hasSpawnedIllusions = true;
            GridManager grid = GridManager.Instance;
            if (grid == null) return;

            Debug.Log("[ShadowMageMalakor] \"Which of us is real, mortal?\" Malakor casts Mirror Image!");

            int spawned = 0;
            foreach (var kvp in grid.Tiles)
            {
                GridTile tile = kvp.Value;
                if (tile != null && tile.IsWalkable && !tile.IsOccupied && tile.GridPosition != gridPosition)
                {
                    int dist = grid.GetDistance(gridPosition, tile.GridPosition);
                    if (dist >= 2 && dist <= 5)
                    {
                        CreateDecoyUnit(tile);
                        spawned++;
                        if (spawned >= decoyCount) break;
                    }
                }
            }

            OnIllusionsSpawned?.Invoke(this, spawned);
        }

        private void CreateDecoyUnit(GridTile tile)
        {
            GameObject decoyObj;
            if (illusionPrefab != null)
            {
                decoyObj = Instantiate(illusionPrefab, tile.transform.position, Quaternion.identity);
            }
            else
            {
                decoyObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                decoyObj.transform.position = tile.transform.position;
            }

            decoyObj.name = isRealMalakorRevealed ? "[Decoy] Shadow Illusion" : "Shadow Mage Malakor";

            EnemyUnit decoyUnit = decoyObj.GetComponent<EnemyUnit>();
            if (decoyUnit == null)
            {
                decoyUnit = decoyObj.AddComponent<EnemyUnit>();
            }

            decoyUnit.InitializeUnit();
            decoyUnit.MoveToTile(tile);
            activeDecoys.Add(decoyUnit);

            TurnManager turnMgr = TurnManager.Instance;
            if (turnMgr != null && turnMgr.IsCombatActive)
            {
                List<CombatUnit> roster = new List<CombatUnit>(turnMgr.ActiveUnits) { decoyUnit };
                turnMgr.StartCombat(roster);
            }
        }

        #endregion
    }
}
