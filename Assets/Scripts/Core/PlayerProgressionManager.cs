using System;
using UnityEngine;
using CastleOfTheD20.UI;
using CastleOfTheD20.Combat;
using CastleOfTheD20.World;
using CastleOfTheD20.Data;

namespace CastleOfTheD20.Core
{
    /// <summary>
    /// Master supervisor for hero milestone progression (Levels 1, 2, and 3).
    /// Replaces XP-based progression with story/encounter milestones:
    /// - Level 1 (Starting Level): Default in Oakhaven Village & Cellar.
    /// - Level 2 (Castle Veteran): Boss 1 (Cursed Commander) defeated OR Rogue unlocks Nature Path secret door.
    /// - Level 3 (Arcane Crusher - Max Level): Boss 2 (Shadow Mage Malakor) defeated in Library.
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
        [Range(1, 3)]
        [SerializeField] private int currentLevel = 1;

        #endregion

        #region Public Properties

        /// <summary>Current hero milestone level (1, 2, or 3).</summary>
        public int CurrentLevel => currentLevel;

        /// <summary>Progression ScriptableObject reference.</summary>
        public PlayerDataSO PlayerData => playerData;

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
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadProgression();
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
            LockpickInteraction.OnLockpickAttempt += HandleLockpickAttempt;
        }

        private void UnsubscribeFromGameEvents()
        {
            GameManager.OnBossDefeated -= HandleBossDefeated;
            LockpickInteraction.OnLockpickAttempt -= HandleLockpickAttempt;
        }

        #endregion

        #region Milestone Triggers

        /// <summary>
        /// Boss Defeat Milestone Handler:
        /// - "CursedCommander" -> Level 2
        /// - "ShadowMageMalakor" -> Level 3
        /// </summary>
        private void HandleBossDefeated(string bossID)
        {
            if (string.IsNullOrWhiteSpace(bossID)) return;

            Debug.Log($"[PlayerProgressionManager] Evaluating milestone trigger for defeated boss: {bossID}");

            if (bossID.Equals("CursedCommander", StringComparison.OrdinalIgnoreCase))
            {
                if (currentLevel < 2)
                {
                    AdvanceToMilestone(2, "Cursed Commander defeated");
                }
            }
            else if (bossID.Equals("ShadowMageMalakor", StringComparison.OrdinalIgnoreCase))
            {
                if (currentLevel < 3)
                {
                    AdvanceToMilestone(3, "Shadow Mage Malakor defeated");
                }
            }
        }

        /// <summary>
        /// Lockpick Secret Route Milestone Handler:
        /// Rogue picking lock with secret passage / Nature Path unlocks Level 2.
        /// </summary>
        private void HandleLockpickAttempt(DiceResult result, bool isSuccess)
        {
            if (!isSuccess || currentLevel >= 2) return;

            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            if (player != null && player.CharacterClass != null && player.CharacterClass.ClassType == CharacterClassType.Rogue)
            {
                // Verify if this lockpick is linked to a secret path or nature door
                LockpickInteraction[] locks = FindObjectsByType<LockpickInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var lk in locks)
                {
                    if (!lk.IsLocked && lk.name.ToLowerInvariant().Contains("nature") || lk.name.ToLowerInvariant().Contains("secret") || lk.name.ToLowerInvariant().Contains("path"))
                    {
                        AdvanceToMilestone(2, "Forest Path Secret Route Unlocked (Rogue)");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Directly triggers Level 2 milestone for secret path unlocking.
        /// </summary>
        public void NotifySecretPathOpened()
        {
            if (currentLevel < 2)
            {
                AdvanceToMilestone(2, "Forest Path Secret Route Picked");
            }
        }

        /// <summary>
        /// Advances player to target milestone level, triggering the modal window.
        /// </summary>
        public void AdvanceToMilestone(int targetLevel, string reason = "")
        {
            if (targetLevel <= currentLevel)
            {
                Debug.Log($"[PlayerProgressionManager] Player already at level {currentLevel}, ignoring milestone {targetLevel}.");
                return;
            }

            int oldLevel = currentLevel;
            currentLevel = Mathf.Clamp(targetLevel, 1, 3);

            Debug.Log($"[PlayerProgressionManager] MILESTONE REACHED! Level {oldLevel} -> {currentLevel} ({reason}).");

            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            if (player != null)
            {
                player.Level = currentLevel;
            }

            if (playerData != null)
            {
                playerData.CurrentLevel = currentLevel;
            }

            OnMilestoneReached?.Invoke(oldLevel, currentLevel);

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

        public void LoadProgression()
        {
            if (playerData == null)
            {
                playerData = Resources.Load<PlayerDataSO>("PlayerData");
            }

            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            PlayerSaveData save = SaveSystem.LoadGame(playerData, player);

            if (save != null)
            {
                currentLevel = save.currentLevel;
            }
            else if (playerData != null)
            {
                currentLevel = playerData.CurrentLevel;
            }
            else
            {
                currentLevel = 1;
            }
        }

        public void ApplyProgressionToCurrentPlayer()
        {
            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            if (player != null)
            {
                player.Level = currentLevel;
                if (playerData != null)
                {
                    playerData.ApplyToPlayer(player);
                }
            }
        }

        #endregion
    }
}
