using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Major atmospheric zones within the game world.
    /// </summary>
    public enum GameLocation
    {
        /// <summary>Safe starting haven: Oakhaven village, blacksmith shop, tavern.</summary>
        Village,

        /// <summary>Wing 1: Outer Castle Courtyard and Watchtower (Cursed Commander).</summary>
        Courtyard,

        /// <summary>Wing 2: Arcane Library and Study (Shadow Mage Malakor).</summary>
        Library,

        /// <summary>Wing 3: The Crown Hall (The Gargoyle King final boss).</summary>
        CrownHall
    }

    /// <summary>
    /// Current high-level game activity mode.
    /// </summary>
    public enum GamePlayMode
    {
        /// <summary>Free-roaming movement, NPC interaction, chest opening.</summary>
        Exploration,

        /// <summary>Tactical grid turn-based battle.</summary>
        Combat,

        /// <summary>Branching NPC conversation or D20 skill check dialogue.</summary>
        Dialogue,

        /// <summary>Blacksmith trade and equipment shop.</summary>
        Shop
    }

    /// <summary>
    /// Persistent high-level game manager supervising location transitions, exploration/combat modes,
    /// boss progression tracking, and overall campaign victory conditions.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region Singleton

        public static GameManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("Starting State")]
        [SerializeField] private GameLocation currentLocation = GameLocation.Village;
        [SerializeField] private GamePlayMode currentMode = GamePlayMode.Exploration;

        [Header("Persistence")]
        [SerializeField] private bool persistAcrossScenes = true;

        #endregion

        #region Private State

        private readonly HashSet<string> defeatedBosses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<GameLocation> clearedLocations = new HashSet<GameLocation>();

        #endregion

        #region Public Properties

        /// <summary>Current active area.</summary>
        public GameLocation CurrentLocation => currentLocation;

        /// <summary>Current gameplay mode.</summary>
        public GamePlayMode CurrentMode => currentMode;

        /// <summary>Whether the Courtyard boss (Cursed Commander) has been defeated.</summary>
        public bool IsCommanderDefeated => defeatedBosses.Contains("CursedCommander");

        /// <summary>Whether the Library boss (Shadow Mage Malakor) has been defeated.</summary>
        public bool IsMalakorDefeated => defeatedBosses.Contains("ShadowMageMalakor");

        /// <summary>Whether the Crown Hall boss (The Gargoyle King) has been defeated.</summary>
        public bool IsGargoyleKingDefeated => defeatedBosses.Contains("GargoyleKing");

        #endregion

        #region Events

        /// <summary>Fired when moving between Village, Courtyard, Library, and Crown Hall.</summary>
        public static event Action<GameLocation> OnLocationChanged;

        /// <summary>Fired when mode switches between Exploration, Combat, Dialogue, and Shop.</summary>
        public static event Action<GamePlayMode> OnPlayModeChanged;

        /// <summary>Fired when a dungeon wing is cleared of hostiles.</summary>
        public static event Action<GameLocation> OnWingCleared;

        /// <summary>Fired when a major boss is vanquished.</summary>
        public static event Action<string> OnBossDefeated;

        /// <summary>Fired when the entire campaign is won (Gargoyle King slain).</summary>
        public static event Action OnGameWon;

        /// <summary>Fired upon hero party wipeout.</summary>
        public static event Action OnGameLost;

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

            // Ensure game always starts in clean exploration mode
            currentMode = GamePlayMode.Exploration;

            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start()
        {
            TurnManager.OnCombatEnded += HandleCombatEnded;

            // Broadcast initial state to ensure all UI and controllers are synchronized
            Debug.Log($"[GameManager] Initialized. Location: {currentLocation}, GameplayMode: {currentMode}");
            OnPlayModeChanged?.Invoke(currentMode);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            TurnManager.OnCombatEnded -= HandleCombatEnded;
        }

        #endregion

        #region State Management

        /// <summary>
        /// Updates the current active location (Village, Courtyard, Library, CrownHall).
        /// </summary>
        public void SetLocation(GameLocation newLocation)
        {
            if (currentLocation == newLocation) return;

            currentLocation = newLocation;
            Debug.Log($"[GameManager] Entered location: {currentLocation}");
            OnLocationChanged?.Invoke(currentLocation);
        }

        /// <summary>
        /// Transitions the active gameplay mode (Exploration, Combat, Dialogue, Shop).
        /// </summary>
        public void SetMode(GamePlayMode newMode)
        {
            if (currentMode == newMode) return;

            GamePlayMode oldMode = currentMode;
            currentMode = newMode;
            Debug.Log($"[GameManager] Switched gameplay mode: {oldMode} -> {currentMode}");
            OnPlayModeChanged?.Invoke(currentMode);
        }

        /// <summary>
        /// Marks a room or castle wing as cleared of enemies.
        /// </summary>
        public void NotifyRoomCleared(GameLocation location)
        {
            clearedLocations.Add(location);
            Debug.Log($"[GameManager] Wing cleared: {location}!");
            OnWingCleared?.Invoke(location);
        }

        /// <summary>
        /// Records the defeat of a major boss and triggers game victory if it is the Gargoyle King.
        /// </summary>
        public void NotifyBossDefeated(string bossID)
        {
            if (string.IsNullOrWhiteSpace(bossID)) return;

            defeatedBosses.Add(bossID);
            Debug.Log($"[GameManager] Boss Defeated: {bossID}!");
            OnBossDefeated?.Invoke(bossID);

            if (bossID.Equals("GargoyleKing", StringComparison.OrdinalIgnoreCase))
            {
                TriggerGameVictory();
            }
        }

        /// <summary>
        /// Concludes the game in complete campaign victory.
        /// </summary>
        public void TriggerGameVictory()
        {
            Debug.Log("[GameManager] CAMPAIGN VICTORY! The Castle of the D20 has been liberated from the stone curse!");
            SetMode(GamePlayMode.Exploration);
            OnGameWon?.Invoke();
        }

        /// <summary>
        /// Concludes the game in defeat.
        /// </summary>
        public void TriggerGameOver()
        {
            Debug.Log("[GameManager] GAME OVER! The hero has fallen in battle.");
            OnGameLost?.Invoke();
        }

        #endregion

        #region Event Handlers

        private void HandleCombatEnded(bool isVictory)
        {
            if (isVictory)
            {
                SetMode(GamePlayMode.Exploration);
            }
            else
            {
                TriggerGameOver();
            }
        }

        #endregion
    }
}
