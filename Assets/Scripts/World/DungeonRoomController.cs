using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Dialogue;

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
        /// <summary>Largest grid side length an encounter may request.</summary>
        public const int MaxGridSize = 32;

        /// <summary>Tiles kept between the hero and the grid edge when the grid slides to fit them.</summary>
        private const int PlayerEdgeMarginTiles = 2;

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

        [Header("Boss Dialogue")]
        [Tooltip("Boss whose intro dialogue plays when the hero walks into this trigger; combat starts when it ends. It cannot be talked to before.")]
        public VillageNPC bossDialogueNpc;

        [Tooltip("If true, the dialogue NPC waits where the boss will stand instead of its authored spot.")]
        public bool stageDialogueNpcAtBoss = true;

        [Header("Tactical Grid Generation")]
        [Tooltip("If true, automatically generates a combat grid in this room when the encounter begins.")]
        public bool generateGridOnCombat = true;

        [Tooltip("Optional custom grid center offset relative to this room or cellar root.")]
        public Vector3 gridCenterOffset = Vector3.zero;

        [Tooltip("Grid column count (1-32).")]
        [Range(1, MaxGridSize)]
        public int gridWidth = 12;

        [Tooltip("Grid row count (1-32).")]
        [Range(1, MaxGridSize)]
        public int gridHeight = 12;

        [Tooltip("If true, the grid slides toward the hero so they start on it instead of being teleported, staying inside the grid area.")]
        public bool fitGridAroundPlayer = true;

        [Tooltip("Walkable floor rectangle (world X/Z) the grid must stay inside. Zero size = use this room's trigger box.")]
        public Rect gridAreaXZ = new Rect(0f, 0f, 0f, 0f);

        [Tooltip("Size of each grid tile in world units.")]
        public float gridTileSize = 1.6f;

        [Tooltip("If true, clears the generated grid tiles once the encounter is cleared.")]
        public bool clearGridOnCombatResolved = true;

        #endregion

        #region Private State

        private RoomState currentState = RoomState.Unexplored;
        private BoxCollider triggerCollider;
        private bool isEncounterTriggered = false;
        private bool isAwaitingBossDialogue = false;
        private readonly List<Pose> enemyStartPoses = new List<Pose>();
        private Pose bossDialogueNpcStartPose;

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
            RecordStartPoses();

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

            // 5. Zone scenes reload on every visit: a room cleared earlier (this session or in the save)
            // stays cleared instead of re-running its fight, boss milestone and chest reward.
            if (WasClearedBefore())
            {
                RestoreClearedState();
            }

            // 6. The boss speaks only when the hero walks into this trigger, never before
            PrepareBossDialogueNpc();
        }

        /// <summary>Remembers where the hostiles and the boss's dialogue stand-in wait, for an encounter reset.</summary>
        private void RecordStartPoses()
        {
            enemyStartPoses.Clear();
            if (roomEnemies != null)
            {
                foreach (GameObject enemy in roomEnemies)
                {
                    enemyStartPoses.Add(enemy != null ? new Pose(enemy.transform.position, enemy.transform.rotation) : Pose.identity);
                }
            }

            if (bossDialogueNpc != null)
            {
                bossDialogueNpcStartPose = new Pose(bossDialogueNpc.transform.position, bossDialogueNpc.transform.rotation);
            }
        }

        private void PrepareBossDialogueNpc()
        {
            if (bossDialogueNpc == null) return;

            if (currentState == RoomState.Cleared)
            {
                bossDialogueNpc.gameObject.SetActive(false);
                return;
            }

            bossDialogueNpc.IsInteractable = false;

            if (stageDialogueNpcAtBoss)
            {
                GameObject boss = FindBossObject();
                if (boss != null)
                {
                    Vector3 spot = boss.transform.position;
                    bossDialogueNpc.transform.position = new Vector3(spot.x, bossDialogueNpc.transform.position.y, spot.z);
                    bossDialogueNpc.transform.rotation = boss.transform.rotation;
                }
            }
        }

        /// <summary>The boss among the room's hostiles: the one named "Boss_...", else the last listed.</summary>
        private GameObject FindBossObject()
        {
            if (roomEnemies == null) return null;

            GameObject last = null;
            foreach (GameObject enemy in roomEnemies)
            {
                if (enemy == null) continue;
                if (enemy.name.StartsWith("Boss_", StringComparison.OrdinalIgnoreCase)) return enemy;
                last = enemy;
            }
            return last;
        }

        /// <summary>
        /// True when the campaign already records this room's boss as defeated or its wing as cleared.
        /// </summary>
        private bool WasClearedBefore()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return false;

            if (!string.IsNullOrEmpty(bossIdentifier))
            {
                return gm.IsBossDefeated(bossIdentifier);
            }

            return Enum.TryParse<GameLocation>(roomLocation, true, out GameLocation location) && gm.IsWingCleared(location);
        }

        private void RestoreClearedState()
        {
            currentState = RoomState.Cleared;
            isEncounterTriggered = true;

            if (triggerCollider != null) triggerCollider.enabled = false;

            // The reward chest stays reachable; it remembers on its own whether it was already looted
            if (secretPassageOrChest != null)
            {
                RevealRewardChest();
            }

            Debug.Log($"[DungeonRoomController] '{roomLocation}' was already cleared; encounter skipped.");
        }

        private void OnDestroy()
        {
            TurnManager.OnCombatEnded -= HandleCombatEnded;
            DialogueController.OnDialogueEnded -= HandleBossDialogueEnded;
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

                BeginEncounter(other.GetComponentInParent<PlayerUnit>());
            }
        }

        /// <summary>
        /// Starts this room's encounter for a hero who walked in: the boss's intro dialogue first when the
        /// room has one (combat follows when it ends), otherwise combat right away.
        /// </summary>
        public void BeginEncounter(PlayerUnit player)
        {
            if (isEncounterTriggered || isAwaitingBossDialogue || currentState != RoomState.Unexplored) return;

            if (!TryStartBossDialogue(player))
            {
                TriggerEncounter();
            }
        }

        /// <summary>Whether the boss's intro dialogue is playing and combat waits for it to end.</summary>
        public bool IsAwaitingBossDialogue => isAwaitingBossDialogue;

        private bool TryStartBossDialogue(PlayerUnit player)
        {
            if (bossDialogueNpc == null || bossDialogueNpc.StartingDialogueNode == null) return false;

            DialogueController dialogue = DialogueController.Instance;
            if (dialogue == null) return false;
            if (dialogue.IsInDialogue) dialogue.EndDialogue();

            isAwaitingBossDialogue = true;
            DialogueController.OnDialogueEnded += HandleBossDialogueEnded;
            dialogue.StartDialogue(bossDialogueNpc.StartingDialogueNode, player);

            // A one-line intro may already have ended (and started combat) inside StartDialogue
            if (dialogue.IsInDialogue || isEncounterTriggered) return true;

            DialogueController.OnDialogueEnded -= HandleBossDialogueEnded;
            isAwaitingBossDialogue = false;
            return false;
        }

        private void HandleBossDialogueEnded()
        {
            DialogueController.OnDialogueEnded -= HandleBossDialogueEnded;
            if (!isAwaitingBossDialogue) return;

            isAwaitingBossDialogue = false;
            TriggerEncounter();
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

            // The fighting boss unit takes the place of its dialogue stand-in
            if (bossDialogueNpc != null) bossDialogueNpc.gameObject.SetActive(false);

            // Disable encounter trigger collider so it does not intercept camera raycasts during combat
            if (triggerCollider != null) triggerCollider.enabled = false;
            Collider directCol = GetComponent<Collider>();
            if (directCol != null && directCol.isTrigger) directCol.enabled = false;

            // 1. Locate or generate combat grid
            GridManager grid = GetComponentInChildren<GridManager>()
                ?? transform.parent?.GetComponentInChildren<GridManager>()
                ?? GridManager.Instance;

            if (grid != null)
            {
                GridManager.Instance = grid;
            }

            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();

            if (generateGridOnCombat && grid != null)
            {
                int width = Mathf.Clamp(gridWidth, 1, MaxGridSize);
                int height = Mathf.Clamp(gridHeight, 1, MaxGridSize);

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
                if (fitGridAroundPlayer && player != null)
                {
                    float tile = gridTileSize > 0.1f ? gridTileSize : 1.6f;
                    center = ComputeGridCenter(center, player.transform.position, width, height, tile, GetGridArea());
                }
                grid.GenerateGridAt(center, width, height, gridTileSize);
                Debug.Log($"[DungeonRoomController] Generated {width}x{height} combat grid centered at {center} for '{roomLocation}'.");
            }

            // 2. Enable all GameObjects in the exitBarriers list (locking the doors)
            SetBarriersLocked(true);

            // 3. Enable all GameObjects in the roomEnemies list and gather combatants
            List<CombatUnit> activeParticipants = new List<CombatUnit>();

            if (player != null && player.IsAlive)
            {
                activeParticipants.Add(player);

                // Snap player to nearest valid walkable grid tile (ensuring not on/overlapping exit barriers)
                if (grid != null)
                {
                    Vector2Int playerTilePos = grid.GetGridPosition(player.transform.position);
                    GridTile pTile = grid.GetTileAt(playerTilePos);
                    if (pTile == null || !pTile.IsWalkable || pTile.IsOccupied || IsTileOverlappingBarrier(pTile))
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
                bool isRegularEncounter = string.IsNullOrEmpty(bossIdentifier);
                int maxAllowedEnemies = isRegularEncounter ? 2 : int.MaxValue;

                if (isRegularEncounter)
                {
                    // Check if any enemy is elite (MaxHP >= 25 or AttackDamage >= 5)
                    foreach (var enemyObj in roomEnemies)
                    {
                        if (enemyObj != null)
                        {
                            EnemyUnit eu = enemyObj.GetComponent<EnemyUnit>();
                            if (eu != null && (eu.MaxHP >= 25 || eu.AttackDamage >= 5))
                            {
                                maxAllowedEnemies = 1; // Solo hero pacing: 1 elite max
                                break;
                            }
                        }
                    }
                }

                // A boss such as the Cursed Commander keeps his room allies in reserve until he calls them in
                IReinforcementSummoner summoner = null;
                foreach (var enemyObj in roomEnemies)
                {
                    if (enemyObj == null) continue;
                    summoner = enemyObj.GetComponent<IReinforcementSummoner>();
                    if (summoner != null) break;
                }

                int activeEnemyCount = 0;
                foreach (var enemyObj in roomEnemies)
                {
                    if (enemyObj != null)
                    {
                        EnemyUnit reserve = summoner != null ? enemyObj.GetComponent<EnemyUnit>() : null;
                        if (reserve != null && !ReferenceEquals(reserve, summoner) && summoner.HoldsBackAtStart(reserve))
                        {
                            enemyObj.SetActive(false);
                            summoner.AddReserve(reserve);
                            continue;
                        }

                        if (activeEnemyCount < maxAllowedEnemies)
                        {
                            enemyObj.SetActive(true);

                            EnemyUnit enemyUnit = enemyObj.GetComponent<EnemyUnit>();
                            if (enemyUnit != null && enemyUnit.IsAlive)
                            {
                                activeParticipants.Add(enemyUnit);
                                activeEnemyCount++;

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
                        else
                        {
                            enemyObj.SetActive(false);
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

            // Guarantee Combat UI is active and displaying
            CastleOfTheD20.UI.CombatUIController.Instance?.EnsureActiveAndReady(true);

            // 4. Initialize the turn-based combat via TurnManager (created on demand)
            TurnManager turnManager = TurnManager.EnsureInstance();
            if (turnManager != null)
            {
                if (activeParticipants.Count > 0)
                {
                    turnManager.StartCombat(activeParticipants);
                }
                else
                {
                    turnManager.StartCombat();
                }
            }

            // Hostiles square up to the hero instead of keeping their authored facing
            if (player != null)
            {
                foreach (CombatUnit unit in activeParticipants)
                {
                    if (unit is EnemyUnit) unit.FaceTowards(player.transform.position);
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
                ChestRewardInteraction chestReward = RevealRewardChest();
                Debug.Log($"[DungeonRoomController] Secret passage or reward chest revealed in '{roomLocation}' with {chestReward.GoldReward} gold reward.");
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

        private ChestRewardInteraction RevealRewardChest()
        {
            secretPassageOrChest.SetActive(true);
            ChestRewardInteraction chestReward = secretPassageOrChest.GetComponent<ChestRewardInteraction>();
            if (chestReward == null)
            {
                chestReward = secretPassageOrChest.AddComponent<ChestRewardInteraction>();
                chestReward.GoldReward = 30;
            }
            chestReward.EnsureChestCollider();
            return chestReward;
        }

        /// <summary>
        /// Puts a room whose fight the hero lost back to how it was before they walked in: doors open,
        /// hostiles revived at full strength and hidden on their starting spots, the grid cleared and
        /// the trigger (and the boss's intro dialogue) ready to start the fight again.
        /// Does nothing for a room that was never entered or is already cleared.
        /// </summary>
        public void ResetEncounter()
        {
            if (currentState != RoomState.CombatActive && !isAwaitingBossDialogue) return;

            if (isAwaitingBossDialogue)
            {
                DialogueController.OnDialogueEnded -= HandleBossDialogueEnded;
                isAwaitingBossDialogue = false;
            }

            currentState = RoomState.Unexplored;
            isEncounterTriggered = false;

            SetBarriersLocked(false);

            if (roomEnemies != null)
            {
                for (int i = 0; i < roomEnemies.Count; i++)
                {
                    GameObject enemyObj = roomEnemies[i];
                    if (enemyObj == null) continue;

                    EnemyUnit enemy = enemyObj.GetComponent<EnemyUnit>();
                    if (enemy != null)
                    {
                        enemy.Revive();
                        enemy.ClearTile();
                    }

                    if (i < enemyStartPoses.Count)
                    {
                        enemyObj.transform.SetPositionAndRotation(enemyStartPoses[i].position, enemyStartPoses[i].rotation);
                    }
                    enemyObj.SetActive(false);
                }
            }

            if (generateGridOnCombat && GridManager.Instance != null)
            {
                GridManager.Instance.ClearGrid();
            }

            if (bossDialogueNpc != null)
            {
                bossDialogueNpc.transform.SetPositionAndRotation(bossDialogueNpcStartPose.position, bossDialogueNpcStartPose.rotation);
                bossDialogueNpc.gameObject.SetActive(true);
                PrepareBossDialogueNpc();
            }

            if (triggerCollider != null) triggerCollider.enabled = true;

            Debug.Log($"[DungeonRoomController] Encounter in '{roomLocation}' reset after a defeat; the fight starts again when the hero walks in.");
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

        /// <summary>
        /// Floor rectangle (world X/Z) the combat grid must stay inside: the authored gridAreaXZ,
        /// or this room's trigger box when none is set. Read from the collider's shape, so it also
        /// works after the trigger has been disabled.
        /// </summary>
        public Rect GetGridArea()
        {
            if (gridAreaXZ.width > 0f && gridAreaXZ.height > 0f) return gridAreaXZ;

            BoxCollider box = triggerCollider != null ? triggerCollider : GetComponent<BoxCollider>();
            if (box == null) return new Rect(0f, 0f, 0f, 0f);

            Vector3 c = transform.TransformPoint(box.center);
            Vector3 lossy = transform.lossyScale;
            float sizeX = Mathf.Abs(box.size.x * lossy.x);
            float sizeZ = Mathf.Abs(box.size.z * lossy.z);
            return new Rect(c.x - sizeX * 0.5f, c.z - sizeZ * 0.5f, sizeX, sizeZ);
        }

        /// <summary>
        /// Picks a grid center that keeps the hero at least two tiles inside the grid, so large rooms do
        /// not teleport them to a far edge, while the grid stays inside <paramref name="area"/>.
        /// An axis on which the grid is already as large as the area keeps its authored center.
        /// </summary>
        public static Vector3 ComputeGridCenter(Vector3 desiredCenter, Vector3 playerPos, int width, int height, float tileSize, Rect area)
        {
            bool hasArea = area.width > 0f && area.height > 0f;
            float x = FitAxis(desiredCenter.x, playerPos.x, (width - 1) * 0.5f * tileSize, tileSize,
                hasArea, area.xMin, area.xMax);
            float z = FitAxis(desiredCenter.z, playerPos.z, (height - 1) * 0.5f * tileSize, tileSize,
                hasArea, area.yMin, area.yMax);
            return new Vector3(x, desiredCenter.y, z);
        }

        private static float FitAxis(float center, float player, float halfSpan, float tileSize, bool hasArea, float areaMin, float areaMax)
        {
            // Outer tile edges must stay within the area: tile centers sit half a tile inside it
            float minCenter = areaMin + halfSpan + tileSize * 0.5f;
            float maxCenter = areaMax - halfSpan - tileSize * 0.5f;
            if (hasArea && minCenter > maxCenter) return center; // grid already spans the whole area

            float margin = Mathf.Min(PlayerEdgeMarginTiles * tileSize, halfSpan);
            if (player > center + halfSpan - margin) center = player - halfSpan + margin;
            else if (player < center - halfSpan + margin) center = player + halfSpan - margin;

            return hasArea ? Mathf.Clamp(center, minCenter, maxCenter) : center;
        }

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

            // Update walkability of tiles overlapping the barrier
            UpdateBarrierTilesWalkability(locked);
        }

        private bool IsTileOverlappingBarrier(GridTile tile)
        {
            if (tile == null || exitBarriers == null) return false;
            foreach (var barrier in exitBarriers)
            {
                if (barrier == null) continue;
                Collider col = barrier.GetComponent<Collider>();
                if (col != null)
                {
                    Vector3 tileCenter = tile.transform.position + Vector3.up * 0.5f;
                    Bounds b = col.bounds;
                    b.Expand(new Vector3(0.6f, 1.0f, 0.6f));
                    if (b.Contains(tileCenter) || b.Contains(tile.transform.position))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void UpdateBarrierTilesWalkability(bool locked)
        {
            GridManager grid = GridManager.Instance;
            if (grid == null || exitBarriers == null) return;

            foreach (var kvp in grid.Tiles)
            {
                GridTile tile = kvp.Value;
                if (tile != null && IsTileOverlappingBarrier(tile))
                {
                    tile.IsWalkable = !locked;
                    if (locked)
                    {
                        tile.IsOccupied = true;
                    }
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
                if (kvp.Value != null && kvp.Value.IsWalkable && !kvp.Value.IsOccupied && !IsTileOverlappingBarrier(kvp.Value))
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
            if (geo != null)
            {
                geo.localPosition = Vector3.zero;

                // Ensure camera-blocking ceiling is removed for top-down isometric view
                Transform ceiling = geo.Find("Cellar_Ceiling");
                if (ceiling != null)
                {
                    DestroyImmediate(ceiling.gameObject);
                }

                // Ensure South wall is a cutaway half-wall without camera-blocking colliders
                Transform wallSouth = geo.Find("Wall_South");
                if (wallSouth != null)
                {
                    wallSouth.localPosition = new Vector3(0f, 0.6f, -9f);
                    wallSouth.localScale = new Vector3(18f, 1.2f, 1f);
                    Collider wsCol = wallSouth.GetComponent<Collider>();
                    if (wsCol != null) DestroyImmediate(wsCol);
                }
            }

            Transform lighting = root.Find("Cellar_Lighting");
            if (lighting != null) lighting.localPosition = Vector3.zero;

            Transform ladder = root.Find("Cellar_Exit_Ladder");
            if (ladder != null) ladder.localPosition = new Vector3(0f, 0f, -8.4f);

            Transform spawn = root.Find("Cellar_PlayerSpawnPoint");
            if (spawn != null) spawn.localPosition = new Vector3(0f, 0.2f, -5.0f);

            Transform barrier = root.Find("Cellar_Exit_Barrier");
            if (barrier != null) barrier.localPosition = new Vector3(0f, 0.6f, -7.5f); // low sill: keeps the ladder blocked without hiding the hero from the camera

            Transform chest = root.Find("Cellar_Reward_Chest");
            if (chest != null)
            {
                chest.localPosition = new Vector3(0f, 0f, 7.2f);
                ChestRewardInteraction chestReward = chest.GetComponent<ChestRewardInteraction>();
                if (chestReward == null)
                {
                    chestReward = chest.gameObject.AddComponent<ChestRewardInteraction>();
                }
                chestReward.GoldReward = 30;
                chestReward.EnsureChestCollider();
            }

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
