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
    /// A real caster (critical review B5): he fires a 4-tile shadow bolt (2d4+3) from a distance and steps away
    /// from a hero who closes in. When hit he teleports away, but at most every other turn.
    /// Spawns one illusionary decoy (Mirror Image) that also fires weaker shadow bolts.
    /// Pre-combat hook: If the player passed the DC 14 Arcane Heresy check, the true Malakor is revealed immediately.
    /// </summary>
    public class ShadowMageMalakorBoss : EnemyUnit
    {
        #region Serialized Fields

        [Header("Illusion & Teleportation")]
        [Tooltip("Prefab instantiated for decoy illusions.")]
        [SerializeField] private GameObject illusionPrefab;

        [Tooltip("Number of illusion decoys summoned (reduced to 1 for solo hero pacing).")]
        [SerializeField] private int decoyCount = 1;

        [Header("Dialogue Hook")]
        [Tooltip("Tag identifying successful Arcane Heresy dialogue check.")]
        [SerializeField] private string arcaneHeresyTag = "ArcaneHeresy";

        #endregion

        #region Private State

        private bool isRealMalakorRevealed = false;
        private bool hasSpawnedIllusions = false;

        // Turns before the next teleport-on-hit (he can blink away at most every other turn)
        private int teleportCooldown;

        /// <summary>Range of Malakor's and his mirror image's shadow bolt.</summary>
        public const int ShadowBoltRange = 4;
        private readonly List<EnemyUnit> activeDecoys = new List<EnemyUnit>();
        private readonly List<EnemyUnit> conjuredDecoys = new List<EnemyUnit>();

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
            maxHP = 44;
            currentHP = maxHP;
            armorClass = 13;
            attackDamage = 8;
            attackBonus = 5;
            movementRange = 3;
            ConfigureDamageDice(2, 4, 3); // shadow bolt, average 8 like the old flat hit
            ConfigureAttackRange(ShadowBoltRange);
            ConfigureInitiative(2);

            // A retry after the hero falls starts the fight over: the first hit casts Mirror Image again
            hasSpawnedIllusions = false;
            teleportCooldown = 0;
            activeDecoys.Clear();

            base.InitializeUnit();

            CheckArcaneHeresyDebuff();
            AdoptStandingDecoys();

            // Illusions conjured in an earlier attempt vanish; decoys placed in the room are revived with it
            for (int i = 0; i < conjuredDecoys.Count; i++)
            {
                if (conjuredDecoys[i] == null) continue;
                conjuredDecoys[i].gameObject.SetActive(false); // leaves this frame's combat roll-call at once
                if (Application.isPlaying) Destroy(conjuredDecoys[i].gameObject);
                else DestroyImmediate(conjuredDecoys[i].gameObject);
            }
            conjuredDecoys.Clear();
        }

        /// <summary>Name an illusion shows: Malakor's own unless Arcane Heresy exposed the trick.</summary>
        private string DecoyDisplayName => isRealMalakorRevealed ? "[Decoy] Shadow Illusion" : "Shadow Mage Malakor";

        /// <summary>
        /// A decoy placed in the Library with the boss is his mirror image: it wears Malakor's name
        /// (so only Arcane Heresy tells them apart) and counts towards the one-clone cap.
        /// </summary>
        private void AdoptStandingDecoys()
        {
            // Inactive ones too: the room may wake the decoy after the boss, or revive it after him on a retry
            EnemyUnit[] enemies = FindObjectsByType<EnemyUnit>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyUnit unit = enemies[i];
                if (unit == null || unit == this || unit is ShadowMageMalakorBoss || conjuredDecoys.Contains(unit)) continue;
                if (unit.name.IndexOf("Decoy", StringComparison.OrdinalIgnoreCase) < 0) continue;

                unit.SetDisplayName(DecoyDisplayName);
                ArmDecoy(unit);
                if (!activeDecoys.Contains(unit)) activeDecoys.Add(unit);
            }
        }

        private int CountLivingDecoys()
        {
            int count = 0;
            for (int i = 0; i < activeDecoys.Count; i++)
            {
                if (activeDecoys[i] != null && activeDecoys[i].IsAlive) count++;
            }
            return count;
        }

        private void CheckArcaneHeresyDebuff()
        {
            DialogueController dialogue = DialogueController.Instance;
            if (dialogue != null && (dialogue.HasCombatDebuff(arcaneHeresyTag) || dialogue.HasCombatDebuff("MalakorRevealed")))
            {
                isRealMalakorRevealed = true;
                dialogue.ConsumeCombatDebuff(arcaneHeresyTag);
                dialogue.ConsumeCombatDebuff("MalakorRevealed");
            }

            // Once pierced, the glamour stays pierced on a retry
            if (isRealMalakorRevealed)
            {
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

                // Teleport to a safe grid coordinate upon sustaining damage, at most every other turn
                if (teleportCooldown <= 0)
                {
                    TeleportToRandomTile();
                    teleportCooldown = 2;
                }
            }
        }

        protected override void OnTurnActionFinished()
        {
            base.OnTurnActionFinished();
            if (teleportCooldown > 0) teleportCooldown--;
        }

        /// <summary>Turns before Malakor can teleport away from a hit again (0 = he will).</summary>
        public int TeleportCooldown => teleportCooldown;

        /// <summary>The mirror image fights too: a weaker 4-tile shadow bolt.</summary>
        private static void ArmDecoy(EnemyUnit decoy)
        {
            if (decoy == null) return;
            decoy.ConfigureAttackRange(ShadowBoltRange);
            decoy.ConfigureDamageDice(1, 6, 0);
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

                Vector3 previousWorldPos = transform.position;
                MoveToTile(chosenTile);
                AbilityVfx.PlayMalakorTeleport(this, previousWorldPos);
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

            // Only one mirror image at a time (solo hero pacing); a standing decoy already fills the slot
            int toSpawn = decoyCount - CountLivingDecoys();
            if (toSpawn <= 0) return;

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
                        if (spawned >= toSpawn) break;
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

            if (illusionPrefab == null)
            {
                // Same profile as the Library's standing Shadow Decoy (ZoneSceneBuilder)
                decoyUnit.ConfigureStats(DecoyDisplayName, hp: 18, ac: 12, damage: 4, bonus: 2);
            }
            else
            {
                decoyUnit.SetDisplayName(DecoyDisplayName);
            }

            decoyUnit.DropsLoot = false; // illusions leave no gold behind
            ArmDecoy(decoyUnit);
            decoyUnit.InitializeUnit();
            decoyUnit.MoveToTile(tile);
            activeDecoys.Add(decoyUnit);
            conjuredDecoys.Add(decoyUnit);

            TurnManager.Instance?.AddCombatant(decoyUnit);
        }

        #endregion
    }
}
