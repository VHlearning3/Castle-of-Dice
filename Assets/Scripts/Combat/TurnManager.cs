using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;

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

        #endregion

        #region Serialized Fields

        [Header("State & Settings")]
        [SerializeField] private TurnState currentState = TurnState.PlayerTurn;
        [SerializeField] private float enemyTurnDelay = 0.6f;

        #endregion

        #region Private State

        private readonly List<CombatUnit> activeUnits = new List<CombatUnit>();
        private int currentUnitIndex = -1;
        private CombatUnit currentActiveUnit;
        private int turnCounter = 1;
        private bool isCombatActive = false;

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

        #endregion

        #region Events

        /// <summary>Fired when the combat state changes (PlayerTurn, EnemyTurn, Victory, Defeat).</summary>
        public static event Action<TurnState> OnTurnStateChanged;

        /// <summary>Fired when a specific unit begins its turn.</summary>
        public static event Action<CombatUnit> OnUnitTurnStarted;

        /// <summary>Fired when combat concludes: true for Victory, false for Defeat.</summary>
        public static event Action<bool> OnCombatEnded;

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
            // Auto-enroll scene units if not manually started
            if (!isCombatActive)
            {
                AutoEnrollSceneUnits();
            }
        }

        private void OnDestroy()
        {
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

            // Prune dead or uninitialized units
            players.RemoveAll(p => p == null || !p.IsAlive);
            enemies.RemoveAll(e => e == null || !e.IsAlive);

            if (players.Count > 0 && enemies.Count > 0)
            {
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
                Debug.Log("[TurnManager] Safe area detected (no living enemies found in scene). Safe exploration mode active.");
            }
        }

        /// <summary>
        /// Initializes the combat state machine with a specified list of combatants.
        /// </summary>
        public void StartCombat(List<CombatUnit> units)
        {
            activeUnits.Clear();
            foreach (var unit in units)
            {
                if (unit != null && unit.IsAlive)
                {
                    activeUnits.Add(unit);
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

            isCombatActive = true;
            turnCounter = 1;
            currentUnitIndex = -1;

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
                SetTurnState(TurnState.PlayerTurn);
                player.ResetTurnFlags();

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

            if (enemy != null && enemy.IsAlive && isCombatActive)
            {
                enemy.ExecuteTurnAction(GridManager.Instance, AbilityExecutor.Instance);
            }

            yield return new WaitForSeconds(0.3f);

            // Conclude enemy turn
            EndActiveUnitTurn();
        }

        /// <summary>
        /// Public API invoked by the player via "End Turn" button or upon exhausting actions.
        /// </summary>
        public void EndPlayerTurn()
        {
            if (currentState != TurnState.PlayerTurn)
            {
                Debug.LogWarning("[TurnManager] Cannot end player turn: Not currently in PlayerTurn state.");
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
                // All heroes defeated
                isCombatActive = false;
                SetTurnState(TurnState.Defeat);
                GridManager.Instance?.ClearAllHighlights();
                Debug.Log("[TurnManager] DEFEAT! All party members have fallen.");
                OnCombatEnded?.Invoke(false);
                return true;
            }

            if (livingEnemies == 0)
            {
                // All enemies defeated
                isCombatActive = false;
                SetTurnState(TurnState.Victory);
                GridManager.Instance?.ClearAllHighlights();
                Debug.Log("[TurnManager] VICTORY! All enemies have been vanquished.");
                OnCombatEnded?.Invoke(true);
                return true;
            }

            return false;
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

            GridManager.Instance.ClearAllHighlights();
            var reachable = GridManager.Instance.GetReachableTiles(player.GridPosition, player.MovementRange);
            GridManager.Instance.HighlightTiles(reachable, TileHighlightType.Reachable);
        }

        #endregion
    }
}
