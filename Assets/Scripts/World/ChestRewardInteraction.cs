using System;
using UnityEngine;
using CastleOfTheD20.Combat;
using CastleOfTheD20.Data;
using CastleOfTheD20.Economy;

namespace CastleOfTheD20.World
{
    /// <summary>
    /// Interactive treasure chest that awards gold and optional items when opened by the player.
    /// Manages open/closed states, visual feedback (lid rotation), and updates interaction prompts.
    /// </summary>
    [SelectionBase]
    public class ChestRewardInteraction : Interactable
    {
        #region Serialized Fields

        [Header("Reward Settings")]
        [Tooltip("Gold awarded to the player upon opening the chest.")]
        [Min(0)]
        [SerializeField] private int goldReward = 30;

        [Tooltip("Optional item awarded along with the gold.")]
        [SerializeField] private ItemSO itemReward;

        [Header("Chest State")]
        [Tooltip("Whether the chest has already been opened and looted.")]
        [SerializeField] private bool isOpen = false;

        [Tooltip("HUD prompt text displayed when chest is unopened.")]
        [SerializeField] private string unopenedPrompt = "Open Chest";

        [Tooltip("HUD prompt text displayed when chest has already been opened.")]
        [SerializeField] private string openedPrompt = "Opened (Empty)";

        [Header("Visual & Audio Feedback")]
        [Tooltip("Optional lid transform to rotate when the chest is opened.")]
        [SerializeField] private Transform chestLid;

        [Tooltip("Local Euler rotation for lid when opened.")]
        [SerializeField] private Vector3 openLidRotation = new Vector3(-65f, 0f, 0f);

        [Tooltip("Optional audio clip played upon opening.")]
        [SerializeField] private AudioClip openSound;

        #endregion

        #region Public Properties

        /// <summary>Amount of gold awarded upon opening.</summary>
        public int GoldReward
        {
            get => goldReward;
            set => goldReward = Mathf.Max(0, value);
        }

        /// <summary>Optional item rewarded upon opening.</summary>
        public ItemSO ItemReward
        {
            get => itemReward;
            set => itemReward = value;
        }

        /// <summary>Whether this chest has been opened.</summary>
        public bool IsOpen => isOpen;

        #endregion

        #region Events

        /// <summary>Fired when this specific chest is opened: (goldAwarded).</summary>
        public event Action<int> OnChestOpened;

        /// <summary>Global event fired when any reward chest is opened: (chest, goldAwarded).</summary>
        public static event Action<ChestRewardInteraction, int> OnAnyChestOpened;

        #endregion

        #region Unity Lifecycle

        private void Reset()
        {
            promptMessage = unopenedPrompt;
            interactionRadius = 3.0f;
            goldReward = 30;
            EnsureChestCollider();
        }

        protected override void Awake()
        {
            EnsureChestCollider();
            base.Awake();

            promptMessage = isOpen ? openedPrompt : unopenedPrompt;

            if (chestLid == null)
            {
                chestLid = FindChildLid(transform);
            }

            if (isOpen && chestLid != null)
            {
                chestLid.localEulerAngles = openLidRotation;
            }
        }

        #endregion

        #region Interaction

        /// <summary>
        /// Handles player interaction: awards gold, distributes items, rotates the lid, and disables repeat claims.
        /// </summary>
        public override void Interact(PlayerUnit player)
        {
            if (isOpen)
            {
                Debug.Log($"[ChestRewardInteraction] '{name}' has already been opened and looted.");
                return;
            }

            isOpen = true;
            promptMessage = openedPrompt;

            // Visual feedback: rotate lid if available
            if (chestLid != null)
            {
                chestLid.localEulerAngles = openLidRotation;
            }

            // Audio feedback
            if (openSound != null)
            {
                AudioSource.PlayClipAtPoint(openSound, transform.position);
            }

            // Distribute gold reward
            if (goldReward > 0)
            {
                InventoryManager inventory = InventoryManager.Instance;
                if (inventory != null)
                {
                    inventory.AddGold(goldReward);
                }
                else
                {
                    Debug.LogWarning($"[ChestRewardInteraction] InventoryManager.Instance is null! Could not award {goldReward} Gold.");
                }
            }

            // Distribute item reward
            if (itemReward != null)
            {
                InventoryManager inventory = InventoryManager.Instance;
                if (inventory != null)
                {
                    inventory.AddItem(itemReward, 1);
                    Debug.Log($"[ChestRewardInteraction] Found '{itemReward.ItemName}' inside '{name}'!");
                }
            }

            string playerName = player != null ? player.UnitName : "Player";
            Debug.Log($"[ChestRewardInteraction] {playerName} opened '{name}' and collected {goldReward} Gold!");

            OnChestOpened?.Invoke(goldReward);
            OnAnyChestOpened?.Invoke(this, goldReward);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Ensures a properly configured BoxCollider exists on this chest for mouse raycasts and physical presence.
        /// </summary>
        public void EnsureChestCollider()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                box = gameObject.AddComponent<BoxCollider>();
            }

            box.center = new Vector3(0f, 0.45f, 0f);
            box.size = new Vector3(1.4f, 0.9f, 1.0f);
        }

        private Transform FindChildLid(Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (child.name.IndexOf("lid", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return child;
                }

                Transform recursive = FindChildLid(child);
                if (recursive != null) return recursive;
            }
            return null;
        }

        #endregion
    }
}
