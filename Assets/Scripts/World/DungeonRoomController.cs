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
    /// Controls a dungeon room/wing encounter.
    /// Manages room states (Unexplored, CombatActive, Cleared), exit portcullis locks,
    /// hostile unit activation, secret chest revelations, and GameManager notifications.
    /// </summary>
    public class DungeonRoomController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Room Identity")]
        [Tooltip("The castle wing this room belongs to.")]
        [SerializeField] private GameLocation roomLocation = GameLocation.Courtyard;

        [Tooltip("Optional boss identifier if this is a boss chamber (e.g., 'CursedCommander', 'ShadowMageMalakor', 'GargoyleKing').")]
        [SerializeField] private string bossIdentifier = "";

        [Header("Obstacles & Barriers")]
        [Tooltip("Exit doors or iron gates enabled during combat to lock the player inside.")]
        [SerializeField] private List<GameObject> exitBarriers = new List<GameObject>();

        [Header("Combatants")]
        [Tooltip("Enemies stationed in this room, activated when encounter starts.")]
        [SerializeField] private List<CombatUnit> roomEnemies = new List<CombatUnit>();

        [Header("Rewards & Secrets")]
        [Tooltip("Loot chest or hidden doorway revealed upon room clearance.")]
        [SerializeField] private GameObject secretPassageOrChest;

        #endregion

        #region Private State

        private RoomState currentState = RoomState.Unexplored;

        #endregion

        #region Public Properties

        /// <summary>Current state of this room.</summary>
        public RoomState CurrentState => currentState;

        /// <summary>Location zone of this room.</summary>
        public GameLocation RoomLocation => roomLocation;

        /// <summary>Whether all hostiles in this room have been defeated.</summary>
        public bool IsCleared => currentState == RoomState.Cleared;

        #endregion

        #region Events

        /// <summary>Fired when room combat begins: (roomController).</summary>
        public static event Action<DungeonRoomController> OnRoomCombatStarted;

        /// <summary>Fired when room is cleared of all hostiles: (roomController).</summary>
        public static event Action<DungeonRoomController> OnRoomCleared;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Ensure exit barriers are unlocked initially
            SetBarriersLocked(false);

            if (secretPassageOrChest != null)
            {
                secretPassageOrChest.SetActive(false);
            }

            TurnManager.OnCombatEnded += HandleCombatEnded;
        }

        private void OnDestroy()
        {
            TurnManager.OnCombatEnded -= HandleCombatEnded;
        }

        #endregion

        #region Encounter Management

        /// <summary>
        /// Triggers combat inside this chamber: locks exit barriers, activates enemies, and notifies TurnManager.
        /// </summary>
        public void TriggerEncounter()
        {
            if (currentState == RoomState.Cleared || currentState == RoomState.CombatActive) return;

            currentState = RoomState.CombatActive;
            Debug.Log($"[DungeonRoomController] Encounter triggered in {roomLocation}! Locking chamber doors.");

            // 1. Lock chamber barriers
            SetBarriersLocked(true);

            // 2. Activate hostile units
            List<CombatUnit> activeParticipants = new List<CombatUnit>();

            PlayerUnit player = FindAnyObjectByType<PlayerUnit>();
            if (player != null && player.IsAlive)
            {
                activeParticipants.Add(player);
            }

            foreach (var enemy in roomEnemies)
            {
                if (enemy != null)
                {
                    enemy.gameObject.SetActive(true);
                    if (enemy.IsAlive)
                    {
                        activeParticipants.Add(enemy);
                    }
                }
            }

            // 3. Inform GameManager of combat mode
            GameManager.Instance?.SetLocation(roomLocation);
            GameManager.Instance?.SetMode(GamePlayMode.Combat);

            // 4. Start combat loop
            TurnManager.Instance?.StartCombat(activeParticipants);

            OnRoomCombatStarted?.Invoke(this);
        }

        /// <summary>
        /// Unlocks the chamber, reveals loot, and notifies GameManager.
        /// </summary>
        public void ClearRoom()
        {
            currentState = RoomState.Cleared;
            Debug.Log($"[DungeonRoomController] {roomLocation} CLEARED! Unlocking exit doors.");

            // Unlock barriers
            SetBarriersLocked(false);

            // Reveal hidden treasure chest or secret doorway
            if (secretPassageOrChest != null)
            {
                secretPassageOrChest.SetActive(true);
                Debug.Log("[DungeonRoomController] Hidden passageway or chest has been revealed!");
            }

            // Record with GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.NotifyRoomCleared(roomLocation);

                if (!string.IsNullOrEmpty(bossIdentifier))
                {
                    GameManager.Instance.NotifyBossDefeated(bossIdentifier);
                }
            }

            OnRoomCleared?.Invoke(this);
        }

        private void SetBarriersLocked(bool locked)
        {
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
            if (currentState == RoomState.CombatActive && isVictory)
            {
                ClearRoom();
            }
        }

        #endregion

        #region Trigger Volumes

        private void OnTriggerEnter(Collider other)
        {
            if (currentState == RoomState.Unexplored && other.GetComponent<PlayerUnit>() != null)
            {
                TriggerEncounter();
            }
        }

        #endregion
    }
}
