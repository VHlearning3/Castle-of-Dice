using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Temporary combat body for a village NPC the hero picked a fight with from dialogue.
    /// Added to the NPC when the brawl starts and removed once it is over, so the villager is never
    /// swept into other encounters. The fight happens on the spot where the NPC stands.
    /// A beaten NPC is knocked out rather than killed: they go down, get back up where they stood,
    /// and their dialogue and shop keep working.
    /// </summary>
    public class NpcBrawlerUnit : EnemyUnit
    {
        #region Constants

        /// <summary>Seconds a beaten NPC stays down before getting back up.</summary>
        public const float RecoveryDelay = 2.5f;

        private const int BrawlGridSize = 12;
        private const float BrawlTileSize = 1.6f;

        #endregion

        #region Private State

        private VillageNPC npc;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private bool isKnockedOut;

        private GridManager grid;
        private Vector3 gridHomePosition;
        private bool ownsGrid;
        private GridManager previousGridInstance;

        #endregion

        #region Public Properties

        /// <summary>The brawl currently in progress (or waiting on the Defeat modal), if any.</summary>
        public static NpcBrawlerUnit Active { get; private set; }

        /// <summary>The villager this combat body belongs to.</summary>
        public VillageNPC Npc => npc;

        /// <summary>Whether the NPC lost and is lying on the ground.</summary>
        public bool IsKnockedOut => isKnockedOut;

        /// <summary>Where the NPC stood when the fight was picked; they return here afterwards.</summary>
        public Vector3 HomePosition => homePosition;

        #endregion

        #region Brawl Lifecycle

        /// <summary>
        /// Turns <paramref name="npc"/> into a hostile combatant and starts turn-based combat against the hero
        /// on a 12x12 grid around the two of them. Returns null if a fight is already running.
        /// </summary>
        public static NpcBrawlerUnit Begin(VillageNPC npc, PlayerUnit player)
        {
            if (npc == null || player == null || !player.IsAlive) return null;
            if (Active != null) return null;
            if (TurnManager.Instance != null && TurnManager.Instance.IsCombatActive) return null;

            NpcBrawlerUnit unit = npc.GetComponent<NpcBrawlerUnit>();
            if (unit == null)
            {
                unit = npc.gameObject.AddComponent<NpcBrawlerUnit>();
            }
            if (npc.GetComponent<StatusEffectController>() == null)
            {
                npc.gameObject.AddComponent<StatusEffectController>();
            }

            unit.Configure(npc);
            npc.IsInteractable = false;
            Active = unit;
            TurnManager.OnCombatEnded += unit.HandleCombatEnded;

            Debug.Log($"[NpcBrawlerUnit] {player.UnitName} picked a fight with {unit.UnitName}!");
            unit.StartBrawl(player);
            return unit;
        }

        /// <summary>
        /// Copies the NPC's brawl stats onto this unit and remembers where they stand.
        /// </summary>
        public void Configure(VillageNPC owner)
        {
            npc = owner;
            unitName = string.IsNullOrWhiteSpace(owner.NpcName) ? "Villager" : owner.NpcName;
            maxHP = Mathf.Max(1, owner.BrawlMaxHP);
            currentHP = maxHP;
            armorClass = owner.BrawlArmorClass;
            attackBonus = owner.BrawlAttackBonus;
            attackDamage = Mathf.Max(1, owner.BrawlDamage);
            attackRange = 1;
            movementRange = 3;
            isDead = false;
            isKnockedOut = false;

            homePosition = transform.position;
            homeRotation = transform.rotation;
        }

        private void StartBrawl(PlayerUnit player)
        {
            // A restart after a lost fight keeps the grid it already borrowed (and its home position)
            if (grid == null)
            {
                grid = ResolveGrid();
            }
            if (grid != null)
            {
                Vector3 center = (player.transform.position + homePosition) * 0.5f;
                center.y = homePosition.y;
                grid.GenerateGridAt(center, BrawlGridSize, BrawlGridSize, BrawlTileSize);

                PlaceOnGrid(player);
                PlaceOnGrid(this);
            }

            GameManager.Instance?.SetState(GamePlayMode.Combat);
            CombatUIController.Instance?.EnsureActiveAndReady(true);
            MusicManager.Instance?.PlayCombatMusicForBoss("", GameLocation.Village.ToString());

            TurnManager turnManager = TurnManager.EnsureInstance();
            if (turnManager != null)
            {
                turnManager.StartCombat(new List<CombatUnit> { player, this });
            }

            // Square up: both fighters and the camera face each other
            FaceTowards(player.transform.position);
            player.FaceTowards(transform.position);
            CameraFollow cameraFollow = FindAnyObjectByType<CameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.FaceDirection(transform.position - player.transform.position);
            }
        }

        /// <summary>
        /// Restarts the brawl after the hero lost and chose Try Again: the NPC is back on their feet at full health.
        /// </summary>
        public void Restart(PlayerUnit player)
        {
            if (player == null) return;

            StopAllCoroutines();
            ClearTile();
            isDead = false;
            isKnockedOut = false;
            currentHP = maxHP;
            if (StatusEffects != null) StatusEffects.ClearAllEffects();
            transform.SetPositionAndRotation(homePosition, homeRotation);
            NotifyHealthChanged();

            StartBrawl(player);
        }

        /// <summary>
        /// Ends the brawl: the NPC gets back up at full health where they stood, becomes talkable again
        /// and loses its temporary combat components.
        /// </summary>
        public void Recover()
        {
            StopAllCoroutines();
            TurnManager.OnCombatEnded -= HandleCombatEnded;
            if (Active == this) Active = null;

            ClearTile();
            ReleaseGrid();

            isDead = false;
            isKnockedOut = false;
            currentHP = maxHP;
            transform.SetPositionAndRotation(homePosition, homeRotation);

            if (npc != null)
            {
                npc.IsInteractable = true;
                Debug.Log($"[NpcBrawlerUnit] {unitName} dusts themselves off and gets back up.");
            }

            // StatusEffectController requires a CombatUnit, so it has to go first
            StatusEffectController effects = GetComponent<StatusEffectController>();
            if (effects != null) DestroyImmediate(effects);

            if (Application.isPlaying)
            {
                Destroy(this);
            }
            else
            {
                DestroyImmediate(this);
            }
        }

        private void HandleCombatEnded(bool isVictory)
        {
            if (Active != this) return;

            // Defeat waits for the Defeat modal: Try Again calls Restart, Return to Village calls Recover
            if (!isVictory) return;

            // Clear the grid now; the NPC stays down for a moment before getting back up
            ClearTile();
            ReleaseGrid();
            if (Application.isPlaying && isActiveAndEnabled)
            {
                StartCoroutine(RecoverAfterDelay());
            }
        }

        private IEnumerator RecoverAfterDelay()
        {
            yield return new WaitForSeconds(RecoveryDelay);
            Recover();
        }

        private void OnDestroy()
        {
            TurnManager.OnCombatEnded -= HandleCombatEnded;
            if (Active == this) Active = null;
        }

        #endregion

        #region Knock-Out

        /// <summary>
        /// A beaten villager is knocked out, not removed: they fall over where they stand.
        /// </summary>
        protected override void HideOnDeath()
        {
            isKnockedOut = true;
            transform.rotation = Quaternion.AngleAxis(-90f, transform.right) * transform.rotation;
            Debug.Log($"[NpcBrawlerUnit] {unitName} is knocked out!");
        }

        #endregion

        #region Grid Placement

        /// <summary>
        /// Stands the NPC on the tile with its feet on the floor. The base placement lifts units by half their
        /// collider height, which floats NPCs whose pivot is already at their feet.
        /// </summary>
        public override void MoveToTile(GridTile tile)
        {
            base.MoveToTile(tile);
            if (tile == null) return;

            Collider col = GetComponent<Collider>();
            if (col == null) return;

            float floorY = GridManager.Instance != null
                ? GridManager.Instance.GetWorldPosition(tile.GridPosition).y
                : tile.transform.position.y;
            float pivotAboveFeet = transform.position.y - col.bounds.min.y;
            Vector3 pos = transform.position;
            pos.y = floorY + pivotAboveFeet;
            transform.position = pos;
            Physics.SyncTransforms();
        }

        private void PlaceOnGrid(CombatUnit unit)
        {
            GridTile tile = grid.GetTileAt(grid.GetGridPosition(unit.transform.position));
            if (tile == null || !tile.IsWalkable || (tile.IsOccupied && tile.OccupyingUnit != unit))
            {
                tile = grid.FindClosestWalkableTile(unit.transform.position);
            }
            if (tile != null)
            {
                unit.MoveToTile(tile);
            }
        }

        /// <summary>
        /// Borrows the scene's combat grid (the village scene shares one with the cellar) or creates a
        /// temporary one when none is active.
        /// </summary>
        private GridManager ResolveGrid()
        {
            GridManager existing = GridManager.Instance;
            if (existing == null)
            {
                existing = FindAnyObjectByType<GridManager>();
            }

            if (existing != null && existing.enabled && existing.gameObject.activeInHierarchy)
            {
                GridManager.Instance = existing;
                gridHomePosition = existing.transform.position;
                ownsGrid = false;
                return existing;
            }

            // An inactive registered grid would make the new one destroy itself in Awake
            previousGridInstance = GridManager.Instance;
            GridManager.Instance = null;

            GameObject gridObject = new GameObject("VillageBrawlGrid");
            GridManager created = gridObject.AddComponent<GridManager>();
            GridManager.Instance = created;
            ownsGrid = true;
            return created;
        }

        private void ReleaseGrid()
        {
            if (grid == null) return;

            grid.ClearAllHighlights();
            grid.ClearGrid();

            if (ownsGrid)
            {
                if (GridManager.Instance == grid) GridManager.Instance = previousGridInstance;
                if (Application.isPlaying) Destroy(grid.gameObject);
                else DestroyImmediate(grid.gameObject);
            }
            else
            {
                // Hand the shared grid back where it was so the cellar encounter finds it untouched
                grid.transform.position = gridHomePosition;
            }

            grid = null;
            ownsGrid = false;
            previousGridInstance = null;
        }

        #endregion
    }
}
