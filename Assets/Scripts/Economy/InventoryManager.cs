using System;
using System.Collections.Generic;
using UnityEngine;
using CastleOfTheD20.Core;
using CastleOfTheD20.Data;
using CastleOfTheD20.Combat;

namespace CastleOfTheD20.Economy
{
    /// <summary>
    /// Persistent singleton managing the player's wealth, scrap metal reserves, and item inventory.
    /// Handles item acquisition, consumable usage, and equipment upgrade application.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        #region Singleton

        private static InventoryManager instance;

        public static InventoryManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<InventoryManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("InventoryManager");
                        instance = go.AddComponent<InventoryManager>();
                        Debug.Log("[InventoryManager] Auto-created InventoryManager GameObject in scene.");
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        #endregion

        #region Serialized Fields

        [Header("Starting Economy")]
        [Tooltip("Initial gold balance in the player's pouch.")]
        [SerializeField] private int currentGold = 100;

        [Tooltip("Initial quantity of raw scrap metal/ore collected from castle ruins.")]
        [SerializeField] private int scrapMetalCount = 5;

        [Header("Persistence")]
        [Tooltip("If true, retains instance across Unity scene transitions.")]
        [SerializeField] private bool persistAcrossScenes = true;

        #endregion

        #region Private State

        private readonly Dictionary<ItemSO, int> items = new Dictionary<ItemSO, int>();

        #endregion

        #region Public Properties

        /// <summary>Current available gold coins.</summary>
        public int CurrentGold => currentGold;

        /// <summary>Current quantity of scrap metal pieces.</summary>
        public int ScrapMetalCount => scrapMetalCount;

        /// <summary>Read-only dictionary of current inventory items and quantities.</summary>
        public IReadOnlyDictionary<ItemSO, int> Items => items;

        #endregion

        #region Events

        /// <summary>Fired when gold balance changes: (newGoldAmount).</summary>
        public static event Action<int> OnGoldChanged;

        /// <summary>Fired when scrap metal count changes: (newScrapCount).</summary>
        public static event Action<int> OnScrapMetalChanged;

        /// <summary>Fired when items are added, removed, or consumed.</summary>
        public static event Action OnInventoryChanged;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        #endregion

        #region Gold & Scrap Operations

        /// <summary>
        /// Adds gold coins to the player's total.
        /// </summary>
        public void AddGold(int amount)
        {
            if (amount <= 0) return;

            currentGold += amount;
            Debug.Log($"[InventoryManager] Gained {amount} Gold. Total Gold: {currentGold}");
            OnGoldChanged?.Invoke(currentGold);
        }

        /// <summary>
        /// Attempts to deduct gold coins from the player's total.
        /// Returns true if successful, false if insufficient funds.
        /// </summary>
        public bool RemoveGold(int amount)
        {
            if (amount <= 0) return true;

            if (currentGold >= amount)
            {
                currentGold -= amount;
                Debug.Log($"[InventoryManager] Spent {amount} Gold. Remaining Gold: {currentGold}");
                OnGoldChanged?.Invoke(currentGold);
                return true;
            }

            Debug.LogWarning($"[InventoryManager] Insufficient Gold! Required: {amount}, Current: {currentGold}");
            return false;
        }

        /// <summary>
        /// Adds scrap metal pieces to the inventory.
        /// </summary>
        public void AddScrapMetal(int amount)
        {
            if (amount <= 0) return;

            scrapMetalCount += amount;
            Debug.Log($"[InventoryManager] Gained {amount} Scrap Metal. Total: {scrapMetalCount}");
            OnScrapMetalChanged?.Invoke(scrapMetalCount);
        }

        /// <summary>
        /// Attempts to deduct scrap metal pieces from the inventory.
        /// </summary>
        public bool RemoveScrapMetal(int amount)
        {
            if (amount <= 0) return true;

            if (scrapMetalCount >= amount)
            {
                scrapMetalCount -= amount;
                Debug.Log($"[InventoryManager] Deducted {amount} Scrap Metal. Remaining: {scrapMetalCount}");
                OnScrapMetalChanged?.Invoke(scrapMetalCount);
                return true;
            }

            return false;
        }

        #endregion

        #region Item Management

        /// <summary>
        /// Adds an item to the inventory.
        /// </summary>
        public void AddItem(ItemSO item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return;

            if (item.ItemType == ItemType.ScrapMetal)
            {
                AddScrapMetal(quantity);
                return;
            }

            if (items.ContainsKey(item))
            {
                items[item] += quantity;
            }
            else
            {
                items[item] = quantity;
            }

            Debug.Log($"[InventoryManager] Added {quantity}x {item.ItemName} to inventory. Total: {items[item]}");
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Removes an item quantity from the inventory.
        /// </summary>
        public bool RemoveItem(ItemSO item, int quantity = 1)
        {
            if (item == null || quantity <= 0) return false;

            if (item.ItemType == ItemType.ScrapMetal)
            {
                return RemoveScrapMetal(quantity);
            }

            if (items.TryGetValue(item, out int currentCount) && currentCount >= quantity)
            {
                items[item] -= quantity;
                if (items[item] <= 0)
                {
                    items.Remove(item);
                }

                Debug.Log($"[InventoryManager] Removed {quantity}x {item.ItemName} from inventory.");
                OnInventoryChanged?.Invoke();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether the player possesses at least the specified quantity of an item.
        /// </summary>
        public bool HasItem(ItemSO item, int quantity = 1)
        {
            if (item == null) return false;

            if (item.ItemType == ItemType.ScrapMetal)
            {
                return scrapMetalCount >= quantity;
            }

            return items.TryGetValue(item, out int count) && count >= quantity;
        }

        /// <summary>
        /// Returns the owned quantity of a given item.
        /// </summary>
        public int GetItemCount(ItemSO item)
        {
            if (item == null) return 0;

            if (item.ItemType == ItemType.ScrapMetal)
            {
                return scrapMetalCount;
            }

            return items.TryGetValue(item, out int count) ? count : 0;
        }

        /// <summary>
        /// Uses an item from the inventory onto a target hero.
        /// Consumes potions to heal, applies permanent weapon/armor upgrades, and removes consumed item.
        /// </summary>
        public bool UseItem(ItemSO item, PlayerUnit target)
        {
            if (item == null || !HasItem(item, 1))
            {
                Debug.LogWarning("[InventoryManager] Cannot use item: Item not available in inventory.");
                return false;
            }

            if (target == null)
            {
                target = FindAnyObjectByType<PlayerUnit>();
            }

            if (target == null)
            {
                Debug.LogWarning("[InventoryManager] Cannot use item: No valid PlayerUnit target.");
                return false;
            }

            bool effectApplied = false;

            switch (item.ItemType)
            {
                case ItemType.Consumable:
                    // Standard potion: heals fixed amount or percentage of max HP
                    int healAmount = item.StatBonusValue > 1 ? item.StatBonusValue : Mathf.RoundToInt(target.MaxHP * 0.50f);
                    target.Heal(healAmount);
                    effectApplied = true;
                    break;

                case ItemType.WeaponUpgrade:
                    target.AddWeaponDamageBonus(item.StatBonusValue);
                    effectApplied = true;
                    break;

                case ItemType.ArmorUpgrade:
                    target.AddArmorClassBonus(item.StatBonusValue);
                    effectApplied = true;
                    break;

                case ItemType.QuestItem:
                case ItemType.ScrapMetal:
                default:
                    Debug.Log($"[InventoryManager] Item {item.ItemName} cannot be directly consumed.");
                    return false;
            }

            if (effectApplied)
            {
                if (item.IsConsumable)
                {
                    RemoveItem(item, 1);
                }
                return true;
            }

            return false;
        }

        #endregion
    }
}
