using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.UI;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.Combat
{
    /// <summary>
    /// Central Turn-Based State Machine managing combat progression.
    /// Orchestrates turn transitions between PlayerTurn, EnemyTurn, ResolveAbilities, Victory, and Defeat.
    /// Coordinates initiative queues, AI execution delays, and win/loss evaluations.
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        #region Singleton

        public static TurnManager Instance { get; private set; }

        /// <summary>
        /// Returns the scene's TurnManager, creating the combat systems (TurnManager + AbilityExecutor)
        /// on demand. The zone scenes do not ship these components, so without this no encounter
        /// could ever start combat.
        /// </summary>
        public static TurnManager EnsureInstance()
        {
            if (Instance == null)
            {
                TurnManager existing = FindAnyObjectByType<TurnManager>();
                if (existing != null)
                {
                    Instance = existing;
                }
                else
                {
                    GameObject systems = new GameObject("CombatSystems");
                    systems.AddComponent<TurnManager>(); // Awake registers Instance
                }
            }

            if (AbilityExecutor.Instance == null && FindAnyObjectByType<AbilityExecutor>() == null && Instance != null)
            {
                Instance.gameObject.AddComponent<AbilityExecutor>();
            }

            return Instance;
        }

        #endregion

        #region Serialized Fields

        [Header("State & Settings")]
        [SerializeField] private TurnState currentState = TurnState.PlayerTurn;
        [SerializeField] private float enemyTurnDelay = 0.6f;
        [Tooltip("If true, automatically discovers living units and starts combat immediately upon scene start. Keep false for exploration zones such as StartVillage.")]
        [SerializeField] private bool autoStartCombatOnStart = false;

        [Header("Combat Victory Loot")]
        [Tooltip("Minimum scrap metal dropped upon combat victory (MasterSpec §5.4).")]
        [Range(1, 20)]
        [SerializeField] private int minVictoryScrapDrop = 2;

        [Tooltip("Maximum scrap metal dropped upon combat victory (MasterSpec §5.4).")]
        [Range(1, 50)]
        [SerializeField] private int maxVictoryScrapDrop = 10;

        #endregion

        #region Private State

        private readonly List<CombatUnit> activeUnits = new List<CombatUnit>();
        private int currentUnitIndex = -1;
        private CombatUnit currentActiveUnit;
        private int turnCounter = 0;
        private bool isCombatActive = false;
        private bool awardScrapOnVictory = true;

        #endregion

        #region Public Properties

        /// <summary>Current active combat state machine phase.</summary>
        public TurnState CurrentState => currentState;

        /// <summary>Current unit whose turn is actively being processed.</summary>
        public CombatUnit CurrentActiveUnit => currentActiveUnit;

        /// <summary>All enrolled combatants participating in this battle.</summary>
        public IReadOnlyList<CombatUnit> ActiveUnits => activeUnits;

        /// <summary>Round number of the ongoing battle.</summary>
        public int TurnCounter => turnCounter;

        /// <summary>Whether combat is currently in progress.</summary>
        public bool IsCombatActive => isCombatActive;

        /// <summary>False for the running (or last) fight when it pays no scrap, e.g. a village brawl.</summary>
        public bool AwardsVictoryScrap => awardScrapOnVictory;

        #endregion

        #region Events

        /// <summary>Fired when the combat state changes (PlayerTurn, EnemyTurn, Victory, Defeat).</summary>
        public static event Action<TurnState> OnTurnStateChanged;

        /// <summary>Fired when a specific unit begins its turn.</summary>
        public static event Action<CombatUnit> OnUnitTurnStarted;

        /// <summary>Fired when combat concludes: true for Victory, false for Defeat.</summary>
        public static event Action<bool> OnCombatEnded;

        /// <summary>Fired upon combat victory when scrap metal is awarded: (scrapAmount).</summary>
        public static event Action<int> OnCombatVictoryScrapAwarded;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            // Auto-enroll scene units only if explicitly enabled (e.g. standalone combat test scenes)
            if (autoStartCombatOnStart && !isCombatActive)
            {
                AutoEnrollSceneUnits();
            }
            else if (!isCombatActive)
            {
                if (GameManager.Instance != null && GameManager.Instance.CurrentMode != GamePlayMode.Exploration)
                {
                    GameManager.Instance.SetMode(GamePlayMode.Exploration);
                }
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromUnitDeaths();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Combat Initialization

        /// <summary>
        /// Automatically discovers all living PlayerUnit and EnemyUnit components in the scene.
        /// Starts combat only if both living players and enemies exist; otherwise keeps exploration mode active.
        /// </summary>
        public void AutoEnrollSceneUnits()
        {
            List<PlayerUnit> players = new List<PlayerUnit>(FindObjectsByType<PlayerUnit>(FindObjectsSortMode.None));
            List<EnemyUnit> enemies = new List<EnemyUnit>(FindObjectsByType<EnemyUnit>(FindObjectsSortMode.None));

            // Prune dead, uninitialized, or inactive units
            players.RemoveAll(p => p == null || !p.IsAlive || !p.gameObject.activeInHierarchy);
            enemies.RemoveAll(e => e == null || !e.IsAlive || !e.gameObject.activeInHierarchy);

            if (players.Count > 0 && enemies.Count > 0)
            {
                // Solo Hero pacing: Limit regular encounters to 1 elite or max 2 weaker enemies
                if (players.Count == 1 && enemies.Count > 2)
                {
                    bool hasBoss = enemies.Exists(e => e is Bosses.CursedCommanderBoss ||
                                                       e is Bosses.ShadowMageMalakorBoss ||
                                                       (e.name.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0));
                    if (!hasBoss)
                    {
                        bool hasElite = enemies.Exists(e => e.MaxHP >= 25 || e.AttackDamage >= 5);
                        int maxAllowed = hasElite ? 1 : 2;

                        PlayerUnit p = players[0];
                        enemies.Sort((a, b) => Vector3.Distance(a.transform.position, p.transform.position)
                            .CompareTo(Vector3.Distance(b.transform.position, p.transform.position)));

                        for (int i = maxAllowed; i < enemies.Count; i++)
                        {
                            enemies[i].gameObject.SetActive(false);
                        }
                        enemies.RemoveRange(maxAllowed, enemies.Count - maxAllowed);
                    }
                }

                List<CombatUnit> allUnits = new List<CombatUnit>();
                allUnits.AddRange(players);
                allUnits.AddRange(enemies);
                StartCombat(allUnits);
            }
            else
            {
                isCombatActive = false;
                GridManager.Instance?.ClearAllHighlights();
                GameManager.Instance?.SetMode(GamePlayMode.Exploration);
                Debug.Log("[TurnManager] Safe area detected (no living active enemies found in scene). Safe exploration mode active.");
            }
        }

        /// <summary>
        /// Starts combat by auto-enrolling all living players and enemies in the scene.
        /// </summary>
        public void StartCombat()
        {
            AutoEnrollSceneUnits();
        }

        /// <summary>
        /// Starts combat for a specific room encounter.
        /// </summary>
        public void StartCombatEncounter(PlayerUnit player, string location = "", string bossId = "")
        {
            StartCombat();
        }

        /// <summary>
        /// Concludes combat cleanly and resets highlights and game play mode.
        /// </summary>
        public void EndCombat(bool isVictory)
        {
            // Combat may already have been concluded by CheckCombatEndConditions (e.g. the Defeat modal
            // calling EndCombat(false) afterwards). Only restore exploration mode in that case so the
            // Victory/Defeat events, loot and modals do not fire twice.
            if (isCombatActive)
            {
                ConcludeCombat(isVictory);
            }
            GameManager.Instance?.SetMode(GamePlayMode.Exploration);
        }

        /// <summary>
        /// Adds a unit (e.g. a boss summon) to the running battle without restarting the turn order.
        /// The new unit acts when the queue reaches it.
        /// </summary>
        public void AddCombatant(CombatUnit unit)
        {
            if (!isCombatActive || unit == null || !unit.IsAlive || activeUnits.Contains(unit)) return;

            activeUnits.Add(unit);
            unit.OnUnitDied += HandleUnitDied;
            Debug.Log($"[TurnManager] {unit.UnitName} joined the battle.");
        }

        /// <summary>
        /// Initializes the combat state machine with a specified list of combatants.
        /// </summary>
        /// <param name="units">Combatants enrolled in this battle.</param>
        /// <param name="awardVictoryScrap">False for fights that pay no scrap on victory (e.g. a repeatable village brawl).</param>
        public void StartCombat(List<CombatUnit> units, bool awardVictoryScrap = true)
        {
            UnsubscribeFromUnitDeaths();
            activeUnits.Clear();
            foreach (var unit in units)
            {
                if (unit != null && unit.IsAlive && !activeUnits.Contains(unit))
                {
                    activeUnits.Add(unit);
                    unit.OnUnitDied += HandleUnitDied;
                }
            }

            if (activeUnits.Count == 0)
            {
                Debug.LogWarning("[TurnManager] Cannot start combat: No active combat units provided.");
                return;
            }

            // Sort turn order: Player units act first, followed by enemies
            activeUnits.Sort((a, b) =>
            {
                if (a is PlayerUnit && b is not PlayerUnit) return -1;
                if (b is PlayerUnit && a is not PlayerUnit) return 1;
                return 0;
            });

            // Ensure tactical grid is generated around combatants if no tiles exist
            if (GridManager.Instance != null && GridManager.Instance.Tiles.Count == 0 && activeUnits.Count > 0)
            {
                Vector3 combatCenter = Vector3.zero;
                foreach (var u in activeUnits) combatCenter += u.transform.position;
                combatCenter /= activeUnits.Count;

                GridManager.Instance.GenerateGridAt(combatCenter, 12, 12, 1.6f);
                Debug.Log($"[TurnManager] Auto-generated 12x12 combat grid around combatants centered at {combatCenter}.");

                foreach (var u in activeUnits)
                {
                    if (u != null && GridManager.Instance != null && Mathf.Abs(u.transform.position.y - GridManager.Instance.transform.position.y) <= 3.5f)
                    {
                        Vector2Int pos = GridManager.Instance.GetGridPosition(u.transform.position);
                        GridTile tile = GridManager.Instance.GetTileAt(pos);
                        if (tile != null)
                        {
                            u.MoveToTile(tile);
                        }
                    }
                }
            }

            isCombatActive = true;
            awardScrapOnVictory = awardVictoryScrap;
            turnCounter = 0; // becomes 1 when the first unit in the queue starts its turn
            currentUnitIndex = -1;

            // Ensure all units are synchronized to their grid positions if on the same floor
            foreach (var u in activeUnits)
            {
                if (u != null)
                {
                    if (GridManager.Instance != null && Mathf.Abs(u.transform.position.y - GridManager.Instance.transform.position.y) <= 3.5f)
                    {
                        u.EnsureTilePosition();
                    }
                    if (u is PlayerUnit p) p.ResetTurnFlags();
                }
            }

            // A Poison Vial is used up automatically at the start of a real fight (not a village brawl)
            if (awardVictoryScrap && InventoryManager.Instance != null)
            {
                foreach (var u in activeUnits)
                {
                    if (u is PlayerUnit hero)
                    {
                        InventoryManager.Instance.TryCoatWithPoisonVial(hero);
                    }
                }
            }

            // Ensure CombatUIController is awake and active
            CombatUIController.Instance?.EnsureActiveAndReady(true);

            Debug.Log($"[TurnManager] Combat initiated with {activeUnits.Count} combatants.");
            NextTurn();
        }

        #endregion

        #region Turn Progression

        /// <summary>
        /// Advances to the next living combatant in the turn queue.
        /// </summary>
        public void NextTurn()
        {
            if (CheckCombatEndConditions()) return;

            // Find next living unit
            int searchCount = 0;
            do
            {
                currentUnitIndex = (currentUnitIndex + 1) % activeUnits.Count;
                if (currentUnitIndex == 0)
                {
                    turnCounter++;
                }

                currentActiveUnit = activeUnits[currentUnitIndex];
                searchCount++;
            } while ((currentActiveUnit == null || !currentActiveUnit.IsAlive) && searchCount <= activeUnits.Count * 2);

            if (currentActiveUnit == null || !currentActiveUnit.IsAlive)
            {
                CheckCombatEndConditions();
                return;
            }

            StartTurnForActiveUnit();
        }

        private void StartTurnForActiveUnit()
        {
            Debug.Log($"[TurnManager] Round {turnCounter} - Starting turn for: {currentActiveUnit.UnitName}");
            OnUnitTurnStarted?.Invoke(currentActiveUnit);

            // 1. Process turn start status effects (e.g. Poison d6 damage ticks)
            currentActiveUnit.StatusEffects?.ProcessTurnStartEffects();

            // Verify unit survived turn start effects
            if (!currentActiveUnit.IsAlive)
            {
                if (!CheckCombatEndConditions())
                {
                    NextTurn();
                }
                return;
            }

            // 2. Dispatch turn state based on unit type
            if (currentActiveUnit is PlayerUnit player)
            {
                CombatUIController.Instance?.EnsureActiveAndReady(true);
                player.EnsureTilePosition();
                player.ResetTurnFlags();
                SetTurnState(TurnState.PlayerTurn);

                // Highlight valid movement cells
                HighlightPlayerReachableTiles(player);
            }
            else if (currentActiveUnit is EnemyUnit enemy)
            {
                SetTurnState(TurnState.EnemyTurn);
                GridManager.Instance?.ClearAllHighlights();

                StartCoroutine(ExecuteEnemyTurnDelayed(enemy));
            }
        }

        private IEnumerator ExecuteEnemyTurnDelayed(EnemyUnit enemy)
        {
            yield return new WaitForSeconds(enemyTurnDelay);

            // Let a hero who ended the turn mid-walk finish walking first
            while (IsAnyUnitWalking()) yield return null;

            if (enemy != null && enemy.IsAlive && isCombatActive)
            {
                enemy.ExecuteTurnAction(GridManager.Instance, AbilityExecutor.Instance);

                // The enemy walks tile by tile and attacks when it arrives
                while (enemy != null && enemy.IsWalking) yield return null;
            }

            yield return new WaitForSeconds(0.3f);

            // Conclude enemy turn
            EndActiveUnitTurn();
        }

        private bool IsAnyUnitWalking()
        {
            for (int i = 0; i < activeUnits.Count; i++)
            {
                CombatUnit unit = activeUnits[i];
                if (unit != null && unit.IsWalking) return true;
            }
            return false;
        }

        /// <summary>
        /// Public API invoked by the player via "End Turn" button or upon exhausting actions.
        /// </summary>
        public void EndPlayerTurn()
        {
            if (currentState != TurnState.PlayerTurn)
            {
                Debug.Log("[TurnManager] Cannot end player turn: Not currently in PlayerTurn state.");
                return;
            }

            GridManager.Instance?.ClearAllHighlights();
            EndActiveUnitTurn();
        }

        private void EndActiveUnitTurn()
        {
            // Process end of turn status effect countdowns
            currentActiveUnit?.StatusEffects?.ProcessTurnEndEffects();

            if (!CheckCombatEndConditions())
            {
                NextTurn();
            }
        }

        #endregion

        #region Victory & Defeat Checks

        /// <summary>
        /// Evaluates battlefield state for victory (all enemies defeated) or defeat (all players defeated).
        /// </summary>
        /// <returns>True if combat ended, false if battle continues.</returns>
        public bool CheckCombatEndConditions()
        {
            if (!isCombatActive) return true;

            int livingPlayers = 0;
            int livingEnemies = 0;

            foreach (var unit in activeUnits)
            {
                if (unit != null && unit.IsAlive)
                {
                    if (unit is PlayerUnit) livingPlayers++;
                    else if (unit is EnemyUnit) livingEnemies++;
                }
            }

            if (livingPlayers == 0)
            {
                Debug.Log("[TurnManager] DEFEAT! All party members have fallen.");
                ConcludeCombat(false);
                return true;
            }

            if (livingEnemies == 0)
            {
                Debug.Log("[TurnManager] VICTORY! All enemies have been vanquished.");
                ConcludeCombat(true);
                return true;
            }

            return false;
        }

        private void ConcludeCombat(bool isVictory)
        {
            isCombatActive = false;
            UnsubscribeFromUnitDeaths();
            SetTurnState(isVictory ? TurnState.Victory : TurnState.Defeat);
            GridManager.Instance?.ClearAllHighlights();
            foreach (var unit in activeUnits)
            {
                if (unit == null) continue;
                if (unit is PlayerUnit hero) hero.ClearPoisonCoating();

                // Combat buffs and debuffs (and their auras) end with the fight
                unit.StatusEffects?.ClearAllEffects();
            }
            if (isVictory)
            {
                if (awardScrapOnVictory)
                {
                    AwardCombatVictoryScrap();
                }
                foreach (var unit in activeUnits)
                {
                    if (unit is PlayerUnit player && player.IsAlive && player.UnitAnimator != null)
                    {
                        player.UnitAnimator.SetTrigger("Victory");
                    }
                }
            }
            OnCombatEnded?.Invoke(isVictory);
        }

        /// <summary>
        /// Resolves victory/defeat the moment a combatant falls, instead of waiting for End Turn.
        /// </summary>
        private void HandleUnitDied(CombatUnit unit)
        {
            if (unit != null)
            {
                unit.OnUnitDied -= HandleUnitDied;
            }

            if (isCombatActive)
            {
                CheckCombatEndConditions();
            }
        }

        private void UnsubscribeFromUnitDeaths()
        {
            for (int i = 0; i < activeUnits.Count; i++)
            {
                if (activeUnits[i] != null)
                {
                    activeUnits[i].OnUnitDied -= HandleUnitDied;
                }
            }
        }

        /// <summary>
        /// Awards 2–10 pieces of scrap metal upon combat victory as specified in MasterSpec §5.4.
        /// </summary>
        public int AwardCombatVictoryScrap()
        {
            int minScrap = Mathf.Max(1, minVictoryScrapDrop);
            int maxScrap = Mathf.Max(minScrap, maxVictoryScrapDrop);
            int scrapReward = UnityEngine.Random.Range(minScrap, maxScrap + 1); // min to max inclusive (e.g. 2 to 10)

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddScrapMetal(scrapReward);
                Debug.Log($"[TurnManager] Combat Victory Loot: +{scrapReward} Scrap Metal dropped (MasterSpec §5.4, 2–10 range)!");
            }
            else
            {
                Debug.LogWarning($"[TurnManager] InventoryManager not found; could not persist {scrapReward} scrap metal drop.");
            }

            OnCombatVictoryScrapAwarded?.Invoke(scrapReward);
            return scrapReward;
        }

        #endregion

        #region State Management & UI Helpers

        private void SetTurnState(TurnState newState)
        {
            currentState = newState;
            OnTurnStateChanged?.Invoke(newState);
        }

        private void HighlightPlayerReachableTiles(PlayerUnit player)
        {
            if (GridManager.Instance == null || player == null) return;

            player.EnsureTilePosition();
            GridManager.Instance.ClearAllHighlights();
            var reachable = GridManager.Instance.GetReachableTiles(player.GridPosition, player.MovementRange);
            GridManager.Instance.HighlightTiles(reachable, TileHighlightType.Reachable);

            // Highlight enemies that are currently within direct primary attack range
            CastleOfTheD20.Data.AbilitySO primaryAbility = player.GetAbility(0);
            int primaryRange = primaryAbility != null ? primaryAbility.Range : 1;
            List<GridTile> attackableTiles = GridManager.Instance.GetTilesInRadius(player.GridPosition, primaryRange);
            foreach (var tile in attackableTiles)
            {
                CombatUnit targetUnit = tile.OccupyingUnit;
                if (targetUnit == null)
                {
                    foreach (var u in activeUnits)
                    {
                        if (u != null && u.IsAlive && u is EnemyUnit && u.GridPosition == tile.GridPosition)
                        {
                            // Let the unit re-register its own tile instead of patching occupancy here
                            u.EnsureTilePosition();
                            targetUnit = u;
                            break;
                        }
                    }
                }
                if (targetUnit != null && targetUnit is EnemyUnit)
                {
                    tile.ApplyHighlight(TileHighlightType.EnemyTarget);
                }
            }
        }

        #endregion
    }
}
