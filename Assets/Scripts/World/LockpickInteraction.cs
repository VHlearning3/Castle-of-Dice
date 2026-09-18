using System;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Implements lockpicked chests, locked iron gates, and secret passageways.
    /// Initiates a D20 lockpicking skill check (default DC 13).
    /// Rogue characters benefit from Advantage (roll twice, take higher).
    /// Upon success: grants gold, distributes loot to InventoryManager, and reveals secret doorways.
    /// Upon failure: retains lock or springs a trap dealing damage.
    /// </summary>
    public class LockpickInteraction : Interactable
    {
        #region Serialized Fields

        [Header("Lockpick Difficulty")]
        [Tooltip("Target Difficulty Class (DC) to pick the lock (standard is DC 13).")]
        [Range(1, 30)]
        [SerializeField] private int lockpickDC = 13;

        [Header("Loot & Rewards")]
        [Tooltip("Gold found inside the locked chest upon success.")]
        [Min(0)]
        [SerializeField] private int rewardGold = 35;

        [Tooltip("Optional item awarded upon unlocking (e.g., Reroll Rune, potion, upgrade).")]
        [SerializeField] private ItemSO rewardItem;

        [Header("Secret Passage / Door")]
        [Tooltip("Optional GameObject activated or opened upon success (e.g., hidden door revealed).")]
        [SerializeField] private GameObject hiddenPathObject;

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

        /// <summary>Fired when a lockpicking attempt completes: (result, isSuccess).</summary>
        public static event Action<DiceResult, bool> OnLockpickAttempt;

        /// <summary>Fired when the lock is successfully opened.</summary>
        public event Action OnUnlocked;

        /// <summary>Fired when a trap is sprung upon failure.</summary>
        public event Action<int> OnTrapSprung;

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            promptMessage = "Pick Lock (DC 13)";
            interactionRadius = 2.5f;
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

            int bonus = player.PrimaryAttributeBonus;

            // Rogues possess the lockpicking expertise trait, granting Advantage on lockpicking
            AdvantageType advantage = AdvantageType.None;
            if (player.CharacterClass != null && player.CharacterClass.ClassType == CharacterClassType.Rogue)
            {
                advantage = AdvantageType.Advantage;
                Debug.Log("[LockpickInteraction] Rogue expertise grants Advantage on this lockpicking check!");
            }

            // Roll D20 vs lock DC
            DiceResult result = DiceSystem.RollD20(bonus, lockpickDC, advantage);
            Debug.Log($"[LockpickInteraction] {player.UnitName} attempts lockpick vs DC {lockpickDC}: {result}");

            OnLockpickAttempt?.Invoke(result, result.isSuccess);

            if (result.isSuccess)
            {
                HandleLockpickSuccess(player);
            }
            else
            {
                HandleLockpickFailure(player);
            }
        }

        private void HandleLockpickSuccess(PlayerUnit player)
        {
            isLocked = false;
            promptMessage = "Opened";

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
            }

            OnUnlocked?.Invoke();
        }

        private void HandleLockpickFailure(PlayerUnit player)
        {
            Debug.Log("[LockpickInteraction] FAILURE! The lock remains shut.");

            if (hasTrap && trapDamage > 0)
            {
                Debug.LogWarning($"[LockpickInteraction] TRAP TRIGGERED! Dart trap deals {trapDamage} damage to {player.UnitName}!");
                player.TakeDamage(trapDamage);
                OnTrapSprung?.Invoke(trapDamage);
            }
        }

        #endregion
    }
}
