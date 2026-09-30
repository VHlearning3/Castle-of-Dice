using System;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Economy;
using CastleOfTheD20.UI;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Implements lockpicked chests, locked iron gates, and secret passageways.
    /// Only the Rogue can pick locks: it opens the lockpick timing minigame (LockpickMinigameUI),
    /// whose difficulty comes from the lock's DC. Other classes get a "Can't lockpick" popup.
    /// Upon success: grants gold, distributes loot to InventoryManager, and reveals secret doorways.
    /// Upon failure (too many slips): the lock stays shut and may spring a trap dealing damage.
    /// </summary>
    public class LockpickInteraction : Interactable
    {
        #region Serialized Fields

        [Header("Lockpick Difficulty")]
        [Tooltip("Lock difficulty (standard is DC 13). Higher DC = narrower gold zone and a faster pick in the minigame.")]
        [Range(1, 30)]
        [SerializeField] private int lockpickDC = 13;

        [Tooltip("Pins the Rogue must set to open the lock.")]
        [Range(1, 6)]
        [SerializeField] private int pinCount = LockpickMinigame.DefaultPinCount;

        [Tooltip("Missed presses allowed before the attempt fails (and the trap springs).")]
        [Range(1, 5)]
        [SerializeField] private int maxSlips = LockpickMinigame.DefaultMaxSlips;

        [Header("Loot & Rewards")]
        [Tooltip("Gold found inside the locked chest upon success.")]
        [Min(0)]
        [SerializeField] private int rewardGold = 35;

        [Tooltip("Optional item awarded upon unlocking (e.g., Reroll Rune, potion, upgrade).")]
        [SerializeField] private ItemSO rewardItem;

        [Header("Secret Passage / Door")]
        [Tooltip("Optional GameObject activated or opened upon success (e.g., hidden door revealed).")]
        [SerializeField] private GameObject hiddenPathObject;

        [Header("Chest Lid (optional)")]
        [Tooltip("Lid transform rotated open when the lock is picked (chests only).")]
        [SerializeField] private Transform chestLid;

        [Tooltip("Local Euler rotation for the lid when opened.")]
        [SerializeField] private Vector3 openLidRotation = new Vector3(-65f, 0f, 0f);

        [Header("Trap Settings")]
        [Tooltip("If true, a failed check springs a trap.")]
        [SerializeField] private bool hasTrap = true;

        [Tooltip("Damage dealt to the player when a trap is triggered.")]
        [SerializeField] private int trapDamage = 4;

        [Header("State")]
        [Tooltip("Whether this lock is currently intact and locked.")]
        [SerializeField] private bool isLocked = true;

        #endregion

        #region Public Properties

        /// <summary>Lock Difficulty Class.</summary>
        public int LockpickDC => lockpickDC;

        /// <summary>Whether the chest or door remains locked.</summary>
        public bool IsLocked => isLocked;

        #endregion

        #region Events

        /// <summary>Popup shown when a non-Rogue tries to pick a lock.</summary>
        public const string CannotLockpickMessage = "Can't lockpick";

        /// <summary>Fired when a lockpicking minigame ends in success or failure (not when the Rogue steps away): (isSuccess).</summary>
        public static event Action<bool> OnLockpickAttempt;

        /// <summary>Fired when the lock is successfully opened.</summary>
        public event Action OnUnlocked;

        /// <summary>Fired when a trap is sprung upon failure.</summary>
        public event Action<int> OnTrapSprung;

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            promptMessage = "Pick Lock";
            interactionRadius = 2.5f;
        }

        protected override void Awake()
        {
            base.Awake();

            // Zone scenes reload on every visit: a picked lock stays open (and its loot stays taken)
            if (isLocked && GameManager.Instance != null && GameManager.Instance.IsRewardClaimed(GameManager.RewardKey(this)))
            {
                isLocked = false;
                promptMessage = "Opened";
                OpenLid();
                if (hiddenPathObject != null)
                {
                    hiddenPathObject.SetActive(true);
                }
            }
        }

        #endregion

        #region Interaction

        public override void Interact(PlayerUnit player)
        {
            if (!isLocked)
            {
                Debug.Log("[LockpickInteraction] The lock has already been opened.");
                return;
            }

            if (player == null)
            {
                Debug.LogWarning("[LockpickInteraction] Cannot pick lock without a PlayerUnit.");
                return;
            }

            // Tiirikointi is the Rogue's exploration passive: nobody else can even try
            if (!CanPickLocks(player))
            {
                Debug.Log($"[LockpickInteraction] {player.UnitName} can't pick locks (Rogue only).");
                LockpickMinigameUI.ShowToast(CannotLockpickMessage);
                return;
            }

            if (LockpickMinigameUI.IsOpen) return;

            LockpickMinigame minigame = new LockpickMinigame(lockpickDC, pinCount, maxSlips);
            Debug.Log($"[LockpickInteraction] {player.UnitName} starts picking '{name}' (DC {lockpickDC}, {pinCount} pins, {maxSlips} slips allowed).");
            string title = chestLid != null ? "Pick the Chest Lock" : "Pick the Lock";
            LockpickMinigameUI.Open(minigame, title, outcome => HandleMinigameFinished(player, outcome));
        }

        /// <summary>Only the Rogue (Varjo-Corvo) can pick locks.</summary>
        public static bool CanPickLocks(PlayerUnit player)
        {
            return player != null && player.CharacterClass != null && player.CharacterClass.ClassType == CharacterClassType.Rogue;
        }

        private void HandleMinigameFinished(PlayerUnit player, LockpickOutcome outcome)
        {
            if (outcome == LockpickOutcome.Cancelled || !isLocked)
            {
                return;
            }

            bool success = outcome == LockpickOutcome.Unlocked;
            if (success)
            {
                HandleLockpickSuccess(player);
            }
            else if (player != null)
            {
                HandleLockpickFailure(player);
            }

            // Raised after the lock state changes so listeners see the opened lock
            OnLockpickAttempt?.Invoke(success);
        }

        private void HandleLockpickSuccess(PlayerUnit player)
        {
            isLocked = false;
            promptMessage = "Opened";
            GameManager.Instance?.MarkRewardClaimed(GameManager.RewardKey(this));
            OpenLid();

            Debug.Log($"[LockpickInteraction] SUCCESS! Lock picked. Gained {rewardGold} Gold.");

            // Distribute rewards
            InventoryManager inventory = InventoryManager.Instance;
            if (inventory != null)
            {
                if (rewardGold > 0)
                {
                    inventory.AddGold(rewardGold);
                }

                if (rewardItem != null)
                {
                    inventory.AddItem(rewardItem, 1);
                    Debug.Log($"[LockpickInteraction] Found {rewardItem.ItemName} in chest!");
                }
            }

            // Reveal secret passage
            if (hiddenPathObject != null)
            {
                hiddenPathObject.SetActive(true);
                Debug.Log("[LockpickInteraction] A secret passage has been revealed!");

                if (PlayerProgressionManager.Instance != null)
                {
                    PlayerProgressionManager.Instance.NotifySecretPathOpened();
                }
            }

            OnUnlocked?.Invoke();
        }

        private void HandleLockpickFailure(PlayerUnit player)
        {
            Debug.Log("[LockpickInteraction] FAILURE! The lock remains shut.");

            if (hasTrap && trapDamage > 0)
            {
                Debug.Log($"[LockpickInteraction] TRAP TRIGGERED! Dart trap deals {trapDamage} damage to {player.UnitName}!");
                player.TakeDamage(trapDamage);
                OnTrapSprung?.Invoke(trapDamage);
            }
        }

        private void OpenLid()
        {
            if (chestLid != null)
            {
                chestLid.localEulerAngles = openLidRotation;
            }
        }

        #endregion
    }
}
