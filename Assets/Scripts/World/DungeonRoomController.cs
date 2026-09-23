using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Lifecycle states of a dungeon room or boss chamber.
    /// </summary>
    public enum RoomState
    {
        /// <summary>Room has not been engaged yet.</summary>
        Unexplored,

        /// <summary>Hostiles are active; room barriers locked.</summary>
        CombatActive,

        /// <summary>Hostiles vanquished; exits unlocked and secrets revealed.</summary>
        Cleared
    }

    /// <summary>
    /// DungeonRoomController acts as an Encounter Manager that isolates a specific room or area.
    /// Handles triggering combat when the player enters, locking exit barriers, activating hostile units,
    /// and rewarding the player with loot/passages when the encounter is resolved.
    /// </summary>
    [SelectionBase]
    [RequireComponent(typeof(BoxCollider))]
    public class DungeonRoomController : MonoBehaviour
    {
        #region Inspector Variables

        [Header("Room Identity")]
        [Tooltip("The location or area name (e.g., 'Village', 'Courtyard', 'Library', 'Cellar').")]
        public string roomLocation = "Village";

        [Tooltip("Leave empty for normal encounters; specifies boss ID for major chambers (e.g., 'CursedCommander', 'ShadowMageMalakor', 'GargoyleKing').")]
        public string bossIdentifier = "";

        [Header("Encounter Boundaries & Barriers")]
        [Tooltip("Exit doors or iron gates enabled during combat to lock the player in.")]
        public List<GameObject> exitBarriers = new List<GameObject>();

        [Header("Encounter Hostiles")]
        [Tooltip("Enemies stationed in this room, each containing an EnemyUnit script. Inactive by default.")]
        public List<GameObject> roomEnemies = new List<GameObject>();

        [Header("Encounter Rewards & Secrets")]
        [Tooltip("Reward chest or hidden passageway revealed upon defeating all enemies. Inactive by default.")]
        public GameObject secretPassageOrChest;

        [Header("Tactical Grid Generation")]
        [Tooltip("If true, automatically generates a combat grid in this room when the encounter begins.")]
        public bool generateGridOnCombat = true;

        [Tooltip("Optional custom grid center offset relative to this room or cellar root.")]
        public Vector3 gridCenterOffset = Vector3.zero;

        [Tooltip("Grid column count.")]
        public int gridWidth = 8;

        [Tooltip("Grid row count.")]
        public int gridHeight = 8;

        [Tooltip("Size of each grid tile in world units.")]
        public float gridTileSize = 2.0f;

        [Tooltip("If true, clears the generated grid tiles once the encounter is cleared.")]
        public bool clearGridOnCombatResolved = true;

        #endregion

        #region Private State

        private RoomState currentState = RoomState.Unexplored;
        private BoxCollider triggerCollider;
        private bool isEncounterTriggered = false;

        #endregion

        #region Public Properties & Events

        /// <summary>Current lifecycle state of this room.</summary>
        public RoomState CurrentState => currentState;

        /// <summary>Whether all hostiles in this room have been defeated and rewards unlocked.</summary>
        public bool IsCleared => currentState == RoomState.Cleared;

        /// <summary>Fired when room combat begins: (roomController).</summary>
        public static event Action<DungeonRoomController> OnRoomCombatStarted;

        /// <summary>Fired when room is cleared of all hostiles: (roomController).</summary>
        public static event Action<DungeonRoomController> OnRoomCleared;

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            // Auto-configure the required BoxCollider as a trigger in the Editor
            BoxCollider col = GetComponent<BoxCollider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void Awake()
        {
            triggerCollider = GetComponent<BoxCollider>();
            if (triggerCollider != null && !triggerCollider.isTrigger)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void Start()
        {
            // 1. Ensure exit barriers are unlocked (disabled) initially
            SetBarriersLocked(false);

            // 2. Ensure room enemies are inactive by default
            if (roomEnemies != null)
            {
                foreach (var enemy in roomEnemies)
                {
                    if (enemy != null)
                    {
                        enemy.SetActive(false);
                    }
                }
            }

            // 3. Ensure rewards/secrets are hidden by default
            if (secretPassageOrChest != null)
            {
                secretPassageOrChest.SetActive(false);
            }

            // 4. Hook into TurnManager combat end events to auto-resolve upon victory
            TurnManager.OnCombatEnded += HandleCombatEnded;
        }

        private void OnDestroy()
        {
            TurnManager.OnCombatEnded -= HandleCombatEnded;
        }

        #endregion

        #region Trigger Logic

        /// <summary>
        /// Detects when the player crosses the room boundary trigger.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (isEncounterTriggered || currentState != RoomState.Unexplored) return;

            // Check if the entering object is tagged "Player"
            if (other.CompareTag("Player") || other.GetComponent<PlayerUnit>() != null)
            {
                // Disable the trigger collider immediately so this event only fires once
                if (triggerCollider != null)
                {
                    triggerCollider.enabled = false;
                }
                else
                {
                    Collider anyCol = GetComponent<Collider>();
                    if (anyCol != null) anyCol.enabled = false;
                }

                TriggerEncounter();
            }
        }

        #endregion

        #region Encounter Execution

        /// <summary>
        /// Initiates the room encounter: locks exit barriers, spawns/activates enemies,
        /// sets game mode to Combat, and launches turn-based combat.
        /// </summary>
        public void TriggerEncounter()
        {
            if (isEncounterTriggered || currentState == RoomState.Cleared) return;

            isEncounterTriggered = true;
            currentState = RoomState.CombatActive;
            Debug.Log($"[DungeonRoomController] Encounter triggered in '{roomLocation}'! Locking chamber doors.");

            // 1. Locate or generate combat grid
            GridManager grid = GetComponentInChildren<GridManager>()
                ?? transform.parent?.GetComponentInChildren<GridManager>()
                ?? GridManager.Instance;

            if (generateGridOnCombat && grid != null)
            {
                // Detect exact floor elevation dynamically via downward raycast from room center
                Vector3 rayOrigin = transform.position + Vector3.up * 2.0f;
                float floorY = transform.position.y;
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20.0f))
                {
                    floorY = hit.point.y + 0.05f;
                }
                else if (transform.parent != null)
                {
                    floorY = transform.parent.position.y + 0.05f;
                }

                Vector3 center = new Vector3(transform.position.x, floorY, transform.position.z) + gridCenterOffset;
                grid.GenerateGridAt(center, gridWidth, gridHeight, gridTileSize);
                Debug.Log($"[DungeonRoomController] Generated {gridWidth}x{gridHeight} combat grid centered at {center} for '{roomLocation}'.");
            }

            // 2. Enable all GameObjects in the exitBarriers list (locking the doors)
            SetBarriersLocked(true);

            // 3. Enable all GameObjects in the roomEnemies list and gather combatants
            List<CombatUnit> activeParticipants = new List<CombatUnit>();

            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            if (player != null && player.IsAlive)
            {
                activeParticipants.Add(player);

                // Snap player to nearest valid walkable grid tile
                if (grid != null)
                {
                    Vector2Int playerTilePos = grid.GetGridPosition(player.transform.position);
                    GridTile pTile = grid.GetTileAt(playerTilePos);
                    if (pTile == null || !pTile.IsWalkable || pTile.IsOccupied)
                    {
                        pTile = FindClosestWalkableTile(grid, player.transform.position);
                    }
                    if (pTile != null)
                    {
                        player.MoveToTile(pTile);
                        Debug.Log($"[DungeonRoomController] Snapped player to tile {pTile.GridPosition}.");
                    }
                }
            }

            if (roomEnemies != null)
            {
                foreach (var enemyObj in roomEnemies)
                {
                    if (enemyObj != null)
                    {
                        enemyObj.SetActive(true);

                        EnemyUnit enemyUnit = enemyObj.GetComponent<EnemyUnit>();
                        if (enemyUnit != null && enemyUnit.IsAlive)
                        {
                            activeParticipants.Add(enemyUnit);

                            // Snap enemy to its nearest newly generated grid tile
                            if (grid != null)
                            {
                                Vector2Int enemyTilePos = grid.GetGridPosition(enemyUnit.transform.position);
                                GridTile tile = grid.GetTileAt(enemyTilePos);
                                if (tile == null || !tile.IsWalkable || tile.IsOccupied)
                                {
                                    tile = FindClosestWalkableTile(grid, enemyUnit.transform.position);
                                }
                                if (tile != null)
                                {
                                    enemyUnit.MoveToTile(tile);
                                }
                            }
                        }
                    }
                }
            }

            // 3. Change the game state to Combat via GameManager
            if (GameManager.Instance != null)
            {
                if (Enum.TryParse<GameLocation>(roomLocation, true, out GameLocation parsedLocation))
                {
                    GameManager.Instance.SetLocation(parsedLocation);
                }
                GameManager.Instance.SetState(GamePlayMode.Combat);
            }

            // 4. Initialize the turn-based combat via TurnManager
            if (TurnManager.Instance != null)
            {
                if (activeParticipants.Count > 0)
                {
                    TurnManager.Instance.StartCombat(activeParticipants);
                }
                else
                {
                    TurnManager.Instance.StartCombat();
                }
            }

            OnRoomCombatStarted?.Invoke(this);
        }

        /// <summary>
        /// Public method called when all room enemies are defeated.
        /// Unlocks exit barriers, reveals reward chest/passage, and restores exploration mode.
        /// </summary>
        public void OnCombatResolved()
        {
            if (currentState == RoomState.Cleared) return;

            currentState = RoomState.Cleared;
            Debug.Log($"[DungeonRoomController] Encounter resolved in '{roomLocation}'! Unlocking exits and revealing rewards.");

            // 1. Disable all exitBarriers (unlocking the doors)
            SetBarriersLocked(false);

            // 2. Enable the secretPassageOrChest to reward the player
            if (secretPassageOrChest != null)
            {
                secretPassageOrChest.SetActive(true);
                Debug.Log($"[DungeonRoomController] Secret passage or reward chest revealed in '{roomLocation}'.");
            }

            // 3. Revert the game state back to Exploration
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GamePlayMode.Exploration);

                if (Enum.TryParse<GameLocation>(roomLocation, true, out GameLocation parsedLocation))
                {
                    GameManager.Instance.NotifyRoomCleared(parsedLocation);
                }

                if (!string.IsNullOrEmpty(bossIdentifier))
                {
                    GameManager.Instance.NotifyBossDefeated(bossIdentifier);
                }
            }

            // 4. Clear the combat grid once exploration resumes
            if (clearGridOnCombatResolved && GridManager.Instance != null)
            {
                GridManager.Instance.ClearGrid();
                Debug.Log($"[DungeonRoomController] Cleared combat grid after resolving '{roomLocation}'.");
            }

            OnRoomCleared?.Invoke(this);
        }

        /// <summary>
        /// Legacy alias for OnCombatResolved for backwards compatibility.
        /// </summary>
        public void ClearRoom()
        {
            OnCombatResolved();
        }

        #endregion

        #region Helper Methods

        private void SetBarriersLocked(bool locked)
        {
            if (exitBarriers == null) return;

            foreach (var barrier in exitBarriers)
            {
                if (barrier != null)
                {
                    barrier.SetActive(locked);
                }
            }
        }

        private void HandleCombatEnded(bool isVictory)
        {
            // If combat was active in this room and ended in player victory, resolve the encounter
            if (currentState == RoomState.CombatActive && isVictory)
            {
                OnCombatResolved();
            }
        }

        private GridTile FindClosestWalkableTile(GridManager grid, Vector3 worldPos)
        {
            if (grid == null) return null;
            GridTile best = null;
            float bestDist = float.MaxValue;
            foreach (var kvp in grid.Tiles)
            {
                if (kvp.Value != null && kvp.Value.IsWalkable && !kvp.Value.IsOccupied)
                {
                    float d = Vector3.Distance(worldPos, kvp.Value.transform.position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = kvp.Value;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// Context menu action to snap all child components of the cellar room into exact alignment.
        /// Fixes any detached geometry, trigger, barrier, chest, or ladder.
        /// </summary>
        [ContextMenu("Align Cellar Components")]
        public void AlignCellarComponents()
        {
            Transform root = transform.parent != null ? transform.parent : transform;

            Transform geo = root.Find("Cellar_Geometry");
            if (geo != null) geo.localPosition = Vector3.zero;

            Transform lighting = root.Find("Cellar_Lighting");
            if (lighting != null) lighting.localPosition = Vector3.zero;

            Transform ladder = root.Find("Cellar_Exit_Ladder");
            if (ladder != null) ladder.localPosition = new Vector3(0f, 0f, -8.4f);

            Transform spawn = root.Find("Cellar_PlayerSpawnPoint");
            if (spawn != null) spawn.localPosition = new Vector3(0f, 0.2f, -6.0f);

            Transform barrier = root.Find("Cellar_Exit_Barrier");
            if (barrier != null) barrier.localPosition = new Vector3(0f, 2.0f, -7.2f);

            Transform chest = root.Find("Cellar_Reward_Chest");
            if (chest != null) chest.localPosition = new Vector3(0f, 0f, 7.2f);

            Transform enemies = root.Find("Cellar_Enemies");
            if (enemies != null) enemies.localPosition = Vector3.zero;

            Transform grid = root.Find("CombatGrid");
            if (grid != null) grid.localPosition = new Vector3(0f, 0.05f, 0f);

            transform.localPosition = new Vector3(0f, 2.0f, 0f);
            BoxCollider col = GetComponent<BoxCollider>();
            if (col != null)
            {
                col.center = Vector3.zero;
                col.size = new Vector3(15f, 4.5f, 13f);
            }
            Debug.Log($"[DungeonRoomController] Successfully aligned all components under root '{root.name}'.");
        }

        #endregion
    }
}
