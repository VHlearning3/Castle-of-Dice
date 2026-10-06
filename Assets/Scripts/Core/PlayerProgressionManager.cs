using System;
using UnityEngine;
using CastleOfTheD20.UI;
using CastleOfTheD20.Combat;
using CastleOfTheD20.World;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Hero levels 1..5 from four milestones (Vili, 2026-10-06). Each gives one level, once, in any order:
    /// - the first battle won (the cellar rats, the Forest Path, wherever it comes first);
    /// - three battles won in all, or the Cursed Commander or the Shadow Witch dealt with;
    /// - the hero's own quest (ClassQuests);
    /// - the Treasure Tower challenge (TowerChallenge).
    /// A battle is a cleared encounter room (a village fist fight is not one). The Gargoyle King gives no level.
    /// The milestones are story flags, so they are saved with the adventure.
    /// </summary>
    public class PlayerProgressionManager : MonoBehaviour
    {
        #region Singleton

        public static PlayerProgressionManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("Progression Data Asset")]
        [Tooltip("Optional ScriptableObject asset holding hero progression.")]
        [SerializeField] private PlayerDataSO playerData;

        [Header("Current Milestone")]
        [Tooltip("Current milestone level (1..3).")]
        [Range(1, 5)]
        [SerializeField] private int currentLevel = 1;

        #endregion

        #region Public Properties

        /// <summary>Current hero milestone level (1, 2, or 3).</summary>
        public int CurrentLevel => currentLevel;

        /// <summary>Progression store shared across scenes (the assigned asset, or the session instance).</summary>
        public PlayerDataSO PlayerData => PlayerDataSO.Session;

        #endregion

        #region Events

        /// <summary>Fired when milestone level increases (oldLevel, newLevel).</summary>
        public static event Action<int, int> OnMilestoneReached;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                ManagerDuplicates.Discard(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (playerData != null)
            {
                PlayerDataSO.Session = playerData;
            }

            // The save is loaded only when the player picks Continue in the main menu (critical review A9),
            // so New Adventure never starts with the previous run's progress applied.
        }

        private void Start()
        {
            SubscribeToGameEvents();
            ApplyProgressionToCurrentPlayer();
        }

        private void OnDestroy()
        {
            UnsubscribeFromGameEvents();
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToGameEvents()
        {
            GameManager.OnBossDefeated += HandleBossDefeated;
            DungeonRoomController.OnRoomCleared += HandleRoomCleared;
        }

        private void UnsubscribeFromGameEvents()
        {
            GameManager.OnBossDefeated -= HandleBossDefeated;
            DungeonRoomController.OnRoomCleared -= HandleRoomCleared;
        }

        #endregion

        #region Milestone Triggers

        /// <summary>Story flag of the "first battle won" milestone.</summary>
        public const string FirstVictoryFlag = "LevelMilestone_FirstVictory";

        /// <summary>Story flag of the "three battles won, or a castle boss dealt with" milestone.</summary>
        public const string SeasonedFlag = "LevelMilestone_Seasoned";

        /// <summary>Prefix of the story flags that record each battle won (one per encounter room).</summary>
        public const string BattleWonPrefix = "BattleWon:";

        /// <summary>Battles that give the second milestone.</summary>
        public const int SeasonedBattles = 3;

        /// <summary>Battles won so far in this adventure (each encounter counts once).</summary>
        public static int BattlesWon
        {
            get
            {
                int count = 0;
                foreach (string flag in StoryFlags.All)
                {
                    if (flag.StartsWith(BattleWonPrefix, StringComparison.OrdinalIgnoreCase)) count++;
                }
                return count;
            }
        }

        /// <summary>The flag a won encounter leaves: its scene and object name, so every room counts once.</summary>
        public static string BattleWonFlag(DungeonRoomController room)
        {
            return BattleWonPrefix + room.gameObject.scene.name + "/" + room.name;
        }

        /// <summary>A cleared encounter room is a battle won: the first gives a level, the third another.</summary>
        public void HandleRoomCleared(DungeonRoomController room)
        {
            if (room == null) return;
            StoryFlags.Set(BattleWonFlag(room));

            int won = BattlesWon;
            GrantMilestone(FirstVictoryFlag, "First battle won");
            if (won >= SeasonedBattles) GrantMilestone(SeasonedFlag, $"{SeasonedBattles} battles won");
        }

        /// <summary>The Cursed Commander or the Shadow Witch (beaten, released or spared) gives the second milestone.</summary>
        public void HandleBossDefeated(string bossID)
        {
            if (string.IsNullOrWhiteSpace(bossID)) return;

            if (bossID.Equals("CursedCommander", StringComparison.OrdinalIgnoreCase))
            {
                GrantMilestone(SeasonedFlag, "Cursed Commander overcome");
            }
            else if (bossID.Equals("ShadowMageMalakor", StringComparison.OrdinalIgnoreCase))
            {
                GrantMilestone(SeasonedFlag, "Shadow Witch overcome");
            }
        }

        /// <summary>Gives one level for a milestone the first time it is reached.</summary>
        public bool GrantMilestone(string flag, string reason)
        {
            if (StoryFlags.Has(flag)) return false;
            StoryFlags.Set(flag);
            GrantBonusLevel(reason);
            return true;
        }

        /// <summary>
        /// One more level (up to 5). Used by every milestone; the hero's own quest and the Tower challenge call
        /// it directly. Level 4 brings the class's fifth ability.
        /// </summary>
        public void GrantBonusLevel(string reason, bool showModal = true)
        {
            if (currentLevel >= PlayerUnit.MaxLevel) return;
            AdvanceToMilestone(currentLevel + 1, reason, showModal);
        }

        /// <summary>
        /// Advances player to target milestone level, triggering the modal window.
        /// </summary>
        public void AdvanceToMilestone(int targetLevel, string reason = "", bool showModal = true)
        {
            if (targetLevel <= currentLevel)
            {
                Debug.Log($"[PlayerProgressionManager] Player already at level {currentLevel}, ignoring milestone {targetLevel}.");
                return;
            }

            int oldLevel = currentLevel;
            currentLevel = Mathf.Clamp(targetLevel, 1, PlayerUnit.MaxLevel);

            Debug.Log($"[PlayerProgressionManager] MILESTONE REACHED! Level {oldLevel} -> {currentLevel} ({reason}).");

            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            if (player != null)
            {
                player.Level = currentLevel;
            }

            PlayerData.CurrentLevel = currentLevel;

            OnMilestoneReached?.Invoke(oldLevel, currentLevel);

            if (!showModal)
            {
                PlayerData.SyncFromPlayer(player);
                return;
            }

            // Open Level-Up UI Modal
            if (LevelUpUIController.Instance != null)
            {
                LevelUpUIController.Instance.ShowLevelUpModal(currentLevel, player);
            }
            else
            {
                LevelUpUIController controller = FindAnyObjectByType<LevelUpUIController>(FindObjectsInactive.Include);
                if (controller != null)
                {
                    controller.ShowLevelUpModal(currentLevel, player);
                }
                else
                {
                    Debug.LogWarning("[PlayerProgressionManager] No LevelUpUIController found in scene to display modal.");
                }
            }
        }

        #endregion

        #region Save & Load

        /// <summary>
        /// Loads the saved adventure into the session (Continue). Returns the save, or null when there is none.
        /// </summary>
        public PlayerSaveData LoadProgression()
        {
            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            PlayerSaveData save = SaveSystem.LoadGame(PlayerData, player);
            currentLevel = save != null ? save.currentLevel : PlayerData.CurrentLevel;
            return save;
        }

        /// <summary>
        /// Starts a fresh adventure: clears session progression and deletes the old save, so Continue can
        /// never bring the previous run back.
        /// </summary>
        public void ResetForNewGame()
        {
            CharacterClassSO keepClass = PlayerData.SelectedClass;
            PlayerData.ResetData();
            PlayerData.SelectedClass = keepClass;
            currentLevel = 1;

            SaveSystem.ClearSave();
            StoryFlags.Clear();

            if (Economy.InventoryManager.Instance != null)
            {
                Economy.InventoryManager.Instance.ResetToStartingValues();
            }

            if (Economy.QuestManager.Instance != null)
            {
                Economy.QuestManager.Instance.ResetAllQuests();
            }

            AdventureStats.Reset();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestoreCampaignProgress(null, null);
            }
        }

        public void ApplyProgressionToCurrentPlayer()
        {
            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            if (player != null)
            {
                PlayerData.ApplyToPlayer(player);
            }
        }

        #endregion
    }
}
