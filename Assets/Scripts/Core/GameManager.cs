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
        CrownHall,

        /// <summary>The Dark Forest entrance and approach to Castle of Dice.</summary>
        Forest = 4,

        /// <summary>Central Hall / HALL: Safe Haven Hub with Rune Shrine.</summary>
        CastleHall = 5,

        /// <summary>Hidden Treasure Tower containing Signet Ring and Giant Elixir.</summary>
        Tower = 6,

        /// <summary>Underground wine cellar beneath Oakhaven village.</summary>
        Cellar = 7,

        /// <summary>Alias for CrownHall (Wing 3 Final Boss Chamber).</summary>
        ThroneRoom = CrownHall
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
        private static GameManager _instance;
        private static bool s_isQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetQuitFlag()
        {
            s_isQuitting = false;
            Application.quitting -= MarkQuitting;
            Application.quitting += MarkQuitting;
        }

        private static void MarkQuitting()
        {
            s_isQuitting = true;
        }

        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<GameManager>();
                    // Never spawn a new manager while the application is shutting down (OnDestroy callers)
                    if (_instance == null && !s_isQuitting)
                    {
                        GameObject managersObj = GameObject.Find("Managers") ?? new GameObject("Managers");
                        _instance = managersObj.AddComponent<GameManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

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

        // One-time world rewards already taken (chests, the Giant's Elixir), keyed by RewardKey()
        private readonly HashSet<string> claimedRewards = new HashSet<string>(StringComparer.Ordinal);

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

        /// <summary>
        /// Whether the Throne Room's sealed gate has opened: both wing bosses (the Cursed Commander and
        /// Shadow Mage Malakor) must fall before the hero may face the Gargoyle King.
        /// </summary>
        public bool IsThroneRoomOpen => IsCommanderDefeated && IsMalakorDefeated;

        /// <summary>
        /// Whether the hero may travel into <paramref name="location"/>. Only the Throne Room is sealed.
        /// The expedition map and the Great Hall's gate both follow this rule.
        /// </summary>
        public bool CanEnterLocation(GameLocation location) => location != GameLocation.CrownHall || IsThroneRoomOpen;

        /// <summary>Whether a specific dungeon wing or location has been cleared of hostiles.</summary>
        public bool IsWingCleared(GameLocation location) => clearedLocations.Contains(location);

        /// <summary>Whether the boss with the given identifier (e.g. "CursedCommander") has been defeated.</summary>
        public bool IsBossDefeated(string bossID) => !string.IsNullOrWhiteSpace(bossID) && defeatedBosses.Contains(bossID);

        /// <summary>Whether a one-time world reward (chest, elixir) has already been taken.</summary>
        public bool IsRewardClaimed(string rewardKey) => !string.IsNullOrEmpty(rewardKey) && claimedRewards.Contains(rewardKey);

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
            if (_instance != null && _instance != this)
            {
                ManagerDuplicates.Discard(this);
                return;
            }

            _instance = this;

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
        /// Alias for SetMode for compatibility with state-based calls.
        /// </summary>
        public void SetState(GamePlayMode state) => SetMode(state);

        /// <summary>
        /// Alias for SetMode for play mode transitions.
        /// </summary>
        public void SetPlayMode(GamePlayMode mode) => SetMode(mode);

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
        /// Records that a one-time world reward (chest, elixir) was taken so it stays taken
        /// when its zone scene is loaded again.
        /// </summary>
        public void MarkRewardClaimed(string rewardKey)
        {
            if (string.IsNullOrEmpty(rewardKey)) return;
            claimedRewards.Add(rewardKey);
        }

        /// <summary>
        /// Builds a stable key for a one-time reward from its scene and object name.
        /// </summary>
        public static string RewardKey(Component reward)
        {
            if (reward == null) return string.Empty;
            return reward.gameObject.scene.name + "/" + reward.gameObject.name;
        }

        /// <summary>
        /// True once anything in the campaign has happened (a boss, a cleared room, a taken reward). A New
        /// Adventure started after that reloads the village, so its rooms and chests start fresh.
        /// </summary>
        public bool HasCampaignProgress => defeatedBosses.Count > 0 || clearedLocations.Count > 0 || claimedRewards.Count > 0;

        /// <summary>
        /// Writes defeated bosses, cleared wings and claimed rewards for the save file.
        /// </summary>
        public void CaptureCampaignProgress(List<string> bosses, List<int> clearedWings, List<string> rewards = null)
        {
            bosses.Clear();
            bosses.AddRange(defeatedBosses);
            clearedWings.Clear();
            foreach (GameLocation location in clearedLocations)
            {
                clearedWings.Add((int)location);
            }

            if (rewards != null)
            {
                rewards.Clear();
                rewards.AddRange(claimedRewards);
            }
        }

        /// <summary>
        /// Restores defeated bosses, cleared wings and claimed rewards from a save without re-firing
        /// defeat/victory events. Null lists reset that part of the campaign (New Adventure).
        /// </summary>
        public void RestoreCampaignProgress(IReadOnlyList<string> bosses, IReadOnlyList<int> clearedWings, IReadOnlyList<string> rewards = null)
        {
            defeatedBosses.Clear();
            clearedLocations.Clear();
            claimedRewards.Clear();

            if (rewards != null)
            {
                for (int i = 0; i < rewards.Count; i++)
                {
                    if (!string.IsNullOrEmpty(rewards[i])) claimedRewards.Add(rewards[i]);
                }
            }

            if (bosses != null)
            {
                for (int i = 0; i < bosses.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(bosses[i])) defeatedBosses.Add(bosses[i]);
                }
            }

            if (clearedWings != null)
            {
                for (int i = 0; i < clearedWings.Count; i++)
                {
                    clearedLocations.Add((GameLocation)clearedWings[i]);
                }
            }

            // The main quest's step follows the defeated bosses
            Economy.QuestManager.Instance?.SyncMainQuest();
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
